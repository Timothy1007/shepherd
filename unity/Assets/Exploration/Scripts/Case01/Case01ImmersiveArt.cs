using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// Phase 1.8：把客廳 v0.6／Phase 1 v0.7 的 Blender 模型與材質「依物件名稱、就地」套到 Phase 1.7 R2 格局（不是搬 V05A 的固定座標）。
    /// 舊的程序方塊一律用 ArtKit 隱藏（碰撞、熱區、H0～H3 會移動的物件保留），不會新舊兩份同時渲染。
    /// 每個房間有自己的牆面／地面處理與生活痕跡（依案件既有的母女生活，不新增劇情線索）。只在 Phase 1.8 QA 的沉浸版建立。
    /// </summary>
    public sealed class Case01ImmersiveArt
    {
        public Case01ArtKit Kit { get; private set; }
        public Transform Root { get; private set; }
        public readonly Dictionary<string, Transform> Pieces = new Dictionary<string, Transform>();
        /// <summary>每個房間牆面用的材質（測試確認房間之間不同）。</summary>
        public readonly Dictionary<Case01Room, Material> WallMaterial = new Dictionary<Case01Room, Material>();
        /// <summary>地毯／走廊長毯的範圍（腳步聲判斷）。</summary>
        public readonly List<Rect> Rugs = new List<Rect>();
        /// <summary>二樓的地毯（平台長毯、書房、臥室床邊）。</summary>
        public readonly List<Rect> RugsUp = new List<Rect>();
        /// <summary>被換皮而隱藏的舊物件（測試確認沒有新舊重疊）。</summary>
        public readonly List<Transform> Replaced = new List<Transform>();
        public int WallPieces { get; private set; }

        private Transform g1, g2, life;
        private Case01Palette palette;
        private const float Ceil1 = 2.795f, Ceil2 = 5.995f;

        public static Case01ImmersiveArt Build(Case01HouseLayout L, Case01Palette p)
        {
            if (L == null || L.Floorplan != Case01Floorplan.R2 || !Case01ArtKit.Available) return null;
            var a = new Case01ImmersiveArt { Kit = new Case01ArtKit(p), palette = p };
            a.g1 = L.Root.Find("GroundFloor");
            a.g2 = L.Root.Find("SecondFloor");
            a.life = L.Root.Find("LifeDetails");
            a.Root = ArenaArt.Group("ImmersiveArtR2", L.Root, Vector3.zero);
            a.Rooms();
            a.Hall();
            a.Living();
            a.Dining();
            a.Kitchen();
            a.Upstairs();
            a.Windows(L);
            Case01AreaArt.DressExterior(L.Exterior, a.Kit, a.Pieces);
            return a;
        }

        // ---------------- 工具 ----------------

        private static Transform N(Transform root, string name, Vector3? near = null)
        {
            Transform best = null;
            float bd = float.MaxValue;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != name) continue;
                float dd = near.HasValue ? (t.position - near.Value).sqrMagnitude : 0f;
                if (best == null || dd < bd)
                {
                    best = t;
                    bd = dd;
                }
            }
            return best;
        }

        private Transform Skin(string key, Transform target, string model, Vector3 local, float yaw, Vector3? scale = null, params string[] keep)
        {
            if (target == null) return null;
            Replaced.Add(target);
            var t = Kit.Skin(target, model, local, yaw, scale, keep);
            if (t != null) Pieces[key] = t;
            return t;
        }

        private Transform Add(string key, string model, Vector3 pos, float yaw, Vector3? scale = null)
        {
            var t = Kit.Add(model, Root, pos, yaw);
            if (t == null) return null;
            if (scale.HasValue) t.localScale = scale.Value;
            Pieces[key] = t;
            return t;
        }

        private void Hide(Transform t)
        {
            if (t == null) return;
            Replaced.Add(t);
            Kit.HideOld(t);
        }

        private Material Tint(string baseName, Color c)
        {
            var m = palette.Own(new Material(Kit.Get(baseName)) { name = baseName + "_tint" });
            m.color = c;
            return m;
        }

        /// <summary>小道具（只是外觀、跟著新美術開關）。</summary>
        private Transform Prop(Transform parent, Vector3 pos, Vector3 scale, Material m, string name, Vector3? euler = null, PrimitiveType type = PrimitiveType.Cube)
        {
            var t = ArenaArt.Prim(type, parent, pos, scale, m, euler, name, false, true);
            Kit.Register(t.gameObject);
            return t;
        }

        private void Room(Case01Room room, Transform walls, float x0, float x1, float z0, float z1, float y0, float ceil, Material wall, Material floor, float floorTile, params string[] extra)
        {
            WallMaterial[room] = wall;
            WallPieces += Kit.DressWalls(walls, Root, x0, x1, z0, z1, y0, ceil, wall, extra);
            Kit.Quad(Root, room + "Floor", new Vector3((x0 + x1) / 2, y0 + 0.003f, (z0 + z1) / 2), Vector3.up, Vector3.forward, x1 - x0, z1 - z0, floor, floorTile, new Vector2(x0, z0));
            Kit.Quad(Root, room + "Ceiling", new Vector3((x0 + x1) / 2, ceil - 0.004f, (z0 + z1) / 2), Vector3.down, Vector3.forward, x1 - x0, z1 - z0, Kit.Get("LR_Ceiling"), 1.5f, new Vector2(x0, z0));
            foreach (var (pos, yaw, len) in new[]
                     {
                         (new Vector3((x0 + x1) / 2, ceil, z0), 0f, x1 - x0), (new Vector3((x0 + x1) / 2, ceil, z1), 180f, x1 - x0),
                         (new Vector3(x0, ceil, (z0 + z1) / 2), 90f, z1 - z0), (new Vector3(x1, ceil, (z0 + z1) / 2), -90f, z1 - z0),
                     })
            {
                var c = Kit.Add("Cornice", Root, pos, yaw);
                if (c != null) c.localScale = new Vector3(len, 1f, 1f);
            }
        }

        // ---------------- 每個房間的牆／地 ----------------

        private void Rooms()
        {
            float hx = Case01HouseBuilder.R17.HallX, ez = Case01HouseBuilder.R17.ExtensionZ, lz = Case01HouseBuilder.R17.LivingZ, sx = Case01HouseBuilder.R17.StoreX;
            const float o = 0.075f;
            float y2 = Case01HouseBuilder.FloorHeight;
            // 玄關／走廊：暖赭色壁紙＋維多利亞式磁磚地（腳步較硬、較亮）。
            var hallTile = Kit.Tiled("P1_Tile", Vector2.one);
            hallTile.color = new Color(0.92f, 0.78f, 0.66f);
            Room(Case01Room.Entrance, g1, hx + o, 10f - o, o, ez - o, 0f, Ceil1, Tint("LR_Wallpaper", new Color(1f, 0.86f, 0.62f)), hallTile, 0.9f, "Bulkhead");
            // 客廳：原本的綠色壁紙、木地板（最「有人住」的房間）。
            Room(Case01Room.Living, g1, o, hx - o, o, lz - o, 0f, Ceil1, Kit.Get("LR_Wallpaper"), Kit.Get("LR_Floor"), 1.2f);
            // 餐廳：深酒紅壁紙、較深的地板（中段、沒有窗）。
            Room(Case01Room.Dining, g1, o, hx - o, lz + o, ez - o, 0f, Ceil1, Tint("LR_Wallpaper", new Color(0.78f, 0.42f, 0.4f)), Tint("LR_Floor", new Color(0.72f, 0.62f, 0.55f)), 1.2f);
            // 廚房：奶油色油漆牆、磁磚地。
            Room(Case01Room.Kitchen, g1, o, 10f - o, ez + o, Case01HouseBuilder.R17.Depth - o, 0f, Ceil1, Tint("LR_Paint", new Color(1f, 0.97f, 0.86f)), Kit.Get("P1_Tile"), 1.2f);
            // 二樓平台：灰藍油漆（比樓下冷）。
            Room(Case01Room.Landing, g2, hx + o, 10f - o, o, ez - o, y2, Ceil2, Tint("LR_Paint", new Color(0.72f, 0.78f, 0.86f)), Kit.Get("LR_Floor"), 1.2f);
            // 書房：深綠壁紙、深色地板。
            Room(Case01Room.Study, g2, sx + o, hx - o, lz + o, ez - o, y2, Ceil2, Tint("LR_Wallpaper", new Color(0.55f, 0.7f, 0.55f)), Tint("LR_Floor", new Color(0.62f, 0.52f, 0.44f)), 1.2f);
            // 臥室：淡玫瑰色壁紙。
            Room(Case01Room.Bedroom, g2, sx + o, hx - o, o, lz - o, y2, Ceil2, Tint("LR_Wallpaper", new Color(1f, 0.82f, 0.82f)), Kit.Get("LR_Floor"), 1.2f);
        }

        // ---------------- 玄關 ----------------

        private void Hall()
        {
            Hide(N(g1, "FrontDoor"));
            Hide(N(g1, "DoorLight"));
            Add("FrontDoor", "FrontDoorArt", new Vector3(Case01HouseBuilder.R17.FrontDoorX, 0f, 0.075f), 0f);
            Skin("HallBench", N(g1, "ShoeBench"), "HallBench", Vector3.zero, 0f);
            Skin("CoatRack", N(g1, "CoatHooks"), "CoatRack", Vector3.zero, 0f);
            Skin("ShoeCabinet", N(g1, "ShoeCabinet"), "ShoeCabinetArt", Vector3.zero, 0f);
            var stand = N(g1, "UmbrellaStand");
            if (stand != null)
            {
                Hide(stand);
                foreach (var u in g1.GetComponentsInChildren<Transform>(true))
                    if (u.name == "Umbrella" && (u.position - stand.position).sqrMagnitude < 1f) Hide(u);
                Add("UmbrellaStand", "UmbrellaStandArt", new Vector3(stand.position.x, 0f, stand.position.z), 0f);
            }
            Skin("UnderStairDoor", N(g1, "UnderStairCupboard"), "PanelDoor", Vector3.zero, 0f, new Vector3(0.8f / 0.82f, 1.95f / 1.98f, 1f));
            Skin("HallPainting", N(g1, "HallLandscape"), "PaintingFrame", new Vector3(0f, 0f, 0.02f), 180f, new Vector3(1.0f / 0.9f, 0.75f / 0.66f, 1f), "Sky", "Land", "Hill");
            var runner = N(g1, "HallRunner");
            if (runner != null) Rugs.Add(new Rect(runner.position.x - runner.localScale.x / 2, runner.position.z - runner.localScale.z / 2, runner.localScale.x, runner.localScale.z));
            // 生活痕跡：信件散在門墊上、一雙鞋踢在長凳下、走廊長毯中間踩出較深的一道。
            var paper = palette.Get("paper");
            Prop(Root, new Vector3(7.0f, 0.016f, 0.55f), new Vector3(0.22f, 0.004f, 0.11f), paper, "Letter", new Vector3(0f, 18f, 0f));
            Prop(Root, new Vector3(7.25f, 0.018f, 0.7f), new Vector3(0.24f, 0.004f, 0.12f), palette.Tint("paper", new Color(0.85f, 0.8f, 0.7f)), "Letter", new Vector3(0f, -24f, 0f));
            var shoe = palette.Own(ArenaArt.Mat(new Color(0.24f, 0.16f, 0.11f), 0.35f));
            Prop(Root, new Vector3(7.05f, 0.05f, 5.25f), new Vector3(0.11f, 0.1f, 0.27f), shoe, "Shoe", new Vector3(0f, 10f, 0f));
            Prop(Root, new Vector3(7.2f, 0.05f, 5.4f), new Vector3(0.11f, 0.1f, 0.27f), shoe, "Shoe", new Vector3(0f, -35f, 8f));
            Prop(Root, new Vector3(7.7f, 0.0135f, 6.6f), new Vector3(0.42f, 0.002f, 6.6f), palette.Own(ArenaArt.Mat(new Color(0.32f, 0.12f, 0.1f), 0.05f)), "RunnerWear");
        }

        // ---------------- 客廳 ----------------

        private void Living()
        {
            var fire = N(g1, "Fireplace");
            if (fire != null)
            {
                Skin("Mantelpiece", fire, "Mantelpiece", new Vector3(0f, 0f, 0.47f), 0f, null, "ChimneyBreast");
                var breast = fire.Find("ChimneyBreast");
                if (breast != null) Kit.Swap(breast.GetComponent<Renderer>(), Kit.Get("LR_Wallpaper"));
                var painting = Kit.Add("PaintingFrame", fire, new Vector3(0f, 1.86f, 0.475f), 0f);
                if (painting != null)
                {
                    var print = ArenaArt.Prim(PrimitiveType.Quad, painting, new Vector3(0f, 0f, 0.012f), new Vector3(0.76f, 0.52f, 1f), Kit.Get("LR_Painting"), new Vector3(0f, 180f, 0f), "PaintingPrint", false, false);
                    print.localScale = new Vector3(0.76f, 0.52f, 1f);
                }
                foreach (var n in new[] { "MantelClock", "Candle", "SmallVase" }) Hide(N(g1, n, fire.position));
                foreach (var (x, child) in new[] { (-0.32f, "MantelClock"), (-0.66f, "Candlestick"), (0.25f, "Candlestick") })
                {
                    var prop = Kit.Place("Props", fire, new Vector3(x, 1.14f, 0.58f), 0f, child);
                    if (prop != null) Kit.Register(prop.gameObject);
                }
            }
            Skin("Bookcase", N(g1, "LivingBookcase"), "Bookcase", Vector3.zero, 0f, null, "Book", "BookWithNote", "NoteTab", "SmallFrame");
            Skin("TvUnit", N(g1, "TvCorner"), "TvUnit", Vector3.zero, 0f);
            var sofa = Skin("Sofa", N(g1, "Sofa"), "Sofa", Vector3.zero, 0f);
            if (sofa != null)
            {
                foreach (var (pos, yaw) in new[] { (new Vector3(0.52f, 0.66f, -0.2f), 12f), (new Vector3(-0.42f, 0.64f, -0.22f), -8f) })
                {
                    var pl = Kit.Place("Pillow", sofa, pos, yaw);
                    if (pl != null) Kit.Register(pl.gameObject);
                }
                // 格紋毯從扶手滑下一半（坐過、起身時隨手一推）。
                var throwRug = ArenaArt.Prim(PrimitiveType.Cube, sofa, new Vector3(-0.75f, 0.58f, 0.15f), new Vector3(0.5f, 0.025f, 0.75f), Kit.Get("LR_Plaid"), new Vector3(0f, 8f, -18f), "PlaidThrow", false, true);
                Kit.Register(throwRug.gameObject);
            }
            var table = N(g1, "CoffeeTable");
            if (table != null)
            {
                Skin("CoffeeTable", table, "CoffeeTable", Vector3.zero, 0f);
                var card = ArenaArt.Prim(PrimitiveType.Cube, table, new Vector3(0.32f, 0.53f, -0.05f), new Vector3(0.16f, 0.12f, 0.005f), palette.Tint("paper", new Color(0.95f, 0.66f, 0.56f)), new Vector3(-12f, 160f, 0f), "FriendCardArt", false, false);
                Kit.Register(card.gameObject);
                foreach (var (pos, yaw, child) in new[] { (new Vector3(-0.28f, 0.46f, 0.1f), 30f, "TeaCup"), (new Vector3(-0.02f, 0.46f, -0.12f), 0f, "FlowerVase") })
                {
                    var prop = Kit.Place("Props", table, pos, yaw, child);
                    if (prop != null) Kit.Register(prop.gameObject);
                }
            }
            Skin("Armchair", N(g1, "Armchair"), "Armchair", Vector3.zero, 0f);
            var lamp = N(g1, "ReadingLamp");
            if (lamp != null)
            {
                Hide(lamp);
                Add("SideTable", "SideTable", new Vector3(lamp.position.x, 0f, lamp.position.z), -90f);
                Add("TableLamp", "TableLamp", new Vector3(lamp.position.x, 0.58f, lamp.position.z), 0f);
                var light = lamp.GetComponentInChildren<Light>();
                if (light != null) Kit.Retransform(light.transform, new Vector3(0f, 0.95f, 0f), light.transform.localScale);
                // 檯燈旁攤開扣著的書與老花眼鏡。
                var book = Kit.Place("Book", Root, new Vector3(lamp.position.x - 0.05f, 0.6f, lamp.position.z + 0.08f), 70f);
                if (book != null)
                {
                    book.localScale = new Vector3(0.05f, 0.24f, 0.17f);
                    book.localRotation = Quaternion.Euler(0f, 70f, 90f);
                    Kit.Register(book.gameObject);
                }
            }
            var rug = N(g1, "Rug");
            if (rug != null)
            {
                Hide(rug);
                Add("Rug", "Rug", new Vector3(rug.position.x, 0f, rug.position.z), 90f);
                Rugs.Add(new Rect(rug.position.x - 1.2f, rug.position.z - 1.4f, 2.4f, 2.8f));
            }
            int pi = 0;
            foreach (var plant in g1.GetComponentsInChildren<Transform>(true))
                if (plant.name == "Plant" && plant.parent == g1) Skin("Plant" + pi++, plant, "Plant", Vector3.zero, 0f);
            Skin("WallFrame", N(g1, "LivingFrame"), "WallFrame", Vector3.zero, 180f, null, "Photo", "BlankPrint");
        }

        // ---------------- 餐廳 ----------------

        private void Dining()
        {
            var table = N(g1, "DiningTable");
            if (table != null)
            {
                foreach (Transform c in table)
                    if (c.name == "Top" || c.name == "Apron" || c.name == "Leg") Hide(c);
                var model = Kit.Add("DiningTableArt", table, Vector3.zero, 0f);
                if (model != null) Pieces["DiningTable"] = model;
                foreach (var chair in new[] { "ChairSouth", "ChairNorth", "ChairWest", "ChairEast" })
                    Skin(chair, table.Find(chair), "DiningChairArt", Vector3.zero, 0f);
                // 東側的椅子被拉開、斜放（有人起身後沒推回去）。
                var east = table.Find("ChairEast");
                if (east != null) Kit.Retransform(east, east.localPosition + new Vector3(0.22f, 0f, 0.08f), east.localScale);
                if (east != null) east.localRotation = Quaternion.Euler(0f, -72f, 0f);
                // 桌上：麵包板與摺起的報紙（只在南側那副餐具旁）。
                Prop(table, new Vector3(-0.45f, 0.77f, -0.3f), new Vector3(0.3f, 0.02f, 0.2f), palette.Get("wood_warm"), "BreadBoard");
                Prop(table, new Vector3(0.5f, 0.765f, -0.25f), new Vector3(0.28f, 0.012f, 0.2f), palette.Get("paper"), "Newspaper", new Vector3(0f, -14f, 0f));
            }
            Skin("Sideboard", N(g1, "Sideboard"), "SideboardArt", Vector3.zero, 0f, null, "TeaTray", "ShoppingList", "Vase");
        }

        // ---------------- 廚房 ----------------

        private void Kitchen()
        {
            Hide(N(g1, "KitchenTiles"));
            var stove = N(g1, "Stove");
            foreach (var run in g1.GetComponentsInChildren<Transform>(true))
            {
                if (run.name != "KitchenRun") continue;
                var cab = run.Find("Cabinets");
                float len = cab != null ? cab.localScale.x : 1f;
                Hide(run);
                int count = Mathf.Max(1, Mathf.RoundToInt(len / 0.6f));
                float w = len / count;
                for (int i = 0; i < count; i++)
                {
                    var local = new Vector3(-len / 2 + w * (i + 0.5f), 0f, 0f);
                    if (stove != null && Mathf.Abs(run.InverseTransformPoint(stove.position).x - local.x) < 0.35f && Mathf.Abs(run.InverseTransformPoint(stove.position).z) < 0.4f) continue;
                    var m = Kit.Add("KitchenBase", run, local, 0f);
                    if (m != null) m.localScale = new Vector3(w / 0.6f, 1f, 1f);
                }
                var top = ArenaArt.Prim(PrimitiveType.Cube, run, new Vector3(0f, 0.885f, 0.01f), new Vector3(len + 0.02f, 0.04f, 0.62f), Kit.Tiled("P1_Worktop", new Vector2(len, 1f)), null, "WorktopArt", false, true);
                Kit.Register(top.gameObject);
            }
            Skin("RangeCooker", stove, "RangeCooker", Vector3.zero, 0f);
            var sink = N(g1, "Sink");
            if (sink != null)
            {
                Hide(sink);
                var b = Kit.Add("ButlerSink", sink, new Vector3(0f, 0.906f, 0.04f), 0f);
                if (b != null) Pieces["Sink"] = b;
            }
            Skin("WallCabinet", N(g1, "UpperCabinet"), "WallCabinet", Vector3.zero, 0f);
            Skin("Fridge", N(g1, "Fridge"), "FridgeArt", Vector3.zero, 0f, null, "FridgeNote", "Magnet");
            Add("Kettle", "Kettle", new Vector3(6.6f, 0.905f, Case01HouseBuilder.R17.Depth - 0.28f), 30f);
            var kt = N(g1, "KitchenTable");
            if (kt != null)
                foreach (Transform c in kt)
                    if (c.name == "KitchenChair") Skin("KitchenChair" + c.GetSiblingIndex(), c, "DiningChairArt", Vector3.zero, 0f);
            // 備餐到一半：砧板上切了一半的胡蘿蔔、掛在爐門把上的擦手巾、水槽邊兩個沒洗的杯子。
            float back = Case01HouseBuilder.R17.Depth - 0.3f;
            Prop(Root, new Vector3(3.3f, 0.92f, back), new Vector3(0.38f, 0.025f, 0.26f), palette.Get("wood_warm"), "ChoppingBoard", new Vector3(0f, 8f, 0f));
            var carrot = palette.Own(ArenaArt.Mat(new Color(0.9f, 0.45f, 0.12f), 0.3f));
            Prop(Root, new Vector3(3.25f, 0.95f, back), new Vector3(0.035f, 0.08f, 0.035f), carrot, "Carrot", new Vector3(0f, 0f, 90f), PrimitiveType.Capsule);
            Prop(Root, new Vector3(3.42f, 0.945f, back + 0.03f), new Vector3(0.03f, 0.02f, 0.03f), carrot, "CarrotSlice", null, PrimitiveType.Cylinder);
            if (stove != null) Prop(Root, stove.position + new Vector3(0.05f, 0.62f, -0.34f), new Vector3(0.28f, 0.34f, 0.01f), Kit.Get("LR_Linen"), "TeaTowel");
            var sinkPos = sink != null ? sink.position : new Vector3(2.4f, 0f, back);
            Prop(Root, sinkPos + new Vector3(0.45f, 0.95f, 0f), new Vector3(0.08f, 0.05f, 0.08f), palette.Get("ceramic"), "DirtyCup", null, PrimitiveType.Cylinder);
            Prop(Root, sinkPos + new Vector3(0.55f, 0.95f, 0.08f), new Vector3(0.08f, 0.05f, 0.08f), palette.Get("ceramic"), "DirtyCup", null, PrimitiveType.Cylinder);
        }

        // ---------------- 二樓 ----------------

        private void Upstairs()
        {
            float y = Case01HouseBuilder.FloorHeight;
            var desk = N(g2, "Desk");
            if (desk != null)
            {
                Hide(desk);
                Add("StudyDesk", "DeskArt", new Vector3(desk.position.x, y - 0.015f, desk.position.z), 180f);
            }
            var chair = N(g2, "DeskChair");
            if (chair != null)
            {
                Hide(chair);
                Add("StudyChair", "DeskChairArt", new Vector3(chair.position.x, y, chair.position.z - 0.1f), 0f);
            }
            Skin("BankersLamp", N(life, "DeskLamp"), "BankersLamp", Vector3.zero, 180f);
            var shelf = N(g2, "StudyShelf2");
            if (shelf != null)
            {
                Hide(shelf);
                var bc = Add("StudyShelf2", "Bookcase", new Vector3(shelf.position.x, y, shelf.position.z), 90f, new Vector3(1.6f / 1.3f, 2.0f / 1.9f, 1f));
                var rng = new System.Random(18);
                if (bc != null)
                    for (int row = 0; row < 3; row++)
                        for (float z = -0.7f; z < 0.7f; z += 0.06f + (float)rng.NextDouble() * 0.04f)
                        {
                            if (rng.NextDouble() < 0.15) continue;
                            var b = Kit.Place("Book", Root, new Vector3(shelf.position.x + 0.05f, y + 0.08f + row * 0.42f, shelf.position.z + z), 90f);
                            if (b == null) break;
                            b.localScale = new Vector3(0.04f, 0.24f + (float)rng.NextDouble() * 0.1f, 0.2f);
                            Kit.Register(b.gameObject);
                        }
            }
            var bedroom = N(life, "Bedroom");
            Hide(N(g2, "Bed"));
            var bed = N(g2, "Bed");
            if (bedroom != null)
            {
                foreach (var n in new[] { "Headboard", "HeadboardCap", "Pillow", "Duvet", "DuvetFold" })
                    foreach (var t in bedroom.GetComponentsInChildren<Transform>(true))
                        if (t.name == n) Hide(t);
                if (bed != null) Add("Bed", "BedArt", new Vector3(bed.position.x, y, bed.position.z), 180f);
                var letter = N(bedroom, "InvitationLetter");
                if (letter != null) letter.position = new Vector3(letter.position.x, y + 0.54f, letter.position.z);
                var wardrobe = N(bedroom, "Wardrobe");
                foreach (var t in bedroom.GetComponentsInChildren<Transform>(true))
                {
                    bool wardrobePart = t.name == "Wardrobe" || t.name == "WardrobeGap" || t.name == "WardrobeDoor" || t.name == "WardrobeDoorAjar" || t.name == "WardrobeCornice" ||
                                        t.name == "WardrobeBase" || t.name == "HangingClothes" || (t.name == "Handle" && wardrobe != null && (t.position - wardrobe.position).sqrMagnitude < 1.2f);
                    bool chairPart = t.name == "DeskChairSeat" || t.name == "DeskChairBack" || t.name == "DeskChairLeg" || t.name == "WritingTable";
                    if (wardrobePart || chairPart) Hide(t);
                }
                if (wardrobe != null) Add("Wardrobe", "WardrobeArt", new Vector3(wardrobe.position.x, y, wardrobe.position.z), 90f);
                var table = N(bedroom, "WritingTable");
                if (table != null)
                {
                    Add("WritingDesk", "OfficeDeskArt", new Vector3(table.position.x, y, table.position.z), 180f, new Vector3(0.8f / 1.5f, 0.96f, 0.5f / 0.7f));
                    Add("BedroomChair", "DiningChairArt", new Vector3(table.position.x, y, table.position.z - 0.5f), 0f);
                    // 椅背上搭著一件開襟毛衣。
                    Prop(Root, new Vector3(table.position.x, y + 0.82f, table.position.z - 0.72f), new Vector3(0.42f, 0.3f, 0.06f), Kit.Get("P1_CoatRed"), "Cardigan", new Vector3(-8f, 0f, 0f));
                }
            }
            var night = N(g2, "Nightstand");
            if (night != null)
            {
                Hide(night);
                Add("Nightstand", "NightstandArt", new Vector3(night.position.x, y, night.position.z), 180f);
            }
            var storage = N(g2, "StorageDoor");
            if (storage != null)
            {
                Hide(storage);
                Add("StorageDoor", "PanelDoor", new Vector3(Case01HouseBuilder.R17.StoreX + 0.075f, y, storage.position.z), 90f, new Vector3(0.85f / 0.82f, 2.2f / 1.98f, 1f));
            }
            // 書房：墨水瓶、攤開的書、揉掉的紙團。
            if (desk != null)
            {
                Prop(Root, desk.position + new Vector3(0.65f, 0.42f, -0.15f), new Vector3(0.05f, 0.06f, 0.05f), palette.Own(ArenaArt.Mat(new Color(0.05f, 0.06f, 0.12f), 0.85f)), "InkBottle", null, PrimitiveType.Cylinder);
                for (int i = 0; i < 3; i++)
                    Prop(Root, new Vector3(desk.position.x - 1.3f + i * 0.35f, y + 0.05f, desk.position.z - 0.9f - i * 0.2f), Vector3.one * 0.08f, palette.Get("paper"), "PaperBall", null, PrimitiveType.Sphere);
            }
            // 平台：洗衣籃放在臥室門邊。
            Prop(Root, new Vector3(6.85f, y + 0.22f, 2.2f), new Vector3(0.45f, 0.44f, 0.34f), palette.Get("wood_warm"), "LaundryBasket");
            Prop(Root, new Vector3(6.85f, y + 0.46f, 2.2f), new Vector3(0.4f, 0.05f, 0.3f), Kit.Get("LR_Linen"), "Laundry");
            foreach (var r in life.GetComponentsInChildren<Transform>(true))
                if (r.name == "Runner" || r.name == "StudyRug" || r.name == "BedsideRug")
                    RugsUp.Add(new Rect(r.position.x - Mathf.Abs(r.lossyScale.x) / 2, r.position.z - Mathf.Abs(r.lossyScale.z) / 2, Mathf.Abs(r.lossyScale.x), Mathf.Abs(r.lossyScale.z)));
        }

        // ---------------- 窗 ----------------

        private void Windows(Case01HouseLayout L)
        {
            foreach (var w in L.Root.GetComponentsInChildren<Transform>(true))
            {
                if (w.name != "Window" || w.position.x < -0.5f || w.position.x > 10.5f || w.position.z > 19f) continue;
                foreach (Transform c in w)
                    if (c.name != "Glass") Hide(c);
                float yaw = w.position.z > 5f ? 180f : 0f;
                var sash = Kit.Add("SashWindow", Root, w.position, yaw);
                if (sash != null) Pieces["Sash_" + Pieces.Count] = sash;
                bool lived = w.position.y > 2f || w.position.z < 1f;
                if (lived) Kit.Add("Curtains", Root, new Vector3(w.position.x, w.position.y > 2f ? Case01HouseBuilder.FloorHeight : 0f, w.position.z), yaw);
            }
        }
    }
}
