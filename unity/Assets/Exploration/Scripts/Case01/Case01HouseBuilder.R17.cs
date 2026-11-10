using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// Phase 1.7 住宅重構：R1（長形連棟）與 R2（R1＋空間節奏）。完整的房屋生成方案（牆、地板、門窗、樓梯、房間判定、
    /// 熱區、到達點、殘響、Portal、外景深度都由這裡的同一份配置決定），日後可直接接正式 Demo；目前只在 Phase 1.7 QA 使用。
    ///
    /// 外框：面寬 10 m（與街區十七號立面相同，16／18 號接壤不變），縱深 18 m（主屋兩層 12 m＋後段單層廚房 6 m）。
    /// 一樓：前門 → 側廊（x6.5～10、12 m 長，含樓梯）→ 左手客廳（前）／餐廳（中）→ 後段廚房；樓梯在走廊盡頭，轉身往前（南）上樓。
    /// 二樓：樓梯頂在 z6.7 → 前段平台（第三扇門在正前方外牆）／沿樓梯井的走廊（書房門）／後段小平台（後窗）；
    /// 艾琳的臥室在前、書房在後（家具與 H0～H3 物件整組沿用原版，以錨點搬到新位置）。
    /// 所有劇情物件 ID、H0～H3 綁定、錯接（書房門 → 餐廳）、第三扇門 → 夾層、夾層與中庭（原座標）都保留。
    /// </summary>
    public static partial class Case01HouseBuilder
    {
        public static bool IsR(Case01Floorplan p)
        {
            return p == Case01Floorplan.R1 || p == Case01Floorplan.R2;
        }

        /// <summary>Phase 1.7 格局尺寸（公尺；x＝東、z＝北、前門在 z0）。</summary>
        public static class R17
        {
            public const float Width = 10f, MainDepth = 12f, Depth = 18f;
            /// <summary>客廳／餐廳 與 走廊 的分界。</summary>
            public const float HallX = 6.5f;
            /// <summary>樓梯西側隔牆（樓梯淨寬 x8.925～9.925）。</summary>
            public const float StairX = 8.85f;
            public const float StairX0 = 8.925f, StairX1 = 9.925f;
            /// <summary>樓梯：起步在北（z10.6、一樓），往南上行，頂端在 z6.7（二樓）。14 階、坡度約 37.6°。</summary>
            public const float StairFootZ = 10.6f, StairTopZ = 6.7f;
            public const int StairSteps = 14;
            /// <summary>樓梯下收納隔牆的北端。</summary>
            public const float UnderStairZ1 = 10.0f;
            public const float LivingZ = 5.8f, ExtensionZ = 12f;
            /// <summary>二樓西側不開放的儲藏室（x0～1.5）。</summary>
            public const float StoreX = 1.5f;
            public const float FrontDoorX = 7.1f, ThirdDoorX = 8.6f;
            public const float BedroomDoorZ = 3.3f;
            /// <summary>書房門在二樓後段小平台（沿樓梯井的走廊走到底才進得去）；書櫃在門的南邊。</summary>
            public const float StudyDoorZ = 11.15f;
            public const float DiningHallDoorZ = 9.2f;
            // R1 的開口（一般寬度）。
            public const float R1LivingDoorZ = 1.8f, R1LivingDiningX = 3.2f, R1KitchenDoorX = 4.6f;
            // R2 的節奏。
            public const float R2VestibuleZ = 1.5f, R2LivingDoorZ = 3.6f, R2LivingDiningX = 1.25f, R2KitchenDoorX = 5.7f;
            /// <summary>R2：二樓前段平台的分段牆（偏置拱門 x7.4）。</summary>
            public const float R2LandingSplitZ = 4.2f, R2LandingArchX = 7.4f;
            /// <summary>R2：走廊在樓梯下收束處的低樑（2.2 m）。</summary>
            public const float R2HallBeamZ = 6.7f;
            public static readonly Vector3 Spawn = new Vector3(7.1f, 0.05f, 1.0f);
            public static readonly Vector3 SpawnR2 = new Vector3(7.1f, 0.05f, 0.8f);
            /// <summary>二樓原版房間的錨點（原版座標 + 這個位移＝新位置）。</summary>
            public static readonly Vector3 BedroomShift = new Vector3(1.5f, 0f, -4.2f);
            public static readonly Vector3 StudyShift = new Vector3(-3.5f, 0f, 2.0f);
            public static readonly Vector3 LandingShift = new Vector3(1.5f, 0f, 0f);
        }

        private static Case01HouseLayout BuildR(Transform parent, Case01Palette palette, Case01Floorplan floorplan, Case01HouseLayout L)
        {
            bool r2 = floorplan == Case01Floorplan.R2;
            thirdDoorX = R17.ThirdDoorX;
            L.Floorplan = floorplan;
            var root = ArenaArt.Group("Case01House", parent, Vector3.zero);
            L.Root = root;
            L.Spawn = new Case01Pose(r2 ? R17.SpawnR2 : R17.Spawn, 0f);
            L.Palette = palette ?? new Case01Palette();
            P = L.Palette;
            Materials();
            MaterialsV05();
            windowCuts.Clear();
            glassMat = null;
            L.Footprint = new Rect(0f, 0f, R17.Width, R17.Depth);

            RoomsR(L);
            trimWalls = true;
            GroundR(L, ArenaArt.Group("GroundFloor", root, Vector3.zero), r2);
            StairsR(L, ArenaArt.Group("Stairs", root, Vector3.zero));
            UpperR(L, ArenaArt.Group("SecondFloor", root, Vector3.zero), r2);
            trimWalls = false;
            BuildMezzanine(L, ArenaArt.Group("Mezzanine", root, Vector3.zero));
            BuildCourtyard(L, ArenaArt.Group("Courtyard", root, Vector3.zero));

            // 二樓的臥室、書房、平台物件：沿用原版（含 H0～H3 物件），以錨點整組搬到新位置。
            var life = ArenaArt.Group("LifeDetails", root, Vector3.zero);
            L.Decor = Case01HouseDecor.Build(L, life, ArenaArt.Group("BedroomAnchor", life, R17.BedroomShift),
                ArenaArt.Group("StudyAnchor", life, R17.StudyShift), ArenaArt.Group("LandingAnchor", life, R17.LandingShift));
            if (r2 && L.Decor.LandingPicture != null)
            {
                // R2：平台西牆的掛畫讓出分段牆（H2 的歪斜位移跟著改）。
                var d = L.Decor;
                var off = d.PictureMovedPos - d.PictureHomePos;
                d.PictureHomePos = new Vector3(d.PictureHomePos.x, d.PictureHomePos.y, 5.5f);
                d.PictureMovedPos = d.PictureHomePos + off;
                d.LandingPicture.localPosition = d.PictureHomePos;
            }
            L.Exterior = Case01Exterior.Build(root, P, false, R17.Depth);
            L.Aging = Case01Aging.Build(L, ArenaArt.Group("Aging", root, Vector3.zero), P);

            SetLayer(root.Find("Mezzanine"), AnomalyLayer);
            SetLayer(root.Find("Courtyard"), AnomalyLayer);
            if (L.Exterior != null) SetLayer(L.Exterior.Root, OutdoorLayer);

            float y = FloorHeight;
            L.Arrivals[Case01Room.Entrance] = L.Spawn;
            L.Arrivals[Case01Room.Dining] = new Case01Pose(new Vector3(3.0f, 0.05f, 7.0f), 0f);
            L.Arrivals[Case01Room.Study] = new Case01Pose(new Vector3(5.8f, y + 0.05f, R17.StudyDoorZ), -90f);
            L.Arrivals[Case01Room.Landing] = new Case01Pose(new Vector3(R17.ThirdDoorX, y + 0.05f, 0.95f), 0f);
            L.Arrivals[Case01Room.Mezzanine] = new Case01Pose(MezzCenter + new Vector3(0f, 0.05f, -4.7f), 0f);
            L.Arrivals[Case01Room.Courtyard] = new Case01Pose(new Vector3(40f, 0.05f, 27.5f), 0f);
            L.StudyCommitPose = new Case01Pose(new Vector3(5.8f, y + 0.05f, R17.StudyDoorZ), 90f);
            L.StudySnapCenter = new Vector3(R17.HallX - 0.1f, y, R17.StudyDoorZ);
            L.StudySnapYaw = -90f;
            L.CheckpointS30 = new Case01Pose(new Vector3(7.7f, y + 0.05f, 5.4f), -20f);
            L.ArtPose = new Case01Pose(new Vector3(5.6f, 0.05f, 4.9f), -125f);
            L.StairFootPoint = new Vector3(9.425f, 0.7f, R17.StairFootZ - 0.3f);
            L.ThirdDoorPoint = new Vector3(R17.ThirdDoorX, y + 1.2f, 0.15f);
            L.EchoSpots[Case01Echoes.Study] = new Case01EchoPlayer.Spot
            {
                Room = Case01Room.Study, Anchor = new Vector3(7.5f, y, 8.4f) + R17.StudyShift,
                Figures = new[] { new Vector3(7.2f, y, 8.85f) + R17.StudyShift }, Yaws = new[] { 10f },
            };
            L.EchoSpots[Case01Echoes.Landing] = new Case01EchoPlayer.Spot
            {
                Room = Case01Room.Landing, Anchor = new Vector3(9.3f, y, 0.5f), Figures = new Vector3[0], Yaws = new float[0],
            };
            return L;
        }

        private static void RoomsR(Case01HouseLayout L)
        {
            const float lo = -1f, mid = 2.75f, hi = 6.5f;
            Room(L, Case01Room.Entrance, R17.HallX, R17.Width, 0f, R17.ExtensionZ, lo, mid);
            Room(L, Case01Room.Living, 0f, R17.HallX, 0f, R17.LivingZ, lo, mid);
            Room(L, Case01Room.Dining, 0f, R17.HallX, R17.LivingZ, R17.ExtensionZ, lo, mid);
            Room(L, Case01Room.Kitchen, 0f, R17.Width, R17.ExtensionZ, R17.Depth, lo, mid);
            Room(L, Case01Room.Landing, R17.HallX, R17.Width, -0.8f, R17.ExtensionZ, mid, hi);
            Room(L, Case01Room.Bedroom, R17.StoreX, R17.HallX, 0f, R17.LivingZ, mid, hi);
            Room(L, Case01Room.Study, R17.StoreX, R17.HallX, R17.LivingZ, R17.ExtensionZ, mid, hi);
            Room(L, Case01Room.Storage, 0f, R17.StoreX, 0f, R17.ExtensionZ, mid, hi);
            Room(L, Case01Room.Mezzanine, 33f, 47f, -7f, 7f, -1f, 6f);
            Room(L, Case01Room.Courtyard, 25f, 55f, 23f, 59f, -1f, 8f);
        }

        // ======================= 一樓 =======================

        private static void GroundR(Case01HouseLayout L, Transform g, bool r2)
        {
            L.Decor = new Case01Decor();
            float hx = R17.HallX, lz = R17.LivingZ, ez = R17.ExtensionZ;
            Solid(g, new Vector3(R17.Width * 0.5f, -0.1f, R17.Depth * 0.5f), new Vector3(R17.Width + 0.4f, 0.2f, R17.Depth + 0.4f), floorWood, "Floor");
            L.PhotoPlace = "一樓客廳";

            // 窗（先登記）：客廳臨街兩扇、廚房後牆兩扇。主屋兩側是共用牆，沒有窗。
            Window(g, new Vector3(1.9f, 1.5f, 0.09f), 0f);
            Window(g, new Vector3(4.7f, 1.5f, 0.09f), 0f);
            Window(g, new Vector3(2.4f, 1.5f, R17.Depth - 0.09f), 0f);
            Window(g, new Vector3(6.2f, 1.5f, R17.Depth - 0.09f), 0f);

            // 外牆（主屋兩層高、後段廚房一層）。
            WallZ(g, 0f, 0f, R17.Width, 0f, FloorHeight, wallMat, R17.FrontDoorX);
            WallX(g, 0f, 0f, R17.Depth, 0f, FloorHeight, wallMat);
            WallX(g, R17.Width, 0f, R17.Depth, 0f, FloorHeight, wallMat);
            WallZ(g, R17.Depth, 0f, R17.Width, 0f, FloorHeight, wallMat, 8.3f);

            float livingDoorZ = r2 ? R17.R2LivingDoorZ : R17.R1LivingDoorZ;
            float livingDiningX = r2 ? R17.R2LivingDiningX : R17.R1LivingDiningX;
            float kitchenDoorX = r2 ? R17.R2KitchenDoorX : R17.R1KitchenDoorX;
            // 內牆：走廊西牆（客廳門、餐廳門）、客廳｜餐廳、主屋｜廚房、樓梯下收納隔牆與樓梯南端牆。
            WallXWide(g, hx, 0f, ez, 0f, 2.8f, wallMat, new Vector2(livingDoorZ, 1.0f), new Vector2(R17.DiningHallDoorZ, 1.0f));
            WallZWide(g, lz, 0f, hx, 0f, 2.8f, wallMat, new Vector2(livingDiningX, r2 ? 1.1f : 1.2f));
            WallZWide(g, ez, 0f, R17.Width, 0f, 2.8f, wallMat, new Vector2(kitchenDoorX, r2 ? 0.9f : 1.0f));
            WallX(g, R17.StairX, R17.StairTopZ, R17.UnderStairZ1, 0f, 2.8f, wallMat);
            WallZ(g, R17.StairTopZ, R17.StairX, R17.Width, 0f, 2.8f, wallMat);
            // 二樓樓板（樓梯井 x8.85～10、z5.7～9.6 留空）與後段廚房的平屋頂。
            float y = FloorHeight;
            Slab(g, 0f, R17.Width, 0f, R17.StairTopZ, y);
            Slab(g, 0f, R17.StairX, R17.StairTopZ, R17.StairFootZ, y);
            Slab(g, 0f, R17.Width, R17.StairFootZ, ez, y);
            Solid(g, new Vector3(R17.Width * 0.5f, y + 0.05f, (ez + R17.Depth) * 0.5f), new Vector3(R17.Width + 0.4f, 0.3f, R17.Depth - ez + 0.2f), M(new Color(0.16f, 0.16f, 0.18f), 0.3f), "ExtensionRoof");
            Deco(g, new Vector3(R17.Width * 0.5f, 2.795f, (ez + R17.Depth) * 0.5f), new Vector3(R17.Width, 0.01f, R17.Depth - ez), ceilMat, "CeilingPlaster");

            if (r2)
            {
                // R2 前廊（收）：前門與內門之間 1.4 m，內門 1.0 m 正對走廊盡頭的掛畫。
                WallZWide(g, R17.R2VestibuleZ, hx, R17.Width, 0f, 2.8f, wallMat, new Vector2(R17.FrontDoorX, 1.0f));
                Lining(g, true, R17.R2VestibuleZ, R17.FrontDoorX, 1.0f, 0.2f, 1f, 0f, "VestibuleDoor");
                // 門框深度：客廳門（客廳側）、客廳↔餐廳（餐廳側）、餐廳→廚房（廚房側）、走廊→餐廳（餐廳側）。
                Lining(g, false, hx, livingDoorZ, 1.0f, 0.3f, -1f, 0f, "LivingDoor");
                Lining(g, true, lz, livingDiningX, 1.1f, 0.3f, 1f, 0f, "LivingDiningDoor");
                Lining(g, true, ez, kitchenDoorX, 0.9f, 0.25f, 1f, 0f, "KitchenDoor");
                Lining(g, false, hx, R17.DiningHallDoorZ, 1.0f, 0.25f, -1f, 0f, "DiningDoor");
                // 走廊進到樓梯下收束段的低樑（2.2 m）與兩側壁柱：寬→窄。
                Solid(g, new Vector3((hx + WallThickness * 0.5f + R17.StairX - WallThickness * 0.5f) * 0.5f, 2.5f, R17.R2HallBeamZ), new Vector3(R17.StairX - hx - WallThickness, 0.6f, 0.3f), wallMat, "Bulkhead");
                Solid(g, new Vector3(hx + WallThickness * 0.5f + 0.1f, 1.1f, R17.R2HallBeamZ), new Vector3(0.2f, 2.2f, 0.3f), trim, "Lining_HallArch");
            }

            // ---------------- 走廊（玄關） ----------------
            FrontDoor(L, g);
            Box(g, new Vector3(R17.FrontDoorX, 0.006f, 0.6f), new Vector3(1.2f, 0.012f, 0.7f), fabricRed, "DoorMat");
            Box(g, new Vector3(7.7f, 0.006f, 6.6f), new Vector3(1.1f, 0.012f, 7.2f), fabricRed, "HallRunner");
            float benchZ = r2 ? 5.5f : 3.6f;
            ShoeBench(g, new Vector3(hx + 0.3f, 0f, benchZ), 90f);
            CoatHooks(g, new Vector3(hx + 0.09f, 1.72f, benchZ), 90f, true);
            var cab = ShoeCabinet(g, new Vector3(R17.Width - 0.25f, 0f, r2 ? 2.4f : 1.2f), -90f);
            KeyTray(g, cab.position + new Vector3(0f, 0.92f, 0f));
            UmbrellaStand(g, new Vector3(R17.Width - 0.35f, 0f, 0.45f));
            PhoneTable(L, g, new Vector3(R17.Width - 0.3f, 0f, 4.0f));
            ClosetDoor(g, new Vector3(R17.StairX - 0.085f, 0f, 8.2f), -90f, 0.8f, 1.95f, "UnderStairCupboard");
            QaSpot(L, g, "qa_obs_understair", "樓梯下的收納櫃", new Vector3(R17.StairX - 0.2f, 1.0f, 8.2f), new Vector3(0.3f, 1.6f, 0.9f),
                "樓梯下的收納櫃：兩把雨傘、一個工具箱，還有一雙小一號的雨鞋，鞋底的泥已經乾了。");
            // 走廊盡頭（樓梯起步前）的掛畫：從前門一路看過去的視線終點。
            Painting(g, new Vector3(7.7f, 1.6f, R17.ExtensionZ - 0.085f), 0f, 1.0f, 0.75f, new Color(0.38f, 0.48f, 0.42f), new Color(0.62f, 0.56f, 0.4f), "HallLandscape");
            RoomLight(L, Case01Room.Entrance, g, new Vector3(7.7f, 2.35f, 2.8f), new Color(1f, 0.78f, 0.52f), 1.0f, false, 2.8f);
            L.LampLights.Add(ArenaArt.PointLight(g, new Vector3(7.7f, 2.3f, 11.2f), new Color(1f, 0.78f, 0.52f), 4.5f, 0.7f, false));
            L.EchoSpots[Case01Echoes.Entrance] = new Case01EchoPlayer.Spot
            {
                Room = Case01Room.Entrance, Anchor = new Vector3(7.6f, 0f, 3.2f),
                Figures = new[] { new Vector3(6.95f, 0f, 3.5f), new Vector3(8.3f, 0f, 3.7f) }, Yaws = new[] { 100f, -110f },
            };

            // ---------------- 客廳（前廳） ----------------
            var fire = Fireplace(g, new Vector3(0f, 0f, 2.9f), 90f);
            PhotoOnMantel(L, g, fire.TransformPoint(new Vector3(0.58f, 1.3f, 0.6f)), 90f);
            MantelClutter(g, fire);
            LivingBookcase(L, g, new Vector3(0.27f, 0f, 5.0f), 90f, 1.3f);
            TvCorner(g, new Vector3(0.62f, 0f, 0.62f), 45f);
            Sofa(g, new Vector3(3.9f, 0f, 2.9f), -90f, 2.2f, fabricGreen);
            CoffeeTable(L, g, new Vector3(2.4f, 0f, 2.9f), 90f);
            Box(g, new Vector3(2.55f, 0.006f, 2.9f), new Vector3(2.4f, 0.012f, 2.8f), fabricRed, "Rug");
            // 扶手椅在東北角斜對壁爐（不擋客廳↔餐廳的門與壁爐旁的通道）。
            Armchair(g, new Vector3(5.3f, 0f, 4.7f), 225f, pale);
            ReadingLamp(L, g, new Vector3(5.95f, 0f, 5.3f));
            Calendar(L, g, new Vector3(hx - 0.085f, 1.5f, r2 ? 1.8f : 3.6f), 90f);
            LivingWallFrame(L, g, new Vector3(5.3f, 1.7f, lz - 0.085f), 0f);
            Plant(g, new Vector3(3.3f, 0f, 0.4f));
            Plant(g, new Vector3(6.05f, 0f, 0.45f));
            RoomLight(L, Case01Room.Living, g, new Vector3(3.0f, 2.3f, 2.9f), new Color(1f, 0.74f, 0.46f), 1.0f, true, 2.8f);

            // ---------------- 餐廳（中段） ----------------
            var table = DiningSet(L, g, new Vector3(3.0f, 0f, 9.0f));
            Sideboard(L, g, new Vector3(0.32f, 0f, 9.0f), 90f);
            ChinaCabinet(g, new Vector3(r2 ? 3.0f : 1.6f, 0f, ez - 0.32f));
            RoomLight(L, Case01Room.Dining, g, new Vector3(3.0f, 2.2f, 9.0f), new Color(1f, 0.72f, 0.42f), 1.2f, true, 2.8f);
            L.EchoSpots[Case01Echoes.Dining] = new Case01EchoPlayer.Spot
            {
                Room = Case01Room.Dining, Anchor = table.position,
                Figures = new[] { table.position + new Vector3(-0.55f, 0f, 0.95f), table.position + new Vector3(0.75f, 0f, 1.0f) }, Yaws = new[] { 160f, -150f },
            };

            // ---------------- 廚房（後段） ----------------
            Box(g, new Vector3(R17.Width * 0.5f, 0.004f, (ez + R17.Depth) * 0.5f), new Vector3(R17.Width - 0.15f, 0.008f, R17.Depth - ez - 0.15f), tileM, "KitchenTiles");
            float back = R17.Depth - 0.3f;
            KitchenRun(g, new Vector3(0.15f, 0f, back), new Vector3(4.0f, 0f, back), 180f);
            Stove(g, new Vector3(1.0f, 0f, back), 180f);
            Sink(g, new Vector3(2.4f, 0f, back), 180f);
            UpperCabinet(g, new Vector3(3.5f, 1.85f, R17.Depth - 0.17f), 180f, 0.9f);
            KitchenRun(g, new Vector3(5.2f, 0f, back), new Vector3(7.3f, 0f, back), 180f);
            Spices(g, new Vector3(1.6f, 0.9f, R17.Depth - 0.22f));
            if (r2)
            {
                // R2：冰箱在廚房門旁（背對門），進門先看到工作檯，轉身才看到紙條；東北角改放高櫃。
                Fridge(L, g, new Vector3(kitchenDoorX - 0.95f, 0f, ez + 0.4f), 0f);
                Larder(g, new Vector3(R17.Width - 0.38f, 0f, back), -90f);
            }
            else Fridge(L, g, new Vector3(R17.Width - 0.38f, 0f, back - 0.1f), -90f);
            KitchenRun(g, new Vector3(R17.Width - 0.3f, 0f, 13.4f), new Vector3(R17.Width - 0.3f, 0f, 15.1f), -90f);
            TeaCorner(L, g, new Vector3(R17.Width - 0.38f, 0.9f, 14.0f));
            DishRack(g, new Vector3(R17.Width - 0.38f, 0.9f, 14.8f));
            KitchenTable(g, new Vector3(r2 ? 6.4f : 5.6f, 0f, 14.6f));
            BackDoor(L, g, new Vector3(8.3f, 0f, R17.Depth - 0.02f));
            RoomLight(L, Case01Room.Kitchen, g, new Vector3(5.0f, 2.35f, 15.0f), new Color(1f, 0.86f, 0.66f), 0.95f, false, 2.8f);
        }

        // ======================= 樓梯 =======================

        private static void StairsR(Case01HouseLayout L, Transform g)
        {
            float zLow = R17.StairFootZ, zHigh = R17.StairTopZ;
            float run = zLow - zHigh, width = R17.StairX1 - R17.StairX0, cx = (R17.StairX0 + R17.StairX1) * 0.5f;
            int steps = R17.StairSteps;
            float rise = FloorHeight / steps, depth = run / steps;
            for (int i = 0; i < steps; i++)
            {
                float top = (i + 1) * rise;
                Deco(g, new Vector3(cx, top * 0.5f, zLow - (i + 0.5f) * depth), new Vector3(width, top, depth), i % 2 == 0 ? wood : darkWood, "Step", true);
            }
            float len = Mathf.Sqrt(run * run + FloorHeight * FloorHeight);
            float ang = Mathf.Atan2(FloorHeight, run) * Mathf.Rad2Deg;
            var ramp = new GameObject("StairRamp");
            ramp.transform.SetParent(g, false);
            ramp.transform.localRotation = Quaternion.Euler(ang, 0f, 0f);
            ramp.transform.localPosition = new Vector3(cx, FloorHeight * 0.5f, (zLow + zHigh) * 0.5f) - ramp.transform.up * 0.1f;
            ramp.AddComponent<BoxCollider>().size = new Vector3(width + 0.05f, 0.2f, len);
            var under = new GameObject("UnderStairBlock").AddComponent<BoxCollider>();
            under.transform.SetParent(g, false);
            under.center = new Vector3(cx, 0.65f, zHigh + 1.2f);
            under.size = new Vector3(width, 1.3f, 2.4f);
            // 扶手（靠走廊那側，只是外觀）與起步柱。
            Deco(g, new Vector3(R17.StairX0 + 0.03f, FloorHeight * 0.5f + 0.9f, (zLow + zHigh) * 0.5f), new Vector3(0.05f, 0.06f, len), darkWood, "Handrail", false,
                new Vector3(ang, 0f, 0f));
            Solid(g, new Vector3(R17.StairX0 + 0.06f, 0.55f, zLow + 0.1f), new Vector3(0.12f, 1.1f, 0.12f), darkWood, "Newel");
            L.StairBottomZ = zLow;
            L.StairTopZ = zHigh;
            L.StairAngle = ang;
        }

        // ======================= 二樓 =======================

        private static void UpperR(Case01HouseLayout L, Transform g, bool r2)
        {
            float y = FloorHeight, hx = R17.HallX, ez = R17.ExtensionZ;
            Solid(g, new Vector3(R17.Width * 0.5f, 2f * FloorHeight + 0.1f, ez * 0.5f), new Vector3(R17.Width + 0.4f, 0.2f, ez + 0.4f), ceilMat, "Roof");
            // 窗：臥室臨街兩扇；書房與後段小平台朝後院。
            Window(g, new Vector3(3.0f, y + 1.5f, 0.09f), 0f);
            Window(g, new Vector3(5.2f, y + 1.5f, 0.09f), 0f);
            Window(g, new Vector3(4.6f, y + 1.5f, ez - 0.09f), 0f);
            Window(g, new Vector3(8.4f, y + 1.5f, ez - 0.09f), 0f);
            // 外牆：南牆 x8.6 是第三扇門（平時封住、沒有門框）。
            WallZ(g, 0f, 0f, R17.Width, y, FloorHeight, wallUp, R17.ThirdDoorX);
            WallX(g, 0f, 0f, ez, y, FloorHeight, wallUp);
            WallX(g, R17.Width, 0f, ez, y, FloorHeight, wallUp);
            WallZ(g, ez, 0f, R17.Width, y, FloorHeight, wallUp);
            // 內牆：平台西牆（臥室門、書房門）、臥室｜書房、儲藏室隔牆。
            WallX(g, hx, 0f, ez, y, FloorHeight, wallUp, R17.BedroomDoorZ, R17.StudyDoorZ);
            WallZ(g, R17.LivingZ, R17.StoreX, hx, y, FloorHeight, wallUp);
            WallX(g, R17.StoreX, 0f, ez, y, FloorHeight, wallUp);
            // 樓梯井欄杆：西側（沿走廊）與北側；南端（樓梯頂）開口。
            Solid(g, new Vector3(R17.StairX, y + 0.5f, (R17.StairTopZ + R17.StairFootZ) * 0.5f), new Vector3(0.08f, 1.0f, R17.StairFootZ - R17.StairTopZ), darkWood, "Rail");
            Solid(g, new Vector3((R17.StairX + R17.Width) * 0.5f, y + 0.5f, R17.StairFootZ), new Vector3(R17.Width - R17.StairX, 1.0f, 0.08f), darkWood, "Rail");
            if (r2)
            {
                // R2 二樓分段：前段平台用偏置拱門分成「樓梯頂的小平台」與「通往臥室與第三扇門的前段」，第三扇門不會在樓梯頂一眼看全。
                WallZWide(g, R17.R2LandingSplitZ, hx, R17.Width, y, FloorHeight, wallUp, new Vector2(R17.R2LandingArchX, 1.1f));
                Lining(g, true, R17.R2LandingSplitZ, R17.R2LandingArchX, 1.1f, 0.3f, -1f, y, "LandingArch");
                Lining(g, false, hx, R17.StudyDoorZ, DoorWidth, 0.3f, 1f, y, "StudyDoor");
                Lining(g, false, hx, R17.BedroomDoorZ, DoorWidth, 0.25f, 1f, y, "BedroomDoor");
            }

            // ---------------- 樓梯平台 ----------------
            L.LandingLight = RoomLight(L, Case01Room.Landing, g, new Vector3(8.2f, y + 2.35f, r2 ? 5.4f : 2.8f), new Color(0.86f, 0.88f, 1f), 0.95f, false, y + 2.8f);
            if (r2) L.LampLights.Add(ArenaArt.PointLight(g, new Vector3(8.0f, y + 2.3f, 1.8f), new Color(0.86f, 0.88f, 1f), 4f, 0.6f, false));
            L.LampLights.Add(ArenaArt.PointLight(g, new Vector3(7.7f, y + 2.3f, 10.9f), new Color(0.86f, 0.88f, 1f), 4f, 0.55f, false));
            // 書房門左側門框上的記號（平台側；面向西看門時的左手邊＝南）。
            L.StudyMarkLabel = Case01Hud.WorldLabel(g, new Vector3(hx + 0.09f, y + 1.6f, R17.StudyDoorZ - 0.9f), -90f, "△", 40, new Color(0.95f, 0.95f, 0.9f), 80f);
            Hotspot(L, g, Case01Ids.StudyMark, "書房門框上的記號", new Vector3(hx + 0.15f, y + 1.6f, R17.StudyDoorZ - 0.9f), new Vector3(0.12f, 0.7f, 0.5f));
            var studyPortal = Portal(L, g, Case01Door.StudyDoor, new Vector3(hx, y, R17.StudyDoorZ), Vector3.left, DoorWidth, DoorHeight, false, false);
            studyPortal.Membrane.SetActive(false);
            var thirdFrameMat = Case01Glitch.NewFrame(P);
            L.ThirdDoorFrame = DoorFrame(g, new Vector3(R17.ThirdDoorX, y, 0.1f), 0f, DoorWidth, DoorHeight, thirdFrameMat, "ThirdDoorFrame");
            L.ThirdDoorFrame.SetActive(false);
            GlitchFrame(Portal(L, g, Case01Door.ThirdDoor, new Vector3(R17.ThirdDoorX, y, 0f), Vector3.back, DoorWidth, DoorHeight, true, true), L.ThirdDoorFrame, thirdFrameMat);
            Hotspot(L, g, Case01Ids.ThirdDoor, "外牆上的門框", new Vector3(R17.ThirdDoorX, y + 1.1f, 0.2f), new Vector3(1.1f, 2.0f, 0.1f));
            L.Interactables[Case01Ids.ThirdDoor].gameObject.SetActive(false);
            var creakGo = new GameObject("LandingCreak");
            creakGo.transform.SetParent(g, false);
            creakGo.transform.localPosition = new Vector3(8.5f, y + 1.5f, 2f);
            L.Creak = creakGo.AddComponent<AudioSource>();
            L.Creak.playOnAwake = false;
            L.Creak.spatialBlend = 0.6f;
            L.Creak.clip = CreakClip();
            // 後段小平台：後窗、亞麻布櫃（觀察點：回訪暗示）。
            Solid(g, new Vector3(9.6f, y + 0.9f, 11.35f), new Vector3(0.6f, 1.8f, 0.9f), wood, "LinenCupboard");
            QaSpot(L, g, "qa_obs_window", "後窗", new Vector3(8.4f, y + 1.5f, ez - 0.25f), new Vector3(1.0f, 1.1f, 0.3f),
                "後窗看得到後院的曬衣繩和隔壁的屋頂。書房那一側的窗簾拉得很緊，縫裡透著一點燈光。");

            // ---------------- 臥室（前）：原版臥室整組，錨點 BedroomShift ----------------
            var b = R17.BedroomShift;
            Solid(g, new Vector3(2.4f, y + 0.25f, 8.6f) + b, new Vector3(1.6f, 0.5f, 2.0f), fabric, "Bed");
            Solid(g, new Vector3(1.1f, y + 0.3f, 9.55f) + b, new Vector3(0.5f, 0.6f, 0.5f), wood, "Nightstand");
            Deco(g, new Vector3(1.1f, y + 0.61f, 9.55f) + b, new Vector3(0.16f, 0.01f, 0.11f), paper, "Postcard");
            Hotspot(L, g, Case01Ids.Postcard, "明信片", new Vector3(1.1f, y + 0.75f, 9.5f) + b, new Vector3(0.5f, 0.3f, 0.5f));
            RoomLight(L, Case01Room.Bedroom, g, new Vector3(2.5f, y + 2.3f, 7.5f) + b, new Color(1f, 0.76f, 0.5f), 0.75f, false, y + 2.8f);
            // 儲藏室（不開放）：門在臥室西牆。
            float storeZ = 3.9f;
            Solid(g, new Vector3(R17.StoreX + 0.095f, y + DoorHeight * 0.5f, storeZ), new Vector3(0.04f, DoorHeight, 0.85f), darkWood, "StorageDoor");
            Hotspot(L, g, Case01Ids.StorageDoor, "儲藏室的門", new Vector3(R17.StoreX + 0.2f, y + 1.1f, storeZ), new Vector3(0.12f, 2.0f, 0.85f));

            // ---------------- 書房（後）：原版書房整組，錨點 StudyShift ----------------
            var s = R17.StudyShift;
            Solid(g, new Vector3(7.5f, y + 0.375f, 9.4f) + s, new Vector3(1.9f, 0.75f, 0.8f), darkWood, "Desk");
            Deco(g, new Vector3(6.9f, y + 0.76f, 9.35f) + s, new Vector3(0.5f, 0.01f, 0.36f), paper, "OldMap");
            Deco(g, new Vector3(6.85f, y + 0.77f, 9.35f) + s, new Vector3(0.02f, 0.012f, 0.3f), riftMat, "MapPencil17");
            Hotspot(L, g, Case01Ids.Map, "街區舊地圖", new Vector3(6.85f, y + 0.9f, 9.3f) + s, new Vector3(0.55f, 0.3f, 0.5f));
            Deco(g, new Vector3(7.75f, y + 0.79f, 9.35f) + s, new Vector3(0.3f, 0.05f, 0.22f), P.Get("book_0"), "Notebook");
            Hotspot(L, g, Case01Ids.EllieNotes, "艾琳的手記", new Vector3(7.75f, y + 0.9f, 9.3f) + s, new Vector3(0.45f, 0.3f, 0.5f));
            var bookcaseBlock = new GameObject("BookcaseBlock").AddComponent<BoxCollider>();
            bookcaseBlock.transform.SetParent(g, false);
            bookcaseBlock.center = new Vector3(BookcaseFrontX + BookcaseDepth * 0.5f, y + 1.1f, 7.5f) + s;
            bookcaseBlock.size = new Vector3(BookcaseDepth, 2.2f, 2.0f);
            Deco(g, new Vector3(BookcaseFrontX + 0.15f, y + BookcaseShelves[2] + 0.165f, 7.5f) + s, new Vector3(0.28f, 0.33f, 0.11f), P.Tint("cardboard", new Color(0.45f, 0.45f, 0.5f)), "RecordFolder");
            Deco(g, new Vector3(BookcaseFrontX + 0.008f, y + BookcaseShelves[2] + 0.2f, 7.5f) + s, new Vector3(0.006f, 0.12f, 0.08f), riftMat, "BlackedOut");
            Hotspot(L, g, Case01Ids.OldRecord, "書架上的校正記錄", new Vector3(BookcaseFrontX - 0.05f, y + BookcaseShelves[2] + 0.17f, 7.5f) + s, new Vector3(0.3f, 0.45f, 0.45f));
            Solid(g, new Vector3(7.5f, y + 0.25f, 8.7f) + s, new Vector3(0.45f, 0.5f, 0.45f), darkWood, "DeskChair");
            // 新格局書房比原版多出的西南角：閱讀椅與第二個書架（書房的「高密度」）。
            Solid(g, new Vector3(R17.StoreX + 0.25f, y + 1.0f, 8.0f), new Vector3(0.36f, 2.0f, 1.6f), darkWood, "StudyShelf2");
            Armchair(g, new Vector3(2.65f, y, 6.6f), 40f, fabricGreen);
            RoomLight(L, Case01Room.Study, g, new Vector3(7.5f, y + 2.3f, 7.5f) + s, new Color(1f, 0.93f, 0.82f), 1.05f, false, y + 2.8f);
        }

        // ======================= 新家具／觀察點 =======================

        /// <summary>QA 的環境觀察點（只在 Phase 1.7 QA 格局；不是正式案件線索）。</summary>
        private static void QaSpot(Case01HouseLayout L, Transform g, string id, string prompt, Vector3 pos, Vector3 size, string text)
        {
            Hotspot(L, g, id, prompt, pos, size);
            L.QaObservations[id] = text;
        }

        private static void PhoneTable(Case01HouseLayout L, Transform g, Vector3 pos)
        {
            var t = Group(g, "PhoneTable", pos, -90f);
            Box(t, new Vector3(0f, 0.74f, 0f), new Vector3(0.9f, 0.04f, 0.38f), wood, "Top");
            foreach (var x in new[] { -0.4f, 0.4f }) Box(t, new Vector3(x, 0.37f, 0f), new Vector3(0.04f, 0.74f, 0.34f), darkWood, "Leg");
            Box(t, new Vector3(-0.15f, 0.8f, 0f), new Vector3(0.22f, 0.08f, 0.18f), darkM, "Telephone");
            Box(t, new Vector3(0.2f, 0.78f, 0.02f), new Vector3(0.18f, 0.04f, 0.13f), M(new Color(0.15f, 0.15f, 0.16f), 0.4f), "AnsweringMachine");
            Box(t, new Vector3(0.28f, 0.805f, -0.03f), new Vector3(0.02f, 0.01f, 0.02f), M(new Color(0.3f, 0.05f, 0.05f), 0.2f), "MessageLight");
            Cyl(t, new Vector3(0.38f, 0.82f, 0.08f), new Vector3(0.06f, 0.06f, 0.06f), P.Get("ceramic"), "PenCup");
            Block(t, new Vector3(0f, 0.4f, 0f), new Vector3(0.9f, 0.8f, 0.38f), "PhoneTableBlock");
            QaSpot(L, g, "qa_obs_phone", "電話桌", pos + new Vector3(0f, 0.95f, 0f), new Vector3(0.5f, 0.35f, 0.9f),
                "門口的電話桌。答錄機的燈沒亮；筆筒裡插著兩支同款的筆，一支的筆蓋不見了。");
        }

        private static void ChinaCabinet(Transform g, Vector3 pos)
        {
            var c = Group(g, "ChinaCabinet", pos, 180f);
            Box(c, new Vector3(0f, 0.45f, 0f), new Vector3(1.4f, 0.9f, 0.45f), darkWood, "Base");
            Box(c, new Vector3(0f, 1.45f, -0.08f), new Vector3(1.36f, 1.1f, 0.28f), darkWood, "Hutch");
            for (int i = 0; i < 2; i++) Box(c, new Vector3(0f, 1.2f + i * 0.45f, 0.02f), new Vector3(1.3f, 0.02f, 0.24f), wood, "Shelf");
            for (int i = 0; i < 5; i++) Cyl(c, new Vector3(-0.5f + i * 0.25f, 1.32f, -0.02f), new Vector3(0.22f, 0.006f, 0.22f), P.Get("ceramic"), "DisplayPlate", new Vector3(80f, 0f, 0f));
            Block(c, new Vector3(0f, 1.0f, 0f), new Vector3(1.4f, 2.0f, 0.45f), "ChinaCabinetBlock");
        }

        private static void KitchenTable(Transform g, Vector3 pos)
        {
            var t = Group(g, "KitchenTable", pos, 0f);
            Box(t, new Vector3(0f, 0.74f, 0f), new Vector3(1.2f, 0.04f, 0.8f), wood, "Top");
            for (int i = 0; i < 4; i++) Box(t, new Vector3(i < 2 ? -0.54f : 0.54f, 0.37f, i % 2 == 0 ? -0.34f : 0.34f), new Vector3(0.05f, 0.74f, 0.05f), darkWood, "Leg");
            Block(t, new Vector3(0f, 0.38f, 0f), new Vector3(1.2f, 0.76f, 0.8f), "KitchenTableBlock");
            Box(t, new Vector3(-0.2f, 0.77f, 0.1f), new Vector3(0.3f, 0.02f, 0.22f), paper, "Newspaper", new Vector3(0f, 12f, 0f));
            Cyl(t, new Vector3(0.3f, 0.8f, -0.1f), new Vector3(0.09f, 0.05f, 0.09f), P.Get("ceramic"), "Mug");
            Chair(t, new Vector3(0f, 0f, -0.65f), 0f, "KitchenChair");
            Chair(t, new Vector3(0f, 0f, 0.65f), 180f, "KitchenChair");
        }

        private static void BackDoor(Case01HouseLayout L, Transform g, Vector3 pos)
        {
            var d = Solid(g, pos + new Vector3(0f, DoorHeight * 0.5f, 0f), new Vector3(DoorWidth, DoorHeight, 0.08f), darkWood, "BackDoor");
            Deco(d, new Vector3(-0.35f, 0f, -0.8f), new Vector3(0.06f, 0.04f, 1.5f), metal, "Knob");
            Box(g, pos + new Vector3(0f, 1.6f, -0.05f), new Vector3(0.5f, 0.45f, 0.01f), M(new Color(0.15f, 0.18f, 0.24f), 0.9f), "BackDoorGlass");
            QaSpot(L, g, "qa_obs_backdoor", "後門", pos + new Vector3(0f, 1.1f, -0.15f), new Vector3(1.1f, 2.0f, 0.12f),
                "後門上了兩道鎖。門邊的鑰匙掛鉤有兩個，只有一個掛著鑰匙。");
        }
    }
}
