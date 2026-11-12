using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;
using Mode = Shepherd.Exploration.Lantern.LanternReveal.Mode;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>可以修復的線索（第十七號門的矛盾）。完成方式依種類：聚光照它、復原、放回原位、找到並閱讀。</summary>
    public sealed class LanternClue : MonoBehaviour
    {
        public enum Kind { Focus, Restore, Place, Read }
        public Kind Type;
        public string Id;
        public string Label;
        public LanternRestorable Restorable;
        public LanternPlacementSpot Spot;
        public LanternReadable Readable;
        public float FocusProgress { get; private set; }
        public bool Done { get; private set; }

        /// <summary>依底下的機制更新完成狀態。回傳是否剛好完成。</summary>
        public bool Refresh()
        {
            if (Done) return false;
            bool ok = Type == Kind.Restore ? Restorable != null && Restorable.Done
                : Type == Kind.Place ? Spot != null && Spot.Solved
                : Type == Kind.Read ? Readable != null && Readable.Read
                : FocusProgress >= 1f;
            if (ok) Done = true;
            return ok;
        }

        /// <summary>聚光照著它（Focus 種類）。</summary>
        public void AddFocus(float amount) { if (!Done && Type == Kind.Focus) FocusProgress = Mathf.Clamp01(FocusProgress + amount); }

        /// <summary>測試／除錯用：直接完成。</summary>
        public void ForceComplete() { Done = true; FocusProgress = 1f; }
    }

    /// <summary>可以閱讀的東西（艾琳的研究筆記）。</summary>
    public sealed class LanternReadable : MonoBehaviour
    {
        public string Title;
        [TextArea] public string Text;
        public bool Read { get; set; }
    }

    /// <summary>
    /// 第十七號門的住宅（灰盒，體感比例）：沿用九區格局，但建築放大（預設 1.4 倍），讓第一人稱走起來像一個有空間的家；
    /// 家具只略微放大，維持可信的大小。兩層樓：一樓玄關、客廳、餐廳、廚房、後廚與側院；二樓母親臥室、艾琳的房間、書房、
    /// 沿著樓梯井的走廊。五個可以修復的線索散在各處，順序不限。
    /// </summary>
    public sealed partial class LanternGraybox
    {
        public static readonly float[] HouseScales = { 1.25f, 1.4f, 1.6f };
        public const float HouseDefaultScale = 1.4f;
        public static readonly Vector3 HouseOrigin = new Vector3(60f, 0f, 0f);
        /// <summary>九區平面的實際尺寸（m）：寬 4.77、深 13.76；一樓層高 3.0（含樓板）、二樓淨高 2.55。</summary>
        public const float RealWidth = 4.77f, RealDepth = 13.76f, RealFloor2 = 3.0f, RealUpperHeight = 2.55f, RealGroundCeiling = 2.75f;

        public Transform HouseRoot { get; private set; }
        public float HouseScale { get; private set; } = HouseDefaultScale;
        public float HouseHeightScale => 1f + (HouseScale - 1f) * 0.55f;
        public float FurnitureScale => 1f + (HouseScale - 1f) * 0.25f;
        public float Floor2Y => RealFloor2 * HouseHeightScale;
        public readonly List<LanternClue> Clues = new List<LanternClue>();
        public readonly List<Vector3> HouseSpawns = new List<Vector3>();
        public Bounds HouseBounds { get; private set; }

        private Material hWall, hWallUp, hFloor, hFloorUp, hCeil, hWood, hFabric, hWhite, hDark, hStair, hRail, hWindow;

        /// <summary>平面座標（實際公尺）→ 世界座標。y 以實際公尺計，乘上高度倍率。</summary>
        private Vector3 HP(float x, float y, float z) => HouseOrigin + new Vector3(x * HouseScale, y * HouseHeightScale, z * HouseScale);
        private Vector3 HS(float sx, float sy, float sz) => new Vector3(sx * HouseScale, sy * HouseHeightScale, sz * HouseScale);

        // ---------------- 建立／重建 ----------------

        /// <summary>以指定倍率建立（或重建）住宅。重建時舊住宅的物件全部移除，線索進度歸零。</summary>
        public void BuildHouse(float scale)
        {
            DestroyHouse();
            HouseScale = scale;
            HouseRoot = new GameObject("LanternHouse").transform;
            HouseRoot.SetParent(Root, false);
            into = HouseRoot;
            try
            {
                HouseMaterials();
                HouseShell();
                HouseUpper();
                HouseStairs();
                HouseFurniture();
                HouseClueSet();
                HouseLights();
            }
            finally { into = null; }
            HouseBounds = new Bounds(HP(RealWidth / 2f, 1.5f, RealDepth / 2f), new Vector3(RealWidth * HouseScale + 2f, 8f, RealDepth * HouseScale + 2f));
        }

        public void DestroyHouse()
        {
            if (HouseRoot == null) return;
            var root = HouseRoot;
            Tetherables.RemoveAll(t => t == null || t.transform.IsChildOf(root));
            Restorables.RemoveAll(t => t == null || t.transform.IsChildOf(root));
            Blockers.RemoveAll(t => t == null || t.transform.IsChildOf(root));
            Spots.RemoveAll(t => t == null || t.transform.IsChildOf(root));
            AnomalyLights.RemoveAll(l => l == null || l.transform.IsChildOf(root));
            RealOnly.RemoveAll(r => r == null || r.transform.IsChildOf(root));
            AnomalyOnly.RemoveAll(r => r == null || r.transform.IsChildOf(root));
            Dual.RemoveAll(r => r == null || r.transform.IsChildOf(root));
            var drop = new List<string>();
            foreach (var kv in Marks) if (kv.Value == null || kv.Value.IsChildOf(root)) drop.Add(kv.Key);
            foreach (var k in drop) Marks.Remove(k);
            Clues.Clear();
            HouseSpawns.Clear();
            ArenaArt.Kill(root.gameObject);
            HouseRoot = null;
        }

        private void HouseMaterials()
        {
            if (hWall != null) return;
            var grid = LanternReveal.Grid;
            hWall = Mat("LH_Wall", Mode.Dual, new Color(0.84f, 0.78f, 0.68f), new Color(0.3f, 0.36f, 0.35f), grid, grid, 0.15f, 0.4f, 0.012f, true);
            hWallUp = Mat("LH_WallUp", Mode.Dual, new Color(0.78f, 0.8f, 0.74f), new Color(0.28f, 0.3f, 0.36f), grid, grid, 0.15f, 0.4f, 0.012f, true);
            hFloor = Mat("LH_Floor", Mode.Dual, new Color(0.55f, 0.4f, 0.27f), new Color(0.26f, 0.28f, 0.3f), grid, grid, 0.35f, 0.6f, 0.006f, true);
            hFloorUp = Mat("LH_FloorUp", Mode.Dual, new Color(0.6f, 0.5f, 0.42f), new Color(0.27f, 0.27f, 0.32f), grid, grid, 0.3f, 0.6f, 0.006f, true);
            hCeil = Mat("LH_Ceiling", Mode.Dual, new Color(0.9f, 0.88f, 0.84f), new Color(0.26f, 0.3f, 0.32f), grid, grid, 0.1f, 0.3f, 0.015f, true);
            hWood = Mat("LH_Wood", Mode.Dual, new Color(0.42f, 0.27f, 0.16f), new Color(0.2f, 0.22f, 0.23f), null, null, 0.4f, 0.5f, 0.01f);
            hFabric = Mat("LH_Fabric", Mode.Dual, new Color(0.52f, 0.2f, 0.18f), new Color(0.28f, 0.32f, 0.36f), null, null, 0.08f, 0.3f, 0.02f);
            hWhite = Mat("LH_White", Mode.Dual, new Color(0.92f, 0.91f, 0.88f), new Color(0.6f, 0.66f, 0.66f), null, null, 0.5f, 0.5f, 0.006f);
            hDark = Mat("LH_Dark", Mode.Dual, new Color(0.18f, 0.17f, 0.17f), new Color(0.12f, 0.13f, 0.14f), null, null, 0.4f, 0.5f, 0.006f);
            hStair = Mat("LH_Stair", Mode.Dual, new Color(0.48f, 0.33f, 0.2f), new Color(0.22f, 0.24f, 0.26f), grid, grid, 0.35f, 0.5f, 0.006f, true);
            hRail = Mat("LH_Rail", Mode.Dual, new Color(0.35f, 0.22f, 0.12f), new Color(0.18f, 0.2f, 0.2f), null, null, 0.5f, 0.6f, 0.004f);
            hWindow = Mat("LH_Window", Mode.Dual, new Color(0.15f, 0.18f, 0.22f), new Color(0.3f, 0.45f, 0.55f), null, null, 0.9f, 0.9f, 0f);
            hWindow.SetColor("_EmissionColor", new Color(0.25f, 0.3f, 0.45f));
            hWindow.SetColor("_AnomalyEmission", new Color(0.12f, 0.2f, 0.28f));
        }

        // ---------------- 牆、地板、天花 ----------------

        private const float ExtT = 0.25f, IntT = 0.12f;

        /// <summary>沿 z 的牆（x 固定，實際公尺），gaps 為門洞（z0, z1）；y0／h 以實際公尺計。</summary>
        private void HWallZ(float x, float z0, float z1, float y0, float h, float t, Material m, float doorH, params Vector2[] gaps)
        {
            float cur = z0;
            foreach (var g in gaps)
            {
                if (g.x > cur) Box("Wall", HP(x, y0 + h / 2f, (cur + g.x) / 2f), new Vector3(t, h * HouseHeightScale, (g.x - cur) * HouseScale), m);
                Box("Lintel", HP(x, y0 + (doorH + h) / 2f, (g.x + g.y) / 2f), new Vector3(t, (h - doorH) * HouseHeightScale, (g.y - g.x) * HouseScale), m);
                cur = g.y;
            }
            if (z1 > cur) Box("Wall", HP(x, y0 + h / 2f, (cur + z1) / 2f), new Vector3(t, h * HouseHeightScale, (z1 - cur) * HouseScale), m);
        }

        private void HWallX(float z, float x0, float x1, float y0, float h, float t, Material m, float doorH, params Vector2[] gaps)
        {
            float cur = x0;
            foreach (var g in gaps)
            {
                if (g.x > cur) Box("Wall", HP((cur + g.x) / 2f, y0 + h / 2f, z), new Vector3((g.x - cur) * HouseScale, h * HouseHeightScale, t), m);
                Box("Lintel", HP((g.x + g.y) / 2f, y0 + (doorH + h) / 2f, z), new Vector3((g.y - g.x) * HouseScale, (h - doorH) * HouseHeightScale, t), m);
                cur = g.y;
            }
            if (x1 > cur) Box("Wall", HP((cur + x1) / 2f, y0 + h / 2f, z), new Vector3((x1 - cur) * HouseScale, h * HouseHeightScale, t), m);
        }

        private void Slab(string name, float x0, float x1, float z0, float z1, float y, float thick, Material m)
        {
            Box(name, HP((x0 + x1) / 2f, y, (z0 + z1) / 2f) + Vector3.down * thick / 2f, new Vector3((x1 - x0) * HouseScale, thick, (z1 - z0) * HouseScale), m);
        }

        private void Window(float x, float z, float w, bool alongX, float y0)
        {
            var size = alongX ? new Vector3(w * HouseScale, 1.3f * HouseHeightScale, 0.03f) : new Vector3(0.03f, 1.3f * HouseHeightScale, w * HouseScale);
            Box("Window", HP(x, y0 + 1.55f, z), size, hWindow, false);
        }

        private void HouseShell()
        {
            const float D = 2.1f, H1 = RealGroundCeiling;
            // 一樓地板（含側院）。
            Slab("HouseFloor", 0f, RealWidth, 0f, RealDepth, 0f, 0.2f, hFloor);
            // 外牆：前牆（玄關大門 x 3.85–4.65）、左右分戶牆、後段（廚房／後廚旁是側院）。
            HWallX(0f, 0f, RealWidth, 0f, RealFloor2 + RealUpperHeight, ExtT, hWall, D, new Vector2(3.85f, 4.65f));
            HWallZ(0f, 0f, RealDepth, 0f, RealFloor2 + RealUpperHeight, ExtT, hWall, D);
            HWallZ(RealWidth, 0f, 7.98f, 0f, RealFloor2 + RealUpperHeight, ExtT, hWall, D);
            HWallX(7.98f, 3.72f, RealWidth, 0f, H1, ExtT, hWall, D);
            HWallZ(2.9f, 7.98f, RealDepth, 0f, H1, ExtT, hWall, D, new Vector2(12.0f, 12.85f));
            HWallX(RealDepth, 0f, 2.9f, 0f, H1, ExtT, hWall, D);
            // 側院圍牆（矮、露天）。
            HWallZ(RealWidth, 7.98f, RealDepth, 0f, 1.6f, 0.2f, hWood, 1.6f);
            HWallX(RealDepth, 2.9f, RealWidth, 0f, 1.6f, 0.2f, hWood, 1.6f);
            Window(1.8f, 0.14f, 1.4f, true, 0f);
            Window(1.8f, 0.14f, 1.6f, true, RealFloor2);
            // 內牆：玄關／客廳（客廳門 z 2.3–3.3）、客廳／餐廳（雙開口 x 0.9–2.7）、餐廳／廚房（x 1.0–1.95）、廚房／後廚（x 1.0–1.9）。
            HWallZ(3.72f, 0f, 7.98f, 0f, H1, IntT, hWall, D, new Vector2(2.3f, 3.3f));
            HWallX(4.26f, 0f, 3.72f, 0f, H1, IntT, hWall, D, new Vector2(0.9f, 2.7f));
            HWallX(7.98f, 0f, 3.72f, 0f, H1, IntT, hWall, D, new Vector2(1.0f, 1.95f));
            HWallX(11.5f, 0f, 2.9f, 0f, H1, IntT, hWall, D, new Vector2(1.0f, 1.9f));
            // 一樓天花（＝二樓樓板底），樓梯井 x 3.72–4.77、z 3.95–7.7 開口。
            Slab("GroundCeiling", 0f, 3.72f, 0f, 7.98f, RealFloor2, 0.25f, hCeil);
            Slab("GroundCeiling", 3.72f, RealWidth, 0f, 3.95f, RealFloor2, 0.25f, hCeil);
            Slab("GroundCeiling", 0f, 3.72f, 7.98f, 10.6f, RealFloor2, 0.25f, hCeil);
            Slab("GroundCeiling", 0f, 2.9f, 10.6f, RealDepth, H1 + 0.05f, 0.1f, hCeil);
            HouseSpawns.Add(HP(4.25f, 0f, 1.2f));
            HouseSpawns.Add(HP(0.9f, 0f, 7.0f));
            HouseSpawns.Add(HP(1.2f, 0f, 10.6f));
            HouseSpawns.Add(HP(1.4f, 0f, 13.0f));
            HouseSpawns.Add(HP(3.8f, 0f, 11.5f));
            Mark("HouseEntry", HP(4.25f, 0f, 0.7f) + Vector3.up * 0.05f, HP(4.25f, 1.4f, 4f));
            Mark("HouseLiving", HP(3.3f, 0f, 2.8f) + Vector3.up * 0.05f, HP(0.3f, 1.2f, 2.1f));
            Mark("HouseDining", HP(1.8f, 0f, 4.7f) + Vector3.up * 0.05f, HP(2.0f, 0.8f, 6.6f));
            Mark("HouseKitchen", HP(1.5f, 0f, 8.4f) + Vector3.up * 0.05f, HP(2.2f, 0.8f, 11.0f));
            Mark("HouseScullery", HP(1.4f, 0f, 11.9f) + Vector3.up * 0.05f, HP(1.6f, 0.6f, 13.2f));
        }

        private void HouseUpper()
        {
            float y = RealFloor2, h = RealUpperHeight, D = 2.05f;
            // 二樓樓板：樓梯井開口 x 3.72–4.77、z 3.95–7.7（沿井有欄杆，旁邊是走廊）。
            Slab("UpperFloor", 0f, 3.72f, 0f, 10.6f, y + 0.02f, 0.06f, hFloorUp);
            Slab("UpperFloor", 3.72f, RealWidth, 0f, 3.95f, y + 0.02f, 0.06f, hFloorUp);
            Slab("UpperFloor", 3.72f, RealWidth, 7.7f, 7.98f, y + 0.02f, 0.06f, hFloorUp);
            Slab("UpperCeiling", 0f, RealWidth, 0f, 7.98f, y + h + 0.1f, 0.1f, hCeil);
            Slab("UpperCeiling", 0f, 3.72f, 7.98f, 10.6f, y + h + 0.1f, 0.1f, hCeil);
            // 母親臥室（前、全寬）門在走廊端 x 2.75–3.6。
            HWallX(3.4f, 0f, RealWidth, y, h, IntT, hWallUp, D, new Vector2(2.75f, 3.6f));
            // 艾琳的房間 x 0–2.6、z 3.4–7.98，門 z 5.2–6.05。
            HWallZ(2.6f, 3.4f, 7.98f, y, h, IntT, hWallUp, D, new Vector2(5.2f, 6.05f));
            // 書房 x 0–3.72、z 7.98–10.6，門在走廊盡頭 x 2.7–3.6。
            HWallX(7.98f, 0f, 3.72f, y, h, IntT, hWallUp, D, new Vector2(2.7f, 3.6f));
            HWallX(7.98f, 3.72f, RealWidth, y, h, ExtT, hWallUp, D);
            HWallZ(3.72f, 7.98f, 10.6f, y, h, ExtT, hWallUp, D);
            HWallX(10.6f, 0f, 3.72f, y, h, ExtT, hWallUp, D);
            // 樓梯井欄杆（走廊旁，x 3.72，z 3.95–7.2；頂端留開口接走廊）。
            Box("Rail", HP(3.72f, y + 0.95f, 5.575f), new Vector3(0.06f, 0.06f, 3.25f * HouseScale), hRail, false);
            Box("RailWall", HP(3.72f, y + 0.47f, 5.575f), new Vector3(0.05f, 0.94f, 3.25f * HouseScale), hRail);
            Box("Rail", HP(4.245f, y + 0.95f, 3.95f), new Vector3(1.05f * HouseScale, 0.06f, 0.06f), hRail, false);
            Box("RailWall", HP(4.245f, y + 0.47f, 3.95f), new Vector3(1.05f * HouseScale, 0.94f, 0.05f), hRail);
            HouseSpawns.Add(HP(3.15f, y, 4.3f));
            HouseSpawns.Add(HP(1.4f, y, 4.4f));
            HouseSpawns.Add(HP(1.0f, y, 9.4f));
            HouseSpawns.Add(HP(3.4f, y, 1.6f));
            Mark("HouseUpper", HP(3.15f, y, 7.5f) + Vector3.up * 0.05f, HP(3.15f, y + 1.3f, 4f));
            Mark("HouseEleanor", HP(2.2f, y, 5.6f) + Vector3.up * 0.05f, HP(0.6f, y + 1.0f, 7.1f));
            Mark("HouseStudy", HP(3.1f, y, 8.4f) + Vector3.up * 0.05f, HP(1.0f, y + 0.8f, 9.6f));
        }

        private void HouseStairs()
        {
            // 樓梯：在玄關內沿分戶牆，從 z 3.95（一樓）往後升到 z 7.7（二樓）。
            // 碰撞用一塊斜板（可直接走上去），外觀是一階一階的踏板。
            float z0 = 3.95f, z1 = 7.7f, x0 = 3.72f + IntT / 2f / HouseScale, x1 = RealWidth - ExtT / 2f / HouseScale;
            var a = HP((x0 + x1) / 2f, 0f, z0);
            var b = HP((x0 + x1) / 2f, RealFloor2, z1);
            var mid = (a + b) / 2f;
            var dir = b - a;
            float len = dir.magnitude;
            var ramp = Box("StairRamp", mid + Vector3.down * 0.04f, new Vector3((x1 - x0) * HouseScale, 0.08f, len), hStair, true, Quaternion.LookRotation(dir.normalized).eulerAngles);
            ramp.GetComponent<Renderer>().enabled = false;
            int steps = 16;
            for (int i = 0; i < steps; i++)
            {
                float k0 = i / (float)steps, k1 = (i + 1) / (float)steps;
                var p = Vector3.Lerp(a, b, (k0 + k1) / 2f);
                float rise = (b.y - a.y) / steps;
                float run = (b.z - a.z) / steps;
                Box("Step", new Vector3(p.x, a.y + rise * (i + 0.5f), p.z), new Vector3((x1 - x0) * HouseScale, rise, run), hStair, false);
            }
            Box("StairStringer", new Vector3(HP(x0, 0f, 0f).x, mid.y, mid.z), new Vector3(0.06f, 0.9f, len), hRail, false, Quaternion.LookRotation(dir.normalized).eulerAngles);
            Box("Handrail", new Vector3(HP(x0, 0f, 0f).x, mid.y + 0.9f, mid.z), new Vector3(0.05f, 0.05f, len), hRail, false, Quaternion.LookRotation(dir.normalized).eulerAngles);
        }

        private Transform Fur(string name, float x, float z, float y, float sx, float sy, float sz, Material m, bool collider = true, float yaw = 0f)
        {
            float f = FurnitureScale;
            return Box(name, HP(x, 0f, z) + Vector3.up * (y * HouseHeightScale + sy * f / 2f), new Vector3(sx * f, sy * f, sz * f), m, collider, new Vector3(0f, yaw, 0f));
        }

        private void HouseFurniture()
        {
            float f = FurnitureScale;
            // 玄關：衣帽架、鞋櫃、鏡子。
            Fur("ShoeCabinet", 4.55f, 1.6f, 0f, 0.3f, 0.9f, 0.8f, hWood);
            Fur("HallMirror", 4.72f, 1.6f, 1.3f, 0.03f, 0.7f, 0.5f, hWhite, false);
            // 客廳：壁爐（分戶牆中段）、沙發、扶手椅、茶几、書櫃、電視櫃。
            Fur("ChimneyBreast", 0.18f, 2.1f, 0f, 0.36f, 2.75f, 1.5f, hWall);
            Fur("Mantel", 0.42f, 2.1f, 1.1f, 0.2f, 0.08f, 1.6f, hWood, false);
            Fur("Hearth", 0.6f, 2.1f, 0f, 0.5f, 0.05f, 1.4f, hDark, false);
            Fur("Sofa", 2.75f, 2.1f, 0f, 0.9f, 0.85f, 2.0f, hFabric);
            Fur("Armchair", 1.0f, 3.6f, 0f, 0.8f, 0.9f, 0.8f, hFabric, true, 30f);
            Fur("CoffeeTable", 1.65f, 2.1f, 0f, 0.55f, 0.42f, 1.0f, hWood);
            Fur("Bookcase", 0.2f, 0.6f, 0f, 0.35f, 1.9f, 0.9f, hWood);
            Fur("TVCabinet", 3.3f, 0.35f, 0f, 0.9f, 0.55f, 0.45f, hDark);
            // 餐廳：餐桌、三張椅子（第四張倒在地上＝線索）、餐邊櫃。
            Fur("DiningTable", 1.8f, 6.1f, 0f, 0.9f, 0.75f, 1.6f, hWood);
            foreach (var p in new[] { new Vector3(1.05f, 5.6f, 90f), new Vector3(1.05f, 6.6f, 90f), new Vector3(2.55f, 5.6f, -90f) })
                Fur("DiningChair", p.x, p.y, 0f, 0.44f, 0.9f, 0.44f, hWood, true, p.z);
            Fur("Sideboard", 0.25f, 6.1f, 0f, 0.45f, 0.85f, 1.4f, hWood);
            // 廚房：流理台、爐具、冰箱、小桌、高櫃（Issue #7）。
            Fur("Counter", 0.32f, 9.7f, 0f, 0.6f, 0.9f, 2.6f, hWood);
            Fur("Stove", 0.32f, 8.4f, 0f, 0.6f, 0.9f, 0.6f, hDark);
            Fur("Fridge", 2.5f, 8.45f, 0f, 0.7f, 1.8f, 0.7f, hWhite);
            Fur("KitchenTable", 1.6f, 9.7f, 0f, 0.8f, 0.75f, 0.8f, hWood);
            Fur("TallCabinet", 2.55f, 11.1f, 0f, 0.6f, 2.0f, 0.45f, hWood);
            // 後廚：洗衣機、水槽、層架、木箱。
            Fur("Washer", 0.4f, 12.0f, 0f, 0.6f, 0.85f, 0.6f, hWhite);
            Fur("Sink", 0.35f, 12.9f, 0f, 0.55f, 0.9f, 0.8f, hWhite);
            Fur("Shelf", 2.6f, 13.4f, 0f, 0.5f, 1.8f, 0.35f, hWood, true, 0f);
            for (int i = 0; i < 2; i++)
            {
                var crate = Tether("HouseCrate", "木箱", HP(2.2f, 0f, 12.2f + i * 0.7f), new Vector3(0.42f, 0.42f, 0.42f), 2.5f);
                Box("CrateBody", crate.transform.position + Vector3.up * 0.21f, new Vector3(0.42f, 0.42f, 0.42f), partition, false, null, crate.transform);
            }
            // 二樓：母親臥室、艾琳的房間、書房。
            float y = RealFloor2;
            Fur("MotherBed", 1.2f, 1.6f, y, 1.5f, 0.55f, 2.0f, hFabric, true, 90f);
            Fur("Wardrobe", 4.4f, 0.5f, y, 0.6f, 2.0f, 1.2f, hWood, true, 90f);
            Fur("Dresser", 0.3f, 3.0f, y, 0.45f, 0.85f, 1.0f, hWood);
            Fur("EleanorBed", 0.55f, 4.6f, y, 0.95f, 0.5f, 1.95f, hFabric);
            Fur("Bookshelf", 2.35f, 7.6f, y, 0.4f, 1.8f, 0.9f, hWood, true, 90f);
            Fur("Boxes", 2.2f, 4.0f, y, 0.5f, 0.5f, 0.5f, partition);
            Fur("StudyDesk", 0.4f, 9.3f, y, 0.6f, 0.76f, 1.3f, hWood);
            Fur("StudyShelves", 3.35f, 9.4f, y, 0.4f, 2.0f, 1.6f, hWood);
            Mark("HouseMother", HP(3.2f, y, 3.0f) + Vector3.up * 0.05f, HP(1.0f, y + 1.0f, 1.2f));
        }

        private void HouseClueSet()
        {
            float y2 = RealFloor2;
            // 1. 客廳壁爐上的全家福：異域裡少了一個人。聚光照著它，真相就被固定下來。
            var photoMat = Mat("LH_Photo", Mode.Dual, Color.white, Color.white, LanternReveal.Photo(true), LanternReveal.Photo(false), 0.4f, 0.4f, 0.002f);
            var photoPos = HP(0.48f, 1.18f, 2.1f) + Vector3.up * 0.18f * FurnitureScale;
            Box("PhotoFrame", photoPos + Vector3.left * 0.01f, new Vector3(0.03f, 0.3f * FurnitureScale, 0.4f * FurnitureScale), hDark, false);
            var photo = Quad("FamilyPhoto", photoPos + Vector3.right * 0.02f, new Vector3(0.36f * FurnitureScale, 0.27f * FurnitureScale, 1f), new Vector3(0f, -90f, 0f), photoMat);
            var pc = photo.gameObject.AddComponent<BoxCollider>();
            pc.size = new Vector3(1f, 1f, 0.05f);
            AddClue(photo.gameObject, LanternClue.Kind.Focus, "photo", "壁爐上的全家福");

            // 2. 餐廳：第四張椅子倒在地上（艾琳的位置）。復原它。
            var chair = new GameObject("EleanorChair").transform;
            chair.SetParent(Into, false);
            var cc = chair.gameObject.AddComponent<BoxCollider>();
            cc.center = new Vector3(0f, 0.45f, 0f);
            cc.size = new Vector3(0.44f, 0.9f, 0.44f);
            chair.position = Vector3.zero;
            var saved = into;
            into = chair;
            ChairParts(chair, hWood, hFabric);
            into = saved;
            chair.localScale = Vector3.one * FurnitureScale;
            var cr = chair.gameObject.AddComponent<LanternRestorable>();
            cr.Label = "倒下的椅子";
            cr.AnchorRadius = 1.6f;
            cr.Init(HP(2.75f, 0f, 6.9f) + Vector3.up * 0.2f, Quaternion.Euler(0f, 60f, 85f), HP(2.55f, 0f, 6.6f), Quaternion.Euler(0f, -90f, 0f));
            Restorables.Add(cr);
            var c2 = AddClue(chair.gameObject, LanternClue.Kind.Restore, "chair", "艾琳的椅子");
            c2.Restorable = cr;

            // 3. 廚房：藍坐墊椅子在後廚，要搬到高櫃前的椅腳凹痕上（Issue #7）。
            var dentMat = Mat("LH_Dent", Mode.Dual, new Color(0.18f, 0.12f, 0.08f), new Color(0.1f, 0.11f, 0.12f), null, null, 0.1f, 0.2f, 0f);
            var spotGo = new GameObject("HouseChairDents");
            spotGo.transform.SetParent(Into, false);
            spotGo.transform.position = HP(2.0f, 0f, 10.75f);
            foreach (var p in new[] { new Vector2(-0.19f, -0.18f), new Vector2(0.19f, -0.18f), new Vector2(-0.19f, 0.18f), new Vector2(0.19f, 0.18f) })
                Box("Dent", spotGo.transform.position + new Vector3(p.x, 0.003f, p.y) * FurnitureScale, new Vector3(0.06f, 0.006f, 0.06f), dentMat, false);
            var glow = spotGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.85f, 0.6f);
            glow.range = 2f;
            glow.intensity = 0.8f;
            glow.enabled = false;
            var cushion = Mat("LH_BlueCushion", Mode.Dual, new Color(0.2f, 0.32f, 0.62f), new Color(0.22f, 0.26f, 0.3f), null, null, 0.1f, 0.3f, 0.01f);
            var blue = Tether("HouseBlueChair", "藍坐墊椅子", HP(1.6f, 0f, 12.8f), new Vector3(0.46f, 0.92f, 0.44f), 4f);
            ChairParts(blue.transform, hWood, cushion);
            var spot = spotGo.AddComponent<LanternPlacementSpot>();
            spot.Expected = blue;
            spot.Glow = glow;
            spot.Tolerance = 0.35f;
            Spots.Add(spot);
            var c3 = AddClue(spotGo, LanternClue.Kind.Place, "dents", "椅腳凹痕");
            c3.Spot = spot;

            // 4. 艾琳的房間：書桌角落被一面只存在於異域的牆封住；牆後是她的研究筆記。
            var fake = Mat("LH_FakeWall", Mode.AnomalyOnly, Color.white, new Color(0.28f, 0.3f, 0.36f), LanternReveal.Grid, LanternReveal.Grid, 0.2f, 0.5f, 0.02f);
            fake.SetFloat("_WorldUV", 1f);
            var fw = Box("AnomalyWall", HP(0.65f, y2 + 1.1f, 6.55f), new Vector3(1.3f * HouseScale, 2.2f * HouseHeightScale, 0.12f), fake, true);
            var blocker = fw.gameObject.AddComponent<LanternAnomalyBlocker>();
            blocker.Solid = fw.GetComponent<Collider>();
            blocker.Probe = HP(0.65f, y2 + 1.0f, 6.55f);
            Blockers.Add(blocker);
            HWallZ(1.3f, 6.55f, 7.98f, y2, RealUpperHeight, IntT, hWallUp, 2.05f);
            Fur("EleanorDesk", 0.4f, 7.35f, y2, 0.6f, 0.76f, 1.1f, hWood);
            var nbMat = Mat("LH_Notebook", Mode.Dual, new Color(0.55f, 0.2f, 0.15f), new Color(0.25f, 0.2f, 0.2f), null, null, 0.3f, 0.3f, 0.002f);
            nbMat.SetColor("_EmissionColor", new Color(0.25f, 0.12f, 0.05f));
            var nb = Box("ResearchNotebook", HP(0.45f, y2, 7.35f) + Vector3.up * (0.76f * FurnitureScale + 0.02f), new Vector3(0.22f, 0.04f, 0.3f), nbMat, true, new Vector3(0f, 20f, 0f));
            var rd = nb.gameObject.AddComponent<LanternReadable>();
            rd.Title = "艾琳的研究筆記";
            rd.Text = "（暫定文字，待正式劇本）\n如果你看到這一頁，代表你也走進來了。\n它們在黑暗裡看不清楚——只有被光照到的地方，它們才會現出原形。\n每當有東西被放回原本的樣子，它們就會出現。它們不想讓這裡變回真的。\n別一直站在暗處。";
            var c4 = AddClue(nb.gameObject, LanternClue.Kind.Read, "notebook", "艾琳的研究筆記");
            c4.Readable = rd;

            // 5. 書房：倒下的地圖板（16 / 17 / 18 號的位置）。復原它。
            var board = Box("MapBoard", HP(1.6f, y2 + 0.9f, 10.45f), new Vector3(1.4f * FurnitureScale, 1.0f * FurnitureScale, 0.06f), hWood, true);
            var mapMat = Mat("LH_Map", Mode.Dual, Color.white, Color.white, LanternReveal.Number(17, new Color32(230, 222, 200, 255), new Color32(60, 40, 30, 255)), LanternReveal.Number(16, new Color32(200, 210, 210, 255), new Color32(30, 50, 60, 255)), 0.3f, 0.3f, 0.003f);
            var mq = ArenaArt.Prim(PrimitiveType.Quad, board, Vector3.zero, Vector3.one, mapMat, null, "MapFace", false, false);
            mq.localPosition = new Vector3(0f, 0f, -0.52f);
            mq.localScale = new Vector3(0.9f, 0.9f, 1f);
            Register(mq.GetComponent<Renderer>(), mapMat);
            var br = board.gameObject.AddComponent<LanternRestorable>();
            br.Label = "倒下的地圖板";
            br.AnchorRadius = 1.6f;
            br.Init(HP(1.4f, y2 + 0.08f, 9.6f), Quaternion.Euler(-88f, 15f, 0f), HP(1.6f, y2 + 0.9f, 10.45f), Quaternion.identity);
            Restorables.Add(br);
            var c5 = AddClue(board.gameObject, LanternClue.Kind.Restore, "map", "倒下的地圖板");
            c5.Restorable = br;
        }

        private LanternClue AddClue(GameObject go, LanternClue.Kind kind, string id, string label)
        {
            var c = go.AddComponent<LanternClue>();
            c.Type = kind;
            c.Id = id;
            c.Label = label;
            Clues.Add(c);
            return c;
        }

        private void HouseLights()
        {
            var bulb = Mat("LH_Bulb", Mode.Dual, new Color(0.3f, 0.3f, 0.28f), new Color(0.7f, 0.85f, 1f), null, null, 0.8f, 0.8f, 0f);
            bulb.SetColor("_AnomalyEmission", new Color(1.6f, 2.2f, 3f));
            void Lamp(string name, float x, float y, float z, Color c, float intensity, float range)
            {
                Ball(name + "Bulb", HP(x, y, z) + Vector3.up * 0.12f, new Vector3(0.12f, 0.14f, 0.12f), bulb);
                AddLight(name, LightType.Point, HP(x, y, z), c, intensity * 1.7f, range * HouseScale * 1.15f, true, LightShadows.Soft);
            }
            Lamp("HallLight", 4.25f, 2.55f, 1.6f, new Color(0.62f, 0.78f, 1f), 0.9f, 4.5f);
            Lamp("LivingLight", 1.8f, 2.55f, 2.1f, new Color(0.7f, 0.6f, 1f), 1.0f, 5f);
            Lamp("DiningLight", 1.8f, 2.55f, 6.1f, new Color(0.62f, 0.8f, 1f), 0.9f, 5f);
            Lamp("KitchenLight", 1.45f, 2.55f, 9.7f, new Color(0.7f, 0.95f, 0.8f), 0.9f, 5f);
            Lamp("SculleryLight", 1.45f, 2.55f, 12.6f, new Color(0.6f, 0.9f, 0.8f), 0.6f, 3.5f);
            Lamp("UpperLight", 3.15f, RealFloor2 + 2.35f, 5.6f, new Color(0.6f, 0.7f, 1f), 0.7f, 4.5f);
            Lamp("EleanorLight", 1.3f, RealFloor2 + 2.35f, 5.4f, new Color(0.7f, 0.6f, 1f), 0.6f, 4f);
            Lamp("StudyLight", 1.8f, RealFloor2 + 2.35f, 9.3f, new Color(0.6f, 0.8f, 1f), 0.6f, 4f);
            Lamp("MotherLight", 2.4f, RealFloor2 + 2.35f, 1.7f, new Color(0.65f, 0.7f, 1f), 0.6f, 4.5f);
            AddLight("YardMoon", LightType.Spot, HP(3.8f, 6f, 10.8f), new Color(0.55f, 0.65f, 0.9f), 2.2f, 12f, true, LightShadows.Soft, 80f, Vector3.down);
        }
    }
}
