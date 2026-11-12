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
        /// <summary>給玩家的提示（右上角清單）。</summary>
        public string Hint = "";
        public LanternRestorable Restorable;
        public LanternPlacementSpot Spot;
        public LanternReadable Readable;
        public float FocusProgress { get; private set; }
        public bool Done { get; private set; }

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

        public void AddFocus(float amount) { if (!Done && Type == Kind.Focus) FocusProgress = Mathf.Clamp01(FocusProgress + amount); }
        public void ForceComplete() { Done = true; FocusProgress = 1f; }
    }

    /// <summary>可以閱讀的東西（線索用的「看完了」旗標；內容由 LanternInspectable 顯示）。</summary>
    public sealed class LanternReadable : MonoBehaviour
    {
        public string Title;
        [TextArea] public string Text;
        public bool Read { get; set; }
    }

    /// <summary>
    /// 第十七號門的住宅（灰盒＋CC0 材質與家具，體感比例）：沿用九區格局，建築放大（預設 1.4 倍）；家具只略微放大。
    /// 每個房間有不同的地板與牆面，門口有暫用房名牌；家具留出可以繞行的通道。
    /// 一開始沒電：修好玄關的電箱，整棟燈才會亮（異域反撲時仍會熄燈）。
    /// </summary>
    public sealed partial class LanternGraybox
    {
        public static readonly float[] HouseScales = { 1.25f, 1.4f, 1.6f };
        public const float HouseDefaultScale = 1.4f;
        public static readonly Vector3 HouseOrigin = new Vector3(60f, 0f, 0f);
        public const float RealWidth = 4.77f, RealDepth = 13.76f, RealFloor2 = 3.0f, RealUpperHeight = 2.55f, RealGroundCeiling = 2.75f;

        public Transform HouseRoot { get; private set; }
        public float HouseScale { get; private set; } = HouseDefaultScale;
        public float HouseHeightScale => 1f + (HouseScale - 1f) * 0.55f;
        public float FurnitureScale => 1f + (HouseScale - 1f) * 0.25f;
        public float Floor2Y => RealFloor2 * HouseHeightScale;
        public readonly List<LanternClue> Clues = new List<LanternClue>();
        public readonly List<Vector3> HouseSpawns = new List<Vector3>();
        public readonly List<LanternToggle> Toggles = new List<LanternToggle>();
        public readonly List<LanternInspectable> Inspectables = new List<LanternInspectable>();
        public Bounds HouseBounds { get; private set; }
        /// <summary>電箱修好了（整棟燈穩定亮起）。</summary>
        public bool HousePowered { get; set; }
        public LanternRestorable FuseBox { get; private set; }

        private Material hWall, hWallUp, hWallKitchen, hWallEleanor, hFloorHall, hFloorLiving, hFloorKitchen, hFloorScullery, hFloorUp, hCeil,
            hWood, hLightWood, hFabric, hRug, hLeather, hWhite, hDark, hStair, hRail, hWindow, hSign;
        private Font signFont;

        private Vector3 HP(float x, float y, float z) => HouseOrigin + new Vector3(x * HouseScale, y * HouseHeightScale, z * HouseScale);

        // ---------------- 建立／重建 ----------------

        public void BuildHouse(float scale)
        {
            DestroyHouse();
            HouseScale = scale;
            HousePowered = false;
            HouseRoot = new GameObject("LanternHouse").transform;
            HouseRoot.SetParent(Root, false);
            into = HouseRoot;
            try
            {
                HouseMaterials();
                HouseShell();
                HouseUpper();
                HouseStairs();
                HouseOutside();
                HouseFurniture();
                HouseSigns();
                HouseInteractions();
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
            Toggles.Clear();
            Inspectables.Clear();
            FuseBox = null;
            ArenaArt.Kill(root.gameObject);
            HouseRoot = null;
        }

        private Material PH(string name, string tex, string folder, float tile, Color tint, Color anom, float smooth = 0.8f, float sat = 0.2f, float wobble = 0.01f)
        {
            var m = LanternReveal.HasPH(tex, folder)
                ? LanternReveal.MakePH(name, tex, folder, tile, tint, anom, smooth, sat, wobble)
                : LanternReveal.Make(name, Mode.Dual, tint, anom, LanternReveal.Grid, LanternReveal.Grid, 0.3f, 0.4f, wobble);
            if (!LanternReveal.HasPH(tex, folder) && tile > 0f) m.SetFloat("_WorldUV", 1f);
            Materials.Add(m);
            return m;
        }

        private void HouseMaterials()
        {
            if (hWall != null) return;
            var anomCold = new Color(0.55f, 0.66f, 0.72f);
            hWall = PH("LH_Wall", "painted_plaster_wall", "Tex", 2.5f, new Color(0.95f, 0.88f, 0.76f), anomCold, 0.6f);
            hWallUp = PH("LH_WallUp", "plastered_wall_03", "Tex", 2.5f, new Color(0.86f, 0.9f, 0.82f), anomCold, 0.6f);
            hWallKitchen = PH("LH_WallKitchen", "long_white_tiles", "Tex", 1.2f, new Color(0.92f, 0.97f, 0.96f), anomCold, 0.9f);
            hWallEleanor = PH("LH_WallEleanor", "decrepit_wallpaper", "Tex", 1.6f, new Color(1f, 0.92f, 0.86f), anomCold, 0.5f);
            hFloorHall = PH("LH_FloorHall", "old_wooden_floor_02", "Tex", 2.2f, new Color(0.95f, 0.85f, 0.75f), anomCold * 0.7f, 0.7f);
            hFloorLiving = PH("LH_FloorLiving", "herringbone_parquet", "Tex", 1.6f, new Color(1f, 0.9f, 0.78f), anomCold * 0.7f, 0.8f);
            hFloorKitchen = PH("LH_FloorKitchen", "floor_tiles_06", "Tex", 1.2f, new Color(0.95f, 0.95f, 0.92f), anomCold * 0.8f, 0.9f);
            hFloorScullery = PH("LH_FloorScullery", "concrete_floor_worn_001", "Tex", 2.5f, new Color(0.85f, 0.83f, 0.8f), anomCold * 0.7f, 0.6f);
            hFloorUp = PH("LH_FloorUp", "plank_flooring_02", "Tex", 2.0f, new Color(0.95f, 0.86f, 0.76f), anomCold * 0.7f, 0.7f);
            hCeil = PH("LH_Ceiling", "plastered_wall_03", "Tex", 3f, new Color(0.95f, 0.94f, 0.9f), anomCold * 0.6f, 0.4f, 0.2f, 0.015f);
            hWood = PH("LH_Wood", "dark_wood", "Tex", 0f, new Color(0.9f, 0.82f, 0.75f), anomCold * 0.6f, 0.8f);
            hLightWood = PH("LH_LightWood", "fine_grained_wood", "Tex", 0f, new Color(1f, 0.92f, 0.82f), anomCold * 0.7f, 0.8f);
            hFabric = PH("LH_Fabric", "quatrefoil_jacquard_fabric", "Tex", 0f, new Color(0.95f, 0.8f, 0.75f), anomCold * 0.6f, 0.5f, 0.15f, 0.02f);
            hRug = PH("LH_Rug", "dirty_carpet", "Tex", 2.0f, new Color(1f, 0.9f, 0.85f), anomCold * 0.6f, 0.5f, 0.15f, 0.006f);
            hLeather = PH("LH_Leather", "brown_leather", "Tex", 0f, new Color(1f, 0.9f, 0.8f), anomCold * 0.6f, 0.8f);
            hWhite = Mat("LH_White", Mode.Dual, new Color(0.92f, 0.91f, 0.88f), new Color(0.6f, 0.66f, 0.66f), null, null, 0.5f, 0.5f, 0.006f);
            hDark = Mat("LH_Dark", Mode.Dual, new Color(0.18f, 0.17f, 0.17f), new Color(0.12f, 0.13f, 0.14f), null, null, 0.4f, 0.5f, 0.006f);
            hStair = hFloorHall;
            hRail = hWood;
            hWindow = Mat("LH_Window", Mode.Dual, new Color(0.15f, 0.18f, 0.22f), new Color(0.3f, 0.45f, 0.55f), null, null, 0.9f, 0.9f, 0f);
            hWindow.SetColor("_EmissionColor", new Color(0.25f, 0.3f, 0.45f));
            hWindow.SetColor("_AnomalyEmission", new Color(0.12f, 0.2f, 0.28f));
            signFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hSign = new Material(signFont.material) { name = "LH_SignText" };
            hSign.color = new Color(0.95f, 0.9f, 0.75f);
            Materials.Add(hSign);
        }

        // ---------------- 牆、地板、天花 ----------------

        private const float ExtT = 0.25f, IntT = 0.12f;

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
            // 一樓地板：每個房間不同（玄關／餐廳舊木地板、客廳人字拼、廚房磁磚、後廚與側院水泥）。
            Slab("FloorHall", 3.72f, RealWidth, 0f, 7.98f, 0f, 0.2f, hFloorHall);
            Slab("FloorLiving", 0f, 3.72f, 0f, 4.26f, 0f, 0.2f, hFloorLiving);
            Slab("FloorDining", 0f, 3.72f, 4.26f, 7.98f, 0f, 0.2f, hFloorHall);
            Slab("FloorKitchen", 0f, 2.9f, 7.98f, 11.5f, 0f, 0.2f, hFloorKitchen);
            Slab("FloorScullery", 0f, 2.9f, 11.5f, RealDepth, 0f, 0.2f, hFloorScullery);
            Slab("FloorYard", 2.9f, RealWidth, 7.98f, RealDepth, 0f, 0.2f, hFloorScullery);
            HWallX(0f, 0f, RealWidth, 0f, RealFloor2 + RealUpperHeight, ExtT, hWall, D, new Vector2(3.85f, 4.65f));
            HWallZ(0f, 0f, 7.98f, 0f, RealFloor2 + RealUpperHeight, ExtT, hWall, D);
            HWallZ(0f, 7.98f, RealDepth, 0f, RealFloor2 + RealUpperHeight, ExtT, hWallKitchen, D);
            HWallZ(RealWidth, 0f, 7.98f, 0f, RealFloor2 + RealUpperHeight, ExtT, hWall, D);
            HWallX(7.98f, 3.72f, RealWidth, 0f, H1, ExtT, hWall, D);
            HWallZ(2.9f, 7.98f, RealDepth, 0f, H1, ExtT, hWallKitchen, D, new Vector2(12.0f, 12.85f));
            HWallX(RealDepth, 0f, 2.9f, 0f, H1, ExtT, hWallKitchen, D);
            // 側院圍牆（加高；上方再有看不見的牆，東西丟不出去）。
            HWallZ(RealWidth, 7.98f, RealDepth, 0f, 2.0f, 0.2f, hWood, 2.0f);
            HWallX(RealDepth, 2.9f, RealWidth, 0f, 2.0f, 0.2f, hWood, 2.0f);
            Invisible(HP(RealWidth, 4f, (7.98f + RealDepth) / 2f), new Vector3(0.3f, 8f, (RealDepth - 7.98f) * HouseScale));
            Invisible(HP((2.9f + RealWidth) / 2f, 4f, RealDepth), new Vector3((RealWidth - 2.9f) * HouseScale, 8f, 0.3f));
            Window(1.8f, 0.14f, 1.4f, true, 0f);
            Window(1.8f, 0.14f, 1.6f, true, RealFloor2);
            HWallZ(3.72f, 0f, 7.98f, 0f, H1, IntT, hWall, D, new Vector2(2.3f, 3.3f));
            HWallX(4.26f, 0f, 3.72f, 0f, H1, IntT, hWall, D, new Vector2(0.9f, 2.7f));
            HWallX(7.98f, 0f, 3.72f, 0f, H1, IntT, hWallKitchen, D, new Vector2(1.0f, 1.95f));
            HWallX(11.5f, 0f, 2.9f, 0f, H1, IntT, hWallKitchen, D, new Vector2(1.0f, 1.9f));
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
            Mark("HouseLiving", HP(3.3f, 0f, 3.8f) + Vector3.up * 0.05f, HP(0.3f, 1.2f, 2.1f));
            Mark("HouseDining", HP(3.25f, 0f, 4.75f) + Vector3.up * 0.05f, HP(2.75f, 0.5f, 6.8f));
            Mark("HouseKitchen", HP(1.5f, 0f, 8.4f) + Vector3.up * 0.05f, HP(2.2f, 0.8f, 11.0f));
            Mark("HouseScullery", HP(1.4f, 0f, 11.9f) + Vector3.up * 0.05f, HP(1.5f, 0.6f, 12.4f));
        }

        private void Invisible(Vector3 center, Vector3 size)
        {
            var go = new GameObject("InvisibleWall");
            go.transform.SetParent(Into, false);
            go.transform.position = center;
            go.AddComponent<BoxCollider>().size = size;
        }

        private void HouseOutside()
        {
            // 房子四周的地面與邊界：東西丟出去也不會掉出世界。
            var c = HP(RealWidth / 2f, 0f, RealDepth / 2f);
            var size = new Vector3(RealWidth * HouseScale + 14f, 0.2f, RealDepth * HouseScale + 14f);
            Box("OutsideGround", c + Vector3.down * 0.12f, size, hFloorScullery);
            Invisible(c + new Vector3(size.x / 2f, 4f, 0f), new Vector3(0.3f, 8f, size.z));
            Invisible(c + new Vector3(-size.x / 2f, 4f, 0f), new Vector3(0.3f, 8f, size.z));
            Invisible(c + new Vector3(0f, 4f, size.z / 2f), new Vector3(size.x, 8f, 0.3f));
            Invisible(c + new Vector3(0f, 4f, -size.z / 2f), new Vector3(size.x, 8f, 0.3f));
        }

        private void HouseUpper()
        {
            float y = RealFloor2, h = RealUpperHeight, D = 2.05f;
            Slab("UpperFloor", 0f, 3.72f, 0f, 10.6f, y + 0.02f, 0.06f, hFloorUp);
            Slab("UpperFloor", 3.72f, RealWidth, 0f, 3.95f, y + 0.02f, 0.06f, hFloorUp);
            Slab("UpperFloor", 3.72f, RealWidth, 7.7f, 7.98f, y + 0.02f, 0.06f, hFloorUp);
            Slab("UpperCeiling", 0f, RealWidth, 0f, 7.98f, y + h + 0.1f, 0.1f, hCeil);
            Slab("UpperCeiling", 0f, 3.72f, 7.98f, 10.6f, y + h + 0.1f, 0.1f, hCeil);
            HWallX(3.4f, 0f, RealWidth, y, h, IntT, hWallUp, D, new Vector2(2.75f, 3.6f));
            HWallZ(2.6f, 3.4f, 7.98f, y, h, IntT, hWallEleanor, D, new Vector2(5.2f, 6.05f));
            HWallX(7.98f, 0f, 3.72f, y, h, IntT, hWallUp, D, new Vector2(2.7f, 3.6f));
            HWallX(7.98f, 3.72f, RealWidth, y, h, ExtT, hWallUp, D);
            HWallZ(3.72f, 7.98f, 10.6f, y, h, ExtT, hWallUp, D);
            HWallX(10.6f, 0f, 3.72f, y, h, ExtT, hWallUp, D);
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
            Mark("HouseMother", HP(3.2f, y, 3.0f) + Vector3.up * 0.05f, HP(1.0f, y + 1.0f, 1.2f));
        }

        private void HouseStairs()
        {
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
                var p = Vector3.Lerp(a, b, (i + 0.5f) / steps);
                float rise = (b.y - a.y) / steps;
                float run = (b.z - a.z) / steps;
                Box("Step", new Vector3(p.x, a.y + rise * (i + 0.5f), p.z), new Vector3((x1 - x0) * HouseScale, rise, run), hStair, false);
            }
            Box("StairStringer", new Vector3(HP(x0, 0f, 0f).x, mid.y, mid.z), new Vector3(0.06f, 0.9f, len), hRail, false, Quaternion.LookRotation(dir.normalized).eulerAngles);
            Box("Handrail", new Vector3(HP(x0, 0f, 0f).x, mid.y + 0.9f, mid.z), new Vector3(0.05f, 0.05f, len), hRail, false, Quaternion.LookRotation(dir.normalized).eulerAngles);
        }

        // ---------------- 家具 ----------------

        private Transform Fur(string name, float x, float z, float y, float sx, float sy, float sz, Material m, bool collider = true, float yaw = 0f)
        {
            float f = FurnitureScale;
            return Box(name, HP(x, 0f, z) + Vector3.up * (y * HouseHeightScale + sy * f / 2f), new Vector3(sx * f, sy * f, sz * f), m, collider, new Vector3(0f, yaw, 0f));
        }

        /// <summary>
        /// 放一件 CC0 家具模型：以真實高度（乘家具倍率）等比縮放、底部貼地、置中於 (x,z)；face＝正面朝向（度，0＝+z）。
        /// 材質改成 LanternReveal 雙狀態（真實＝模型自己的貼圖；異域＝去飽和偏冷）。找不到模型時退回方塊。
        /// </summary>
        private Transform Model(string name, string model, float x, float z, float y, float height, float face, bool collider, Material fallback, Vector3 fallbackSize)
        {
            var prefab = Resources.Load<GameObject>("PH/Models/" + model);
            if (prefab == null) return Fur(name, x, z, y, fallbackSize.x, fallbackSize.y, fallbackSize.z, fallback, collider, face);
            var root = new GameObject(name).transform;
            root.SetParent(Into, false);
            var inst = Object.Instantiate(prefab, root, false);
            inst.name = model;
            var rends = inst.GetComponentsInChildren<Renderer>(true);
            var b = new Bounds();
            bool any = false;
            foreach (var r in rends) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            if (!any || b.size.y < 1e-4f) { ArenaArt.Kill(root.gameObject); return Fur(name, x, z, y, fallbackSize.x, fallbackSize.y, fallbackSize.z, fallback, collider, face); }
            float s = height * FurnitureScale / b.size.y;
            inst.transform.localScale *= s;
            inst.transform.localPosition = new Vector3(-b.center.x * s, -b.min.y * s, -b.center.z * s);
            var mats = ModelMaterials(model, rends);
            foreach (var r in rends)
            {
                var arr = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < arr.Length; i++) arr[i] = mats[Mathf.Min(i, mats.Length - 1)];
                r.sharedMaterials = arr;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                Register(r, arr[0]);
            }
            if (collider)
            {
                var box = root.gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, b.size.y * s / 2f, 0f);
                box.size = new Vector3(b.size.x * s, b.size.y * s, b.size.z * s);
            }
            root.position = HP(x, y, z);
            root.rotation = Quaternion.Euler(0f, face + ModelYaw(model), 0f);
            return root;
        }

        /// <summary>模型的原生正面方向修正（Poly Haven 匯入後的正面）。</summary>
        private static float ModelYaw(string model) => 0f;

        private readonly Dictionary<string, Material[]> modelMats = new Dictionary<string, Material[]>();

        private Material[] ModelMaterials(string model, Renderer[] rends)
        {
            Material[] m;
            if (modelMats.TryGetValue(model, out m) && m != null && m.Length > 0 && m[0] != null) return m;
            var anom = new Color(0.5f, 0.6f, 0.66f);
            if (LanternReveal.HasPH(model + "_a", "Models"))
                m = new[] { PH("LM_" + model + "_a", model + "_a", "Models", 0f, Color.white, anom, 0.9f), PH("LM_" + model + "_b", model + "_b", "Models", 0f, Color.white, anom, 0.9f) };
            else
                m = new[] { PH("LM_" + model, model, "Models", 0f, Color.white, anom, 0.9f) };
            modelMats[model] = m;
            return m;
        }

        private void HouseFurniture()
        {
            float y = RealFloor2;
            // 玄關：鞋櫃、鏡子。
            Fur("ShoeCabinet", 4.55f, 1.6f, 0f, 0.3f, 0.9f, 0.8f, hWood);
            Fur("HallMirror", 4.72f, 1.6f, 1.3f, 0.03f, 0.7f, 0.5f, hWhite, false);
            // 客廳：壁爐在分戶牆中段；沙發面向壁爐，背後留 1 m 以上通道（客廳門在這側）。
            Fur("ChimneyBreast", 0.18f, 2.1f, 0f, 0.36f, 2.75f, 1.5f, hWall);
            Fur("Mantel", 0.42f, 2.1f, 1.1f, 0.2f, 0.08f, 1.6f, hWood, false);
            Fur("Hearth", 0.6f, 2.1f, 0f, 0.5f, 0.05f, 1.4f, hDark, false);
            Box("LivingRug", HP(1.7f, 0f, 2.1f) + Vector3.up * 0.006f, new Vector3(1.8f * HouseScale, 0.012f, 2.0f * HouseScale), hRug, false);
            Model("Sofa", "Sofa_01", 2.55f, 2.1f, 0f, 0.85f, -90f, true, hFabric, new Vector3(0.9f, 0.85f, 2.0f));
            Model("Armchair", "ArmChair_01", 0.95f, 0.75f, 0f, 0.95f, 35f, true, hLeather, new Vector3(0.8f, 0.9f, 0.8f));
            Model("CoffeeTable", "CoffeeTable_01", 1.6f, 2.1f, 0f, 0.42f, 90f, true, hWood, new Vector3(0.55f, 0.42f, 1.0f));
            Model("Bookcase", "wooden_bookshelf_worn", 0.25f, 3.65f, 0f, 1.9f, 90f, true, hWood, new Vector3(0.35f, 1.9f, 0.9f));
            Model("TVStand", "side_table_01", 2.9f, 0.32f, 0f, 0.55f, 0f, true, hWood, new Vector3(0.6f, 0.55f, 0.45f));
            Model("Television", "Television_01", 2.9f, 0.32f, 0.55f / HouseHeightScale * FurnitureScale, 0.45f, 0f, false, hDark, new Vector3(0.5f, 0.45f, 0.4f));
            Model("MantelClock", "mantel_clock_01", 0.42f, 2.6f, 1.18f, 0.28f, 90f, false, hWood, new Vector3(0.15f, 0.28f, 0.25f));
            // 餐廳：餐桌置中，兩側各留 1 m 以上通道；餐邊櫃靠後牆（不再擋路）。
            Model("DiningTable", "dining_table", 1.95f, 6.1f, 0f, 0.76f, 0f, true, hWood, new Vector3(0.9f, 0.75f, 1.6f));
            foreach (var p in new[] { new Vector3(1.2f, 5.6f, 90f), new Vector3(1.2f, 6.6f, 90f), new Vector3(2.7f, 5.6f, -90f) })
                Model("DiningChair", "dining_chair_02", p.x, p.y, 0f, 0.95f, p.z, true, hWood, new Vector3(0.44f, 0.9f, 0.44f));
            Model("Sideboard", "vintage_cabinet_01", 3.05f, 7.62f, 0f, 1.0f, 180f, true, hWood, new Vector3(1.2f, 0.9f, 0.45f));
            // 廚房：流理台沿分戶牆、冰箱在門邊、小餐桌、高櫃。
            Fur("Counter", 0.32f, 9.7f, 0f, 0.6f, 0.9f, 2.6f, hLightWood);
            Fur("CounterTop", 0.32f, 9.7f, 0.9f, 0.64f, 0.04f, 2.64f, hWhite, false);
            Fur("Stove", 0.32f, 8.4f, 0f, 0.6f, 0.9f, 0.6f, hDark);
            Fur("KitchenTable", 1.55f, 9.6f, 0f, 0.75f, 0.75f, 0.75f, hLightWood);
            Fur("TallCabinet", 2.55f, 11.1f, 0f, 0.6f, 2.0f, 0.45f, hWood);
            // 後廚：洗衣機、水槽、層架、兩個木箱（不擋後門）。
            Fur("Washer", 0.4f, 12.0f, 0f, 0.6f, 0.85f, 0.6f, hWhite);
            Fur("Sink", 0.35f, 12.9f, 0f, 0.55f, 0.9f, 0.8f, hWhite);
            Fur("Shelf", 2.55f, 13.45f, 0f, 0.6f, 1.8f, 0.35f, hWood);
            for (int i = 0; i < 2; i++)
            {
                var crate = Tether("HouseCrate", "木箱", HP(1.15f + i * 0.55f, 0f, 13.35f), new Vector3(0.42f, 0.42f, 0.42f), 2.5f);
                Box("CrateBody", crate.transform.position + Vector3.up * 0.21f, new Vector3(0.42f, 0.42f, 0.42f), hLightWood, false, null, crate.transform);
            }
            // 二樓：母親臥室（床、床頭櫃、衣櫃、抽屜櫃）、艾琳的房間、書房。
            Model("MotherBed", "old_bed_frame", 1.25f, 1.55f, y, 1.0f, 90f, true, hFabric, new Vector3(1.5f, 0.55f, 2.0f));
            Model("Nightstand", "ClassicNightstand_01", 0.3f, 2.85f, y, 0.6f, 90f, true, hWood, new Vector3(0.4f, 0.6f, 0.4f));
            Fur("Wardrobe", 4.4f, 0.5f, y, 0.6f, 2.0f, 1.2f, hWood, true, 90f);
            Model("Dresser", "drawer_cabinet", 3.9f, 3.05f, y, 0.9f, 180f, true, hWood, new Vector3(1.0f, 0.85f, 0.45f));
            Model("EleanorBed", "old_bed_frame", 0.75f, 4.5f, y, 0.9f, 90f, true, hFabric, new Vector3(0.95f, 0.5f, 1.95f));
            Model("Bookshelf", "wooden_bookshelf_worn", 2.35f, 7.55f, y, 1.8f, -90f, true, hWood, new Vector3(0.4f, 1.8f, 0.9f));
            Fur("Boxes", 2.2f, 4.0f, y, 0.5f, 0.5f, 0.5f, hLightWood);
            Fur("StudyDesk", 0.4f, 9.3f, y, 0.6f, 0.76f, 1.3f, hWood);
            Model("StudyShelves", "wooden_bookshelf_worn", 3.35f, 9.4f, y, 2.0f, -90f, true, hWood, new Vector3(0.4f, 2.0f, 1.6f));
        }

        // ---------------- 門牌 ----------------

        private void Sign(string text, Vector3 at, float faceYaw)
        {
            var go = new GameObject("Sign_" + text);
            go.transform.SetParent(Into, false);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, faceYaw + 180f, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.font = signFont;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = 0.022f;
            tm.anchor = TextAnchor.MiddleCenter;
            go.GetComponent<MeshRenderer>().sharedMaterial = hSign;
            var plate = Box("SignPlate", at - go.transform.forward * -0.012f, new Vector3(0.05f + text.Length * 0.17f, 0.2f, 0.015f), hWood, false, go.transform.eulerAngles + new Vector3(0f, 180f, 0f));
        }

        private void HouseSigns()
        {
            float dh = 2.1f * HouseHeightScale + 0.16f;
            Sign("客廳", HP(3.72f, 0f, 2.8f) + new Vector3(0.08f, dh, 0f), 90f);
            Sign("餐廳", HP(1.8f, 0f, 4.26f) + new Vector3(0f, dh, -0.08f), 180f);
            Sign("廚房", HP(1.47f, 0f, 7.98f) + new Vector3(0f, dh, -0.08f), 180f);
            Sign("後廚", HP(1.45f, 0f, 11.5f) + new Vector3(0f, dh, -0.08f), 180f);
            float uy = RealFloor2 * HouseHeightScale + 2.05f * HouseHeightScale + 0.16f;
            Sign("母親的房間", HP(3.17f, 0f, 3.4f) + new Vector3(0f, uy, 0.08f), 0f);
            Sign("艾琳的房間", HP(2.6f, 0f, 5.62f) + new Vector3(0.08f, uy, 0f), 90f);
            Sign("書房", HP(3.15f, 0f, 7.98f) + new Vector3(0f, uy, -0.08f), 180f);
        }

        // ---------------- 電箱、生活互動、近看 ----------------

        private void HouseInteractions()
        {
            float f = FurnitureScale;
            // 電箱（玄關樓梯下的牆上）：按住 E 把拉桿推回去，整棟燈亮起。
            Fur("FuseBoxCase", 4.68f, 3.45f, 1.2f, 0.12f, 0.55f, 0.45f, hDark, false);
            var lever = Box("FuseLever", HP(4.6f, 1.45f, 3.45f), new Vector3(0.05f, 0.22f, 0.05f), hWhite, true);
            var fr = lever.gameObject.AddComponent<LanternRestorable>();
            fr.Label = "電箱的拉桿（恢復供電）";
            fr.Seconds = 1.1f;
            fr.AnchorRadius = 0.6f;
            fr.Init(lever.position + Vector3.down * 0.08f, Quaternion.Euler(0f, 0f, 70f), lever.position, Quaternion.identity);
            Restorables.Add(fr);
            FuseBox = fr;

            // 冰箱門（轉開，裡面有燈）。
            var fridge = Fur("Fridge", 2.5f, 8.45f, 0f, 0.7f, 1.8f, 0.7f, hWhite);
            var hinge = new GameObject("FridgeHinge").transform;
            hinge.SetParent(Into, false);
            hinge.position = fridge.position + new Vector3(-0.37f * f, 0f, -0.35f * f);
            var door = Box("FridgeDoor", hinge.position + new Vector3(-0.02f, 0f, 0.35f * f), new Vector3(0.05f, 1.75f * f, 0.7f * f), hWhite, true, null, hinge);
            var fl = new GameObject("FridgeLight").AddComponent<Light>();
            fl.transform.SetParent(Into, false);
            fl.transform.position = fridge.position + new Vector3(-0.2f, 0.3f, 0f);
            fl.type = LightType.Point; fl.range = 1.5f; fl.intensity = 0.9f; fl.color = new Color(0.9f, 0.95f, 1f); fl.enabled = false;
            AddToggle(door.gameObject, "冰箱門", LanternToggle.Kind.Rotate, hinge, Vector3.zero, Vector3.zero, Quaternion.identity, Quaternion.Euler(0f, 105f, 0f), new[] { fl });

            // 母親抽屜櫃：最上層抽屜可以拉開（裡面是一張舊照片）。
            var drawer = Box("Drawer", HP(3.9f, RealFloor2, 3.05f) + new Vector3(0f, 0.72f * f, -0.24f * f), new Vector3(0.8f * f, 0.15f * f, 0.05f), hWood, true);
            var dpos = drawer.localPosition;
            AddToggle(drawer.gameObject, "抽屜", LanternToggle.Kind.Slide, drawer, dpos, dpos + new Vector3(0f, 0f, -0.32f * f), Quaternion.identity, Quaternion.identity, null, "拉開", "推回");

            // 客廳窗簾。
            var curtain = Box("Curtain", HP(1.1f, 1.6f, 0.22f), new Vector3(0.9f * HouseScale, 2.2f * HouseHeightScale, 0.06f), hFabric, false);
            var cpos = curtain.localPosition;
            AddToggle(curtain.gameObject, "窗簾", LanternToggle.Kind.Slide, curtain, cpos, cpos + new Vector3(-0.75f * HouseScale, 0f, 0f), Quaternion.identity, Quaternion.identity, null, "拉開", "拉上");

            // 電視（開了只有雜訊閃光）。
            var screen = Box("TVScreen", HP(2.9f, 0f, 0.32f) + new Vector3(0f, (0.55f + 0.24f) * f, 0.13f * f), new Vector3(0.36f * f, 0.27f * f, 0.01f), hDark, true);
            var tl = new GameObject("TVLight").AddComponent<Light>();
            tl.transform.SetParent(Into, false);
            tl.transform.position = screen.position + new Vector3(0f, 0f, 0.5f);
            tl.type = LightType.Point; tl.range = 3f; tl.intensity = 0.8f; tl.color = new Color(0.7f, 0.8f, 1f); tl.enabled = false;
            var tg = AddToggle(screen.gameObject, "電視", LanternToggle.Kind.Light, null, Vector3.zero, Vector3.zero, Quaternion.identity, Quaternion.identity, new[] { tl });
            tg.Screen = screen.GetComponent<Renderer>();

            // 近看：玄關桌上的信、母親房間的合照、二樓走廊的畫、壁爐上的鐘。
            var letter = Box("Letter", HP(4.55f, 0f, 1.6f) + Vector3.up * (0.9f * f + 0.012f), new Vector3(0.18f, 0.012f, 0.24f), hWhite, true, new Vector3(0f, 15f, 0f));
            AddInspect(letter.gameObject, LanternInspectable.Kind.Pages, "玄關的信", null, null, new[]
            {
                "（暫定文字）\n寄給：艾琳・維爾\n地址：……街 17 號\n\n郵戳日期和餐廳日曆上的日期對不起來。",
                "（暫定文字）\n「您申請的外地職位已通過初審，請於下月前回覆。」\n\n信封已經拆開過。",
            }, "");
            var mphoto = Box("BedsidePhoto", HP(0.3f, RealFloor2, 2.85f) + Vector3.up * (0.6f * f + 0.12f), new Vector3(0.03f, 0.2f, 0.16f), hDark, true, new Vector3(0f, 0f, 8f));
            AddInspect(mphoto.gameObject, LanternInspectable.Kind.Image, "床頭的合照", LanternReveal.Photo(true), null, null, "（背面，暫定）「海邊，週日下午。」");
            var painting = Box("CorridorPainting", HP(2.66f, RealFloor2 + 1.55f, 7.0f), new Vector3(0.04f, 0.6f, 0.8f), hWood, true);
            var paintMat = Mat("LH_Painting", Mode.Dual, Color.white, Color.white, LanternReveal.Painting(true), LanternReveal.Painting(false), 0.2f, 0.3f, 0.004f);
            var pq = Quad("PaintingFace", painting.position + Vector3.right * 0.025f, new Vector3(0.7f, 0.52f, 1f), new Vector3(0f, -90f, 0f), paintMat);
            AddInspect(painting.gameObject, LanternInspectable.Kind.Image, "走廊的畫", LanternReveal.Painting(true), LanternReveal.Painting(false), null, "（暫定）提燈照著時看到的是山丘；暗處看是一隻眼睛。");
            var clock = HouseRoot.Find("MantelClock");
            if (clock != null) AddInspect(clock.gameObject, LanternInspectable.Kind.Object, "壁爐上的鐘", null, null, null, "（暫定）指針停在 4:10。");
            Mark("HouseHallLetter", HP(4.1f, 0f, 1.0f) + Vector3.up * 0.05f, HP(4.5f, 0.92f, 1.6f));
        }

        private LanternToggle AddToggle(GameObject go, string label, LanternToggle.Kind kind, Transform moving, Vector3 closed, Vector3 open, Quaternion closedRot, Quaternion openRot, Light[] lights, string openVerb = "打開", string closeVerb = "關上")
        {
            var t = go.AddComponent<LanternToggle>();
            t.Label = label;
            t.Type = kind;
            t.Moving = moving;
            t.ClosedLocal = closed;
            t.OpenLocal = open;
            t.ClosedRot = closedRot;
            t.OpenRot = openRot;
            t.Lights = lights;
            t.OpenVerb = openVerb;
            t.CloseVerb = closeVerb;
            Toggles.Add(t);
            return t;
        }

        private LanternInspectable AddInspect(GameObject go, LanternInspectable.Kind kind, string title, Texture front, Texture back, string[] pages, string backText)
        {
            var i = go.AddComponent<LanternInspectable>();
            i.Type = kind;
            i.Title = title;
            i.Front = front;
            i.Back = back;
            i.Pages = pages ?? new string[0];
            i.BackText = backText ?? "";
            if (go.GetComponent<Collider>() == null)
            {
                // 模型沒有碰撞體：用外觀的範圍做一個只供瞄準的小碰撞體。
                var rs = go.GetComponentsInChildren<Renderer>();
                var col = go.AddComponent<BoxCollider>();
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds;
                    foreach (var r in rs) b.Encapsulate(r.bounds);
                    var ls = go.transform.lossyScale;
                    col.center = go.transform.InverseTransformPoint(b.center);
                    col.size = new Vector3(b.size.x / Mathf.Max(ls.x, 1e-4f), b.size.y / Mathf.Max(ls.y, 1e-4f), b.size.z / Mathf.Max(ls.z, 1e-4f));
                }
            }
            Inspectables.Add(i);
            return i;
        }

        // ---------------- 線索 ----------------

        private void HouseClueSet()
        {
            float y2 = RealFloor2;
            // 1. 客廳壁爐上的全家福：聚光照著它。
            var photoMat = Mat("LH_Photo", Mode.Dual, Color.white, Color.white, LanternReveal.Photo(true), LanternReveal.Photo(false), 0.4f, 0.4f, 0.002f);
            var photoPos = HP(0.48f, 1.18f, 1.75f) + Vector3.up * 0.18f * FurnitureScale;
            Box("PhotoFrame", photoPos + Vector3.left * 0.01f, new Vector3(0.03f, 0.3f * FurnitureScale, 0.4f * FurnitureScale), hWood, false);
            var photo = Quad("FamilyPhoto", photoPos + Vector3.right * 0.02f, new Vector3(0.36f * FurnitureScale, 0.27f * FurnitureScale, 1f), new Vector3(0f, -90f, 0f), photoMat);
            photo.gameObject.AddComponent<BoxCollider>().size = new Vector3(1f, 1f, 0.05f);
            AddClue(photo.gameObject, LanternClue.Kind.Focus, "photo", "壁爐上的全家福", "客廳壁爐上的全家福好像少了一個人……用聚光（Space）照著它看清楚。");

            // 2. 餐廳：艾琳的椅子倒在地上。
            var chair = new GameObject("EleanorChair").transform;
            chair.SetParent(Into, false);
            var saved = into;
            into = chair;
            var cm = Model("EleanorChairModel", "dining_chair_02", 0f, 0f, 0f, 0.95f, 0f, false, hWood, new Vector3(0.44f, 0.9f, 0.44f));
            into = saved;
            cm.SetParent(chair, false);
            cm.localPosition = Vector3.zero;
            cm.localRotation = Quaternion.identity;
            var cc = chair.gameObject.AddComponent<BoxCollider>();
            cc.center = new Vector3(0f, 0.45f * FurnitureScale, 0f);
            cc.size = new Vector3(0.48f, 0.95f, 0.48f) * FurnitureScale;
            var cr = chair.gameObject.AddComponent<LanternRestorable>();
            cr.Label = "倒下的椅子";
            cr.AnchorRadius = 1.6f;
            cr.Init(HP(2.95f, 0f, 6.95f) + Vector3.up * 0.2f, Quaternion.Euler(0f, 60f, 85f), HP(2.7f, 0f, 6.6f), Quaternion.Euler(0f, -90f, 0f));
            Restorables.Add(cr);
            var c2 = AddClue(chair.gameObject, LanternClue.Kind.Restore, "chair", "艾琳的椅子", "餐廳有一張椅子倒在地上——那是艾琳的位置。按住 E 把它扶正。");
            c2.Restorable = cr;

            // 3. 廚房：藍坐墊椅子在後廚，要搬到高櫃前的椅腳凹痕。
            var dentMat = Mat("LH_Dent", Mode.Dual, new Color(0.25f, 0.16f, 0.08f), new Color(0.1f, 0.11f, 0.12f), null, null, 0.1f, 0.2f, 0f);
            dentMat.SetColor("_EmissionColor", new Color(0.35f, 0.22f, 0.08f));
            var spotGo = new GameObject("HouseChairDents");
            spotGo.transform.SetParent(Into, false);
            spotGo.transform.position = HP(2.0f, 0f, 10.75f);
            foreach (var p in new[] { new Vector2(-0.19f, -0.18f), new Vector2(0.19f, -0.18f), new Vector2(-0.19f, 0.18f), new Vector2(0.19f, 0.18f) })
                Box("Dent", spotGo.transform.position + new Vector3(p.x, 0.003f, p.y) * FurnitureScale, new Vector3(0.07f, 0.006f, 0.07f), dentMat, false);
            var glow = spotGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.85f, 0.6f);
            glow.range = 2.2f;
            glow.intensity = 0.8f;
            glow.enabled = false;
            var cushion = Mat("LH_BlueCushion", Mode.Dual, new Color(0.2f, 0.32f, 0.62f), new Color(0.22f, 0.26f, 0.3f), null, null, 0.1f, 0.3f, 0.01f);
            var blue = Tether("HouseBlueChair", "藍坐墊椅子", HP(1.5f, 0f, 12.35f), new Vector3(0.46f, 0.92f, 0.44f), 4f);
            ChairParts(blue.transform, hLightWood, cushion);
            var spot = spotGo.AddComponent<LanternPlacementSpot>();
            spot.Expected = blue;
            spot.Glow = glow;
            spot.Tolerance = 0.4f;
            spot.Hint = "廚房高櫃前";
            Spots.Add(spot);
            var c3 = AddClue(spotGo, LanternClue.Kind.Place, "dents", "椅腳凹痕", "後廚有一張藍坐墊椅子……廚房高櫃前的地上有四個凹痕。抓起椅子時，該放的地方會發光。");
            c3.Spot = spot;

            // 4. 艾琳的房間：異域牆後的研究筆記（拿起來翻頁，放下時才算看完）。
            var fake = Mat("LH_FakeWall", Mode.AnomalyOnly, Color.white, new Color(0.28f, 0.3f, 0.36f), LanternReveal.Grid, LanternReveal.Grid, 0.2f, 0.5f, 0.02f);
            fake.SetFloat("_WorldUV", 1f);
            var fw = Box("AnomalyWall", HP(0.65f, y2 + 1.1f, 6.55f), new Vector3(1.3f * HouseScale, 2.2f * HouseHeightScale, 0.12f), fake, true);
            var blocker = fw.gameObject.AddComponent<LanternAnomalyBlocker>();
            blocker.Solid = fw.GetComponent<Collider>();
            blocker.Probe = HP(0.65f, y2 + 1.0f, 6.55f);
            Blockers.Add(blocker);
            HWallZ(1.3f, 6.55f, 7.98f, y2, RealUpperHeight, IntT, hWallEleanor, 2.05f);
            Fur("EleanorDesk", 0.4f, 7.35f, y2, 0.6f, 0.76f, 1.1f, hWood);
            var nbMat = Mat("LH_Notebook", Mode.Dual, new Color(0.55f, 0.2f, 0.15f), new Color(0.25f, 0.2f, 0.2f), null, null, 0.3f, 0.3f, 0.002f);
            nbMat.SetColor("_EmissionColor", new Color(0.25f, 0.12f, 0.05f));
            var nb = Box("ResearchNotebook", HP(0.45f, y2, 7.35f) + Vector3.up * (0.76f * FurnitureScale + 0.02f), new Vector3(0.22f, 0.04f, 0.3f), nbMat, true, new Vector3(0f, 20f, 0f));
            var rd = nb.gameObject.AddComponent<LanternReadable>();
            rd.Title = "艾琳的研究筆記";
            rd.Text = "（暫定文字，待正式劇本）";
            var ins = AddInspect(nb.gameObject, LanternInspectable.Kind.Pages, "艾琳的研究筆記", null, null, new[]
            {
                "（暫定文字，待正式劇本）\n\n如果你看到這一頁，代表你也走進來了。",
                "它們在黑暗裡看不清楚——\n只有被光照到的地方，它們才會現出原形。",
                "每當有東西被放回原本的樣子，它們就會出現。\n它們不想讓這裡變回真的。",
                "別一直站在暗處。\n\n（筆記的最後幾頁被撕掉了。）",
            }, "");
            ins.Completes = rd;
            var c4 = AddClue(nb.gameObject, LanternClue.Kind.Read, "notebook", "艾琳的研究筆記", "二樓艾琳的房間，書桌那個角落被一面不該存在的牆封住了。用光打開它，看看她留下的筆記。");
            c4.Readable = rd;

            // 5. 書房：倒下的地圖板。
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
            var c5 = AddClue(board.gameObject, LanternClue.Kind.Restore, "map", "倒下的地圖板", "二樓書房的地圖板倒了——上面標著這條街的門牌。按住 E 扶起來。");
            c5.Restorable = br;
        }

        private LanternClue AddClue(GameObject go, LanternClue.Kind kind, string id, string label, string hint)
        {
            var c = go.AddComponent<LanternClue>();
            c.Type = kind;
            c.Id = id;
            c.Label = label;
            c.Hint = hint;
            Clues.Add(c);
            return c;
        }

        private void HouseLights()
        {
            var bulb = Mat("LH_Bulb", Mode.Dual, new Color(1f, 0.9f, 0.7f), new Color(0.7f, 0.85f, 1f), null, null, 0.8f, 0.8f, 0f);
            bulb.SetColor("_EmissionColor", new Color(2.2f, 1.7f, 1.0f));
            bulb.SetColor("_AnomalyEmission", new Color(1.6f, 2.2f, 3f));
            void Lamp(string name, float x, float y, float z, Color c, float intensity, float range)
            {
                Ball(name + "Bulb", HP(x, y, z) + Vector3.up * 0.12f, new Vector3(0.12f, 0.14f, 0.12f), bulb);
                AddLight(name, LightType.Point, HP(x, y, z), c, intensity * 1.7f, range * HouseScale * 1.15f, true, LightShadows.Soft);
            }
            Lamp("HallLight", 4.25f, 2.55f, 1.6f, new Color(0.9f, 0.85f, 1f), 0.9f, 4.5f);
            Lamp("LivingLight", 1.8f, 2.55f, 2.1f, new Color(0.9f, 0.8f, 1f), 1.0f, 5f);
            Lamp("DiningLight", 1.8f, 2.55f, 6.1f, new Color(0.9f, 0.88f, 1f), 0.9f, 5f);
            Lamp("KitchenLight", 1.45f, 2.55f, 9.7f, new Color(0.85f, 1f, 0.9f), 0.9f, 5f);
            Lamp("SculleryLight", 1.45f, 2.55f, 12.6f, new Color(0.8f, 0.95f, 0.9f), 0.6f, 3.5f);
            Lamp("UpperLight", 3.15f, RealFloor2 + 2.35f, 5.6f, new Color(0.85f, 0.85f, 1f), 0.7f, 4.5f);
            Lamp("EleanorLight", 1.3f, RealFloor2 + 2.35f, 5.4f, new Color(0.95f, 0.85f, 1f), 0.6f, 4f);
            Lamp("StudyLight", 1.8f, RealFloor2 + 2.35f, 9.3f, new Color(0.85f, 0.9f, 1f), 0.6f, 4f);
            Lamp("MotherLight", 2.4f, RealFloor2 + 2.35f, 1.7f, new Color(0.9f, 0.88f, 1f), 0.6f, 4.5f);
            AddLight("YardMoon", LightType.Spot, HP(3.8f, 6f, 10.8f), new Color(0.55f, 0.65f, 0.9f), 2.2f, 12f, true, LightShadows.Soft, 80f, Vector3.down);
        }
    }
}
