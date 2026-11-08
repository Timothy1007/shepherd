using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Case01
{
    public struct Case01Pose
    {
        public Vector3 Position;
        public float Yaw;

        public Case01Pose(Vector3 p, float yaw)
        {
            Position = p;
            Yaw = yaw;
        }
    }

    public sealed class Case01RoomVolume
    {
        public Case01Room Room;
        public Bounds Bounds;
    }

    /// <summary>
    /// 傳送通路：玩家從來源側（Normal 的反方向）穿過門洞平面時觸發。
    /// 門洞是真正沒有碰撞體的開口；門後另有不可見的擋牆，萬一沒有觸發也不會走出場景。
    /// </summary>
    public sealed class Case01Portal
    {
        public Case01Door Door;
        public Vector3 PlanePoint;
        public Vector3 Normal;
        public Vector3 Right;
        public float HalfWidth;
        public float Height;
        public GameObject Membrane;
        public GameObject Seal;

        /// <summary>從 prev 移動到 cur 是否由來源側穿過門洞。</summary>
        public bool Crossed(Vector3 prev, Vector3 cur)
        {
            float a = Vector3.Dot(prev - PlanePoint, Normal);
            float b = Vector3.Dot(cur - PlanePoint, Normal);
            if (!(a < 0f && b >= 0f)) return false;
            var d = cur - PlanePoint;
            if (Mathf.Abs(Vector3.Dot(d, Right)) > HalfWidth) return false;
            return d.y > -0.6f && d.y < Height;
        }
    }

    /// <summary>建好的室內 QA 空間中，流程需要操作的物件與資料。</summary>
    public sealed class Case01HouseLayout
    {
        public Transform Root;
        public Case01Pose Spawn;
        public readonly List<Case01RoomVolume> Rooms = new List<Case01RoomVolume>();
        public readonly Dictionary<Case01Room, Case01Pose> Arrivals = new Dictionary<Case01Room, Case01Pose>();
        public readonly List<Case01Portal> Portals = new List<Case01Portal>();
        public readonly Dictionary<string, Case01Interactable> Interactables = new Dictionary<string, Case01Interactable>();
        public readonly List<Light> HouseLights = new List<Light>();

        public GameObject DiningSecondSet;
        public GameObject DiningTrace;
        public Vector3 TablePoint;
        public Renderer PhotoSecondFigure;
        public Material PhotoFigureMat;
        public Material PhotoFadedMat;
        public Light LandingLight;
        public GameObject ThirdDoorFrame;
        public Text StudyMarkLabel;
        public Text FrontDoorLabel;
        public AudioSource Creak;

        public Transform Core;
        public Material CoreMat;
        public Light CoreLight;
        public readonly List<Light> MezzLights = new List<Light>();
        public readonly List<Material> MezzLineMats = new List<Material>();
        public GameObject Door17Frame;
        public readonly List<Transform> Overlaps = new List<Transform>();
        public readonly List<Quaternion> OverlapSkew = new List<Quaternion>();
        public readonly List<Transform> Floating = new List<Transform>();
        public readonly List<Vector3> FloatingHome = new List<Vector3>();

        /// <summary>本次建置的材質（重建／離開時 Dispose）。</summary>
        public Case01Palette Palette;
        /// <summary>各房間的主燈與其基準顏色／亮度（H1～H3 依此調整，不累積漂移）。</summary>
        public readonly Dictionary<Case01Room, Light> RoomLights = new Dictionary<Case01Room, Light>();
        public readonly Dictionary<Case01Room, Color> RoomLightColor = new Dictionary<Case01Room, Color>();
        public readonly Dictionary<Case01Room, float> RoomLightIntensity = new Dictionary<Case01Room, float>();
        public readonly List<Light> LampLights = new List<Light>();
        public Transform DiningChairNorth;
        public Case01Decor Decor;

        public Transform EncounterAnchor;
        public Light CourtyardLight;
        public GameObject TearFrame;

        public Case01Portal Portal(Case01Door door)
        {
            foreach (var p in Portals) if (p.Door == door) return p;
            return null;
        }

        public Case01Room RoomAt(Vector3 feet)
        {
            var p = feet + Vector3.up * 0.3f;
            foreach (var r in Rooms) if (r.Bounds.Contains(p)) return r.Room;
            return Case01Room.None;
        }
    }

    /// <summary>
    /// 十七號室內 Graybox（只用 Primitive 與 Standard 材質）。單位：公尺；+z 為北，街道在南（z＜0）。
    /// 一樓 10×10（玄關／樓梯、廚房、客廳、餐廳），二樓（樓梯平台、書房、臥室、儲藏室），
    /// 另有兩個「不存在」的空間：夾層（x≈40）與異常中庭（z≈24～58），只能經由錯接的門到達。
    /// 牆、家具保留碰撞體；可調查物件是 Trigger 熱區。實際座標見 <see cref="Case01HouseBuilder"/> 的常數。
    /// </summary>
    public static class Case01HouseBuilder
    {
        public const float FloorHeight = 3f;
        public const float DoorWidth = 1.1f;
        public const float DoorHeight = 2.2f;
        public const float WallThickness = 0.15f;
        public static readonly Vector3 SpawnPoint = new Vector3(7.1f, 0.05f, 1.6f);
        public static readonly Vector3 MezzCenter = new Vector3(40f, 0f, 0f);
        public static readonly Vector3 EncounterAnchorPosition = new Vector3(40f, 0f, 46f);
        public const float StudyDoorX = 6.95f;
        public const float StairX0 = 8.8f;
        public const float StairZ0 = 1.0f;
        public const float StairZ1 = 4.6f;

        private static Case01Palette P;
        private static bool trimWalls;
        private static Material wallMat, wallUp, floorWood, ceilMat, trim, wood, darkWood, fabric, white, metal, paper, glassNight;
        private static Material riftMat, frameGlow, membraneMat, mezzFloor, mezzWall, courtFloor, courtWall, crystal;

        public static Case01HouseLayout Build(Transform parent, Case01Palette palette = null)
        {
            var L = new Case01HouseLayout();
            var root = ArenaArt.Group("Case01House", parent, Vector3.zero);
            L.Root = root;
            L.Spawn = new Case01Pose(SpawnPoint, 0f);
            L.Palette = palette ?? new Case01Palette();
            P = L.Palette;
            Materials();

            BuildRooms(L);
            trimWalls = true;
            BuildGroundFloor(L, ArenaArt.Group("GroundFloor", root, Vector3.zero));
            BuildStairs(L, ArenaArt.Group("Stairs", root, Vector3.zero));
            BuildSecondFloor(L, ArenaArt.Group("SecondFloor", root, Vector3.zero));
            trimWalls = false;
            BuildMezzanine(L, ArenaArt.Group("Mezzanine", root, Vector3.zero));
            BuildCourtyard(L, ArenaArt.Group("Courtyard", root, Vector3.zero));

            // v0.2：生活細節、階段異變物件與校正後證物（只加在現有空間內，不改牆、門、樓梯與熱區）。
            L.Decor = Case01HouseDecor.Build(L, ArenaArt.Group("LifeDetails", root, Vector3.zero));

            L.Arrivals[Case01Room.Entrance] = new Case01Pose(SpawnPoint, 0f);
            L.Arrivals[Case01Room.Dining] = new Case01Pose(new Vector3(6.5f, 0.05f, 5.8f), 0f);
            L.Arrivals[Case01Room.Study] = new Case01Pose(new Vector3(StudyDoorX, FloorHeight + 0.05f, 5.8f), 0f);
            L.Arrivals[Case01Room.Landing] = new Case01Pose(new Vector3(StudyDoorX, FloorHeight + 0.05f, 0.95f), 0f);
            L.Arrivals[Case01Room.Mezzanine] = new Case01Pose(MezzCenter + new Vector3(0f, 0.05f, -4.7f), 0f);
            L.Arrivals[Case01Room.Courtyard] = new Case01Pose(new Vector3(40f, 0.05f, 27.5f), 0f);
            return L;
        }

        /// <summary>從 Case01 材質資產取得主要材質（共用資產，不在執行時修改）。</summary>
        private static void Materials()
        {
            wallMat = P.Get("wall_plaster");
            wallUp = P.Get("wall_paper");
            floorWood = P.Get("floor_wood");
            ceilMat = P.Get("ceiling");
            trim = P.Get("trim_paint");
            wood = P.Get("wood_warm");
            darkWood = P.Get("wood_dark");
            fabric = P.Get("fabric_blue");
            white = P.Get("ceramic");
            metal = P.Get("brass");
            paper = P.Get("paper");
            glassNight = P.Get("glass_night");
            riftMat = P.Get("rift");
            frameGlow = P.Get("frame_glow");
            membraneMat = P.Own(ArenaArt.FadeMat(new Color(0.35f, 0.15f, 0.6f, 0.35f), ArenaArt.Rift * 0.6f));
            mezzFloor = P.Get("mezz_floor");
            mezzWall = P.Get("mezz_wall");
            courtFloor = P.Get("court_floor");
            courtWall = P.Get("court_wall");
            crystal = P.Get("crystal");
        }

        /// <summary>本次建置擁有的純色小道具材質（重建時釋放）。</summary>
        private static Material M(Color c, float smooth = 0.15f, Color? emission = null, float metallic = 0f)
        {
            return P.Own(ArenaArt.Mat(c, smooth, emission, metallic));
        }

        // ---------------- 房間範圍 ----------------

        private static void BuildRooms(Case01HouseLayout L)
        {
            Room(L, Case01Room.Kitchen, 0, 5, 0, 5, -1f, 2.75f);
            Room(L, Case01Room.Entrance, 5, 10, 0, 5, -1f, 2.75f);
            Room(L, Case01Room.Living, 0, 5, 5, 10, -1f, 2.75f);
            Room(L, Case01Room.Dining, 5, 10, 5, 10, -1f, 2.75f);
            Room(L, Case01Room.Storage, 0, 5, 0, 5, 2.75f, 6.5f);
            Room(L, Case01Room.Landing, 5, 10, -0.8f, 5, 2.75f, 6.5f);
            Room(L, Case01Room.Bedroom, 0, 5, 5, 10, 2.75f, 6.5f);
            Room(L, Case01Room.Study, 5, 10, 5, 10, 2.75f, 6.5f);
            Room(L, Case01Room.Mezzanine, 33f, 47f, -7f, 7f, -1f, 6f);
            Room(L, Case01Room.Courtyard, 25f, 55f, 23f, 59f, -1f, 8f);
        }

        private static void Room(Case01HouseLayout L, Case01Room room, float x0, float x1, float z0, float z1, float y0, float y1)
        {
            var b = new Bounds();
            b.SetMinMax(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1));
            L.Rooms.Add(new Case01RoomVolume { Room = room, Bounds = b });
        }

        // ---------------- 一樓 ----------------

        private static void BuildGroundFloor(Case01HouseLayout L, Transform g)
        {
            Solid(g, new Vector3(5f, -0.1f, 5f), new Vector3(10.4f, 0.2f, 10.4f), floorWood, "Floor");
            // 外牆（兩層高）；前門在南牆 x=7.1。
            WallZ(g, 0f, 0f, 10f, 0f, FloorHeight, wallMat, 7.1f);
            WallZ(g, 10f, 0f, 10f, 0f, FloorHeight, wallMat);
            WallX(g, 0f, 0f, 10f, 0f, FloorHeight, wallMat);
            WallX(g, 10f, 0f, 10f, 0f, FloorHeight, wallMat);
            // 內牆：x=5（廚房↔玄關 z2.5、客廳↔餐廳 z7.5），z=5（廚房↔客廳 x2.5、玄關↔餐廳 x6.5）。
            WallX(g, 5f, 0f, 10f, 0f, 2.8f, wallMat, 2.5f, 7.5f);
            WallZ(g, 5f, 0f, 10f, 0f, 2.8f, wallMat, 2.5f, 6.5f);
            // 夜晚的窗。
            Window(g, new Vector3(2.5f, 1.5f, 0.09f), 0f);
            Window(g, new Vector3(2.5f, 1.5f, 9.91f), 0f);
            Window(g, new Vector3(7.5f, 1.5f, 9.91f), 0f);
            Window(g, new Vector3(0.09f, 1.5f, 7.5f), 90f);

            // ---- 玄關 ----
            var door = Solid(g, new Vector3(7.1f, DoorHeight * 0.5f, 0.02f), new Vector3(DoorWidth, DoorHeight, 0.08f), darkWood, "FrontDoor");
            Deco(door, new Vector3(0.35f, 0f, 0.8f), new Vector3(0.06f, 0.04f, 1.5f), metal, "Knob");
            Hotspot(L, g, Case01Ids.FrontDoor, "前門（QA 返回／重來）", new Vector3(7.1f, 1.1f, 0.15f), new Vector3(1.1f, 2.0f, 0.12f));
            L.FrontDoorLabel = Case01Hud.WorldLabel(g, new Vector3(7.1f, 2.5f, 0.09f), 180f, "QA 返回／重來", 26, new Color(1f, 0.85f, 0.5f), 220f);
            Solid(g, new Vector3(5.6f, 0.45f, 0.35f), new Vector3(0.8f, 0.9f, 0.4f), wood, "ShoeCabinet");
            RoomLight(L, Case01Room.Entrance, g, new Vector3(7f, 2.35f, 2.5f), new Color(1f, 0.78f, 0.52f), 1.0f, false, 2.8f);

            // ---- 廚房 ----
            Solid(g, new Vector3(0.45f, 0.45f, 2.3f), new Vector3(0.7f, 0.9f, 3.0f), white, "Counter");
            Solid(g, new Vector3(0.45f, 0.9f, 4.3f), new Vector3(0.7f, 1.8f, 0.7f), white, "Fridge");
            Deco(g, new Vector3(0.82f, 1.35f, 4.3f), new Vector3(0.02f, 0.18f, 0.14f), paper, "FridgeNote");
            Hotspot(L, g, Case01Ids.SundayNote, "冰箱上的紙條", new Vector3(0.9f, 1.35f, 4.3f), new Vector3(0.2f, 0.5f, 0.5f));
            Prim(PrimitiveType.Cylinder, g, new Vector3(0.55f, 0.95f, 1.8f), new Vector3(0.08f, 0.05f, 0.08f), white, "Cup");
            Prim(PrimitiveType.Cylinder, g, new Vector3(0.6f, 0.95f, 2.05f), new Vector3(0.08f, 0.05f, 0.08f), white, "Cup");
            Hotspot(L, g, Case01Ids.Tea, "茶杯", new Vector3(0.6f, 1.0f, 1.95f), new Vector3(0.5f, 0.3f, 0.6f));
            RoomLight(L, Case01Room.Kitchen, g, new Vector3(2.5f, 2.35f, 2.5f), new Color(1f, 0.86f, 0.66f), 0.95f, false, 2.8f);

            // ---- 客廳 ----
            Solid(g, new Vector3(2.5f, 0.4f, 9.3f), new Vector3(2.2f, 0.8f, 0.8f), fabric, "Sofa");
            Solid(g, new Vector3(2.5f, 0.2f, 8.1f), new Vector3(1.2f, 0.4f, 0.6f), wood, "CoffeeTable");
            Deco(g, new Vector3(0.09f, 1.5f, 6.5f), new Vector3(0.02f, 0.55f, 0.42f), paper, "Calendar");
            Hotspot(L, g, Case01Ids.Calendar, "日曆", new Vector3(0.15f, 1.5f, 6.5f), new Vector3(0.12f, 0.7f, 0.6f));
            RoomLight(L, Case01Room.Living, g, new Vector3(2.5f, 2.3f, 7.5f), new Color(1f, 0.74f, 0.46f), 1.0f, true, 2.8f);

            // ---- 餐廳 ----
            var table = ArenaArt.Group("DiningTable", g, new Vector3(7.5f, 0f, 7.6f));
            Solid(table, new Vector3(0f, 0.72f, 0f), new Vector3(1.6f, 0.06f, 1.2f), wood, "Top");
            for (int i = 0; i < 4; i++)
                Deco(table, new Vector3(i < 2 ? -0.7f : 0.7f, 0.35f, i % 2 == 0 ? -0.5f : 0.5f), new Vector3(0.07f, 0.7f, 0.07f), darkWood, "Leg");
            var block = new GameObject("TableBlock").AddComponent<BoxCollider>();
            block.transform.SetParent(table, false);
            block.center = new Vector3(0f, 0.375f, 0f);
            block.size = new Vector3(1.6f, 0.75f, 1.2f);
            Solid(table, new Vector3(0f, 0.25f, -0.95f), new Vector3(0.45f, 0.5f, 0.45f), darkWood, "ChairSouth");
            L.DiningChairNorth = Solid(table, new Vector3(0f, 0.25f, 0.95f), new Vector3(0.45f, 0.5f, 0.45f), darkWood, "ChairNorth");
            Setting(table, -0.32f, "SetSouth");
            L.DiningSecondSet = Setting(table, 0.32f, "SetNorth").gameObject;
            // 再訪時：收走的那一側，桌面比較乾淨。
            L.DiningTrace = Deco(table, new Vector3(0f, 0.753f, 0.32f), new Vector3(0.55f, 0.005f, 0.42f),
                P.Tint("wood_warm", new Color(1.15f, 1f, 0.85f)), "CleanPatch").gameObject;
            L.DiningTrace.SetActive(false);
            L.TablePoint = table.position + Vector3.up * 0.95f;
            Hotspot(L, table, Case01Ids.Table, "餐桌", new Vector3(0f, 0.95f, 0f), new Vector3(1.6f, 0.4f, 1.2f));

            Solid(g, new Vector3(9.65f, 0.45f, 7.6f), new Vector3(0.5f, 0.9f, 1.6f), darkWood, "Sideboard");
            var frame = ArenaArt.Group("PhotoFrame", g, new Vector3(9.6f, 1.12f, 7.6f));
            // 正面（本地 -z）朝西，面向餐廳。
            frame.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Deco(frame, Vector3.zero, new Vector3(0.5f, 0.38f, 0.03f), darkWood, "Frame");
            Deco(frame, new Vector3(0f, 0f, -0.016f), new Vector3(0.42f, 0.3f, 0.005f), P.Get("paper_old"), "Print");
            L.PhotoFigureMat = M(new Color(0.25f, 0.22f, 0.22f), 0.1f);
            L.PhotoFadedMat = M(new Color(0.6f, 0.58f, 0.53f), 0.1f);
            Deco(frame, new Vector3(-0.08f, -0.02f, -0.02f), new Vector3(0.07f, 0.2f, 0.005f), L.PhotoFigureMat, "FigureA");
            L.PhotoSecondFigure = Deco(frame, new Vector3(0.08f, -0.03f, -0.02f), new Vector3(0.065f, 0.18f, 0.005f), L.PhotoFigureMat, "FigureB")
                .GetComponent<Renderer>();
            Case01Hud.WorldLabel(frame, new Vector3(0f, -0.25f, -0.02f), 0f, "合照（佔位）", 14, new Color(0.9f, 0.88f, 0.8f), 120f);
            Hotspot(L, g, Case01Ids.Photo, "合照", new Vector3(9.5f, 1.15f, 7.6f), new Vector3(0.3f, 0.55f, 0.7f));
            RoomLight(L, Case01Room.Dining, g, new Vector3(7.5f, 2.2f, 7.6f), new Color(1f, 0.72f, 0.42f), 1.2f, true, 2.8f);

            // 天花板（二樓地板）見 BuildSecondFloor。
        }

        /// <summary>一副餐具：盤、杯、刀叉。z＝相對桌面中心的位置（南負北正）。</summary>
        private static Transform Setting(Transform table, float z, string name)
        {
            var s = ArenaArt.Group(name, table, new Vector3(0f, 0.75f, z));
            Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 0.01f, 0f), new Vector3(0.28f, 0.01f, 0.28f), white, "Plate");
            Prim(PrimitiveType.Cylinder, s, new Vector3(0.28f, 0.06f, 0f), new Vector3(0.08f, 0.06f, 0.08f), white, "Cup");
            Prim(PrimitiveType.Cube, s, new Vector3(-0.2f, 0.01f, 0f), new Vector3(0.02f, 0.01f, 0.2f), metal, "Fork");
            Prim(PrimitiveType.Cube, s, new Vector3(0.18f, 0.01f, 0.1f), new Vector3(0.02f, 0.01f, 0.2f), metal, "Knife");
            return s;
        }

        // ---------------- 樓梯 ----------------

        private static void BuildStairs(Case01HouseLayout L, Transform g)
        {
            float run = StairZ1 - StairZ0;
            float width = 10f - StairX0;
            float cx = StairX0 + width * 0.5f;
            const int steps = 12;
            float rise = FloorHeight / steps, depth = run / steps;
            // 看得到的踏階（無碰撞體，實心到地面）。
            for (int i = 0; i < steps; i++)
            {
                float top = (i + 1) * rise;
                Deco(g, new Vector3(cx, top * 0.5f, StairZ0 + (i + 0.5f) * depth), new Vector3(width, top, depth), i % 2 == 0 ? wood : darkWood, "Step", true);
            }
            // 走路用的斜坡碰撞體（約 40°，CharacterController 可直接走上去）。
            float len = Mathf.Sqrt(run * run + FloorHeight * FloorHeight);
            float ang = Mathf.Atan2(FloorHeight, run) * Mathf.Rad2Deg;
            var ramp = new GameObject("StairRamp");
            ramp.transform.SetParent(g, false);
            ramp.transform.localRotation = Quaternion.Euler(-ang, 0f, 0f);
            var up = ramp.transform.up;
            ramp.transform.localPosition = new Vector3(cx, FloorHeight * 0.5f, (StairZ0 + StairZ1) * 0.5f) - up * 0.1f;
            ramp.AddComponent<BoxCollider>().size = new Vector3(width, 0.2f, len);
            // 樓梯下方封住（不能鑽到樓梯底下）。
            var under = new GameObject("UnderStairBlock").AddComponent<BoxCollider>();
            under.transform.SetParent(g, false);
            under.center = new Vector3(cx, 0.65f, 3.6f);
            under.size = new Vector3(width, 1.3f, 2.0f);
        }

        // ---------------- 二樓 ----------------

        private static void BuildSecondFloor(Case01HouseLayout L, Transform g)
        {
            float y = FloorHeight;
            // 樓板（一樓天花板）：樓梯井 x8.8～10、z1.0～4.6 留空。
            Slab(g, 0f, 10f, 5f, 10f, y);
            Slab(g, 0f, StairX0, 0f, 5f, y);
            Slab(g, StairX0, 10f, 0f, StairZ0, y);
            Slab(g, StairX0, 10f, StairZ1, 5f, y);
            // 屋頂。
            Solid(g, new Vector3(5f, 2f * FloorHeight + 0.1f, 5f), new Vector3(10.4f, 0.2f, 10.4f), ceilMat, "Roof");
            // 外牆：南牆 x6.95 是「第三扇門」的位置（平時封住）。
            WallZ(g, 0f, 0f, 10f, y, FloorHeight, wallUp, StudyDoorX);
            WallZ(g, 10f, 0f, 10f, y, FloorHeight, wallUp);
            WallX(g, 0f, 0f, 10f, y, FloorHeight, wallUp);
            WallX(g, 10f, 0f, 10f, y, FloorHeight, wallUp);
            // 內牆：x=5（儲藏室門 z2.5、臥室↔書房 z7.5），z=5（樓梯平台↔書房 x6.95）。
            WallX(g, 5f, 0f, 10f, y, FloorHeight, wallUp, 2.5f, 7.5f);
            WallZ(g, 5f, 0f, 10f, y, FloorHeight, wallUp, StudyDoorX);
            Window(g, new Vector3(2.5f, y + 1.5f, 9.91f), 0f);
            Window(g, new Vector3(8.5f, y + 1.5f, 9.91f), 0f);
            Window(g, new Vector3(9.91f, y + 1.5f, 7.5f), 90f);

            // 樓梯井欄杆。
            Solid(g, new Vector3(StairX0, y + 0.5f, (StairZ0 + 4.0f) * 0.5f), new Vector3(0.08f, 1.0f, 4.0f - StairZ0), darkWood, "Rail");
            Solid(g, new Vector3((StairX0 + 10f) * 0.5f, y + 0.5f, StairZ0), new Vector3(10f - StairX0, 1.0f, 0.08f), darkWood, "Rail");

            // ---- 樓梯平台 ----
            L.LandingLight = RoomLight(L, Case01Room.Landing, g, new Vector3(7f, y + 2.35f, 2.5f), new Color(0.86f, 0.88f, 1f), 0.95f, false, y + 2.8f);
            // 書房門左側門框上的記號（樓梯平台側）。
            L.StudyMarkLabel = Case01Hud.WorldLabel(g, new Vector3(StudyDoorX - 0.9f, y + 1.6f, 4.91f), 0f, "△", 40, new Color(0.95f, 0.95f, 0.9f), 80f);
            Hotspot(L, g, Case01Ids.StudyMark, "書房門框上的記號", new Vector3(StudyDoorX - 0.9f, y + 1.6f, 4.85f), new Vector3(0.5f, 0.7f, 0.12f));
            var studyPortal = Portal(L, g, Case01Door.StudyDoor, new Vector3(StudyDoorX, y, 5f), Vector3.forward, DoorWidth, DoorHeight, false, false);
            studyPortal.Membrane.SetActive(false);
            // 第三扇門：外牆上的門框（異常時浮現）、封口、門後擋牆。
            L.ThirdDoorFrame = DoorFrame(g, new Vector3(StudyDoorX, y, 0.1f), 0f, DoorWidth, DoorHeight, frameGlow, "ThirdDoorFrame");
            L.ThirdDoorFrame.SetActive(false);
            Portal(L, g, Case01Door.ThirdDoor, new Vector3(StudyDoorX, y, 0f), Vector3.back, DoorWidth, DoorHeight, true, true);
            Hotspot(L, g, Case01Ids.ThirdDoor, "外牆上的門框", new Vector3(StudyDoorX, y + 1.1f, 0.2f), new Vector3(1.1f, 2.0f, 0.1f));
            L.Interactables[Case01Ids.ThirdDoor].gameObject.SetActive(false);
            // 儲藏室（不開放）。
            Solid(g, new Vector3(5.02f, y + DoorHeight * 0.5f, 2.5f), new Vector3(0.08f, DoorHeight, DoorWidth), darkWood, "StorageDoor");
            Hotspot(L, g, Case01Ids.StorageDoor, "儲藏室的門", new Vector3(5.15f, y + 1.1f, 2.5f), new Vector3(0.12f, 2.0f, 1.1f));
            var creakGo = new GameObject("LandingCreak");
            creakGo.transform.SetParent(g, false);
            creakGo.transform.localPosition = new Vector3(7f, y + 1.5f, 2f);
            L.Creak = creakGo.AddComponent<AudioSource>();
            L.Creak.playOnAwake = false;
            L.Creak.spatialBlend = 0.6f;
            L.Creak.clip = CreakClip();

            // ---- 書房 ----
            Solid(g, new Vector3(7.5f, y + 0.375f, 9.4f), new Vector3(1.9f, 0.75f, 0.8f), darkWood, "Desk");
            Deco(g, new Vector3(6.9f, y + 0.76f, 9.35f), new Vector3(0.5f, 0.01f, 0.36f), paper, "OldMap");
            Deco(g, new Vector3(6.85f, y + 0.77f, 9.35f), new Vector3(0.02f, 0.012f, 0.3f), riftMat, "MapPencil17");
            Hotspot(L, g, Case01Ids.Map, "街區舊地圖", new Vector3(6.85f, y + 0.9f, 9.3f), new Vector3(0.55f, 0.3f, 0.5f));
            Deco(g, new Vector3(7.75f, y + 0.79f, 9.35f), new Vector3(0.3f, 0.05f, 0.22f), P.Get("book_0"), "Notebook");
            Hotspot(L, g, Case01Ids.EllieNotes, "艾琳的手記", new Vector3(7.75f, y + 0.9f, 9.3f), new Vector3(0.45f, 0.3f, 0.5f));
            Solid(g, new Vector3(9.7f, y + 1.1f, 7.5f), new Vector3(0.45f, 2.2f, 2.0f), wood, "Bookshelf");
            Deco(g, new Vector3(9.45f, y + 1.35f, 7.5f), new Vector3(0.04f, 0.32f, 0.26f), P.Tint("cardboard", new Color(0.45f, 0.45f, 0.5f)), "RecordFolder");
            Deco(g, new Vector3(9.43f, y + 1.35f, 7.5f), new Vector3(0.01f, 0.12f, 0.2f), riftMat, "BlackedOut");
            Hotspot(L, g, Case01Ids.OldRecord, "書架上的校正記錄", new Vector3(9.35f, y + 1.35f, 7.5f), new Vector3(0.25f, 0.5f, 0.5f));
            Solid(g, new Vector3(7.5f, y + 0.25f, 8.7f), new Vector3(0.45f, 0.5f, 0.45f), darkWood, "DeskChair");
            RoomLight(L, Case01Room.Study, g, new Vector3(7.5f, y + 2.3f, 7.5f), new Color(1f, 0.93f, 0.82f), 1.05f, false, y + 2.8f);

            // ---- 臥室 ----
            Solid(g, new Vector3(2.4f, y + 0.25f, 8.6f), new Vector3(1.6f, 0.5f, 2.0f), fabric, "Bed");
            Solid(g, new Vector3(1.1f, y + 0.3f, 9.55f), new Vector3(0.5f, 0.6f, 0.5f), wood, "Nightstand");
            Deco(g, new Vector3(1.1f, y + 0.61f, 9.55f), new Vector3(0.16f, 0.01f, 0.11f), paper, "Postcard");
            Hotspot(L, g, Case01Ids.Postcard, "明信片", new Vector3(1.1f, y + 0.75f, 9.5f), new Vector3(0.5f, 0.3f, 0.5f));
            RoomLight(L, Case01Room.Bedroom, g, new Vector3(2.5f, y + 2.3f, 7.5f), new Color(1f, 0.76f, 0.5f), 0.75f, false, y + 2.8f);
        }

        private static AudioClip CreakClip()
        {
            const int rate = 22050;
            int n = rate * 3 / 2;
            var data = new float[n];
            var rng = new System.Random(17);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Sin(Mathf.PI * t / 1.5f);
                float tone = Mathf.Sin(2f * Mathf.PI * (70f + 18f * Mathf.Sin(t * 5f)) * t);
                float grit = ((float)rng.NextDouble() * 2f - 1f) * 0.25f * Mathf.Abs(Mathf.Sin(t * 37f));
                data[i] = (tone * 0.5f + grit) * env * 0.6f;
            }
            var clip = AudioClip.Create("Case01Creak", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ---------------- 不存在的夾層 ----------------

        private static void BuildMezzanine(Case01HouseLayout L, Transform g)
        {
            var c = MezzCenter;
            const float h = 4.5f;
            Solid(g, c + new Vector3(0f, -0.1f, 0f), new Vector3(12.4f, 0.2f, 12.4f), mezzFloor, "Floor");
            WallZ(g, c.z - 6f, c.x - 6f, c.x + 6f, 0f, h, mezzWall, c.x);
            WallZ(g, c.z + 6f, c.x - 6f, c.x + 6f, 0f, h, mezzWall, c.x);
            WallX(g, c.x - 6f, c.z - 6f, c.z + 6f, 0f, h, mezzWall, c.z);
            WallX(g, c.x + 6f, c.z - 6f, c.z + 6f, 0f, h, mezzWall, c.z);

            Portal(L, g, Case01Door.MezzBack, c + new Vector3(0f, 0f, -6f), Vector3.back, DoorWidth, DoorHeight, false, true);
            Portal(L, g, Case01Door.MezzTriangle, c + new Vector3(-6f, 0f, 0f), Vector3.left, DoorWidth, DoorHeight, false, true);
            Portal(L, g, Case01Door.MezzCircle, c + new Vector3(6f, 0f, 0f), Vector3.right, DoorWidth, DoorHeight, false, true);
            Portal(L, g, Case01Door.Mezz17, c + new Vector3(0f, 0f, 6f), Vector3.forward, DoorWidth, DoorHeight, true, true);
            L.Door17Frame = DoorFrame(g, c + new Vector3(0f, 0f, 5.9f), 0f, DoorWidth, DoorHeight, frameGlow, "Door17Frame");
            L.Door17Frame.SetActive(false);

            var labelColor = new Color(0.95f, 0.92f, 1f);
            Case01Hud.WorldLabel(g, c + new Vector3(0f, 2.75f, -5.91f), 180f, "返回樓梯平台", 26, labelColor, 220f);
            Case01Hud.WorldLabel(g, c + new Vector3(-5.91f, 2.75f, 0f), -90f, "△", 48, labelColor, 80f);
            Case01Hud.WorldLabel(g, c + new Vector3(5.91f, 2.75f, 0f), 90f, "○", 48, labelColor, 80f);
            Case01Hud.WorldLabel(g, c + new Vector3(0f, 2.75f, 5.91f), 0f, "□ 17", 40, labelColor, 160f);
            Hotspot(L, g, Case01Ids.MezzTriangleMark, "△ 門", c + new Vector3(-5.85f, 1.5f, -0.95f), new Vector3(0.12f, 1.2f, 0.6f));
            Hotspot(L, g, Case01Ids.MezzCircleMark, "○ 門", c + new Vector3(5.85f, 1.5f, 0.95f), new Vector3(0.12f, 1.2f, 0.6f));
            Hotspot(L, g, Case01Ids.Wall17, "北牆的「17」", c + new Vector3(0f, 1.1f, 5.82f), new Vector3(1.3f, 2.0f, 0.12f));

            // 重疊的房門／房間框架（錯位）：核穩定後轉回原位。
            Vector3[] fp = { new Vector3(-3.2f, 0f, 3.6f), new Vector3(3.4f, 0f, 3.2f), new Vector3(-3.8f, 0f, -2.6f), new Vector3(3.6f, 0f, -2.8f), new Vector3(0f, 0f, 4.4f) };
            Vector3[] skew = { new Vector3(0f, 25f, 12f), new Vector3(0f, -32f, -9f), new Vector3(8f, 60f, 0f), new Vector3(-6f, -48f, 14f), new Vector3(0f, 12f, -18f) };
            for (int i = 0; i < fp.Length; i++)
            {
                var f = DoorFrame(g, c + fp[i], 0f, 1.6f, 2.6f, i % 2 == 0 ? trim : mezzWall, "OverlapFrame").transform;
                L.Overlaps.Add(f);
                L.OverlapSkew.Add(Quaternion.Euler(skew[i]));
                f.localRotation = Quaternion.Euler(skew[i]);
            }
            // 斷裂的舊術式標記（地面上的線）。
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f;
                var m = P.Instance("rift");
                L.MezzLineMats.Add(m);
                var d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                Deco(g, c + new Vector3(0f, 0.012f, 1.2f) + d * (i % 2 == 0 ? 1.6f : 2.2f), new Vector3(0.06f, 0.01f, i % 2 == 0 ? 1.4f : 0.8f), m, "BrokenLine",
                    false, new Vector3(0f, a, 0f));
            }

            // 核：幾何物（暫用，不是正式外觀）。
            var core = ArenaArt.Group("Core", g, c + new Vector3(0f, 0f, 1.2f));
            Solid(core, new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.6f, 0.9f), mezzWall, "Pedestal");
            L.CoreMat = P.Instance("core_body");
            L.Core = ArenaArt.Group("CoreShape", core, new Vector3(0f, 1.35f, 0f));
            Deco(L.Core, Vector3.zero, new Vector3(0.38f, 0.38f, 0.38f), L.CoreMat, "Cube", true, new Vector3(35f, 45f, 0f));
            Deco(L.Core, Vector3.zero, new Vector3(0.3f, 0.6f, 0.3f), L.CoreMat, "Shard", true, new Vector3(0f, 20f, 25f));
            L.CoreLight = ArenaArt.PointLight(core, new Vector3(0f, 1.4f, -0.6f), ArenaArt.Rift, 8f, 2.0f, false);
            Hotspot(L, core, Case01Ids.Core, "核（舊校正術式的斷口）", new Vector3(0f, 1.2f, 0f), new Vector3(1.3f, 1.6f, 1.3f));
            L.MezzLights.Add(ArenaArt.PointLight(g, c + new Vector3(0f, 3.8f, -3f), new Color(0.6f, 0.55f, 0.85f), 12f, 1.0f, false));
            L.MezzLights.Add(ArenaArt.PointLight(g, c + new Vector3(0f, 3.8f, 3.5f), new Color(0.6f, 0.55f, 0.85f), 12f, 0.8f, false));
        }

        // ---------------- 異常中庭（戰鬥舞台） ----------------

        private static void BuildCourtyard(Case01HouseLayout L, Transform g)
        {
            const float x0 = 26f, x1 = 54f, z0 = 24f, z1 = 58f, h = 7f;
            var mid = new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f);
            Solid(g, mid + new Vector3(0f, -0.1f, 0f), new Vector3(x1 - x0 + 0.4f, 0.2f, z1 - z0 + 0.4f), courtFloor, "Floor");
            WallZ(g, z0, x0, x1, 0f, h, courtWall, 40f);
            WallZ(g, z1, x0, x1, 0f, h, courtWall);
            WallX(g, x0, z0, z1, 0f, h, courtWall);
            WallX(g, x1, z0, z1, 0f, h, courtWall);
            var tear = Portal(L, g, Case01Door.CourtyardTear, new Vector3(40f, 0f, z0), Vector3.back, 1.4f, 2.6f, true, true);
            tear.Seal.GetComponent<Renderer>().sharedMaterial = riftMat;
            L.TearFrame = DoorFrame(g, new Vector3(40f, 0f, z0 + 0.1f), 0f, 1.4f, 2.6f, frameGlow, "TearFrame");
            Case01Hud.WorldLabel(g, new Vector3(40f, 3.1f, z0 + 0.09f), 180f, "裂口：返回夾層", 26, new Color(0.95f, 0.92f, 1f), 240f);

            // 重疊的房間碎片（在兩側，不擋戰術鏡頭）。
            var rng = new System.Random(1717);
            System.Func<float, float, float> R = (a, b) => a + (float)rng.NextDouble() * (b - a);
            for (int i = 0; i < 8; i++)
            {
                bool west = i % 2 == 0;
                var p = new Vector3(west ? R(28f, 32f) : R(48f, 52f), 0f, R(28f, 56f));
                var f = DoorFrame(g, p, R(-40f, 40f), 1.6f, 2.6f, i % 3 == 0 ? trim : courtWall, "FloatingRoomFrame");
                f.transform.localRotation = Quaternion.Euler(R(-12f, 12f), R(0f, 360f), R(-15f, 15f));
            }
            for (int i = 0; i < 4; i++)
            {
                Solid(g, new Vector3(i % 2 == 0 ? R(28.5f, 31f) : R(49f, 51.5f), 1.2f, R(30f, 55f)), new Vector3(R(2f, 3.5f), 2.4f, 0.2f), wallMat, "WallFragment",
                    new Vector3(0f, R(0f, 180f), R(-8f, 8f)));
            }
            for (int i = 0; i < 14; i++)
            {
                var s = Deco(g, new Vector3(R(28f, 52f), R(2.5f, 6.5f), R(30f, 57f)), Vector3.one * R(0.15f, 0.5f), crystal, "FloatingShard", false,
                    new Vector3(R(0, 360f), R(0, 360f), R(0, 360f)));
                L.Floating.Add(s);
                L.FloatingHome.Add(s.localPosition);
            }
            for (int i = 0; i < 10; i++)
                Deco(g, new Vector3(R(32f, 48f), 0.012f, R(40f, 55f)), new Vector3(R(0.05f, 0.14f), 0.02f, R(1.5f, 4f)), riftMat, "Crack", false,
                    new Vector3(0f, R(-80f, 80f), 0f));

            L.EncounterAnchor = ArenaArt.Group("EncounterAnchor", g, EncounterAnchorPosition);
            L.CourtyardLight = ArenaArt.PointLight(g, new Vector3(40.6f, 3.5f, 50f), ArenaArt.Rift, 18f, 2.2f, false);
            ArenaArt.PointLight(g, new Vector3(40f, 4f, 30f), new Color(0.7f, 0.65f, 0.9f), 14f, 1.1f, false);
            ArenaArt.PointLight(g, new Vector3(34f, 4f, 42f), new Color(0.55f, 0.45f, 0.85f), 12f, 0.9f, false);
            ArenaArt.PointLight(g, new Vector3(46f, 4f, 42f), new Color(0.55f, 0.45f, 0.85f), 12f, 0.9f, false);
        }

        // ---------------- 小工具 ----------------

        private static Transform Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, string name)
        {
            return ArenaArt.Prim(type, parent, pos, scale, mat, null, name, false, false);
        }

        private static Transform Solid(Transform parent, Vector3 pos, Vector3 scale, Material mat, string name, Vector3? euler = null)
        {
            return ArenaArt.Prim(PrimitiveType.Cube, parent, pos, scale, mat, euler, name, true, true);
        }

        private static Transform Deco(Transform parent, Vector3 pos, Vector3 scale, Material mat, string name, bool shadows = false, Vector3? euler = null)
        {
            return ArenaArt.Prim(PrimitiveType.Cube, parent, pos, scale, mat, euler, name, false, shadows);
        }

        private static void Slab(Transform g, float x0, float x1, float z0, float z1, float top)
        {
            Solid(g, new Vector3((x0 + x1) * 0.5f, top - 0.1f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.2f, z1 - z0), floorWood, "Slab");
            // 一樓天花板：樓板下方的灰泥面（只是外觀）。
            Deco(g, new Vector3((x0 + x1) * 0.5f, top - 0.205f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.01f, z1 - z0), ceilMat, "CeilingPlaster");
        }

        /// <summary>夜晚的窗：玻璃＋窗框、十字窗櫺與窗台（木作漆面）。</summary>
        private static void Window(Transform g, Vector3 pos, float yaw)
        {
            var w = ArenaArt.Group("Window", g, pos);
            w.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Deco(w, Vector3.zero, new Vector3(1.0f, 1.1f, 0.02f), glassNight, "Glass");
            Deco(w, new Vector3(0f, 0.58f, 0f), new Vector3(1.12f, 0.07f, 0.06f), trim, "FrameTop");
            Deco(w, new Vector3(0f, -0.58f, 0f), new Vector3(1.12f, 0.07f, 0.06f), trim, "FrameBottom");
            Deco(w, new Vector3(-0.53f, 0f, 0f), new Vector3(0.07f, 1.2f, 0.06f), trim, "FrameLeft");
            Deco(w, new Vector3(0.53f, 0f, 0f), new Vector3(0.07f, 1.2f, 0.06f), trim, "FrameRight");
            Deco(w, Vector3.zero, new Vector3(0.035f, 1.1f, 0.04f), trim, "MullionV");
            Deco(w, new Vector3(0f, 0.12f, 0f), new Vector3(1.0f, 0.035f, 0.04f), trim, "MullionH");
            Deco(w, new Vector3(0f, -0.63f, 0f), new Vector3(1.24f, 0.045f, 0.16f), trim, "Sill", true);
        }

        /// <summary>沿 x 方向（固定 z）的牆，openings＝門洞中心的 x。室內牆另加踢腳線與門框（只是外觀）。</summary>
        private static void WallZ(Transform g, float z, float x0, float x1, float y, float h, Material m, params float[] openings)
        {
            float o = WallThickness * 0.5f + 0.012f;
            Segments(g, x0, x1, openings, DoorWidth,
                (a, b) =>
                {
                    Solid(g, new Vector3((a + b) * 0.5f, y + h * 0.5f, z), new Vector3(b - a, h, WallThickness), m, "Wall");
                    if (!trimWalls) return;
                    Deco(g, new Vector3((a + b) * 0.5f, y + 0.07f, z - o), new Vector3(b - a, 0.14f, 0.025f), trim, "Skirting");
                    Deco(g, new Vector3((a + b) * 0.5f, y + 0.07f, z + o), new Vector3(b - a, 0.14f, 0.025f), trim, "Skirting");
                },
                (a, b) =>
                {
                    Solid(g, new Vector3((a + b) * 0.5f, y + DoorHeight + (h - DoorHeight) * 0.5f, z), new Vector3(b - a, h - DoorHeight, WallThickness), m, "Lintel");
                    if (!trimWalls || IsThirdDoor((a + b) * 0.5f, y, z)) return;
                    foreach (var s in new[] { -o, o })
                    {
                        Deco(g, new Vector3(a - 0.04f, y + DoorHeight * 0.5f, z + s), new Vector3(0.08f, DoorHeight, 0.03f), trim, "Casing");
                        Deco(g, new Vector3(b + 0.04f, y + DoorHeight * 0.5f, z + s), new Vector3(0.08f, DoorHeight, 0.03f), trim, "Casing");
                        Deco(g, new Vector3((a + b) * 0.5f, y + DoorHeight + 0.04f, z + s), new Vector3(b - a + 0.16f, 0.08f, 0.03f), trim, "Casing");
                    }
                });
        }

        /// <summary>沿 z 方向（固定 x）的牆，openings＝門洞中心的 z。</summary>
        private static void WallX(Transform g, float x, float z0, float z1, float y, float h, Material m, params float[] openings)
        {
            float o = WallThickness * 0.5f + 0.012f;
            Segments(g, z0, z1, openings, DoorWidth,
                (a, b) =>
                {
                    Solid(g, new Vector3(x, y + h * 0.5f, (a + b) * 0.5f), new Vector3(WallThickness, h, b - a), m, "Wall");
                    if (!trimWalls) return;
                    Deco(g, new Vector3(x - o, y + 0.07f, (a + b) * 0.5f), new Vector3(0.025f, 0.14f, b - a), trim, "Skirting");
                    Deco(g, new Vector3(x + o, y + 0.07f, (a + b) * 0.5f), new Vector3(0.025f, 0.14f, b - a), trim, "Skirting");
                },
                (a, b) =>
                {
                    Solid(g, new Vector3(x, y + DoorHeight + (h - DoorHeight) * 0.5f, (a + b) * 0.5f), new Vector3(WallThickness, h - DoorHeight, b - a), m, "Lintel");
                    if (!trimWalls) return;
                    foreach (var s in new[] { -o, o })
                    {
                        Deco(g, new Vector3(x + s, y + DoorHeight * 0.5f, a - 0.04f), new Vector3(0.03f, DoorHeight, 0.08f), trim, "Casing");
                        Deco(g, new Vector3(x + s, y + DoorHeight * 0.5f, b + 0.04f), new Vector3(0.03f, DoorHeight, 0.08f), trim, "Casing");
                        Deco(g, new Vector3(x + s, y + DoorHeight + 0.04f, (a + b) * 0.5f), new Vector3(0.03f, 0.08f, b - a + 0.16f), trim, "Casing");
                    }
                });
        }

        /// <summary>二樓外牆的第三扇門：平時是封住的牆，不加門框。</summary>
        private static bool IsThirdDoor(float x, float y, float z)
        {
            return Mathf.Abs(z) < 0.01f && y > 1f && Mathf.Abs(x - StudyDoorX) < 0.01f;
        }

        /// <summary>房間主燈：吊燈（電線、燈罩、燈泡）＋燈泡正下方的點光源。燈罩與燈泡不投影，避免擋住自己的光。</summary>
        private static Light RoomLight(Case01HouseLayout L, Case01Room room, Transform g, Vector3 pos, Color color, float intensity, bool shadows, float ceiling)
        {
            var fx = ArenaArt.Group("Pendant_" + room, g, pos);
            float cord = ceiling - (pos.y + 0.2f);
            ArenaArt.Prim(PrimitiveType.Cylinder, fx, new Vector3(0f, 0.2f + cord * 0.5f, 0f), new Vector3(0.015f, cord * 0.5f, 0.015f), darkWood, null, "Cord", false, false);
            ArenaArt.Prim(PrimitiveType.Cylinder, fx, new Vector3(0f, 0.13f, 0f), new Vector3(0.42f, 0.07f, 0.42f), P.Get("lamp_shade"), null, "Shade", false, false);
            ArenaArt.Prim(PrimitiveType.Sphere, fx, new Vector3(0f, 0.05f, 0f), new Vector3(0.1f, 0.1f, 0.1f), P.Get("glow"), null, "Bulb", false, false);
            var light = ArenaArt.PointLight(g, pos - Vector3.up * 0.05f, color, 7f, intensity, shadows);
            if (shadows)
            {
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.7f;
            }
            L.HouseLights.Add(light);
            L.RoomLights[room] = light;
            L.RoomLightColor[room] = color;
            L.RoomLightIntensity[room] = intensity;
            return light;
        }

        private static void Segments(Transform g, float a0, float a1, float[] openings, float w, System.Action<float, float> wall, System.Action<float, float> lintel)
        {
            var list = new List<float>(openings);
            list.Sort();
            float cur = a0;
            foreach (var o in list)
            {
                float s = o - w * 0.5f, e = o + w * 0.5f;
                if (s > cur) wall(cur, s);
                lintel(s, e);
                cur = e;
            }
            if (a1 > cur) wall(cur, a1);
        }

        /// <summary>門框（三根細條），只是外觀。</summary>
        private static GameObject DoorFrame(Transform g, Vector3 basePos, float yaw, float w, float h, Material m, string name)
        {
            var f = ArenaArt.Group(name, g, basePos);
            f.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Deco(f, new Vector3(-w * 0.5f - 0.05f, h * 0.5f, 0f), new Vector3(0.1f, h, 0.1f), m, "Jamb", true);
            Deco(f, new Vector3(w * 0.5f + 0.05f, h * 0.5f, 0f), new Vector3(0.1f, h, 0.1f), m, "Jamb", true);
            Deco(f, new Vector3(0f, h + 0.05f, 0f), new Vector3(w + 0.2f, 0.1f, 0.1f), m, "Head", true);
            return f.gameObject;
        }

        /// <summary>
        /// 建立通路：門洞平面、門後的半透明薄膜（目的地看不見）、可選的封口（實心、有碰撞體），以及門後的不可見擋牆。
        /// normal 指向門的另一側（目的地方向）。
        /// </summary>
        private static Case01Portal Portal(Case01HouseLayout L, Transform g, Case01Door door, Vector3 plane, Vector3 normal, float w, float h, bool seal,
            bool blocker)
        {
            var right = Vector3.Cross(Vector3.up, normal).normalized;
            var p = new Case01Portal { Door = door, PlanePoint = plane, Normal = normal, Right = right, HalfWidth = w * 0.5f, Height = h };
            var rot = Quaternion.LookRotation(normal, Vector3.up);
            var mem = ArenaArt.Prim(PrimitiveType.Cube, g, plane + normal * 0.06f + Vector3.up * h * 0.5f, new Vector3(w, h, 0.02f), membraneMat, null,
                "PortalMembrane_" + door, false, false);
            mem.localRotation = rot;
            p.Membrane = mem.gameObject;
            if (seal)
            {
                var s = ArenaArt.Prim(PrimitiveType.Cube, g, plane + Vector3.up * h * 0.5f, new Vector3(w, h, WallThickness), wallUp, null,
                    "PortalSeal_" + door, true, true);
                s.localRotation = rot;
                p.Seal = s.gameObject;
            }
            if (blocker)
            {
                var b = new GameObject("PortalBackstop_" + door).AddComponent<BoxCollider>();
                b.transform.SetParent(g, false);
                b.transform.localPosition = plane + normal * 0.75f + Vector3.up * (h * 0.5f + 0.2f);
                b.transform.localRotation = rot;
                b.size = new Vector3(w + 0.8f, h + 0.4f, 0.3f);
            }
            L.Portals.Add(p);
            return p;
        }

        /// <summary>可調查物件的 Trigger 熱區。</summary>
        internal static Case01Interactable Hotspot(Case01HouseLayout L, Transform parent, string id, string prompt, Vector3 pos, Vector3 size)
        {
            var go = new GameObject("Interact_" + id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var c = go.AddComponent<BoxCollider>();
            c.isTrigger = true;
            c.size = size;
            var it = go.AddComponent<Case01Interactable>();
            it.Id = id;
            it.Prompt = prompt;
            L.Interactables[id] = it;
            return it;
        }
    }
}
