using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// v0.5 一樓兩個獨立可玩的格局（不取代原版／v0.4 B；由「住宅 v0.5 A/B QA」選用）：
    /// 先定房間功能與動線，再放主家具，最後才是小物；所有劇情物件 ID（餐桌、合照、日曆、紙條、茶杯、茶葉盒、書與便箋、卡片、購物清單、前門）、
    /// H0～H3 異變物件與錯接到餐廳的路線都保留。家具是結構清楚的自製低面數幾何（沒有外部模型），不是正式高精模。
    /// </summary>
    public static partial class Case01HouseBuilder
    {
        /// <summary>方案 A 的牆線（公尺）。</summary>
        public static class V05A
        {
            public const float HallX0 = 5.4f;   // 客廳／餐廳 與 玄關／廚房 的分界
            public const float HallX1 = 8.65f;  // 玄關 與 樓梯 的分界
            public const float HallZ1 = 4.4f;   // 玄關北牆（掛畫）＝廚房南界
            public const float LivingZ1 = 5.4f; // 客廳／餐廳 分界（寬開口）
            public const float LivingDoorZ = 1.4f, LivingDoorW = 1.4f;
            public const float DiningGapX = 2.7f, DiningGapW = 2.4f;
            public const float KitchenDoorX = 8.0f;
            public const float DiningKitchenDoorZ = 7.6f;
            public static readonly Vector3 Spawn = new Vector3(7.1f, 0.05f, 1.0f);
            public static readonly Vector3 TableCenter = new Vector3(2.7f, 0f, 7.8f);
        }

        public static bool IsV05(Case01Floorplan p)
        {
            return p == Case01Floorplan.V05A || p == Case01Floorplan.V05B;
        }

        private static Material pale, fabricRed, fabricGreen, paperOld, card, leafM, potM, cardboardM, glowM, shadeM, frameM, tileM, darkM, steelM, stoneM;
        private static Material[] bookM;

        private static void MaterialsV05()
        {
            pale = P.Get("fabric_cream");
            fabricRed = P.Get("fabric_red");
            fabricGreen = P.Get("fabric_green");
            paperOld = P.Get("paper_old");
            card = P.Tint("paper", new Color(0.95f, 0.66f, 0.56f));
            leafM = P.Get("leaf");
            potM = P.Get("pot");
            cardboardM = P.Get("cardboard");
            glowM = P.Get("glow");
            shadeM = P.Get("lamp_shade");
            frameM = P.Get("frame_wood");
            tileM = P.Tint("court_floor", new Color(0.78f, 0.78f, 0.76f));
            darkM = M(new Color(0.06f, 0.06f, 0.07f), 0.75f);
            steelM = M(new Color(0.62f, 0.64f, 0.66f), 0.75f, null, 0.8f);
            stoneM = M(new Color(0.5f, 0.48f, 0.45f), 0.2f);
            bookM = new Material[7];
            for (int i = 0; i < bookM.Length; i++) bookM[i] = P.Get("book_" + i);
        }

        private static void BuildGroundFloorV05(Case01HouseLayout L, Transform g)
        {
            MaterialsV05();
            L.Decor = new Case01Decor();
            Solid(g, new Vector3(5f, -0.1f, 5f), new Vector3(10.4f, 0.2f, 10.4f), floorWood, "Floor");
            if (plan == Case01Floorplan.V05A) GroundA(L, g);
            else GroundB(L, g);
        }

        // ======================= 方案 A =======================

        private static void GroundA(Case01HouseLayout L, Transform g)
        {
            float hx0 = V05A.HallX0, hx1 = V05A.HallX1, hz1 = V05A.HallZ1, lz1 = V05A.LivingZ1;
            L.Spawn = new Case01Pose(V05A.Spawn, 0f);
            L.Arrivals[Case01Room.Dining] = new Case01Pose(new Vector3(V05A.DiningGapX, 0.05f, lz1 + 0.6f), 0f);
            L.ArtPose = new Case01Pose(new Vector3(4.7f, 0.05f, 4.5f), -125f);
            L.PhotoPlace = "一樓客廳";

            // 窗（先登記）：客廳南牆兩扇、餐廳北牆與西牆、廚房水槽上方（北）與東牆。
            Window(g, new Vector3(1.6f, 1.5f, 0.09f), 0f);
            Window(g, new Vector3(3.8f, 1.5f, 0.09f), 0f);
            Window(g, new Vector3(2.7f, 1.5f, 9.91f), 0f);
            Window(g, new Vector3(0.09f, 1.5f, 9.0f), 90f);
            Window(g, new Vector3(7.4f, 1.5f, 9.91f), 0f);
            Window(g, new Vector3(9.91f, 1.5f, 7.9f), 90f);

            // 外牆。
            WallZ(g, 0f, 0f, 10f, 0f, FloorHeight, wallMat, 7.1f);
            WallZ(g, 10f, 0f, 10f, 0f, FloorHeight, wallMat);
            WallX(g, 0f, 0f, 10f, 0f, FloorHeight, wallMat);
            WallX(g, 10f, 0f, 10f, 0f, FloorHeight, wallMat);
            // 內牆：x=5.4（玄關→客廳 1.4 m 開口、餐廳→廚房門）、z=5.4（客廳↔餐廳 2.4 m 半開放）、z=4.4（玄關→廚房門）、x=8.65（玄關｜樓梯）。
            WallXWide(g, hx0, 0f, 10f, 0f, 2.8f, wallMat, new Vector2(V05A.LivingDoorZ, V05A.LivingDoorW), new Vector2(V05A.DiningKitchenDoorZ, DoorWidth));
            WallZWide(g, lz1, 0f, hx0, 0f, 2.8f, wallMat, new Vector2(V05A.DiningGapX, V05A.DiningGapW));
            WallZWide(g, hz1, hx0, hx1, 0f, 2.8f, wallMat, new Vector2(V05A.KitchenDoorX, DoorWidth));
            WallX(g, hx1, 0f, hz1, 0f, 2.8f, wallMat);

            // ---------------- 玄關（出入、換鞋、掛外套） ----------------
            FrontDoor(L, g);
            Box(g, new Vector3(7.1f, 0.006f, 0.6f), new Vector3(1.2f, 0.012f, 0.7f), fabricRed, "DoorMat");
            var bench = ShoeBench(g, new Vector3(hx0 + 0.3f, 0f, 3.25f), 90f);
            CoatHooks(g, new Vector3(hx0 + 0.09f, 1.72f, 3.25f), 90f, true);
            var cab = ShoeCabinet(g, new Vector3(hx1 - 0.25f, 0f, 3.4f), -90f);
            KeyTray(g, cab.position + new Vector3(0f, 0.92f, 0f));
            UmbrellaStand(g, new Vector3(hx1 - 0.3f, 0f, 0.45f));
            ClosetDoor(g, new Vector3(hx1 - 0.085f, 0f, 1.45f), -90f, 0.8f, 1.95f, "UnderStairCupboard");
            Painting(g, new Vector3(6.45f, 1.6f, hz1 - 0.085f), 0f, 1.1f, 0.75f, new Color(0.38f, 0.48f, 0.42f), new Color(0.62f, 0.56f, 0.4f), "HallLandscape");
            RoomLight(L, Case01Room.Entrance, g, new Vector3(7.0f, 2.35f, 2.2f), new Color(1f, 0.78f, 0.52f), 1.0f, false, 2.8f);
            L.EchoSpots[Case01Echoes.Entrance] = new Case01EchoPlayer.Spot
            {
                Room = Case01Room.Entrance, Anchor = new Vector3(6.9f, 0f, 2.6f),
                Figures = new[] { new Vector3(6.05f, 0f, 3.3f), new Vector3(7.55f, 0f, 3.45f) }, Yaws = new[] { 100f, -110f },
            };

            // ---------------- 客廳（母女日常休息） ----------------
            // 西牆壁爐（合照在壁爐架右側）、北側凹處書櫃、西南角電視、面向壁爐的沙發與茶几、扶手椅＋閱讀燈。
            // v0.6：客廳家具集中在 LivingOld 群組（客廳美術標準間 QA 可整組換成新美術）；調查熱區移回一樓群組，不受切換影響。
            var lg = ArenaArt.Group("LivingOld", g, Vector3.zero);
            L.LivingOldRoot = lg;
            var fire = Fireplace(lg, new Vector3(0f, 0f, 3.0f), 90f);
            PhotoOnMantel(L, lg, fire.TransformPoint(new Vector3(0.58f, 1.3f, 0.6f)), 90f);
            MantelClutter(lg, fire);
            LivingBookcase(L, lg, new Vector3(0.27f, 0f, 4.6f), 90f, 1.3f);
            TvCorner(lg, new Vector3(0.62f, 0f, 0.62f), 45f);
            Sofa(lg, new Vector3(3.62f, 0f, 3.05f), -90f, 2.0f, fabricGreen);
            CoffeeTable(L, lg, new Vector3(2.2f, 0f, 3.05f), 90f);
            Box(lg, new Vector3(2.35f, 0.006f, 3.05f), new Vector3(2.2f, 0.012f, 2.6f), fabricRed, "Rug");
            Armchair(lg, new Vector3(2.6f, 0f, 4.45f), 200f, pale);
            ReadingLamp(L, lg, new Vector3(3.3f, 0f, 4.85f));
            Calendar(L, g, new Vector3(hx0 - 0.085f, 1.5f, 3.4f), 90f);
            LivingWallFrame(L, lg, new Vector3(4.65f, 1.7f, lz1 - 0.085f), 0f);
            Plant(lg, new Vector3(2.7f, 0f, 0.4f));
            foreach (var it in lg.GetComponentsInChildren<Case01Interactable>(true)) it.transform.SetParent(g, true);
            foreach (Transform w in g)
                if (w.name == "Window" && w.position.y < 2f && w.position.x < hx0 && w.position.z < lz1) L.LivingWindows.Add(w);
            RoomLight(L, Case01Room.Living, g, new Vector3(2.5f, 2.3f, 2.8f), new Color(1f, 0.74f, 0.46f), 1.0f, true, 2.8f);

            // ---------------- 餐廳（二→一的主要矛盾） ----------------
            var table = DiningSet(L, g, V05A.TableCenter);
            Sideboard(L, g, new Vector3(0.32f, 0f, 7.3f), 90f);
            RoomLight(L, Case01Room.Dining, g, V05A.TableCenter + new Vector3(0f, 2.2f, 0f), new Color(1f, 0.72f, 0.42f), 1.2f, true, 2.8f);
            L.EchoSpots[Case01Echoes.Dining] = new Case01EchoPlayer.Spot
            {
                Room = Case01Room.Dining, Anchor = table.position,
                Figures = new[] { table.position + new Vector3(-0.55f, 0f, 0.95f), table.position + new Vector3(0.75f, 0f, 1.0f) }, Yaws = new[] { 160f, -150f },
            };

            // ---------------- 廚房（備餐、收納） ----------------
            Box(g, new Vector3((hx0 + 10f) * 0.5f, 0.004f, (hz1 + 10f) * 0.5f), new Vector3(10f - hx0 - 0.15f, 0.008f, 10f - hz1 - 0.15f), tileM, "KitchenTiles");
            // 北牆一整排：爐台（左）、水槽（窗下）、檯面；上櫃在窗與抽油煙機之外。
            KitchenRun(g, new Vector3(5.55f, 0f, 9.7f), new Vector3(8.95f, 0f, 9.7f), 180f);
            Stove(g, new Vector3(6.15f, 0f, 9.7f), 180f);
            Sink(g, new Vector3(7.4f, 0f, 9.7f), 180f);
            UpperCabinet(g, new Vector3(8.45f, 1.85f, 9.83f), 180f, 0.9f);
            Fridge(L, g, new Vector3(9.62f, 0f, 9.6f), -90f);
            // 東牆側檯：茶葉盒、兩個茶杯、碗盤架（茶具在料理檯旁，不是孤立的展示台）。
            KitchenRun(g, new Vector3(9.7f, 0f, 6.95f), new Vector3(9.7f, 0f, 8.65f), -90f);
            TeaCorner(L, g, new Vector3(9.62f, 0.9f, 7.55f));
            DishRack(g, new Vector3(9.62f, 0.9f, 8.35f));
            Spices(g, new Vector3(6.75f, 0.9f, 9.78f));
            RoomLight(L, Case01Room.Kitchen, g, new Vector3(7.5f, 2.35f, 7.3f), new Color(1f, 0.86f, 0.66f), 0.95f, false, 2.8f);
        }

        // ======================= 方案 B =======================

        private static void GroundB(Case01HouseLayout L, Transform g)
        {
            L.Spawn = new Case01Pose(SpawnPoint, 0f);
            L.Arrivals[Case01Room.Dining] = new Case01Pose(new Vector3(6.5f, 0.05f, 5.8f), 0f);
            L.ArtPose = new Case01Pose(new Vector3(4.3f, 0.05f, 6.1f), -60f);
            L.PhotoPlace = "一樓客廳";

            Window(g, new Vector3(2.5f, 1.5f, 0.09f), 0f);   // 廚房水槽上方
            Window(g, new Vector3(2.5f, 1.5f, 9.91f), 0f);   // 客廳北窗
            Window(g, new Vector3(7.5f, 1.5f, 9.91f), 0f);   // 餐廳北窗

            WallZ(g, 0f, 0f, 10f, 0f, FloorHeight, wallMat, 7.1f);
            WallZ(g, 10f, 0f, 10f, 0f, FloorHeight, wallMat);
            WallX(g, 0f, 0f, 10f, 0f, FloorHeight, wallMat);
            WallX(g, 10f, 0f, 10f, 0f, FloorHeight, wallMat);
            // 分區同原版；客廳↔餐廳的門移到 z9.0（讓客廳能面向壁爐擺沙發）。
            WallX(g, 5f, 0f, 10f, 0f, 2.8f, wallMat, 2.5f, 9.0f);
            WallZ(g, 5f, 0f, 10f, 0f, 2.8f, wallMat, 2.5f, 6.5f);

            // ---------------- 玄關＋樓梯旁的收納 ----------------
            FrontDoor(L, g);
            Box(g, new Vector3(7.1f, 0.006f, 0.7f), new Vector3(1.2f, 0.012f, 0.7f), fabricRed, "DoorMat");
            ShoeBench(g, new Vector3(5.3f, 0f, 3.9f), 90f);
            CoatHooks(g, new Vector3(5.09f, 1.72f, 3.9f), 90f, true);
            var cab = ShoeCabinet(g, new Vector3(8.3f, 0f, 0.25f), 0f);
            KeyTray(g, cab.position + new Vector3(0f, 0.92f, 0f));
            UmbrellaStand(g, new Vector3(5.4f, 0f, 0.45f));
            // 樓梯高端下方：側板＋掃具櫃門（真正的收納，不是不可走的縫）；北端 z4.6～5 的空檔做成矮櫃。
            Box(g, new Vector3(StairX0 - 0.02f, 0.85f, 3.65f), new Vector3(0.04f, 1.7f, 1.9f), wood, "UnderStairPanel");
            ClosetDoor(g, new Vector3(StairX0 - 0.045f, 0f, 3.75f), -90f, 0.7f, 1.45f, "UnderStairCupboard");
            Solid(g, new Vector3(9.4f, 0.45f, 4.8f), new Vector3(1.2f, 0.9f, 0.36f), wood, "LowCupboard");
            Painting(g, new Vector3(7.95f, 1.6f, 4.915f), 0f, 1.0f, 0.7f, new Color(0.38f, 0.48f, 0.42f), new Color(0.62f, 0.56f, 0.4f), "HallLandscape");
            RoomLight(L, Case01Room.Entrance, g, new Vector3(7f, 2.35f, 2.5f), new Color(1f, 0.78f, 0.52f), 1.0f, false, 2.8f);

            // ---------------- 廚房（西南；南牆水槽在窗下） ----------------
            Box(g, new Vector3(2.5f, 0.004f, 2.5f), new Vector3(4.85f, 0.008f, 4.85f), tileM, "KitchenTiles");
            KitchenRun(g, new Vector3(0.15f, 0f, 0.3f), new Vector3(4.0f, 0f, 0.3f), 0f);
            Stove(g, new Vector3(1.0f, 0f, 0.3f), 0f);
            Sink(g, new Vector3(2.5f, 0f, 0.3f), 0f);
            UpperCabinet(g, new Vector3(3.55f, 1.85f, 0.17f), 0f, 0.9f);
            Fridge(L, g, new Vector3(0.38f, 0f, 4.4f), 90f);
            KitchenRun(g, new Vector3(0.3f, 0f, 1.25f), new Vector3(0.3f, 0f, 3.1f), 90f);
            TeaCorner(L, g, new Vector3(0.38f, 0.9f, 2.2f));
            DishRack(g, new Vector3(0.38f, 0.9f, 1.45f));
            Spices(g, new Vector3(1.6f, 0.9f, 0.22f));
            RoomLight(L, Case01Room.Kitchen, g, new Vector3(2.5f, 2.35f, 2.5f), new Color(1f, 0.86f, 0.66f), 0.95f, false, 2.8f);

            // ---------------- 客廳（西北） ----------------
            var fire = Fireplace(g, new Vector3(0f, 0f, 7.5f), 90f);
            PhotoOnMantel(L, g, fire.TransformPoint(new Vector3(0.58f, 1.3f, 0.6f)), 90f);
            MantelClutter(g, fire);
            LivingBookcase(L, g, new Vector3(0.27f, 0f, 5.95f), 90f, 1.1f);
            TvCorner(g, new Vector3(0.62f, 0f, 9.38f), 135f);
            Sofa(g, new Vector3(3.3f, 0f, 7.5f), -90f, 1.8f, fabricGreen);
            CoffeeTable(L, g, new Vector3(1.95f, 0f, 7.5f), 90f);
            Box(g, new Vector3(2.15f, 0.006f, 7.5f), new Vector3(2.0f, 0.012f, 2.4f), fabricRed, "Rug");
            Armchair(g, new Vector3(3.95f, 0f, 5.95f), -60f, pale);
            ReadingLamp(L, g, new Vector3(4.55f, 0f, 5.5f));
            Calendar(L, g, new Vector3(4.915f, 1.5f, 6.6f), 90f);
            LivingWallFrame(L, g, new Vector3(4.1f, 1.7f, 9.915f), 0f);
            RoomLight(L, Case01Room.Living, g, new Vector3(2.5f, 2.3f, 7.5f), new Color(1f, 0.74f, 0.46f), 1.0f, true, 2.8f);

            // ---------------- 餐廳（東北） ----------------
            DiningSet(L, g, new Vector3(7.6f, 0f, 7.6f));
            Sideboard(L, g, new Vector3(9.68f, 0f, 7.6f), -90f);
            RoomLight(L, Case01Room.Dining, g, new Vector3(7.6f, 2.2f, 7.6f), new Color(1f, 0.72f, 0.42f), 1.2f, true, 2.8f);
        }

        // ======================= 家具（共用） =======================
        // 每件家具都以「本地 +z＝正面朝向」建立，yaw 旋轉擺放；有碰撞體的只有實際會擋路的主體。

        private static Transform Group(Transform g, string name, Vector3 pos, float yaw)
        {
            var t = ArenaArt.Group(name, g, pos);
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        private static Transform Box(Transform p, Vector3 pos, Vector3 scale, Material m, string name, Vector3? euler = null)
        {
            return ArenaArt.Prim(PrimitiveType.Cube, p, pos, scale, m, euler, name, false, false);
        }

        private static Transform Cyl(Transform p, Vector3 pos, Vector3 scale, Material m, string name, Vector3? euler = null)
        {
            return ArenaArt.Prim(PrimitiveType.Cylinder, p, pos, scale, m, euler, name, false, false);
        }

        private static void Block(Transform p, Vector3 center, Vector3 size, string name)
        {
            var b = new GameObject(name).AddComponent<BoxCollider>();
            b.transform.SetParent(p, false);
            b.center = center;
            b.size = size;
        }

        private static void FrontDoor(Case01HouseLayout L, Transform g)
        {
            var door = Solid(g, new Vector3(7.1f, DoorHeight * 0.5f, 0.02f), new Vector3(DoorWidth, DoorHeight, 0.08f), darkWood, "FrontDoor");
            Deco(door, new Vector3(0.35f, 0f, 0.8f), new Vector3(0.06f, 0.04f, 1.5f), metal, "Knob");
            // 門上的小窗（玻璃）與門牌位置。
            Box(g, new Vector3(7.1f, 1.75f, 0.065f), new Vector3(0.5f, 0.32f, 0.01f), M(new Color(0.15f, 0.18f, 0.24f), 0.9f), "DoorLight");
            Hotspot(L, g, Case01Ids.FrontDoor, "前門（QA 返回／重來）", new Vector3(7.1f, 1.1f, 0.15f), new Vector3(1.1f, 2.0f, 0.12f));
            L.FrontDoorLabel = Case01Hud.WorldLabel(g, new Vector3(7.1f, 2.5f, 0.09f), 180f, "QA 返回／重來", 26, new Color(1f, 0.85f, 0.5f), 220f);
        }

        private static Transform ShoeBench(Transform g, Vector3 pos, float yaw)
        {
            var b = Group(g, "ShoeBench", pos, yaw);
            Box(b, new Vector3(0f, 0.44f, 0f), new Vector3(1.1f, 0.05f, 0.4f), wood, "Seat");
            Box(b, new Vector3(0f, 0.2f, 0f), new Vector3(1.04f, 0.03f, 0.36f), wood, "ShoeShelf");
            foreach (var x in new[] { -0.52f, 0.52f }) Box(b, new Vector3(x, 0.22f, 0f), new Vector3(0.05f, 0.44f, 0.38f), darkWood, "Leg");
            Box(b, new Vector3(-0.25f, 0.27f, 0.02f), new Vector3(0.26f, 0.1f, 0.12f), darkM, "Shoe", new Vector3(0f, 8f, 0f));
            Box(b, new Vector3(0.2f, 0.27f, 0.03f), new Vector3(0.24f, 0.09f, 0.11f), M(new Color(0.45f, 0.3f, 0.2f), 0.4f), "Shoe", new Vector3(0f, -6f, 0f));
            Block(b, new Vector3(0f, 0.25f, 0f), new Vector3(1.1f, 0.5f, 0.4f), "BenchBlock");
            return b;
        }

        private static void CoatHooks(Transform g, Vector3 pos, float yaw, bool coats)
        {
            var h = Group(g, "CoatHooks", pos, yaw);
            Box(h, Vector3.zero, new Vector3(1.0f, 0.1f, 0.025f), wood, "Rail");
            for (int i = 0; i < 4; i++) Cyl(h, new Vector3(-0.36f + i * 0.24f, -0.02f, 0.05f), new Vector3(0.025f, 0.05f, 0.025f), metal, "Hook", new Vector3(90f, 0f, 0f));
            if (!coats) return;
            ArenaArt.Prim(PrimitiveType.Capsule, h, new Vector3(-0.36f, -0.5f, 0.12f), new Vector3(0.36f, 0.45f, 0.16f), fabricRed, null, "Coat", false, true);
            ArenaArt.Prim(PrimitiveType.Capsule, h, new Vector3(0.12f, -0.45f, 0.11f), new Vector3(0.32f, 0.4f, 0.15f), bookM[1], null, "Coat", false, true);
            Box(h, new Vector3(0.36f, -0.35f, 0.07f), new Vector3(0.08f, 0.6f, 0.03f), fabricGreen, "Scarf");
        }

        private static Transform ShoeCabinet(Transform g, Vector3 pos, float yaw)
        {
            var c = Group(g, "ShoeCabinet", pos, yaw);
            Box(c, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.9f, 0.36f), wood, "Body");
            foreach (var x in new[] { -0.22f, 0.22f })
            {
                Box(c, new Vector3(x, 0.47f, 0.185f), new Vector3(0.42f, 0.8f, 0.01f), darkWood, "DoorPanel");
                Box(c, new Vector3(x * 0.25f, 0.7f, 0.2f), new Vector3(0.02f, 0.1f, 0.02f), metal, "Handle");
            }
            Block(c, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.9f, 0.36f), "CabinetBlock");
            return c;
        }

        private static void KeyTray(Transform g, Vector3 pos)
        {
            Cyl(g, pos + new Vector3(0f, 0.005f, 0f), new Vector3(0.22f, 0.012f, 0.22f), P.Get("ceramic"), "KeyTray");
            Box(g, pos + new Vector3(-0.04f, 0.02f, -0.02f), new Vector3(0.05f, 0.006f, 0.02f), metal, "Key", new Vector3(0f, 30f, 0f));
        }

        private static void UmbrellaStand(Transform g, Vector3 pos)
        {
            ArenaArt.Prim(PrimitiveType.Cylinder, g, pos + new Vector3(0f, 0.25f, 0f), new Vector3(0.24f, 0.25f, 0.24f), darkWood, null, "UmbrellaStand", true, true);
            Cyl(g, pos + new Vector3(-0.02f, 0.6f, -0.03f), new Vector3(0.05f, 0.45f, 0.05f), bookM[1], "Umbrella", new Vector3(6f, 0f, 4f));
            Cyl(g, pos + new Vector3(0.04f, 0.55f, 0.05f), new Vector3(0.045f, 0.4f, 0.045f), bookM[0], "Umbrella", new Vector3(-5f, 0f, -7f));
        }

        private static void ClosetDoor(Transform g, Vector3 pos, float yaw, float w, float h, string name)
        {
            var d = Group(g, name, pos, yaw);
            Box(d, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, 0.03f), wood, "Door");
            Box(d, new Vector3(0f, h * 0.5f, 0.018f), new Vector3(w * 0.7f, h * 0.75f, 0.006f), darkWood, "Panel");
            Box(d, new Vector3(w * 0.36f, h * 0.52f, 0.03f), new Vector3(0.03f, 0.03f, 0.04f), metal, "Knob");
        }

        private static void Painting(Transform g, Vector3 pos, float yaw, float w, float h, Color sky, Color land, string name)
        {
            // 正面為本地 -z（與現有相框相同的慣例）。
            var p = Group(g, name, pos, yaw);
            Box(p, Vector3.zero, new Vector3(w, h, 0.04f), frameM, "Frame");
            Box(p, new Vector3(0f, h * 0.12f, -0.022f), new Vector3(w - 0.1f, h * 0.55f, 0.004f), M(sky, 0.1f), "Sky");
            Box(p, new Vector3(0f, -h * 0.25f, -0.023f), new Vector3(w - 0.1f, h * 0.3f, 0.004f), M(land, 0.1f), "Land");
            Box(p, new Vector3(w * 0.18f, -h * 0.08f, -0.025f), new Vector3(w * 0.3f, h * 0.18f, 0.004f), M(land * 0.7f, 0.1f), "Hill");
        }

        /// <summary>壁爐：凸出牆面的煙囪壁、爐口（暗）、石材爐床、木作壁爐架。回傳的群組本地 +z＝朝向房間，+x＝房間的右手邊。</summary>
        private static Transform Fireplace(Transform g, Vector3 wallPos, float yaw)
        {
            var f = Group(g, "Fireplace", wallPos, yaw);
            // 本地座標：x＝沿牆，z＝離牆。
            Solid(f, new Vector3(0f, 1.4f, 0.27f), new Vector3(1.6f, 2.8f, 0.4f), wallMat, "ChimneyBreast");
            Box(f, new Vector3(0f, 0.42f, 0.475f), new Vector3(0.72f, 0.62f, 0.02f), darkM, "Firebox");
            Box(f, new Vector3(0f, 0.42f, 0.48f), new Vector3(0.86f, 0.76f, 0.012f), stoneM, "Surround");
            Box(f, new Vector3(0f, 0.02f, 0.75f), new Vector3(1.5f, 0.04f, 0.6f), stoneM, "Hearth");
            Box(f, new Vector3(0f, 1.12f, 0.56f), new Vector3(1.72f, 0.06f, 0.26f), wood, "Mantel");
            Box(f, new Vector3(0f, 0.04f, 0.52f), new Vector3(0.5f, 0.06f, 0.18f), darkM, "Grate");
            return f;
        }

        private static void MantelClutter(Transform g, Transform fire)
        {
            Box(g, fire.TransformPoint(new Vector3(-0.1f, 1.27f, 0.58f)), new Vector3(0.2f, 0.24f, 0.08f), darkWood, "MantelClock").rotation = fire.rotation;
            Cyl(g, fire.TransformPoint(new Vector3(-0.62f, 1.25f, 0.58f)), new Vector3(0.06f, 0.08f, 0.06f), pale, "Candle");
            Cyl(g, fire.TransformPoint(new Vector3(-0.45f, 1.23f, 0.6f)), new Vector3(0.08f, 0.06f, 0.08f), M(new Color(0.35f, 0.5f, 0.6f), 0.7f), "SmallVase");
        }

        /// <summary>合照（Case01Ids.Photo）：放在壁爐架上的相框，正面朝向房間——生活照片，不是對著餐桌的祭壇。</summary>
        private static void PhotoOnMantel(Case01HouseLayout L, Transform g, Vector3 pos, float wallYaw)
        {
            var frame = ArenaArt.Group("PhotoFrame", g, pos);
            // 相框正面為本地 -z；朝向房間（牆的法線方向）並微微斜放。
            frame.localRotation = Quaternion.Euler(-8f, wallYaw - 180f + 14f, 0f);
            Deco(frame, Vector3.zero, new Vector3(0.36f, 0.28f, 0.03f), darkWood, "Frame");
            Deco(frame, new Vector3(0f, 0f, -0.016f), new Vector3(0.3f, 0.22f, 0.005f), paperOld, "Print");
            L.PhotoFigureMat = M(new Color(0.25f, 0.22f, 0.22f), 0.1f);
            L.PhotoFadedMat = M(new Color(0.6f, 0.58f, 0.53f), 0.1f);
            Deco(frame, new Vector3(-0.06f, -0.015f, -0.02f), new Vector3(0.05f, 0.15f, 0.005f), L.PhotoFigureMat, "FigureA");
            L.PhotoSecondFigure = Deco(frame, new Vector3(0.06f, -0.02f, -0.02f), new Vector3(0.048f, 0.135f, 0.005f), L.PhotoFigureMat, "FigureB").GetComponent<Renderer>();
            Deco(frame, new Vector3(0f, -0.15f, 0.05f), new Vector3(0.04f, 0.04f, 0.14f), darkWood, "Stand", false, new Vector3(30f, 0f, 0f));
            Hotspot(L, g, Case01Ids.Photo, "壁爐架上的合照", pos, new Vector3(0.5f, 0.45f, 0.5f));
        }

        /// <summary>客廳書櫃（夾著便箋的書、H1 會缺的兩本書）。正面為本地 +z。</summary>
        private static void LivingBookcase(Case01HouseLayout L, Transform g, Vector3 pos, float yaw, float width)
        {
            var d = L.Decor;
            var s = Group(g, "LivingBookcase", pos, yaw);
            float hw = width * 0.5f;
            Box(s, new Vector3(0f, 0.95f, -0.15f), new Vector3(width, 1.9f, 0.03f), darkWood, "Back");
            foreach (var x in new[] { -hw, hw }) Box(s, new Vector3(x, 0.95f, 0f), new Vector3(0.04f, 1.9f, 0.32f), wood, "Side");
            Box(s, new Vector3(0f, 1.9f, 0f), new Vector3(width + 0.04f, 0.04f, 0.34f), wood, "Top");
            float[] tops = { 0.08f, 0.5f, 0.92f, 1.34f };
            foreach (var t in tops) Box(s, new Vector3(0f, t - 0.015f, 0f), new Vector3(width - 0.04f, 0.03f, 0.3f), wood, "Shelf");
            Block(s, new Vector3(0f, 0.95f, 0f), new Vector3(width, 1.9f, 0.32f), "BookcaseBlock");
            var rng = new System.Random(55);
            System.Func<float, float, float> R = (a, b) => a + (float)rng.NextDouble() * (b - a);
            for (int row = 0; row < 3; row++)
            {
                float x = -hw + 0.05f;
                int n = 0;
                while (x < hw - 0.07f)
                {
                    // 第三層（視線高度）中段兩本較厚、顏色明亮的書：H1 時消失，留下明顯空位。
                    bool marked = row == 2 && (n == 4 || n == 5);
                    float w = marked ? 0.08f : R(0.03f, 0.065f), h = marked ? 0.34f : R(0.24f, 0.36f);
                    var b = Box(s, new Vector3(x + w * 0.5f, tops[row] + h * 0.5f, 0.0f), new Vector3(w, h, 0.22f), marked ? bookM[n == 4 ? 3 : 5] : bookM[(n + row * 3) % bookM.Length], "Book");
                    if (marked) d.LivingMissingBooks.Add(b.gameObject);
                    x += w + 0.004f;
                    n++;
                    if (row == 1 && n == 7) x += 0.12f;
                }
            }
            // 第二層右端：夾著便箋、微微突出的那本書（可調查）。
            var noteBook = Box(s, new Vector3(hw - 0.12f, tops[1] + 0.15f, 0.06f), new Vector3(0.05f, 0.3f, 0.22f), bookM[3], "BookWithNote");
            Box(s, noteBook.localPosition + new Vector3(0f, 0.17f, 0.02f), new Vector3(0.008f, 0.06f, 0.18f), paper, "NoteTab");
            Hotspot(L, g, Case01Ids.LivingBookNote, "夾著便箋的書", noteBook.position + (Quaternion.Euler(0f, yaw, 0f) * Vector3.forward) * 0.12f, new Vector3(0.35f, 0.45f, 0.35f));
            // 最上層：幾張小生活照（母女的日常，不是合照本身）。
            for (int i = 0; i < 2; i++)
                Box(s, new Vector3(-0.2f + i * 0.3f, 1.47f, 0.02f), new Vector3(0.16f, 0.12f, 0.02f), frameM, "SmallFrame", new Vector3(-6f, i == 0 ? 8f : -6f, 0f));
        }

        private static void TvCorner(Transform g, Vector3 pos, float yaw)
        {
            var t = Group(g, "TvCorner", pos, yaw);
            Box(t, new Vector3(0f, 0.25f, 0f), new Vector3(1.0f, 0.5f, 0.42f), darkWood, "TvUnit");
            Box(t, new Vector3(0f, 0.27f, 0.215f), new Vector3(0.9f, 0.02f, 0.01f), wood, "DrawerLine");
            Box(t, new Vector3(0f, 0.53f, -0.02f), new Vector3(0.36f, 0.04f, 0.2f), darkM, "TvStand");
            Box(t, new Vector3(0f, 0.86f, 0f), new Vector3(0.92f, 0.56f, 0.07f), darkM, "Tv");
            Box(t, new Vector3(0f, 0.86f, 0.037f), new Vector3(0.86f, 0.5f, 0.004f), M(new Color(0.02f, 0.025f, 0.035f), 0.95f), "Screen");
            Block(t, new Vector3(0f, 0.5f, 0f), new Vector3(1.0f, 1.0f, 0.42f), "TvBlock");
        }

        /// <summary>沙發：座墊、靠背、扶手、坐墊分縫與抱枕。正面（坐下時面向）為本地 +z。</summary>
        private static Transform Sofa(Transform g, Vector3 pos, float yaw, float length, Material fab)
        {
            var s = Group(g, "Sofa", pos, yaw);
            float hl = length * 0.5f;
            Box(s, new Vector3(0f, 0.22f, 0.02f), new Vector3(length, 0.3f, 0.86f), fab, "Base");
            for (int i = 0; i < 2; i++)
                Box(s, new Vector3((i - 0.5f) * (length - 0.36f) * 0.5f, 0.42f, 0.06f), new Vector3((length - 0.36f) * 0.5f - 0.02f, 0.12f, 0.72f), fab, "Cushion");
            Box(s, new Vector3(0f, 0.62f, -0.33f), new Vector3(length, 0.62f, 0.22f), fab, "Back");
            foreach (var x in new[] { -hl + 0.09f, hl - 0.09f }) Box(s, new Vector3(x, 0.45f, 0.02f), new Vector3(0.18f, 0.5f, 0.86f), fab, "Arm");
            Box(s, new Vector3(-hl + 0.4f, 0.62f, -0.15f), new Vector3(0.36f, 0.32f, 0.12f), pale, "Pillow", new Vector3(-12f, 18f, 0f));
            Box(s, new Vector3(0.2f, 0.52f, 0.1f), new Vector3(0.6f, 0.03f, 0.4f), fabricRed, "Throw", new Vector3(0f, 14f, 4f));
            Block(s, new Vector3(0f, 0.45f, 0f), new Vector3(length, 0.9f, 0.86f), "SofaBlock");
            return s;
        }

        private static void Armchair(Transform g, Vector3 pos, float yaw, Material fab)
        {
            var a = Group(g, "Armchair", pos, yaw);
            Box(a, new Vector3(0f, 0.22f, 0.02f), new Vector3(0.78f, 0.3f, 0.78f), fab, "Base");
            Box(a, new Vector3(0f, 0.42f, 0.06f), new Vector3(0.5f, 0.1f, 0.62f), fab, "Cushion");
            Box(a, new Vector3(0f, 0.66f, -0.3f), new Vector3(0.78f, 0.72f, 0.18f), fab, "Back");
            foreach (var x in new[] { -0.32f, 0.32f }) Box(a, new Vector3(x, 0.45f, 0.02f), new Vector3(0.14f, 0.45f, 0.78f), fab, "Arm");
            Block(a, new Vector3(0f, 0.45f, 0f), new Vector3(0.78f, 0.9f, 0.78f), "ArmchairBlock");
        }

        private static void ReadingLamp(Case01HouseLayout L, Transform g, Vector3 pos)
        {
            var lamp = ArenaArt.Group("ReadingLamp", g, pos);
            Cyl(lamp, new Vector3(0f, 0.02f, 0f), new Vector3(0.3f, 0.02f, 0.3f), metal, "Base");
            Cyl(lamp, new Vector3(0f, 0.75f, 0f), new Vector3(0.035f, 0.75f, 0.035f), metal, "Pole");
            Cyl(lamp, new Vector3(0f, 1.55f, 0f), new Vector3(0.36f, 0.14f, 0.36f), shadeM, "Shade");
            L.LampLights.Add(ArenaArt.PointLight(lamp, new Vector3(0f, 1.4f, 0f), new Color(1f, 0.74f, 0.45f), 3.2f, 0.6f, false));
        }

        /// <summary>茶几（朋友卡片、馬克杯、雜誌、遙控器）。</summary>
        private static void CoffeeTable(Case01HouseLayout L, Transform g, Vector3 pos, float yaw)
        {
            var t = Group(g, "CoffeeTable", pos, yaw);
            Box(t, new Vector3(0f, 0.38f, 0f), new Vector3(1.1f, 0.05f, 0.6f), wood, "Top");
            Box(t, new Vector3(0f, 0.12f, 0f), new Vector3(1.0f, 0.03f, 0.5f), wood, "LowerShelf");
            for (int i = 0; i < 4; i++) Box(t, new Vector3(i < 2 ? -0.5f : 0.5f, 0.19f, i % 2 == 0 ? -0.25f : 0.25f), new Vector3(0.05f, 0.38f, 0.05f), darkWood, "Leg");
            Block(t, new Vector3(0f, 0.2f, 0f), new Vector3(1.1f, 0.4f, 0.6f), "TableBlock");
            Cyl(t, new Vector3(-0.3f, 0.45f, 0.05f), new Vector3(0.08f, 0.05f, 0.08f), P.Get("ceramic"), "Mug");
            Box(t, new Vector3(0.05f, 0.41f, 0.12f), new Vector3(0.28f, 0.01f, 0.2f), bookM[6], "Magazine", new Vector3(0f, 18f, 0f));
            Box(t, new Vector3(-0.1f, 0.415f, -0.15f), new Vector3(0.05f, 0.02f, 0.16f), frameM, "Remote", new Vector3(0f, -25f, 0f));
            Box(t, new Vector3(0.62f - 0.3f, 0.47f, -0.05f), new Vector3(0.16f, 0.12f, 0.005f), card, "FriendCard", new Vector3(-12f, 160f, 0f));
            Box(t, new Vector3(0.1f, 0.135f, 0f), new Vector3(0.3f, 0.06f, 0.22f), bookM[2], "StackedBooks");
            Hotspot(L, g, Case01Ids.FriendCard, "茶几上的卡片", t.TransformPoint(new Vector3(0.32f, 0.5f, -0.05f)), new Vector3(0.3f, 0.25f, 0.3f));
        }

        private static void Calendar(Case01HouseLayout L, Transform g, Vector3 pos, float yaw)
        {
            var c = Group(g, "Calendar", pos, yaw);
            Box(c, Vector3.zero, new Vector3(0.42f, 0.55f, 0.02f), paper, "Sheet");
            Box(c, new Vector3(0f, 0.2f, -0.012f), new Vector3(0.42f, 0.14f, 0.004f), M(new Color(0.55f, 0.2f, 0.2f), 0.2f), "Header");
            Hotspot(L, g, Case01Ids.Calendar, "日曆", pos, new Vector3(0.6f, 0.7f, 0.6f));
        }

        /// <summary>客廳牆上的相框（H0 有照片、H1 起空白）。</summary>
        private static void LivingWallFrame(Case01HouseLayout L, Transform g, Vector3 pos, float yaw)
        {
            var d = L.Decor;
            var frame = Group(g, "LivingFrame", pos, yaw);
            Box(frame, Vector3.zero, new Vector3(0.62f, 0.46f, 0.04f), frameM, "Frame");
            d.LivingFrameBlank = Box(frame, new Vector3(0f, 0f, -0.022f), new Vector3(0.54f, 0.38f, 0.005f), pale, "BlankPrint").gameObject;
            var photo = ArenaArt.Group("Photo", frame, Vector3.zero);
            Box(photo, new Vector3(0f, 0.06f, -0.025f), new Vector3(0.54f, 0.26f, 0.005f), M(new Color(0.45f, 0.62f, 0.75f), 0.1f), "Sky");
            Box(photo, new Vector3(0f, -0.12f, -0.025f), new Vector3(0.54f, 0.14f, 0.005f), M(new Color(0.75f, 0.68f, 0.5f), 0.1f), "Sand");
            Box(photo, new Vector3(-0.07f, -0.04f, -0.03f), new Vector3(0.05f, 0.18f, 0.004f), frameM, "FigureA");
            Box(photo, new Vector3(0.05f, -0.05f, -0.03f), new Vector3(0.045f, 0.15f, 0.004f), frameM, "FigureB");
            d.LivingFramePhoto = photo.gameObject;
        }

        private static void Plant(Transform g, Vector3 pos)
        {
            var plant = ArenaArt.Group("Plant", g, pos);
            ArenaArt.Prim(PrimitiveType.Cylinder, plant, new Vector3(0f, 0.18f, 0f), new Vector3(0.34f, 0.18f, 0.34f), potM, null, "Pot", true, true);
            ArenaArt.Prim(PrimitiveType.Sphere, plant, new Vector3(0f, 0.58f, 0f), new Vector3(0.48f, 0.52f, 0.48f), leafM, null, "Leaves", false, true);
            ArenaArt.Prim(PrimitiveType.Sphere, plant, new Vector3(0.1f, 0.82f, 0.05f), new Vector3(0.28f, 0.3f, 0.28f), leafM, null, "Leaves", false, true);
        }

        /// <summary>
        /// 餐桌＋四張成套椅子＋兩副餐具（南／北）。群組結構與原版相同（DiningChairNorth 的 parent＝桌子），
        /// 所以餐具改寫、H1 北側椅子移位、布巾、殘響位置都沿用既有邏輯。回傳桌子群組。
        /// </summary>
        private static Transform DiningSet(Case01HouseLayout L, Transform g, Vector3 center)
        {
            var d = L.Decor;
            var table = ArenaArt.Group("DiningTable", g, center);
            Solid(table, new Vector3(0f, 0.72f, 0f), new Vector3(1.6f, 0.06f, 1.2f), wood, "Top");
            Box(table, new Vector3(0f, 0.66f, 0f), new Vector3(1.44f, 0.08f, 1.04f), darkWood, "Apron");
            for (int i = 0; i < 4; i++)
                Deco(table, new Vector3(i < 2 ? -0.7f : 0.7f, 0.35f, i % 2 == 0 ? -0.5f : 0.5f), new Vector3(0.07f, 0.7f, 0.07f), darkWood, "Leg");
            Block(table, new Vector3(0f, 0.375f, 0f), new Vector3(1.6f, 0.75f, 1.2f), "TableBlock");
            Chair(table, new Vector3(0f, 0f, -0.95f), 0f, "ChairSouth");
            L.DiningChairNorth = Chair(table, new Vector3(0f, 0f, 0.95f), 180f, "ChairNorth");
            Chair(table, new Vector3(-1.05f, 0f, 0f), 90f, "ChairWest");
            Chair(table, new Vector3(1.05f, 0f, 0f), -90f, "ChairEast");
            Setting(table, -0.32f, "SetSouth");
            L.DiningSecondSet = Setting(table, 0.32f, "SetNorth").gameObject;
            L.DiningTrace = Deco(table, new Vector3(0f, 0.753f, 0.32f), new Vector3(0.55f, 0.005f, 0.42f), P.Tint("wood_warm", new Color(1.15f, 1f, 0.85f)), "CleanPatch").gameObject;
            L.DiningTrace.SetActive(false);
            L.TablePoint = table.position + Vector3.up * 0.95f;
            Hotspot(L, table, Case01Ids.Table, "餐桌", new Vector3(0f, 0.95f, 0f), new Vector3(1.6f, 0.4f, 1.2f));
            d.DiningRunner = Box(table, new Vector3(0f, 0.754f, 0f), new Vector3(1.75f, 0.004f, 0.32f), M(new Color(0.7f, 0.25f, 0.22f), 0.05f), "TableRunner").gameObject;
            Box(table, new Vector3(0f, 0.752f, -0.32f), new Vector3(0.6f, 0.004f, 0.4f), pale, "PlacematSouth");
            Box(L.DiningSecondSet.transform, new Vector3(0f, 0.002f, 0f), new Vector3(0.6f, 0.004f, 0.4f), pale, "PlacematNorth");
            Box(L.DiningSecondSet.transform, new Vector3(-0.2f, 0.004f, 0.12f), new Vector3(0.12f, 0.006f, 0.16f), pale, "Napkin", new Vector3(0f, 20f, 0f));
            d.BindChair(L.DiningChairNorth);
            return table;
        }

        /// <summary>餐椅（座板、椅腳、靠背橫條）；群組本身是可移動的 Transform（H1 北側椅子會被拉開）。正面為本地 +z。</summary>
        private static Transform Chair(Transform table, Vector3 pos, float yaw, string name)
        {
            var c = ArenaArt.Group(name, table, pos);
            c.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var seat = Solid(c, new Vector3(0f, 0.45f, 0f), new Vector3(0.44f, 0.05f, 0.44f), darkWood, "Seat");
            seat.GetComponent<BoxCollider>().center = new Vector3(0f, -4f, 0f);
            seat.GetComponent<BoxCollider>().size = new Vector3(1f, 9f, 1f);
            for (int i = 0; i < 4; i++)
                Box(c, new Vector3(i < 2 ? -0.19f : 0.19f, 0.22f, i % 2 == 0 ? -0.19f : 0.19f), new Vector3(0.04f, 0.44f, 0.04f), darkWood, "Leg");
            foreach (var x in new[] { -0.19f, 0.19f }) Box(c, new Vector3(x, 0.72f, -0.2f), new Vector3(0.04f, 0.5f, 0.04f), darkWood, "BackPost");
            Box(c, new Vector3(0f, 0.9f, -0.2f), new Vector3(0.42f, 0.1f, 0.03f), darkWood, "BackRail");
            Box(c, new Vector3(0f, 0.7f, -0.2f), new Vector3(0.38f, 0.05f, 0.025f), darkWood, "BackRail");
            return c;
        }

        /// <summary>餐邊櫃：托盤與兩人份茶具（第二個杯子 H1 起消失）、購物清單、花瓶。正面為本地 +z。</summary>
        private static void Sideboard(Case01HouseLayout L, Transform g, Vector3 pos, float yaw)
        {
            var d = L.Decor;
            var s = Group(g, "Sideboard", pos, yaw);
            Box(s, new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.82f, 0.46f), darkWood, "Body");
            Box(s, new Vector3(0f, 0.88f, 0f), new Vector3(1.66f, 0.04f, 0.5f), wood, "Top");
            for (int i = 0; i < 3; i++)
            {
                Box(s, new Vector3(-0.52f + i * 0.52f, 0.45f, 0.235f), new Vector3(0.48f, 0.7f, 0.01f), wood, "DoorPanel");
                Box(s, new Vector3(-0.52f + i * 0.52f, 0.62f, 0.25f), new Vector3(0.08f, 0.02f, 0.02f), metal, "Handle");
            }
            Block(s, new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.9f, 0.46f), "SideboardBlock");
            var tray = ArenaArt.Group("TeaTray", s, new Vector3(-0.35f, 0.9f, 0f));
            Box(tray, new Vector3(0f, 0.01f, 0f), new Vector3(0.46f, 0.02f, 0.34f), metal, "Tray");
            ArenaArt.Prim(PrimitiveType.Sphere, tray, new Vector3(0.1f, 0.1f, 0f), new Vector3(0.16f, 0.14f, 0.16f), P.Get("ceramic"), null, "Teapot", false, false);
            Cyl(tray, new Vector3(0.1f, 0.12f, -0.09f), new Vector3(0.03f, 0.05f, 0.03f), P.Get("ceramic"), "Spout", new Vector3(50f, 0f, 0f));
            Cyl(tray, new Vector3(-0.1f, 0.05f, 0.05f), new Vector3(0.08f, 0.035f, 0.08f), P.Get("ceramic"), "Teacup");
            d.TraySecondCup = Cyl(tray, new Vector3(-0.15f, 0.05f, -0.07f), new Vector3(0.08f, 0.035f, 0.08f), P.Get("ceramic"), "Teacup2").gameObject;
            Box(s, new Vector3(0.45f, 0.905f, 0.05f), new Vector3(0.2f, 0.004f, 0.12f), paper, "ShoppingList", new Vector3(0f, 14f, 0f));
            Cyl(s, new Vector3(0.68f, 0.96f, -0.08f), new Vector3(0.1f, 0.06f, 0.1f), M(new Color(0.4f, 0.55f, 0.7f), 0.7f), "Vase");
            Hotspot(L, g, Case01Ids.ShoppingList, "餐邊櫃上的購物清單", s.TransformPoint(new Vector3(0.45f, 0.98f, 0.05f)), new Vector3(0.4f, 0.25f, 0.4f));
        }

        /// <summary>一排下櫃＋檯面（from→to 沿牆），正面朝 yaw（本地 +z）。門板分縫、踢腳。</summary>
        private static void KitchenRun(Transform g, Vector3 from, Vector3 to, float yaw)
        {
            var mid = (from + to) * 0.5f;
            float len = Vector3.Distance(from, to);
            var k = Group(g, "KitchenRun", mid, yaw);
            Box(k, new Vector3(0f, 0.45f, 0f), new Vector3(len, 0.82f, 0.58f), P.Get("ceramic"), "Cabinets");
            Box(k, new Vector3(0f, 0.885f, 0.01f), new Vector3(len + 0.02f, 0.04f, 0.62f), stoneM, "Worktop");
            Box(k, new Vector3(0f, 0.05f, 0.25f), new Vector3(len, 0.1f, 0.06f), darkM, "Kick");
            int doors = Mathf.Max(1, Mathf.RoundToInt(len / 0.55f));
            for (int i = 1; i < doors; i++) Box(k, new Vector3(-len * 0.5f + len * i / doors, 0.48f, 0.295f), new Vector3(0.01f, 0.7f, 0.01f), darkWood, "DoorGap");
            for (int i = 0; i < doors; i++) Box(k, new Vector3(-len * 0.5f + len * (i + 0.5f) / doors, 0.78f, 0.3f), new Vector3(0.12f, 0.015f, 0.02f), steelM, "Handle");
            Block(k, new Vector3(0f, 0.45f, 0f), new Vector3(len, 0.9f, 0.6f), "RunBlock");
        }

        private static void Stove(Transform g, Vector3 pos, float yaw)
        {
            var s = Group(g, "Stove", pos, yaw);
            Box(s, new Vector3(0f, 0.45f, 0.005f), new Vector3(0.6f, 0.82f, 0.6f), steelM, "Oven");
            Box(s, new Vector3(0f, 0.42f, 0.306f), new Vector3(0.5f, 0.42f, 0.01f), darkM, "OvenDoor");
            Box(s, new Vector3(0f, 0.68f, 0.31f), new Vector3(0.4f, 0.02f, 0.02f), steelM, "OvenHandle");
            Box(s, new Vector3(0f, 0.912f, 0f), new Vector3(0.6f, 0.01f, 0.6f), darkM, "Hob");
            for (int i = 0; i < 4; i++) Cyl(s, new Vector3(i % 2 == 0 ? -0.14f : 0.14f, 0.921f, i < 2 ? -0.13f : 0.13f), new Vector3(0.17f, 0.006f, 0.17f), M(new Color(0.12f, 0.12f, 0.12f), 0.4f), "Burner");
            Cyl(s, new Vector3(0.14f, 0.97f, 0.13f), new Vector3(0.22f, 0.06f, 0.22f), steelM, "Pot");
            // 抽油煙機（窗旁的牆上）。
            Box(s, new Vector3(0f, 1.82f, -0.12f), new Vector3(0.64f, 0.12f, 0.36f), steelM, "Hood");
            Box(s, new Vector3(0f, 2.3f, -0.2f), new Vector3(0.26f, 0.84f, 0.2f), steelM, "HoodChimney");
        }

        private static void Sink(Transform g, Vector3 pos, float yaw)
        {
            var s = Group(g, "Sink", pos, yaw);
            Box(s, new Vector3(0f, 0.909f, 0.04f), new Vector3(0.64f, 0.01f, 0.44f), steelM, "SinkRim");
            Box(s, new Vector3(0f, 0.912f, 0.04f), new Vector3(0.52f, 0.012f, 0.34f), darkM, "Basin");
            Cyl(s, new Vector3(0f, 1.02f, -0.2f), new Vector3(0.04f, 0.12f, 0.04f), steelM, "Tap");
            Box(s, new Vector3(0f, 1.13f, -0.12f), new Vector3(0.03f, 0.03f, 0.18f), steelM, "Spout");
            Box(s, new Vector3(0.24f, 0.92f, -0.2f), new Vector3(0.08f, 0.03f, 0.05f), steelM, "TapHandle");
        }

        private static void UpperCabinet(Transform g, Vector3 pos, float yaw, float width)
        {
            var u = Group(g, "UpperCabinet", pos, yaw);
            Box(u, Vector3.zero, new Vector3(width, 0.7f, 0.34f), P.Get("ceramic"), "Body");
            Box(u, new Vector3(0f, 0f, 0.175f), new Vector3(0.01f, 0.66f, 0.01f), darkWood, "DoorGap");
            foreach (var x in new[] { -0.06f, 0.06f }) Box(u, new Vector3(x, -0.25f, 0.18f), new Vector3(0.015f, 0.1f, 0.02f), steelM, "Handle");
        }

        /// <summary>冰箱（正面朝 yaw）：門縫、把手、門上的「週日紙條」。</summary>
        private static void Fridge(Case01HouseLayout L, Transform g, Vector3 pos, float yaw)
        {
            var f = Group(g, "Fridge", pos, yaw);
            Box(f, new Vector3(0f, 0.92f, 0f), new Vector3(0.66f, 1.84f, 0.64f), P.Get("ceramic"), "Body");
            Box(f, new Vector3(0f, 1.25f, 0.325f), new Vector3(0.64f, 0.012f, 0.01f), darkM, "DoorGap");
            Box(f, new Vector3(-0.27f, 1.5f, 0.34f), new Vector3(0.03f, 0.35f, 0.03f), steelM, "Handle");
            Box(f, new Vector3(-0.27f, 0.85f, 0.34f), new Vector3(0.03f, 0.35f, 0.03f), steelM, "Handle");
            Block(f, new Vector3(0f, 0.92f, 0f), new Vector3(0.66f, 1.84f, 0.64f), "FridgeBlock");
            var note = Box(f, new Vector3(0.08f, 1.4f, 0.326f), new Vector3(0.14f, 0.18f, 0.004f), paper, "FridgeNote", new Vector3(0f, 0f, 4f));
            Box(f, new Vector3(0.08f, 1.5f, 0.33f), new Vector3(0.03f, 0.03f, 0.01f), M(new Color(0.8f, 0.2f, 0.2f), 0.4f), "Magnet");
            Hotspot(L, g, Case01Ids.SundayNote, "冰箱上的紙條", note.position + (Quaternion.Euler(0f, yaw, 0f) * Vector3.forward) * 0.08f, new Vector3(0.45f, 0.5f, 0.45f));
        }

        /// <summary>料理檯上的茶角：茶葉盒（可開蓋）與兩個茶杯（茶杯熱區）。</summary>
        private static void TeaCorner(Case01HouseLayout L, Transform g, Vector3 pos)
        {
            var d = L.Decor;
            var tin = ArenaArt.Group("TeaTin", g, pos);
            Cyl(tin, new Vector3(0f, 0.09f, 0f), new Vector3(0.12f, 0.09f, 0.12f), M(new Color(0.2f, 0.35f, 0.35f), 0.6f, null, 0.4f), "Body");
            d.TeaTinLid = Cyl(tin, Vector3.zero, new Vector3(0.13f, 0.012f, 0.13f), M(new Color(0.75f, 0.6f, 0.25f), 0.6f, null, 0.6f), "Lid");
            d.TeaLidClosedPos = new Vector3(0f, 0.185f, 0f);
            d.TeaLidClosedRot = Quaternion.identity;
            d.TeaLidOpenPos = new Vector3(0.1f, 0.12f, 0.02f);
            d.TeaLidOpenRot = Quaternion.Euler(0f, 0f, 75f);
            Cyl(tin, new Vector3(0f, 0.17f, 0f), new Vector3(0.1f, 0.005f, 0.1f), M(new Color(0.25f, 0.18f, 0.1f), 0.1f), "Leaves");
            Hotspot(L, g, Case01Ids.TeaTin, "茶葉盒", pos + new Vector3(0f, 0.1f, 0f), new Vector3(0.3f, 0.3f, 0.3f));
            var cups = pos + new Vector3(0f, 0f, 0.38f);
            Cyl(g, cups + new Vector3(0f, 0.05f, -0.06f), new Vector3(0.08f, 0.05f, 0.08f), P.Get("ceramic"), "Cup");
            Cyl(g, cups + new Vector3(0.04f, 0.05f, 0.1f), new Vector3(0.08f, 0.05f, 0.08f), P.Get("ceramic"), "Cup");
            Hotspot(L, g, Case01Ids.Tea, "茶杯", cups + new Vector3(0f, 0.1f, 0f), new Vector3(0.45f, 0.3f, 0.4f));
        }

        private static void DishRack(Transform g, Vector3 pos)
        {
            var rack = ArenaArt.Group("DishRack", g, pos);
            Box(rack, new Vector3(0f, 0.01f, 0f), new Vector3(0.32f, 0.02f, 0.4f), metal, "Base");
            for (int i = 0; i < 4; i++) Cyl(rack, new Vector3(0f, 0.12f, -0.15f + i * 0.1f), new Vector3(0.22f, 0.006f, 0.22f), P.Get("ceramic"), "Plate", new Vector3(90f, 0f, 0f));
        }

        private static void Spices(Transform g, Vector3 pos)
        {
            Color[] spice = { new Color(0.7f, 0.3f, 0.15f), new Color(0.85f, 0.75f, 0.3f), new Color(0.3f, 0.5f, 0.25f), new Color(0.9f, 0.9f, 0.88f) };
            for (int i = 0; i < spice.Length; i++)
                Cyl(g, pos + new Vector3(i * 0.09f, 0.05f + i * 0.005f, (i % 2) * 0.05f), new Vector3(0.07f, 0.05f + i * 0.01f, 0.07f), M(spice[i], 0.6f), "SpiceJar");
        }
    }
}
