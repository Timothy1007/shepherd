using UnityEngine;
using Mode = Shepherd.Exploration.Lantern.LanternReveal.Mode;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 改衣店正式美術（2026-11-17，使用者看過「只換 shader」的測試後決定全部重新建模）：
    /// 和縫紉機桌同一套風格的 Blender 模型（tools/blender/case01_shop_stylized/build_shop.py），每件東西都是自己的物件。
    /// 尺寸、位置、名字和原本的 v2_ 模型一樣（布局 A、謎題、動線、碰撞都不用重調），只換外觀；
    /// 原本放在一起的東西拆開（櫃台上的東西、熨斗、衣架上的衣服、架上的布、傘、布捲、層板上的東西、掛鉤板）。
    /// 牆、地板、天花板換成手繪貼圖（世界座標重複，卡通 shader，照樣有褪色／記憶兩種狀態）。
    /// UseStylizedShop = false 就回到原本的模型。
    /// </summary>
    public sealed partial class LanternGraybox
    {
        public static bool UseStylizedShop = true;
        public bool StylizedShopInUse { get; private set; }

        /// <summary>改衣店的模型：開了正式美術而且有新模型時用 v3_，否則用原本的。</summary>
        private string SM(string v2, string v3)
        {
            if (UseStylizedShop && Resources.Load<GameObject>("Models/Custom/" + v3) != null) { StylizedShopInUse = true; return v3; }
            return v2;
        }

        /// <summary>在某件東西的座標裡放另一件獨立的東西（local＝原始模型尺寸；不設成子物件，之後可以單獨移動）。</summary>
        private Transform PutOn(Transform host, string name, string model, Vector3 local, float yaw = 0f, bool collider = false, Transform parent = null)
        {
            if (host == null) return null;
            var t = CustomModel(name, model, host.TransformPoint(local), 0f, collider);
            if (t == null) return null;
            t.rotation = host.rotation * Quaternion.Euler(0f, yaw, 0f);
            if (parent != null) t.SetParent(parent, true);
            return t;
        }

        /// <summary>V2Shop 建好主要家具之後：拆出來的獨立小東西。</summary>
        private void V2ShopStylizedExtras()
        {
            if (!StylizedShopInUse) return;
            Transform Child(string n) { foreach (Transform t in Into) if (t.name == n) return t; return null; }
            var rack = Child("V2ClothesRack");
            PutOn(rack, "V2ClothesRackGarments", "v3_rack_clothes", Vector3.zero);
            var board = Child("V2IroningBoard");
            PutOn(board, "V2Iron", "v3_iron", new Vector3(-0.47f, 0.895f, 0f), 180f);
            var shelf = Child("V2FabricShelf");
            PutOn(shelf, "V2FabricShelfFabrics", "v3_shelf_fabrics", Vector3.zero);
            var stand = Child("V2Umbrellas");
            PutOn(stand, "V2UmbrellasSet", "v3_umbrellas", Vector3.zero);
            var bin = Child("V2FabricBin");
            PutOn(bin, "V2FabricBolts", "v3_fabric_bolts", Vector3.zero);
            var coat = Child("V2HangingCoat");
            PutOn(coat, "V2CoatPegboard", "v3_coat_pegboard", Vector3.zero);
            var wallShelf = Child("V2WallShelf");
            PutOn(wallShelf, "V2WallShelfRadio", "v3_radio", new Vector3(-0.3f, 1.43f, 0.13f));
            for (int i = 0; i < 3; i++) PutOn(wallShelf, "V2WallShelfJar", "v3_button_jar", new Vector3(0.04f + i * 0.11f, 1.43f, 0.12f));
            PutOn(wallShelf, "V2WallShelfCloth", "v3_folded_cloth", new Vector3(0.4f, 1.43f, 0.13f), 4f);
            // 櫃台上的東西：一件一件分開，放在一個跟著櫃台走的空物件底下（布局 A 會把櫃台和它們一起移到門口）。
            var counter = Child("V2Counter");
            if (counter != null)
            {
                var g = new GameObject("V2CounterClutter").transform;
                g.SetParent(Into, false);
                g.SetPositionAndRotation(counter.position, counter.rotation);
                g.localScale = counter.localScale;
                const float top = 0.95f;   // 新櫃台的桌面高度（原始尺寸）
                PutOn(counter, "V2CounterButtonDisplay", "v3_button_display", new Vector3(0.95f, top, 0.0f), 0f, false, g);
                PutOn(counter, "V2CounterJar", "v3_button_jar", new Vector3(0.16f, top, -0.18f), 0f, false, g);
                PutOn(counter, "V2CounterJar", "v3_button_jar", new Vector3(0.27f, top, -0.21f), 0f, false, g);
                PutOn(counter, "V2CounterJar", "v3_button_jar", new Vector3(0.23f, top, -0.08f), 0f, false, g);
                PutOn(counter, "V2CounterLedger", "v3_ledger", new Vector3(-0.15f, top, -0.02f), 4f, false, g);
                PutOn(counter, "V2CounterReceiptBook", "v3_receipt_book", new Vector3(-0.12f, top + 0.052f, 0.0f), 14f, false, g);
                PutOn(counter, "V2CounterTape", "v3_tape_measure", new Vector3(-0.36f, top, 0.17f), 30f, false, g);
                PutOn(counter, "V2CounterScissors", "v3_scissors", new Vector3(-0.42f, top, -0.16f), -20f, false, g);
                PutOn(counter, "V2CounterPencil", "v3_pencil", new Vector3(-0.2f, top, 0.21f), 70f, false, g);
                PutOn(counter, "V2CounterChalk", "v3_chalk", new Vector3(-0.27f, top, 0.25f), 10f, false, g);
                PutOn(counter, "V2CounterTeacup", "v3_teacup", new Vector3(-0.6f, top, 0.15f), 0f, false, g);
                PutOn(counter, "V2CounterBell", "v3_counter_bell", new Vector3(-0.62f, top, -0.12f), 0f, false, g);
                PutOn(counter, "V2CounterPinCushion", "v3_pin_cushion", new Vector3(0.08f, top, 0.16f), 0f, false, g);
                PutOn(counter, "V2CounterSpool", "v3_spool", new Vector3(0.04f, top, -0.2f), 0f, false, g);
                PutOn(counter, "V2CounterSpool", "v3_spool", new Vector3(0.0f, top, -0.12f), 0f, false, g);
            }
            // 可以搬的紙箱的來源（BuildShopInteractables 會把它疊到不能動的箱子上）。
            var boxes = Child("V2Boxes");
            if (boxes != null) CustomModel("V2MovableBoxSrc", "v3_box_movable", boxes.position + Vector3.up * 0.5f, boxes.eulerAngles.y, false);
        }

        // ---------------- 牆、地板、天花板 ----------------

        private Material surfaceFloor, surfaceWall, surfaceCeiling;

        private Material SurfaceToon(string tex, float metersPerTile)
        {
            var sh = Shader.Find("Shepherd/PainterlyReveal");
            var t = Resources.Load<Texture2D>("StyleShop/" + tex);
            if (sh == null || t == null) return null;
            var m = new Material(sh) { name = "V3_" + tex };
            m.SetTexture("_BaseMap", t);
            m.SetTextureScale("_BaseMap", Vector2.one / metersPerTile);
            m.SetFloat("_WorldUV", 1f);
            m.SetFloat("_ShadowStrength", 2.6f);   // 牆、天花板的暗面不要太黑（大面積）
            m.SetFloat("_AmbientFlatten", 1f);
            return m;
        }

        /// <summary>改衣店的地板換手繪木地板；牆和天花板在室內那一面貼一層手繪灰泥（只在店裡，不動走廊、餐廳）。</summary>
        private void V2ShopStylizedSurfaces()
        {
            if (!StylizedShopInUse) return;
            surfaceFloor = SurfaceToon("v3_floor_planks", 2.4f);
            surfaceWall = SurfaceToon("v3_wall_plaster", 3f);
            surfaceCeiling = SurfaceToon("v3_ceiling_plaster", 3f);
            if (surfaceFloor == null || surfaceWall == null || surfaceCeiling == null) return;
            foreach (Transform t in Into)
            {
                if (t.name == "V2FloorShop") { var r = t.GetComponent<Renderer>(); if (r != null) r.sharedMaterial = surfaceFloor; }
                if (t.name == "V2ShopFrontPlaster") { var r = t.GetComponent<Renderer>(); if (r != null) r.sharedMaterial = surfaceWall; }
            }
            float ceil = HP(0f, V2Ground, 0f).y - 0.25f;          // 一樓天花板下緣
            float floorY = V2Origin.y;
            float hgt = ceil - floorY;
            float xL = HP(0f, 0f, 0f).x + 0.125f + 0.004f;         // 左牆（外牆）室內面
            float xR = HP(V2Width, 0f, 0f).x - 0.125f - 0.004f;    // 右牆室內面
            float zB = HP(0f, 0f, 5f).z - 0.06f - 0.004f;          // 店面後牆（往走廊）室內面
            float zF = HP(0f, 0f, 0f).z + 0.125f;
            float x0 = HP(0f, 0f, 0f).x, x1 = HP(V2Width, 0f, 0f).x;
            void Skin(string n, Vector3 c, Vector3 s, Material m) { var b = Box(n, c, s, m, false); b.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            Skin("V3ShopWallL", new Vector3(xL, floorY + hgt / 2f, (zF + zB) / 2f), new Vector3(0.006f, hgt, zB - zF), surfaceWall);
            Skin("V3ShopWallR", new Vector3(xR, floorY + hgt / 2f, (zF + zB) / 2f), new Vector3(0.006f, hgt, zB - zF), surfaceWall);
            // 後牆：門洞（x 4.7～5.45、高 2.25×1.165）兩邊整面，門洞上方一段。
            float d0 = HP(4.7f, 0f, 0f).x, d1 = HP(5.45f, 0f, 0f).x, dh = 2.25f * 1.165f;
            Skin("V3ShopWallB", new Vector3((x0 + d0) / 2f, floorY + hgt / 2f, zB), new Vector3(d0 - x0, hgt, 0.006f), surfaceWall);
            Skin("V3ShopWallB", new Vector3((d1 + x1) / 2f, floorY + hgt / 2f, zB), new Vector3(x1 - d1, hgt, 0.006f), surfaceWall);
            Skin("V3ShopWallB", new Vector3((d0 + d1) / 2f, floorY + dh + (hgt - dh) / 2f, zB), new Vector3(d1 - d0, hgt - dh, 0.006f), surfaceWall);
            Skin("V3ShopCeiling", new Vector3((x0 + x1) / 2f, ceil - 0.004f, (zF + zB) / 2f), new Vector3(x1 - x0, 0.006f, zB - zF), surfaceCeiling);
        }
    }
}
