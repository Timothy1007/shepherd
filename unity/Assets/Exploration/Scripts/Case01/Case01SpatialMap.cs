using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// Phase 1.6：從房屋「實際的碰撞體」畫出俯視灰盒平面圖（牆、家具、樓梯、窗、調查點），
    /// 並計算某個眼位／視角實際看得到哪些地面與焦點（視線用 Physics.Linecast，只算碰撞體；沒有碰撞的小裝飾不算遮擋）。
    /// QA 小地圖與截圖報表共用。
    /// </summary>
    public static class Case01SpatialMap
    {
        public const float X0 = -0.5f, Z0 = -0.6f, Size = 11.2f;
        /// <summary>可見地面格的間距（公尺）與取樣高度（地板上方，約桌面高）。</summary>
        public const float Cell = 0.25f, SampleHeight = 1.0f;
        public const float MaxSight = 14f;

        public static readonly Color32 Background = new Color32(24, 26, 31, 255);
        public static readonly Color32 WallColor = new Color32(222, 222, 216, 255);
        public static readonly Color32 LiningColor = new Color32(250, 214, 120, 255);
        public static readonly Color32 FurnitureColor = new Color32(120, 126, 138, 255);
        public static readonly Color32 DoorColor = new Color32(150, 104, 70, 255);
        public static readonly Color32 StairColor = new Color32(176, 150, 116, 255);
        public static readonly Color32 WindowColor = new Color32(110, 180, 240, 255);
        public static readonly Color32 HotspotColor = new Color32(255, 170, 60, 255);

        private static readonly Dictionary<Case01Room, Color32> roomColors = new Dictionary<Case01Room, Color32>
        {
            { Case01Room.Entrance, new Color32(62, 58, 44, 255) },
            { Case01Room.Living, new Color32(44, 62, 48, 255) },
            { Case01Room.Dining, new Color32(66, 50, 40, 255) },
            { Case01Room.Kitchen, new Color32(40, 54, 68, 255) },
            { Case01Room.Landing, new Color32(62, 58, 44, 255) },
            { Case01Room.Study, new Color32(56, 46, 66, 255) },
            { Case01Room.Bedroom, new Color32(68, 46, 54, 255) },
            { Case01Room.Storage, new Color32(36, 36, 38, 255) },
        };

        public static bool HouseRoom(Case01Room r)
        {
            return r != Case01Room.None && r != Case01Room.Mezzanine && r != Case01Room.Courtyard;
        }

        public static int FloorOf(float y) { return y > 2.4f ? 1 : 0; }

        public static float FloorY(int floor) { return floor * Case01HouseBuilder.FloorHeight; }

        public static int Pixels(int ppm) { return Mathf.RoundToInt(Size * ppm); }

        public static Vector2 ToMap(Vector3 world, float pixelsPerMetre)
        {
            return new Vector2((world.x - X0) * pixelsPerMetre, (world.z - Z0) * pixelsPerMetre);
        }

        /// <summary>只算房屋本體（不含窗外街景與兩個異常空間）的碰撞體與熱區。</summary>
        private static bool InHouse(Vector3 c)
        {
            return c.x > X0 && c.x < X0 + Size && c.z > Z0 && c.z < Z0 + Size && c.y > -0.5f && c.y < 6.3f;
        }

        /// <summary>某一層（0＝一樓、1＝二樓）的俯視平面：房間底色、碰撞體（牆／家具／樓梯／門框深度）、窗、調查點。北方朝上。</summary>
        public static Texture2D Plan(Case01HouseLayout L, int floor, int ppm)
        {
            int n = Pixels(ppm);
            var px = new Color32[n * n];
            for (int i = 0; i < px.Length; i++) px[i] = Background;
            float fy = FloorY(floor);
            // 倒著畫：清單前面的房間（RoomAt 優先判定的，例如 C 的短走廊）最後上色。
            for (int i = L.Rooms.Count - 1; i >= 0; i--)
            {
                var r = L.Rooms[i];
                if (!HouseRoom(r.Room) || FloorOf(r.Bounds.center.y) != floor) continue;
                Color32 c;
                if (!roomColors.TryGetValue(r.Room, out c)) continue;
                FillRect(px, n, ppm, r.Bounds.min.x, r.Bounds.min.z, r.Bounds.max.x, r.Bounds.max.z, c);
            }
            // 碰撞體：只畫會擋到人的高度帶（地板上 0.3～1.0 m），所以門洞上方的楣不會蓋住門洞，樓板與天花也不會。
            var boxes = L.Root.GetComponentsInChildren<BoxCollider>(false);
            var order = new List<KeyValuePair<int, BoxCollider>>();
            foreach (var b in boxes)
            {
                if (!b.enabled || b.isTrigger || b.name.StartsWith("PortalBackstop")) continue;
                var bb = b.bounds;
                if (!InHouse(bb.center) || bb.min.y > fy + 1.0f || bb.max.y < fy + 0.3f) continue;
                order.Add(new KeyValuePair<int, BoxCollider>(Rank(b.name), b));
            }
            order.Sort((a, b) => a.Key.CompareTo(b.Key));
            foreach (var kv in order) FillBox(px, n, ppm, kv.Value, ColorFor(kv.Value.name));
            // 窗（窗洞位置的藍線）。
            foreach (var t in L.Root.GetComponentsInChildren<Transform>(false))
            {
                if (t.name != "Window" || FloorOf(t.position.y) != floor || !InHouse(t.position)) continue;
                var right = t.right * 0.5f;
                FillQuad(px, n, ppm, new Vector2(t.position.x, t.position.z), new Vector2(right.x, right.z), new Vector2(-right.z, right.x).normalized * 0.06f, WindowColor);
            }
            foreach (var it in L.Interactables.Values)
            {
                if (it == null || !it.gameObject.activeInHierarchy) continue;
                var p = it.transform.position;
                if (!InHouse(p) || FloorOf(p.y - 0.2f) != floor) continue;
                FillDisc(px, n, ppm, p.x, p.z, 0.09f, HotspotColor);
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Case01SpatialPlan" + floor };
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        private static int Rank(string name)
        {
            if (name == "StairRamp") return 0;
            if (IsWall(name)) return 2;
            return 1;
        }

        private static bool IsWall(string name)
        {
            return name == "Wall" || name == "WindowApron" || name == "ChimneyBreast" || name.StartsWith("PortalSeal");
        }

        private static Color32 ColorFor(string name)
        {
            if (name == "StairRamp") return StairColor;
            if (name.StartsWith("Lining_")) return LiningColor;
            if (IsWall(name)) return WallColor;
            if (name == "FrontDoor" || name == "StorageDoor") return DoorColor;
            return FurnitureColor;
        }

        private static void FillBox(Color32[] px, int n, int ppm, BoxCollider b, Color32 color)
        {
            var t = b.transform;
            var c = t.TransformPoint(b.center);
            var ax = t.TransformVector(new Vector3(b.size.x * 0.5f, 0f, 0f));
            var az = t.TransformVector(new Vector3(0f, 0f, b.size.z * 0.5f));
            FillQuad(px, n, ppm, new Vector2(c.x, c.z), new Vector2(ax.x, ax.z), new Vector2(az.x, az.z), color);
        }

        /// <summary>以中心與兩個（互相垂直的）半軸填滿一個旋轉矩形。</summary>
        private static void FillQuad(Color32[] px, int n, int ppm, Vector2 c, Vector2 a, Vector2 b, Color32 color)
        {
            float la = Mathf.Max(a.magnitude, 0.5f / ppm), lb = Mathf.Max(b.magnitude, 0.5f / ppm);
            var ua = a.sqrMagnitude > 1e-8f ? a.normalized : new Vector2(-b.y, b.x).normalized;
            var ub = b.sqrMagnitude > 1e-8f ? b.normalized : new Vector2(-a.y, a.x).normalized;
            float ex = Mathf.Abs(ua.x) * la + Mathf.Abs(ub.x) * lb, ez = Mathf.Abs(ua.y) * la + Mathf.Abs(ub.y) * lb;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((c.x - ex - X0) * ppm)), x1 = Mathf.Min(n - 1, Mathf.CeilToInt((c.x + ex - X0) * ppm));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((c.y - ez - Z0) * ppm)), z1 = Mathf.Min(n - 1, Mathf.CeilToInt((c.y + ez - Z0) * ppm));
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                var d = new Vector2(X0 + (x + 0.5f) / ppm, Z0 + (z + 0.5f) / ppm) - c;
                if (Mathf.Abs(Vector2.Dot(d, ua)) <= la && Mathf.Abs(Vector2.Dot(d, ub)) <= lb) px[z * n + x] = color;
            }
        }

        private static void FillRect(Color32[] px, int n, int ppm, float xa, float za, float xb, float zb, Color32 color)
        {
            FillQuad(px, n, ppm, new Vector2((xa + xb) * 0.5f, (za + zb) * 0.5f), new Vector2((xb - xa) * 0.5f, 0f), new Vector2(0f, (zb - za) * 0.5f), color);
        }

        private static void FillDisc(Color32[] px, int n, int ppm, float cx, float cz, float r, Color32 color)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt((cx - r - X0) * ppm)), x1 = Mathf.Min(n - 1, Mathf.CeilToInt((cx + r - X0) * ppm));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((cz - r - Z0) * ppm)), z1 = Mathf.Min(n - 1, Mathf.CeilToInt((cz + r - Z0) * ppm));
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = X0 + (x + 0.5f) / ppm - cx, dz = Z0 + (z + 0.5f) / ppm - cz;
                if (dx * dx + dz * dz <= r * r) px[z * n + x] = color;
            }
        }

        // ---------------- 視線 ----------------

        public static int SightMask { get { return ~((1 << Case01HouseBuilder.OutdoorLayer) | (1 << Case01HouseBuilder.AnomalyLayer)); } }

        /// <summary>點是否在攝影機視錐內（水平 FOV 由垂直 FOV 與長寬比算出）。</summary>
        public static bool InView(Vector3 eye, Quaternion view, float vFov, float aspect, Vector3 p)
        {
            var l = Quaternion.Inverse(view) * (p - eye);
            if (l.z <= 0.05f) return false;
            float tv = Mathf.Tan(vFov * 0.5f * Mathf.Deg2Rad);
            return Mathf.Abs(l.y / l.z) <= tv && Mathf.Abs(l.x / l.z) <= tv * aspect;
        }

        public static bool LineOfSight(Vector3 eye, Vector3 p)
        {
            return !Physics.Linecast(eye, p, SightMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>目前畫面裡看得到的地面格（世界 xz）。cells＝輸出；回傳同一層每個房間的「看得到／總格數」。</summary>
        public static Dictionary<Case01Room, Vector2Int> Visible(Case01HouseLayout L, Vector3 eye, Quaternion view, float vFov, float aspect, List<Vector2> cells)
        {
            var stats = new Dictionary<Case01Room, Vector2Int>();
            if (cells != null) cells.Clear();
            int floor = FloorOf(eye.y - 1.0f);
            float y = FloorY(floor) + SampleHeight;
            for (float x = Cell * 0.5f; x < 10f; x += Cell)
            for (float z = Cell * 0.5f; z < 10f; z += Cell)
            {
                var p = new Vector3(x, y, z);
                var room = L.RoomAt(new Vector3(x, FloorY(floor), z));
                if (!HouseRoom(room)) continue;
                // 格點在牆或家具裡（不是可看到的地面）：不計入。
                if (Physics.CheckSphere(p, 0.04f, SightMask, QueryTriggerInteraction.Ignore)) continue;
                Vector2Int s;
                stats.TryGetValue(room, out s);
                s.y++;
                if ((p - eye).sqrMagnitude < MaxSight * MaxSight && InView(eye, view, vFov, aspect, p) && LineOfSight(eye, p))
                {
                    s.x++;
                    if (cells != null) cells.Add(new Vector2(x, z));
                }
                stats[room] = s;
            }
            return stats;
        }

        /// <summary>報表／QA 面板用的焦點（劇情物件與動線焦點）。點＝熱區中心；沒有熱區的用固定點。</summary>
        public struct Focus
        {
            public string Label, Id;
            public Vector3 Point;
        }

        public static List<Focus> Foci(Case01HouseLayout L)
        {
            var list = new List<Focus>();
            System.Action<string, string> add = (id, label) =>
            {
                Case01Interactable it;
                if (!L.Interactables.TryGetValue(id, out it) || it == null || !it.gameObject.activeInHierarchy) return;
                var bc = it.GetComponent<BoxCollider>();
                list.Add(new Focus { Id = id, Label = label, Point = bc != null ? it.transform.TransformPoint(bc.center) : it.transform.position });
            };
            add(Case01Ids.Photo, "合照（壁爐架）");
            add(Case01Ids.Calendar, "日曆");
            add(Case01Ids.LivingBookNote, "夾便箋的書");
            add(Case01Ids.FriendCard, "茶几卡片");
            add(Case01Ids.Table, "餐桌");
            add(Case01Ids.ShoppingList, "購物清單");
            add(Case01Ids.Tea, "茶杯");
            add(Case01Ids.SundayNote, "冰箱紙條");
            list.Add(new Focus { Id = "stairs", Label = "樓梯起步", Point = new Vector3(9.4f, 0.7f, 4.3f) });
            add(Case01Ids.StudyMark, "書房門記號");
            add(Case01Ids.StorageDoor, "儲藏室門");
            list.Add(new Focus { Id = "third_door_wall", Label = "第三扇門位置", Point = new Vector3(Case01HouseBuilder.StudyDoorX, Case01HouseBuilder.FloorHeight + 1.2f, 0.15f) });
            add(Case01Ids.Map, "舊地圖");
            add(Case01Ids.OldRecord, "校正記錄");
            add(Case01Ids.JobLetter, "床上的信");
            add(Case01Ids.Postcard, "明信片");
            return list;
        }

        public static List<string> VisibleFoci(Case01HouseLayout L, Vector3 eye, Quaternion view, float vFov, float aspect)
        {
            var seen = new List<string>();
            int floor = FloorOf(eye.y - 1.0f);
            foreach (var f in Foci(L))
            {
                if (FloorOf(f.Point.y - 0.2f) != floor) continue;
                if ((f.Point - eye).sqrMagnitude > MaxSight * MaxSight) continue;
                if (InView(eye, view, vFov, aspect, f.Point) && LineOfSight(eye, f.Point)) seen.Add(f.Label);
            }
            return seen;
        }

        public static string RoomName(Case01Room r) { return Case01Texts.RoomName(r); }

        /// <summary>「客廳 61%｜餐廳 8%」：依房間的可見地面比例（只列有看到的與目前所在房間）。</summary>
        public static string Summary(Dictionary<Case01Room, Vector2Int> stats, Case01Room here)
        {
            var sb = new StringBuilder();
            var rooms = new List<Case01Room>(stats.Keys);
            rooms.Sort((a, b) => ((int)a).CompareTo((int)b));
            foreach (var r in rooms)
            {
                var s = stats[r];
                if (s.y == 0 || (s.x == 0 && r != here)) continue;
                if (sb.Length > 0) sb.Append("｜");
                sb.Append(RoomName(r)).Append(' ').Append(Mathf.RoundToInt(100f * s.x / s.y)).Append('%');
            }
            return sb.Length == 0 ? "（無）" : sb.ToString();
        }
    }
}
