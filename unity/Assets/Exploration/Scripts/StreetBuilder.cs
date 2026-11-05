using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration
{
    /// <summary>建好的街區中，探索流程需要用到的物件。</summary>
    public sealed class StreetLayout
    {
        public Transform Root;
        public Vector3 Spawn;
        public float SpawnYaw;
        /// <summary>戰場原點：戰鬥以此為原點擺放玩家、敵人與戰術鏡頭（與 CombatLab3D 的座標相同）。</summary>
        public Transform EncounterAnchor;
        public Transform EncounterRing;
        public Material EncounterRingMat;
        public Light RiftLight;
        public Transform Anomaly;
        public Material AnomalyMat;
        public Light AnomalyLight;
        public readonly List<Transform> Floating = new List<Transform>();
        public readonly List<Vector3> FloatingHome = new List<Vector3>();
    }

    /// <summary>
    /// 約 40×40 公尺的英倫風異域街區 Graybox（只用 Primitive 與 Standard 材質）。
    /// 座標：+z 為北。南端出生點 → 主街 → 西側小巷（異常物件）→ 北側被裂痕侵蝕的小廣場（遭遇敵人）。
    /// 建築、牆、車輛、障礙物保留碰撞體；裝飾（窗、裂縫、光暈）不保留。
    /// </summary>
    public static class StreetBuilder
    {
        public const float HalfSize = 20f;
        public static readonly Vector3 SpawnPoint = new Vector3(0f, 0.05f, -17f);
        /// <summary>戰場原點（北側廣場）。戰術鏡頭在原點後方約 11.5 m，落在主街上，不會被建築擋住。</summary>
        public static readonly Vector3 EncounterAnchorPosition = new Vector3(0f, 0f, 10f);
        public static readonly Vector3 AnomalyPosition = new Vector3(-15.6f, 0f, -7f);

        public static StreetLayout Build(Transform parent)
        {
            var L = new StreetLayout();
            var root = ArenaArt.Group("Street", parent, Vector3.zero);
            L.Root = root;
            L.Spawn = SpawnPoint;
            L.SpawnYaw = 0f;
            var rng = new System.Random(4417);
            System.Func<float, float, float> R = (a, b) => a + (float)rng.NextDouble() * (b - a);

            var asphalt = ArenaArt.Mat(new Color(0.13f, 0.13f, 0.15f), 0.4f);
            var pavement = ArenaArt.Mat(new Color(0.27f, 0.27f, 0.28f), 0.2f);
            var cobble = ArenaArt.Mat(new Color(0.24f, 0.23f, 0.24f), 0.25f);
            var brick = ArenaArt.Mat(new Color(0.29f, 0.18f, 0.14f), 0.05f);
            var brickDark = ArenaArt.Mat(new Color(0.18f, 0.13f, 0.13f), 0.05f);
            var stone = ArenaArt.Mat(new Color(0.40f, 0.38f, 0.35f), 0.05f);
            var iron = ArenaArt.Mat(new Color(0.08f, 0.09f, 0.10f), 0.5f, null, 0.6f);
            var wood = ArenaArt.Mat(new Color(0.3f, 0.21f, 0.13f), 0.15f);
            var red = ArenaArt.Mat(new Color(0.55f, 0.06f, 0.05f), 0.4f);
            var windowLit = ArenaArt.Mat(new Color(0.2f, 0.15f, 0.08f), 0.6f, ArenaArt.LampWarm * 1.4f);
            var windowDark = ArenaArt.Mat(new Color(0.05f, 0.06f, 0.08f), 0.8f);
            var lampGlow = ArenaArt.Mat(ArenaArt.LampWarm, 0.5f, ArenaArt.LampWarm * 2.2f);
            var riftMat = ArenaArt.Mat(new Color(0.12f, 0.03f, 0.2f), 0.9f, ArenaArt.Rift * 2.6f);
            var crystal = ArenaArt.Mat(new Color(0.2f, 0.06f, 0.3f), 0.95f, ArenaArt.Rift * 1.3f, 0.2f);
            var stripe = ArenaArt.Mat(new Color(0.75f, 0.72f, 0.6f), 0.3f);
            var barrier = ArenaArt.Mat(new Color(0.85f, 0.45f, 0.1f), 0.3f);

            // ---------------- 地面 ----------------
            var ground = ArenaArt.Group("Ground", root, Vector3.zero);
            Solid(PrimitiveType.Cube, ground, new Vector3(0, -0.1f, 0), new Vector3(44f, 0.2f, 44f), asphalt, "GroundBase");
            // 人行道（約 12 cm 高，CharacterController 可直接走上）。
            Solid(PrimitiveType.Cube, ground, new Vector3(-4.5f, 0.06f, -8f), new Vector3(2f, 0.12f, 24f), pavement, "SidewalkWest");
            Solid(PrimitiveType.Cube, ground, new Vector3(4.5f, 0.06f, -8f), new Vector3(2f, 0.12f, 24f), pavement, "SidewalkEast");
            for (int i = 0; i < 6; i++)
                Deco(PrimitiveType.Cube, ground, new Vector3(0, 0.005f, -18f + i * 4f), new Vector3(0.14f, 0.01f, 1.8f), stripe, "LaneMark");
            // 小巷地面、廣場石板。
            Deco(PrimitiveType.Cube, ground, new Vector3(-11.5f, 0.01f, -7f), new Vector3(12.5f, 0.02f, 3.2f), cobble, "AlleyFloor");
            Deco(PrimitiveType.Cube, ground, new Vector3(0f, 0.012f, 12f), new Vector3(28f, 0.024f, 15f), cobble, "PlazaFloor");

            // ---------------- 建築 ----------------
            var buildings = ArenaArt.Group("Buildings", root, Vector3.zero);
            // 西側：小巷以南、小巷以北兩段（中間 z -8.6～-5.4 為小巷入口）。
            Block(buildings, "WestSouth", new Vector3(-12.75f, 0, -14.4f), new Vector3(14.5f, 9.5f, 11.2f), brick, windowLit, windowDark, rng, -90f);
            Block(buildings, "WestNorth", new Vector3(-12.75f, 0, -0.45f), new Vector3(14.5f, 11f, 9.9f), brickDark, windowLit, windowDark, rng, -90f);
            // 東側一整排（分成兩棟不同高度）。
            Block(buildings, "EastSouth", new Vector3(12.75f, 0, -13f), new Vector3(14.5f, 8.5f, 14f), stone, windowLit, windowDark, rng, 90f);
            Block(buildings, "EastNorth", new Vector3(12.75f, 0, -1f), new Vector3(14.5f, 12f, 10f), brick, windowLit, windowDark, rng, 90f);
            // 廣場北側與左右兩側。
            Block(buildings, "PlazaNorth", new Vector3(0f, 0, 21.5f), new Vector3(4f, 10f, 42f), brickDark, windowLit, windowDark, rng, 0f);
            Block(buildings, "PlazaWest", new Vector3(-17.5f, 0, 12f), new Vector3(5f, 8f, 15f), stone, windowLit, windowDark, rng, -90f);
            Block(buildings, "PlazaEast", new Vector3(17.5f, 0, 12f), new Vector3(5f, 9f, 15f), brick, windowLit, windowDark, rng, 90f);
            // 小巷盡頭的牆。
            Solid(PrimitiveType.Cube, buildings, new Vector3(-18.6f, 2.5f, -7f), new Vector3(0.6f, 5f, 3.4f), brickDark, "AlleyEndWall");
            // 南端：施工圍欄封住街道（地圖邊界）。
            var works = ArenaArt.Group("RoadWorks", root, new Vector3(0, 0, -19.6f));
            for (int i = 0; i < 4; i++)
            {
                var b = ArenaArt.Group("Barrier", works, new Vector3(-4.5f + i * 3f, 0, 0));
                Solid(PrimitiveType.Cube, b, new Vector3(0, 0.55f, 0), new Vector3(2.6f, 0.3f, 0.12f), barrier, "Board");
                Solid(PrimitiveType.Cube, b, new Vector3(-1.1f, 0.5f, 0), new Vector3(0.1f, 1f, 0.5f), iron, "Leg");
                Solid(PrimitiveType.Cube, b, new Vector3(1.1f, 0.5f, 0), new Vector3(0.1f, 1f, 0.5f), iron, "Leg");
            }

            // 地圖邊界（不可見的牆，40×40 範圍外無法離開）。
            var bounds = ArenaArt.Group("Bounds", root, Vector3.zero);
            Invisible(bounds, new Vector3(0, 3f, HalfSize + 0.5f), new Vector3(44f, 6f, 1f));
            Invisible(bounds, new Vector3(0, 3f, -HalfSize - 0.5f), new Vector3(44f, 6f, 1f));
            Invisible(bounds, new Vector3(HalfSize + 0.5f, 3f, 0), new Vector3(1f, 6f, 44f));
            Invisible(bounds, new Vector3(-HalfSize - 0.5f, 3f, 0), new Vector3(1f, 6f, 44f));

            // ---------------- 街燈 ----------------
            var lamps = ArenaArt.Group("StreetLamps", root, Vector3.zero);
            Vector3[] lampPos =
            {
                new Vector3(-4.9f, 0.12f, -15f), new Vector3(4.9f, 0.12f, -11f), new Vector3(-4.9f, 0.12f, -3f), new Vector3(4.9f, 0.12f, 2.5f),
                new Vector3(-11f, 0f, 6.5f), new Vector3(11f, 0f, 6.5f), new Vector3(-11.5f, 0f, 17.5f), new Vector3(11.5f, 0f, 17.5f),
                new Vector3(-13.5f, 0f, -5.8f),
            };
            for (int i = 0; i < lampPos.Length; i++)
            {
                var lamp = ArenaArt.Group("Lamp_" + i, lamps, lampPos[i]);
                Solid(PrimitiveType.Cylinder, lamp, new Vector3(0, 0.15f, 0), new Vector3(0.36f, 0.15f, 0.36f), iron, "Base");
                Solid(PrimitiveType.Cylinder, lamp, new Vector3(0, 1.9f, 0), new Vector3(0.14f, 1.9f, 0.14f), iron, "Post");
                Deco(PrimitiveType.Cube, lamp, new Vector3(0, 3.95f, 0), new Vector3(0.42f, 0.5f, 0.42f), lampGlow, "LampHead", false);
                Deco(PrimitiveType.Cube, lamp, new Vector3(0, 4.25f, 0), new Vector3(0.56f, 0.08f, 0.56f), iron, "Cap");
                bool flicker = i == 8;
                ArenaArt.PointLight(lamp, new Vector3(0, 3.7f, 0), flicker ? new Color(0.6f, 0.5f, 1f) : ArenaArt.LampWarm, 10f,
                    flicker ? 0.8f : 1.4f, i == 2 || i == 4);
            }

            // ---------------- 障礙物與道具 ----------------
            var props = ArenaArt.Group("Props", root, Vector3.zero);
            Car(props, new Vector3(-2.4f, 0, -11.5f), 2f, ArenaArt.Mat(new Color(0.12f, 0.2f, 0.3f), 0.6f, null, 0.3f), iron, windowDark);
            Car(props, new Vector3(2.5f, 0, -2.5f), -8f, ArenaArt.Mat(new Color(0.35f, 0.33f, 0.3f), 0.6f, null, 0.3f), iron, windowDark);
            var booth = ArenaArt.Group("PhoneBox", props, new Vector3(4.6f, 0.12f, -6.5f));
            Solid(PrimitiveType.Cube, booth, new Vector3(0, 1.25f, 0), new Vector3(1f, 2.5f, 1f), red, "Box");
            Deco(PrimitiveType.Cube, booth, new Vector3(-0.51f, 1.4f, 0), new Vector3(0.02f, 1.5f, 0.7f), windowLit, "Glass", false);
            Deco(PrimitiveType.Cube, booth, new Vector3(0, 2.6f, 0), new Vector3(1.1f, 0.2f, 1.1f), red, "Roof");
            Solid(PrimitiveType.Cylinder, props, new Vector3(-4.6f, 0.55f, -9.5f), new Vector3(0.6f, 0.45f, 0.6f), iron, "Bin");
            Solid(PrimitiveType.Cylinder, props, new Vector3(4.4f, 0.55f, 4.2f), new Vector3(0.6f, 0.45f, 0.6f), iron, "Bin");
            // 小巷：木箱與鐵桶（需要繞行）。
            Solid(PrimitiveType.Cube, props, new Vector3(-8.2f, 0.45f, -8f), new Vector3(0.9f, 0.9f, 0.9f), wood, "Crate");
            Solid(PrimitiveType.Cube, props, new Vector3(-8.3f, 1.2f, -8.05f), new Vector3(0.6f, 0.6f, 0.6f), wood, "Crate");
            Solid(PrimitiveType.Cube, props, new Vector3(-11.4f, 0.4f, -6.1f), new Vector3(1.2f, 0.8f, 0.8f), wood, "Crate");
            Solid(PrimitiveType.Cylinder, props, new Vector3(-13.2f, 0.5f, -8.1f), new Vector3(0.6f, 0.5f, 0.6f), iron, "Drum");
            // 廣場：長椅、矮牆、欄杆、路障（都在兩側，不擋戰術鏡頭）。
            Bench(props, new Vector3(-9f, 0, 11f), 90f, wood, iron);
            Bench(props, new Vector3(9.5f, 0, 13f), -90f, wood, iron);
            Solid(PrimitiveType.Cube, props, new Vector3(-7.5f, 0.6f, 15.5f), new Vector3(0.4f, 1.2f, 4.2f), stone, "BrokenWall");
            Solid(PrimitiveType.Cube, props, new Vector3(-7.5f, 1.45f, 14.6f), new Vector3(0.4f, 0.5f, 1.6f), stone, "BrokenWallTop");
            Solid(PrimitiveType.Cube, props, new Vector3(8f, 0.45f, 17f), new Vector3(3.2f, 0.9f, 0.4f), stone, "LowWall");
            for (int i = 0; i < 4; i++)
            {
                Solid(PrimitiveType.Cylinder, props, new Vector3(-10.5f + i * 1.2f, 0.4f, 5.4f), new Vector3(0.22f, 0.4f, 0.22f), iron, "Bollard");
                Solid(PrimitiveType.Cylinder, props, new Vector3(6.9f + i * 1.2f, 0.4f, 5.4f), new Vector3(0.22f, 0.4f, 0.22f), iron, "Bollard");
            }

            // ---------------- 異域：廣場裂痕 ----------------
            var anomaly = ArenaArt.Group("Anomaly", root, Vector3.zero);
            for (int i = 0; i < 12; i++)
            {
                Deco(PrimitiveType.Cube, anomaly, new Vector3(R(-7f, 7f), 0.03f, R(8f, 18f)), new Vector3(R(0.05f, 0.14f), 0.02f, R(1.5f, 4.5f)),
                    riftMat, "Crack", false, new Vector3(0, R(-80f, 80f), 0));
            }
            // 從主街延伸到廣場的細裂縫，引導玩家往北走。
            for (int i = 0; i < 5; i++)
                Deco(PrimitiveType.Cube, anomaly, new Vector3(R(-1.5f, 1.5f), 0.02f, -2f + i * 2f), new Vector3(0.05f, 0.02f, R(1f, 2f)), riftMat,
                    "Crack", false, new Vector3(0, R(-35f, 35f), 0));
            Vector3[] clusters = { new Vector3(-5.5f, 0, 14f), new Vector3(5.2f, 0, 16.5f), new Vector3(-3f, 0, 18.2f), new Vector3(11f, 0, 9f),
                new Vector3(-11.5f, 0, 14.5f) };
            foreach (var c in clusters)
            {
                var g = ArenaArt.Group("CrystalCluster", anomaly, c);
                int n = rng.Next(3, 6);
                for (int k = 0; k < n; k++)
                {
                    float h = R(0.6f, 2.1f);
                    Deco(PrimitiveType.Cube, g, new Vector3(R(-0.5f, 0.5f), h * 0.4f, R(-0.5f, 0.5f)), new Vector3(R(0.18f, 0.35f), h, R(0.18f, 0.35f)),
                        crystal, "Crystal", true, new Vector3(R(-25f, 25f), R(0, 90f), R(-25f, 25f)));
                }
                var col = g.gameObject.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0, 1f, 0);
                col.height = 2f;
                col.radius = 0.7f;
            }
            var rift = ArenaArt.Group("Rift", anomaly, new Vector3(0.6f, 0, 18.4f));
            Deco(PrimitiveType.Cube, rift, new Vector3(0, 3.4f, 0), new Vector3(0.35f, 6.8f, 0.1f), riftMat, "RiftCore", false, new Vector3(0, 0, 4f));
            Deco(PrimitiveType.Cube, rift, new Vector3(0.3f, 2.2f, 0), new Vector3(0.12f, 3.2f, 0.08f), riftMat, "RiftBranch", false, new Vector3(0, 0, -18f));
            Deco(PrimitiveType.Cube, rift, new Vector3(-0.35f, 4.6f, 0), new Vector3(0.1f, 2.4f, 0.08f), riftMat, "RiftBranch", false, new Vector3(0, 0, 22f));
            L.RiftLight = ArenaArt.PointLight(rift, new Vector3(0, 2.5f, -1.5f), ArenaArt.Rift, 14f, 2.4f, false);
            for (int i = 0; i < 16; i++)
            {
                var shard = Deco(PrimitiveType.Cube, anomaly, new Vector3(R(-8f, 8f), R(1.5f, 6f), R(9f, 19f)), Vector3.one * R(0.12f, 0.45f),
                    crystal, "FloatingShard", false, new Vector3(R(0, 360f), R(0, 360f), R(0, 360f)));
                L.Floating.Add(shard);
                L.FloatingHome.Add(shard.localPosition);
            }

            // 遭遇區：戰場原點與地面上的淡紫色範圍圈。
            L.EncounterAnchor = ArenaArt.Group("EncounterAnchor", root, EncounterAnchorPosition);
            L.EncounterRingMat = ArenaArt.FadeMat(new Color(0.62f, 0.25f, 1f, 0.16f), ArenaArt.Rift * 0.5f);
            L.EncounterRing = Deco(PrimitiveType.Cylinder, root, Vector3.zero, Vector3.one, L.EncounterRingMat, "EncounterZoneRing", false);

            // ---------------- 小巷盡頭：可調查的異常物件 ----------------
            var obj = ArenaArt.Group("AnomalyObject", root, AnomalyPosition);
            L.AnomalyMat = ArenaArt.Mat(new Color(0.25f, 0.08f, 0.35f), 0.95f, ArenaArt.Rift * 2.2f, 0.2f);
            Solid(PrimitiveType.Cylinder, obj, new Vector3(0, 0.3f, 0), new Vector3(0.9f, 0.3f, 0.9f), stone, "Pedestal");
            L.Anomaly = Deco(PrimitiveType.Cube, obj, new Vector3(0, 1.5f, 0), new Vector3(0.45f, 0.75f, 0.45f), L.AnomalyMat, "FrozenShard", true,
                new Vector3(20f, 35f, 15f));
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                Deco(PrimitiveType.Cube, obj, new Vector3(Mathf.Cos(a) * 0.9f, 0.9f + (i % 3) * 0.5f, Mathf.Sin(a) * 0.9f), Vector3.one * 0.08f,
                    L.AnomalyMat, "StillDrop", false);
            }
            for (int i = 0; i < 3; i++)
                Deco(PrimitiveType.Cube, obj, new Vector3(R(-1.2f, 1.2f), 0.03f, R(-1.2f, 1.2f)), new Vector3(0.06f, 0.02f, R(0.8f, 1.6f)), riftMat,
                    "Crack", false, new Vector3(0, R(0, 180f), 0));
            L.AnomalyLight = ArenaArt.PointLight(obj, new Vector3(0, 1.6f, 0.6f), ArenaArt.Rift, 5f, 1.8f, false);
            return L;
        }

        // ---------------- 小工具 ----------------

        /// <summary>有碰撞體的 Primitive（建築、牆、障礙物）。</summary>
        private static Transform Solid(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, string name,
            Vector3? euler = null)
        {
            return ArenaArt.Prim(type, parent, pos, scale, mat, euler, name, true, true);
        }

        /// <summary>沒有碰撞體的裝飾。</summary>
        private static Transform Deco(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, string name,
            bool shadows = false, Vector3? euler = null)
        {
            return ArenaArt.Prim(type, parent, pos, scale, mat, euler, name, false, shadows);
        }

        private static void Invisible(Transform parent, Vector3 pos, Vector3 size)
        {
            var go = new GameObject("BoundaryWall");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var c = go.AddComponent<BoxCollider>();
            c.size = size;
        }

        /// <summary>連棟建築：主體有碰撞體，正面（面向街道）有窗戶。yaw 決定正面朝向。</summary>
        private static void Block(Transform parent, string name, Vector3 pos, Vector3 size, Material wall, Material lit, Material dark,
            System.Random rng, float yaw)
        {
            var b = ArenaArt.Group(name, parent, pos);
            // size.x＝正面朝向方向的深度、size.z＝立面寬度；正面為本地 -z 面，yaw 決定它朝哪裡（-90＝朝東、90＝朝西、0＝朝南）。
            b.localRotation = Quaternion.Euler(0, yaw, 0);
            Solid(PrimitiveType.Cube, b, new Vector3(0, size.y * 0.5f, 0), new Vector3(size.z, size.y, size.x), wall, "Mass");
            Deco(PrimitiveType.Cube, b, new Vector3(0, size.y + 0.1f, 0), new Vector3(size.z + 0.3f, 0.2f, size.x + 0.3f), wall, "Cornice", true);
            int cols = Mathf.Max(2, Mathf.FloorToInt(size.z / 1.5f));
            int rows = Mathf.Max(2, Mathf.FloorToInt((size.y - 1.5f) / 1.9f));
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                float x = (c - (cols - 1) * 0.5f) * (size.z / cols);
                float y = 1.7f + r * 1.9f;
                bool on = rng.NextDouble() < 0.22;
                Deco(PrimitiveType.Cube, b, new Vector3(x, y, -size.x * 0.5f - 0.02f), new Vector3(0.6f, 1f, 0.04f), on ? lit : dark, "Window");
            }
            // 門。
            Deco(PrimitiveType.Cube, b, new Vector3(0, 1.1f, -size.x * 0.5f - 0.03f), new Vector3(1.1f, 2.2f, 0.05f), dark, "Door");
        }

        private static void Car(Transform parent, Vector3 pos, float yaw, Material paint, Material tyre, Material glass)
        {
            var car = ArenaArt.Group("ParkedCar", parent, pos);
            car.localRotation = Quaternion.Euler(0, yaw, 0);
            Solid(PrimitiveType.Cube, car, new Vector3(0, 0.6f, 0), new Vector3(1.8f, 0.7f, 4.2f), paint, "Body");
            Solid(PrimitiveType.Cube, car, new Vector3(0, 1.2f, -0.2f), new Vector3(1.6f, 0.6f, 2.2f), paint, "Cabin");
            Deco(PrimitiveType.Cube, car, new Vector3(0, 1.22f, -0.2f), new Vector3(1.62f, 0.45f, 2f), glass, "Glass");
            for (int i = 0; i < 4; i++)
                Deco(PrimitiveType.Cylinder, car, new Vector3(i < 2 ? -0.85f : 0.85f, 0.32f, i % 2 == 0 ? 1.3f : -1.3f), new Vector3(0.62f, 0.12f, 0.62f),
                    tyre, "Wheel", true, new Vector3(0, 0, 90f));
        }

        private static void Bench(Transform parent, Vector3 pos, float yaw, Material wood, Material iron)
        {
            var b = ArenaArt.Group("Bench", parent, pos);
            b.localRotation = Quaternion.Euler(0, yaw, 0);
            Solid(PrimitiveType.Cube, b, new Vector3(0, 0.45f, 0), new Vector3(1.8f, 0.08f, 0.5f), wood, "Seat");
            Solid(PrimitiveType.Cube, b, new Vector3(0, 0.8f, 0.22f), new Vector3(1.8f, 0.5f, 0.06f), wood, "Back");
            Deco(PrimitiveType.Cube, b, new Vector3(-0.8f, 0.22f, 0), new Vector3(0.08f, 0.45f, 0.5f), iron, "Leg");
            Deco(PrimitiveType.Cube, b, new Vector3(0.8f, 0.22f, 0), new Vector3(0.08f, 0.45f, 0.5f), iron, "Leg");
        }
    }
}
