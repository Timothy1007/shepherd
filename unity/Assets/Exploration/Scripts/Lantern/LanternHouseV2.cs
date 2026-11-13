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
        public readonly List<LanternToggle> V2Doors = new List<LanternToggle>();
        /// <summary>推門（拉鋸）的門。</summary>
        public readonly List<LanternStruggle> Struggles = new List<LanternStruggle>();
        public LanternStruggle V2DiningStruggle { get; private set; }
        private Material v2Plate, v2Brick;
        private Texture v2Num16, v2Num17;
        private float v2FlickerTimer;
        private bool v2Show17;
        private Vector3 V2(float x, float y, float z) => V2Origin + new Vector3(x * V2Scale, y * (1f + (V2Scale - 1f) * 0.55f), z * V2Scale);

        public void BuildHouseV2()
        {
            if (V2Root != null) { ArenaArt.Kill(V2Root.gameObject); LanternNav.Remove("v2"); }
            V2Doors.Clear();
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

        private void V2Street()
        {
            var road = Mat("V2_Road", Mode.Dual, new Color(0.18f, 0.18f, 0.19f), new Color(0.12f, 0.13f, 0.14f), null, null, 0.35f, 0.4f, 0.004f, true);
            var pave = PH("V2_Pavement", "concrete_floor_worn_001", "Tex", 1.5f, new Color(0.7f, 0.7f, 0.72f), new Color(0.4f, 0.45f, 0.5f), 0.5f);
            v2Brick = Mat("V2_Brick", Mode.Dual, new Color(0.45f, 0.24f, 0.18f), new Color(0.25f, 0.24f, 0.26f), null, null, 0.2f, 0.25f, 0.006f, true);
            LanternReveal.AddRuin(v2Brick, "raw_plank_wall", 0f, new Color(0.5f, 0.52f, 0.55f), 0.3f);
            Box("V2Road", V2(V2Width / 2f, 0f, -6f) + Vector3.down * 0.1f, new Vector3(30f, 0.2f, 6f * V2Scale), road);
            Box("V2Pavement", V2(V2Width / 2f, 0f, -1.8f) + Vector3.down * 0.06f, new Vector3(30f, 0.12f, 3.6f * V2Scale), pave);
            Box("V2Kerb", V2(V2Width / 2f, 0f, -3.65f), new Vector3(30f, 0.14f, 0.15f), hWhite, false);
            // 左右鄰居（16、18 號）的門面。
            var dark = Mat("V2_DarkWindow", Mode.Dual, new Color(0.04f, 0.05f, 0.07f), new Color(0.03f, 0.04f, 0.05f), null, null, 0.9f, 0.9f, 0f);
            foreach (var side in new[] { -1, 1 })
            {
                float cx = side < 0 ? -V2Width / 2f : V2Width * 1.5f;
                Box("V2Neighbour", V2(cx, 3.0f, -0.2f), new Vector3(V2Width * V2Scale, 7f, 0.4f), v2Brick);
                Box("V2NeighbourDoor", V2(cx + side * 1.6f, 1.1f, -0.42f), new Vector3(1.0f * V2Scale, 2.2f * 1.165f, 0.06f), hDark, false);
                Box("V2NeighbourWin", V2(cx - side * 1.0f, 1.6f, -0.42f), new Vector3(2.4f * V2Scale, 1.4f, 0.04f), dark, false);
                Box("V2NeighbourWin", V2(cx, 4.4f, -0.42f), new Vector3(3.6f * V2Scale, 1.3f, 0.04f), dark, false);
                var nm = Mat("V2_Num" + (side < 0 ? 16 : 18), Mode.Dual, Color.white, Color.white,
                    LanternReveal.Number(side < 0 ? 16 : 18, new Color32(220, 214, 200, 255), new Color32(30, 30, 30, 255)),
                    LanternReveal.Number(side < 0 ? 16 : 18, new Color32(200, 205, 210, 255), new Color32(30, 30, 30, 255)), 0.3f, 0.3f, 0f);
                Quad("V2NeighbourNumber", V2(cx + side * 2.35f, 2.1f, -0.43f), new Vector3(0.28f, 0.28f, 1f), new Vector3(0f, 0f, 0f), nm);
            }
            // 我們的門牌：16 和 17 之間一直閃。
            v2Num16 = LanternReveal.Number(16, new Color32(220, 214, 200, 255), new Color32(30, 30, 30, 255));
            v2Num17 = LanternReveal.Number(17, new Color32(235, 225, 200, 255), new Color32(90, 20, 20, 255));
            v2Plate = Mat("V2_Num17", Mode.Dual, Color.white, Color.white, v2Num16, v2Num16, 0.3f, 0.3f, 0f);
            v2Plate.SetColor("_EmissionColor", new Color(0.25f, 0.22f, 0.18f));
            v2Plate.SetColor("_AnomalyEmission", new Color(0.2f, 0.2f, 0.22f));
            Quad("V2DoorNumber", V2(5.95f, 2.15f, -0.16f), new Vector3(0.3f, 0.3f, 1f), new Vector3(0f, 0f, 0f), v2Plate);
            // 店名招牌（只剩一半，暫定）。
            Box("V2ShopSign", V2(V2Width / 2f, 2.75f, -0.18f), new Vector3(6.0f * V2Scale, 0.45f, 0.08f), hWood, false);
            // 街燈（橘光）：從外面照進店面的大櫥窗。
            Cyl("V2LampPost", V2(1.2f, 0f, -3.3f) + Vector3.up * 2.3f, new Vector3(0.12f, 2.3f, 0.12f), hDark);
            var sl = AddLight("V2StreetLamp", LightType.Spot, V2(1.2f, 0f, -3.1f) + Vector3.up * 4.4f, new Color(1f, 0.62f, 0.3f), 6f, 16f, false, LightShadows.Soft, 85f, new Vector3(0.15f, -0.55f, 1f));
            sl.innerSpotAngle = 40f;
            AddLight("V2StreetGlow", LightType.Point, V2(1.2f, 0f, -3.1f) + Vector3.up * 4.4f, new Color(1f, 0.62f, 0.3f), 1.2f, 6f, false, LightShadows.None);
            // 街道範圍（看不見的牆）。
            var c = V2(V2Width / 2f, 0f, -4.5f);
            Invisible(c + new Vector3(-9f, 4f, 0f), new Vector3(0.3f, 8f, 12f));
            Invisible(c + new Vector3(15.5f, 4f, 0f), new Vector3(0.3f, 8f, 12f));
            Invisible(c + new Vector3(3f, 4f, -4.2f), new Vector3(26f, 8f, 0.3f));
            Mark("V2Street", V2(5.1f, 0f, -2.6f) + Vector3.up * 0.05f, V2(5.1f, 1.3f, 0f));
        }

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

            // 門面（街道側）：店門、大櫥窗、二樓兩扇窗。
            Win(0.5f, 4.0f, 0.45f, 2.55f, 1f);
            Win(0.8f, 2.6f, H1 + 0.9f, H1 + 2.2f, 1f);
            Win(3.7f, 5.7f, H1 + 0.9f, H1 + 2.2f, 1f);
            HWallX(0f, 0f, V2Width, 0f, H, ExtT, v2Brick, D, new Vector2(4.6f, 5.6f));
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

            // 開場的前門：暗處是木板釘死的門洞（異域），提燈照到才是開著的門。
            var boarded = Box("V2FrontBoards", V2(5.1f, 1.12f, 0f), new Vector3(1.0f * V2Scale, 2.25f * 1.165f, ExtT + 0.04f), hBoard, true);
            var bl = boarded.gameObject.AddComponent<LanternAnomalyBlocker>();
            bl.Solid = boarded.GetComponent<Collider>();
            bl.Probe = V2(5.1f, 1.0f, 0f);
            bl.ExtraProbes = new[] { V2(5.1f, 1.0f, 0.8f), V2(5.1f, 1.0f, 1.5f) };
            Blockers.Add(bl);
            V2FrontDoor = bl;
            for (int i = 0; i < 5; i++)
                Box("V2FrontPlank", V2(5.1f, 0.3f + i * 0.45f, -0.03f) + Vector3.back * 0.12f, new Vector3(1.25f * V2Scale, 0.16f, 0.04f), hBoard, false, new Vector3(0f, 0f, (i % 2 == 0 ? 5f : -4f)));
            // 真實的門：開著的深綠色木門（只在光裡）。
            var doorMat = Mat("V2_FrontDoor", Mode.RealOnly, new Color(0.12f, 0.25f, 0.2f), Color.black, null, null, 0.45f, 0.4f, 0f);
            var leafHinge = V2(4.6f, 0f, 0f) + new Vector3(0f, 0f, 0.05f);
            Box("V2FrontDoorLeaf", leafHinge + Quaternion.Euler(0f, -80f, 0f) * new Vector3(0.65f, 1.3f, 0f), new Vector3(1.0f * V2Scale, 2.25f * 1.165f, 0.05f), doorMat, false, new Vector3(0f, -80f, 0f));
            // 封門：進去之後出現的牆（兩個狀態都在，擋住回頭路）。
            V2SealWall = Box("V2SealWall", V2(5.1f, 1.2f, 0f) + Vector3.forward * 0.02f, new Vector3(1.05f * V2Scale, 2.4f * 1.165f, ExtT + 0.06f), v2Brick, true);
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
            // 店面：櫃台、衣架（掛著改好的衣服）、人台、縫紉台、試衣簾。
            Fur("V2Counter", 2.4f, 4.1f, 0f, 2.6f, 0.95f, 0.6f, hWood);
            Fur("V2CounterTop", 2.4f, 4.1f, 0.95f, 2.7f, 0.04f, 0.66f, hLightWood, false);
            Fur("V2SewingTable", 0.55f, 3.2f, 0f, 0.6f, 0.75f, 1.2f, hWood);
            var rackMat = hDark;
            Cyl("V2RackRail", V2(0.35f, 1.65f, 2.2f), new Vector3(0.03f, 1.0f * V2Scale, 0.03f), rackMat, new Vector3(90f, 0f, 0f));
            var cloth = new[] { hFabric, hLeather, hWhite, hFabric, hDark, hLeather };
            for (int i = 0; i < 6; i++)
                Box("V2Garment", V2(0.38f, 1.15f, 1.0f + i * 0.4f), new Vector3(0.12f, 0.95f, 0.45f), cloth[i], false, new Vector3(0f, (i % 2) * 6f, 0f));
            Cyl("V2Mannequin", V2(3.2f, 0f, 2.0f) + Vector3.up * 0.75f, new Vector3(0.36f, 0.45f, 0.26f), hLightWood);
            Cyl("V2MannequinPole", V2(3.2f, 0f, 2.0f) + Vector3.up * 0.2f, new Vector3(0.05f, 0.2f, 0.05f), hDark);
            Cyl("V2MannequinBase", V2(3.2f, 0f, 2.0f) + Vector3.up * 0.02f, new Vector3(0.35f, 0.02f, 0.35f), hDark);
            Box("V2FittingCurtain", V2(5.9f, 1.2f, 3.6f), new Vector3(1.0f * V2Scale, 2.2f, 0.05f), hFabric, false);
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
        }

        private void V2Lights()
        {
            void Lamp(string name, float x, float z, float range)
            {
                var at = V2(x, V2Ground - 0.45f, z);
                Ball(name + "Bulb", at + Vector3.up * 0.12f, new Vector3(0.12f, 0.14f, 0.12f), hBulb);
                Cyl(name + "Shade", at + Vector3.up * 0.2f, new Vector3(0.28f, 0.06f, 0.28f), hTrim);
                var l = AddLight(name, LightType.Point, at, new Color(0.9f, 0.85f, 1f), 1.5f, range * V2Scale, true, LightShadows.Soft);
                HouseLamps.Add(l);
            }
            Lamp("V2ShopLight", 2.6f, 2.5f, 5.5f);
            Lamp("V2HallLight", 5.0f, 7.5f, 4f);
            Lamp("V2DiningLight", 2.2f, 7.5f, 4.5f);
            Lamp("V2KitchenLight", 2.0f, 12.5f, 4.5f);
        }

        // ---------------- 每個影格 ----------------

        /// <summary>門牌閃爍、進門後封門。回傳這一格是否剛封門（播放聲音用）。</summary>
        public bool V2Tick(Vector3 playerFeet, float dt)
        {
            if (V2Root == null) return false;
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
                    if (V2FrontDoor != null) { Blockers.Remove(V2FrontDoor); V2FrontDoor.Solid.enabled = true; }
                    return true;
                }
            }
            return false;
        }

        public bool InV2Street(Vector3 p)
        {
            var local = p - V2Origin;
            return local.z < 0f && local.z > -6f * V2Scale && local.x > -9f && local.x < V2Width * V2Scale + 9f;
        }
    }
}
