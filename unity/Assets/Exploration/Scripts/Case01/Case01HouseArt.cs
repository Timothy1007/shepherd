using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// v0.7 Demo Phase 1：方案 A 住宅其他房間拉齊到客廳 v0.6 的初步品質（玄關、廚房、餐廳、書房、艾琳臥室、樓梯平台）。
    /// 全部用「換皮」：舊物件的碰撞、熱區、H0～H3 會移動／消失的物件（北側餐椅、第二副餐具、托盤第二杯、布巾）照舊，
    /// 只把舊的 Primitive 外觀換成 Blender 程序模型，並給牆／地／天花一致的材質。只在客廳美術 QA 與 Demo（方案 A）建立。
    /// </summary>
    public sealed class Case01HouseArt
    {
        public Case01ArtKit Kit { get; private set; }
        public Transform Root { get; private set; }
        public readonly Dictionary<string, Transform> Pieces = new Dictionary<string, Transform>();
        public bool Active { get { return Kit != null && Kit.Active; } }

        private Transform g1, g2, life;

        public static Case01HouseArt Build(Case01HouseLayout layout, Case01Palette p)
        {
            if (layout == null || layout.Floorplan != Case01Floorplan.V05A || !Case01ArtKit.Available) return null;
            var a = new Case01HouseArt { Kit = new Case01ArtKit(p) };
            a.g1 = layout.Root.Find("GroundFloor");
            a.g2 = layout.Root.Find("SecondFloor");
            a.life = layout.Root.Find("LifeDetails");
            a.Root = ArenaArt.Group("HouseArtV07", layout.Root, Vector3.zero);
            a.Hall();
            a.Kitchen();
            a.Dining();
            a.Upstairs();
            // 窗外的街景與 Demo 街區同一套外景美術。
            Case01AreaArt.DressExterior(layout.Exterior, a.Kit, a.Pieces);
            layout.HouseArt = a;
            return a;
        }

        public void SetActive(bool art) { Kit.SetActive(art); }

        // ---------------- 工具 ----------------

        /// <summary>依名稱找物件（有多個時取離 near 最近的）。</summary>
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

        private void Rec(string key, Transform t)
        {
            if (t != null) Pieces[key] = t;
        }

        private void Floor(string name, float x0, float x1, float z0, float z1, float y, Material m, float tile)
        {
            Kit.Quad(Root, name, new Vector3((x0 + x1) / 2, y + 0.003f, (z0 + z1) / 2), Vector3.up, Vector3.forward, x1 - x0, z1 - z0, m, tile, new Vector2(x0, z0));
        }

        private void Ceiling(float x0, float x1, float z0, float z1, float y)
        {
            Kit.Quad(Root, "CeilingDress", new Vector3((x0 + x1) / 2, y - 0.004f, (z0 + z1) / 2), Vector3.down, Vector3.forward, x1 - x0, z1 - z0, Kit.Get("LR_Ceiling"), 1.5f, new Vector2(x0, z0));
        }

        private void Cornice(float x0, float x1, float z0, float z1, float y)
        {
            foreach (var (pos, yaw, len) in new[]
                     {
                         (new Vector3((x0 + x1) / 2, y, z0), 0f, x1 - x0), (new Vector3((x0 + x1) / 2, y, z1), 180f, x1 - x0),
                         (new Vector3(x0, y, (z0 + z1) / 2), 90f, z1 - z0), (new Vector3(x1, y, (z0 + z1) / 2), -90f, z1 - z0),
                     })
            {
                var c = Kit.Add("Cornice", Root, pos, yaw);
                if (c != null) c.localScale = new Vector3(len, 1f, 1f);
            }
        }

        // ---------------- 玄關 ----------------

        private void Hall()
        {
            float x0 = Case01HouseBuilder.V05A.HallX0 + 0.075f, x1 = Case01HouseBuilder.V05A.HallX1 - 0.075f, z1 = Case01HouseBuilder.V05A.HallZ1 - 0.075f;
            Kit.HideOld(N(g1, "FrontDoor"));
            Kit.HideOld(N(g1, "DoorLight"));
            Rec("FrontDoor", Kit.Add("FrontDoorArt", Root, new Vector3(7.1f, 0f, 0.075f), 0f));
            Rec("HallBench", Kit.Skin(N(g1, "ShoeBench"), "HallBench", Vector3.zero, 0f));
            Rec("CoatRack", Kit.Skin(N(g1, "CoatHooks"), "CoatRack", Vector3.zero, 0f));
            Rec("ShoeCabinet", Kit.Skin(N(g1, "ShoeCabinet"), "ShoeCabinetArt", Vector3.zero, 0f));
            var stand = N(g1, "UmbrellaStand");
            if (stand != null)
            {
                Kit.HideOld(stand);
                foreach (var u in g1.GetComponentsInChildren<Transform>(true))
                    if (u.name == "Umbrella" && (u.position - stand.position).sqrMagnitude < 1f) Kit.HideOld(u);
                Rec("UmbrellaStand", Kit.Add("UmbrellaStandArt", Root, new Vector3(stand.position.x, 0f, stand.position.z), 0f));
            }
            Rec("UnderStairDoor", Kit.Skin(N(g1, "UnderStairCupboard"), "PanelDoor", new Vector3(0f, 0f, 0.0f), 0f, new Vector3(0.8f / 0.82f, 1.95f / 1.98f, 1f)));
            Rec("HallPainting", Kit.Skin(N(g1, "HallLandscape"), "PaintingFrame", new Vector3(0f, 0f, 0.02f), 180f, new Vector3(1.2f / 0.9f, 0.8f / 0.66f, 1f), "Sky", "Land", "Hill"));
            Kit.DressWalls(g1, Root, x0, x1, 0.075f, z1, 0f, Case01LivingArt.CeilingY, Kit.Get("LR_Wallpaper"));
            Floor("HallFloor", x0, x1, 0.075f, z1, 0f, Kit.Get("LR_Floor"), 1.2f);
            Ceiling(x0, x1, 0.075f, z1, Case01LivingArt.CeilingY);
            Cornice(x0, x1, 0.075f, z1, Case01LivingArt.CeilingY);
        }

        // ---------------- 廚房 ----------------

        private void Kitchen()
        {
            float x0 = Case01HouseBuilder.V05A.HallX0 + 0.075f, z0 = Case01HouseBuilder.V05A.HallZ1 + 0.075f;
            var tiles = N(g1, "KitchenTiles");
            if (tiles != null) Kit.HideOld(tiles);
            Floor("KitchenFloor", x0, 9.925f, z0, 9.925f, 0f, Kit.Get("P1_Tile"), 1.2f);
            Kit.DressWalls(g1, Root, x0, 9.925f, z0, 9.925f, 0f, Case01LivingArt.CeilingY, Kit.Get("LR_Paint"));
            Ceiling(x0, 9.925f, z0, 9.925f, Case01LivingArt.CeilingY);
            var stove = N(g1, "Stove");
            foreach (var run in g1.GetComponentsInChildren<Transform>(true))
            {
                if (run.name != "KitchenRun") continue;
                var cab = run.Find("Cabinets");
                float len = cab != null ? cab.localScale.x : 1f;
                Kit.HideOld(run);
                int count = Mathf.Max(1, Mathf.RoundToInt(len / 0.6f));
                float w = len / count;
                for (int i = 0; i < count; i++)
                {
                    var local = new Vector3(-len / 2 + w * (i + 0.5f), 0f, 0f);
                    // 爐台的位置不放下櫃（爐台自己有爐身）。
                    if (stove != null && Mathf.Abs(run.InverseTransformPoint(stove.position).x - local.x) < 0.35f && Mathf.Abs(run.InverseTransformPoint(stove.position).z) < 0.4f) continue;
                    var m = Kit.Add("KitchenBase", run, local, 0f);
                    if (m != null) m.localScale = new Vector3(w / 0.6f, 1f, 1f);
                }
                var top = ArenaArt.Prim(PrimitiveType.Cube, run, new Vector3(0f, 0.885f, 0.01f), new Vector3(len + 0.02f, 0.04f, 0.62f), Kit.Tiled("P1_Worktop", new Vector2(len, 1f)),
                    null, "WorktopArt", false, true);
                Kit.Register(top.gameObject);
            }
            Rec("RangeCooker", Kit.Skin(stove, "RangeCooker", Vector3.zero, 0f));
            var sink = N(g1, "Sink");
            if (sink != null)
            {
                Kit.HideOld(sink);
                Rec("Sink", Kit.Add("ButlerSink", sink, new Vector3(0f, 0.906f, 0.04f), 0f));
            }
            Rec("WallCabinet", Kit.Skin(N(g1, "UpperCabinet"), "WallCabinet", Vector3.zero, 0f));
            Rec("Fridge", Kit.Skin(N(g1, "Fridge"), "FridgeArt", Vector3.zero, 0f, null, "FridgeNote", "Magnet"));
            Rec("Kettle", Kit.Add("Kettle", Root, new Vector3(8.45f, 0.905f, 9.72f), 30f));
        }

        // ---------------- 餐廳 ----------------

        private void Dining()
        {
            float x0 = 0.075f, x1 = Case01HouseBuilder.V05A.HallX0 - 0.075f, z0 = Case01HouseBuilder.V05A.LivingZ1 + 0.075f;
            Kit.DressWalls(g1, Root, x0, x1, z0, 9.925f, 0f, Case01LivingArt.CeilingY, Kit.Get("LR_Wallpaper"));
            Floor("DiningFloor", x0, x1, z0, 9.925f, 0f, Kit.Get("LR_Floor"), 1.2f);
            Ceiling(x0, x1, z0, 9.925f, Case01LivingArt.CeilingY);
            Cornice(x0, x1, z0, 9.925f, Case01LivingArt.CeilingY);
            var table = N(g1, "DiningTable");
            if (table != null)
            {
                foreach (Transform c in table)
                    if (c.name == "Top" || c.name == "Apron" || c.name == "Leg") Kit.HideOld(c);
                Rec("DiningTable", Kit.Add("DiningTableArt", table, Vector3.zero, 0f));
                foreach (var chair in new[] { "ChairSouth", "ChairNorth", "ChairWest", "ChairEast" })
                    Rec(chair, Kit.Skin(table.Find(chair), "DiningChairArt", Vector3.zero, 0f));
            }
            Rec("Sideboard", Kit.Skin(N(g1, "Sideboard"), "SideboardArt", Vector3.zero, 0f, null, "TeaTray", "ShoppingList", "Vase"));
        }

        // ---------------- 二樓 ----------------

        private void Upstairs()
        {
            float y = Case01HouseBuilder.FloorHeight;
            // 書房：雙抽屜櫃書桌（桌面高度與舊桌相同，地圖／手記照放）、皮椅、綠罩桌燈。
            Kit.HideOld(N(g2, "Desk"));
            Rec("StudyDesk", Kit.Add("DeskArt", Root, new Vector3(7.5f, y - 0.015f, 9.4f), 180f));
            Kit.HideOld(N(g2, "DeskChair"));
            Rec("StudyChair", Kit.Add("DeskChairArt", Root, new Vector3(7.5f, y, 8.6f), 0f));
            var lamp = N(life, "DeskLamp");
            Rec("BankersLamp", Kit.Skin(lamp, "BankersLamp", Vector3.zero, 180f));
            Floor("StudyFloor", 5.075f, 9.925f, 5.075f, 9.925f, y, Kit.Get("LR_Floor"), 1.2f);
            // 艾琳臥室：床（床頭板、床尾板、床墊、枕頭、被子）、衣櫃、床頭櫃、小書桌＋椅。
            var bedroom = life.Find("Bedroom");
            Kit.HideOld(N(g2, "Bed"));
            foreach (var n in new[] { "Headboard", "HeadboardCap", "Pillow", "Duvet", "DuvetFold" })
                foreach (var t in bedroom.GetComponentsInChildren<Transform>(true))
                    if (t.name == n) Kit.HideOld(t);
            Rec("Bed", Kit.Add("BedArt", Root, new Vector3(2.4f, y, 8.6f), 180f));
            var letter = N(bedroom, "InvitationLetter");
            if (letter != null) letter.position = new Vector3(letter.position.x, y + 0.54f, letter.position.z);
            Kit.HideOld(N(g2, "Nightstand"));
            Rec("Nightstand", Kit.Add("NightstandArt", Root, new Vector3(1.1f, y, 9.55f), 180f));
            foreach (var t in bedroom.GetComponentsInChildren<Transform>(true))
            {
                bool wardrobePart = t.name == "Wardrobe" || t.name == "WardrobeGap" || t.name == "WardrobeDoor" || t.name == "WardrobeDoorAjar" || t.name == "WardrobeCornice" ||
                                    t.name == "WardrobeBase" || t.name == "HangingClothes" || (t.name == "Handle" && t.position.x < 1.3f && t.position.z < 7f);
                bool chairPart = t.name == "DeskChairSeat" || t.name == "DeskChairBack" || t.name == "DeskChairLeg" || t.name == "WritingTable";
                if (wardrobePart || chairPart) Kit.HideOld(t);
            }
            Rec("Wardrobe", Kit.Add("WardrobeArt", Root, new Vector3(0.4f, y, 6.0f), 90f));
            var desk = Kit.Add("OfficeDeskArt", Root, new Vector3(4.3f, y, 9.45f), 180f);
            if (desk != null) desk.localScale = new Vector3(0.8f / 1.5f, 0.96f, 0.5f / 0.7f);
            Rec("WritingDesk", desk);
            Rec("BedroomChair", Kit.Add("DiningChairArt", Root, new Vector3(4.3f, y, 8.95f), 0f));
            Floor("BedroomFloor", 0.075f, 4.925f, 5.075f, 9.925f, y, Kit.Get("LR_Floor"), 1.2f);
            // 樓梯平台：儲藏室門換成嵌板門（文案已是劇內的「門鎖住了」）。
            var storage = N(g2, "StorageDoor");
            if (storage != null)
            {
                Kit.HideOld(storage);
                var door = Kit.Add("PanelDoor", Root, new Vector3(5.075f, y, 2.5f), 90f);
                if (door != null) door.localScale = new Vector3(1.1f / 0.82f, 2.2f / 1.98f, 1f);
                Rec("StorageDoor", door);
            }
        }
    }
}
