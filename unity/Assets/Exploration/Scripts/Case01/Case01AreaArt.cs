using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// v0.7 Demo Phase 1：事務所與街區拉齊到客廳 v0.6 的初步品質（同一套 Blender 程序模型與材質，見 <see cref="Case01ArtKit"/>）。
    /// 一樣用「換皮」：碰撞、熱區、出生點、可走範圍全部不動；新模型不帶碰撞。街區外觀不透露任何異常（沒有門形痕跡、沒有紫色紋路）。
    /// 外景（<see cref="DressExterior"/>）也套在 Demo 方案 A 屋內窗外看到的街景，讓窗外與實際街區一致。
    /// </summary>
    public sealed class Case01AreaArt
    {
        public Case01ArtKit Kit { get; private set; }
        public Transform Root { get; private set; }
        public readonly Dictionary<string, Transform> Pieces = new Dictionary<string, Transform>();
        public bool Active { get { return Kit != null && Kit.Active; } }

        public void SetActive(bool art)
        {
            if (Kit != null) Kit.SetActive(art);
        }

        private static IEnumerable<Transform> All(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) yield return t;
        }

        private static Transform N(Transform root, string name, Vector3? near = null)
        {
            Transform best = null;
            float bd = float.MaxValue;
            foreach (var t in All(root, name))
            {
                float d = near.HasValue ? (t.position - near.Value).sqrMagnitude : 0f;
                if (best == null || d < bd)
                {
                    best = t;
                    bd = d;
                }
            }
            return best;
        }

        private void Rec(string key, Transform t)
        {
            if (t != null) Pieces[key] = t;
        }

        // ---------------- 事務所 ----------------

        /// <summary>事務所：雙抽屜櫃書桌（委託資料、電話、筆電照放）、綠罩銀行家桌燈、皮革辦公椅與兩張皮墊會客椅、金屬檔案櫃、
        /// 四片嵌板門、木地板、壁紙、深色木護牆板、灰泥天花與線腳。</summary>
        public static Case01AreaArt Office(Case01DemoAreaLayout L, Case01Palette p)
        {
            if (L == null || !Case01ArtKit.Available) return null;
            var a = new Case01AreaArt { Kit = new Case01ArtKit(p) };
            var g = L.Root;
            var k = a.Kit;
            a.Root = ArenaArt.Group("OfficeArtV07", g, Vector3.zero);
            const float W = Case01OfficeBuilder.W, D = Case01OfficeBuilder.D, H = Case01OfficeBuilder.H;
            float x0 = 0.075f, x1 = W - 0.075f, z0 = 0.075f, z1 = D - 0.075f;

            // 牆／地／天花：與住宅同一套木地板與灰泥天花；壁紙用偏綠的色調和住宅區分。
            var paper = k.Tiled("LR_Wallpaper", Vector2.one);
            paper.color = new Color(0.78f, 0.9f, 0.8f);
            k.DressWalls(g, a.Root, x0, x1, z0, z1, 0f, H, paper, "Apron");
            k.Quad(a.Root, "OfficeFloor", new Vector3(W / 2, 0.003f, D / 2), Vector3.up, Vector3.forward, x1 - x0, z1 - z0, k.Get("LR_Floor"), 1.2f, new Vector2(x0, z0));
            k.Quad(a.Root, "OfficeCeiling", new Vector3(W / 2, H - 0.004f, D / 2), Vector3.down, Vector3.forward, x1 - x0, z1 - z0, k.Get("LR_Ceiling"), 1.5f, new Vector2(x0, z0));
            foreach (var (pos, yaw, len) in new[]
                     {
                         (new Vector3(W / 2, H, z0), 0f, x1 - x0), (new Vector3(W / 2, H, z1), 180f, x1 - x0),
                         (new Vector3(x0, H, D / 2), 90f, z1 - z0), (new Vector3(x1, H, D / 2), -90f, z1 - z0),
                     })
            {
                var c = k.Add("Cornice", a.Root, pos, yaw);
                if (c != null) c.localScale = new Vector3(len, 1f, 1f);
            }
            foreach (var t in All(g, "Wainscot")) k.Swap(t.GetComponent<Renderer>(), k.Get("LR_WoodDark"));
            foreach (var t in All(g, "WindowSill")) k.Swap(t.GetComponent<Renderer>(), k.Get("LR_Paint"));
            foreach (var t in All(g, "Mullion")) k.Swap(t.GetComponent<Renderer>(), k.Get("LR_Paint"));

            // 辦公桌：換成雙抽屜櫃書桌（坐的那一側朝北＝+z）；桌上的物件與熱區照舊。
            var desk = N(g, "Desk");
            if (desk != null)
            {
                a.Rec("Desk", k.Skin(desk, "DeskArt", Vector3.zero, 0f, null,
                    "Laptop", "LaptopScreen", "Telephone", "FileStack", "CaseFolder", "CaseTab", "CaseSheet", "DeskLamp"));
                a.Rec("DeskLamp", k.Skin(desk.Find("DeskLamp"), "BankersLamp", Vector3.zero, 0f));
            }
            a.Rec("DeskChair", k.Skin(N(g, "Chair", new Vector3(4.5f, 0f, 5.45f)), "DeskChairArt", Vector3.zero, 0f));
            int ci = 0;
            foreach (var near in new[] { new Vector3(3.95f, 0f, 3.55f), new Vector3(5.05f, 0f, 3.55f) })
            {
                var ch = k.Skin(N(g, "Chair", near), "DiningChairArt", Vector3.zero, 0f);
                if (ch != null) k.Recolor(ch, "LR_CushionRed", "P1_Leather");
                a.Rec("ClientChair" + (ci++), ch);
            }
            ci = 0;
            foreach (var cab in All(g, "FilingCabinet")) a.Rec("FilingCabinet" + (ci++), k.Skin(cab, "FilingCabinetArt", Vector3.zero, 0f));

            // 書櫃骨架與會客沙發換材質（形狀不變；不把客廳沙發搬過來）。
            var shelf = N(g, "Bookshelf");
            if (shelf != null)
                foreach (var r in shelf.GetComponentsInChildren<Renderer>(true))
                    if (r.name == "Back" || r.name == "Side" || r.name == "Shelf") k.Swap(r, k.Get(r.name == "Back" ? "LR_WoodDark" : "LR_WoodMid"));
            var sofa = N(g, "ClientSofa");
            if (sofa != null)
                foreach (var r in sofa.GetComponentsInChildren<Renderer>(true)) k.Swap(r, k.Get("P1_Leather"));
            var stand = N(g, "CoatStand");
            if (stand != null)
                foreach (var r in stand.GetComponentsInChildren<Renderer>(true))
                    k.Swap(r, k.Get(r.name == "Coat" ? "P1_Coat" : "LR_WoodDark"));

            // 出口門：四片嵌板門（門牌、磨砂玻璃與熱區照舊）。
            var door = N(g, "OfficeDoor");
            if (door != null)
            {
                k.HideOld(door);
                k.HideOld(N(g, "Knob"));
                var pd = k.Add("PanelDoor", a.Root, new Vector3(Case01OfficeBuilder.DoorX, 0f, 0.02f), 0f);
                if (pd != null) pd.localScale = new Vector3(1.1f / 0.82f, 2.2f / 1.98f, 1f);
                a.Rec("OfficeDoor", pd);
            }
            L.Art = a;
            return a;
        }

        // ---------------- 街區 ----------------

        /// <summary>街區：外景（<see cref="DressExterior"/>）＋十七號立面模組（一樓窗框、石窗台、門框、台階、腰線、屋簷）、
        /// 16／18 號與 17 號前院矮牆上的鑄鐵欄杆、路牌與告示桿換成鑄鐵材質。</summary>
        public static Case01AreaArt Street(Case01DemoAreaLayout L, Case01Palette p, Case01Floorplan plan)
        {
            if (L == null || L.Exterior == null || !Case01ArtKit.Available) return null;
            var a = new Case01AreaArt { Kit = new Case01ArtKit(p) };
            var g = L.Root;
            var k = a.Kit;
            a.Root = ArenaArt.Group("StreetArtV07", g, Vector3.zero);
            DressExterior(L.Exterior, k, a.Pieces);

            var h17 = g.Find("House17");
            if (h17 != null)
            {
                foreach (Transform t in h17)
                {
                    var r = t.GetComponent<Renderer>();
                    if (r == null) continue;
                    if (t.name == "Body") k.SwapTiled(r, "P1_Brick", 1.6f);
                    else if (t.name == "Roof") k.HideOld(t);
                    else if (t.name == "FrontDoor17") k.Swap(r, k.Get("P1_DoorGreen"));
                    else if (t.name == "Knob") k.Swap(r, k.Get("LR_Brass"));
                    else if (t.name == "DoorHead" || t.name == "Sill") k.HideOld(t);
                    else if (t.name == "Window17") k.Retransform(t, new Vector3(t.localPosition.x, 1.5f, -0.072f), new Vector3(0.88f, 1.08f, 0.004f));
                }
                var body17 = h17.Find("Body");
                if (body17 != null)
                    a.Rec("Roof17", GableRoof(k, h17, new Vector3(body17.localPosition.x, body17.localPosition.y + body17.localScale.y * 0.5f, body17.localPosition.z),
                        body17.localScale.x, body17.localScale.z, RoofPitch));
                // 十七號沒有二樓正面窗（屋內二樓正面就是第三扇門那面牆）；模組只有一樓與屋簷細節，二樓正面不放任何門形痕跡。
                a.Rec("Front17", k.Add(plan == Case01Floorplan.V05A ? "Front17A" : "Front17O", h17, new Vector3(5f, 0f, 0f), 180f));
            }

            // 前院矮牆上的鑄鐵欄杆（17 號留出入口；16、18 號整段）。
            float wz = Case01Exterior.PavementNorth + 0.1f;
            int ri = 0;
            foreach (var (from, to) in new[] { (-0.9f, 6.7f), (7.9f, 10.4f), (-11f, -1.6f), (11.6f, 21f) })
            {
                float len = to - from;
                int n = Mathf.Max(1, Mathf.CeilToInt(len / 2f));
                float seg = len / n;
                for (int i = 0; i < n; i++)
                {
                    var rl = k.Add("Railing", a.Root, new Vector3(from + seg * (i + 0.5f), 0.7f, wz), 0f);
                    if (rl != null) rl.localScale = new Vector3(seg / 2f, 1f, 1f);
                    if (i == 0) a.Rec("Railing" + (ri++), rl);
                }
            }
            foreach (var t in All(g, "NeighbourWall")) k.SwapTiled(t.GetComponent<Renderer>(), "P1_Brick", 1.6f);
            foreach (var name in new[] { "StreetSign", "PostalListing", "TaxiStop" })
            {
                var grp = g.Find(name);
                if (grp == null) continue;
                foreach (var r in grp.GetComponentsInChildren<Renderer>(true))
                    if (r.name == "Post") k.Swap(r, k.Get("LR_Iron"));
            }
            L.Art = a;
            return a;
        }

        public const float RoofPitch = 30f;

        /// <summary>
        /// 雙坡屋頂（取代舊的兩片斜方塊）：兩片石板瓦坡面（雙面，屋簷與山牆各出挑 0.22 m）＋兩端磚造山牆三角。
        /// eaveCenter＝屋身頂面中心（本地座標），w／d＝屋身寬（x）與深（z），屋脊沿 x。沒有碰撞。
        /// </summary>
        public static Transform GableRoof(Case01ArtKit k, Transform parent, Vector3 eaveCenter, float w, float d, float pitch)
        {
            float t = Mathf.Tan(pitch * Mathf.Deg2Rad), o = 0.22f, rh = d * 0.5f * t;
            float hw = w * 0.5f + o, hd = d * 0.5f + o, ey = -o * t;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var norms = new List<Vector3>();
            var slate = new List<int>();
            var brick = new List<int>();

            void Face(Vector3[] p, Vector2[] uv, List<int> tris)
            {
                var n = Vector3.Cross(p[1] - p[0], p[2] - p[0]).normalized;
                for (int side = 0; side < 2; side++)
                {
                    int b = verts.Count;
                    for (int i = 0; i < p.Length; i++)
                    {
                        verts.Add(p[i]);
                        uvs.Add(uv[i]);
                        norms.Add(side == 0 ? n : -n);
                    }
                    for (int i = 1; i + 1 < p.Length; i++)
                    {
                        if (side == 0) tris.AddRange(new[] { b, b + i, b + i + 1 });
                        else tris.AddRange(new[] { b, b + i + 1, b + i });
                    }
                }
            }

            float slope = Mathf.Sqrt(hd * hd + (rh - ey) * (rh - ey)) / 1.5f;
            foreach (int s in new[] { -1, 1 })
            {
                Face(new[] { new Vector3(-hw, ey, s * hd), new Vector3(hw, ey, s * hd), new Vector3(hw, rh, 0f), new Vector3(-hw, rh, 0f) },
                    new[] { new Vector2(-hw / 1.5f, 0f), new Vector2(hw / 1.5f, 0f), new Vector2(hw / 1.5f, slope), new Vector2(-hw / 1.5f, slope) }, slate);
                float gx = s * (w * 0.5f - 0.01f);
                Face(new[] { new Vector3(gx, 0f, -d * 0.5f), new Vector3(gx, 0f, d * 0.5f), new Vector3(gx, rh, 0f) },
                    new[] { new Vector2(-d * 0.5f / 1.6f, 0f), new Vector2(d * 0.5f / 1.6f, 0f), new Vector2(0f, rh / 1.6f) }, brick);
            }
            var mesh = new Mesh { name = "GableRoof" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(norms);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(slate, 0);
            mesh.SetTriangles(brick, 1);
            mesh.RecalculateBounds();
            var go = new GameObject("RoofArt");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = eaveCenter;
            go.layer = parent.gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { k.Get("P1_Slate"), k.Get("P1_Brick") };
            k.Register(go);
            return go.transform;
        }

        /// <summary>
        /// 外景換皮（街區與屋內窗外共用）：人行道石板、柏油路面、石路緣、磚牆、石板瓦屋頂、灰泥勒腳；
        /// 每戶聯排住宅加上立面模組（窗框、石窗台／過梁、凸窗、門框、台階、腰線、屋簷），亮著的窗玻璃對齊新窗框；
        /// 路燈換成維多利亞式鑄鐵燈柱（燈籠自發光）。不新增碰撞。
        /// </summary>
        public static void DressExterior(Case01Exterior.Result ex, Case01ArtKit k, Dictionary<string, Transform> pieces = null)
        {
            if (ex == null || ex.Root == null || k == null) return;
            var root = ex.Root;
            foreach (Transform t in root)
            {
                var r = t.GetComponent<Renderer>();
                if (r == null) continue;
                switch (t.name)
                {
                    case "Pavement":
                    case "PavementOpposite":
                    case "FrontPath":
                        k.SwapTiled(r, "P1_Pavement", 2.4f, true);
                        break;
                    case "Road":
                        k.SwapTiled(r, "P1_Road", 3f, true);
                        break;
                    case "Kerb":
                        k.SwapTiled(r, "P1_Stone", 1.2f);
                        break;
                    case "FrontWall":
                        k.SwapTiled(r, "P1_Brick", 1.6f);
                        break;
                }
            }
            int ti = 0;
            foreach (Transform h in root)
            {
                if (h.name != "Terrace") continue;
                var body = h.Find("Body");
                if (body == null) continue;
                float width = body.localScale.x + 0.1f, half = body.localScale.z * 0.5f;
                bool bay = false;
                foreach (Transform c in h)
                {
                    var r = c.GetComponent<Renderer>();
                    if (r == null) continue;
                    switch (c.name)
                    {
                        case "Body":
                        case "Chimney":
                            k.SwapTiled(r, "P1_Brick", 1.6f);
                            break;
                        case "Roof":
                            k.HideOld(c);
                            break;
                        case "Plinth":
                            k.SwapTiled(r, "P1_Render", 1.5f);
                            break;
                        case "Door":
                            k.Swap(r, k.Get(ti % 3 == 1 ? "P1_DoorGreen" : "LR_WoodDark"));
                            break;
                        case "DoorHead":
                        case "Sill":
                        case "Lintel":
                        case "Mullion":
                            k.HideOld(c);
                            break;
                    }
                    if (c.name == "Window" && c.localPosition.y < 3f && c.localScale.x > 1.4f) bay = true;
                }
                float eave = body.localPosition.y + body.localScale.y * 0.5f;
                GableRoof(k, h, new Vector3(0f, eave, 0f), body.localScale.x, body.localScale.z, RoofPitch);
                var chimney = h.Find("Chimney");
                if (chimney != null)
                {
                    float ridge = eave + body.localScale.z * 0.5f * Mathf.Tan(RoofPitch * Mathf.Deg2Rad);
                    k.Retransform(chimney, new Vector3(chimney.localPosition.x, ridge + 0.25f, chimney.localPosition.z), chimney.localScale);
                }
                if (width > 9f) continue; // 只有選用的十七號立面（街區另外處理）。
                float sx = width / 6.2f;
                foreach (Transform c in h)
                {
                    if (c.name != "Window") continue;
                    var lp = c.localPosition;
                    if (lp.y < 3f)
                    {
                        if (bay) k.Retransform(c, new Vector3(lp.x, 1.3f, -half - 0.548f), new Vector3(1.5f * sx, 1.4f, 0.004f));
                        else k.Retransform(c, new Vector3(lp.x, 1.5f, -half - 0.072f), new Vector3(0.88f * sx, 1.08f, 0.004f));
                    }
                    else k.Retransform(c, new Vector3(lp.x, 4.4f, -half - 0.072f), new Vector3(0.83f * sx, 1.13f, 0.004f));
                }
                var front = k.Add(bay ? "TerraceFrontBay" : "TerraceFront", h, new Vector3(0f, 0f, -half), 180f);
                if (front != null) front.localScale = new Vector3(sx, 1f, 1f);
                if (pieces != null && ti == 0) pieces["TerraceFront"] = front;
                ti++;
            }
            int li = 0;
            foreach (var lamp in ex.Lamps)
            {
                var lp = k.Skin(lamp, "LampPost", Vector3.zero, 0f, null, "Pool");
                if (pieces != null && li == 0) pieces["LampPost"] = lp;
                li++;
            }
        }
    }
}
