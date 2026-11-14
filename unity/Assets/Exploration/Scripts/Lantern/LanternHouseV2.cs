using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;
using Mode = Shepherd.Exploration.Lantern.LanternReveal.Mode;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 第十七號門 v2（灰盒，見 docs/level/CASE01_INVESTIGATION_AND_LAYOUT_V2.md）：
    /// 英式聯排屋的外殼＋後方 L 形加建＋東亞移民家庭的室內。樓下是母親的改衣店，樓上是住家。
    /// 這一版先蓋：門外的街道與開場（門牌閃爍、舉起提燈才看得到的門、進去後門封起來）、一樓（店面、走廊、餐廳、廚房、側院）、
    /// 通往二樓的樓梯（二樓暫時封住）。和舊房子並存，舊房子的測試不受影響。
    /// 尺寸以真實公尺描述（寬 6.5、主屋深 10、加建深 5），遊戲內放大 1.3 倍。
    /// </summary>
    public sealed partial class LanternGraybox
    {
        public static readonly Vector3 V2Origin = new Vector3(130f, 0f, 0f);
        public const float V2Scale = 1.3f;
        public const float V2Width = 6.5f, V2Main = 10f, V2Depth = 15f, V2Ground = 3.0f, V2Upper = 2.8f, V2ExtWidth = 4.0f;

        public Transform V2Root { get; private set; }
        public Bounds V2Bounds { get; private set; }
        public Bounds V2Interior { get; private set; }
        /// <summary>前門：暗處是釘死的磚牆，提燈照到才是門；進去之後封起來。</summary>
        public LanternAnomalyBlocker V2FrontDoor { get; private set; }
        public bool V2Sealed { get; private set; }
        public Vector3 V2SealedAt { get; private set; }
        public Transform V2SealWall { get; private set; }
        /// <summary>開著的前門（門板、鑲板、門把）；封門後消失。</summary>
        public Transform V2FrontDoorLeaf { get; private set; }
        public readonly List<LanternToggle> V2Doors = new List<LanternToggle>();
        /// <summary>推門（拉鋸）的門。</summary>
        public readonly List<LanternStruggle> Struggles = new List<LanternStruggle>();
        public LanternStruggle V2DiningStruggle { get; private set; }
        /// <summary>謎題用的可使用物件。</summary>
        public readonly List<LanternUsable> Usables = new List<LanternUsable>();
        /// <summary>新房子的電箱修好了（店裡的燈穩定亮起）。</summary>
        public bool V2Powered { get; set; }
        /// <summary>修好的房間裡常亮的暖燈（新房子）。</summary>
        public readonly HashSet<Light> V2SafeLamps = new HashSet<Light>();
        private Material v2Plate, v2Brick;
        private Texture v2Num16, v2Num17;
        private float v2FlickerTimer;
        private bool v2Show17;
        private Vector3 V2(float x, float y, float z) => V2Origin + new Vector3(x * V2Scale, y * (1f + (V2Scale - 1f) * 0.55f), z * V2Scale);

        public void BuildHouseV2()
        {
            if (V2Root != null) { ArenaArt.Kill(V2Root.gameObject); LanternNav.Remove("v2"); }
            V2Doors.Clear();
            Usables.RemoveAll(x => x == null || V2Root == null || x.transform.IsChildOf(V2Root));
            V2Powered = false;
            V2SafeLamps.Clear();
            Struggles.RemoveAll(x => x == null || V2Root == null || x.transform.IsChildOf(V2Root));
            V2Sealed = false;
            HouseMaterials();
            V2Root = new GameObject("LanternHouseV2").transform;
            V2Root.SetParent(Root, false);
            // 暫時借用住宅的建造工具（HP、牆、窗、門框、踢腳板）：原點與倍率換成 v2，蓋完還原。
            var savedOrigin = hpOrigin;
            var savedScale = HouseScale;
            var savedInto = into;
            hpOrigin = V2Origin;
            HouseScale = V2Scale;
            into = V2Root;
            try
            {
                V2Street();
                V2Shell();
                V2StairsAndUpper();
                V2Furnish();
                V2Lights();
            }
            finally
            {
                hpOrigin = savedOrigin;
                HouseScale = savedScale;
                into = savedInto;
            }
            YardDoorToMarket();
            V2Bounds = new Bounds(V2(V2Width / 2f, 2f, V2Depth / 2f), new Vector3(V2Width * V2Scale + 2f, 9f, V2Depth * V2Scale + 2f));
            var inside = new Bounds();
            inside.SetMinMax(V2(0.1f, -0.5f, 0.1f), V2(V2Width - 0.1f, V2Ground + V2Upper, V2Depth - 0.1f));
            V2Interior = inside;
            Physics.SyncTransforms();
            var nav = V2Bounds;
            nav.Expand(new Vector3(2f, 2f, 12f));
            LanternNav.Build("v2", nav);
        }

        // ---------------- 門外：街道與開場 ----------------

        /// <summary>
        /// 街區（有人在生活的城市一角）＋16 號與 18 號之間的窄巷。巷底是死路：一面普通的磚牆。
        /// 17 號在所有人的認知裡都不存在，只有提燈照得出牆上的門。
        /// 巷子：x 4.2～6.0、z -10～0（真實公尺），巷底就是 17 號的門面。
        /// </summary>
        private void V2Street()
        {
            float zs = -10f, ax0 = 4.2f, ax1 = 6.0f;
            var road = Mat("V2_Road", Mode.Dual, new Color(0.16f, 0.16f, 0.17f), new Color(0.12f, 0.13f, 0.14f), null, null, 0.45f, 0.45f, 0.004f, true);
            var pave = PH("V2_Pavement", "concrete_floor_worn_001", "Tex", 1.5f, new Color(0.72f, 0.7f, 0.68f), new Color(0.4f, 0.45f, 0.5f), 0.55f);
            v2Brick = Mat("V2_Brick", Mode.Dual, new Color(0.5f, 0.27f, 0.2f), new Color(0.3f, 0.28f, 0.29f), null, null, 0.2f, 0.25f, 0.006f, true);
            LanternReveal.AddRuin(v2Brick, "raw_plank_wall", 0f, new Color(0.5f, 0.52f, 0.55f), 0.3f);
            var brick2 = Mat("V2_Brick2", Mode.Dual, new Color(0.36f, 0.3f, 0.27f), new Color(0.28f, 0.28f, 0.3f), null, null, 0.2f, 0.25f, 0.006f, true);
            var cream = Mat("V2_Stucco", Mode.Dual, new Color(0.82f, 0.78f, 0.7f), new Color(0.5f, 0.52f, 0.55f), null, null, 0.2f, 0.25f, 0.006f, true);
            var warmWin = Mat("V2_WarmWindow", Mode.Dual, new Color(0.9f, 0.7f, 0.45f), new Color(0.9f, 0.7f, 0.45f), null, null, 0.8f, 0.8f, 0f);
            warmWin.SetColor("_EmissionColor", new Color(1.6f, 1.05f, 0.55f));
            warmWin.SetColor("_AnomalyEmission", new Color(1.6f, 1.05f, 0.55f));
            var tvWin = Mat("V2_TVWindow", Mode.Dual, new Color(0.5f, 0.6f, 0.8f), new Color(0.5f, 0.6f, 0.8f), null, null, 0.8f, 0.8f, 0f);
            tvWin.SetColor("_EmissionColor", new Color(0.55f, 0.75f, 1.2f));
            tvWin.SetColor("_AnomalyEmission", new Color(0.55f, 0.75f, 1.2f));
            var dark = Mat("V2_DarkWindow", Mode.Dual, new Color(0.04f, 0.05f, 0.07f), new Color(0.03f, 0.04f, 0.05f), null, null, 0.9f, 0.9f, 0f);
            var neon = Mat("V2_Neon", Mode.Dual, new Color(1f, 0.3f, 0.25f), new Color(1f, 0.3f, 0.25f), null, null, 0.5f, 0.5f, 0f);
            neon.SetColor("_EmissionColor", new Color(3.5f, 0.7f, 0.5f));
            neon.SetColor("_AnomalyEmission", new Color(3.5f, 0.7f, 0.5f));
            var red = Mat("V2_PhoneRed", Mode.Dual, new Color(0.65f, 0.08f, 0.06f), new Color(0.65f, 0.08f, 0.06f), null, null, 0.55f, 0.55f, 0f);
            var shadowMat = Mat("V2_Silhouette", Mode.Dual, new Color(0.08f, 0.06f, 0.05f), new Color(0.08f, 0.06f, 0.05f), null, null, 0.1f, 0.1f, 0f);

            // 街道是現實：所有材質永遠顯示真實的樣子（提燈照了也不變）。只有巷底藏門的牆與閃爍的門牌例外。
            foreach (var sm in new[] { road, pave, brick2, cream, warmWin, tvWin, dark, neon, red, shadowMat }) sm.SetFloat("_Mode", (float)Mode.AlwaysReal);
            // 路面、人行道、路緣。
            Box("V2Road", V2(3f, 0f, zs - 6.5f) + Vector3.down * 0.1f, new Vector3(56f, 0.2f, 5.5f * V2Scale), road);
            Box("V2Pavement", V2(3f, 0f, zs - 1.9f) + Vector3.down * 0.06f, new Vector3(56f, 0.12f, 3.8f * V2Scale), pave);
            Box("V2PavementFar", V2(3f, 0f, zs - 11.2f) + Vector3.down * 0.06f, new Vector3(56f, 0.12f, 3.8f * V2Scale), pave);
            Box("V2Kerb", V2(3f, 0f, zs - 3.8f), new Vector3(56f, 0.14f, 0.15f), Real(hWhite), false);
            Box("V2KerbFar", V2(3f, 0f, zs - 9.3f), new Vector3(56f, 0.14f, 0.15f), Real(hWhite), false);
            // 巷子的地面（石板、濕）。
            var cobble = PH("V2_Cobble", "concrete_floor_worn_001", "Tex", 0.9f, new Color(0.5f, 0.5f, 0.52f), new Color(0.35f, 0.38f, 0.42f), 0.75f);
            cobble.SetFloat("_Mode", (float)Mode.AlwaysReal);
            Box("V2AlleyFloor", V2((ax0 + ax1) / 2f, 0f, zs / 2f) + Vector3.down * 0.05f, new Vector3((ax1 - ax0) * V2Scale + 0.4f, 0.1f, -zs * V2Scale + 0.4f), cobble);

            // 16 號（左）與 18 號（右）：兩棟聯排屋，中間夾著巷子。
            void Terrace(string num, float x0, float x1, Material wall, bool left)
            {
                float cx = (x0 + x1) / 2f, w = x1 - x0;
                Box("V2House" + num, V2(cx, 3.2f, zs / 2f), new Vector3(w * V2Scale, 6.6f * 1.165f, -zs * V2Scale), wall);
                Box("V2Roof" + num, V2(cx, 6.75f, zs / 2f), new Vector3(w * V2Scale + 0.3f, 0.3f, -zs * V2Scale), Real(hDark));
                float doorX = left ? x1 - 1.4f : x0 + 1.4f;
                Box("V2Door" + num, V2(doorX, 1.1f, zs - 0.02f), new Vector3(1.0f * V2Scale, 2.2f * 1.165f, 0.08f), num == "16" ? red : Real(hDark), true);
                Box("V2Step" + num, V2(doorX, 0.08f, zs - 0.35f), new Vector3(1.3f * V2Scale, 0.16f, 0.5f), Real(hWhite), true);
                var nm = Mat("V2_Num" + num, Mode.Dual, Color.white, Color.white,
                    LanternReveal.Number(int.Parse(num), new Color32(230, 222, 205, 255), new Color32(30, 30, 30, 255)),
                    LanternReveal.Number(int.Parse(num), new Color32(230, 222, 205, 255), new Color32(30, 30, 30, 255)), 0.3f, 0.3f, 0f);
                Quad("V2Number" + num, V2(doorX, 2.55f, zs - 0.06f), new Vector3(0.26f, 0.26f, 1f), Vector3.zero, nm);
                // 窗：樓下一扇亮著暖光、樓上一扇電視的藍光、一扇暗著。
                float winX = left ? x0 + 2.0f : x1 - 2.0f;
                Box("V2Win" + num, V2(winX, 1.6f, zs - 0.03f), new Vector3(2.2f * V2Scale, 1.4f, 0.04f), warmWin, false);
                Box("V2WinUp" + num, V2(winX, 4.6f, zs - 0.03f), new Vector3(1.5f * V2Scale, 1.3f, 0.04f), num == "18" ? tvWin : dark, false);
                Box("V2WinUp2" + num, V2(doorX, 4.6f, zs - 0.03f), new Vector3(1.2f * V2Scale, 1.3f, 0.04f), num == "16" ? warmWin : dark, false);
                if (!V2Day) AddLight("V2WinLight" + num, LightType.Point, V2(winX, 1.6f, zs - 0.6f), new Color(1f, 0.72f, 0.45f), 1.4f, 4.5f, false, LightShadows.None);
                // 窗框與窗台的花盆。
                Box("V2Sill" + num, V2(winX, 0.85f, zs - 0.12f), new Vector3(2.4f * V2Scale, 0.08f, 0.22f), Real(hWhite), false);
                Model("V2SillPlant" + num, "potted_plant_02", winX + 0.6f, zs - 0.12f, 0.9f, 0.35f, 0f, false, Real(hWood), Vector3.one * 0.2f, Mode.AlwaysReal);
            }
            Terrace("16", -9.5f, ax0, cream, true);
            Terrace("18", ax1, 20f, v2Brick, false);
            // 窗裡走動的人影（16 號樓下的窗）。
            v2Silhouette = Quad("V2Silhouette", V2(-7.5f, 1.5f, zs) + Vector3.back * 0.02f, new Vector3(0.55f, 1.1f, 1f), Vector3.zero, shadowMat);
            v2SilhouetteBase = v2Silhouette.position;

            // 對街：咖啡店（暖光、OPEN 霓虹）、雜貨店、樓上的公寓。
            float zf = zs - 13.2f;
            Box("V2Opposite", V2(3f, 3.5f, zf) + Vector3.back * 1.6f, new Vector3(42f, 7f * 1.165f, 3f), brick2);
            Box("V2CafeWindow", V2(-1.5f, 1.4f, zf), new Vector3(4.5f * V2Scale, 1.8f, 0.05f), warmWin, false);
            Box("V2CafeNeon", V2(-1.5f, 2.7f, zf + 0.02f), new Vector3(1.2f, 0.25f, 0.05f), neon, false);
            Box("V2ShopWindow", V2(7.5f, 1.4f, zf), new Vector3(3.5f * V2Scale, 1.8f, 0.05f), warmWin, false);
            for (int i = 0; i < 6; i++) Box("V2FlatWindow", V2(-6f + i * 3.6f, 4.8f, zf), new Vector3(1.4f * V2Scale, 1.3f, 0.05f), i % 3 == 1 ? dark : (i % 2 == 0 ? warmWin : tvWin), false);
            if (!V2Day) AddLight("V2CafeLight", LightType.Point, V2(-1.5f, 1.4f, zf + 0.8f), new Color(1f, 0.7f, 0.42f), 4f, 9f, false, LightShadows.None);
            if (!V2Day) AddLight("V2ShopLight", LightType.Point, V2(7.5f, 1.4f, zf + 0.8f), new Color(1f, 0.85f, 0.6f), 3f, 8f, false, LightShadows.None);
            if (!V2Day) AddLight("V2NeonLight", LightType.Point, V2(-1.5f, 2.7f, zf + 0.5f), new Color(1f, 0.3f, 0.25f), 1.2f, 3.5f, false, LightShadows.None);

            // 維多利亞式街燈（暖光）。
            foreach (var lx in new[] { -7f, 1.0f, 10.5f, 17f })
            {
                Cyl("V2LampPost", V2(lx, 0f, zs - 3.4f) + Vector3.up * 2.3f, new Vector3(0.1f, 2.3f, 0.1f), Real(hDark));
                Box("V2LampHead", V2(lx, 0f, zs - 3.4f) + Vector3.up * 4.75f, new Vector3(0.36f, 0.45f, 0.36f), warmWin, false);
                if (!V2Day) AddLight("V2StreetLamp", LightType.Point, V2(lx, 0f, zs - 3.4f) + Vector3.up * 4.6f, new Color(1f, 0.68f, 0.38f), 9f, 15f, false, LightShadows.Soft);
            }
            // 紅色電話亭、垃圾桶、腳踏車架。
            var booth = V2(2.6f, 0f, zs - 2.9f);
            Box("V2PhoneBox", booth + Vector3.up * 1.25f, new Vector3(0.95f, 2.5f, 0.95f), red, true);
            Box("V2PhoneBoxGlow", booth + new Vector3(0f, 1.5f, -0.48f), new Vector3(0.7f, 1.3f, 0.02f), warmWin, false);
            Box("V2PhoneBoxSign", booth + new Vector3(0f, 2.35f, -0.49f), new Vector3(0.6f, 0.14f, 0.02f), warmWin, false);
            if (!V2Day) AddLight("V2PhoneBoxLight", LightType.Point, booth + Vector3.up * 1.6f, new Color(1f, 0.85f, 0.6f), 0.8f, 3f, false, LightShadows.None);
            Box("V2Bin", V2(ax1 + 0.35f, 0.55f, zs + 0.6f), new Vector3(0.6f, 1.1f, 0.6f), Real(hDark), true);
            Box("V2Bin2", V2(ax0 - 0.35f, 0.55f, zs - 0.6f), new Vector3(0.6f, 1.1f, 0.6f), cream, true);

            // 巷子：牆燈、水管、晾衣繩、木箱、積水。
            // 巷底的一盞老牆燈：看得到死路那面牆（這裡應該什麼都沒有）。
            var endLamp = V2(ax1, 2.7f, -1.4f) + Vector3.left * 0.12f;
            Box("V2AlleyEndLampBox", endLamp, new Vector3(0.12f, 0.2f, 0.28f), warmWin, false);
            AddLight("V2AlleyEndLamp", LightType.Point, endLamp + Vector3.left * 0.35f, new Color(1f, 0.66f, 0.4f), 2.4f, 6.5f, true, LightShadows.Soft);
            var bulkhead = V2(ax1, 2.6f, zs + 2.5f) + Vector3.left * 0.12f;
            Box("V2AlleyLampBox", bulkhead, new Vector3(0.12f, 0.2f, 0.28f), warmWin, false);
            if (!V2Day) AddLight("V2AlleyLamp", LightType.Point, bulkhead + Vector3.left * 0.3f, new Color(1f, 0.7f, 0.45f), 1.8f, 6f, false, LightShadows.Soft);
            Cyl("V2DrainPipe", V2(ax0, 3f, zs + 5f) + Vector3.right * 0.08f, new Vector3(0.09f, 3f, 0.09f), Real(hDark));
            Cyl("V2ClothesLine", V2((ax0 + ax1) / 2f, 3.4f, zs + 4f), new Vector3(0.01f, (ax1 - ax0) * V2Scale / 2f, 0.01f), Real(hDark), new Vector3(0f, 0f, 90f));
            for (int i = 0; i < 3; i++) Box("V2Laundry", V2(ax0 + 0.4f + i * 0.5f, 3.0f, zs + 4f), new Vector3(0.38f, 0.75f, 0.02f), i == 1 ? Real(hFabric) : Real(hWhite), false, new Vector3(0f, 0f, (i - 1) * 4f));
            Box("V2AlleyCrate", V2(ax0 + 0.35f, 0.3f, zs + 7.2f), new Vector3(0.6f, 0.6f, 0.6f), Real(hLightWood), true, new Vector3(0f, 12f, 0f));
            var puddle = Mat("V2_Puddle", Mode.AlwaysReal, new Color(0.05f, 0.06f, 0.07f), new Color(0.05f, 0.06f, 0.07f), null, null, 0.98f, 0.98f, 0f);
            Box("V2Puddle", V2((ax0 + ax1) / 2f, 0.004f, zs + 3.2f), new Vector3(1.1f, 0.006f, 1.6f), puddle, false, new Vector3(0f, 18f, 0f));
            // 巷底牆上的門牌：16 和 17 之間一直閃（這裡應該什麼都沒有）。
            v2Num16 = LanternReveal.Number(16, new Color32(220, 214, 200, 255), new Color32(30, 30, 30, 255));
            v2Num17 = LanternReveal.Number(17, new Color32(235, 225, 200, 255), new Color32(90, 20, 20, 255));
            v2Plate = Mat("V2_Num17", Mode.Dual, Color.white, Color.white, v2Num16, v2Num16, 0.3f, 0.3f, 0f);
            v2Plate.SetColor("_EmissionColor", new Color(0.3f, 0.26f, 0.2f));
            v2Plate.SetColor("_AnomalyEmission", new Color(0.25f, 0.24f, 0.26f));
            Quad("V2DoorNumber", V2(5.85f, 2.2f, -0.16f), new Vector3(0.24f, 0.24f, 1f), Vector3.zero, v2Plate);

            // 可以互動的：18 號的對講機、公告欄、信箱。
            var intercom = Box("V2Intercom", V2(ax1 + 1.4f + 0.75f, 1.35f, zs - 0.07f), new Vector3(0.14f, 0.24f, 0.05f), Real(hTrim), true);
            AddInspect(intercom.gameObject, LanternInspectable.Kind.Pages, "18 號的對講機", null, null, new[]
            {
                "（暫定）沙沙聲，然後一個上了年紀的聲音：\n「找誰？」",
                "「17 號？……沒有 17 號啦。16 過來就是我們 18。我在這住了二十年了。」\n\n對講機喀一聲掛斷。",
            }, "");
            var board = Box("V2NoticeBoard", V2(ax0 - 1.2f, 1.5f, zs - 0.08f), new Vector3(1.2f, 0.9f, 0.05f), Real(hWood), true);
            AddInspect(board.gameObject, LanternInspectable.Kind.Pages, "社區公告欄", null, null, new[]
            {
                "（暫定）「本週日上午，巷口清潔，請勿堆放雜物。」\n\n旁邊一張尋物啟事：「遺失深色外套一件，袖口有手縫的字……」\n聯絡人那一欄，被雨水暈開了。",
            }, "");
            var mail = Box("V2Mailboxes", V2(ax1 + 0.55f, 1.2f, zs - 0.06f), new Vector3(0.5f, 0.35f, 0.08f), Real(hDark), true);
            AddInspect(mail.gameObject, LanternInspectable.Kind.Pages, "信箱", null, null, new[]
            {
                "（暫定）一排信箱：16、18。\n\n兩個之間的空位上，有被撬掉的螺絲孔，和一個看不清楚的「1」。",
            }, "");

            // 範圍：街道左右、對街、巷子（巷子兩側就是房子的牆）。
            var c = V2(3f, 0f, zs - 6f);
            Invisible(c + new Vector3(-16f, 4f, 0f), new Vector3(0.3f, 8f, 18f));
            Invisible(c + new Vector3(22f, 4f, 0f), new Vector3(0.3f, 8f, 18f));
            Invisible(V2(3f, 4f, zs - 9.5f), new Vector3(42f, 8f, 0.3f));
            Mark("V2Street", V2(5.1f, 0f, zs - 2.4f) + Vector3.up * 0.05f, V2(5.1f, 1.4f, 0f));
            Mark("V2Alley", V2(5.1f, 0f, -2.6f) + Vector3.up * 0.05f, V2(5.1f, 1.3f, 0f));
            if (V2Day) V2Chinatown(zs, zf, ax0, ax1);
        }

        /// <summary>美術方向 v1（2026-11-14）：第十七號門改成白天（下午）。夜晚的窗光、街燈關掉。</summary>
        public const bool V2Day = true;
        /// <summary>下午的太陽（只在新房子一帶開，舊房子仍是夜晚）。</summary>
        public Light V2Sun { get; private set; }

        /// <summary>
        /// 白天的華人街（參考倫敦 Gerrard Street、利物浦 Nelson Street、曼徹斯特唐人街）：
        /// 街上空一排排紅燈籠、中英雙語招牌、漆成紅金綠的店面與遮雨棚、街口的牌樓。不放石獅等帶宗教意味的擺設。
        /// </summary>
        private void V2Chinatown(float zs, float zf, float ax0, float ax1)
        {
            V2Sun = AddLight("V2Sun", LightType.Directional, V2(3f, 12f, zs), new Color(1f, 0.88f, 0.72f), 1.9f, 0f, false, LightShadows.Soft, 0f, new Vector3(0.72f, -0.5f, -0.48f)); // 低低的午後陽光，沿著街斜照：對街的店面被照亮，巷子在陰影裡
            V2Sun.shadowStrength = 0.85f;
            Material Flat(string n, Color c) { var m = Mat(n, Mode.AlwaysReal, c, c, null, null, 0.3f, 0.3f, 0f); return m; }
            var lanternRed = Flat("V2_LanternRed", new Color(0.86f, 0.12f, 0.08f));
            lanternRed.SetColor("_EmissionColor", new Color(0.35f, 0.04f, 0.02f));
            var gold = Flat("V2_Gold", new Color(0.86f, 0.64f, 0.22f));
            var signRed = Flat("V2_SignRed", new Color(0.72f, 0.1f, 0.08f));
            var signGreen = Flat("V2_SignGreen", new Color(0.1f, 0.42f, 0.3f));
            var signYellow = Flat("V2_SignYellow", new Color(0.93f, 0.76f, 0.25f));
            var wire = Flat("V2_Wire", new Color(0.1f, 0.1f, 0.1f));
            var tile = Flat("V2_GreenTile", new Color(0.18f, 0.5f, 0.38f));
            var pillar = Flat("V2_PillarRed", new Color(0.66f, 0.1f, 0.08f));
            var awnRed = Flat("V2_AwningRed", new Color(0.7f, 0.16f, 0.12f));
            var awnGreen = Flat("V2_AwningGreen", new Color(0.16f, 0.4f, 0.3f));
            var glass = Flat("V2_DayGlass", new Color(0.42f, 0.5f, 0.58f));

            // 白天的窗：反射天空的玻璃（蓋在原本夜晚發光的窗上）。
            foreach (var t in V2Root.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("V2Win") || t.name == "V2CafeWindow" || t.name == "V2ShopWindow" || t.name == "V2FlatWindow")
                {
                    var r = t.GetComponent<Renderer>(); if (r != null) r.sharedMaterial = glass;
                }

            // 一排排紅燈籠：從這一側的二樓拉到對街。
            float zNear = zs - 0.3f, zFar = zf + 0.3f, h = 5.4f;
            for (int row = 0; row < 6; row++)
            {
                float x = -8f + row * 4.2f;
                var a = V2(x, h, zNear); var b = V2(x + 0.6f, h, zFar);
                var mid = (a + b) / 2f; var d = b - a;
                Cyl("V2LanternWire", mid, new Vector3(0.012f, d.magnitude / 2f, 0.012f), wire, new Vector3(90f, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f));
                int n = 7;
                for (int i = 1; i < n; i++)
                {
                    float k = i / (float)n;
                    var p = Vector3.Lerp(a, b, k) + Vector3.down * (0.35f * Mathf.Sin(Mathf.PI * k) + 0.32f);
                    Ball("V2Lantern", p, new Vector3(0.36f, 0.42f, 0.36f), lanternRed);
                    Cyl("V2LanternCap", p + Vector3.up * 0.22f, new Vector3(0.16f, 0.025f, 0.16f), gold);
                    Cyl("V2LanternCap", p + Vector3.down * 0.22f, new Vector3(0.16f, 0.025f, 0.16f), gold);
                    Cyl("V2LanternTassel", p + Vector3.down * 0.36f, new Vector3(0.025f, 0.11f, 0.025f), lanternRed);
                }
            }

            // 對街的店面：漆成紅、綠、金的招牌與遮雨棚，中英雙語。
            var shops = new (float x, string zh, string en, Material board, Material awning, Color ink)[]
            {
                (-6.5f, "金華燒臘", "GOLDEN BBQ HOUSE", signRed, awnRed, new Color(0.98f, 0.82f, 0.35f)),
                (-1.5f, "茶餐廳", "TEA HOUSE CAFÉ", signGreen, awnGreen, new Color(0.98f, 0.9f, 0.6f)),
                (3.5f, "龍鳳餅家", "DRAGON PHOENIX BAKERY", signYellow, awnRed, new Color(0.55f, 0.08f, 0.06f)),
                (8.0f, "永記雜貨", "WING KEE GROCERY", signRed, awnGreen, new Color(0.98f, 0.82f, 0.35f)),
                (13.0f, "新華書局", "BOOKS & STATIONERY", signGreen, awnRed, new Color(0.98f, 0.9f, 0.6f)),
            };
            // 對街不再是一整片灰牆：每間店一段不同顏色的立面（一樓店面、二三樓灰泥或紅磚）、高低不同的屋簷、白色窗框。
            var facades = new[] { new Color(0.86f, 0.8f, 0.68f), new Color(0.62f, 0.32f, 0.24f), new Color(0.78f, 0.74f, 0.66f), new Color(0.55f, 0.36f, 0.3f), new Color(0.84f, 0.78f, 0.62f) };
            var fronts = new[] { new Color(0.62f, 0.1f, 0.08f), new Color(0.14f, 0.36f, 0.28f), new Color(0.82f, 0.62f, 0.2f), new Color(0.6f, 0.12f, 0.1f), new Color(0.16f, 0.32f, 0.36f) };
            var trim = Flat("V2_WhiteTrim", new Color(0.92f, 0.9f, 0.86f));
            for (int i = 0; i < shops.Length; i++)
            {
                float x = shops[i].x, w = 4.6f, top = 6.6f + (i % 3) * 0.7f;
                var up = Flat("V2_Facade" + i, facades[i]);
                var fr = Flat("V2_Shopfront" + i, fronts[i]);
                Box("V2OppFacade", V2(x, top / 2f, zf) + Vector3.back * 0.3f, new Vector3(w * V2Scale, top * 1.165f, 0.6f), up, false);
                Box("V2OppCornice", V2(x, top, zf) + Vector3.back * 0.2f, new Vector3(w * V2Scale + 0.2f, 0.25f, 0.8f), trim, false);
                Box("V2OppShopfront", V2(x, 1.25f, zf) + Vector3.forward * 0.02f, new Vector3(w * V2Scale - 0.4f, 2.5f, 0.06f), fr, false);
                Box("V2OppShopGlass", V2(x - 0.4f, 1.25f, zf) + Vector3.forward * 0.06f, new Vector3(w * V2Scale * 0.55f, 1.7f, 0.04f), glass, false);
                Box("V2OppDoor", V2(x + 1.45f, 1.05f, zf) + Vector3.forward * 0.06f, new Vector3(0.9f, 2.1f, 0.04f), Real(hDark), false);
                for (int f = 0; f < 2; f++)
                    for (int wi = -1; wi <= 1; wi += 2)
                    {
                        var wc = V2(x + wi * 1.1f, 4.2f + f * 1.9f, zf) + Vector3.forward * 0.02f;
                        Box("V2OppWin", wc, new Vector3(0.9f, 1.2f, 0.04f), glass, false);
                        Box("V2OppWinFrame", wc + Vector3.back * 0.01f, new Vector3(1.05f, 1.35f, 0.03f), trim, false);
                        Box("V2OppWinSill", wc + new Vector3(0f, -0.7f, 0.06f), new Vector3(1.1f, 0.06f, 0.14f), trim, false);
                    }
            }
            foreach (var t in V2Root.GetComponentsInChildren<Transform>(true))
                if (t.name == "V2Opposite" || t.name == "V2FlatWindow" || t.name == "V2CafeWindow" || t.name == "V2ShopWindow" || t.name == "V2CafeNeon") t.gameObject.SetActive(false);
            foreach (var s in shops)
            {
                var signAt = V2(s.x, 2.75f, zf) + Vector3.forward * 0.06f;
                var board = Box("V2ShopSign", signAt, new Vector3(3.4f, 0.75f, 0.08f), s.board, false);
                WallText(board, s.zh, Quaternion.Euler(0f, 180f, 0f), new Vector3(0f, 0.08f, 0.05f), 0.034f, s.ink);
                WallText(board, s.en, Quaternion.Euler(0f, 180f, 0f), new Vector3(0f, -0.24f, 0.05f), 0.011f, s.ink);
                Box("V2Awning", V2(s.x, 2.2f, zf) + new Vector3(0f, 0f, 0.55f), new Vector3(3.6f, 0.05f, 1.1f), s.awning, false, new Vector3(-18f, 0f, 0f));
            }
            // 這一側（16、18 號）樓下也掛一兩塊直式招牌。
            var v1 = Box("V2HangSign", V2(-3.2f, 3.4f, zs) + new Vector3(0f, 0f, -0.55f), new Vector3(0.08f, 1.4f, 0.55f), signRed, false);
            WallText(v1, "麻\n將\n館", Quaternion.Euler(0f, 90f, 0f), new Vector3(-0.05f, 0f, 0f), 0.022f, new Color(0.98f, 0.82f, 0.35f));
            WallText(v1, "麻\n將\n館", Quaternion.Euler(0f, -90f, 0f), new Vector3(0.05f, 0f, 0f), 0.022f, new Color(0.98f, 0.82f, 0.35f));
            Cyl("V2HangSignArm", V2(-3.2f, 4.15f, zs) + new Vector3(0f, 0f, -0.35f), new Vector3(0.02f, 0.35f, 0.02f), wire, new Vector3(90f, 0f, 0f));
            var v2s = Box("V2HangSign", V2(14.5f, 3.4f, zs) + new Vector3(0f, 0f, -0.55f), new Vector3(0.08f, 1.4f, 0.55f), signGreen, false);
            WallText(v2s, "洗\n衣\n店", Quaternion.Euler(0f, 90f, 0f), new Vector3(-0.05f, 0f, 0f), 0.022f, new Color(0.98f, 0.9f, 0.6f));
            WallText(v2s, "洗\n衣\n店", Quaternion.Euler(0f, -90f, 0f), new Vector3(0.05f, 0f, 0f), 0.022f, new Color(0.98f, 0.9f, 0.6f));
            Cyl("V2HangSignArm", V2(14.5f, 4.15f, zs) + new Vector3(0f, 0f, -0.35f), new Vector3(0.02f, 0.35f, 0.02f), wire, new Vector3(90f, 0f, 0f));

            // 街口的牌樓（紅柱、綠瓦、金字「華埠」）：站在門口往左看得到。
            var gx = V2(-12.5f, 0f, zs - 6.5f);
            float span = 9f;
            foreach (float side in new[] { -1f, 1f })
            {
                Cyl("V2GatePillar", gx + new Vector3(0f, 2.6f, side * span / 2f), new Vector3(0.45f, 2.6f, 0.45f), pillar);
                Box("V2GateBase", gx + new Vector3(0f, 0.3f, side * span / 2f), new Vector3(0.8f, 0.6f, 0.8f), Real(hWhite), true);
            }
            Box("V2GateBeam", gx + new Vector3(0f, 5.0f, 0f), new Vector3(0.5f, 0.5f, span + 0.6f), pillar, false);
            Box("V2GateRoof", gx + new Vector3(0f, 5.75f, 0f), new Vector3(1.6f, 0.35f, span + 2.2f), tile, false);
            Box("V2GateRoofTop", gx + new Vector3(0f, 6.05f, 0f), new Vector3(0.7f, 0.3f, span + 1.4f), tile, false);
            var plaque = Box("V2GatePlaque", gx + new Vector3(0.3f, 4.3f, 0f), new Vector3(0.08f, 0.8f, 2.4f), gold, false);
            WallText(plaque, "華 埠", Quaternion.Euler(0f, -90f, 0f), new Vector3(0.06f, 0f, 0f), 0.05f, new Color(0.55f, 0.08f, 0.06f));
            WallText(plaque, "華 埠", Quaternion.Euler(0f, 90f, 0f), new Vector3(-0.06f, 0f, 0f), 0.05f, new Color(0.55f, 0.08f, 0.06f));
        }

        private readonly Dictionary<Material, Material> realCopies = new Dictionary<Material, Material>();
        /// <summary>現實世界用的材質副本（AlwaysReal）。</summary>
        private Material Real(Material m)
        {
            Material c;
            if (m == null) return null;
            if (realCopies.TryGetValue(m, out c) && c != null) return c;
            c = new Material(m) { name = m.name + "_Real" };
            c.SetFloat("_Mode", (float)Mode.AlwaysReal);
            Materials.Add(c);
            realCopies[m] = c;
            return c;
        }

        private float v2Time;
        private Transform v2Silhouette;
        private Vector3 v2SilhouetteBase;
        // ---------------- 牆、地板、天花 ----------------

        private void V2Shell()
        {
            float H1 = V2Ground, H = V2Ground + V2Upper, D = 2.25f;
            var shopFloor = PH("V2_ShopFloor", "old_wooden_floor_02", "Tex", 2.0f, new Color(0.95f, 0.85f, 0.74f), new Color(0.4f, 0.45f, 0.5f), 0.7f);
            LanternReveal.AddRuin(shopFloor, "old_planks_02", 2.0f, new Color(0.5f, 0.52f, 0.55f), 0.4f);
            Slab("V2FloorShop", 0f, V2Width, 0f, 5f, 0f, 0.2f, shopFloor);
            Slab("V2FloorHall", 4.6f, V2Width, 5f, V2Main, 0f, 0.2f, hFloorHall);
            Slab("V2FloorDining", 0f, 4.6f, 5f, V2Main, 0f, 0.2f, hFloorLiving);
            Slab("V2FloorKitchen", 0f, V2ExtWidth, V2Main, V2Depth, 0f, 0.2f, hFloorKitchen);
            Slab("V2FloorYard", V2ExtWidth, V2Width, V2Main, V2Depth, 0f, 0.2f, hFloorScullery);

            // 門面＝巷底的牆：兩側被 16、18 號擋住，只露出巷子那一段。門框只在光裡出現（暗處就是一面普通的牆）。
            trims = false;
            HWallX(0f, 0f, V2Width, 0f, H, ExtT, v2Brick, D, new Vector2(4.6f, 5.6f));
            trims = true;
            // 左右是和鄰居共用的牆（沒有窗）。
            HWallZ(0f, 0f, V2Main, 0f, H, ExtT, hWall, D);
            HWallZ(0f, V2Main, V2Depth, 0f, H1, ExtT, hWallKitchen, D);
            HWallZ(V2Width, 0f, V2Main, 0f, H, ExtT, hWall, D);
            // 店面後牆：門通往走廊。
            HWallX(5f, 0f, V2Width, 0f, H1, IntT, hWall, D, new Vector2(4.7f, 5.45f));
            // 走廊西牆：門通往餐廳。
            HWallZ(4.6f, 5f, V2Main, 0f, H1, IntT, hWall, D, new Vector2(6.0f, 6.9f));
            // 主屋後牆：餐廳 → 廚房、走廊 → 側院的後門。
            HWallX(V2Main, 0f, V2Width, 0f, H, ExtT, hWall, D, new Vector2(1.2f, 2.1f), new Vector2(4.75f, 5.45f));
            // 加建（廚房）：東牆有窗與後門，後牆有窗。
            Win(11.2f, 12.6f, 1.0f, 2.1f, -1f);
            HWallZ(V2ExtWidth, V2Main, V2Depth, 0f, H1, ExtT, hWallKitchen, D, new Vector2(13.4f, 14.2f));
            Win(1.2f, 2.6f, 1.1f, 2.1f, -1f);
            HWallX(V2Depth, 0f, V2ExtWidth, 0f, H1, ExtT, hWallKitchen, D);
            // 側院的圍牆。
            trims = false;
            HWallZ(V2Width, V2Main, V2Depth, 0f, 2.0f, 0.2f, hWood, 2.0f);
            HWallX(V2Depth, V2ExtWidth, V2Width, 0f, 2.0f, 0.2f, hWood, 2.0f);
            trims = true;
            Invisible(V2(V2Width, 4f, (V2Main + V2Depth) / 2f), new Vector3(0.3f, 8f, (V2Depth - V2Main) * V2Scale));
            Invisible(V2((V2ExtWidth + V2Width) / 2f, 4f, V2Depth), new Vector3((V2Width - V2ExtWidth) * V2Scale, 8f, 0.3f));

            // 一樓天花板（樓梯井留空）、加建的屋頂。
            Slab("V2Ceiling", 0f, 5.5f, 0f, V2Main, H1, 0.25f, hCeil);
            Slab("V2Ceiling", 5.5f, V2Width, 0f, 5.6f, H1, 0.25f, hCeil);
            Slab("V2Ceiling", 5.5f, V2Width, 9.7f, V2Main, H1, 0.25f, hCeil);
            Slab("V2KitchenRoof", 0f, V2ExtWidth, V2Main, V2Depth, H1 + 0.05f, 0.12f, hCeil);

            // 開場的前門：暗處是一面普通的磚牆（只存在於異域），提燈照到才是門。
            // 和巷底的牆完全一樣的材質（同一套世界座標 UV），只是只存在於異域：暗處看不出這裡有門。
            var hiddenBrick = new Material(v2Brick) { name = "V2_HiddenBrick" };
            hiddenBrick.SetFloat("_Mode", (float)Mode.AnomalyOnly);
            Materials.Add(hiddenBrick);
            // 巷底的牆本身是現實世界的牆（提燈照了不變）；只有門洞那一塊是異域。
            v2Brick.SetFloat("_Mode", (float)Mode.AlwaysReal);
            var boarded = Box("V2FrontBoards", V2(5.1f, 1.12f, 0f), new Vector3(1.0f * V2Scale, 2.25f * 1.165f, ExtT + 0.04f), hiddenBrick, true);
            var bl = boarded.gameObject.AddComponent<LanternAnomalyBlocker>();
            bl.Solid = boarded.GetComponent<Collider>();
            bl.Probe = V2(5.1f, 1.0f, 0f);
            bl.ExtraProbes = new[] { V2(5.1f, 1.0f, 0.8f), V2(5.1f, 1.0f, 1.5f) };
            Blockers.Add(bl);
            V2FrontDoor = bl;
            // 真實的門框（只在光裡）。
            var frameMat = Mat("V2_RealFrame", Mode.RealOnly, new Color(0.92f, 0.88f, 0.8f), Color.black, null, null, 0.4f, 0.4f, 0f);
            Box("V2RealFrame", V2(4.6f, 1.2f, 0f) + new Vector3(-0.05f, 0f, -0.16f), new Vector3(0.1f, 2.7f, 0.08f), frameMat, false);
            Box("V2RealFrame", V2(5.6f, 1.2f, 0f) + new Vector3(0.05f, 0f, -0.16f), new Vector3(0.1f, 2.7f, 0.08f), frameMat, false);
            Box("V2RealFrame", V2(5.1f, 2.3f, 0f) + new Vector3(0f, 0.08f, -0.16f), new Vector3(1.5f, 0.12f, 0.08f), frameMat, false);
            // 真實的門：開著的深綠色木門（只在光裡）。
            var doorMat = Mat("V2_FrontDoor", Mode.RealOnly, new Color(0.12f, 0.25f, 0.2f), Color.black, null, null, 0.45f, 0.4f, 0f);
            var leafHinge = V2(4.6f, 0f, 0f) + new Vector3(0f, 0f, 0.05f);
            // 門板＋四片凸起的鑲板＋黃銅門把，看得出是一扇門；進門封起來之後門板就不見了。
            var leafRot = Quaternion.Euler(0f, -80f, 0f);
            V2FrontDoorLeaf = new GameObject("V2FrontDoorGroup").transform;
            V2FrontDoorLeaf.SetParent(Into, false);
            Box("V2FrontDoorLeaf", leafHinge + leafRot * new Vector3(0.65f, 1.3f, 0f), new Vector3(1.0f * V2Scale, 2.25f * 1.165f, 0.05f), doorMat, false, new Vector3(0f, -80f, 0f), V2FrontDoorLeaf);
            var panelMat = Mat("V2_FrontDoorPanel", Mode.RealOnly, new Color(0.1f, 0.22f, 0.17f), Color.black, null, null, 0.5f, 0.4f, 0f);
            var knobMat = Mat("V2_FrontDoorKnob", Mode.RealOnly, new Color(0.8f, 0.62f, 0.3f), Color.black, null, null, 0.8f, 0.4f, 0f);
            for (int i = 0; i < 4; i++)
            {
                float px = (i % 2 == 0 ? 0.33f : 0.97f), py = (i < 2 ? 0.65f : 1.85f), ph = (i < 2 ? 0.9f : 1.0f);
                foreach (float side in new[] { -1f, 1f })
                    Box("V2FrontDoorPanel", leafHinge + leafRot * new Vector3(px, py, side * 0.035f), new Vector3(0.48f, ph, 0.02f), panelMat, false, new Vector3(0f, -80f, 0f), V2FrontDoorLeaf);
            }
            foreach (float side in new[] { -1f, 1f })
                Ball("V2FrontDoorKnob", leafHinge + leafRot * new Vector3(1.18f, 1.05f, side * 0.06f), Vector3.one * 0.06f, knobMat, V2FrontDoorLeaf);
            // 封門：進去之後出現的牆（兩個狀態都在，擋住回頭路）。
            V2SealWall = Box("V2SealWall", V2(5.1f, 1.2f, 0f) + Vector3.forward * 0.02f, new Vector3(1.05f * V2Scale, 2.4f * 1.165f, ExtT + 0.06f), hWall, true); // 門不見了，只剩和店裡一樣的灰泥牆
            V2SealWall.gameObject.SetActive(false);

            Mark("V2Shop", V2(5.0f, 0f, 0.9f) + Vector3.up * 0.05f, V2(1.5f, 1.2f, 3.5f));
            Mark("V2Hall", V2(5.0f, 0f, 5.4f) + Vector3.up * 0.05f, V2(5.0f, 1.4f, 9.5f));
            Mark("V2Dining", V2(4.2f, 0f, 6.4f) + Vector3.up * 0.05f, V2(1.0f, 1.0f, 8.0f));
            Mark("V2Kitchen", V2(1.6f, 0f, 10.5f) + Vector3.up * 0.05f, V2(2.0f, 1.0f, 14.0f));
            Mark("V2Yard", V2(5.2f, 0f, 10.6f) + Vector3.up * 0.05f, V2(4.8f, 1.2f, 14.5f));
        }

        // ---------------- 樓梯（二樓暫時封住） ----------------

        private void V2StairsAndUpper()
        {
            float H1 = V2Ground;
            float z0 = 5.7f, z1 = 9.6f, x0 = 5.55f, x1 = V2Width - ExtT / 2f / V2Scale;
            var a = V2((x0 + x1) / 2f, 0f, z0);
            var b = V2((x0 + x1) / 2f, H1, z1);
            var dir = b - a;
            var ramp = Box("V2StairRamp", (a + b) / 2f + Vector3.down * 0.04f, new Vector3((x1 - x0) * V2Scale, 0.08f, dir.magnitude), hStair, true, Quaternion.LookRotation(dir.normalized).eulerAngles);
            ramp.GetComponent<Renderer>().enabled = false;
            int steps = 17;
            for (int i = 0; i < steps; i++)
            {
                var p = Vector3.Lerp(a, b, (i + 0.5f) / steps);
                float rise = (b.y - a.y) / steps, run = (b.z - a.z) / steps;
                Box("V2Step", new Vector3(p.x, a.y + rise * (i + 0.5f), p.z), new Vector3((x1 - x0) * V2Scale, rise, run), hStair, false);
            }
            // 樓梯旁的欄杆牆（走廊從旁邊過）。
            var mid = (a + b) / 2f;
            Box("V2StairWall", new Vector3(V2(x0, 0f, 0f).x, mid.y - 0.2f, mid.z), new Vector3(0.08f, 1.0f, dir.magnitude), hRail, true, Quaternion.LookRotation(dir.normalized).eulerAngles);
            Box("V2Handrail", new Vector3(V2(x0, 0f, 0f).x, mid.y + 0.45f, mid.z), new Vector3(0.06f, 0.06f, dir.magnitude), hRail, false, Quaternion.LookRotation(dir.normalized).eulerAngles);
            // 樓梯下的電話桌與答錄機（之後的可選線索）。
            Box("V2PhoneShelf", V2(4.6f, 1.0f, 8.0f) + Vector3.right * 0.2f, new Vector3(0.3f, 0.04f, 0.5f), hWood, false);
            Box("V2Phone", V2(4.6f, 1.0f, 8.0f) + new Vector3(0.2f, 0.07f, 0f), new Vector3(0.2f, 0.09f, 0.22f), hDark, true);
            // 二樓平台（暫時封住：v2 下一步再蓋二樓）。
            Slab("V2Landing", 4.6f, V2Width, 9.6f, V2Main, H1 + 0.02f, 0.1f, hFloorUp);
            // 平台往二樓的方向（西側）暫時封住。
            Box("V2UpperClosed", V2(5.45f, H1 + 1.3f, 9.8f), new Vector3(0.15f, 2.6f, 0.5f * V2Scale + 0.1f), hWall, true);
            Slab("V2UpperFloor", 0f, 5.5f, 0f, V2Main, H1 + 0.02f, 0.06f, hFloorUp);
            Slab("V2Roof", 0f, V2Width, 0f, V2Main, H1 + V2Upper + 0.1f, 0.15f, hCeil);
            // 走廊的門：店面 → 走廊、走廊 → 餐廳、餐廳 → 廚房（真的門，E 開關）。
            V2Door("店面通往走廊的門", true, 5f, 4.7f, 5.45f, 1f);
            var dining = V2Door("餐廳的門", false, 4.6f, 6.0f, 6.9f, -1f);
            // 推門（拉鋸）原型：餐廳的門像有人在另一邊抵著；門縫漏出一點會呼吸的暖光。
            var gap = AddLight("V2DiningGapLight", LightType.Point, V2(4.2f, 1.0f, 6.45f), new Color(1f, 0.75f, 0.45f), 0.3f, 3.5f, false, LightShadows.None);
            var st = dining.gameObject.AddComponent<LanternStruggle>();
            st.Label = "餐廳的門";
            st.Door = dining;
            st.GapLight = gap;
            Struggles.Add(st);
            V2DiningStruggle = st;
            V2Door("廚房的門", true, V2Main, 1.2f, 2.1f, 1f);
            V2Door("後門", true, V2Main, 4.75f, 5.45f, 1f);
        }

        /// <summary>真的門：鉸鍊在一側，E 打開／關上（轉 100°）。alongX＝門洞在沿 x 的牆上。</summary>
        private LanternToggle V2Door(string label, bool alongX, float fixedC, float a0, float a1, float swing)
        {
            float w = (a1 - a0) * V2Scale - 0.04f, h = 2.25f * 1.165f - 0.03f;
            var hingePos = alongX ? V2(a0, 0f, fixedC) : V2(fixedC, 0f, a0);
            var hinge = new GameObject("DoorHinge").transform;
            hinge.SetParent(Into, false);
            hinge.position = hingePos;
            var along = alongX ? Vector3.right : Vector3.forward;
            var leaf = Box("Door", hingePos + along * (w / 2f + 0.02f) + Vector3.up * h / 2f, alongX ? new Vector3(w, h, 0.05f) : new Vector3(0.05f, h, w), hWood, true, null, hinge);
            Box("DoorKnob", hingePos + along * (w - 0.08f) + Vector3.up * 1.0f + (alongX ? Vector3.back : Vector3.left) * 0.05f, Vector3.one * 0.06f, hTrim, false, null, hinge);
            var t = AddToggle(leaf.gameObject, label, LanternToggle.Kind.Rotate, hinge, Vector3.zero, Vector3.zero, Quaternion.identity, Quaternion.Euler(0f, 100f * swing, 0f), null, "打開", "關上");
            V2Doors.Add(t);
            return t;
        }

        // ---------------- 家具（灰盒，之後換模型） ----------------

        private void V2Furnish()
        {
            float f = FurnitureScale;
            V2Shop();
            // 餐廳：餐桌、四張椅子、碗櫃、日曆。
            Furnish("V2DiningTable", "dining_table", 2.2f, 7.5f, 0f, 0.76f, 90f, hWood, new Vector3(1.5f, 0.75f, 0.9f));
            foreach (var p in new[] { new Vector3(1.6f, 6.85f, 0f), new Vector3(2.8f, 6.85f, 0f), new Vector3(1.6f, 8.15f, 180f), new Vector3(2.8f, 8.15f, 180f) })
                Furnish("V2DiningChair", "dining_chair_02", p.x, p.y, 0f, 0.95f, p.z, hWood, new Vector3(0.44f, 0.9f, 0.44f), false);
            Furnish("V2BowlCabinet", "vintage_cabinet_01", 0.3f, 7.5f, 0f, 1.0f, 90f, hWood, new Vector3(0.45f, 0.9f, 1.2f));
            // 廚房：流理台、爐子、冰箱、高櫃。
            Fur("V2Counter2", 0.35f, 12.4f, 0f, 0.6f, 0.9f, 3.4f, hLightWood);
            Fur("V2CounterTop2", 0.35f, 12.4f, 0.9f, 0.64f, 0.04f, 3.44f, hWhite, false);
            Fur("V2Stove", 0.35f, 10.7f, 0f, 0.6f, 0.9f, 0.6f, hDark);
            Fur("V2Fridge", 3.4f, 10.6f, 0f, 0.7f, 1.8f, 0.7f, hWhite);
            Fur("V2TallCabinet", 2.6f, 14.6f, 0f, 0.6f, 2.0f, 0.5f, hWood);
            // 側院：晾衣繩、花盆、幾個箱子。
            Cyl("V2ClothesLine", V2(5.25f, 1.9f, 12.5f), new Vector3(0.01f, 2.0f * V2Scale, 0.01f), hDark, new Vector3(90f, 0f, 0f));
            Prop("V2YardPlant", "potted_plant_02", 6.1f, 14.4f, 0f, 0.5f, 0f, false, true);
            Prop("V2YardBox", "cardboard_box_01", 4.4f, 14.5f, 0f, 0.4f, 20f, false, true);
            // Y1 洗衣籃與紅線：晾衣繩下的洗衣籃，裡面有一捲紅線（縫紉機要用）。
            Model("V2LaundryBasket", "wicker_basket_01", 5.6f, 13.6f, 0f, 0.42f, 15f, true, hLightWood, Vector3.one * 0.4f, Mode.Dual);
            var spoolMat = Mat("V2_RedThread", Mode.Dual, new Color(0.75f, 0.08f, 0.07f), new Color(0.45f, 0.18f, 0.18f), null, null, 0.3f, 0.3f, 0.004f);
            spoolMat.SetColor("_EmissionColor", new Color(0.25f, 0.02f, 0.02f));
            var spool = Cyl("V2ThreadSpool", HP(5.6f, 0f, 13.6f) + Vector3.up * 0.5f, new Vector3(0.05f, 0.04f, 0.05f), spoolMat);
            spool.gameObject.AddComponent<BoxCollider>().size = new Vector3(2f, 2f, 2f);
            V2ThreadSpool = spool;
            // 晾衣繩上的衣服：兩個人的，一大一小。
            for (int i = 0; i < 4; i++) Box("V2YardLaundry", V2(4.6f + i * 0.45f, 1.55f, 12.5f), new Vector3(0.36f, i % 2 == 0 ? 0.7f : 0.5f, 0.02f), i % 2 == 0 ? hWhite : hFabric, false);
        }

        private void V2Lights()
        {
            void Lamp(string name, float x, float z, float range)
            {
                var at = V2(x, V2Ground - 0.45f, z);
                Ball(name + "Bulb", at + Vector3.up * 0.12f, new Vector3(0.12f, 0.14f, 0.12f), hBulb);
                Cyl(name + "Shade", at + Vector3.up * 0.2f, new Vector3(0.28f, 0.06f, 0.28f), hTrim);
                // 異域裡的燈：冷色、會閃，但亮度足以看清路（光的分工：場景自己有光，提燈負責照出不存在的東西）。
                AddLight(name, LightType.Point, at, new Color(0.72f, 0.82f, 1f), 1.6f, range * V2Scale, true, LightShadows.Soft);
            }
            // 店面櫃台上一盞會閃的暖色小燈泡：進門第一眼會走過去的地方（地標）。
            var counterBulb = Mat("V2_CounterBulb", Mode.Dual, new Color(1f, 0.85f, 0.6f), new Color(1f, 0.85f, 0.6f), null, null, 0.8f, 0.8f, 0f);
            counterBulb.SetColor("_EmissionColor", new Color(3f, 2f, 1.1f));
            counterBulb.SetColor("_AnomalyEmission", new Color(3f, 2f, 1.1f));
            var cb = V2(2.0f, 0f, 4.1f) + Vector3.up * (0.99f * FurnitureScale + 0.18f);
            Cyl("V2CounterLampBase", cb + Vector3.down * 0.14f, new Vector3(0.12f, 0.02f, 0.12f), hDark);
            Cyl("V2CounterLampStem", cb + Vector3.down * 0.07f, new Vector3(0.02f, 0.06f, 0.02f), hDark);
            Ball("V2CounterBulb", cb, new Vector3(0.09f, 0.11f, 0.09f), counterBulb);
            AddLight("V2CounterLight", LightType.Point, cb + Vector3.up * 0.05f, new Color(1f, 0.72f, 0.42f), 1.8f, 5.5f, true, LightShadows.Soft);
            // 走廊盡頭的後門上方，一盞戶外燈從門縫照進來；廚房窗外的月光。
            AddLight("V2BackDoorGlow", LightType.Point, V2(5.1f, 2.4f, V2Main + 0.6f), new Color(0.6f, 0.72f, 1f), 1.2f, 5f, false, LightShadows.Soft);
            AddLight("V2KitchenMoon", LightType.Spot, V2(V2ExtWidth + 1.4f, 3.2f, 11.9f), new Color(0.55f, 0.68f, 1f), 3.5f, 9f, false, LightShadows.Soft, 60f, new Vector3(-1f, -0.6f, 0f));
            Lamp("V2ShopLight", 2.6f, 2.5f, 5.5f);
            V2ShopLamps.Add(AnomalyLights[AnomalyLights.Count - 1]);
            Lamp("V2HallLight", 5.0f, 7.5f, 4f);
            Lamp("V2DiningLight", 2.2f, 7.5f, 4.5f);
            Lamp("V2KitchenLight", 2.0f, 12.5f, 4.5f);
        }

        // ---------------- 店面（改衣店）：照真實的店擺，動線先行 ----------------

        public Transform V2CoatTag { get; private set; }
        public Transform V2SewingMachine { get; private set; }
        public LanternRestorable V2PowerLever { get; private set; }
        public Transform V2ThreadSpool { get; private set; }
        public LanternInspectable V2SecretPhoto { get; private set; }
        public readonly List<Light> V2ShopLamps = new List<Light>();
        /// <summary>牆上影子殘影的位置（店面：試衣區旁的白牆，朝向店內）。</summary>
        public Vector3 V2ShopEchoAt { get; private set; }
        public Quaternion V2ShopEchoRot { get; private set; }
        public Bounds V2ShopRoom { get; private set; }

        /// <summary>
        /// 動線：前門（x≈5.1）直通後面走廊的門（x≈5.1），x 4.3～5.9 整條留空。
        /// 左後：櫃台（面向門口），櫃台後是電箱與鈕扣抽屜櫃；左牆：衣架、縫紉機；中間偏左：穿著外套的人台（進門第一眼）；
        /// 右牆：布料架、試衣鏡、試衣間；門邊：傘架、腳踏墊；牆上：小神桌、營業時間、月曆。
        /// </summary>
        private void V2Shop()
        {
            V2ShopLamps.Clear();
            // 動線：門口（右前）→ 中間整片空地 → 櫃台（後）；左前窗下是工作角（縫紉機＋燙衣板），左牆衣架與那件外套，右牆布料架、鏡子、客人的椅子。
            CustomModel("V2Counter", "v2_shop_counter", HP(2.4f, 0f, 4.1f), 180f);
            CustomModel("V2ButtonCabinet", "v2_button_cabinet", HP(0.6f, 0f, 4.7f), 180f);
            CustomModel("V2ClothesRack", "v2_clothes_rack", HP(0.42f, 0f, 2.2f), 90f);
            V2SewingMachine = CustomModel("V2SewingMachine", "v2_sewing_machine", HP(1.35f, 0f, 0.45f), 0f);
            CustomModel("V2IroningBoard", "v2_ironing_board", HP(3.0f, 0f, 0.55f), 0f, false);
            // 那件外套：不穿在人台上，掛在左牆的木掛鉤上（客人的衣服都在衣架上，只有它單獨掛著）。
            var coat = CustomModel("V2HangingCoat", "v2_hanging_coat", HP(0f, 0f, 3.55f) + new Vector3(0.36f, 0f, 0f), 90f);
            CustomModel("V2FabricShelf", "v2_fabric_shelf", HP(6.18f, 0f, 4.2f), -90f);
            CustomModel("V2Mirror", "v2_cheval_mirror", HP(6.1f, 0f, 3.05f), -110f);
            CustomModel("V2Umbrellas", "v2_umbrella_stand", HP(4.3f, 0f, 0.3f), 0f);
            float top = 0.99f * FurnitureScale;
            CustomModel("V2Register", "v2_cash_register", HP(3.15f, 0f, 4.15f) + Vector3.up * top, 180f, false);
            V2ShopWindow();
            // 樣板間：小東西放在人會放的地方（Blender 做的，平塗顏色；異域時自動變成褪色的樣子）。
            float wallIn = 0.13f;
            CustomModel("V2ThreadRack", "v2_thread_rack", HP(3.0f, 0f, 0f) + new Vector3(0f, 0f, wallIn), 0f, false);
            CustomModel("V2CounterClutter", "v2_counter_clutter", HP(2.3f, 0f, 4.1f) + Vector3.up * top, 180f, false);
            CustomModel("V2FabricBin", "v2_fabric_bin", HP(0.38f, 0f, 0.42f), 90f);
            CustomModel("V2WallShelf", "v2_wall_shelf", HP(4.15f, 0f, 5f) + new Vector3(0f, 0f, -0.07f), 180f, false);
            CustomModel("V2WallClock", "v2_wall_clock", HP(V2Width, 0f, 3.9f) + new Vector3(-wallIn, 0f, 0f), -90f, false);
            CustomModel("V2Boxes", "v2_boxes", HP(3.35f, 0f, 4.72f), 172f);
            CustomModel("V2Stool", "v2_stool", HP(1.35f, 0f, 1.05f), 20f);
            if (V2SewingMachine != null)
            {
                float tableTop = 0.74f * FurnitureScale;
                CustomModel("V2SewingClutter", "v2_sewing_clutter", HP(1.35f, 0f, 0.45f) + new Vector3(0.12f, tableTop, 0.0f), 0f, false);
            }
            // 門口的腳踏墊、客人坐的椅子。
            Box("V2DoorMat", HP(5.1f, 0f, 0.55f) + Vector3.up * 0.006f, new Vector3(1.2f, 0.012f, 0.8f), hRug, false);
            Furnish("V2CustomerChair", "dining_chair_02", 6.1f, 0.7f, 0f, 0.95f, -90f, hWood, new Vector3(0.44f, 0.9f, 0.44f), false);
            // 牆上：營業時間的牌子、月曆、裱框的老照片。
            var paper = Mat("V2_ShopPaper", Mode.Dual, new Color(0.92f, 0.88f, 0.78f), new Color(0.6f, 0.62f, 0.64f), null, null, 0.2f, 0.2f, 0.004f);
            var hours = Box("V2HoursSign", HP(4.4f, 0f, 0.0f) + new Vector3(0f, 1.7f, 0.15f), new Vector3(0.32f, 0.42f, 0.015f), paper, false);
            WallText(hours, "營業時間\n\n09:00 – 19:00\n週一公休\n\n改衣・修補・訂做", Quaternion.Euler(0f, 180f, 0f), new Vector3(0f, 0f, 0.012f), 0.0055f, new Color(0.18f, 0.12f, 0.08f));
            var cal = Box("V2ShopCalendar", HP(6.5f, 0f, 0.9f) + new Vector3(-0.15f, 1.65f, 0f), new Vector3(0.015f, 0.5f, 0.36f), paper, false);
            WallText(cal, "十 月\n\n日 一 二 三 四 五 六\n       1  2  3  4\n 5  6  7  8  9 10 11\n12 13 14 15 16 17 18\n19 20 21 22 23 24 25\n26 27 28 29 30 31", Quaternion.Euler(0f, 90f, 0f), new Vector3(-0.012f, 0f, 0f), 0.0042f, new Color(0.2f, 0.12f, 0.1f));
            WallProp("V2ShopPhoto", "fancy_picture_frame_01", HP(6.5f, 0f, 2.6f) + new Vector3(-0.16f, 1.95f, 0f), 0.4f, -90f, Mode.Dual);

            // 謎題物件 ------------------------------------------------------------
            // S1 電箱：櫃台後的牆上（暗處只看得到小紅燈）。
            var pbAt = HP(1.85f, 0f, 5f) + new Vector3(0f, 1.45f, -(IntT / 2f + 0.09f));
            WallProp("V2PowerBox", "power_box_01", pbAt, 0.5f, 180f, Mode.Dual);
            var led = Mat("V2_PowerLed", Mode.Dual, new Color(0.6f, 0.1f, 0.08f), new Color(0.6f, 0.1f, 0.08f), null, null, 0.5f, 0.5f, 0f);
            led.SetColor("_EmissionColor", new Color(2.2f, 0.25f, 0.15f));
            led.SetColor("_AnomalyEmission", new Color(2.2f, 0.25f, 0.15f));
            Ball("V2PowerLed", pbAt + new Vector3(0.12f, 0.3f, -0.08f), Vector3.one * 0.025f, led);
            var lever = Box("V2PowerLever", pbAt + new Vector3(0f, 0f, -0.12f), new Vector3(0.05f, 0.22f, 0.05f), hWhite, true);
            var lr = lever.gameObject.AddComponent<LanternRestorable>();
            lr.Label = "電箱的拉桿（恢復供電）";
            lr.Seconds = 1.1f;
            lr.AnchorRadius = 0f; // 電箱修好的效果是燈穩下來，不在上面留一圈穩定區（試玩回饋：不自然的光圈）
            lr.Init(lever.position + Vector3.down * 0.08f, Quaternion.Euler(70f, 0f, 0f), lever.position, Quaternion.identity);
            Restorables.Add(lr);
            V2PowerLever = lr;
            // S2 外套吊牌：袖口那張紙吊牌（聚光才看得清楚）。
            var tag = new GameObject("V2CoatTag").transform;
            tag.SetParent(Into, false);
            Renderer tagR = null;
            if (coat != null) foreach (var r in coat.GetComponentsInChildren<Renderer>()) if (r.name.Contains("CoatTag")) tagR = r;
            tag.position = tagR != null ? tagR.bounds.center : HP(0f, 0f, 3.55f) + new Vector3(0.3f, 0.95f, 0.25f);
            var tc = tag.gameObject.AddComponent<BoxCollider>();
            tc.size = new Vector3(0.18f, 0.22f, 0.18f);
            V2CoatTag = tag;
            // 外套被一道冷光照著（告訴你「看這裡」）。
            var coatAt = HP(0f, 0f, 3.55f) + new Vector3(0.25f, 1.1f, 0f);
            var spotFrom = coatAt + new Vector3(1.3f, 1.8f, -0.4f);
            var spot = AddLight("V2CoatSpot", LightType.Spot, spotFrom, new Color(0.75f, 0.85f, 1f), 3.2f, 5f, false, LightShadows.Soft, 30f, coatAt - spotFrom);
            spot.innerSpotAngle = 16f;
            // 秘密：衣架後面牆上的舊照片（只在光裡）。
            var secretMat = Mat("V2_SecretPhoto", Mode.RealOnly, Color.white, Color.black, LanternReveal.Photo(true), null, 0.3f, 0.3f, 0f);
            var sp = Box("V2SecretPhoto", HP(0f, 0f, 2.6f) + new Vector3(0.14f, 1.25f, 0f), new Vector3(0.012f, 0.16f, 0.12f), secretMat, true, new Vector3(0f, 0f, 4f));
            V2SecretPhoto = AddInspect(sp.gameObject, LanternInspectable.Kind.Pages, "衣架後面的舊照片", null, null, new[]
            {
                "（暫定）泛黃的照片：一個小女孩站在人台旁邊，踮著腳比身高。\n\n背面用鉛筆寫著：七歲，一百二十二。",
            }, "");
            V2SecretPhoto.Secret = true;
            // 影子殘影的牆：右牆、鏡子旁邊那段空牆（媽媽在鏡子前幫她量身）。
            V2ShopEchoAt = HP(6.5f, 0f, 1.85f) + new Vector3(-0.14f, 1.25f, 0f);
            V2ShopEchoRot = Quaternion.Euler(0f, 90f, 0f);
            var shop = new Bounds();
            shop.SetMinMax(HP(0f, 0f, 0f) + new Vector3(0f, -0.3f, 0f), HP(V2Width, 0f, 5f) + new Vector3(0f, V2Ground * 1.165f - 0.1f, 0f));
            V2ShopRoom = shop;
        }

        /// <summary>貼在紙板上的字（灰盒：TextMesh；之後換成正式貼圖）。rot＝字面朝向（字的正面朝 -Z）。</summary>
        private void WallText(Transform board, string s, Quaternion rot, Vector3 offset, float size, Color c)
        {
            var go = new GameObject(board.name + "Text");
            go.transform.SetParent(Into, false);
            go.transform.SetPositionAndRotation(board.position + offset, rot);
            var tm = go.AddComponent<TextMesh>();
            tm.font = LanternFonts.UI;
            tm.fontSize = 64;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.text = s;
            var m = LanternFonts.WorldMaterial(tm.font, c, "V2_WallText");
            m.color = c;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
        }

        /// <summary>
        /// 店面的前窗（左前、縫紉機上方）：毛玻璃透進巷子路燈的冷光，下半截有一條短的蕾絲窗簾。
        /// 房子門面藏在巷底，外面看不到這扇窗，所以只做室內這一面。
        /// </summary>
        private void V2ShopWindow()
        {
            float z = 0.14f;                       // 前牆室內那一面
            // 前牆在外面是巷底的磚牆；室內貼一層和其他牆一樣的灰泥（不然整面是暗紅色的磚）。
            float wallH = V2Ground * 1.165f;
            Box("V2ShopFrontPlaster", (HP(0f, 0f, 0f) + HP(4.6f, 0f, 0f)) / 2f + new Vector3(0f, wallH / 2f, 0.13f), new Vector3(4.6f * V2Scale, wallH, 0.01f), hWall, false);
            Box("V2ShopFrontPlaster", (HP(5.6f, 0f, 0f) + HP(V2Width, 0f, 0f)) / 2f + new Vector3(0f, wallH / 2f, 0.13f), new Vector3((V2Width - 5.6f) * V2Scale, wallH, 0.01f), hWall, false);
            Box("V2ShopFrontPlaster", HP(5.1f, 0f, 0f) + new Vector3(0f, (2.25f * 1.165f + 0.1f + wallH) / 2f, 0.13f), new Vector3(1.0f * V2Scale + 0.02f, wallH - 2.25f * 1.165f - 0.1f, 0.01f), hWall, false);
            var c = HP(1.35f, 0f, 0f);
            float w = 1.5f, h = 1.25f, bottom = 0.95f, ft = 0.06f;
            float cy = bottom + h / 2f;
            var glass = Mat("V2_ShopWindowGlass", Mode.Dual, new Color(0.55f, 0.62f, 0.7f), new Color(0.18f, 0.2f, 0.22f), null, null, 0.85f, 0.4f, 0f);
            glass.SetColor("_EmissionColor", V2Day ? new Color(1.25f, 1.18f, 1.05f) : new Color(0.55f, 0.68f, 0.85f)); // 白天：午後的陽光透過毛玻璃
            glass.SetColor("_AnomalyEmission", new Color(0.05f, 0.07f, 0.09f));
            var glassBox = Box("V2ShopWindowGlass", c + new Vector3(0f, cy, z + 0.005f), new Vector3(w, h, 0.01f), glass, false);
            glassBox.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // 窗外走過的人影（毛玻璃上模糊的影子），只在窗戶範圍內。
            if (V2Day) LanternPasserby.Create(Into, c + new Vector3(0f, cy, z + 0.012f), Quaternion.Euler(0f, 180f, 0f), new Vector2(w, h));
            Box("V2ShopWindowFrameT", c + new Vector3(0f, bottom + h + ft / 2f, z + 0.03f), new Vector3(w + 2f * ft, ft, 0.06f), hTrim, false);
            Box("V2ShopWindowFrameB", c + new Vector3(0f, bottom - ft / 2f, z + 0.03f), new Vector3(w + 2f * ft, ft, 0.06f), hTrim, false);
            Box("V2ShopWindowFrameL", c + new Vector3(-(w + ft) / 2f, cy, z + 0.03f), new Vector3(ft, h, 0.06f), hTrim, false);
            Box("V2ShopWindowFrameR", c + new Vector3((w + ft) / 2f, cy, z + 0.03f), new Vector3(ft, h, 0.06f), hTrim, false);
            Box("V2ShopWindowMullion", c + new Vector3(0f, cy, z + 0.025f), new Vector3(0.035f, h, 0.04f), hTrim, false);
            Box("V2ShopWindowTransom", c + new Vector3(0f, bottom + h * 0.68f, z + 0.025f), new Vector3(w, 0.035f, 0.04f), hTrim, false);
            Box("V2ShopWindowSill", c + new Vector3(0f, bottom - ft - 0.02f, z + 0.09f), new Vector3(w + 0.25f, 0.04f, 0.2f), hTrim, false);
            // 下半截的短窗簾（蕾絲）＋細桿。
            var lace = Mat("V2_ShopLace", Mode.Dual, new Color(0.93f, 0.9f, 0.82f), new Color(0.42f, 0.42f, 0.4f), null, null, 0.2f, 0.3f, 0.006f);
            Box("V2ShopWindowLace", c + new Vector3(0f, bottom + 0.3f, z + 0.07f), new Vector3(w - 0.04f, 0.58f, 0.012f), lace, false);
            Cyl("V2ShopWindowLaceRod", c + new Vector3(0f, bottom + 0.6f, z + 0.075f), new Vector3(0.012f, w / 2f, 0.012f), hTrim, new Vector3(0f, 0f, 90f));
            // 窗外路燈透進來的冷光（照在縫紉機和地板上）。
            var from = c + new Vector3(0f, bottom + h, z - 0.3f);
            AddLight("V2ShopWindowLight", LightType.Spot, from, V2Day ? new Color(1f, 0.86f, 0.66f) : new Color(0.62f, 0.72f, 0.9f), V2Day ? 5f : 1.6f, 7f, false, LightShadows.Soft, 75f, new Vector3(0.1f, -0.8f, 1f));
            // 窗光的光束（幾片交叉的柔邊面，加亮）＋光裡飄的灰塵。
            var shaftDir = new Vector3(0.1f, -0.8f, 1f).normalized;
            var shaftAt = c + new Vector3(0f, cy, z + 0.02f) + shaftDir * 1.1f;
            var shaftMat = ShaftMaterial();
            for (int i = 0; i < 3; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "V2ShopWindowShaft";
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(Into, false);
                q.transform.position = shaftAt;
                q.transform.rotation = Quaternion.LookRotation(shaftDir, Vector3.up) * Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, i * 60f, 0f);
                q.transform.localScale = new Vector3(w * 0.95f, 2.3f, 1f);
                var r = q.GetComponent<Renderer>();
                r.sharedMaterial = shaftMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            Dust("V2ShopWindowDust", shaftAt, new Vector3(w, 1.6f, 1.6f));
        }

        private Material shaftMat;

        /// <summary>光束材質：加亮、上濃下淡、兩側柔邊（程式產生的漸層）。</summary>
        private Material ShaftMaterial()
        {
            if (shaftMat != null) return shaftMat;
            const int W = 32, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "ShaftGradient", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                float u = x / (W - 1f), v = y / (H - 1f);
                float a = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 3f) * Mathf.Pow(1f - v, 1.4f) * Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, v * 6f)); // v＝0 是窗邊（最亮），往房間裡淡掉
                px[y * W + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(px); tex.Apply();
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            shaftMat = new Material(sh != null ? sh : Shader.Find("Sprites/Default")) { name = "V2_WindowShaft", hideFlags = HideFlags.DontSave };
            shaftMat.SetFloat("_Surface", 1f);
            shaftMat.SetFloat("_Blend", 2f); // Additive
            shaftMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            shaftMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            shaftMat.SetFloat("_ZWrite", 0f);
            shaftMat.SetFloat("_Cull", 0f);
            shaftMat.SetOverrideTag("RenderType", "Transparent");
            shaftMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            shaftMat.EnableKeyword("_BLENDMODE_ADD");
            shaftMat.renderQueue = 3100;
            shaftMat.SetTexture("_BaseMap", tex);
            shaftMat.SetTexture("_MainTex", tex);
            var col = V2Day ? new Color(1f, 0.85f, 0.6f, 0.08f) : new Color(0.55f, 0.65f, 0.85f, 0.07f);
            shaftMat.SetColor("_BaseColor", col);
            shaftMat.SetColor("_Color", col);
            return shaftMat;
        }

        /// <summary>光裡慢慢飄的灰塵（小顆、很淡、會在範圍裡繞）。</summary>
        private void Dust(string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Into, false);
            go.transform.position = center;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.duration = 10f;
            main.startLifetime = 9f;
            main.startSpeed = 0.01f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.014f);
            main.startColor = new Color(0.85f, 0.9f, 1f, 0.55f);
            main.maxParticles = 140;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var em = ps.emission; em.rateOverTime = 14f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = size;
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.05f; noise.frequency = 0.25f;
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = new Material(sh != null ? sh : Shader.Find("Sprites/Default")) { name = "V2_Dust", hideFlags = HideFlags.DontSave };
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3100;
            pr.sharedMaterial = m;
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
        }

        // ---------------- Claude 在 Blender 建的模型 ----------------

        /// <summary>Blender 材質名稱 → 我們的雙狀態材質（真實的顏色或 PH 貼圖、金屬度、光滑度）。</summary>
        private static readonly Dictionary<string, (string tex, Color color, float smooth, float metal)> CustomMats = new Dictionary<string, (string, Color, float, float)>
        {
            { "SC_Wood", ("dark_wood", new Color(0.5f, 0.32f, 0.24f), 0.65f, 0f) },
            { "SC_WoodTop", ("dark_wood", new Color(0.72f, 0.5f, 0.36f), 0.75f, 0f) },
            { "SC_Brass", (null, new Color(0.85f, 0.62f, 0.28f), 0.75f, 1f) },
            { "SC_Felt", (null, new Color(0.12f, 0.28f, 0.2f), 0.1f, 0f) },
            { "SC_Paper", (null, new Color(0.85f, 0.8f, 0.68f), 0.2f, 0f) },
            { "SC_Leather", (null, new Color(0.3f, 0.09f, 0.06f), 0.45f, 0f) },
            { "SC_Tape", (null, new Color(0.85f, 0.7f, 0.25f), 0.3f, 0f) },
            { "SC_Steel", (null, new Color(0.7f, 0.7f, 0.72f), 0.75f, 1f) },
            { "SC_Thread", (null, new Color(0.6f, 0.08f, 0.08f), 0.3f, 0f) },
            { "SC_ThreadBlue", (null, new Color(0.1f, 0.18f, 0.45f), 0.3f, 0f) },
            { "SC_JarGlass", (null, new Color(0.75f, 0.82f, 0.8f), 0.9f, 0f) },
            { "MN_Linen", ("fabric_pattern_07", new Color(0.95f, 0.88f, 0.72f), 0.1f, 0f) },
            { "MN_Seam", (null, new Color(0.4f, 0.32f, 0.22f), 0.2f, 0f) },
            { "RK_Iron", (null, new Color(0.06f, 0.06f, 0.065f), 0.55f, 0.8f) },
            { "RK_Wool1", (null, new Color(0.18f, 0.2f, 0.26f), 0.1f, 0f) },
            { "RK_Wool2", (null, new Color(0.35f, 0.22f, 0.14f), 0.1f, 0f) },
            { "RK_Wool3", (null, new Color(0.12f, 0.12f, 0.12f), 0.1f, 0f) },
            { "RK_Wool4", (null, new Color(0.42f, 0.38f, 0.3f), 0.1f, 0f) },
            { "RK_Wool5", (null, new Color(0.3f, 0.08f, 0.07f), 0.1f, 0f) },
            { "RK_Wool6", (null, new Color(0.16f, 0.25f, 0.2f), 0.1f, 0f) },
            { "SW_BlackEnamel", (null, new Color(0.025f, 0.025f, 0.028f), 0.85f, 0f) },
            { "SW_GoldDecal", (null, new Color(0.85f, 0.65f, 0.25f), 0.7f, 1f) },
        };
        private readonly Dictionary<string, Material> customMatCache = new Dictionary<string, Material>();

        private Material CustomMaterial(Material src)
        {
            string sourceName = src != null ? src.name : "";
            string key = sourceName.Replace(" (Instance)", "");
            int dot = key.LastIndexOf('.');
            if (dot > 0 && int.TryParse(key.Substring(dot + 1), out _)) key = key.Substring(0, dot);
            Material m;
            if (customMatCache.TryGetValue(key, out m) && m != null) return m;
            (string tex, Color color, float smooth, float metal) d;
            if (!CustomMats.TryGetValue(key, out d))
            {
                // 沒有登記的材質：用 Blender 匯出的顏色。
                var c = new Color(0.6f, 0.58f, 0.55f);
                if (src != null && src.HasProperty("_BaseColor")) c = src.GetColor("_BaseColor");
                else if (src != null && src.HasProperty("_Color")) c = src.GetColor("_Color");
                bool mirror = key == "SH_Mirror";
                d = (null, c, mirror ? 0.95f : 0.25f, mirror ? 1f : 0f);
            }
            float g = d.color.grayscale;
            var anom = Color.Lerp(new Color(g, g, g), d.color, 0.25f) * new Color(0.82f, 0.9f, 1f);
            if (d.tex != null && LanternReveal.HasPH(d.tex, "Tex"))
            {
                m = PH("LC_" + key, d.tex, "Tex", 0f, d.color, anom, d.smooth);
                LanternReveal.AddRuin(m, "distressed_painted_planks", 0f, new Color(0.5f, 0.52f, 0.55f), 0.35f);
            }
            else
            {
                m = Mat("LC_" + key, Mode.Dual, d.color, anom, null, null, d.smooth, 0.3f, 0.006f);
                m.SetFloat("_Metallic", d.metal);
            }
            if (key == "SH_RedBulb") { m.SetColor("_EmissionColor", new Color(2.5f, 0.3f, 0.2f)); m.SetColor("_AnomalyEmission", new Color(2.5f, 0.3f, 0.2f)); }
            customMatCache[key] = m;
            return m;
        }

        /// <summary>放一件 Blender 建的模型（Resources/Models/Custom）：真實尺寸 × 家具倍率，底部貼地；材質換成雙狀態。玻璃先隱藏（shader 不透明）。</summary>
        private Transform CustomModel(string name, string model, Vector3 worldPos, float yaw, bool collider = true)
        {
            var prefab = Resources.Load<GameObject>("Models/Custom/" + model);
            if (prefab == null) return null;
            var root = new GameObject(name).transform;
            root.SetParent(Into, false);
            var inst = Object.Instantiate(prefab, root, false);
            inst.name = model;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var src = r.sharedMaterials;
                if (src.Length == 1 && src[0] != null && src[0].name.StartsWith("SC_Glass")) { r.enabled = false; continue; }
                var arr = new Material[src.Length];
                for (int i = 0; i < src.Length; i++) arr[i] = CustomMaterial(src[i]);
                r.sharedMaterials = arr;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                Register(r, arr[0]);
            }
            root.localScale = Vector3.one * FurnitureScale;
            root.position = worldPos;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (collider)
            {
                var rs = inst.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds;
                    foreach (var r in rs) b.Encapsulate(r.bounds);
                    var box = root.gameObject.AddComponent<BoxCollider>();
                    box.center = root.InverseTransformPoint(b.center);
                    var ls = root.lossyScale;
                    var size = Quaternion.Inverse(root.rotation) * b.size;
                    box.size = new Vector3(Mathf.Abs(size.x) / ls.x, Mathf.Abs(size.y) / ls.y, Mathf.Abs(size.z) / ls.z);
                }
            }
            return root;
        }

        // ---------------- 每個影格 ----------------

        /// <summary>門牌閃爍、進門後封門。回傳這一格是否剛封門（播放聲音用）。</summary>
        public bool V2Tick(Vector3 playerFeet, float dt)
        {
            if (V2Root == null) return false;
            v2Time += dt;
            if (v2Silhouette != null) v2Silhouette.position = v2SilhouetteBase + Vector3.right * Mathf.Sin(v2Time * 0.35f) * 1.0f;
            v2FlickerTimer -= dt;
            if (v2FlickerTimer <= 0f && v2Plate != null)
            {
                v2Show17 = !v2Show17;
                v2FlickerTimer = v2Show17 ? Random.Range(0.06f, 0.35f) : Random.Range(0.4f, 2.2f);
                var t = v2Show17 ? v2Num17 : v2Num16;
                v2Plate.SetTexture("_BaseMap", t);
                v2Plate.SetTexture("_AnomalyMap", t);
            }
            if (!V2Sealed)
            {
                var local = playerFeet - V2Origin;
                if (local.z > 1.3f * V2Scale && local.x > 0f && local.x < V2Width * V2Scale && local.y < 2f)
                {
                    V2Sealed = true;
                    V2SealedAt = playerFeet;
                    if (V2SealWall != null) V2SealWall.gameObject.SetActive(true);
                    if (V2FrontDoorLeaf != null) V2FrontDoorLeaf.gameObject.SetActive(false);
                    if (V2FrontDoor != null) { Blockers.Remove(V2FrontDoor); V2FrontDoor.Solid.enabled = true; }
                    return true;
                }
            }
            return false;
        }

        /// <summary>在新房子裡（含側院）。</summary>
        public bool InV2House(Vector3 p)
        {
            var local = (p - V2Origin) / V2Scale;
            return local.z >= -0.2f && local.z < V2Depth + 2f && local.x > -1f && local.x < V2Width + V2ExtWidth + 1f;
        }

        public bool InV2Street(Vector3 p)
        {
            var local = p - V2Origin;
            return local.z < 0f && local.z > -24f * V2Scale && local.x > -16f && local.x < 30f;
        }
    }
}
