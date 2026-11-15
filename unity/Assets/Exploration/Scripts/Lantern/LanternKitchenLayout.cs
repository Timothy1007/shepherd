using System.Collections.Generic;
using UnityEngine;
using Mode = Shepherd.Exploration.Lantern.LanternReveal.Mode;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 一樓廚房「空間改造 B 方案」（以餐廳 B 的空間品質為基準）。正式遊戲一律是 A（現況），只有廚房 QA 場景會切到 B。
    ///
    /// 建築（都有理由）：
    ///   1. 斜天花板：廚房是後加建的單層附屋，屋頂是從主屋後牆往後下降的單斜屋頂；室內天花板跟著斜下來
    ///      （靠主屋約 3.05 m → 後牆約 2.65 m，平均約 2.85 m）。地板不改（不做往下一階）。
    ///   2. 爐灶凹槽：西牆是和 16 號共用的牆。附屋原本的燒煤爐灶有自己的煙囪壁柱貼在這面共用牆上，
    ///      煙道直接往上穿出附屋屋頂（屋頂上有一根小煙囪）；它和主屋餐廳那根壁柱在同一面牆、相隔約 2 m，互不相連。
    ///      現在把瓦斯爐放進舊爐灶的凹槽裡，抽油煙機裝在凹槽頂下方。
    ///   3. 東北角與後窗下：單純的下櫃＋檯面（使用者回饋 2026-11-16：原本的落地食物櫃看不出用途，改成一般櫃子）。
    ///
    /// 家具與群組（2026-11-16 依試玩回饋修改）：
    ///   煮飯區（西牆）：凹槽南側一段備料台＋碗盤架；凹槽北側緊接著水槽（從後窗下移過來）、再來是切菜與電鍋的台面、最後是微波爐；
    ///   洗菜 → 切菜 → 下鍋一條線。上方吊櫃、磁磚防濺牆。
    ///   後窗下與東北角：下櫃＋檯面；K2 高櫃（原位）在兩段之間。
    ///   窗邊小桌與凳子拿掉（有飯廳，廚房不需要第二張桌子），東窗下留空。
    ///   晾衣架拿掉（一般廚房不會有）。
    ///   儲物：K1 冰箱（原位）、K2 高櫃與餅乾鐵盒（原位）、米袋在冰箱旁。
    ///   生活細節：牆上的舊鍋爐與管線、後門邊的掛鉤與雨鞋。
    ///
    /// 未來的雙狀態：B 的物件依用途分組（KitchenGroup），之後褪色態可以只關掉「生活細節」那組，提燈照亮時再局部顯現。
    /// 未來的隱藏門／樓梯：不在這次新增；刻意保留可擴充的牆面（見 docs/prototype/KITCHEN_LAYOUT_QA_GUIDE.md）。
    /// </summary>
    public sealed partial class LanternGraybox
    {
        public enum KitchenGroup { Structure, Fixtures, Narrative, Life, Memory }

        private readonly List<LayoutMove> kitchenMoves = new List<LayoutMove>();
        private readonly List<GameObject> kitchenOnlyA = new List<GameObject>();
        private readonly List<GameObject> kitchenOnlyB = new List<GameObject>();
        private readonly List<Renderer> kitchenHiddenInB = new List<Renderer>();
        private readonly Dictionary<KitchenGroup, List<GameObject>> kitchenGroups = new Dictionary<KitchenGroup, List<GameObject>>();
        private readonly Dictionary<KitchenGroup, bool> kitchenGroupOn = new Dictionary<KitchenGroup, bool>();

        public bool KitchenLayoutIsB { get; private set; }
        /// <summary>廚房室內範圍（世界座標，QA 用）。</summary>
        public Bounds V2KitchenRoom { get; private set; }

        /// <summary>切換 A（現況）／B（改造）。只移動、顯示或隱藏物件，不重建房子。</summary>
        public void SetKitchenLayout(bool b)
        {
            KitchenLayoutIsB = b;
            foreach (var m in kitchenMoves)
            {
                if (m.T == null) continue;
                m.T.position = b ? m.PB : m.PA;
                m.T.rotation = b ? m.RB : m.RA;
            }
            foreach (var g in kitchenOnlyA) if (g != null) g.SetActive(!b);
            foreach (var g in kitchenOnlyB) if (g != null) g.SetActive(b);
            foreach (var r in kitchenHiddenInB) if (r != null) r.enabled = !b;
            ApplyKitchenGroups();
            Physics.SyncTransforms();
        }

        /// <summary>未來雙狀態的預留開關：顯示／隱藏某一組（例如褪色態只關掉 Life）。只影響 B 的物件與被移到 B 的物件。</summary>
        public void SetKitchenGroupVisible(KitchenGroup g, bool on)
        {
            kitchenGroupOn[g] = on;
            ApplyKitchenGroups();
        }

        public bool KitchenGroupVisible(KitchenGroup g) => !kitchenGroupOn.TryGetValue(g, out var on) || on;

        public IReadOnlyList<GameObject> KitchenGroupMembers(KitchenGroup g) =>
            kitchenGroups.TryGetValue(g, out var l) ? l : (IReadOnlyList<GameObject>)System.Array.Empty<GameObject>();

        private void ApplyKitchenGroups()
        {
            foreach (var kv in kitchenGroups)
            {
                bool on = KitchenGroupVisible(kv.Key);
                foreach (var go in kv.Value)
                {
                    if (go == null) continue;
                    bool onlyB = kitchenOnlyB.Contains(go);
                    if (onlyB) go.SetActive(KitchenLayoutIsB && on);
                    else if (kitchenOnlyA.Contains(go)) continue;
                    else go.SetActive(on || !KitchenLayoutIsB);
                }
            }
        }

        private void KAdd(KitchenGroup g, GameObject go)
        {
            if (go == null) return;
            if (!kitchenGroups.TryGetValue(g, out var l)) kitchenGroups[g] = l = new List<GameObject>();
            l.Add(go);
        }

        private void KMove(Transform t, Vector3 posB, float yawB, params Transform[] followers)
        {
            if (t == null) return;
            var ra = t.rotation; var pa = t.position;
            // 只改水平朝向（保留物件原本的俯仰／側傾，例如躺著的橫桿）。
            var q = Quaternion.Euler(0f, yawB - ra.eulerAngles.y, 0f);
            var rb = q * ra;
            kitchenMoves.Add(new LayoutMove { T = t, PA = pa, RA = ra, PB = posB, RB = rb });
            foreach (var f in followers)
                if (f != null) kitchenMoves.Add(new LayoutMove { T = f, PA = f.position, RA = f.rotation, PB = posB + q * (f.position - pa), RB = q * f.rotation });
        }

        /// <summary>依模型實際外框微調 B 的位置：minX＝左側貼齊（例如背貼西牆），或 cx＝中心 X；cz＝中心 Z；bottom＝底面高度。</summary>
        private void FitB(Transform t, float? minX, float? cx, float cz, float bottom)
        {
            int idx = kitchenMoves.FindLastIndex(m => m.T == t);
            if (idx < 0) return;
            var mv = kitchenMoves[idx];
            t.SetPositionAndRotation(mv.PB, mv.RB);
            var rs = t.GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0)
            {
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float dx = minX.HasValue ? minX.Value - b.min.x : (cx.HasValue ? cx.Value - b.center.x : 0f);
                mv.PB += new Vector3(dx, bottom - b.min.y, cz - b.center.z);
                kitchenMoves[idx] = mv;
            }
            t.SetPositionAndRotation(mv.PA, mv.RA);
        }

        private List<Transform> KFind(string name)
        {
            var l = new List<Transform>();
            foreach (var t in Into.GetComponentsInChildren<Transform>(true)) if (t.name == name && t.parent == Into) l.Add(t);
            return l;
        }
        private Transform KFind1(string name) { var l = KFind(name); return l.Count > 0 ? l[0] : null; }

        /// <summary>在 BuildHouseV2 的最後呼叫（家具、燈都建好之後）。建好 B 的物件並登記移動，預設回到 A。</summary>
        private void V2KitchenLayout()
        {
            kitchenMoves.Clear(); kitchenOnlyA.Clear(); kitchenOnlyB.Clear(); kitchenHiddenInB.Clear(); kitchenGroups.Clear(); kitchenGroupOn.Clear();
            KitchenLayoutIsB = false;
            float f = FurnitureScale;
            // 室內座標：X 往東（離西牆內面）、Y 往北（離主屋後牆內面）。
            float kx0 = HP(0f, 0f, 0f).x + 0.125f, kz0 = HP(0f, 0f, V2Main).z + 0.125f;
            float kW = HP(V2ExtWidth, 0f, 0f).x - 0.125f - kx0, kD = HP(0f, 0f, V2Depth).z - 0.125f - kz0;
            Vector3 K(float X, float y, float Y) => new Vector3(kx0 + X, y, kz0 + Y);
            float CeilY(float Y) => 3.05f - 0.4f * Mathf.Clamp01(Y / kD);
            var room = new Bounds(); room.SetMinMax(K(0f, 0f, 0f), K(kW, 3.5f, kD)); V2KitchenRoom = room;

            // ---------- 材質 ----------
            var anom = new Color(0.5f, 0.5f, 0.5f);
            var tiles = PH("KT_Tiles", "long_white_tiles", "Tex", 0.3f, new Color(0.95f, 0.96f, 0.93f), anom, 0.85f);
            var paintCab = PH("KT_CabinetPaint", "painted_plaster_wall", "Tex", 0.8f, new Color(0.86f, 0.84f, 0.74f), anom, 0.45f);
            var worktop = PH("KT_Worktop", "fine_grained_wood", "Tex", 0.6f, new Color(0.72f, 0.56f, 0.4f), anom, 0.6f);
            var plinth = Mat("KT_Plinth", Mode.Dual, new Color(0.22f, 0.18f, 0.15f), anom, null, null, 0.3f, 0.3f, 0f);
            var knob = Mat("KT_Knob", Mode.Dual, new Color(0.75f, 0.75f, 0.72f), anom, null, null, 0.75f, 0.5f, 0f); knob.SetFloat("_Metallic", 1f);
            var enamel = Mat("KT_Enamel", Mode.Dual, new Color(0.93f, 0.92f, 0.88f), anom, null, null, 0.8f, 0.5f, 0f);
            var hob = Mat("KT_Hob", Mode.Dual, new Color(0.08f, 0.08f, 0.08f), anom, null, null, 0.55f, 0.4f, 0f);
            var ovenGlass = Mat("KT_OvenGlass", Mode.Dual, new Color(0.05f, 0.05f, 0.06f), anom, null, null, 0.9f, 0.6f, 0f);
            var copper = Mat("KT_Copper", Mode.Dual, new Color(0.78f, 0.45f, 0.28f), anom, null, null, 0.7f, 0.5f, 0f); copper.SetFloat("_Metallic", 1f);
            var timber = PH("KT_Timber", "dark_wood", "Tex", 0.6f, new Color(0.7f, 0.55f, 0.42f), anom, 0.4f);
            var cloth1 = PH("KT_TeaTowel", "fabric_pattern_07", "Tex", 0.25f, new Color(0.95f, 0.9f, 0.85f), anom, 0.1f);
            var cloth2 = Mat("KT_Shirt", Mode.Dual, new Color(0.55f, 0.62f, 0.72f), anom, null, null, 0.1f, 0.1f, 0.004f);
            var apron = Mat("KT_Apron", Mode.Dual, new Color(0.7f, 0.25f, 0.2f), anom, null, null, 0.1f, 0.1f, 0.004f);
            var boots = Mat("KT_Boots", Mode.Dual, new Color(0.15f, 0.25f, 0.2f), anom, null, null, 0.5f, 0.4f, 0f);
            var strip = Mat("KT_StripLight", Mode.Dual, new Color(1f, 0.98f, 0.94f), anom, null, null, 0.6f, 0.5f, 0f);
            strip.SetColor("_EmissionColor", new Color(2.2f, 2.1f, 1.9f)); strip.SetColor("_AnomalyEmission", new Color(0.4f, 0.42f, 0.45f));
            var jar = Mat("KT_Jar", Mode.Dual, new Color(0.62f, 0.5f, 0.3f), anom, null, null, 0.7f, 0.5f, 0f);
            var china = Mat("KT_China", Mode.Dual, new Color(0.94f, 0.94f, 0.9f), anom, null, null, 0.85f, 0.5f, 0f);
            var blueChina = Mat("KT_BlueChina", Mode.Dual, new Color(0.38f, 0.5f, 0.7f), anom, null, null, 0.85f, 0.5f, 0f);
            var grille = Mat("KT_LarderGrille", Mode.Dual, new Color(0.25f, 0.24f, 0.22f), anom, null, null, 0.4f, 0.3f, 0f);
            var brick = PH("KT_StackBrick", "raw_plank_wall", "Tex", 0.5f, new Color(0.55f, 0.32f, 0.25f), anom, 0.2f);

            Transform Group(string name) { var t = new GameObject(name).transform; t.SetParent(Into, false); kitchenOnlyB.Add(t.gameObject); return t; }
            var gStruct = Group("KT_B_Structure"); var gFix = Group("KT_B_Fixtures"); var gLife = Group("KT_B_Life"); var gMem = Group("KT_B_Memory");
            KAdd(KitchenGroup.Structure, gStruct.gameObject); KAdd(KitchenGroup.Fixtures, gFix.gameObject); KAdd(KitchenGroup.Life, gLife.gameObject); KAdd(KitchenGroup.Memory, gMem.gameObject);

            // ---------- 1. 斜天花板（單斜屋頂的內面） ----------
            float slope = Mathf.Atan2(0.4f, kD) * Mathf.Rad2Deg;
            var ceil = Box("KT_SlopedCeiling", K(kW / 2f, 2.85f + 0.03f, kD / 2f), new Vector3(kW + 0.02f, 0.06f, Mathf.Sqrt(kD * kD + 0.16f) + 0.02f), hCeil, false, new Vector3(slope, 0f, 0f), gStruct);
            ceil.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            // 兩根外露的椽木（看得出天花板是跟著屋頂斜的）。
            foreach (var X in new[] { kW * 0.33f, kW * 0.67f })
                Box("KT_CeilingJoist", K(X, 2.85f - 0.04f, kD / 2f), new Vector3(0.09f, 0.09f, kD), timber, false, new Vector3(slope, 0f, 0f), gStruct);
            // 主屋後牆和斜天花板的交接：一條橫向的木條。
            Box("KT_WallPlate", K(kW / 2f, CeilY(0f) - 0.06f, 0.05f), new Vector3(kW, 0.1f, 0.1f), timber, false, null, gStruct);

            // ---------- 2. 爐灶凹槽（共用牆上的附屋煙囪壁柱） ----------
            const float brY0 = 1.45f, brY1 = 3.0f, brD = 0.42f, cheek = 0.2f, openTop = 1.75f;
            float hS = CeilY(brY0), hN = CeilY(brY1);
            Box("KT_BreastCheekS", K(brD / 2f, hS / 2f, brY0 + cheek / 2f), new Vector3(brD, hS, cheek), hWallKitchen, true, null, gStruct);
            Box("KT_BreastCheekN", K(brD / 2f, hN / 2f, brY1 - cheek / 2f), new Vector3(brD, hN, cheek), hWallKitchen, true, null, gStruct);
            float hMid = CeilY((brY0 + brY1) / 2f);
            Box("KT_BreastHead", K(brD / 2f, (openTop + hMid) / 2f, (brY0 + brY1) / 2f), new Vector3(brD, hMid - openTop + 0.05f, brY1 - brY0 - 2f * cheek), hWallKitchen, false, null, gStruct);
            Box("KT_BreastLintel", K(brD + 0.01f, openTop + 0.04f, (brY0 + brY1) / 2f), new Vector3(0.04f, 0.1f, brY1 - brY0 - 2f * cheek + 0.08f), timber, false, null, gStruct);
            Box("KT_RecessTiles", K(0.012f, 1.25f, (brY0 + brY1) / 2f), new Vector3(0.01f, 1.0f, brY1 - brY0 - 2f * cheek), tiles, false, null, gStruct);
            Box("KT_MantelShelf", K(brD + 0.08f, openTop + 0.17f, (brY0 + brY1) / 2f), new Vector3(0.16f, 0.03f, brY1 - brY0 + 0.1f), timber, false, null, gStruct);
            // 煙道往上穿出附屋屋頂：屋頂上的小煙囪（從側院看得到）。
            float roofTop = HP(0f, V2Ground + 0.05f, 0f).y;
            Box("KT_ChimneyStack", K(brD / 2f + 0.02f, roofTop + 0.5f, (brY0 + brY1) / 2f), new Vector3(brD + 0.08f, 1.0f, 0.62f), brick, false, null, gStruct);
            foreach (var dz in new[] { -0.14f, 0.14f })
                Cyl("KT_ChimneyPot", K(brD / 2f + 0.02f, roofTop + 1.12f, (brY0 + brY1) / 2f + dz), new Vector3(0.14f, 0.12f, 0.14f), brick, null, gStruct);

            // 瓦斯爐（白色琺瑯）放進凹槽。
            float cy = (brY0 + brY1) / 2f;
            Box("KT_CookerBody", K(0.33f, 0.45f, cy), new Vector3(0.58f, 0.88f, 0.6f), enamel, true, null, gFix);
            Box("KT_CookerHob", K(0.33f, 0.895f, cy), new Vector3(0.58f, 0.02f, 0.6f), hob, false, null, gFix);
            Box("KT_CookerBack", K(0.06f, 1.0f, cy), new Vector3(0.04f, 0.2f, 0.6f), enamel, false, null, gFix);
            Box("KT_OvenDoor", K(0.625f, 0.4f, cy), new Vector3(0.012f, 0.5f, 0.5f), enamel, false, null, gFix);
            Box("KT_OvenWindow", K(0.633f, 0.42f, cy), new Vector3(0.006f, 0.24f, 0.36f), ovenGlass, false, null, gFix);
            Cyl("KT_OvenHandle", K(0.65f, 0.68f, cy), new Vector3(0.018f, 0.22f, 0.018f), knob, new Vector3(90f, 0f, 0f), gFix);
            for (int i = 0; i < 4; i++) Cyl("KT_Knob", K(0.63f, 0.8f, cy - 0.18f + i * 0.12f), new Vector3(0.035f, 0.012f, 0.035f), knob, new Vector3(0f, 0f, 90f), gFix);
            for (int i = 0; i < 4; i++) Cyl("KT_Burner", K(0.2f + (i / 2) * 0.24f, 0.91f, cy - 0.13f + (i % 2) * 0.26f), new Vector3(0.1f, 0.01f, 0.1f), hob, null, gFix);

            // ---------- 3. 流理台（凹槽兩側各一段）＋防濺磁磚＋吊櫃與碗盤架 ----------
            const float topY = 0.94f, depth = 0.62f;
            void Counter(string name, float y0, float y1, Transform parent)
            {
                float len = y1 - y0, mid = (y0 + y1) / 2f;
                Box(name + "_Plinth", K(0.26f, 0.05f, mid), new Vector3(0.52f, 0.1f, len), plinth, true, null, parent);
                Box(name + "_Carcass", K(0.29f, 0.5f, mid), new Vector3(0.58f, 0.8f, len), paintCab, true, null, parent);
                Box(name + "_Top", K(depth / 2f, topY - 0.018f, mid), new Vector3(depth, 0.036f, len + 0.01f), worktop, false, null, parent);
                int doors = Mathf.Max(1, Mathf.RoundToInt(len / 0.5f));
                float dw = len / doors;
                for (int i = 0; i < doors; i++)
                {
                    float c = y0 + dw * (i + 0.5f);
                    Box(name + "_Door", K(0.585f, 0.48f, c), new Vector3(0.012f, 0.68f, dw - 0.03f), paintCab, false, null, parent);
                    Box(name + "_DoorPanel", K(0.593f, 0.48f, c), new Vector3(0.006f, 0.54f, dw - 0.13f), paintCab, false, null, parent);
                    Box(name + "_Knob", K(0.6f, 0.7f, c + (i % 2 == 0 ? dw / 2f - 0.07f : -dw / 2f + 0.07f)), new Vector3(0.02f, 0.025f, 0.025f), knob, false, null, parent);
                }
                Box(name + "_Splash", K(0.008f, topY + 0.3f, mid), new Vector3(0.012f, 0.6f, len), tiles, false, null, parent);
            }
            Counter("KT_CounterS", 0.55f, brY0, gFix);
            // 吊櫃（北段上方）。
            float wcY0 = brY1 + 0.15f, wcY1 = 5.45f, wcMid = (wcY0 + wcY1) / 2f, wcLen = wcY1 - wcY0;
            Box("KT_WallCabinet", K(0.17f, 1.85f, wcMid), new Vector3(0.34f, 0.7f, wcLen), paintCab, false, null, gFix);
            int wd = Mathf.RoundToInt(wcLen / 0.55f);
            for (int i = 0; i < wd; i++)
            {
                float c = wcY0 + wcLen / wd * (i + 0.5f);
                Box("KT_WallCabinetDoor", K(0.345f, 1.85f, c), new Vector3(0.01f, 0.64f, wcLen / wd - 0.03f), paintCab, false, null, gFix);
                Box("KT_WallCabinetKnob", K(0.355f, 1.6f, c + (i % 2 == 0 ? 0.18f : -0.18f)), new Vector3(0.02f, 0.025f, 0.025f), knob, false, null, gFix);
            }
            // 南段上方：開放式碗盤架（看得到碗盤，屬生活細節）。
            Box("KT_PlateShelf", K(0.13f, 1.55f, (0.55f + brY0) / 2f), new Vector3(0.26f, 0.025f, brY0 - 0.6f), timber, false, null, gFix);
            Box("KT_PlateShelf2", K(0.13f, 1.9f, (0.55f + brY0) / 2f), new Vector3(0.26f, 0.025f, brY0 - 0.6f), timber, false, null, gFix);
            for (int i = 0; i < 5; i++) Cyl("KT_ShelfPlate", K(0.12f, 1.58f + i * 0.012f, 0.8f), new Vector3(0.22f, 0.006f, 0.22f), i % 2 == 0 ? china : blueChina, null, gLife);
            for (int i = 0; i < 3; i++) Cyl("KT_ShelfBowl", K(0.12f, 1.6f + i * 0.035f, 1.15f), new Vector3(0.13f, 0.02f, 0.13f), china, null, gLife);
            for (int i = 0; i < 3; i++) Cyl("KT_ShelfJar", K(0.12f, 1.98f, 0.75f + i * 0.2f), new Vector3(0.09f, 0.07f, 0.09f), jar, null, gLife);
            // 凹槽上方的層板：鐵罐、茶葉罐。
            for (int i = 0; i < 3; i++) Cyl("KT_MantelTin", K(brD + 0.08f, openTop + 0.25f, cy - 0.35f + i * 0.33f), new Vector3(0.08f, 0.06f + i * 0.01f, 0.08f), i == 1 ? blueChina : jar, null, gLife);

            // ---------- 4. 水槽移到切菜區旁（使用者回饋 2026-11-16）：北段流理台＝水槽＋備料台面 ----------
            // 水槽單元（原本在後窗下）轉 90° 靠西牆，緊接在凹槽北側；旁邊就是砧板與電鍋，洗菜 → 切菜 → 下鍋一條線。
            var sinkT = KFind1("V2SinkUnit");
            float sinkW = 1.0f;
            if (sinkT != null) { var rs0 = sinkT.GetComponentsInChildren<Renderer>(); if (rs0.Length > 0) { var b0 = rs0[0].bounds; foreach (var r in rs0) b0.Encapsulate(r.bounds); sinkW = b0.size.x; } }
            float sinkY0 = brY1 + 0.03f, sinkY1 = sinkY0 + sinkW;
            Counter("KT_CounterN", sinkY1 + 0.02f, 5.6f, gFix);
            Box("KT_SinkSplash", K(0.008f, topY + 0.3f, (sinkY0 + sinkY1) / 2f), new Vector3(0.012f, 0.6f, sinkW), tiles, false, null, gFix);

            // ---------- 5. 後牆（後窗下）與東北角：單純的下櫃＋檯面 ----------
            void CounterBack(string name, float x0, float x1, Transform parent)
            {
                float len = x1 - x0, mid = (x0 + x1) / 2f;
                Box(name + "_Plinth", K(mid, 0.05f, kD - 0.26f), new Vector3(len, 0.1f, 0.52f), plinth, true, null, parent);
                Box(name + "_Carcass", K(mid, 0.5f, kD - 0.29f), new Vector3(len, 0.8f, 0.58f), paintCab, true, null, parent);
                Box(name + "_Top", K(mid, topY - 0.018f, kD - depth / 2f), new Vector3(len + 0.01f, 0.036f, depth), worktop, false, null, parent);
                int doors = Mathf.Max(1, Mathf.RoundToInt(len / 0.5f));
                float dw = len / doors;
                for (int i = 0; i < doors; i++)
                {
                    float c = x0 + dw * (i + 0.5f);
                    Box(name + "_Door", K(c, 0.48f, kD - 0.585f), new Vector3(dw - 0.03f, 0.68f, 0.012f), paintCab, false, null, parent);
                    Box(name + "_DoorPanel", K(c, 0.48f, kD - 0.593f), new Vector3(dw - 0.13f, 0.54f, 0.006f), paintCab, false, null, parent);
                    Box(name + "_Knob", K(c + (i % 2 == 0 ? dw / 2f - 0.07f : -dw / 2f + 0.07f), 0.7f, kD - 0.6f), new Vector3(0.025f, 0.025f, 0.02f), knob, false, null, parent);
                }
                Box(name + "_Splash", K(mid, topY + 0.15f, kD - 0.008f), new Vector3(len, 0.3f, 0.012f), tiles, false, null, parent);
            }
            CounterBack("KT_CounterBack", 0f, 2.88f, gFix);       // 後窗下（原本水槽的位置），K2 高櫃在它東邊
            CounterBack("KT_CounterCorner", 3.65f, kW, gFix);     // 東北角（原本的落地食物櫃改成單純的下櫃）

            // ---------- 6. 生活細節：鍋爐、後門掛鉤與雨鞋（晾衣架依回饋拿掉） ----------
            // 鍋爐（南牆、流理台南段上方），銅管往下進地板，一條沿踢腳板往東接到餐廳暖氣。
            var boiler = K(0.42f, 1.9f, 0.17f);
            Box("KT_Boiler", boiler, new Vector3(0.42f, 0.66f, 0.3f), enamel, false, null, gFix);
            Box("KT_BoilerPanel", boiler + new Vector3(0f, -0.22f, 0.152f), new Vector3(0.3f, 0.12f, 0.006f), plinth, false, null, gFix);
            foreach (var dx in new[] { -0.1f, 0.1f })
                Cyl("KT_BoilerPipe", K(0.42f + dx, 0.78f, 0.06f), new Vector3(0.022f, 0.79f, 0.022f), copper, null, gFix);
            Cyl("KT_PipeRun", K(0.95f, 0.08f, 0.04f), new Vector3(0.022f, 0.45f, 0.022f), copper, new Vector3(0f, 0f, 90f), gFix);
            // 後門邊：掛鉤條、圍裙、購物袋、雨鞋。
            Box("KT_HookRail", K(kW - 0.02f, 1.6f, 5.5f), new Vector3(0.025f, 0.06f, 0.3f), timber, false, null, gLife);
            Box("KT_Apron", K(kW - 0.04f, 1.28f, 5.45f), new Vector3(0.01f, 0.6f, 0.26f), apron, false, null, gLife);
            Box("KT_ShoppingBag", K(kW - 0.06f, 1.3f, 5.6f), new Vector3(0.05f, 0.32f, 0.22f), cloth1, false, null, gLife);
            foreach (var dz in new[] { 0f, 0.13f })
            {
                Box("KT_Boot", K(kW - 0.18f, 0.16f, 5.4f + dz), new Vector3(0.24f, 0.32f, 0.1f), boots, false, null, gLife);
                Box("KT_BootFoot", K(kW - 0.1f, 0.04f, 5.4f + dz), new Vector3(0.12f, 0.08f, 0.1f), boots, false, null, gLife);
            }

            // ---------- 7. B 的燈：天花板日光燈條＋爐灶凹槽的小燈 ----------
            var stripAt = K(2.3f, CeilY(3.1f) - 0.05f, 3.1f);
            Box("KT_StripLight", stripAt, new Vector3(0.12f, 0.06f, 1.2f), strip, false, new Vector3(slope, 0f, 0f), gStruct);
            var ls = AddLight("KT_StripLightLight", LightType.Point, stripAt + Vector3.down * 0.25f, new Color(1f, 0.94f, 0.84f), 1.5f, 5.5f, false, LightShadows.Soft);
            var lh = AddLight("KT_HoodLight", LightType.Point, K(0.5f, 1.45f, cy), new Color(1f, 0.78f, 0.52f), 0.55f, 1.8f, false, LightShadows.None);
            ls.transform.SetParent(gStruct, true); lh.transform.SetParent(gFix, true);

            // 接觸陰影。
            Blob(K(0.31f, 0f, (0.55f + brY0) / 2f), new Vector2(0.62f, brY0 - 0.55f), 0f, 0.3f).SetParent(gFix, true);
            Blob(K(0.31f, 0f, (brY1 + 5.6f) / 2f), new Vector2(0.62f, 5.6f - brY1), 0f, 0.3f).SetParent(gFix, true);
            Blob(K(0.33f, 0f, cy), new Vector2(0.62f, 0.62f), 0f, 0.3f).SetParent(gFix, true);
            Blob(K(1.44f, 0f, kD - 0.3f), new Vector2(2.88f, 0.62f), 0f, 0.3f).SetParent(gFix, true);
            Blob(K((3.65f + kW) / 2f, 0f, kD - 0.3f), new Vector2(kW - 3.65f, 0.62f), 0f, 0.3f).SetParent(gFix, true);

            // ---------- 既有物件：A 專屬（B 換成上面的新家具）、移到 B 的位置 ----------
            foreach (var n in new[] { "V2Counter2", "V2CounterTop2", "V2Stove", "V2WallCupboard", "V2KitchenTable" })
                foreach (var t in KFind(n)) kitchenOnlyA.Add(t.gameObject);
            foreach (var t in KFind("V2CupboardSeam")) kitchenOnlyA.Add(t.gameObject);
            // 抽油煙機進凹槽頂下方。
            KMove(KFind1("V2RangeHood"), K(0.3f, 1.58f, cy), 0f);
            // 炒鍋放到瓦斯爐上（前左爐口）。
            KMove(KFind1("V2Wok"), K(0.44f, 0.905f, cy - 0.13f), 90f);
            // 掛勺子的橫桿：南段防濺牆上。
            var rail = KFind1("V2UtensilRail");
            var ladles = new List<Transform>(); ladles.AddRange(KFind("V2Ladle")); ladles.AddRange(KFind("V2LadleHead"));
            KMove(rail, K(0.04f, 1.38f, 1.0f), rail != null ? rail.eulerAngles.y : 0f, ladles.ToArray());
            // 水槽單元：靠西牆、凹槽北側（背貼牆、底貼地，依模型實際大小對齊）。
            if (sinkT != null) { KMove(sinkT, K(0.3f, 0f, (sinkY0 + sinkY1) / 2f), 90f); FitB(sinkT, kx0 + 0.005f, null, kz0 + (sinkY0 + sinkY1) / 2f, 0f); }
            // 電鍋、砧板等（clutter）緊接在水槽旁的台面；微波爐放台面最後面，依模型實際大小放在檯面上、不超出檯面。
            float prepMid = (sinkY1 + 5.6f) / 2f;
            KMove(KFind1("V2KitchenClutter"), K(0.33f, topY, prepMid + 0.1f), 90f);
            // 微波爐：後窗下那段台面的東端、K2 高櫃旁邊（面向南），把西牆的備料台面留給切菜與電鍋。
            var mw = KFind1("V2Microwave");
            if (mw != null) { KMove(mw, K(2.45f, topY, kD - 0.3f), 180f); FitB(mw, null, kx0 + 2.45f, kz0 + kD - 0.32f, topY); }
            // 窗邊小桌與凳子依回饋拿掉（B 不需要第二張桌子；有飯廳）：既有的凳子只在 A 出現。
            foreach (var t in KFind("V2KitchenStool")) kitchenOnlyA.Add(t.gameObject);
            // 碗筷：洗好放在水槽旁的台面上。
            var bowl = KFind1("V2KitchenBowl"); var sticks = KFind1("V2KitchenChopsticks");
            if (bowl != null) KMove(bowl, K(0.3f, topY + 0.03f, sinkY1 + 0.18f), bowl.eulerAngles.y);
            if (sticks != null) KMove(sticks, K(0.42f, topY + 0.006f, sinkY1 + 0.18f), sticks.eulerAngles.y);
            // 米袋：冰箱西側靠牆。
            KMove(KFind1("V2RiceSack"), K(3.55f, 0f, 0.32f), 25f);
            // 舊燈（異域的冷燈）：燈泡與燈罩在斜天花板上方，B 時隱藏；燈本身移到天花板下。
            foreach (var t in KFind("V2KitchenLightBulb")) { var r = t.GetComponent<Renderer>(); if (r != null) kitchenHiddenInB.Add(r); }
            foreach (var t in KFind("V2KitchenLightShade")) { var r = t.GetComponent<Renderer>(); if (r != null) kitchenHiddenInB.Add(r); }
            foreach (var t in KFind("V2KitchenLight")) if (t.GetComponent<Light>() != null) KMove(t, K(2.4f, 2.45f, 3.1f), t.eulerAngles.y);
            // 附屋原本的平屋頂：B 的斜天花板在下面，室內看不到；保留（外觀改成斜屋頂之後再做）。

            // 雙狀態分組：既有物件也登記進去（K1／K2 屬敘事組，永遠不隱藏）。
            foreach (var n in new[] { "V2KitchenClutter", "V2Wok", "V2KitchenBowl", "V2KitchenChopsticks", "V2RiceSack" }) KAdd(KitchenGroup.Life, KFind1(n)?.gameObject);
            foreach (var n in new[] { "V2FridgeModel", "V2TallCupboard", "V2TallCabinet", "V2CookieTin" }) KAdd(KitchenGroup.Narrative, KFind1(n)?.gameObject);
            foreach (var n in new[] { "V2SinkUnit", "V2Microwave", "V2KitchenBins", "V2KitchenStool", "V2RangeHood" }) KAdd(KitchenGroup.Fixtures, KFind1(n)?.gameObject);

            SetKitchenLayout(false);
        }
    }
}
