using System.Collections.Generic;
using System.Text;
using Shepherd.Battle.UI;
using Shepherd.Battle.UI.Arena;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// Phase 1.7 住宅尺度與探索重構 QA（選單「Shepherd/Case01/Phase 1.7 住宅尺度與探索重構 QA」）。
    /// 包一個十七號屋內 QA，可切 O（v0.5 方案 A＝Phase 1.6 玩過的版本）／R1（長形連棟）／R2（R1＋空間節奏），
    /// 可即時切室內步速、FOV、眼高、滑鼠靈敏度、加減速，並記錄這一趟手動走到各房間的秒數。
    /// 劇情、互動、戰鬥、異域照舊（同一個 Case01HouseQA）。正式 Demo 與其他 QA 不受影響（玩家的預設常數沒變）。
    /// </summary>
    public sealed class Case01ScaleQA : MonoBehaviour
    {
        public static readonly Case01Floorplan[] Plans = { Case01Floorplan.V05A, Case01Floorplan.R1, Case01Floorplan.R2 };
        /// <summary>步行／奔跑速度候選（m/s）；最後一組＝目前正式值。候選只是待驗收值，不是正式常數。</summary>
        public static readonly Vector2[] Speeds = { new Vector2(1.8f, 3.2f), new Vector2(2.2f, 3.8f), new Vector2(2.6f, 4.4f), new Vector2(FirstPersonController.WalkSpeed, FirstPersonController.RunSpeed) };
        public static readonly float[] Fovs = { 55f, 60f, 65f, 70f };
        public static readonly float[] EyeHeights = { 1.58f, 1.65f, 1.72f };
        public static readonly float[] LookScales = { 0.65f, 1f, 1.4f };
        public const float AccelerationOn = 12f;

        public struct View
        {
            public string Name;
            public Vector3 Position;
            public float Yaw, Pitch;

            public View(string name, float x, float y, float z, float yaw, float pitch = 0f)
            {
                Name = name;
                Position = new Vector3(x, y, z);
                Yaw = yaw;
                Pitch = pitch;
            }
        }

        private const float Up = Case01HouseBuilder.FloorHeight + 0.05f;

        /// <summary>固定視點（截圖／同角度比較）。三個格局用相同的名稱與順序，位置各自對應。</summary>
        public static View[] ViewsFor(Case01Floorplan p)
        {
            if (p == Case01Floorplan.V05A)
                return new[]
                {
                    new View("玄關：進門看屋內", 7.1f, 0.05f, 0.9f, -20f),
                    new View("客廳：入口", 4.6f, 0.05f, 1.6f, -60f),
                    new View("客廳：看餐廳方向", 4.3f, 0.05f, 4.7f, -40f),
                    new View("餐廳：看廚房", 1.4f, 0.05f, 6.3f, 75f),
                    new View("廚房：剛進門", 5.9f, 0.05f, 7.6f, 95f),
                    new View("走廊／樓梯起步", 7.6f, 0.05f, 6.2f, 120f),
                    new View("樓梯：上到一半", 9.4f, 1.75f, 2.6f, 180f, -12f),
                    new View("二樓平台：看第三扇門", 6.95f, Up, 2.8f, 180f),
                    new View("書房：入口", 6.95f, Up, 5.7f, 20f),
                    new View("臥室：入口", 5.7f, Up, 7.5f, -90f),
                };
            bool r2 = p == Case01Floorplan.R2;
            return new[]
            {
                new View("玄關：進門看走廊", 7.1f, 0.05f, r2 ? 0.8f : 0.9f, 0f),
                new View("客廳：入口", 5.9f, 0.05f, r2 ? Case01HouseBuilder.R17.R2LivingDoorZ : Case01HouseBuilder.R17.R1LivingDoorZ, -95f),
                new View("客廳：看餐廳方向", 4.75f, 0.05f, 3.25f, -50f),
                new View("餐廳：看廚房", 1.6f, 0.05f, 6.6f, 60f),
                new View("廚房：剛進門", r2 ? Case01HouseBuilder.R17.R2KitchenDoorX : Case01HouseBuilder.R17.R1KitchenDoorX, 0.05f, 12.7f, 0f),
                new View("走廊／樓梯起步", 7.7f, 0.05f, 4.6f, 0f),
                new View("樓梯：上到一半", 9.45f, 1.6f, 8.6f, 180f, -10f),
                new View("二樓平台：看第三扇門", 8.4f, Up, 5.8f, 180f),
                new View("書房：入口", 5.8f, Up, Case01HouseBuilder.R17.StudyDoorZ, -90f),
                new View("臥室：入口", 5.9f, Up, Case01HouseBuilder.R17.BedroomDoorZ, -90f),
                new View("走廊盡頭回看前門", 7.7f, 0.05f, 11.2f, 180f),
            };
        }

        public Case01HouseQA House { get; private set; }
        public Case01Floorplan Plan { get; private set; } = Case01Floorplan.R1;
        public int SpeedIndex { get; private set; } = 1;
        public int FovIndex { get; private set; } = 3;
        public int EyeIndex { get; private set; } = 1;
        public int LookIndex { get; private set; } = 1;
        public bool Smooth { get; private set; }
        public int Stage { get; private set; }
        public int ViewIndex { get; private set; } = -1;
        public bool PositionRemapped { get; private set; }
        public bool MapOn { get; private set; } = true;
        public string LastToast { get; private set; }

        /// <summary>這一趟（F1 或換格局後重算）第一次走進各房間的秒數與水平步行距離。</summary>
        public readonly List<KeyValuePair<Case01Room, float>> Arrivals = new List<KeyValuePair<Case01Room, float>>();
        public float RunClock { get; private set; }
        public float Walked { get; private set; }

        public Vector2 Speed { get { return Speeds[SpeedIndex]; } }
        public float FieldOfView { get { return Fovs[FovIndex]; } }
        public float EyeHeight { get { return EyeHeights[EyeIndex]; } }
        public float LookScale { get { return LookScales[LookIndex]; } }

        private bool built, playMode;
        private Vector3? pendingCheck;
        private int pendingFrame;
        private Case01HouseLayout seenLayout;
        private FirstPersonController seenPlayer;
        private Vector3 lastPos;
        private Case01Room lastRoom;

        private void Awake()
        {
            if (Application.isPlaying) Setup(true);
        }

        public void Setup(bool play, CombatLabScenario scenario = null, int? seed = null, Case01Floorplan? plan = null, Case01QaMode mode = Case01QaMode.CoreV03)
        {
            if (built) return;
            built = true;
            playMode = play;
            if (plan.HasValue) Plan = plan.Value;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            var go = new GameObject("Case01HouseQA");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            House = go.AddComponent<Case01HouseQA>();
            House.InputSource = new Filter(new LegacyCase01Input(), this);
            House.Build(play, scenario, seed, Case01Checkpoint.S00, mode, Plan);
            go.SetActive(true);
            BuildUi();
            AfterRebuild();
            ResetRun();
        }

        public void UseInput(ICase01InputSource source)
        {
            House.InputSource = new Filter(source, this);
        }

        /// <summary>QA 功能鍵由這裡處理（不交給屋內 QA）；F8 降低閃爍照舊。F11、F12 直接讀鍵盤（只在 Play）。E、WASD、滑鼠、J、Tab、1～3 不受影響。</summary>
        private sealed class Filter : ICase01InputSource
        {
            private readonly ICase01InputSource inner;
            private readonly Case01ScaleQA owner;

            public Filter(ICase01InputSource inner, Case01ScaleQA owner)
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
                if (Application.isPlaying && inner is LegacyCase01Input)
                {
                    if (Input.GetKeyDown(KeyCode.F11)) key = 11;
                    else if (Input.GetKeyDown(KeyCode.F12)) key = 12;
                }
                if (key > 0 && key != 8) owner.Press(key);
                return r;
            }
        }

        // ---------------- 按鍵 ----------------

        /// <summary>QA 功能鍵（1～12＝F1～F12）。</summary>
        public void Press(int key)
        {
            switch (key)
            {
                case 1:
                    ViewIndex = -1;
                    Place(House.Layout.Spawn.Position, House.Layout.Spawn.Yaw, 0f);
                    ResetRun();
                    Toast("QA：回到玄關（劇情進度不變；路線計時重新開始）");
                    break;
                case 2: GoView((ViewIndex + 1) % ViewsFor(Plan).Length); break;
                case 3: GoView((ViewIndex - 1 + ViewsFor(Plan).Length) % ViewsFor(Plan).Length); break;
                case 4:
                    MapOn = !MapOn;
                    Toast(MapOn ? "QA：平面圖　開" : "QA：平面圖　關");
                    break;
                case 5:
                    FovIndex = (FovIndex + 1) % Fovs.Length;
                    Toast("QA：垂直 FOV " + FieldOfView.ToString("0") + "°");
                    break;
                case 6:
                    SpeedIndex = (SpeedIndex + 1) % Speeds.Length;
                    Toast("QA：步速 " + SpeedText());
                    break;
                case 7:
                    EyeIndex = (EyeIndex + 1) % EyeHeights.Length;
                    Toast("QA：眼高 " + EyeHeight.ToString("0.00") + " m（碰撞身高 1.8 m、半徑 0.35 m 不變）");
                    break;
                case 9: SetPlan(Plans[(System.Array.IndexOf(Plans, Plan) + 1) % Plans.Length]); break;
                case 10:
                    LookIndex = (LookIndex + 1) % LookScales.Length;
                    Toast("QA：滑鼠靈敏度 ×" + LookScale.ToString("0.00"));
                    break;
                case 11: SetStage((Stage + 1) % 4); break;
                case 12:
                    Smooth = !Smooth;
                    Toast(Smooth ? "QA：柔和加減速　開" : "QA：柔和加減速　關（立即到速）");
                    break;
            }
            ApplyPlayer();
            RefreshUi();
        }

        public static string PlanName(Case01Floorplan p)
        {
            switch (p)
            {
                case Case01Floorplan.R1: return "R1｜長形連棟（10 × 18 m）";
                case Case01Floorplan.R2: return "R2｜R1＋空間節奏";
                default: return "O｜Phase 1.6 玩過的版本（v0.5 A，10 × 10 m）";
            }
        }

        public string SpeedText()
        {
            var s = Speed;
            return "步行 " + s.x.ToString("0.0") + "／奔跑 " + s.y.ToString("0.0") + " m/s" + (SpeedIndex == Speeds.Length - 1 ? "（目前正式值）" : "（候選）");
        }

        /// <summary>換格局：同一劇情進度，回到該格局的玄關（不同格局的座標不能直接對照）。</summary>
        public void SetPlan(Case01Floorplan p)
        {
            Plan = p;
            House.RebuildWithPlan(p, House.Flow, null);
            ViewIndex = -1;
            AfterRebuild();
            ResetRun();
            Toast("QA：" + PlanName(p) + "（同劇情進度，從玄關開始）");
        }

        public void SetStage(int h)
        {
            Stage = Mathf.Clamp(h, 0, 3);
            var p = House.Player;
            House.QaView(Case01Flow.ForHouseStage(Stage), new Case01Pose(p.transform.position, p.Yaw), p.Pitch);
            AfterRebuild();
            Toast("QA：房屋 H" + Stage + "（站在原地）");
        }

        public void GoView(int i)
        {
            var views = ViewsFor(Plan);
            ViewIndex = i;
            var v = views[i];
            Place(v.Position, v.Yaw, v.Pitch);
            Toast("QA：視點 " + (i + 1) + "／" + views.Length + "　" + v.Name);
        }

        private void Place(Vector3 pos, float yaw, float pitch)
        {
            House.Player.Teleport(pos, yaw, pitch);
            lastPos = House.Player.transform.position;
            SnapCamera();
            PositionRemapped = false;
            if (playMode)
            {
                pendingCheck = pos;
                pendingFrame = Time.frameCount;
            }
            else ResolvePosition(pos);
        }

        private void SnapCamera()
        {
            var cam = House.ExploreCamera;
            cam.transform.position = House.Player.EyePosition;
            cam.transform.rotation = House.Player.ViewRotation;
            cam.fieldOfView = FieldOfView;
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

        private void ResolvePosition(Vector3 pos)
        {
            if (Free(pos)) return;
            var room = House.Layout.RoomAt(pos);
            for (float rad = 0.15f; rad <= 1.6f; rad += 0.15f)
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI / 8f;
                var c = pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rad;
                if ((room == Case01Room.None || House.Layout.RoomAt(c) == room) && Free(c))
                {
                    House.Player.Teleport(c, House.Player.Yaw, House.Player.Pitch);
                    SnapCamera();
                    PositionRemapped = true;
                    return;
                }
            }
        }

        private void ResetRun()
        {
            Arrivals.Clear();
            RunClock = 0f;
            Walked = 0f;
            if (House.Player != null)
            {
                lastPos = House.Player.transform.position;
                lastRoom = House.Layout.RoomAt(lastPos);
                Arrivals.Add(new KeyValuePair<Case01Room, float>(lastRoom, 0f));
            }
        }

        /// <summary>把 QA 的步速／眼高／靈敏度／加減速套到目前的玩家（每次重建都會產生新玩家）。</summary>
        private void ApplyPlayer()
        {
            var p = House.Player;
            if (p == null) return;
            p.WalkSpeedValue = Speed.x;
            p.RunSpeedValue = Speed.y;
            p.EyeHeightValue = EyeHeight;
            p.LookScale = LookScale;
            p.Acceleration = Smooth ? AccelerationOn : 0f;
        }

        private void AfterRebuild()
        {
            seenLayout = House.Layout;
            seenPlayer = House.Player;
            ApplyPlayer();
            mapFloor = -1;
            RebuildMaps();
            SnapCamera();
            RefreshUi();
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
            Frame(Time.deltaTime);
        }

        /// <summary>每影格（Play：LateUpdate；測試可直接呼叫）：FOV、玩家參數、路線計時、面板。</summary>
        public void Frame(float dt)
        {
            if (House.Layout != seenLayout || House.Player != seenPlayer)
            {
                AfterRebuild();
                lastPos = House.Player.transform.position;
            }
            var cam = House.ExploreCamera;
            if (House.Mode == Case01Mode.Exploring && cam.enabled)
            {
                cam.fieldOfView = FieldOfView;
                cam.transform.position = House.Player.EyePosition;
            }
            House.Hud.Base.ViewMode.text = "Phase 1.7 住宅尺度與探索重構 QA｜按鍵見右側面板";
            if (House.Mode == Case01Mode.Exploring)
            {
                RunClock += dt;
                var pos = House.Player.transform.position;
                var step = new Vector2(pos.x - lastPos.x, pos.z - lastPos.z).magnitude;
                if (step < 2f) Walked += step;
                lastPos = pos;
                var room = House.CurrentRoom;
                if (room != lastRoom)
                {
                    lastRoom = room;
                    bool seen = false;
                    foreach (var kv in Arrivals) if (kv.Key == room) seen = true;
                    if (!seen && room != Case01Room.None) Arrivals.Add(new KeyValuePair<Case01Room, float>(room, RunClock));
                }
            }
            RefreshUi();
        }

        private void Toast(string s)
        {
            LastToast = s;
            if (House != null && House.Hud != null) House.Hud.Base.ShowToast(s);
        }

        // ---------------- UI ----------------

        public const int MapPpm = 30;
        private Canvas canvas;
        private Text panelText, mapTitle;
        private RectTransform mapRoot, playerDot, playerDir, fovL, fovR;
        private RawImage mapImage;
        private Texture2D plan0, plan1;
        private int mapFloor = -1;

        public Canvas QaCanvas { get { return canvas; } }
        public Texture2D PlanTexture(int floor) { return floor == 0 ? plan0 : plan1; }

        private void OnDestroy()
        {
            foreach (var t in new[] { plan0, plan1 }) if (t != null) ArenaArt.Kill(t);
        }

        private void BuildUi()
        {
            var cgo = new GameObject("Case01ScaleQaCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)cgo.transform;
            var panel = Case01SpatialQA.Rect(root, "ScalePanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -98f), new Vector2(580f, 330f));
            Case01SpatialQA.Img(panel, new Color(0.03f, 0.035f, 0.045f, 0.8f));
            panelText = Case01SpatialQA.Txt(panel, 17, TextAnchor.UpperLeft, new Color(0.88f, 0.9f, 0.92f));
            Case01SpatialQA.Inset(panelText.rectTransform, 12f, 8f);
            mapRoot = Case01SpatialQA.Rect(root, "ScaleMap", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 128f), new Vector2(300f, 470f));
            Case01SpatialQA.Img(mapRoot, new Color(0.03f, 0.035f, 0.045f, 0.82f));
            mapTitle = Case01SpatialQA.Txt(Case01SpatialQA.Rect(mapRoot, "Title", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -4f), new Vector2(400f, 26f)), 15, TextAnchor.UpperLeft, new Color(0.9f, 0.9f, 0.85f));
            var area = Case01SpatialQA.Rect(mapRoot, "Plan", Vector2.zero, Vector2.zero, new Vector2(6f, 6f), new Vector2(288f, 432f));
            mapImage = area.gameObject.AddComponent<RawImage>();
            mapImage.raycastTarget = false;
            fovL = Case01SpatialQA.Line(area, new Color(1f, 0.85f, 0.4f, 0.8f));
            fovR = Case01SpatialQA.Line(area, new Color(1f, 0.85f, 0.4f, 0.8f));
            playerDir = Case01SpatialQA.Line(area, new Color(1f, 0.3f, 0.25f, 1f));
            playerDot = Case01SpatialQA.Rect(area, "Player", Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            Case01SpatialQA.Img(playerDot, new Color(1f, 0.3f, 0.25f, 1f));
        }

        private void RebuildMaps()
        {
            if (plan0 != null) ArenaArt.Kill(plan0);
            if (plan1 != null) ArenaArt.Kill(plan1);
            plan0 = Case01SpatialMap.Plan(House.Layout, 0, MapPpm);
            plan1 = Case01SpatialMap.Plan(House.Layout, 1, MapPpm);
        }

        private string ArrivalText()
        {
            var sb = new StringBuilder();
            foreach (var kv in Arrivals)
            {
                if (kv.Value <= 0f) continue;
                if (sb.Length > 0) sb.Append("｜");
                sb.Append(Case01Texts.RoomName(kv.Key)).Append(' ').Append(kv.Value.ToString("0.0")).Append("s");
            }
            return sb.Length == 0 ? "（還沒走進別的房間）" : sb.ToString();
        }

        private void RefreshUi()
        {
            if (canvas == null || House == null || House.Player == null) return;
            var p = House.Player;
            var pos = p.transform.position;
            var room = House.Layout.RoomAt(pos);
            var views = ViewsFor(Plan);
            panelText.text =
                "<b>Phase 1.7 住宅尺度與探索重構 QA</b>\n<color=#E6BD52>" + PlanName(Plan) + "</color>\n" +
                "位置：" + Case01Texts.RoomName(room) + "（" + pos.x.ToString("0.0") + ", " + pos.z.ToString("0.0") + "）" + (PositionRemapped ? " <color=#E6A050>［已移到安全點］</color>" : "") +
                "｜H" + House.HouseStage + "\n" +
                SpeedText() + "｜FOV " + FieldOfView.ToString("0") + "°｜眼高 " + EyeHeight.ToString("0.00") + " m\n" +
                "滑鼠 ×" + LookScale.ToString("0.00") + "｜加減速 " + (Smooth ? "開" : "關") + "｜" +
                (Case01Cursor.Looking(House.CursorCaptured) ? "滑鼠鎖定" : "<color=#E6A050>滑鼠已釋放：點擊畫面恢復視角</color>") + "\n" +
                "視點：" + (ViewIndex < 0 ? "自由行走" : (ViewIndex + 1) + "／" + views.Length + " " + views[ViewIndex].Name) + "\n" +
                "這一趟：" + RunClock.ToString("0") + " 秒、走了 " + Walked.ToString("0.0") + " m\n首次到達：" + ArrivalText() + "\n" +
                "<color=#9AA4B0>F1 回玄關（重新計時）｜F2／F3 固定視點｜F4 平面圖｜F5 FOV｜F6 步速｜F7 眼高｜F8 降低閃爍｜F9 格局 O→R1→R2｜F10 滑鼠靈敏度｜F11 H0～H3｜F12 加減速｜E 調查｜Esc 釋放滑鼠</color>";

            mapRoot.gameObject.SetActive(MapOn);
            if (!MapOn) return;
            int floor = Case01SpatialMap.FloorOf(pos.y + 0.6f);
            if (floor != mapFloor)
            {
                mapFloor = floor;
                mapImage.texture = floor == 0 ? plan0 : plan1;
            }
            var areaRt = (RectTransform)mapImage.transform;
            Case01SpatialMap.UseRegion(House.Layout);
            float side = House.Layout.Footprint.height > 12f ? 432f : 288f;
            areaRt.sizeDelta = new Vector2(side, side);
            mapRoot.sizeDelta = new Vector2(side + 12f, side + 38f);
            float k = side / Case01SpatialMap.Size;
            mapTitle.text = (floor == 0 ? "一樓" : "二樓") + "（碰撞體平面）";
            var mp = Case01SpatialMap.ToMap(pos, k);
            playerDot.anchoredPosition = mp;
            playerDot.localEulerAngles = new Vector3(0f, 0f, 45f - p.Yaw);
            Ray(playerDir, mp, p.Yaw, 0.9f * k);
            float h = Mathf.Atan(Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) * Case01SpatialQA.Aspect(House.ExploreCamera)) * Mathf.Rad2Deg;
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
