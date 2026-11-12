using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Case01
{
    /// <summary>Demo 切片中，事務所與街區的互動 ID。</summary>
    public static class Case01DemoIds
    {
        public const string OfficeCase = "office_case";
        public const string OfficeBoard = "office_board";
        public const string OfficeCabinet = "office_cabinet";
        public const string OfficeDoor = "office_door";
        public const string StreetSign = "street_sign";
        public const string StreetListing = "street_listing";
        public const string Plate16 = "street_plate16";
        public const string Plate17 = "street_plate17";
        public const string Plate18 = "street_plate18";
        public const string Door17 = "street_door17";
        public const string StreetBack = "street_back";

        public const string SpawnOfficeStart = "office_start";
        public const string SpawnOfficeReturn = "office_from_street";
        public const string SpawnStreetStart = "street_start";
        public const string SpawnStreetFromHouse = "street_from_house";
    }

    /// <summary>Demo 切片的文字（不寫死艾琳的下落或組織真相；同伴不替玩家報答案）。</summary>
    public static class Case01DemoTexts
    {
        public const string StreetName = "拉克斯珀巷";
        public const string StreetNameEn = "LARKSPUR ROW";

        public const string CaseBrief =
            "<b>新委託：〈第十七號門〉</b>\n委託人：住在" + StreetName + "的鄰居（匿名）。\n" +
            "內容：巷子裡 16 號與 18 號之間，有人看見一扇「不該存在的門」。郵差說那條巷子沒有 17 號；鄰居說那棟屋子的燈有時會亮。\n" +
            "屋主據說是一位叫艾琳的女性，已經一段時間沒人見過她。\n\n<color=#E6BD52>先到現場確認門牌與房屋，再決定是否入內調查。</color>\n<color=#8E98A6>（已接下委託，記入日誌）</color>";
        public const string CaseAcceptedMonologue = "先去看看那條巷子。16 和 18 之間，到底有什麼。";
        public const string CaseAgain = "〈第十七號門〉的委託資料。地址：" + StreetName + "，16 號與 18 號之間。";
        public const string Board =
            "<b>案件公告板</b>\n釘著一張東倫敦的街區圖，幾條巷子用藍色圖釘標出。\n本週：〈第十七號門〉（新）｜遺失的懷錶（結案）｜地鐵站的重複廣播（觀察中）。\n" +
            "<color=#8E98A6>牧羊人的工作：觀測、記錄、校正——把被扭曲的現實接回原位。</color>";
        public const string Cabinet =
            "<b>舊案卷</b>\n抽屜裡是按年份排好的記錄。最上面一份寫著：「觀測先於校正。先確認什麼是真的，再動手。」\n" +
            "<color=#8E98A6>操作：WASD 移動｜滑鼠 視角｜E 調查／互動｜J 日誌｜Esc 釋放滑鼠</color>";
        public const string DoorLocked = "先看看桌上的新委託。";
        public const string ClosureTitle = "〈第十七號門〉　暫時結案";
        public const string ClosureMonologue = "寫下來。十七號的錯接，暫時接回去了。";
        public const string ClosedAgain = "結案記錄已經寫好了。";

        public const string Sign = "<b>路牌</b>\n「" + StreetName + "　" + StreetNameEn + "」。巷子不長，兩側是磚造的聯排住宅。";
        public const string Listing =
            "<b>郵遞名冊</b>\n貼在路燈桿上的住戶名冊：15、16、18、19……\n<color=#C79BFF>沒有 17 號。</color>";
        public const string Plate16 = "門牌「16」。普通的黃銅門牌，擦得很亮。";
        public const string Plate18 = "門牌「18」。門口有一盆快枯掉的天竺葵。";
        public const string Plate17 =
            "門牌「17」。數字的漆看起來很新，可是盯久了，它好像會微微閃一下。\n<color=#C79BFF>名冊上沒有這一號，門卻在這裡。</color>";
        public const string FirstStreetMonologue = "16、18……中間那一棟，門牌掛著 17。";
        public const string NeedSurvey = "先確認一下門牌，還有那張郵遞名冊。";
        public const string EnterHouse = "推開十七號的門。";
        public const string HouseDone = "調查已經告一段落。回事務所寫報告吧。";
        public const string BackLocked = "十七號還沒查清楚。";
        public const string Back = "<b>計程車招呼站</b>\n回事務所。";
        public const string ReturnStreetMonologue = "外面還是一樣安靜。名冊上，依然沒有 17。";

        public static string ClosureBody(bool fp001)
        {
            return "十七號屋內的錯接已局部校正：書房、玄關與那扇本不該存在的門，暫時回到原位。\n" +
                   (fp001 ? "收回的證物：FP-001 十七號的合照（夾層殘留）。\n" : "證物 FP-001 沒有帶回（夾層裡的照片仍留在原處）。\n") +
                   "屋主艾琳的下落：<b>仍未確認</b>。\n\n<color=#8E98A6>Demo 切片到此結束。可以關閉 Play，或從選單重新開始。</color>";
        }
    }

    /// <summary>事務所或街區建好之後的資料。</summary>
    public sealed class Case01DemoAreaLayout
    {
        public Transform Root;
        public readonly Dictionary<string, Case01Pose> Spawns = new Dictionary<string, Case01Pose>();
        public readonly Dictionary<string, Case01Interactable> Interactables = new Dictionary<string, Case01Interactable>();
        public readonly List<Light> Lights = new List<Light>();
        public Text Plate17Label;
        /// <summary>Phase 1.5：路牌（前後兩面）、郵遞名冊、計程車牌的文字（板面文字回歸測試用）。</summary>
        public readonly List<Text> SignLabels = new List<Text>();
        public Text ListingLabel, TaxiLabel;
        public Case01Exterior.Result Exterior;
        /// <summary>v0.7 Phase 1 美術（Blender 模型換皮；資源不存在時為 null）。</summary>
        public Case01AreaArt Art;
        public Color SkyColor = new Color(0.045f, 0.055f, 0.095f);
    }

    /// <summary>
    /// 牧羊人調查事務所（英倫現代都市的小事務所）：辦公桌與委託資料、檔案櫃、案件公告板與街區圖、書櫃、會客沙發、外套架、北窗外的城市夜景、南牆出口。
    /// 單位公尺；房間 x0～9、z0～7、高 3 m。只用 Primitive，graybox 等級（不是正式美術）。
    /// </summary>
    public static class Case01OfficeBuilder
    {
        public const float W = 9f, D = 7f, H = 3f;
        public const float DoorX = 7f;

        public static Case01DemoAreaLayout Build(Transform parent, Case01Palette p)
        {
            var L = new Case01DemoAreaLayout();
            var g = ArenaArt.Group("Case01Office", parent, Vector3.zero);
            L.Root = g;
            var floor = p.Tint("floor_wood", new Color(0.62f, 0.5f, 0.42f));
            var wall = p.Tint("wall_paper", new Color(0.62f, 0.7f, 0.62f));
            var panel = p.Get("wood_dark");
            var wood = p.Get("wood_warm");
            var trim = p.Get("trim_paint");
            var ceil = p.Get("ceiling");
            var paper = p.Get("paper");
            var paperOld = p.Get("paper_old");
            var metal = p.Get("brass");
            var leather = Own(p, new Color(0.28f, 0.14f, 0.1f), 0.45f);
            var steel = Own(p, new Color(0.45f, 0.47f, 0.5f), 0.55f, null, 0.6f);
            var dark = Own(p, new Color(0.06f, 0.06f, 0.07f), 0.7f);
            var cork = Own(p, new Color(0.55f, 0.4f, 0.26f), 0.05f);
            var red = Own(p, new Color(0.7f, 0.12f, 0.1f), 0.3f);
            var manila = Own(p, new Color(0.85f, 0.72f, 0.45f), 0.15f);

            // 地板、天花板、牆（北牆兩扇窗、南牆出口）。
            Solid(g, new Vector3(W * 0.5f, -0.1f, D * 0.5f), new Vector3(W + 0.4f, 0.2f, D + 0.4f), floor, "Floor");
            Box(g, new Vector3(W * 0.5f, H + 0.1f, D * 0.5f), new Vector3(W + 0.4f, 0.2f, D + 0.4f), ceil, "Ceiling");
            WallWithOpenings(g, true, D, 0f, W, wall, new[] { new Vector4(3f, 1.4f, 0.9f, 2.3f), new Vector4(6f, 1.4f, 0.9f, 2.3f) });
            WallWithOpenings(g, true, 0f, 0f, W, wall, new[] { new Vector4(DoorX, 1.1f, 0f, 2.2f) });
            Solid(g, new Vector3(0f, H * 0.5f, D * 0.5f), new Vector3(0.15f, H, D), wall, "Wall");
            Solid(g, new Vector3(W, H * 0.5f, D * 0.5f), new Vector3(0.15f, H, D), wall, "Wall");
            // 牆裙（深色木護牆板）與踢腳線。
            Box(g, new Vector3(0.09f, 0.5f, D * 0.5f), new Vector3(0.03f, 1.0f, D - 0.2f), panel, "Wainscot");
            Box(g, new Vector3(W - 0.09f, 0.5f, D * 0.5f), new Vector3(0.03f, 1.0f, D - 0.2f), panel, "Wainscot");
            Box(g, new Vector3(1.6f, 0.5f, 0.09f), new Vector3(3.0f, 1.0f, 0.03f), panel, "Wainscot");
            Box(g, new Vector3(4.6f, 0.5f, 0.09f), new Vector3(3.0f, 1.0f, 0.03f), panel, "Wainscot");
            for (int i = 0; i < 2; i++)
            {
                float x = 3f + i * 3f;
                Box(g, new Vector3(x, 0.86f, D - 0.14f), new Vector3(1.6f, 0.05f, 0.2f), trim, "WindowSill");
                Box(g, new Vector3(x, 1.65f, D), new Vector3(1.4f, 1.4f, 0.012f), Glass(p), "Glass");
                Box(g, new Vector3(x, 1.65f, D - 0.06f), new Vector3(0.04f, 1.4f, 0.04f), trim, "Mullion");
                Box(g, new Vector3(x, 1.65f, D - 0.06f), new Vector3(1.4f, 0.04f, 0.04f), trim, "Mullion");
            }
            // 出口（南牆）：門片、磨砂玻璃、門牌。
            var door = Box(g, new Vector3(DoorX, 1.1f, 0.03f), new Vector3(1.1f, 2.2f, 0.06f), wood, "OfficeDoor");
            Box(g, new Vector3(DoorX, 1.55f, 0.065f), new Vector3(0.7f, 0.6f, 0.01f), Own(p, new Color(0.75f, 0.78f, 0.8f), 0.6f, new Color(0.08f, 0.08f, 0.09f)), "FrostedGlass");
            Box(g, new Vector3(DoorX + 0.42f, 1.05f, 0.08f), new Vector3(0.05f, 0.05f, 0.08f), metal, "Knob");
            Case01Hud.WorldLabel(g, new Vector3(DoorX, 1.55f, 0.08f), 180f, "SHEPHERD\n<size=14>INVESTIGATIONS</size>", 18, new Color(0.15f, 0.15f, 0.17f), 200f);
            Hot(L, g, Case01DemoIds.OfficeDoor, "出口（往" + Case01DemoTexts.StreetName + "）", new Vector3(DoorX, 1.1f, 0.15f), new Vector3(1.1f, 2.0f, 0.15f));
            Case01Hud.WorldLabel(g, new Vector3(4.5f, 2.45f, D - 0.09f), 0f, "牧羊人調查事務所", 30, new Color(0.9f, 0.82f, 0.6f), 420f);

            // 辦公桌（主物件）：桌面、抽屜、桌燈、電話、筆電、檔案堆，以及新委託的紅標資料夾。
            var desk = ArenaArt.Group("Desk", g, new Vector3(4.5f, 0f, 4.6f));
            Solid(desk, new Vector3(0f, 0.74f, 0f), new Vector3(1.9f, 0.05f, 0.9f), wood, "Top");
            Box(desk, new Vector3(-0.68f, 0.37f, 0f), new Vector3(0.5f, 0.72f, 0.86f), panel, "Pedestal");
            Box(desk, new Vector3(0.68f, 0.37f, 0f), new Vector3(0.5f, 0.72f, 0.86f), panel, "Pedestal");
            Box(desk, new Vector3(0f, 0.45f, 0.4f), new Vector3(0.86f, 0.5f, 0.03f), panel, "ModestyPanel");
            Block(desk, new Vector3(0f, 0.38f, 0f), new Vector3(1.9f, 0.76f, 0.9f), "DeskBlock");
            Box(desk, new Vector3(0.55f, 0.77f, 0.15f), new Vector3(0.34f, 0.02f, 0.24f), dark, "Laptop");
            Box(desk, new Vector3(0.55f, 0.88f, 0.27f), new Vector3(0.34f, 0.22f, 0.015f), dark, "LaptopScreen", new Vector3(-12f, 0f, 0f));
            Box(desk, new Vector3(-0.7f, 0.79f, 0.25f), new Vector3(0.2f, 0.06f, 0.15f), dark, "Telephone");
            for (int i = 0; i < 4; i++) Box(desk, new Vector3(-0.55f, 0.775f + i * 0.02f, -0.15f), new Vector3(0.32f, 0.018f, 0.24f), i % 2 == 0 ? manila : paperOld, "FileStack", new Vector3(0f, i * 5f - 7f, 0f));
            var folder = Box(desk, new Vector3(-0.12f, 0.775f, -0.12f), new Vector3(0.34f, 0.02f, 0.25f), manila, "CaseFolder", new Vector3(0f, -8f, 0f));
            Box(desk, new Vector3(-0.27f, 0.787f, -0.04f), new Vector3(0.05f, 0.006f, 0.08f), red, "CaseTab", new Vector3(0f, -8f, 0f));
            Box(desk, new Vector3(-0.1f, 0.787f, -0.14f), new Vector3(0.24f, 0.004f, 0.16f), paper, "CaseSheet", new Vector3(0f, 4f, 0f));
            Hot(L, g, Case01DemoIds.OfficeCase, "桌上的委託資料", folder.position + Vector3.up * 0.08f, new Vector3(0.45f, 0.25f, 0.4f));
            var lamp = ArenaArt.Group("DeskLamp", desk, new Vector3(0.82f, 0.765f, -0.25f));
            Cyl(lamp, new Vector3(0f, 0.01f, 0f), new Vector3(0.14f, 0.012f, 0.14f), metal, "Base");
            Cyl(lamp, new Vector3(0f, 0.2f, 0f), new Vector3(0.02f, 0.2f, 0.02f), metal, "Stem");
            Cyl(lamp, new Vector3(0f, 0.42f, -0.04f), new Vector3(0.16f, 0.07f, 0.16f), Own(p, new Color(0.15f, 0.35f, 0.2f), 0.6f, new Color(0.05f, 0.1f, 0.06f)), "GreenShade");
            L.Lights.Add(ArenaArt.PointLight(lamp, new Vector3(0f, 0.32f, -0.05f), new Color(1f, 0.85f, 0.62f), 3.5f, 1.1f, false));
            // 辦公椅（桌後）與兩張會客椅。
            Chair(g, new Vector3(4.5f, 0f, 5.45f), 180f, leather, steel);
            Chair(g, new Vector3(3.95f, 0f, 3.55f), 0f, leather, steel);
            Chair(g, new Vector3(5.05f, 0f, 3.55f), 0f, leather, steel);
            Box(g, new Vector3(4.5f, 0.006f, 4.3f), new Vector3(3.2f, 0.012f, 2.6f), Own(p, new Color(0.35f, 0.12f, 0.12f), 0.05f), "Rug");

            // 西牆：三個檔案櫃。
            for (int i = 0; i < 3; i++)
            {
                var cab = ArenaArt.Group("FilingCabinet", g, new Vector3(0.33f, 0f, 1.7f + i * 0.55f));
                cab.localRotation = Quaternion.Euler(0f, 90f, 0f);
                Box(cab, new Vector3(0f, 0.66f, 0f), new Vector3(0.5f, 1.32f, 0.6f), steel, "Body");
                for (int d = 0; d < 4; d++)
                {
                    Box(cab, new Vector3(0f, 0.18f + d * 0.31f, 0.305f), new Vector3(0.44f, 0.27f, 0.01f), Own(p, new Color(0.4f, 0.42f, 0.45f), 0.5f, null, 0.5f), "Drawer");
                    Box(cab, new Vector3(0f, 0.25f + d * 0.31f, 0.32f), new Vector3(0.12f, 0.02f, 0.02f), metal, "Handle");
                }
                Block(cab, new Vector3(0f, 0.66f, 0f), new Vector3(0.5f, 1.32f, 0.6f), "CabinetBlock");
            }
            Hot(L, g, Case01DemoIds.OfficeCabinet, "檔案櫃", new Vector3(0.75f, 1.0f, 2.25f), new Vector3(0.4f, 0.6f, 1.6f));

            // 東牆：案件公告板（軟木板、街區圖、圖釘、紅線、公告）。
            var board = ArenaArt.Group("CaseBoard", g, new Vector3(W - 0.09f, 1.6f, 3.6f));
            board.localRotation = Quaternion.Euler(0f, -90f, 0f);
            Box(board, Vector3.zero, new Vector3(2.2f, 1.3f, 0.04f), cork, "Cork");
            Box(board, new Vector3(-0.35f, 0.05f, -0.025f), new Vector3(1.1f, 0.8f, 0.004f), paperOld, "StreetMap");
            for (int i = 0; i < 6; i++)
                Box(board, new Vector3(-0.7f + (i % 3) * 0.32f, 0.3f - (i / 3) * 0.42f, -0.03f), new Vector3(0.025f, 0.025f, 0.02f), i == 2 ? red : Own(p, new Color(0.2f, 0.35f, 0.8f), 0.4f), "Pin");
            Box(board, new Vector3(0.55f, 0.3f, -0.025f), new Vector3(0.36f, 0.26f, 0.004f), paper, "Notice");
            Box(board, new Vector3(0.62f, -0.15f, -0.025f), new Vector3(0.3f, 0.36f, 0.004f), paper, "Notice");
            Box(board, new Vector3(0.1f, 0.05f, -0.03f), new Vector3(0.9f, 0.006f, 0.003f), red, "String", new Vector3(0f, 0f, 18f));
            Hot(L, g, Case01DemoIds.OfficeBoard, "案件公告板", new Vector3(W - 0.3f, 1.6f, 3.6f), new Vector3(0.4f, 1.2f, 2.0f));

            // 北牆西側書櫃、西牆北側會客沙發、門邊外套架、角落植物。
            var shelf = ArenaArt.Group("Bookshelf", g, new Vector3(1.0f, 0f, D - 0.25f));
            Box(shelf, new Vector3(0f, 1.0f, 0.1f), new Vector3(1.4f, 2.0f, 0.03f), panel, "Back");
            foreach (var x in new[] { -0.7f, 0.7f }) Box(shelf, new Vector3(x, 1.0f, 0f), new Vector3(0.04f, 2.0f, 0.3f), wood, "Side");
            for (int s = 0; s < 5; s++) Box(shelf, new Vector3(0f, 0.08f + s * 0.45f, 0f), new Vector3(1.38f, 0.03f, 0.28f), wood, "Shelf");
            var rng = new System.Random(77);
            for (int s = 0; s < 4; s++)
            {
                float x = -0.64f;
                int n = 0;
                while (x < 0.62f)
                {
                    float w = 0.03f + (float)rng.NextDouble() * 0.04f, h = 0.24f + (float)rng.NextDouble() * 0.14f;
                    Box(shelf, new Vector3(x + w * 0.5f, 0.095f + s * 0.45f + h * 0.5f, -0.02f), new Vector3(w, h, 0.22f), p.Get("book_" + ((n + s) % 7)), "Book");
                    x += w + 0.005f;
                    n++;
                }
            }
            Block(shelf, new Vector3(0f, 1.0f, 0f), new Vector3(1.42f, 2.0f, 0.32f), "ShelfBlock");
            var sofa = ArenaArt.Group("ClientSofa", g, new Vector3(0.55f, 0f, 5.2f));
            sofa.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Box(sofa, new Vector3(0f, 0.22f, 0f), new Vector3(1.7f, 0.3f, 0.8f), leather, "Base");
            Box(sofa, new Vector3(0f, 0.6f, -0.3f), new Vector3(1.7f, 0.55f, 0.2f), leather, "Back");
            foreach (var x in new[] { -0.8f, 0.8f }) Box(sofa, new Vector3(x, 0.4f, 0f), new Vector3(0.14f, 0.4f, 0.8f), leather, "Arm");
            Block(sofa, new Vector3(0f, 0.45f, 0f), new Vector3(1.7f, 0.9f, 0.8f), "SofaBlock");
            var stand = ArenaArt.Group("CoatStand", g, new Vector3(8.3f, 0f, 0.6f));
            Cyl(stand, new Vector3(0f, 0.9f, 0f), new Vector3(0.05f, 0.9f, 0.05f), panel, "Pole");
            Cyl(stand, new Vector3(0f, 0.02f, 0f), new Vector3(0.4f, 0.02f, 0.4f), panel, "Foot");
            ArenaArt.Prim(PrimitiveType.Capsule, stand, new Vector3(0.1f, 1.3f, 0f), new Vector3(0.36f, 0.45f, 0.18f), Own(p, new Color(0.2f, 0.18f, 0.16f), 0.3f), null, "Coat", false, true);
            var plant = ArenaArt.Group("Plant", g, new Vector3(8.4f, 0f, 6.4f));
            ArenaArt.Prim(PrimitiveType.Cylinder, plant, new Vector3(0f, 0.2f, 0f), new Vector3(0.38f, 0.2f, 0.38f), p.Get("pot"), null, "Pot", true, true);
            ArenaArt.Prim(PrimitiveType.Sphere, plant, new Vector3(0f, 0.75f, 0f), new Vector3(0.6f, 0.75f, 0.6f), p.Get("leaf"), null, "Leaves", false, true);

            // 窗外的城市夜景（遠處樓房與亮著的窗；沒有碰撞體、沒有光源）。
            var city = ArenaArt.Group("CityBackdrop", g, Vector3.zero);
            var bldg = Own(p, new Color(0.12f, 0.12f, 0.15f), 0.2f, new Color(0.02f, 0.02f, 0.03f));
            var lit = Own(p, new Color(0.9f, 0.75f, 0.5f), 0.2f, new Color(1.2f, 0.85f, 0.5f));
            for (int i = 0; i < 9; i++)
            {
                float bx = -4f + i * 2.4f, bh = 6f + (float)rng.NextDouble() * 12f, bz = 12f + (float)rng.NextDouble() * 10f;
                Box(city, new Vector3(bx, bh * 0.5f - 3f, bz), new Vector3(2.0f, bh, 2.0f), bldg, "Building");
                for (int k = 0; k < 7; k++)
                    if (rng.NextDouble() < 0.5)
                        Box(city, new Vector3(bx - 0.6f + (k % 3) * 0.6f, (float)rng.NextDouble() * (bh - 2f) - 2f, bz - 1.02f), new Vector3(0.3f, 0.4f, 0.02f), lit, "LitWindow");
            }

            // 燈：兩盞吊燈（一盞有陰影）＋桌燈。
            for (int i = 0; i < 2; i++)
            {
                var pos = new Vector3(2.8f + i * 3.4f, H - 0.55f, 3.6f);
                Cyl(g, pos + new Vector3(0f, 0.3f, 0f), new Vector3(0.015f, 0.25f, 0.015f), panel, "Cord");
                Cyl(g, pos, new Vector3(0.45f, 0.08f, 0.45f), p.Get("lamp_shade"), "Shade");
                var l = ArenaArt.PointLight(g, pos - Vector3.up * 0.1f, new Color(1f, 0.82f, 0.6f), 7.5f, 1.05f, i == 1);
                L.Lights.Add(l);
            }
            L.Spawns[Case01DemoIds.SpawnOfficeStart] = new Case01Pose(new Vector3(6.7f, 0.05f, 1.2f), -25f);
            L.Spawns[Case01DemoIds.SpawnOfficeReturn] = new Case01Pose(new Vector3(DoorX, 0.05f, 0.9f), -10f);
            // 品質回歸 QA 的固定視點。
            L.Spawns["office_desk"] = new Case01Pose(new Vector3(4.5f, 0.05f, 2.6f), 0f);
            L.Spawns["office_board"] = new Case01Pose(new Vector3(6.6f, 0.05f, 3.6f), 90f);
            return L;
        }

        private static void Chair(Transform g, Vector3 pos, float yaw, Material leather, Material steel)
        {
            var c = ArenaArt.Group("Chair", g, pos);
            c.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box(c, new Vector3(0f, 0.46f, 0f), new Vector3(0.5f, 0.08f, 0.5f), leather, "Seat");
            Box(c, new Vector3(0f, 0.8f, -0.23f), new Vector3(0.5f, 0.6f, 0.07f), leather, "Back");
            Cyl(c, new Vector3(0f, 0.22f, 0f), new Vector3(0.05f, 0.22f, 0.05f), steel, "Column");
            Cyl(c, new Vector3(0f, 0.03f, 0f), new Vector3(0.5f, 0.02f, 0.5f), steel, "Base");
            Block(c, new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 1.0f, 0.5f), "ChairBlock");
        }

        /// <summary>沿 x 的牆（固定 z），openings＝(中心 x、寬、底、頂)。</summary>
        internal static void WallWithOpenings(Transform g, bool alongX, float z, float x0, float x1, Material m, Vector4[] openings)
        {
            float cur = x0;
            foreach (var o in openings)
            {
                float s = o.x - o.y * 0.5f, e = o.x + o.y * 0.5f;
                if (s > cur) Solid(g, new Vector3((cur + s) * 0.5f, H * 0.5f, z), new Vector3(s - cur, H, 0.15f), m, "Wall");
                Solid(g, new Vector3(o.x, (o.w + H) * 0.5f, z), new Vector3(o.y, H - o.w, 0.15f), m, "Lintel");
                if (o.z > 0f) Solid(g, new Vector3(o.x, o.z * 0.5f, z), new Vector3(o.y, o.z, 0.15f), m, "Apron");
                cur = e;
            }
            if (x1 > cur) Solid(g, new Vector3((cur + x1) * 0.5f, H * 0.5f, z), new Vector3(x1 - cur, H, 0.15f), m, "Wall");
        }

        internal static Material Glass(Case01Palette p)
        {
            var m = ArenaArt.Mat(new Color(0.62f, 0.7f, 0.78f, 0.14f), 0.94f);
            ShepherdShading.MakeGlass(m);
            return p.Own(m);
        }

        internal static Material Own(Case01Palette p, Color c, float smooth, Color? emission = null, float metallic = 0f)
        {
            return p.Own(ArenaArt.Mat(c, smooth, emission, metallic));
        }

        internal static Transform Box(Transform p, Vector3 pos, Vector3 scale, Material m, string name, Vector3? euler = null)
        {
            return ArenaArt.Prim(PrimitiveType.Cube, p, pos, scale, m, euler, name, false, false);
        }

        internal static Transform Cyl(Transform p, Vector3 pos, Vector3 scale, Material m, string name)
        {
            return ArenaArt.Prim(PrimitiveType.Cylinder, p, pos, scale, m, null, name, false, false);
        }

        internal static Transform Solid(Transform p, Vector3 pos, Vector3 scale, Material m, string name)
        {
            return ArenaArt.Prim(PrimitiveType.Cube, p, pos, scale, m, null, name, true, true);
        }

        internal static void Block(Transform p, Vector3 center, Vector3 size, string name)
        {
            var b = new GameObject(name).AddComponent<BoxCollider>();
            b.transform.SetParent(p, false);
            b.center = center;
            b.size = size;
        }

        internal static Case01Interactable Hot(Case01DemoAreaLayout L, Transform parent, string id, string prompt, Vector3 pos, Vector3 size)
        {
            var go = new GameObject("Interact_" + id);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
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

    /// <summary>
    /// 十七號門外的街區（16／17／18 號那一小段）：沿用 <see cref="Case01Exterior"/> 的夜景，加上與屋內一致的十七號立面（前門 x7.1、一樓前窗依格局）、
    /// 路牌、郵遞名冊、門牌、計程車招呼站、可走的地面與邊界、路燈的點光源。不是任何真實地標；不在街區開戰。
    /// </summary>
    public static class Case01StreetBuilder
    {
        public static Case01DemoAreaLayout Build(Transform parent, Case01Palette p, Case01Floorplan plan)
        {
            var L = new Case01DemoAreaLayout();
            var g = ArenaArt.Group("Case01Street", parent, Vector3.zero);
            L.Root = g;
            L.Exterior = Case01Exterior.Build(g, p);
            var brick17 = p.Tint("court_wall", new Color(0.62f, 0.56f, 0.55f));
            var trim = Case01OfficeBuilder.Own(p, new Color(0.85f, 0.84f, 0.8f), 0.2f, new Color(0.05f, 0.05f, 0.05f));
            var doorMat = Case01OfficeBuilder.Own(p, new Color(0.08f, 0.2f, 0.16f), 0.5f);
            var dark = Case01OfficeBuilder.Own(p, new Color(0.03f, 0.03f, 0.04f), 0.5f);
            var warmWin = Case01OfficeBuilder.Own(p, new Color(0.6f, 0.45f, 0.3f), 0.4f, new Color(0.45f, 0.3f, 0.16f));
            var roof = Case01OfficeBuilder.Own(p, new Color(0.13f, 0.13f, 0.15f), 0.3f);
            var metal = Case01OfficeBuilder.Own(p, new Color(0.08f, 0.08f, 0.09f), 0.5f, null, 0.6f);
            var brass = p.Get("brass");
            var paper = p.Get("paper");
            var signMat = Case01OfficeBuilder.Own(p, new Color(0.9f, 0.9f, 0.88f), 0.3f, new Color(0.08f, 0.08f, 0.08f));

            // 十七號的立面（與屋內一致：前門 x7.1；二樓正面沒有窗——只有一道很淡的門形痕跡）。
            var h17 = ArenaArt.Group("House17", g, Vector3.zero);
            Case01OfficeBuilder.Box(h17, new Vector3(5f, 3.1f, 5f), new Vector3(9.9f, 6.2f, 10f), brick17, "Body");
            for (int s = -1; s <= 1; s += 2)
            {
                var rp = ArenaArt.Prim(PrimitiveType.Cube, h17, Vector3.zero, new Vector3(10.2f, 0.12f, 6f), roof, new Vector3(s * -32f, 0f, 0f), "Roof", false, true);
                rp.localPosition = new Vector3(5f, 6.9f, 5f + s * 2.4f);
            }
            Case01OfficeBuilder.Box(h17, new Vector3(7.1f, 1.1f, -0.04f), new Vector3(1.1f, 2.2f, 0.08f), doorMat, "FrontDoor17");
            Case01OfficeBuilder.Box(h17, new Vector3(7.1f, 2.33f, -0.07f), new Vector3(1.3f, 0.12f, 0.12f), trim, "DoorHead");
            Case01OfficeBuilder.Box(h17, new Vector3(7.45f, 1.05f, -0.1f), new Vector3(0.05f, 0.05f, 0.06f), brass, "Knob");
            var windows = plan == Case01Floorplan.V05A ? new[] { 1.6f, 3.8f } : new[] { 2.5f };
            foreach (var x in windows)
            {
                Case01OfficeBuilder.Box(h17, new Vector3(x, 1.5f, -0.03f), new Vector3(1.0f, 1.1f, 0.04f), warmWin, "Window17");
                Case01OfficeBuilder.Box(h17, new Vector3(x, 0.9f, -0.1f), new Vector3(1.2f, 0.08f, 0.14f), trim, "Sill");
            }
            // v0.7：拿掉二樓正面的「門形痕跡」——從街上看起來像室內異常門外洩（敘事上不該在屋外先透露）。
            // 十七號在街上的異常只由「名冊沒有這一號」與門牌偶爾閃一下表現。
            // 門牌。
            Plate(L, g, new Vector3(7.85f, 1.7f, -0.11f), "17", Case01DemoIds.Plate17, "門牌「17」", brass);
            L.Plate17Label = L.Interactables[Case01DemoIds.Plate17].GetComponentInChildren<Text>();
            var d16 = L.Exterior.House16.TransformPoint(new Vector3(-6.4f * 0.28f, 0f, -5f));
            var d18 = L.Exterior.House18.TransformPoint(new Vector3(-6.4f * 0.28f, 0f, -5f));
            Plate(L, g, d16 + new Vector3(0.75f, 1.7f, -0.11f), "16", Case01DemoIds.Plate16, "門牌「16」", brass);
            Plate(L, g, d18 + new Vector3(0.75f, 1.7f, -0.11f), "18", Case01DemoIds.Plate18, "門牌「18」", brass);
            Case01OfficeBuilder.Hot(L, g, Case01DemoIds.Door17, "十七號的門", new Vector3(7.1f, 1.1f, -0.25f), new Vector3(1.1f, 2.0f, 0.3f));

            // 路牌、郵遞名冊（貼在路燈桿旁的小告示板）、計程車招呼站。
            var sign = ArenaArt.Group("StreetSign", g, new Vector3(-1.2f, 0f, -2.3f));
            Case01OfficeBuilder.Box(sign, new Vector3(0f, 1.1f, 0f), new Vector3(0.08f, 2.2f, 0.08f), metal, "Post");
            // Phase 1.5 P0：路牌字原本 3 m 寬的畫布塞一行中英文，實際超出 1.5 m 牌面。改成兩行、依文字實際尺寸縮到牌面內，背面也有一份（不會從背後看到反字）。
            Case01OfficeBuilder.Box(sign, new Vector3(0f, 2.15f, 0f), new Vector3(1.5f, SignPlateH, 0.04f), signMat, "Plate");
            string signText = Case01DemoTexts.StreetName + "\n<size=15>" + Case01DemoTexts.StreetNameEn + "</size>";
            foreach (var side in new[] { -1f, 1f })
                L.SignLabels.Add(Case01Hud.WorldLabel(sign, new Vector3(0f, 2.15f, side * 0.03f), side < 0f ? 0f : 180f, signText, 22, new Color(0.1f, 0.1f, 0.12f), 300f));
            foreach (var lab in L.SignLabels) Case01Hud.FitLabel(lab, 1.5f - 2f * SignMargin, SignPlateH - 2f * SignMargin);
            Case01OfficeBuilder.Hot(L, g, Case01DemoIds.StreetSign, "路牌", new Vector3(-1.2f, 1.8f, -2.3f), new Vector3(1.6f, 1.0f, 0.4f));
            var listing = ArenaArt.Group("PostalListing", g, new Vector3(-3.4f, 0f, -2.25f));
            Case01OfficeBuilder.Box(listing, new Vector3(0f, 0.75f, 0f), new Vector3(0.08f, 1.5f, 0.08f), metal, "Post");
            Case01OfficeBuilder.Box(listing, new Vector3(0f, 1.45f, -0.05f), new Vector3(0.6f, 0.75f, 0.03f), Case01OfficeBuilder.Own(p, new Color(0.15f, 0.25f, 0.2f), 0.4f), "Board");
            Case01OfficeBuilder.Box(listing, new Vector3(0f, 1.45f, -0.07f), new Vector3(0.48f, 0.62f, 0.004f), paper, "List");
            L.ListingLabel = Case01Hud.WorldLabel(listing, new Vector3(0f, 1.5f, -0.08f), 0f, "郵遞名冊\n<size=14>15 · 16 · 18 · 19</size>", 16, new Color(0.15f, 0.15f, 0.15f), 120f);
            Case01Hud.FitLabel(L.ListingLabel, 0.48f - 2f * SignMargin, 0.5f);
            Case01OfficeBuilder.Hot(L, g, Case01DemoIds.StreetListing, "郵遞名冊", new Vector3(-3.4f, 1.45f, -2.4f), new Vector3(0.8f, 0.9f, 0.4f));
            var taxi = ArenaArt.Group("TaxiStop", g, new Vector3(-10.8f, 0f, -3.65f));
            Case01OfficeBuilder.Box(taxi, new Vector3(0f, 1.2f, 0f), new Vector3(0.08f, 2.4f, 0.08f), metal, "Post");
            Case01OfficeBuilder.Box(taxi, new Vector3(0f, 2.3f, 0f), new Vector3(0.7f, 0.4f, 0.04f), Case01OfficeBuilder.Own(p, new Color(0.95f, 0.8f, 0.2f), 0.3f, new Color(0.35f, 0.28f, 0.05f)), "Sign");
            L.TaxiLabel = Case01Hud.WorldLabel(taxi, new Vector3(0f, 2.3f, -0.03f), 0f, "TAXI\n<size=14>回事務所</size>", 20, new Color(0.1f, 0.1f, 0.1f), 140f);
            Case01Hud.FitLabel(L.TaxiLabel, 0.7f - 2f * SignMargin, 0.4f - 2f * SignMargin);
            Case01OfficeBuilder.Hot(L, g, Case01DemoIds.StreetBack, "計程車招呼站（回事務所）", new Vector3(-10.8f, 1.6f, -3.65f), new Vector3(0.9f, 1.6f, 0.9f));

            // 可走的地面與邊界（外景本身沒有碰撞體）。
            Case01OfficeBuilder.Block(g, new Vector3(5f, -0.1f, -6f), new Vector3(40f, 0.2f, 16f), "WalkFloor");
            Case01OfficeBuilder.Block(g, new Vector3(5f, 2f, 0.25f), new Vector3(40f, 4f, 0.4f), "FacadeLine");
            Case01OfficeBuilder.Block(g, new Vector3(5f, 2f, -12.7f), new Vector3(40f, 4f, 0.4f), "SouthEdge");
            Case01OfficeBuilder.Block(g, new Vector3(-12.6f, 2f, -6f), new Vector3(0.4f, 4f, 14f), "WestEdge");
            Case01OfficeBuilder.Block(g, new Vector3(22.6f, 2f, -6f), new Vector3(0.4f, 4f, 14f), "EastEdge");
            Case01OfficeBuilder.Block(g, new Vector3(2.9f, 0.5f, Case01Exterior.PavementNorth + 0.1f), new Vector3(7.6f, 1f, 0.22f), "FrontWallBlock");
            Case01OfficeBuilder.Block(g, new Vector3(9.15f, 0.5f, Case01Exterior.PavementNorth + 0.1f), new Vector3(2.5f, 1f, 0.22f), "FrontWallBlock");
            // 鄰居家前院的矮牆（16、18 號也有前院，路線才清楚）。v0.7：碰撞處也有看得到的矮牆（不再是隱形牆）。
            Case01OfficeBuilder.Block(g, new Vector3(-6.3f, 0.5f, Case01Exterior.PavementNorth + 0.1f), new Vector3(9.4f, 1f, 0.22f), "FrontWallBlock");
            Case01OfficeBuilder.Block(g, new Vector3(16.3f, 0.5f, Case01Exterior.PavementNorth + 0.1f), new Vector3(9.4f, 1f, 0.22f), "FrontWallBlock");
            var wallMat = p.Tint("court_wall", new Color(0.62f, 0.5f, 0.46f));
            Case01OfficeBuilder.Box(g, new Vector3(-6.3f, 0.35f, Case01Exterior.PavementNorth + 0.1f), new Vector3(9.4f, 0.7f, 0.22f), wallMat, "NeighbourWall");
            Case01OfficeBuilder.Box(g, new Vector3(16.3f, 0.35f, Case01Exterior.PavementNorth + 0.1f), new Vector3(9.4f, 0.7f, 0.22f), wallMat, "NeighbourWall");
            foreach (var lamp in L.Exterior.Lamps)
            {
                Case01OfficeBuilder.Block(g, lamp.position + new Vector3(0f, 1.2f, 0f), new Vector3(0.25f, 2.4f, 0.25f), "LampBlock");
                L.Lights.Add(ArenaArt.PointLight(g, lamp.position + new Vector3(0f, 4.0f, -0.72f), new Color(1f, 0.8f, 0.55f), 11f, 1.35f, false));
            }
            L.Lights.Add(ArenaArt.PointLight(g, new Vector3(7.1f, 2.6f, -0.8f), new Color(0.75f, 0.78f, 0.95f), 4f, 0.5f, false));
            L.Spawns[Case01DemoIds.SpawnStreetStart] = new Case01Pose(new Vector3(-10.2f, 0.05f, -3.0f), 90f);
            L.Spawns[Case01DemoIds.SpawnStreetFromHouse] = new Case01Pose(new Vector3(7.1f, 0.05f, -1.0f), 180f);
            // 品質回歸 QA 的固定視點（從對街看 17 號、17 號門前、郵遞名冊）。
            L.Spawns["street_qa17"] = new Case01Pose(new Vector3(5.5f, 0.05f, -9.5f), 10f);
            L.Spawns["street_door17"] = new Case01Pose(new Vector3(7.1f, 0.05f, -3.2f), 0f);
            L.Spawns["street_listing"] = new Case01Pose(new Vector3(-3.4f, 0.05f, -3.8f), 0f);
            return L;
        }

        /// <summary>Phase 1.5：牌面高度與文字到邊緣的留白（公尺）。</summary>
        public const float SignPlateH = 0.44f, SignMargin = 0.04f, PlateMargin = 0.02f;

        private static void Plate(Case01DemoAreaLayout L, Transform g, Vector3 pos, string number, string id, string prompt, Material brass)
        {
            var it = Case01OfficeBuilder.Hot(L, g, id, prompt, pos + new Vector3(0f, 0f, -0.1f), new Vector3(0.45f, 0.4f, 0.3f));
            Case01OfficeBuilder.Box(g, pos, new Vector3(0.24f, 0.17f, 0.02f), brass, "Plate" + number);
            var label = Case01Hud.WorldLabel(it.transform, new Vector3(0f, 0f, 0.085f), 0f, number, 34, new Color(0.08f, 0.07f, 0.05f), 80f);
            Case01Hud.FitLabel(label, 0.24f - 2f * PlateMargin, 0.17f - 2f * PlateMargin);
        }
    }
}
