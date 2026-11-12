using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// v0.6 客廳美術標準間（只用於方案 A 客廳；只有「客廳美術標準間 QA」會建立）。
    /// 模型與貼圖由 Blender 程序建模（tools/blender/case01_living_room），放在 Resources/Case01LivingRoom；這裡依名稱套材質、
    /// 擺到原家具的同一個錨點（調查熱區不動）、加上依外形的碰撞體、燈與牆／地／天花板的表面。
    /// 新舊兩套各自一個根物件：<see cref="SetActive"/> 只切換顯示與碰撞，事件、熱區、H0～H3 邏輯共用。
    /// 這是可重建的程序模型，不是外包高精模；沒有外部素材。
    /// </summary>
    public sealed class Case01LivingArt
    {
        public const string Folder = "Case01LivingRoom/";
        public static readonly string[] Models =
        {
            "Sofa", "Armchair", "Pillow", "CoffeeTable", "SideTable", "Bookcase", "Book", "SashWindow", "Curtains", "TableLamp", "Rug",
            "Mantelpiece", "TvUnit", "PhotoFrame", "PaintingFrame", "WallFrame", "Plant", "Props", "Cornice",
        };

        public Transform Root { get; private set; }
        public bool Active { get; private set; }
        public Light LampLight { get; private set; }
        public Light FireLight { get; private set; }
        public readonly Dictionary<string, Transform> Pieces = new Dictionary<string, Transform>();
        public readonly List<GameObject> Books = new List<GameObject>();
        public int Stage { get; private set; } = -1;

        private readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        private readonly List<Renderer> oldWindowFrames = new List<Renderer>();
        private Case01HouseLayout layout;
        private Case01Palette palette;
        private Material ember, shade;

        /// <summary>美術資產是否在專案中（找不到時客廳美術 QA 退回舊版並提示）。</summary>
        public static bool AssetsAvailable { get { return Resources.Load<GameObject>(Folder + "Sofa") != null; } }

        public static Case01LivingArt Build(Case01HouseLayout L, Case01Palette p)
        {
            if (L == null || L.Floorplan != Case01Floorplan.V05A || L.LivingOldRoot == null || !AssetsAvailable) return null;
            var a = new Case01LivingArt { layout = L, palette = p };
            a.Root = ArenaArt.Group("LivingArtV06", L.Root.Find("GroundFloor"), Vector3.zero);
            a.MakeMaterials();
            a.BuildShell();
            a.BuildFurniture();
            foreach (var w in L.LivingWindows)
                foreach (var r in w.GetComponentsInChildren<Renderer>(true))
                    if (r.name != "Glass") a.oldWindowFrames.Add(r);
            L.LivingArt = a;
            a.SetActive(true);
            return a;
        }

        // ---------------- 材質 ----------------

        private Material Tex(string name, float smooth, float metal = 0f, Color? tint = null, Color? emission = null)
        {
            var m = palette.Own(ArenaArt.Mat(tint ?? Color.white, smooth, emission, metal));
            m.name = name;
            m.mainTexture = Resources.Load<Texture2D>(Folder + name);
            mats[name] = m;
            return m;
        }

        private void MakeMaterials()
        {
            Tex("LR_WoodMid", 0.32f);
            Tex("LR_WoodDark", 0.36f);
            Tex("LR_SofaFabric", 0.06f);
            Tex("LR_CushionCream", 0.05f);
            Tex("LR_CushionRed", 0.05f);
            Tex("LR_Plaid", 0.05f);
            Tex("LR_Curtain", 0.08f);
            Tex("LR_Paint", 0.18f);
            Tex("LR_Rug", 0.04f);
            Tex("LR_Brass", 0.55f, 0.75f);
            Tex("LR_Ceramic", 0.45f);
            shade = Tex("LR_Shade", 0.05f, 0f, null, new Color(1f, 0.72f, 0.42f) * 0.55f);
            Tex("LR_Iron", 0.38f, 0.35f);
            Tex("LR_HearthTile", 0.3f);
            Tex("LR_Linen", 0.05f);
            Tex("LR_Wallpaper", 0.08f);
            Tex("LR_Floor", 0.28f);
            Tex("LR_Ceiling", 0.05f);
            Tex("LR_Painting", 0.1f);
            Tex("LR_FamilyPhoto", 0.15f);
            var plastic = palette.Own(ArenaArt.Mat(new Color(0.07f, 0.07f, 0.08f), 0.55f));
            plastic.name = "LR_Plastic";
            mats["LR_Plastic"] = plastic;
            var screen = palette.Own(ArenaArt.Mat(new Color(0.02f, 0.025f, 0.03f), 0.92f));
            screen.name = "LR_Screen";
            mats["LR_Screen"] = screen;
            ember = palette.Own(ArenaArt.Mat(new Color(0.25f, 0.08f, 0.03f), 0.2f, new Color(1.6f, 0.55f, 0.15f)));
            ember.name = "LR_Ember";
            mats["LR_Ember"] = ember;
            var flower = palette.Own(ArenaArt.Mat(new Color(0.92f, 0.88f, 0.78f), 0.1f));
            flower.name = "LR_Flower";
            mats["LR_Flower"] = flower;
            var glass = palette.Own(ArenaArt.FadeMat(new Color(0.75f, 0.82f, 0.85f, 0.35f)));
            glass.name = "LR_Glass";
            mats["LR_Glass"] = glass;
            var leaf = Tex("LR_Leaf", 0.2f);
            ShepherdShading.MakeCutout(leaf, 0.5f);
            leaf.SetInt("_Cull", 0);
            var pages = Tex("LR_Books", 0.25f);
            pages.name = "LR_Pages";
            mats["LR_Pages"] = pages;
            // 書背圖集不直接對應（每本書在 SetMat 依顏色欄位指定）。
            mats.Remove("LR_Books");
        }

        /// <summary>書背圖集的第 i 欄（共 7 色）。</summary>
        private readonly Dictionary<int, Material> bookMats = new Dictionary<int, Material>();

        private Material BookMat(int i)
        {
            Material m;
            if (bookMats.TryGetValue(i, out m)) return m;
            m = palette.Own(ArenaArt.Mat(Color.white, 0.3f));
            m.name = "LR_Books_" + i;
            m.mainTexture = Resources.Load<Texture2D>(Folder + "LR_Books");
            m.mainTextureOffset = new Vector2((i % 7) * 0.125f, 0f);
            bookMats[i] = m;
            return m;
        }

        // ---------------- 擺放工具 ----------------

        /// <summary>實例化一個模型（整個 FBX），依材質名稱換成本套材質。</summary>
        private Transform Place(string model, Vector3 pos, float yaw, Transform parent = null, string child = null)
        {
            var prefab = Resources.Load<GameObject>(Folder + model);
            var go = Object.Instantiate(prefab);
            go.name = model;
            var t = go.transform;
            if (child != null)
            {
                var c = t.Find(child);
                c.SetParent(null, false);
                ArenaArt.Kill(go);
                t = c;
                go = c.gameObject;
            }
            t.SetParent(parent ?? Root, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Remap(go);
            Pieces[child ?? model] = t;
            return t;
        }

        private void Remap(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    if (ms[i] == null) continue;
                    var key = ms[i].name.Replace(" (Instance)", "");
                    Material m;
                    if (mats.TryGetValue(key, out m)) ms[i] = m;
                }
                r.sharedMaterials = ms;
            }
        }

        private static BoxCollider Block(Transform parent, Vector3 center, Vector3 size, float yaw, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var b = go.AddComponent<BoxCollider>();
            b.size = size;
            return b;
        }

        /// <summary>單面方片（法線＝normal），UV 以公尺計（tile＝一張貼圖覆蓋幾公尺）。</summary>
        private Transform Quad(string name, Vector3 center, Vector3 normal, Vector3 up, float w, float h, Material m, float tile, Vector2 uvOrigin)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.LookRotation(-normal, up);
            var mesh = new Mesh { name = name };
            float hw = w / 2, hh = h / 2;
            mesh.vertices = new[] { new Vector3(-hw, -hh, 0), new Vector3(hw, -hh, 0), new Vector3(hw, hh, 0), new Vector3(-hw, hh, 0) };
            var o = uvOrigin / tile;
            mesh.uv = new[] { o + new Vector2(0, 0), o + new Vector2(w / tile, 0), o + new Vector2(w / tile, h / tile), o + new Vector2(0, h / tile) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        private Material FigureMat(Color c)
        {
            var m = palette.Own(ArenaArt.Mat(c, 0.1f));
            m.name = "LR_Figure";
            m.mainTexture = Resources.Load<Texture2D>(Folder + "LR_Figure");
            ShepherdShading.MakeCutout(m, 0.5f);
            return m;
        }

        private Transform Print(Transform parent, Vector3 local, float w, float h, Material m, string name)
        {
            var q = ArenaArt.Prim(PrimitiveType.Quad, parent, local, new Vector3(w, h, 1f), m, new Vector3(0f, 180f, 0f), name, false, false);
            return q;
        }

        // ---------------- 牆、地、天花板 ----------------

        public const float X0 = 0.075f, X1 = Case01HouseBuilder.V05A.HallX0 - 0.075f, Z0 = 0.075f, Z1 = Case01HouseBuilder.V05A.LivingZ1 - 0.075f;
        public const float CeilingY = 2.795f;

        private void BuildShell()
        {
            // 地板：寬約 15 cm 的長條木地板（1.2 m 一張貼圖）。
            Quad("FloorBoards", new Vector3((X0 + X1) / 2, 0.003f, (Z0 + Z1) / 2), Vector3.up, Vector3.forward, X1 - X0, Z1 - Z0, mats["LR_Floor"], 1.2f,
                new Vector2(X0, Z0));
            Quad("CeilingPlaster", new Vector3((X0 + X1) / 2, CeilingY - 0.004f, (Z0 + Z1) / 2), Vector3.down, Vector3.forward, X1 - X0, Z1 - Z0, mats["LR_Ceiling"], 1.5f,
                new Vector2(X0, Z0));
            // 四面牆的室內面：依現有牆段（含窗下牆裙、窗上楣、門楣）逐段貼壁紙，門洞與窗洞自然留空。
            var g = layout.Root.Find("GroundFloor");
            int n = 0;
            // 用 Renderer 的範圍（剛建立、尚未同步物理時 Collider.bounds 還是空的）。
            foreach (var rend in g.GetComponentsInChildren<MeshRenderer>(true))
            {
                var nm = rend.name;
                if (nm != "Wall" && nm != "Lintel" && nm != "WindowHead" && nm != "WindowApron") continue;
                var b = rend.bounds;
                float y0 = Mathf.Max(0f, b.min.y), y1 = Mathf.Min(CeilingY, b.max.y);
                if (y1 - y0 < 0.01f) continue;
                var paper = mats["LR_Wallpaper"];
                // 西牆（x=0）、東牆（x=5.4）：沿 z。
                if (Mathf.Abs(b.max.x - X0) < 0.02f || Mathf.Abs(b.min.x - X1) < 0.02f)
                {
                    bool west = Mathf.Abs(b.max.x - X0) < 0.02f;
                    float z0 = Mathf.Max(Z0, b.min.z), z1 = Mathf.Min(Z1, b.max.z);
                    if (z1 - z0 < 0.01f) continue;
                    float x = west ? X0 + 0.004f : X1 - 0.004f;
                    Quad("Wallpaper", new Vector3(x, (y0 + y1) / 2, (z0 + z1) / 2), west ? Vector3.right : Vector3.left, Vector3.up, z1 - z0, y1 - y0, paper, 1.0f,
                        new Vector2(west ? z0 : -z1, y0));
                    n++;
                }
                // 南牆（z=0）、北牆（z=5.4）：沿 x。
                else if (Mathf.Abs(b.max.z - Z0) < 0.02f || Mathf.Abs(b.min.z - Z1) < 0.02f)
                {
                    bool south = Mathf.Abs(b.max.z - Z0) < 0.02f;
                    float x0 = Mathf.Max(X0, b.min.x), x1 = Mathf.Min(X1, b.max.x);
                    if (x1 - x0 < 0.01f) continue;
                    float z = south ? Z0 + 0.004f : Z1 - 0.004f;
                    Quad("Wallpaper", new Vector3((x0 + x1) / 2, (y0 + y1) / 2, z), south ? Vector3.forward : Vector3.back, Vector3.up, x1 - x0, y1 - y0, paper, 1.0f,
                        new Vector2(south ? -x1 : x0, y0));
                    n++;
                }
            }
            WallPieces = n;
            // 天花板線板（四面）。
            Cornice(new Vector3((X0 + X1) / 2, CeilingY, Z0), 0f, X1 - X0);
            Cornice(new Vector3((X0 + X1) / 2, CeilingY, Z1), 180f, X1 - X0);
            Cornice(new Vector3(X0, CeilingY, (Z0 + Z1) / 2), 90f, Z1 - Z0);
            Cornice(new Vector3(X1, CeilingY, (Z0 + Z1) / 2), -90f, Z1 - Z0);
        }

        public int WallPieces { get; private set; }

        private void Cornice(Vector3 pos, float yaw, float length)
        {
            var c = Place("Cornice", pos, yaw);
            c.localScale = new Vector3(length, 1f, 1f);
            foreach (var r in c.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ---------------- 家具 ----------------

        private void BuildFurniture()
        {
            var L = layout;
            var d = L.Decor;

            // 壁爐：胸牆（同原位置、有碰撞）、維多利亞式壁爐架、鑄鐵爐框與爐火（只在 H0 亮）、上方風景畫、壁爐架上的合照與小物。
            var breast = ArenaArt.Prim(PrimitiveType.Cube, Root, new Vector3(0.27f, 1.4f, 3.0f), new Vector3(0.4f, 2.8f, 1.6f), mats["LR_Wallpaper"], null, "ChimneyBreast", true, true);
            Pieces["ChimneyBreast"] = breast;
            Place("Mantelpiece", new Vector3(0.47f, 0f, 3.0f), 90f);
            FireLight = ArenaArt.PointLight(Root, new Vector3(0.78f, 0.32f, 3.0f), new Color(1f, 0.55f, 0.22f), 3.2f, 0.9f, false);
            FireLight.name = "FireGlow";
            var painting = Place("PaintingFrame", new Vector3(0.475f, 1.86f, 3.0f), 90f);
            Print(painting, new Vector3(0f, 0f, 0.012f), 0.76f, 0.52f, mats["LR_Painting"], "PaintingPrint");
            // 合照（Case01Ids.Photo 的熱區位置不變）。
            var photo = Place("PhotoFrame", new Vector3(0.6f, 1.28f, 2.42f), 0f);
            photo.localRotation = Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(-9f, 0f, 0f);
            Print(photo, new Vector3(0f, 0f, 0.012f), 0.29f, 0.21f, mats["LR_FamilyPhoto"], "PhotoPrint");
            // 人影：柔邊剪影（alpha cutout）；第二個人在核穩定後換成褪色版（與原版同一套規則）。
            L.PhotoFigureMatAlt = FigureMat(new Color(0.22f, 0.19f, 0.19f));
            L.PhotoFadedMatAlt = FigureMat(new Color(0.66f, 0.63f, 0.57f));
            Print(photo, new Vector3(-0.05f, -0.02f, 0.014f), 0.09f, 0.15f, L.PhotoFigureMatAlt, "FigureA");
            L.PhotoSecondFigureAlt = Print(photo, new Vector3(0.05f, -0.025f, 0.015f), 0.085f, 0.14f, L.PhotoFigureMatAlt, "FigureB").GetComponent<Renderer>();
            Block(photo, new Vector3(0f, -0.11f, -0.05f), new Vector3(0.05f, 0.05f, 0.1f), 0f, "PhotoStand").enabled = false;
            Place("Props", new Vector3(0.58f, 1.14f, 3.32f), 90f, null, "MantelClock");
            Place("Props", new Vector3(0.58f, 1.14f, 3.66f), 90f, null, "Candlestick");
            Place("Props", new Vector3(0.58f, 1.14f, 2.75f), 90f, null, "Candlestick");

            // 書櫃（書放在層板上；H1 會缺的兩本、夾便箋的書，位置與原版相同）。
            var bc = Place("Bookcase", new Vector3(0.27f, 0f, 4.6f), 90f);
            Block(Root, new Vector3(0.27f, 0.95f, 4.6f), new Vector3(1.3f, 1.9f, 0.32f), 90f, "BookcaseBlock");
            BuildBooks(bc, d, 1.3f);

            // 電視與矮櫃（西南角；稍微離開窗簾）。
            Place("TvUnit", new Vector3(0.75f, 0f, 0.8f), 45f);
            Block(Root, new Vector3(0.75f, 0.5f, 0.8f), new Vector3(1.0f, 1.0f, 0.44f), 45f, "TvBlock");

            // 沙發（面向壁爐）＋抱枕、格紋毯（毯子在沙發模型裡）。
            var sofa = Place("Sofa", new Vector3(3.62f, 0f, 3.05f), -90f);
            Block(Root, new Vector3(3.62f, 0.45f, 3.05f), new Vector3(1.9f, 0.9f, 0.88f), -90f, "SofaBlock");
            var p1 = Place("Pillow", new Vector3(0.52f, 0.66f, -0.2f), 0f, sofa);
            p1.localRotation = Quaternion.Euler(-14f, -10f, 6f);
            SetMat(p1, "LR_CushionRed");
            var p2 = Place("Pillow", new Vector3(-0.42f, 0.64f, -0.22f), 0f, sofa);
            p2.localRotation = Quaternion.Euler(-16f, 12f, -8f);
            SetMat(p2, "LR_CushionCream");

            // 茶几（桌面上的朋友卡片位置與熱區相同）＋生活小物。
            var table = Place("CoffeeTable", new Vector3(2.2f, 0f, 3.05f), 90f);
            Block(Root, new Vector3(2.2f, 0.23f, 3.05f), new Vector3(1.1f, 0.46f, 0.6f), 90f, "CoffeeTableBlock");
            var card = ArenaArt.Prim(PrimitiveType.Cube, table, new Vector3(0.32f, 0.53f, -0.05f), new Vector3(0.16f, 0.12f, 0.005f), palette.Tint("paper", new Color(0.95f, 0.66f, 0.56f)),
                new Vector3(-12f, 160f, 0f), "FriendCard", false, false);
            Place("Props", new Vector3(-0.28f, 0.46f, 0.1f), 30f, table, "TeaCup");
            Place("Props", new Vector3(-0.02f, 0.46f, -0.12f), 0f, table, "FlowerVase");
            for (int i = 0; i < 2; i++)
            {
                var b = Place("Book", new Vector3(-0.38f, 0.46f + i * 0.045f, -0.12f), 90f + i * 9f, table);
                b.localScale = new Vector3(0.042f, 0.22f, 0.16f);
                b.localRotation = Quaternion.Euler(0f, 90f + i * 9f, 90f);
                SetMat(b, null, 2 + i);
            }
            Place("Rug", new Vector3(2.35f, 0f, 3.05f), 90f);

            // 扶手椅、側桌＋桌燈（取代原本的落地燈；燈光由 Case01Mood 依 H0～H3 調整）。
            Place("Armchair", new Vector3(2.6f, 0f, 4.45f), 200f);
            Block(Root, new Vector3(2.6f, 0.45f, 4.45f), new Vector3(0.95f, 0.9f, 0.85f), 200f, "ArmchairBlock");
            Place("SideTable", new Vector3(3.62f, 0f, 4.32f), -90f);
            Block(Root, new Vector3(3.62f, 0.29f, 4.32f), new Vector3(0.44f, 0.58f, 0.44f), -90f, "SideTableBlock");
            Place("TableLamp", new Vector3(3.62f, 0.58f, 4.32f), 0f);
            LampLight = ArenaArt.PointLight(Root, new Vector3(3.62f, 0.95f, 4.32f), new Color(1f, 0.74f, 0.45f), 4.2f, 1.15f, false);
            LampLight.name = "TableLampLight";
            L.LampLights.Add(LampLight);

            // 盆栽（兩扇窗之間）。
            Place("Plant", new Vector3(2.7f, 0f, 0.4f), 0f);
            Block(Root, new Vector3(2.7f, 0.3f, 0.4f), new Vector3(0.36f, 0.6f, 0.36f), 0f, "PlantBlock");

            // 兩扇英式上下拉窗（保留原本的透明玻璃與窗外街景）＋厚重窗簾。
            foreach (var w in L.LivingWindows)
            {
                Place("SashWindow", new Vector3(w.position.x, w.position.y, w.position.z), 0f);
                Place("Curtains", new Vector3(w.position.x, 0f, w.position.z), 0f);
            }

            // 北牆的家庭相框（H0 照片／H1 起空白：同一套 Decor 切換）。
            var wf = Place("WallFrame", new Vector3(4.65f, 1.7f, Z1), 180f);
            var ph = ArenaArt.Group("Photo", wf, Vector3.zero);
            Print(ph, new Vector3(0f, 0.06f, 0.012f), 0.52f, 0.22f, palette.Own(ArenaArt.Mat(new Color(0.48f, 0.62f, 0.72f), 0.1f)), "Sky");
            Print(ph, new Vector3(0f, -0.1f, 0.013f), 0.52f, 0.14f, palette.Own(ArenaArt.Mat(new Color(0.74f, 0.66f, 0.5f), 0.1f)), "Sand");
            Print(ph, new Vector3(-0.07f, -0.04f, 0.015f), 0.1f, 0.18f, L.PhotoFigureMatAlt, "FigureA");
            Print(ph, new Vector3(0.06f, -0.05f, 0.016f), 0.09f, 0.16f, L.PhotoFigureMatAlt, "FigureB");
            d.LivingFramePhotoAlt = ph.gameObject;
            d.LivingFrameBlankAlt = Print(wf, new Vector3(0f, 0f, 0.011f), 0.54f, 0.38f, mats["LR_Linen"], "BlankPrint").gameObject;
            d.ApplyLiving(d.LivingChanged, d.LivingStable);
        }

        private void SetMat(Transform t, string name, int book = -1)
        {
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    if (book >= 0) ms[i] = ms[i] != null && ms[i].name.StartsWith("LR_Pages") ? mats["LR_Pages"] : BookMat(book);
                    else ms[i] = mats[name];
                }
                r.sharedMaterials = ms;
            }
        }

        /// <summary>依原版相同的亂數與位置擺書（本地 x 沿書櫃寬、z 為前後）；第三層中段兩本是 H1 會消失的書。</summary>
        private void BuildBooks(Transform bc, Case01Decor d, float width)
        {
            var g = ArenaArt.Group("Books", bc, Vector3.zero);
            float hw = width * 0.5f;
            float[] tops = { 0.08f, 0.5f, 0.92f, 1.34f };
            var rng = new System.Random(55);
            System.Func<float, float, float> R = (a, b) => a + (float)rng.NextDouble() * (b - a);
            for (int row = 0; row < 3; row++)
            {
                float x = -hw + 0.05f;
                int n = 0;
                while (x < hw - 0.07f)
                {
                    bool marked = row == 2 && (n == 4 || n == 5);
                    float w = marked ? 0.08f : R(0.03f, 0.065f), h = marked ? 0.34f : R(0.24f, 0.36f);
                    var b = Place("Book", new Vector3(x + w * 0.5f, tops[row], 0f), -90f, g);
                    // Book 模型：x＝厚度、y＝高度、z＝深度（書背朝 +z）；書櫃本地 +z 朝房間，旋轉 -90 讓書背朝外再轉回來。
                    b.localRotation = Quaternion.identity;
                    b.localScale = new Vector3(w, h, 0.22f);
                    SetMat(b, null, marked ? (n == 4 ? 0 : 4) : (n + row * 3) % 7);
                    if (marked) d.LivingMissingBooks.Add(b.gameObject);
                    Books.Add(b.gameObject);
                    x += w + 0.004f;
                    n++;
                    if (row == 1 && n == 7) x += 0.12f;
                }
            }
            // 第二層右端：夾著便箋、微微突出的那本書。
            var nb = Place("Book", new Vector3(hw - 0.12f, tops[1], 0.06f), 0f, g);
            nb.localScale = new Vector3(0.05f, 0.3f, 0.22f);
            SetMat(nb, null, 1);
            ArenaArt.Prim(PrimitiveType.Cube, g, new Vector3(hw - 0.12f, tops[1] + 0.32f, 0.08f), new Vector3(0.008f, 0.06f, 0.18f), palette.Get("paper"), null, "NoteTab", false, false);
            // 最上層：橫放的一疊書、兩張小生活照、一個小陶瓶。
            for (int i = 0; i < 3; i++)
            {
                var b = Place("Book", new Vector3(-0.42f, tops[3] + 0.02f + i * 0.042f, 0f), 0f, g);
                b.localRotation = Quaternion.Euler(0f, 0f, 90f);
                b.localScale = new Vector3(0.04f, 0.24f, 0.18f);
                b.localPosition += new Vector3(0.12f, 0f, 0f);
                SetMat(b, null, 5 - i);
            }
            for (int i = 0; i < 2; i++)
            {
                var f = Place("PhotoFrame", new Vector3(0.05f + i * 0.28f, tops[3] + 0.09f, -0.05f), 0f, g);
                f.localScale = Vector3.one * 0.55f;
                f.localRotation = Quaternion.Euler(-8f, i == 0 ? 6f : -8f, 0f);
                Print(f, new Vector3(0f, 0f, 0.012f), 0.29f, 0.21f, mats["LR_FamilyPhoto"], "Print");
            }
            Place("Props", new Vector3(0.5f, tops[3], 0f), 0f, g, "FlowerVase").localScale = Vector3.one * 0.8f;
        }

        // ---------------- 切換與階段 ----------------

        /// <summary>新美術（true）／舊版（false）。只切換顯示與碰撞；熱區、事件、H0～H3 物件共用。</summary>
        public void SetActive(bool art)
        {
            Active = art;
            Root.gameObject.SetActive(art);
            if (layout.LivingOldRoot != null) layout.LivingOldRoot.gameObject.SetActive(!art);
            foreach (var r in oldWindowFrames) if (r != null) r.enabled = !art;
            ApplyStage(Mathf.Max(0, Stage));
        }

        /// <summary>H0：爐火與暖色燈罩；H1 起爐火熄滅、燈罩失溫（燈光本身由 Case01Mood 處理，H3 不回暖）。</summary>
        public void ApplyStage(int stage)
        {
            Stage = stage;
            bool warm = stage == 0;
            if (FireLight != null) FireLight.enabled = Active && warm;
            ArenaArt.SetEmission(ember, warm ? new Color(1.6f, 0.55f, 0.15f) : new Color(0.04f, 0.02f, 0.02f));
            var sc = stage == 0 ? new Color(1f, 0.72f, 0.42f) * 0.55f : stage == 1 ? new Color(0.85f, 0.75f, 0.6f) * 0.22f : new Color(0.55f, 0.6f, 0.68f) * 0.06f;
            ArenaArt.SetEmission(shade, sc);
        }
    }
}
