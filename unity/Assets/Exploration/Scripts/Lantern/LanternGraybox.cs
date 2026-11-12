using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;
using Mode = Shepherd.Exploration.Lantern.LanternReveal.Mode;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 提燈手感 QA 的灰盒空間（不是正式美術）：
    /// ① 暗走廊（2.0 m 寬轉角）→ ② 暗客廳（5×5 m，書櫃隔牆擋住後方角落）→ ③ 亮房間（大窗、強烈的異域日光）。
    /// 每個空間都有「真實／異域」不同的東西，用來驗證：光圈內外兩個樣子、暗處與亮處都成立、異域光看得見但不一定是真的。
    /// </summary>
    public sealed class LanternGraybox
    {
        public const float H = 2.7f;
        public const float T = 0.12f;
        public const string RootName = "LanternGraybox";

        public Transform Root { get; private set; }
        public readonly Dictionary<string, Transform> Marks = new Dictionary<string, Transform>();
        public readonly List<Material> Materials = new List<Material>();
        public readonly List<Light> AnomalyLights = new List<Light>();
        public readonly List<Renderer> RealOnly = new List<Renderer>();
        public readonly List<Renderer> AnomalyOnly = new List<Renderer>();
        public readonly List<Renderer> Dual = new List<Renderer>();
        public Transform ClockAnomalyHands { get; private set; }
        public Bounds Area { get; private set; }

        private float[] flickerSeed;
        private Material wall, wallBright, floor, ceiling, wood, fabric, metal, white, partition;

        public static LanternGraybox Build(Transform parent)
        {
            var g = new LanternGraybox();
            g.Root = new GameObject(RootName).transform;
            g.Root.SetParent(parent, false);
            g.MakeMaterials();
            g.Shell();
            g.Hallway();
            g.Living();
            g.Bright();
            g.Lights();
            g.Area = new Bounds(new Vector3(5.2f, 1.35f, 4.2f), new Vector3(10.4f, 2.7f, 8.4f));
            g.flickerSeed = new float[g.AnomalyLights.Count];
            for (int i = 0; i < g.flickerSeed.Length; i++) g.flickerSeed[i] = i * 13.7f;
            return g;
        }

        public void Destroy()
        {
            if (Root != null) ArenaArt.Kill(Root.gameObject);
            foreach (var m in Materials) ArenaArt.Kill(m);
            Materials.Clear();
        }

        // ---------------- 材質 ----------------

        private Material Mat(string name, Mode mode, Color real, Color anomaly, Texture rt = null, Texture at = null, float rs = 0.3f, float asm = 0.5f, float wobble = 0.012f, bool worldUv = false)
        {
            var m = LanternReveal.Make(name, mode, real, anomaly, rt, at, rs, asm, wobble);
            if (worldUv) m.SetFloat("_WorldUV", 1f);
            Materials.Add(m);
            return m;
        }

        private void MakeMaterials()
        {
            var grid = LanternReveal.Grid;
            wall = Mat("LG_Wall", Mode.Dual, new Color(0.86f, 0.8f, 0.7f), new Color(0.36f, 0.44f, 0.41f), grid, grid, 0.15f, 0.45f, 0.012f, true);
            wallBright = Mat("LG_WallBright", Mode.Dual, new Color(0.9f, 0.84f, 0.72f), new Color(0.82f, 0.95f, 0.98f), grid, grid, 0.15f, 0.2f, 0.01f, true);
            wallBright.SetColor("_AnomalyEmission", new Color(0.08f, 0.12f, 0.13f));
            floor = Mat("LG_Floor", Mode.Dual, new Color(0.58f, 0.42f, 0.28f), new Color(0.3f, 0.33f, 0.34f), grid, grid, 0.35f, 0.7f, 0.006f, true);
            ceiling = Mat("LG_Ceiling", Mode.Dual, new Color(0.9f, 0.88f, 0.84f), new Color(0.3f, 0.34f, 0.36f), grid, grid, 0.1f, 0.3f, 0.02f, true);
            wood = Mat("LG_Wood", Mode.Dual, new Color(0.42f, 0.27f, 0.16f), new Color(0.22f, 0.24f, 0.25f), null, null, 0.4f, 0.6f, 0.01f);
            fabric = Mat("LG_Fabric", Mode.Dual, new Color(0.55f, 0.18f, 0.16f), new Color(0.3f, 0.36f, 0.4f), null, null, 0.08f, 0.3f, 0.02f);
            metal = Mat("LG_Metal", Mode.Dual, new Color(0.2f, 0.2f, 0.21f), new Color(0.2f, 0.22f, 0.24f), null, null, 0.5f, 0.6f, 0.005f);
            white = Mat("LG_White", Mode.Dual, new Color(0.94f, 0.93f, 0.9f), new Color(0.7f, 0.76f, 0.76f), null, null, 0.6f, 0.6f, 0.005f);
            partition = Mat("LG_Bookcase", Mode.Dual, new Color(0.36f, 0.22f, 0.13f), new Color(0.18f, 0.22f, 0.22f), grid, grid, 0.3f, 0.5f, 0.01f, true);
        }

        // ---------------- 幾何 ----------------

        private Transform Box(string name, Vector3 center, Vector3 size, Material m, bool collider = true, Vector3? euler = null, Transform parent = null)
        {
            var t = ArenaArt.Prim(PrimitiveType.Cube, parent ?? Root, center, size, m, euler, name, collider, true);
            Place(t, center, euler);
            Register(t.GetComponent<Renderer>(), m);
            return t;
        }

        private Transform Cyl(string name, Vector3 center, Vector3 size, Material m, Vector3? euler = null, Transform parent = null)
        {
            var t = ArenaArt.Prim(PrimitiveType.Cylinder, parent ?? Root, center, size, m, euler, name, false, true);
            Place(t, center, euler);
            Register(t.GetComponent<Renderer>(), m);
            return t;
        }

        private Transform Ball(string name, Vector3 center, Vector3 size, Material m, Transform parent = null)
        {
            var t = ArenaArt.Prim(PrimitiveType.Sphere, parent ?? Root, center, size, m, null, name, false, true);
            Register(t.GetComponent<Renderer>(), m);
            return t;
        }

        private Transform Quad(string name, Vector3 center, Vector3 size, Vector3 euler, Material m)
        {
            var t = ArenaArt.Prim(PrimitiveType.Quad, Root, center, size, m, euler, name, false, false);
            Register(t.GetComponent<Renderer>(), m);
            return t;
        }

        /// <summary>位置與旋轉一律以世界座標指定（父物件可以有旋轉）。</summary>
        private static void Place(Transform t, Vector3 center, Vector3? euler)
        {
            t.position = center;
            t.rotation = Quaternion.Euler(euler ?? Vector3.zero);
        }

        private void Register(Renderer r, Material m)
        {
            switch (LanternReveal.GetMode(m))
            {
                case Mode.RealOnly: RealOnly.Add(r); break;
                case Mode.AnomalyOnly: AnomalyOnly.Add(r); break;
                default: Dual.Add(r); break;
            }
        }

        /// <summary>沿 z 方向的牆（x 固定），gaps＝(z0, z1) 門洞。</summary>
        private void WallZ(float x, float z0, float z1, Material m, params Vector2[] gaps)
        {
            float cur = z0;
            foreach (var g in gaps)
            {
                if (g.x > cur) Box("Wall", new Vector3(x, H / 2, (cur + g.x) / 2), new Vector3(T, H, g.x - cur), m);
                Box("Lintel", new Vector3(x, (2.1f + H) / 2, (g.x + g.y) / 2), new Vector3(T, H - 2.1f, g.y - g.x), m);
                cur = g.y;
            }
            if (z1 > cur) Box("Wall", new Vector3(x, H / 2, (cur + z1) / 2), new Vector3(T, H, z1 - cur), m);
        }

        /// <summary>沿 x 方向的牆（z 固定）。</summary>
        private void WallX(float z, float x0, float x1, Material m, params Vector2[] gaps)
        {
            float cur = x0;
            foreach (var g in gaps)
            {
                if (g.x > cur) Box("Wall", new Vector3((cur + g.x) / 2, H / 2, z), new Vector3(g.x - cur, H, T), m);
                Box("Lintel", new Vector3((g.x + g.y) / 2, (2.1f + H) / 2, z), new Vector3(g.y - g.x, H - 2.1f, T), m);
                cur = g.y;
            }
            if (x1 > cur) Box("Wall", new Vector3((cur + x1) / 2, H / 2, z), new Vector3(x1 - cur, H, T), m);
        }

        private Transform Mark(string name, Vector3 pos, Vector3 lookAt)
        {
            var t = new GameObject(name).transform;
            t.SetParent(Root, false);
            t.position = pos;
            // 標記放在腳底，朝向以眼睛高度（1.62 m）計算。
            var d = lookAt - (pos + Vector3.up * 1.62f);
            if (d.sqrMagnitude > 1e-4f) t.rotation = Quaternion.LookRotation(d);
            Marks[name] = t;
            return t;
        }

        private void Shell()
        {
            Box("Floor", new Vector3(5.2f, -0.05f, 4.2f), new Vector3(10.6f, 0.1f, 8.6f), floor);
            // 走廊 x 0–2.0、z 0–7.0；客廳 x 2.0–7.0、z 3.4–8.4；亮房間 x 7.0–11.0、z 2.4–7.4。
            WallZ(0f, 0f, 7f, wall);
            WallX(0f, 0f, 2f, wall);
            WallX(7f, 0f, 2f, wall);
            WallZ(2f, 0f, 8.4f, wall, new Vector2(5.6f, 6.6f));
            WallX(8.4f, 2f, 7f, wall);
            WallX(3.4f, 2f, 7f, wall);
            WallZ(7f, 2.4f, 8.4f, wall, new Vector2(4.0f, 5.0f));
            WallX(2.4f, 7f, 11f, wallBright);
            WallX(7.4f, 7f, 11f, wallBright);
            // 亮房間東牆：大窗 z 3.9–6.1、y 0.8–2.3。
            Box("Wall", new Vector3(11f, H / 2, 3.15f), new Vector3(T, H, 1.5f), wallBright);
            Box("Wall", new Vector3(11f, H / 2, 6.75f), new Vector3(T, H, 1.3f), wallBright);
            Box("Sill", new Vector3(11f, 0.4f, 5f), new Vector3(T, 0.8f, 2.2f), wallBright);
            Box("Lintel", new Vector3(11f, (2.3f + H) / 2, 5f), new Vector3(T, H - 2.3f, 2.2f), wallBright);
            var pane = Mat("LG_WindowLight", Mode.Dual, new Color(0.2f, 0.18f, 0.15f), new Color(0.6f, 0.75f, 0.8f), null, null, 0.9f, 0.9f, 0f);
            pane.SetColor("_EmissionColor", new Color(1.6f, 1.15f, 0.7f));
            pane.SetColor("_AnomalyEmission", new Color(2.6f, 3.4f, 3.6f));
            Box("WindowPane", new Vector3(11.1f, 1.55f, 5f), new Vector3(0.02f, 1.5f, 2.2f), pane, false);
            // 天花板（分房，避免縫隙漏光）。
            Box("Ceiling", new Vector3(1f, H + 0.05f, 3.5f), new Vector3(2.1f, 0.1f, 7.1f), ceiling);
            Box("Ceiling", new Vector3(4.5f, H + 0.05f, 5.9f), new Vector3(5.1f, 0.1f, 5.1f), ceiling);
            Box("Ceiling", new Vector3(9f, H + 0.05f, 4.9f), new Vector3(4.1f, 0.1f, 5.1f), ceiling);
        }

        private void Hallway()
        {
            // 牆上的畫：真實是山丘，異域是一隻眼睛。
            Box("PaintingFrame", new Vector3(0.09f, 1.6f, 2.2f), new Vector3(0.05f, 0.62f, 0.8f), wood, false);
            var canvas = Mat("LG_Painting", Mode.Dual, Color.white, Color.white, LanternReveal.Painting(true), LanternReveal.Painting(false), 0.2f, 0.4f, 0.004f);
            Quad("Painting", new Vector3(0.12f, 1.6f, 2.2f), new Vector3(0.7f, 0.52f, 1f), new Vector3(0f, -90f, 0f), canvas);
            // 只存在於異域：一扇不存在的門。
            var fakeDoor = Mat("LG_FakeDoor", Mode.AnomalyOnly, Color.white, new Color(0.35f, 0.08f, 0.08f), null, null, 0.3f, 0.6f, 0.02f);
            fakeDoor.SetColor("_AnomalyEmission", new Color(0.12f, 0.01f, 0.01f));
            Box("FakeDoor", new Vector3(0.09f, 1.0f, 4.6f), new Vector3(0.06f, 2.0f, 0.9f), fakeDoor, false);
            Box("FakeDoorFrame", new Vector3(0.1f, 2.05f, 4.6f), new Vector3(0.08f, 0.1f, 1.05f), fakeDoor, false);
            // 只存在於真實：掛著外套的衣帽架。
            var coatReal = Mat("LG_Coat", Mode.RealOnly, new Color(0.25f, 0.22f, 0.3f), Color.black, null, null, 0.1f, 0.1f, 0f);
            var rackReal = Mat("LG_Rack", Mode.RealOnly, new Color(0.35f, 0.22f, 0.12f), Color.black, null, null, 0.4f, 0.4f, 0f);
            Cyl("CoatRack", new Vector3(1.7f, 0.9f, 1.0f), new Vector3(0.05f, 0.9f, 0.05f), rackReal);
            Cyl("CoatRackBase", new Vector3(1.7f, 0.02f, 1.0f), new Vector3(0.4f, 0.02f, 0.4f), rackReal);
            ArenaArt.Prim(PrimitiveType.Capsule, Root, new Vector3(1.7f, 1.25f, 0.92f), new Vector3(0.36f, 0.42f, 0.18f), coatReal, null, "Coat", false, true);
            RealOnly.Add(Root.Find("Coat").GetComponent<Renderer>());
            // 走廊盡頭的門牌：真實 17、異域 16。
            var plate = Mat("LG_DoorNumber", Mode.Dual, Color.white, Color.white,
                LanternReveal.Number(17, new Color32(235, 228, 210, 255), new Color32(40, 30, 25, 255)),
                LanternReveal.Number(16, new Color32(200, 215, 210, 255), new Color32(20, 40, 40, 255)), 0.4f, 0.5f, 0.003f);
            Quad("DoorNumber", new Vector3(1f, 1.6f, 6.93f), new Vector3(0.36f, 0.36f, 1f), new Vector3(0f, 0f, 0f), plate);
            Box("HallRunner", new Vector3(1f, 0.005f, 3.2f), new Vector3(0.9f, 0.01f, 4.8f), fabric, false);

            Mark("Spawn", new Vector3(1f, 0.05f, 0.7f), new Vector3(1f, 1.4f, 5f));
            Mark("HallView", new Vector3(1f, 0.05f, 0.9f), new Vector3(0.4f, 1.3f, 3.6f));
        }

        private void Living()
        {
            // 書櫃隔牆：擋住後方角落（一進門看不到全貌）。
            Box("BookcaseWall", new Vector3(4.4f, 1.05f, 4.5f), new Vector3(0.35f, 2.1f, 2.2f), partition);
            for (int i = 0; i < 4; i++)
                Box("Shelf", new Vector3(4.2f, 0.35f + i * 0.5f, 4.5f), new Vector3(0.04f, 0.03f, 2.1f), wood, false);
            // 餐桌、餐盤：異域只有一副餐具，真實有兩副。
            Box("TableTop", new Vector3(4.8f, 0.74f, 7.3f), new Vector3(1.6f, 0.05f, 0.9f), wood);
            foreach (var p in new[] { new Vector2(4.1f, 6.95f), new Vector2(5.5f, 6.95f), new Vector2(4.1f, 7.65f), new Vector2(5.5f, 7.65f) })
                Box("TableLeg", new Vector3(p.x, 0.36f, p.y), new Vector3(0.06f, 0.72f, 0.06f), wood, false);
            Cyl("PlateA", new Vector3(4.4f, 0.775f, 7.15f), new Vector3(0.26f, 0.008f, 0.26f), white);
            var plateReal = Mat("LG_PlateReal", Mode.RealOnly, new Color(0.94f, 0.93f, 0.9f), Color.black, null, null, 0.6f, 0.6f, 0f);
            Cyl("PlateB", new Vector3(5.2f, 0.775f, 7.15f), new Vector3(0.26f, 0.008f, 0.26f), plateReal);
            // 椅子：真實在桌邊；異域則倒掛在天花板下。
            var chairReal = Mat("LG_ChairReal", Mode.RealOnly, new Color(0.4f, 0.26f, 0.15f), Color.black, null, null, 0.35f, 0.35f, 0f);
            var chairAnom = Mat("LG_ChairAnomaly", Mode.AnomalyOnly, Color.black, new Color(0.24f, 0.27f, 0.28f), null, null, 0.5f, 0.6f, 0.03f);
            Chair("ChairReal", new Vector3(5.2f, 0f, 6.55f), 0f, chairReal, false);
            Chair("ChairAnomaly", new Vector3(4.4f, 2.62f, 6.55f), 0f, chairAnom, true);
            Chair("ChairFixed", new Vector3(4.4f, 0f, 8.0f), 180f, wood, false);
            // 餐邊櫃與全家福：異域裡少了一個人。
            Box("Sideboard", new Vector3(2.9f, 0.42f, 8.12f), new Vector3(1.4f, 0.84f, 0.42f), wood);
            Box("PhotoFrame", new Vector3(2.9f, 1.02f, 8.2f), new Vector3(0.42f, 0.34f, 0.03f), metal, false, new Vector3(8f, 0f, 0f));
            var photo = Mat("LG_Photo", Mode.Dual, Color.white, Color.white, LanternReveal.Photo(true), LanternReveal.Photo(false), 0.5f, 0.5f, 0.002f);
            Quad("Photo", new Vector3(2.9f, 1.02f, 8.18f), new Vector3(0.38f, 0.29f, 1f), new Vector3(8f, 0f, 0f), photo);
            // 牆上的鐘：異域指針一直轉；真實停在 4:10。
            Cyl("ClockFace", new Vector3(6.0f, 2.0f, 8.33f), new Vector3(0.4f, 0.02f, 0.4f), white, new Vector3(90f, 0f, 0f));
            var handReal = Mat("LG_HandReal", Mode.RealOnly, new Color(0.05f, 0.05f, 0.05f), Color.black, null, null, 0.3f, 0.3f, 0f);
            var handAnom = Mat("LG_HandAnomaly", Mode.AnomalyOnly, Color.black, new Color(0.5f, 0.05f, 0.05f), null, null, 0.3f, 0.3f, 0f);
            var fixedHands = new GameObject("ClockRealHands").transform;
            fixedHands.SetParent(Root, false);
            fixedHands.position = new Vector3(6.0f, 2.0f, 8.3f);
            Box("HourHand", fixedHands.position + Quaternion.Euler(0, 0, -125f) * new Vector3(0, 0.05f, 0), new Vector3(0.018f, 0.1f, 0.01f), handReal, false, new Vector3(0, 0, -125f), fixedHands);
            Box("MinuteHand", fixedHands.position + Quaternion.Euler(0, 0, -60f) * new Vector3(0, 0.08f, 0), new Vector3(0.012f, 0.16f, 0.01f), handReal, false, new Vector3(0, 0, -60f), fixedHands);
            ClockAnomalyHands = new GameObject("ClockAnomalyHands").transform;
            ClockAnomalyHands.SetParent(Root, false);
            ClockAnomalyHands.position = new Vector3(6.0f, 2.0f, 8.29f);
            Box("SpinHand", ClockAnomalyHands.position + new Vector3(0, 0.08f, 0), new Vector3(0.014f, 0.17f, 0.01f), handAnom, false, null, ClockAnomalyHands);
            // 沙發（靠走廊這側）。
            Box("SofaBase", new Vector3(3.0f, 0.22f, 4.4f), new Vector3(1.8f, 0.44f, 0.8f), fabric);
            Box("SofaBack", new Vector3(3.0f, 0.62f, 4.0f), new Vector3(1.8f, 0.6f, 0.2f), fabric, false);
            // 隔牆後的角落：真實的高櫃好好站著；異域裡它倒在地上。
            var cabReal = Mat("LG_CabinetReal", Mode.RealOnly, new Color(0.38f, 0.24f, 0.14f), Color.black, null, null, 0.35f, 0.35f, 0f);
            var cabAnom = Mat("LG_CabinetAnomaly", Mode.AnomalyOnly, Color.black, new Color(0.2f, 0.23f, 0.24f), null, null, 0.5f, 0.6f, 0.02f);
            Box("CabinetReal", new Vector3(6.5f, 0.95f, 3.8f), new Vector3(0.8f, 1.9f, 0.45f), cabReal, false);
            Box("CabinetFallen", new Vector3(5.9f, 0.23f, 4.2f), new Vector3(1.9f, 0.45f, 0.8f), cabAnom, false, new Vector3(0f, 20f, 0f));
            var lampShade = Mat("LG_LampShade", Mode.Dual, new Color(0.3f, 0.28f, 0.25f), new Color(0.35f, 0.3f, 0.45f), null, null, 0.1f, 0.2f, 0.01f);
            lampShade.SetColor("_AnomalyEmission", new Color(0.9f, 0.6f, 1.6f));
            Cyl("FloorLampPole", new Vector3(6.6f, 0.75f, 7.9f), new Vector3(0.04f, 0.75f, 0.04f), metal);
            Cyl("FloorLampShade", new Vector3(6.6f, 1.55f, 7.9f), new Vector3(0.38f, 0.14f, 0.38f), lampShade);

            Mark("LivingEntry", new Vector3(1.4f, 0.05f, 6.1f), new Vector3(5f, 1.1f, 6.6f));
            Mark("LivingAlcove", new Vector3(4.6f, 0.05f, 6.3f), new Vector3(6.2f, 0.8f, 3.9f));
            Mark("LivingTable", new Vector3(3.3f, 0.05f, 6.3f), new Vector3(4.8f, 0.9f, 7.3f));
        }

        private void Chair(string name, Vector3 pos, float yaw, Material m, bool upsideDown)
        {
            var root = new GameObject(name).transform;
            root.SetParent(Root, false);
            root.position = pos;
            root.rotation = Quaternion.Euler(upsideDown ? 180f : 0f, yaw, 0f);
            Box("Seat", root.TransformPoint(new Vector3(0, 0.45f, 0)), new Vector3(0.44f, 0.04f, 0.42f), m, false, root.eulerAngles, root);
            Box("Back", root.TransformPoint(new Vector3(0, 0.75f, -0.19f)), new Vector3(0.44f, 0.55f, 0.04f), m, false, root.eulerAngles, root);
            foreach (var p in new[] { new Vector2(-0.19f, -0.18f), new Vector2(0.19f, -0.18f), new Vector2(-0.19f, 0.18f), new Vector2(0.19f, 0.18f) })
                Box("Leg", root.TransformPoint(new Vector3(p.x, 0.22f, p.y)), new Vector3(0.04f, 0.44f, 0.04f), m, false, root.eulerAngles, root);
        }

        private void Bright()
        {
            var calendar = Mat("LG_Calendar", Mode.Dual, Color.white, Color.white,
                LanternReveal.Number(17, new Color32(245, 240, 230, 255), new Color32(150, 30, 30, 255)),
                LanternReveal.Number(16, new Color32(225, 245, 248, 255), new Color32(30, 90, 100, 255)), 0.3f, 0.3f, 0.004f);
            Quad("Calendar", new Vector3(8.6f, 1.6f, 7.33f), new Vector3(0.5f, 0.5f, 1f), new Vector3(0f, 0f, 0f), calendar);
            Box("Desk", new Vector3(9.9f, 0.38f, 3.1f), new Vector3(1.4f, 0.76f, 0.7f), wood);
            Chair("DeskChair", new Vector3(9.9f, 0f, 3.75f), 180f, wood, false);
            var rug = Mat("LG_Rug", Mode.Dual, new Color(0.55f, 0.2f, 0.14f), new Color(0.85f, 0.9f, 0.88f), null, null, 0.05f, 0.1f, 0.004f);
            Box("Rug", new Vector3(8.9f, 0.006f, 5f), new Vector3(2.4f, 0.012f, 1.7f), rug, false);
            Cyl("Pot", new Vector3(7.6f, 0.22f, 6.8f), new Vector3(0.36f, 0.22f, 0.36f), fabric);
            var leaves = Mat("LG_Leaves", Mode.RealOnly, new Color(0.22f, 0.45f, 0.18f), Color.black, null, null, 0.3f, 0.3f, 0f);
            var dead = Mat("LG_DeadTwigs", Mode.AnomalyOnly, Color.black, new Color(0.08f, 0.08f, 0.07f), null, null, 0.2f, 0.2f, 0.01f);
            Ball("Leaves", new Vector3(7.6f, 0.85f, 6.8f), new Vector3(0.7f, 0.8f, 0.7f), leaves);
            for (int i = 0; i < 5; i++)
                Cyl("Twig", new Vector3(7.6f + Mathf.Sin(i * 1.3f) * 0.12f, 0.7f, 6.8f + Mathf.Cos(i * 1.3f) * 0.12f), new Vector3(0.02f, 0.3f, 0.02f), dead, new Vector3(Mathf.Sin(i) * 25f, i * 70f, Mathf.Cos(i) * 25f));

            Mark("BrightEntry", new Vector3(6.4f, 0.05f, 4.5f), new Vector3(9.5f, 1.2f, 5.2f));
            Mark("BrightCalendar", new Vector3(8.4f, 0.05f, 5.4f), new Vector3(8.6f, 1.5f, 7.3f));
        }

        private Light AddLight(string name, LightType type, Vector3 pos, Color c, float intensity, float range, bool anomaly, LightShadows shadows, float angle = 0f, Vector3? dir = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            if (dir.HasValue) go.transform.rotation = Quaternion.LookRotation(dir.Value);
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows;
            l.shadowStrength = 0.9f;
            if (type == LightType.Spot) { l.spotAngle = angle; l.innerSpotAngle = angle * 0.55f; }
            if (anomaly) AnomalyLights.Add(l);
            return l;
        }

        private void Lights()
        {
            // 異域的光：冷色、會閃。看得見，但照出的是扭曲後的樣子；燈泡本身在真實裡是熄滅的。
            var bulb = Mat("LG_Bulb", Mode.Dual, new Color(0.3f, 0.3f, 0.28f), new Color(0.7f, 0.85f, 1f), null, null, 0.8f, 0.8f, 0f);
            bulb.SetColor("_AnomalyEmission", new Color(1.6f, 2.2f, 3f));
            Ball("HallBulb", new Vector3(1f, 2.5f, 3.4f), new Vector3(0.1f, 0.12f, 0.1f), bulb);
            AddLight("HallAnomalyLight", LightType.Point, new Vector3(1f, 2.38f, 3.4f), new Color(0.62f, 0.78f, 1f), 1.1f, 6f, true, LightShadows.Soft);
            AddLight("LivingAnomalyLamp", LightType.Point, new Vector3(6.6f, 1.45f, 7.9f), new Color(0.72f, 0.55f, 1f), 1.4f, 7f, true, LightShadows.Soft);
            // 亮房間：強烈、偏青白、過曝的「異域日光」。
            AddLight("BrightAnomalySun", LightType.Spot, new Vector3(12.6f, 2.6f, 5f), new Color(0.82f, 0.97f, 1f), 9f, 16f, true, LightShadows.Soft, 110f, new Vector3(-1f, -0.45f, 0f));
            AddLight("BrightAnomalyFill", LightType.Point, new Vector3(8.6f, 2.3f, 4.9f), new Color(0.8f, 0.95f, 1f), 2.2f, 7f, true, LightShadows.None);
        }

        private readonly float[] baseIntensity = new float[8];

        /// <summary>異域燈閃爍、鐘的指針轉動。</summary>
        public void Tick(float time)
        {
            for (int i = 0; i < AnomalyLights.Count; i++)
            {
                var l = AnomalyLights[i];
                if (l == null) continue;
                if (baseIntensity[i] <= 0f) baseIntensity[i] = l.intensity;
                if (l.type == LightType.Spot) continue;
                float n = Mathf.PerlinNoise(time * 3.1f, flickerSeed[i]);
                float drop = Mathf.PerlinNoise(time * 0.7f, flickerSeed[i] + 5f) > 0.78f ? 0.25f : 1f;
                l.intensity = baseIntensity[i] * Mathf.Lerp(0.7f, 1.1f, n) * drop;
            }
            if (ClockAnomalyHands != null) ClockAnomalyHands.rotation = Quaternion.Euler(0f, 0f, -time * 137f);
        }
    }
}
