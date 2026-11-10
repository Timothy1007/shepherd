using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// Phase 1.9 美術重構（只在 Phase 1.9 QA）：
    /// 夾層＝「被折疊的住宅記憶」——用住宅自己的壁紙、地板、線腳、門與窗、樓梯重排成不可能但有秩序的房間；
    /// 中庭＝「被拆解又重組的都市內庭」——磚牆、被掛上牆／側翻的街屋立面、北側拱廊、中央可走位的廣場與低矮掩體、懸浮的十七號門面；
    /// 世界內圖文正式化——相片、日曆、冰箱紙條、購物清單、舊地圖、手記、明信片、信、旅行路線、檔案盒標籤、粉筆記號、夾層黃銅門牌、搪瓷指示牌。
    /// 舊的佔位幾何（旋轉門框、斷線、牆碎、晶體、裂縫、浮空字）確實撤除或換成新外觀；功能物件（Portal、核、遭遇點、熱區、H 階段綁定）不動。
    /// </summary>
    public sealed class Case01Phase19Art
    {
        public struct GraphicItem
        {
            public string Name, Carrier;
            public Transform T;
            public Vector2 Size;
            public float Glyph;
            /// <summary>平放在桌面上（從上方看）。</summary>
            public bool Flat;
        }

        public readonly List<GraphicItem> Graphics = new List<GraphicItem>();
        public Transform MezzRoot { get; private set; }
        public Transform CourtRoot { get; private set; }
        public int RemovedPlaceholders { get; private set; }
        public readonly List<Transform> MezzFeatures = new List<Transform>();
        public readonly List<Transform> CourtCover = new List<Transform>();

        private Case01HouseLayout L;
        private Case01Palette p;
        private Case01ArtKit kit;
        private Renderer photo;
        private Material photoNormal, photoFaded;
        private Transform chalkWord;
        private readonly List<KeyValuePair<Text, Material>> plaques = new List<KeyValuePair<Text, Material>>();

        public static Case01Phase19Art Build(Case01HouseLayout layout, Case01Palette palette, Case01ArtKit kit)
        {
            var a = new Case01Phase19Art { L = layout, p = palette, kit = kit };
            a.HouseGraphics();
            a.Mezzanine();
            a.Courtyard();
            Physics.SyncTransforms();
            return a;
        }

        /// <summary>WorldLabel 的畫布（建置時整棵樹還沒啟用，Text.canvas 會是 null，所以自己往上找）。</summary>
        private static Transform CanvasOf(Text t)
        {
            for (var x = t.transform; x != null; x = x.parent) if (x.GetComponent<Canvas>() != null) return x;
            return t.transform;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        private void Hide(Transform t)
        {
            if (t == null) return;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }

        private void Register(string name, string carrier, Transform sheet, float glyph, bool flat = false)
        {
            Graphics.Add(new GraphicItem { Name = name, Carrier = carrier, T = sheet, Size = new Vector2(sheet.lossyScale.x, sheet.lossyScale.y), Glyph = glyph, Flat = flat });
        }

        private Transform Box(Transform parent, Vector3 pos, Vector3 scale, Material m, string name, Vector3? euler = null, bool collider = false)
        {
            var t = ArenaArt.Prim(PrimitiveType.Cube, parent, Vector3.zero, scale, m, null, name, collider, true);
            t.position = pos;
            t.rotation = Quaternion.Euler(euler ?? Vector3.zero);
            return t;
        }

        private Material Tint(string name, Color c, Vector2? tiling = null)
        {
            var src = kit.Get(name);
            var m = p.Own(new Material(src) { name = name + "_p19" });
            m.color = c;
            if (tiling.HasValue) m.mainTextureScale = tiling.Value;
            return m;
        }

        // ======================= 住宅的圖文 =======================

        private sealed class Follow
        {
            public Transform Src, Sheet;
            public Vector3 Pos;
            public Quaternion Rot;
            /// <summary>掛在牆上的：哪一面朝房間要等物理啟用後（第一個影格）才判斷得出來。</summary>
            public bool Probe;
            public float Half;
            /// <summary>平放的：等美術模型都顯示後（第一個影格）貼到下面那個面（床、餐邊櫃的新模型常比原本的方塊高）。</summary>
            public bool Flat;
        }

        /// <summary>平放的紙：找正下方（水平範圍蓋住紙中心、頂面在紙下 5 cm 到紙上 15 cm 之間）最高的大面，貼在上面 3 mm。</summary>
        private void SettleOnTop(Follow f)
        {
            var c = f.Sheet.position;
            float best = float.NegativeInfinity;
            foreach (var r in L.Root.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r.transform == f.Sheet || r.transform.IsChildOf(f.Sheet) || (gfxRoot != null && r.transform.IsChildOf(gfxRoot))) continue;
                var b = r.bounds;
                if (b.size.x < 0.25f || b.size.z < 0.25f) continue;
                if (c.x < b.min.x || c.x > b.max.x || c.z < b.min.z || c.z > b.max.z) continue;
                float top = b.max.y;
                if (top < c.y - 0.05f || top > c.y + 0.15f) continue;
                if (top > best) best = top;
            }
            if (float.IsNegativeInfinity(best) || best <= c.y - 0.003f) return;
            f.Sheet.position = new Vector3(c.x, best + 0.003f, c.z);
            f.Pos = Quaternion.Inverse(f.Src.rotation) * (f.Sheet.position - f.Src.position);
        }

        /// <summary>牆那一側：貼著物件幾公分處就在牆體（或家具）裡；另一側是空的房間。是牆那側就把圖文翻到另一面。</summary>
        private static void ResolveFacing(Follow f)
        {
            var n = -f.Sheet.forward;
            var c = f.Src.position;
            System.Func<Vector3, int> solid = dir =>
            {
                int k = 0;
                foreach (var off in new[] { 0.03f, 0.06f, 0.1f })
                    if (Physics.CheckSphere(c + dir * (f.Half + off), 0.015f, ~0, QueryTriggerInteraction.Ignore)) k++;
                if (Physics.Raycast(c + dir * (f.Half + 0.005f), dir, 0.25f, ~0, QueryTriggerInteraction.Ignore)) k++;
                return k;
            };
            if (solid(n) <= solid(-n)) return;
            float along = Vector3.Dot(f.Sheet.position - c, n);
            f.Sheet.position -= n * (2f * along);
            f.Sheet.rotation = Quaternion.AngleAxis(180f, Vector3.up) * f.Sheet.rotation;
            f.Pos = Quaternion.Inverse(f.Src.rotation) * (f.Sheet.position - c);
            f.Rot = Quaternion.Inverse(f.Src.rotation) * f.Sheet.rotation;
        }

        private readonly List<Follow> follows = new List<Follow>();
        private Transform gfxRoot;

        /// <summary>
        /// 把一張正式圖文貼到既有物件（薄方塊）的可見面上：尺寸＝原物件的面（比例跟原本一樣，不另外估）、
        /// 法線＝原物件最薄的軸，朝向沒有牆的那一側；平放的（法線朝上）就貼在上面。
        /// 圖文放在不縮放的群組下（原物件常有非等比縮放），每影格跟著原物件的位置與顯示狀態。
        /// </summary>
        private Transform Cover(Transform src, string name, Material m, float tilt = 0f, Vector2? sizeOverride = null, Vector3? normal = null)
        {
            if (src == null) return null;
            var group = src;
            bool probe = false;
            // 群組（本身沒有網格，例如 V05 的日曆＝底板＋頁）：以最大的那片當載體，整組隱藏。
            if (src.GetComponent<Renderer>() == null)
            {
                src = Largest(src);
                if (src == null) return null;
            }
            var s = src.lossyScale;
            var ax = new[] { src.right, src.up, src.forward };
            var len = new[] { Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z) };
            int thin = 0;
            for (int i = 1; i < 3; i++) if (len[i] < len[thin]) thin = i;
            var n = ax[thin].normalized;
            int a = (thin + 1) % 3, b = (thin + 2) % 3;
            Vector3 up;
            Vector2 size;
            if (Mathf.Abs(n.y) > 0.7f)
            {
                n = Vector3.up;
                // 平放：長邊當寬，短邊當高（字順著長邊寫）。
                int wide = len[a] >= len[b] ? a : b, tall = wide == a ? b : a;
                size = new Vector2(len[wide], len[tall]);
                up = Vector3.ProjectOnPlane(ax[tall], Vector3.up).normalized;
                // 字的「上」朝遠離房間中央的方向不重要；統一讓字的上方朝 +z 或 +x 較大的一側，免得倒著放。
                if (Vector3.Dot(up, new Vector3(0.3f, 0f, 1f)) < 0f) up = -up;
            }
            else
            {
                n.y = 0f;
                n.Normalize();
                if (normal.HasValue) n = normal.Value.normalized;
                else probe = true;
                up = Vector3.up;
                int vert = Mathf.Abs(Vector3.Dot(ax[a], Vector3.up)) > Mathf.Abs(Vector3.Dot(ax[b], Vector3.up)) ? a : b;
                int hor = vert == a ? b : a;
                size = new Vector2(len[hor], len[vert]);
            }
            if (sizeOverride.HasValue) size = sizeOverride.Value;
            if (gfxRoot == null) gfxRoot = ArenaArt.Group("GraphicsP19", L.Root, Vector3.zero);
            var pos = src.position + n * (len[thin] * 0.5f + 0.0025f);
            var q = ArenaArt.Prim(PrimitiveType.Quad, gfxRoot, Vector3.zero, new Vector3(size.x, size.y, 1f), m, null, name, false, false);
            q.position = pos;
            q.rotation = Quaternion.LookRotation(-n, up) * Quaternion.Euler(0f, 0f, tilt);
            q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            follows.Add(new Follow { Src = src, Sheet = q, Pos = Quaternion.Inverse(src.rotation) * (q.position - src.position), Rot = Quaternion.Inverse(src.rotation) * q.rotation, Probe = probe, Half = len[thin] * 0.5f, Flat = n == Vector3.up });
            Hide(group);
            return q;
        }

        private static Transform Largest(Transform group)
        {
            Transform best = null;
            float area = 0f;
            foreach (var r in group.GetComponentsInChildren<Renderer>(true))
            {
                var s = r.transform.lossyScale;
                float k = Mathf.Abs(s.x * s.y) + Mathf.Abs(s.y * s.z) + Mathf.Abs(s.x * s.z);
                if (k > area)
                {
                    area = k;
                    best = r.transform;
                }
            }
            return best;
        }

        private static string Cm(Vector2 size)
        {
            return (size.x * 100f).ToString("0") + "×" + (size.y * 100f).ToString("0") + " cm";
        }

        private void HouseGraphics()
        {
            var root = L.Root;
            var ink = new Color(0.1f, 0.1f, 0.16f);
            var paper = Case01Graphics.Mat(p, "paper");
            var ruled = Case01Graphics.Mat(p, "paper_ruled");
            System.Action<string, string, Transform, float, bool> reg = (n, carrier, sheet, glyph, flat) =>
            {
                if (sheet != null) Register(n, carrier + " " + Cm(new Vector2(sheet.lossyScale.x, sheet.lossyScale.y)), sheet, glyph, flat);
            };
            // 壁爐架上的合照：母女並肩的相紙（H1 第二個人淡掉——跟原本的事件同步）。
            var frame = Find(root, "PhotoFrame");
            if (frame != null)
            {
                photoNormal = Case01Graphics.Mat(p, "photo_family");
                photoFaded = Case01Graphics.Mat(p, "photo_family_faded");
                var print = frame.Find("Print");
                var s = Cover(print, "PhotoPrint", photoNormal);
                foreach (var n in new[] { "FigureA", "FigureB" }) Hide(frame.Find(n));
                if (s != null)
                {
                    photo = s.GetComponent<Renderer>();
                    reg("壁爐架上的合照", "相紙", s, 0f, false);
                }
            }
            // 客廳牆上的相框（H1 變空白：相片跟著原本的 Photo 群組一起消失）。
            var living = Find(root, "LivingFrame");
            var photoGroup = living != null ? living.Find("Photo") : null;
            if (photoGroup != null)
            {
                var big = Largest(photoGroup);
                foreach (Transform c in photoGroup) Hide(c);
                reg("客廳牆上的海邊照片", "相紙", Cover(big, "BeachPrint", Case01Graphics.Mat(p, "photo_beach")), 0f, false);
            }
            // 日曆：月曆格與週日的紅圈（越後面越淡，和調查文字一致）。
            reg("日曆", "掛曆", Cover(Find(root, "Calendar"), "CalendarSheet", Case01Graphics.Mat(p, "calendar")), 0.018f, false);
            // 冰箱紙條。
            var note = Cover(Find(root, "FridgeNote"), "FridgeNotePaper", ruled, -4f);
            if (note != null) reg("冰箱上的紙條", "便條紙", note, Case01Graphics.Write(note, "週日不用再\n帶那麼多菜了。", ink, 0.12f), false);
            // 餐邊櫃上的購物清單（平放）。
            var list = Cover(Find(root, "ShoppingList"), "ShoppingListPaper", ruled);
            if (list != null) reg("購物清單", "便條紙（平放）", list, Case01Graphics.Write(list, "牛奶　雞蛋\n她愛吃的那種餅乾 <color=#8A2A22>×2</color>", ink, 0.1f, true, 28), true);
            // 書房：舊地圖（16 與 18 之間鉛筆補畫的 17）、手記封面、旅行路線、明信片、信、檔案盒標籤。
            Hide(Find(root, "MapPencil17"));
            reg("街區舊地圖", "舊地圖（平放）", Cover(Find(root, "OldMap"), "OldMapSheet", Case01Graphics.Mat(p, "map")), 0.024f, true);
            var book = Find(root, "Notebook");
            if (book != null)
            {
                var label = ArenaArt.Prim(PrimitiveType.Cube, gfxRoot ?? (gfxRoot = ArenaArt.Group("GraphicsP19", L.Root, Vector3.zero)), Vector3.zero, new Vector3(0.2f, 0.002f, 0.07f), paper, null, "NotebookLabelBase", false, false);
                label.position = book.position + Vector3.up * (book.lossyScale.y * 0.5f + 0.001f);
                label.rotation = book.rotation;
                var s = Cover(label, "NotebookLabel", paper);
                reg("艾琳的手記（封面標籤）", "封面貼紙", s, Case01Graphics.Write(s, "艾琳．修復紀錄", ink, 0.1f), true);
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == "RouteDash") Hide(t);
            reg("書桌上的旅行路線", "手畫地圖（平放）", Cover(Find(root, "TravelRoute"), "TravelRouteSheet", Case01Graphics.Mat(p, "route")), 0f, true);
            var postcard = Cover(Find(root, "Postcard"), "PostcardBack", paper);
            if (postcard != null) reg("沒寄出的明信片", "明信片（平放）", postcard, Case01Graphics.Write(postcard, "媽，下週日\n我晚點到。", ink, 0.12f), true);
            var letter = Cover(Find(root, "InvitationLetter"), "InvitationPaper", paper);
            if (letter != null)
            {
                // 信放在床尾的毯子上（毯子比床墊高約 4 cm）。
                var fl = follows[follows.Count - 1];
                fl.Sheet.position += Vector3.up * 0.045f;
                fl.Pos = Quaternion.Inverse(fl.Src.rotation) * (fl.Sheet.position - fl.Src.position);
                fl.Flat = false;
            }
            if (letter != null) reg("床上的信", "信紙（平放）", letter, Case01Graphics.Write(letter, "檔案修復工作邀請\n回覆：<color=#8A2A22>延後</color>", ink, 0.1f, false, 26), true);
            string[] tabs = { "已修復", "待確認", "不該存在" };
            for (int i = 0; i < 3; i++)
            {
                var box = Find(root, "FileBox_" + i);
                var tab = box != null ? box.Find("Tab") : null;
                var s = Cover(tab, "FileBoxLabel", paper, 0f, null, box.right);
                if (s != null) reg("檔案盒標籤「" + tabs[i] + "」", "紙標籤", s, Case01Graphics.Write(s, tabs[i], ink, 0.1f, false, 26), false);
            }
            // 書房門框上的記號：艾琳的粉筆三角（覆寫後下面多一行粉筆字）。
            if (L.StudyMarkLabel != null)
            {
                var label = L.StudyMarkLabel;
                label.enabled = false;
                var forward = -CanvasOf(label).forward;
                var pos = CanvasOf(label).position;
                var s = Case01Graphics.Sheet(CanvasOf(label).parent, "ChalkTriangle", pos + forward * 0.004f, forward, new Vector2(0.18f, 0.18f), Case01Graphics.Decal(p, "chalk_triangle"));
                Register("書房門框上的粉筆記號", "粉筆畫（牆面）18×18 cm", s, 0f);
                var word = Case01Graphics.Sheet(CanvasOf(label).parent, "ChalkWord", pos + forward * 0.004f - Vector3.up * 0.16f, forward, new Vector2(0.2f, 0.09f), Case01Graphics.Decal(p, "clear"));
                word.GetComponent<Renderer>().enabled = false;
                Case01Graphics.Write(word, "歸位", new Color(0.92f, 0.92f, 0.88f, 0.9f), 0.05f);
                chalkWord = word;
                chalkWord.gameObject.SetActive(false);
            }
            if (L.FrontDoorLabel != null) L.FrontDoorLabel.enabled = false;
        }

        // ======================= 夾層 =======================

        private void Mezzanine()
        {
            var mezz = L.Root.Find("Mezzanine");
            if (mezz == null) return;
            MezzRoot = ArenaArt.Group("MezzanineMemoryP19", mezz, Vector3.zero);
            var c = Case01HouseBuilder.MezzCenter;
            const float h = 4.5f;
            // 地面與牆：住宅的木地板與壁紙，但褪成冷灰綠（像記憶裡的顏色）。
            var floor = Find(mezz, "Floor");
            if (floor != null) kit.Swap(floor.GetComponent<Renderer>(), Tint("LR_Floor", new Color(0.62f, 0.68f, 0.64f), new Vector2(10f, 10f)));
            kit.DressWalls(mezz, MezzRoot, c.x - 5.925f, c.x + 5.925f, c.z - 5.925f, c.z + 5.925f, 0f, h, Tint("LR_Wallpaper", new Color(0.62f, 0.72f, 0.68f)));
            kit.Quad(MezzRoot, "MezzCeiling", c + new Vector3(0f, h - 0.004f, 0f), Vector3.down, Vector3.forward, 11.85f, 11.85f, Tint("LR_Ceiling", new Color(0.8f, 0.84f, 0.82f)), 1.5f, Vector2.zero);
            foreach (var (pos, yaw) in new[] { (c + new Vector3(0f, h, -5.925f), 0f), (c + new Vector3(0f, h, 5.925f), 180f), (c + new Vector3(-5.925f, h, 0f), 90f), (c + new Vector3(5.925f, h, 0f), -90f) })
            {
                var cor = kit.Add("Cornice", MezzRoot, pos, yaw);
                if (cor != null) cor.localScale = new Vector3(11.85f, 1f, 1f);
            }
            var wood = kit.Get("P1_WoodPaint");
            var brass = kit.Get("LR_Brass");
            // 佔位：地上的斷線（材質仍由核校正狀態驅動，換成黃銅錨線使用）。
            foreach (var t in mezz.GetComponentsInChildren<Transform>(true))
                if (t.name == "BrokenLine" && t.GetComponent<Renderer>() != null && t.GetComponent<Renderer>().enabled)
                {
                    t.GetComponent<Renderer>().enabled = false;
                    RemovedPlaceholders++;
                }
            // 錨線：從核出發、筆直通往四扇門的黃銅嵌條＋環繞核的圓環（核校正後由舊的材質發出穩定的暖光）。
            int mi = 0;
            System.Func<Material> lineMat = () => L.MezzLineMats.Count > 0 ? L.MezzLineMats[mi++ % L.MezzLineMats.Count] : brass;
            var core = c + new Vector3(0f, 0f, 1.2f);
            foreach (var dir in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                float len = dir == Vector3.forward ? 3.6f : dir == Vector3.back ? 6f : 5.2f;
                var start = core + dir * 1.25f;
                var t = Box(MezzRoot, start + dir * (len * 0.5f) + Vector3.up * 0.012f, new Vector3(Mathf.Abs(dir.x) > 0.5f ? len : 0.05f, 0.006f, Mathf.Abs(dir.z) > 0.5f ? len : 0.05f), lineMat(), "AnchorInlay");
                t.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            for (int i = 0; i < 24; i++)
            {
                float a0 = i * Mathf.PI * 2f / 24f;
                var pos = core + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 1.15f + Vector3.up * 0.012f;
                Box(MezzRoot, pos, new Vector3(0.05f, 0.006f, 0.32f), lineMat(), "AnchorRing", new Vector3(0f, -a0 * Mathf.Rad2Deg, 0f));
            }
            // 核的台座：石材＋黃銅頂（不是祭壇；像樓梯起步柱頭被放大）。
            var ped = Find(mezz, "Pedestal");
            if (ped != null)
            {
                kit.Swap(ped.GetComponent<Renderer>(), kit.Get("P1_Stone"));
                Box(MezzRoot, ped.position + Vector3.up * 0.32f, new Vector3(1.0f, 0.05f, 1.0f), brass, "PedestalCap");
            }
            // 記憶門：五個舊的旋轉門框改成「嵌在牆上、在牆面內歪斜」的住宅房門；核校正後在牆面內轉正（沿用既有的校正動畫）。
            var anchors = new[]
            {
                (c + new Vector3(-3.2f, 0f, 5.86f), 180f), (c + new Vector3(3.2f, 0f, 5.86f), 180f), (c + new Vector3(-5.86f, 0f, 3.2f), 90f),
                (c + new Vector3(3.2f, 0f, -5.86f), 0f), (c + new Vector3(5.86f, 0f, 3.2f), -90f),
            };
            float[] tilt = { 9f, -14f, 18f, -7f, 12f };
            for (int i = 0; i < L.Overlaps.Count && i < anchors.Length; i++)
            {
                var anchor = ArenaArt.Group("MemoryDoorAnchor_" + i, MezzRoot, anchors[i].Item1);
                anchor.rotation = Quaternion.Euler(0f, anchors[i].Item2, 0f);
                var ov = L.Overlaps[i];
                ov.SetParent(anchor, false);
                ov.localPosition = Vector3.zero;
                var skew = Quaternion.Euler(0f, 0f, tilt[i]);
                ov.localRotation = skew;
                L.OverlapSkew[i] = skew;
                foreach (var r in ov.GetComponentsInChildren<Renderer>(true)) kit.Swap(r, wood);
                var door = kit.Place("PanelDoor", ov, new Vector3(0f, 0f, 0.02f), 0f);
                if (door != null)
                {
                    door.localScale = new Vector3(1.4f / 0.82f, 2.55f / 1.98f, 1f);
                    kit.Register(door.gameObject);
                }
                MezzFeatures.Add(ov);
            }
            // 折疊的樓梯：一段住宅樓梯從地面往西南角爬上去，碰到牆後翻折、倒掛在天花板上走回來（不能走上去：欄杆與碰撞擋住）。
            var stair = ArenaArt.Group("FoldedStair", MezzRoot, c + new Vector3(-1.8f, 0f, -3.7f));
            for (int i = 0; i < 9; i++)
            {
                float sh = 0.2f + i * 0.19f;
                Box(stair, stair.position + new Vector3(-i * 0.42f, sh * 0.5f, 0f), new Vector3(0.42f, sh, 1.0f), i % 2 == 0 ? kit.Get("LR_WoodMid") : kit.Get("LR_WoodDark"), "Step");
                Box(stair, stair.position + new Vector3(-3.6f + i * 0.42f, h - 0.1f - i * 0.19f, 0f), new Vector3(0.42f, 0.2f, 1.0f), i % 2 == 0 ? kit.Get("LR_WoodMid") : kit.Get("LR_WoodDark"), "CeilingStep");
            }
            for (int i = 0; i < 9; i += 2) Box(stair, stair.position + new Vector3(-i * 0.42f, 0.55f + i * 0.19f, -0.48f), new Vector3(0.05f, 0.9f, 0.05f), kit.Get("LR_WoodDark"), "Baluster");
            Box(stair, stair.position + new Vector3(-1.7f, 1.35f, -0.48f), new Vector3(3.8f, 0.06f, 0.07f), kit.Get("LR_WoodDark"), "Handrail", new Vector3(0f, 0f, -24f));
            var block = new GameObject("FoldedStairBlock").AddComponent<BoxCollider>();
            block.transform.SetParent(stair, false);
            block.transform.position = stair.position + new Vector3(-1.7f, 1.2f, 0f);
            block.size = new Vector3(4.0f, 2.4f, 1.15f);
            MezzFeatures.Add(stair);
            // 錯置的窗：住宅的上下拉窗嵌在東牆南段，高低不一（地板邊、眼高、天花板下），窗後是記憶裡的暖光。
            var glow = p.Own(ArenaArt.Mat(new Color(1f, 0.86f, 0.6f), 0.1f, new Color(0.9f, 0.7f, 0.42f)));
            foreach (var (z, y) in new[] { (-1.7f, 0.75f), (-3.1f, 1.55f), (-4.5f, 3.0f) })
            {
                var pos = c + new Vector3(5.9f, y, z);
                Box(MezzRoot, pos + new Vector3(0.02f, 0f, 0f), new Vector3(0.01f, 1.05f, 0.95f), glow, "MemoryWindowGlow");
                var w = kit.Add("SashWindow", MezzRoot, pos, -90f);
                if (w != null) MezzFeatures.Add(w);
            }
            foreach (var l in L.MezzLights) l.range = Mathf.Max(l.range, 12f);
            ArenaArt.PointLight(MezzRoot, c + new Vector3(4.5f, 2.0f, 3.0f), new Color(1f, 0.82f, 0.55f), 4.5f, 0.6f, false);
            // 門上的記號：黃銅刻紋門牌（△、○、□17）與搪瓷指示牌；原本浮在空中的字隱藏，顏色仍由校正狀態驅動。
            foreach (var kv in L.MezzLabels)
            {
                string tex = kv.Key == Case01Door.MezzTriangle ? "sym_triangle" : kv.Key == Case01Door.MezzCircle ? "sym_circle" : "sym_square17";
                Plaque(kv.Value, tex, kv.Key == Case01Door.Mezz17 ? "門牌「□17」（黃銅 26 cm）" : kv.Key == Case01Door.MezzTriangle ? "門牌「△」（黃銅 22 cm）" : "門牌「○」（黃銅 22 cm）");
            }
            foreach (var t in mezz.GetComponentsInChildren<Text>(true))
                if (t.text == "返回樓梯平台") Sign(t, "返回樓梯平台", new Vector2(0.62f, 0.2f));
            Case01HouseBuilder.SetLayer(MezzRoot, Case01HouseBuilder.AnomalyLayer);
        }

        private void Plaque(Text label, string tex, string carrier)
        {
            label.enabled = false;
            var fwd = -CanvasOf(label).forward;
            var pos = CanvasOf(label).position - Vector3.up * 0.25f + fwd * 0.01f;
            var size = tex == "sym_square17" ? 0.26f : 0.22f;
            var plate = Box(MezzRoot, pos - fwd * 0.012f, new Vector3(size + 0.03f, size + 0.03f, 0.015f), kit.Get("LR_WoodDark"), "PlaqueBacking");
            plate.rotation = Quaternion.LookRotation(-fwd);
            var m = Case01Graphics.Mat(p, tex, 0.55f);
            var s = Case01Graphics.Sheet(MezzRoot, "BrassPlaque", pos, fwd, new Vector2(size, size), m);
            plaques.Add(new KeyValuePair<Text, Material>(label, m));
            Register(carrier, "黃銅門牌，刻紋", s, size * 0.6f);
        }

        private void Sign(Text label, string text, Vector2 size)
        {
            label.enabled = false;
            var fwd = -CanvasOf(label).forward;
            var parent = CanvasOf(label).parent;
            var s = Case01Graphics.Sheet(parent, "EnamelSign", CanvasOf(label).position + fwd * 0.012f - Vector3.up * 0.15f, fwd, size, Case01Graphics.Mat(p, "enamel", 0.6f));
            Register("指示牌「" + text + "」", "搪瓷牌 " + (size.x * 100f).ToString("0") + "×" + (size.y * 100f).ToString("0") + " cm", s, Case01Graphics.Write(s, text, new Color(0.92f, 0.88f, 0.76f), 0.12f, false, 30));
        }

        // ======================= 中庭 =======================

        private void Courtyard()
        {
            var court = L.Root.Find("Courtyard");
            if (court == null) return;
            CourtRoot = ArenaArt.Group("CourtyardP19", court, Vector3.zero);
            const float x0 = 26f, x1 = 54f, z0 = 24f, z1 = 58f, h = 7f;
            // 撤除佔位：浮動門框、牆碎（有碰撞，換成下面有設計的掩體）、地上裂縫。浮動晶體改成鬆脫的屋瓦與窗玻璃（保留漂浮動畫）。
            foreach (var t in court.GetComponentsInChildren<Transform>(true))
            {
                if (t.parent != court) continue;
                if (t.name == "FloatingRoomFrame" || t.name == "WallFragment" || t.name == "Crack")
                {
                    t.gameObject.SetActive(false);
                    RemovedPlaceholders++;
                }
            }
            var slate = kit.Get("P1_Slate");
            var glass = kit.Get("P1_GlassDark");
            for (int i = 0; i < L.Floating.Count; i++)
            {
                var f = L.Floating[i];
                kit.Swap(f.GetComponent<Renderer>(), i % 4 == 0 ? glass : slate);
                f.localScale = i % 4 == 0 ? new Vector3(0.5f, 0.6f, 0.02f) : new Vector3(0.42f, 0.02f, 0.3f);
            }
            var floor = Find(court, "Floor");
            if (floor != null) kit.Swap(floor.GetComponent<Renderer>(), Tint("P1_Stone", new Color(0.62f, 0.62f, 0.66f), new Vector2(14f, 17f)));
            var brick = Tint("P1_Brick", new Color(0.55f, 0.48f, 0.48f));
            kit.DressWalls(court, CourtRoot, x0 + 0.075f, x1 - 0.075f, z0 + 0.075f, z1 - 0.075f, 0f, h, brick);
            var dark = Tint("P1_Stone", new Color(0.3f, 0.3f, 0.33f));
            // 排水溝與路緣：兩條沿長邊的溝、廣場邊的石緣。
            foreach (var x in new[] { 29f, 51f }) Box(CourtRoot, new Vector3(x, 0.006f, (z0 + z1) * 0.5f), new Vector3(0.32f, 0.012f, z1 - z0 - 1f), dark, "Drain");
            // 東牆：四段街屋立面掛在牆上（窗、門、門牌位置都在，但沒有屋子在後面）。
            for (int i = 0; i < 4; i++)
            {
                var f = kit.Add(i % 2 == 0 ? "TerraceFront" : "TerraceFrontBay", CourtRoot, new Vector3(x1 - 0.08f, 0f, 28f + i * 7.2f), -90f);
                if (f != null) f.localScale = new Vector3(1.05f, 1f, 1f);
            }
            // 西牆：同樣的立面側翻九十度，整條街像被轉了個方向貼上去。
            // 立面的正面＝本地 +z、原點在底邊中央：正面朝 +x（朝向中庭），本地「上」沿牆往北——整片立面在牆面內轉了 90°。
            for (int i = 0; i < 3; i++)
            {
                var f = kit.Add(i % 2 == 0 ? "TerraceFrontBay" : "TerraceFront", CourtRoot, new Vector3(x0 + 0.08f, 3.6f, 27f + i * 9.5f), 90f);
                if (f != null) f.rotation = Quaternion.LookRotation(Vector3.right, Vector3.forward);
            }
            // 牆面打光：讓立面與拱廊在暗處讀得出輪廓（冷色月光＋路燈暖光）。
            foreach (var (pos, col) in new[] { (new Vector3(50.5f, 4.5f, 34f), new Color(0.7f, 0.75f, 0.95f)), (new Vector3(50.5f, 4.5f, 48f), new Color(0.7f, 0.75f, 0.95f)), (new Vector3(29.5f, 4.5f, 36f), new Color(0.75f, 0.7f, 0.95f)), (new Vector3(29.5f, 4.5f, 48f), new Color(0.75f, 0.7f, 0.95f)), (new Vector3(40f, 3.2f, 53.5f), new Color(1f, 0.72f, 0.45f)) })
                ArenaArt.PointLight(CourtRoot, pos, col, 9f, 0.9f, false);
            // 北側拱廊：磚柱（可當掩體）、拱頂與過梁，形成廣場後方的層次與失序體登場的背景。
            for (int i = 0; i <= 5; i++)
            {
                float x = 30f + i * 4f;
                CourtCover.Add(Box(CourtRoot, new Vector3(x, 2.1f, 55.2f), new Vector3(0.7f, 4.2f, 0.7f), brick, "ArcadePier", null, true));
                if (i < 5)
                {
                    Box(CourtRoot, new Vector3(x + 2f, 4.6f, 55.2f), new Vector3(3.3f, 0.8f, 0.7f), brick, "ArcadeLintel");
                    Box(CourtRoot, new Vector3(x + 0.85f, 4.05f, 55.2f), new Vector3(1.2f, 0.35f, 0.7f), brick, "ArcadeHaunch", new Vector3(0f, 0f, 35f));
                    Box(CourtRoot, new Vector3(x + 3.15f, 4.05f, 55.2f), new Vector3(1.2f, 0.35f, 0.7f), brick, "ArcadeHaunch", new Vector3(0f, 0f, -35f));
                    Box(CourtRoot, new Vector3(x + 2f, 4.15f, 55.25f), new Vector3(0.36f, 0.5f, 0.75f), dark, "Keystone");
                }
            }
            Box(CourtRoot, new Vector3(40f, 5.2f, 55.2f), new Vector3(21f, 0.4f, 0.9f), dark, "ArcadeCornice");
            // 中央廣場（約 16 × 16 m 淨空，走位用）：邊緣是矮磚牆與鐵欄杆段（可躲、可繞），四角路燈（兩盞歪斜）。
            var iron = kit.Get("LR_Iron");
            foreach (var (pos, size) in new[]
                     {
                         (new Vector3(33.5f, 0.45f, 36.6f), new Vector3(5f, 0.9f, 0.4f)), (new Vector3(46.5f, 0.45f, 36.6f), new Vector3(5f, 0.9f, 0.4f)),
                         (new Vector3(30.6f, 0.45f, 43f), new Vector3(0.4f, 0.9f, 6f)), (new Vector3(49.4f, 0.45f, 43f), new Vector3(0.4f, 0.9f, 6f)),
                     })
            {
                CourtCover.Add(Box(CourtRoot, pos, size, brick, "Parapet", null, true));
                Box(CourtRoot, pos + Vector3.up * 0.5f, new Vector3(size.x, 0.08f, size.z) + new Vector3(0.06f, 0f, 0.06f), dark, "ParapetCoping");
                for (float k = -0.45f; k <= 0.46f; k += 0.15f)
                    Box(CourtRoot, pos + new Vector3(size.x > 1f ? k * size.x : 0f, 0.95f, size.z > 1f ? k * size.z : 0f), new Vector3(0.03f, 0.9f, 0.03f), iron, "RailBar");
            }
            int lamp = 0;
            foreach (var pos in new[] { new Vector3(32f, 0f, 38f), new Vector3(48f, 0f, 38f), new Vector3(32f, 0f, 54f), new Vector3(48f, 0f, 54f) })
            {
                var lp = kit.Add("LampPost", CourtRoot, pos, 0f);
                if (lp != null && lamp % 2 == 1) lp.rotation = Quaternion.Euler(lamp == 1 ? 9f : -12f, 0f, lamp == 1 ? 6f : -4f);
                var b = new GameObject("LampBlock").AddComponent<BoxCollider>();
                b.transform.SetParent(CourtRoot, false);
                b.transform.position = pos + Vector3.up * 1.2f;
                b.size = new Vector3(0.3f, 2.4f, 0.3f);
                ArenaArt.PointLight(CourtRoot, pos + Vector3.up * 3.8f, new Color(1f, 0.82f, 0.58f), 7f, 0.9f, false);
                lamp++;
            }
            // 廣場中央：街道石板在這裡被撕開的接縫（環狀，微弱紫光）——失序體登場的位置（遭遇點不變）。
            var seam = p.Own(ArenaArt.Mat(new Color(0.2f, 0.16f, 0.26f), 0.4f, new Color(0.32f, 0.18f, 0.5f)));
            var anchorPos = L.EncounterAnchor != null ? L.EncounterAnchor.position : new Vector3(40f, 0f, 46f);
            for (int i = 0; i < 36; i++)
            {
                float a0 = i * Mathf.PI * 2f / 36f;
                Box(CourtRoot, anchorPos + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 3.2f + Vector3.up * 0.01f, new Vector3(0.1f, 0.012f, 0.62f), seam, "PlazaSeam", new Vector3(0f, -a0 * Mathf.Rad2Deg, 0f));
            }
            // 戲劇焦點：十七號的門面浮在廣場北上方，正對從裂口進來的玩家，緩緩起伏（沿用漂浮動畫）。
            // Front17A 是整面 10 m 的街屋正面：縮成 0.42 倍（約 4.3 × 2.7 m）、底邊離地 2.4 m，浮在拱廊前、略微歪斜。
            var door17 = kit.Add("Front17A", CourtRoot, new Vector3(40f, 2.4f, 51.5f), 180f);
            if (door17 != null)
            {
                door17.localScale = Vector3.one * 0.42f;
                door17.rotation = Quaternion.Euler(4f, 186f, -3f);
                door17.SetParent(court, true);
                L.Floating.Add(door17);
                L.FloatingHome.Add(door17.localPosition);
            }
            var spot = new GameObject("CourtKeyLight").AddComponent<Light>();
            spot.transform.SetParent(CourtRoot, false);
            spot.transform.position = new Vector3(40f, 9f, 40f);
            spot.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -1f, 0.35f));
            spot.type = LightType.Spot;
            spot.spotAngle = 70f;
            spot.range = 18f;
            spot.intensity = 1.4f;
            spot.color = new Color(0.82f, 0.86f, 0.95f);
            foreach (var t in court.GetComponentsInChildren<Text>(true))
                if (t.text.StartsWith("裂口")) SignCourt(t);
            Case01HouseBuilder.SetLayer(CourtRoot, Case01HouseBuilder.AnomalyLayer);
            if (door17 != null) Case01HouseBuilder.SetLayer(door17, Case01HouseBuilder.AnomalyLayer);
        }

        private void SignCourt(Text label)
        {
            label.enabled = false;
            var fwd = -CanvasOf(label).forward;
            var s = Case01Graphics.Sheet(CourtRoot, "EnamelSign", CanvasOf(label).position + fwd * 0.02f, fwd, new Vector2(0.8f, 0.24f), Case01Graphics.Mat(p, "enamel", 0.6f));
            Register("指示牌「裂口：返回夾層」", "搪瓷牌 80×24 cm", s, Case01Graphics.Write(s, "裂口　返回夾層", new Color(0.92f, 0.88f, 0.76f), 0.12f, false, 30));
        }

        // ======================= 每影格 =======================

        public void Tick(Case01HouseQA qa)
        {
            foreach (var f in follows)
            {
                if (f.Src == null || f.Sheet == null) continue;
                bool on = f.Src.gameObject.activeInHierarchy;
                if (f.Sheet.gameObject.activeSelf != on) f.Sheet.gameObject.SetActive(on);
                f.Sheet.position = f.Src.position + f.Src.rotation * f.Pos;
                f.Sheet.rotation = f.Src.rotation * f.Rot;
                if (f.Probe && on)
                {
                    f.Probe = false;
                    Physics.SyncTransforms();
                    ResolveFacing(f);
                }
                if (f.Flat && on)
                {
                    f.Flat = false;
                    SettleOnTop(f);
                }
            }
            if (photo != null && L.PhotoSecondFigure != null)
                photo.sharedMaterial = L.PhotoSecondFigure.sharedMaterial == L.PhotoFadedMat ? photoFaded : photoNormal;
            if (chalkWord != null && L.StudyMarkLabel != null) chalkWord.gameObject.SetActive(L.StudyMarkLabel.text.Contains("歸位"));
            foreach (var kv in plaques)
            {
                if (kv.Key == null) continue;
                var col = kv.Key.color;
                bool fixedState = col.g > col.r + 0.15f;
                ArenaArt.SetEmission(kv.Value, fixedState ? new Color(0.2f, 0.5f, 0.25f) : Color.black);
            }
        }
    }
}
