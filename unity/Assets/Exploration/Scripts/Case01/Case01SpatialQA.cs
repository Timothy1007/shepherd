using System.Collections.Generic;
using Shepherd.Battle.UI;
using Shepherd.Battle.UI.Arena;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// Phase 1.6 空間結構灰盒 QA（選單「Shepherd/Case01/Phase 1.6 空間結構灰盒 QA」）。
    /// 包一個方案 A（V05A）的十七號屋內 QA，每次建好房屋就套用目前的 A／B／C 方案；可自由走動、調查、H0～H3，
    /// 另有固定視點、FOV 60／70、灰盒／原材質、俯視平面圖（含目前看得到的地面）與盲測標籤。
    /// 不改正式 Demo、其他 QA、玩家操作（移動、滑鼠、Esc、點擊恢復都沿用 Case01HouseQA）。
    /// </summary>
    public sealed class Case01SpatialQA : MonoBehaviour
    {
        public struct View
        {
            public string Name;
            public Vector3 Position;
            public float Yaw, Pitch;
            /// <summary>只有方案 C 有的視點（臥室短走廊）；其他方案改用 <see cref="Fallback"/>。</summary>
            public bool OnlyC;
            public int Fallback;

            public View(string name, float x, float y, float z, float yaw, float pitch, bool onlyC = false, int fallback = -1)
            {
                Name = name;
                Position = new Vector3(x, y, z);
                Yaw = yaw;
                Pitch = pitch;
                OnlyC = onlyC;
                Fallback = fallback;
            }
        }

        private const float Up = Case01HouseBuilder.FloorHeight + 0.05f;

        /// <summary>固定視點（三個方案同一組座標；同眼高 1.65 m）。</summary>
        public static readonly View[] Views =
        {
            new View("玄關：進門看向屋內", 7.1f, 0.05f, 0.9f, -20f, 0f),
            new View("玄關：看客廳門", 6.4f, 0.05f, 2.3f, -100f, 0f),
            new View("客廳：入口內看餐廳方向", 4.6f, 0.05f, 1.6f, -25f, 0f),
            new View("客廳：靠近餐廳側", 4.3f, 0.05f, 4.7f, -40f, 0f),
            new View("餐廳：看廚房門", 1.4f, 0.05f, 6.3f, 75f, 0f),
            new View("廚房：剛進門", 5.9f, 0.05f, 7.6f, 95f, 0f),
            new View("廚房：看樓梯起步", 7.6f, 0.05f, 6.2f, 120f, 0f),
            new View("樓梯：上到一半看平台", 9.4f, 1.75f, 2.6f, 180f, -12f),
            new View("二樓平台：看房門", 8.3f, Up, 1.5f, -50f, 0f),
            new View("二樓平台：看第三扇門", 6.95f, Up, 2.8f, 180f, 0f),
            new View("書房：入口", 6.95f, Up, 5.7f, 20f, 0f),
            new View("臥室：入口（經書房）", 5.7f, Up, 7.5f, -90f, 0f),
            new View("臥室：入口（C 短走廊）", 4.15f, Up, 4.4f, 0f, 0f, true, 11),
        };

        public Case01HouseQA House { get; private set; }
        public Case01SpatialVariant Variant { get; private set; }
        public bool Graybox { get; private set; } = true;
        public float FieldOfView { get; private set; } = 70f;
        public int Stage { get; private set; }
        public int ViewIndex { get; private set; } = -1;
        /// <summary>上一個視點是否改用替代視點（不是嚴格同座標）。</summary>
        public bool ViewSubstituted { get; private set; }
        /// <summary>上一次切換方案時，原位置在新方案的牆或家具裡，被移到最近的安全點（不是嚴格同座標）。</summary>
        public bool PositionRemapped { get; private set; }
        /// <summary>0＝關、1＝小地圖、2＝大地圖。</summary>
        public int MapMode { get; private set; } = 1;
        public bool Blind { get; private set; }
        public string LastToast { get; private set; }
        public string Metrics { get; private set; } = "";
        public string FocusText { get; private set; } = "";

        private bool built, playMode;
        private int[] blindOrder = { 0, 1, 2 };
        private Case01HouseLayout grayLayout;
        private readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        private Material grayWall, grayFloor, grayCeil, grayFurn, grayStair, grayFocus, grayLining;
        private Vector3? pendingCheck;
        private int pendingFrame;
        private float metricsTimer;
        private readonly List<Vector2> visibleCells = new List<Vector2>();

        // ---------------- 建立 ----------------

        private void Awake()
        {
            if (Application.isPlaying) Setup(true);
        }

        /// <summary>建立整個 QA。playMode＝false 供 EditMode 測試與截圖。</summary>
        public void Setup(bool play, CombatLabScenario scenario = null, int? seed = null)
        {
            if (built) return;
            built = true;
            playMode = play;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            var rng = new System.Random(play ? System.Environment.TickCount : 16);
            for (int i = 2; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = blindOrder[i];
                blindOrder[i] = blindOrder[j];
                blindOrder[j] = t;
            }
            MakeGrayMaterials();
            var go = new GameObject("Case01HouseQA");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            House = go.AddComponent<Case01HouseQA>();
            House.InputSource = new Filter(new LegacyCase01Input(), this);
            House.LayoutPostBuild = l => Case01HouseBuilder.ApplySpatialVariant(l, Variant);
            House.Build(play, scenario, seed, Case01Checkpoint.S00, Case01QaMode.Standard, Case01Floorplan.V05A);
            go.SetActive(true);
            BuildUi();
            AfterRebuild();
        }

        /// <summary>換輸入來源（PlayMode 測試用腳本輸入）；一樣經過 QA 的功能鍵過濾。</summary>
        public void UseInput(ICase01InputSource source)
        {
            House.InputSource = new Filter(source, this);
        }

        /// <summary>QA 專用輸入：F1～F7、F9、F10 由這裡處理（不交給屋內 QA，避免觸發它原本的檢查點跳轉）；F8 降低閃爍照舊。</summary>
        private sealed class Filter : ICase01InputSource
        {
            private readonly ICase01InputSource inner;
            private readonly Case01SpatialQA owner;

            public Filter(ICase01InputSource inner, Case01SpatialQA owner)
            {
                this.inner = inner;
                this.owner = owner;
            }

            public Case01Input Read()
            {
                var r = inner.Read();
                int key = r.Jump > 0 ? r.Jump : r.Function;
                r.Jump = 0;
                if (r.Function != 8) r.Function = 0;
                if (key > 0 && key != 8) owner.Press(key);
                return r;
            }
        }

        // ---------------- 按鍵 ----------------

        /// <summary>處理 QA 功能鍵（1～10＝F1～F10）。測試直接呼叫。</summary>
        public void Press(int key)
        {
            switch (key)
            {
                case 1:
                    ViewIndex = -1;
                    Place(House.Layout.Spawn.Position, House.Layout.Spawn.Yaw, 0f);
                    Toast("QA：回到玄關（出生點）");
                    break;
                case 2: GoView((ViewIndex + 1) % Views.Length); break;
                case 3: GoView((ViewIndex - 1 + Views.Length) % Views.Length); break;
                case 4:
                    MapMode = (MapMode + 1) % 3;
                    Toast(MapMode == 0 ? "QA：平面圖　關" : MapMode == 1 ? "QA：平面圖　小" : "QA：平面圖　大（黃色＝目前畫面看得到的地面）");
                    break;
                case 5:
                    FieldOfView = FieldOfView > 65f ? 60f : 70f;
                    Toast("QA：垂直 FOV " + FieldOfView.ToString("0") + "°（只改鏡頭，不改幾何、眼高與移動）");
                    break;
                case 6: SetStage((Stage + 1) % 4); break;
                case 7: SetGraybox(!Graybox); break;
                case 9: SetVariant((Case01SpatialVariant)(((int)Variant + 1) % 3)); break;
                case 10:
                    Blind = !Blind;
                    Toast(Blind ? "QA：盲測　開（方案只顯示代號）" : "QA：盲測　關｜甲＝" + ShortName(Unblind(0)) + "、乙＝" + ShortName(Unblind(1)) + "、丙＝" + ShortName(Unblind(2)));
                    break;
            }
            UpdateMetrics();
            RefreshUi();
        }

        /// <summary>切換方案：同一流程（劇情進度不變）、同位置與視角重建；原位置在新方案的牆／家具裡時移到最近的安全點。</summary>
        public void SetVariant(Case01SpatialVariant v)
        {
            var p = House.Player;
            var pos = p.transform.position;
            float yaw = p.Yaw, pitch = p.Pitch;
            Variant = v;
            House.QaView(House.Flow, new Case01Pose(pos, yaw), pitch);
            AfterRebuild();
            PositionRemapped = false;
            RequestCheck(pos);
            Toast("QA：" + VariantLabel());
        }

        /// <summary>H0～H3（站在原地、視角不變；以該階段的流程重建）。</summary>
        public void SetStage(int h)
        {
            Stage = Mathf.Clamp(h, 0, 3);
            var p = House.Player;
            var pos = p.transform.position;
            float yaw = p.Yaw, pitch = p.Pitch;
            House.QaView(Case01Flow.ForHouseStage(Stage), new Case01Pose(pos, yaw), pitch);
            AfterRebuild();
            Toast("QA：房屋 H" + Stage);
        }

        public void SetGraybox(bool on)
        {
            Graybox = on;
            ApplyGraybox();
            Toast(on ? "QA：灰盒（只看牆、門洞、樓梯與家具量體；橘色＝調查點）" : "QA：原材質（v0.5 方案 A 的材質與燈光）");
        }

        public void GoView(int i)
        {
            ViewIndex = i;
            var v = Views[i];
            ViewSubstituted = false;
            if (v.OnlyC && Variant != Case01SpatialVariant.C)
            {
                v = Views[v.Fallback];
                ViewSubstituted = true;
            }
            Place(v.Position, v.Yaw, v.Pitch);
            Toast("QA：視點 " + (i + 1) + "／" + Views.Length + "　" + Views[i].Name + (ViewSubstituted ? "（此方案沒有這條走廊 → 改用「" + v.Name + "」）" : ""));
        }

        private void Place(Vector3 pos, float yaw, float pitch)
        {
            House.Player.Teleport(pos, yaw, pitch);
            SnapCamera();
            PositionRemapped = false;
            RequestCheck(pos);
        }

        private void SnapCamera()
        {
            var cam = House.ExploreCamera;
            cam.transform.position = House.Player.EyePosition;
            cam.transform.rotation = House.Player.ViewRotation;
            cam.fieldOfView = FieldOfView;
        }

        // ---------------- 位置合法性 ----------------

        private void RequestCheck(Vector3 pos)
        {
            // Play 模式剛重建的那一影格，裝飾物的碰撞體還沒真正刪掉（Destroy 延到影格結束），下一影格再檢查。
            if (playMode)
            {
                pendingCheck = pos;
                pendingFrame = Time.frameCount;
            }
            else ResolvePosition(pos);
        }

        private void ResolvePosition(Vector3 pos)
        {
            if (Free(pos)) return;
            var alt = NearestFree(pos);
            var target = alt ?? House.Layout.Spawn.Position;
            House.Player.Teleport(target, House.Player.Yaw, House.Player.Pitch);
            SnapCamera();
            PositionRemapped = true;
            Toast("QA：原位置在此方案的牆或家具裡 → 移到最近的安全點（不是嚴格同座標比較）");
        }

        public bool Free(Vector3 feet)
        {
            var cc = House.Player.Body;
            bool was = cc.enabled;
            cc.enabled = false;
            const float r = 0.33f;
            bool hit = Physics.CheckCapsule(feet + Vector3.up * (r + 0.06f), feet + Vector3.up * (FirstPersonController.Height - r), r, Case01SpatialMap.SightMask, QueryTriggerInteraction.Ignore);
            cc.enabled = was;
            return !hit;
        }

        private Vector3? NearestFree(Vector3 pos)
        {
            var room = House.Layout.RoomAt(pos);
            for (float rad = 0.15f; rad <= 1.6f; rad += 0.15f)
            {
                for (int k = 0; k < 16; k++)
                {
                    float a = k * Mathf.PI / 8f;
                    var c = pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rad;
                    if ((room == Case01Room.None || House.Layout.RoomAt(c) == room) && Free(c)) return c;
                }
            }
            return null;
        }

        // ---------------- 每影格 ----------------

        private void LateUpdate()
        {
            if (!built || House == null || House.Layout == null) return;
            if (pendingCheck.HasValue && Time.frameCount > pendingFrame)
            {
                var p = pendingCheck.Value;
                pendingCheck = null;
                ResolvePosition(p);
            }
            if (House.Layout != grayLayout) AfterRebuild();
            Frame(Time.deltaTime);
        }

        /// <summary>每影格的 QA 覆寫（Play：LateUpdate；測試可直接呼叫）：FOV、HUD 說明列、平面圖標記、可見範圍。</summary>
        public void Frame(float dt)
        {
            var cam = House.ExploreCamera;
            if (House.Mode == Case01Mode.Exploring && cam.enabled) cam.fieldOfView = FieldOfView;
            House.Hud.Base.ViewMode.text = "Phase 1.6 空間結構灰盒 QA｜F1～F10 見右側面板";
            metricsTimer -= dt;
            if (metricsTimer <= 0f)
            {
                metricsTimer = 0.25f;
                UpdateMetrics();
            }
            RefreshUi();
        }

        private void AfterRebuild()
        {
            grayLayout = House.Layout;
            originals.Clear();
            ApplyGraybox();
            RebuildMaps();
            SnapCamera();
            UpdateMetrics();
            RefreshUi();
        }

        // ---------------- 名稱 ----------------

        public static string VariantName(Case01SpatialVariant v)
        {
            switch (v)
            {
                case Case01SpatialVariant.B: return "B｜錯位開口與視線遮擋（不改分區）";
                case Case01SpatialVariant.C: return "C｜過渡空間與節奏（C1，有限重分區，不擴框）";
                default: return "A｜v0.5 方案 A 原樣（基準）";
            }
        }

        private static string ShortName(Case01SpatialVariant v) { return v.ToString(); }

        private static readonly string[] blindNames = { "甲", "乙", "丙" };

        private Case01SpatialVariant Unblind(int code)
        {
            for (int i = 0; i < 3; i++) if (blindOrder[i] == code) return (Case01SpatialVariant)i;
            return Case01SpatialVariant.A;
        }

        public string VariantLabel()
        {
            return Blind ? "方案 " + blindNames[blindOrder[(int)Variant]] : VariantName(Variant);
        }

        private void Toast(string s)
        {
            LastToast = s;
            if (House != null && House.Hud != null) House.Hud.Base.ShowToast(s);
        }

        // ---------------- 灰盒 ----------------

        private void MakeGrayMaterials()
        {
            grayWall = ArenaArt.Mat(new Color(0.78f, 0.78f, 0.76f), 0.05f);
            grayLining = ArenaArt.Mat(new Color(0.86f, 0.8f, 0.62f), 0.05f);
            grayFloor = ArenaArt.Mat(new Color(0.42f, 0.42f, 0.43f), 0.05f);
            grayCeil = ArenaArt.Mat(new Color(0.66f, 0.66f, 0.66f), 0.05f);
            grayFurn = ArenaArt.Mat(new Color(0.55f, 0.57f, 0.6f), 0.1f);
            grayStair = ArenaArt.Mat(new Color(0.64f, 0.58f, 0.5f), 0.05f);
            grayFocus = ArenaArt.Mat(new Color(0.95f, 0.58f, 0.22f), 0.1f);
        }

        private void OnDestroy()
        {
            foreach (var m in new[] { grayWall, grayLining, grayFloor, grayCeil, grayFurn, grayStair, grayFocus }) if (m != null) ArenaArt.Kill(m);
            foreach (var t in new Texture[] { plan0, plan1, sightTex }) if (t != null) ArenaArt.Kill(t);
        }

        private static readonly HashSet<string> wallNames = new HashSet<string> { "Wall", "Lintel", "WindowHead", "WindowApron", "ChimneyBreast", "Skirting", "Casing", "Bulkhead", "UnderStairPanel" };
        private static readonly HashSet<string> floorNames = new HashSet<string> { "Floor", "Slab", "KitchenTiles", "Rug", "DoorMat", "Runner", "StudyRug", "BedsideRug" };
        private static readonly HashSet<string> keepNames = new HashSet<string> { "Glass", "Bulb", "Shade", "Cord", "SconceShade", "Head", "LampShade" };

        /// <summary>灰盒：房屋本體換成中性灰（牆淺、地板深、家具中灰、門框深度淡黃、調查點橘色）；窗玻璃、燈泡燈罩、失序門、舊化痕跡、窗外與異常空間不換。</summary>
        private void ApplyGraybox()
        {
            foreach (var kv in originals) if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
            originals.Clear();
            if (!Graybox || House.Layout == null) return;
            var L = House.Layout;
            var skipRoots = new List<Transform>();
            foreach (var n in new[] { "Aging", "Mezzanine", "Courtyard", "EvidenceFP001", "MezzanineScatter" })
            {
                var t = L.Root.Find(n);
                if (t != null) skipRoots.Add(t);
            }
            if (L.Exterior != null && L.Exterior.Root != null) skipRoots.Add(L.Exterior.Root);
            foreach (var g in new[] { L.ThirdDoorFrame, L.Door17Frame, L.TearFrame }) if (g != null) skipRoots.Add(g.transform);
            var focusBounds = new List<Bounds>();
            foreach (var it in L.Interactables.Values)
            {
                if (it == null || !it.gameObject.activeInHierarchy) continue;
                var c = it.GetComponent<BoxCollider>();
                if (c == null) continue;
                var b = new Bounds(it.transform.TransformPoint(c.center), Vector3.Scale(c.size, it.transform.lossyScale));
                b.Expand(0.03f);
                focusBounds.Add(b);
            }
            foreach (var r in L.Root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var t = r.transform;
                string n = t.name;
                if (keepNames.Contains(n) || n.Contains("Portal") && !n.StartsWith("PortalSeal") || n.Contains("Edge") || n.Contains("Void") || n.Contains("Membrane")) continue;
                bool skip = false;
                foreach (var s in skipRoots) if (t.IsChildOf(s)) skip = true;
                if (skip) continue;
                var c = r.bounds.center;
                if (c.x < -0.6f || c.x > 10.6f || c.z < -0.7f || c.z > 10.6f || c.y > 6.3f) continue;
                Material m;
                if (wallNames.Contains(n) || n.StartsWith("PortalSeal")) m = grayWall;
                else if (n.StartsWith("Lining_")) m = grayLining;
                else if (floorNames.Contains(n)) m = grayFloor;
                else if (n == "CeilingPlaster" || n == "Roof") m = grayCeil;
                else if (n == "Step") m = grayStair;
                else
                {
                    m = grayFurn;
                    foreach (var b in focusBounds)
                        if (b.Intersects(r.bounds) && r.bounds.size.magnitude < 2.5f)
                        {
                            m = grayFocus;
                            break;
                        }
                }
                originals[r] = r.sharedMaterials;
                var arr = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < arr.Length; i++) arr[i] = m;
                r.sharedMaterials = arr;
            }
        }

        // ---------------- 量測 ----------------

        public Dictionary<Case01Room, Vector2Int> LastStats { get; private set; }
        public List<string> LastFoci { get; private set; }

        public static float Aspect(Camera cam)
        {
            return cam.targetTexture != null ? cam.targetTexture.width / (float)cam.targetTexture.height : cam.pixelHeight > 0 ? cam.pixelWidth / (float)cam.pixelHeight : 16f / 9f;
        }

        public void UpdateMetrics()
        {
            if (House == null || House.ExploreCamera == null) return;
            UpdateMetrics(Aspect(House.ExploreCamera));
        }

        /// <summary>依目前眼位、視角與 FOV 計算可見地面與焦點（截圖報表指定長寬比）。</summary>
        public void UpdateMetrics(float aspect)
        {
            if (House == null || House.Layout == null || House.Player == null) return;
            var eye = House.Player.EyePosition;
            var view = House.Player.ViewRotation;
            LastStats = Case01SpatialMap.Visible(House.Layout, eye, view, FieldOfView, aspect, visibleCells);
            LastFoci = Case01SpatialMap.VisibleFoci(House.Layout, eye, view, FieldOfView, aspect);
            Metrics = Case01SpatialMap.Summary(LastStats, House.Layout.RoomAt(House.Player.transform.position));
            FocusText = LastFoci.Count == 0 ? "（無）" : string.Join("、", LastFoci.ToArray());
            DrawSight();
        }

        // ---------------- UI（QA 專用 Canvas，不動一般 HUD） ----------------

        public const int MapPpm = 40;
        private Canvas canvas;
        private Text panelText, mapTitle;
        private RectTransform mapRoot, playerDot, playerDir, fovL, fovR, sightRect;
        private RawImage mapImage, sightImage;
        private readonly List<Text> roomLabels = new List<Text>();
        private Texture2D plan0, plan1, sightTex;
        private int mapFloor = -1;

        public Texture2D PlanTexture(int floor) { return floor == 0 ? plan0 : plan1; }
        public Canvas QaCanvas { get { return canvas; } }

        private void BuildUi()
        {
            var cgo = new GameObject("Case01SpatialQaCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)cgo.transform;

            var panel = Rect(root, "SpatialPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -98f), new Vector2(560f, 268f));
            Img(panel, new Color(0.03f, 0.035f, 0.045f, 0.78f));
            panelText = Txt(panel, 17, TextAnchor.UpperLeft, new Color(0.88f, 0.9f, 0.92f));
            Inset(panelText.rectTransform, 12f, 8f);

            mapRoot = Rect(root, "SpatialMap", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 128f), new Vector2(380f, 410f));
            Img(mapRoot, new Color(0.03f, 0.035f, 0.045f, 0.82f));
            mapTitle = Txt(Rect(mapRoot, "Title", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -4f), new Vector2(560f, 26f)), 16, TextAnchor.UpperLeft, new Color(0.9f, 0.9f, 0.85f));
            var area = Rect(mapRoot, "Plan", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(6f, 6f), new Vector2(368f, 368f));
            mapImage = area.gameObject.AddComponent<RawImage>();
            mapImage.raycastTarget = false;
            sightRect = Rect(area, "Sight", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            sightImage = sightRect.gameObject.AddComponent<RawImage>();
            sightImage.raycastTarget = false;
            sightTex = new Texture2D(40, 40, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Case01SpatialSight" };
            sightImage.texture = sightTex;
            fovL = Line(area, new Color(1f, 0.85f, 0.4f, 0.8f));
            fovR = Line(area, new Color(1f, 0.85f, 0.4f, 0.8f));
            playerDir = Line(area, new Color(1f, 0.3f, 0.25f, 1f));
            playerDot = Rect(area, "Player", new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(11f, 11f));
            Img(playerDot, new Color(1f, 0.3f, 0.25f, 1f));
        }

        internal static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        internal static Image Img(RectTransform rt, Color c)
        {
            var i = rt.gameObject.AddComponent<Image>();
            i.color = c;
            i.raycastTarget = false;
            return i;
        }

        internal static Text Txt(RectTransform rt, int size, TextAnchor anchor, Color c)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            var t = go.AddComponent<Text>();
            ((RectTransform)go.transform).SetParent(rt, false);
            Inset(t.rectTransform, 0f, 0f);
            t.font = Case01Hud.UiFont;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = c;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        internal static void Inset(RectTransform rt, float x, float y)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(x, y);
            rt.offsetMax = new Vector2(-x, -y);
        }

        internal static RectTransform Line(RectTransform parent, Color c)
        {
            var rt = Rect(parent, "Line", Vector2.zero, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(2f, 10f));
            Img(rt, c);
            return rt;
        }

        private void RebuildMaps()
        {
            if (plan0 != null) ArenaArt.Kill(plan0);
            if (plan1 != null) ArenaArt.Kill(plan1);
            plan0 = Case01SpatialMap.Plan(House.Layout, 0, MapPpm);
            plan1 = Case01SpatialMap.Plan(House.Layout, 1, MapPpm);
            mapFloor = -1;
        }

        private void DrawSight()
        {
            if (sightTex == null) return;
            var px = new Color32[40 * 40];
            var c = new Color32(255, 214, 90, 120);
            foreach (var cell in visibleCells)
            {
                int x = Mathf.Clamp((int)(cell.x / Case01SpatialMap.Cell), 0, 39), z = Mathf.Clamp((int)(cell.y / Case01SpatialMap.Cell), 0, 39);
                px[z * 40 + x] = c;
            }
            sightTex.SetPixels32(px);
            sightTex.Apply(false);
        }

        private void RefreshUi()
        {
            if (canvas == null || House == null || House.Player == null) return;
            var p = House.Player;
            var pos = p.transform.position;
            var room = House.Layout.RoomAt(pos);
            string view = ViewIndex < 0 ? "自由行走" : (ViewIndex + 1) + "／" + Views.Length + " " + Views[ViewIndex].Name + (ViewSubstituted ? " <color=#E6A050>［替代視點］</color>" : "");
            panelText.text =
                "<b>Phase 1.6 空間結構灰盒 QA</b>　<color=#E6BD52>" + VariantLabel() + "</color>\n" +
                "位置：" + Case01Texts.RoomName(room) + "（" + pos.x.ToString("0.0") + ", " + pos.z.ToString("0.0") + "）朝向 " + p.Yaw.ToString("0") + "°" +
                (PositionRemapped ? " <color=#E6A050>［已移到安全點］</color>" : "") + "\n" +
                "FOV " + FieldOfView.ToString("0") + "°｜眼高 " + FirstPersonController.EyeHeight.ToString("0.00") + " m｜H" + Stage + "｜" + (Graybox ? "灰盒" : "原材質") +
                "｜滑鼠 " + (House.CursorCaptured ? "鎖定" : "<color=#E6A050>已釋放（點擊畫面恢復）</color>") + "\n" +
                "視點：" + view + "\n" +
                "畫面中看得到的地面：" + Metrics + "\n" +
                "看得到的焦點：" + FocusText + "\n" +
                "<color=#9AA4B0>F1 回玄關｜F2／F3 下／上一個視點｜F4 平面圖｜F5 FOV 70／60｜F6 H0～H3｜F7 灰盒／原材質｜F8 降低閃爍｜F9 方案 A→B→C｜F10 盲測｜Esc 釋放滑鼠</color>";

            mapRoot.gameObject.SetActive(MapMode > 0);
            if (MapMode == 0) return;
            float side = MapMode == 1 ? 368f : 820f;
            mapRoot.anchorMin = mapRoot.anchorMax = mapRoot.pivot = MapMode == 1 ? new Vector2(1f, 0f) : new Vector2(0.5f, 0.5f);
            mapRoot.anchoredPosition = MapMode == 1 ? new Vector2(-16f, 128f) : Vector2.zero;
            mapRoot.sizeDelta = new Vector2(side + 12f, side + 42f);
            var area = (RectTransform)mapImage.transform;
            area.sizeDelta = new Vector2(side, side);
            int floor = Case01SpatialMap.FloorOf(pos.y + 0.6f);
            if (floor != mapFloor)
            {
                mapFloor = floor;
                mapImage.texture = floor == 0 ? plan0 : plan1;
                foreach (var t in roomLabels) if (t != null) ArenaArt.Kill(t.transform.parent.gameObject);
                roomLabels.Clear();
                var seen = new HashSet<Case01Room>();
                foreach (var r in House.Layout.Rooms)
                {
                    if (!Case01SpatialMap.HouseRoom(r.Room) || Case01SpatialMap.FloorOf(r.Bounds.center.y) != floor || r.Bounds.size.x < 2f || !seen.Add(r.Room)) continue;
                    var lrt = Rect(area, "Room", Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 24f));
                    var t = Txt(lrt, 15, TextAnchor.MiddleCenter, new Color(0.95f, 0.95f, 0.9f, 0.85f));
                    t.text = Case01Texts.RoomName(r.Room);
                    lrt.name = "Room_" + r.Room;
                    lrt.SetSiblingIndex(1);
                    roomLabels.Add(t);
                    lrt.anchoredPosition = Case01SpatialMap.ToMap(r.Bounds.center, side / Case01SpatialMap.Size);
                }
            }
            float k = side / Case01SpatialMap.Size;
            foreach (var t in roomLabels)
            {
                var rt = (RectTransform)t.transform.parent;
                var rr = House.Layout.Rooms.Find(v => "Room_" + v.Room == rt.name && Case01SpatialMap.FloorOf(v.Bounds.center.y) == floor && v.Bounds.size.x >= 2f);
                if (rr != null) rt.anchoredPosition = Case01SpatialMap.ToMap(rr.Bounds.center, k);
            }
            mapTitle.text = (floor == 0 ? "一樓" : "二樓") + "平面（碰撞體）｜" + VariantLabel() + (MapMode == 2 ? "｜黃＝畫面看得到的地面" : "");
            sightRect.gameObject.SetActive(floor == Case01SpatialMap.FloorOf(p.EyePosition.y - 1.0f));
            sightRect.anchoredPosition = Case01SpatialMap.ToMap(Vector3.zero, k);
            sightRect.sizeDelta = new Vector2(10f * k, 10f * k);
            sightRect.pivot = Vector2.zero;
            var mp = Case01SpatialMap.ToMap(pos, k);
            playerDot.anchoredPosition = mp;
            playerDot.localEulerAngles = new Vector3(0f, 0f, 45f - p.Yaw);
            Ray(playerDir, mp, p.Yaw, 0.9f * k);
            float h = Mathf.Atan(Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) * Aspect(House.ExploreCamera)) * Mathf.Rad2Deg;
            Ray(fovL, mp, p.Yaw - h, 3f * k);
            Ray(fovR, mp, p.Yaw + h, 3f * k);
        }

        private static void Ray(RectTransform rt, Vector2 from, float yaw, float length)
        {
            rt.anchoredPosition = from;
            rt.sizeDelta = new Vector2(2f, length);
            rt.localEulerAngles = new Vector3(0f, 0f, -yaw);
        }
    }
}
