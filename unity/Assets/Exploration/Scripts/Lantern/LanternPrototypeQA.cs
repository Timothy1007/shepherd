using System.Text;
using Shepherd.Battle.UI.Arena;
using Shepherd.Exploration.Case01;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 提燈手感 QA（灰盒原型）：第十七號門住宅（體感放大的兩層樓）＋修復線索與異域反撲＋場景崩壞後的最終戰。
    /// 左手提燈（光圈內顯示真實）、右手穩定器（左鍵射擊）、E 情境互動、Q 擬態錨定、按住 Space 聚光。
    /// 不接舊卡牌戰鬥、不修改正式 Demo 或其他 QA。
    /// </summary>
    [DefaultExecutionOrder(40)]
    public sealed class LanternPrototypeQA : MonoBehaviour
    {
        public static readonly float[] Fovs = { 55f, 62f, 70f };
        /// <summary>true＝餐廳、廚房用使用者確認過的 B 版（正式預設）。</summary>
        public static bool UseConfirmedLayouts = true;
        // 正式移動參數＝移動手感 QA 的「A｜慢速探索」（2026-11-16 使用者試玩後採用）。
        // 舊值：步行 1.9／快走 3.0／加減速同為 7／時間式晃動／腳步聲 0.8 m。
        public const float WalkSpeed = 1.5f;
        public const float BriskSpeed = 2.4f;
        /// <summary>加速：約 0.45 秒到步行速度。</summary>
        public const float MoveAcceleration = WalkSpeed / 0.45f;
        /// <summary>減速（另外設定）：放開方向鍵後約 0.2～0.3 秒停住。</summary>
        public const float MoveDeceleration = 8f;
        /// <summary>目前使用的步行／快走速度（預設＝上面的正式常數）。只有移動手感 QA 會改它。</summary>
        public float WalkSpeedNow = WalkSpeed, BriskSpeedNow = BriskSpeed;
        /// <summary>腳步聲間隔（公尺），和步伐式晃動（一步 0.7 m）對齊。</summary>
        public float StepLength = 0.7f;

        public LanternGraybox World { get; private set; }
        public LanternRig Rig { get; private set; }
        public LanternActions Actions { get; private set; }
        public LanternCombat Combat { get; private set; }
        public LanternFlow Flow { get; private set; }
        /// <summary>新房子一樓的謎題（第一批：店面＋側院紅線）。</summary>
        public LanternV2Puzzles V2 { get; private set; }
        /// <summary>能力小原型：「定」（Q）與三個目標（N 進入）。</summary>
        public LanternHold Hold { get; private set; }
        public LanternSandboxRun Sandbox { get; private set; }
        /// <summary>記憶空間 A：老街市場（從新房子側院的後門進去；M 直接過去）。</summary>
        public LanternMarketRun Market { get; private set; }

        public void GoToMarket() { Actions.DropHeld(false); Market.Enter(); Rig.SetOn(true); }
        private bool holdQWas;

        /// <summary>進入能力原型（每次都重建，等於重來）。</summary>
        public void GoToSandbox()
        {
            Actions.DropHeld(false);
            Hold.Clear();
            World.BuildSandbox();
            Sandbox.Reset();
            GoTo("Sandbox");
            Rig.SetOn(true);
            Flow.ShowToast("（能力原型：照、定、牽。三個目標，各有不只一種解法）", 4f);
        }
        public LanternAudio Audio { get; private set; }
        public FirstPersonController Player { get; private set; }
        public Camera Cam { get; private set; }
        public Case01RebuildPost Post { get; private set; }
        /// <summary>風格測試（F11 切換 A／B）。</summary>
        public LanternStyle Style { get; private set; }
        public void ToggleStyle() { if (Style != null) Style.Set(!Style.On); }
        public int FovIndex { get; private set; } = 1;
        public bool DebugVisible { get; private set; }
        public bool Ready { get; private set; }
        public bool FocusHeld;
        public ExploreInput? ScriptedInput;
        public LanternActionInput? ScriptedAction;
        public LanternReadable Reading { get; private set; }
        public float ReadingTimer { get; private set; }
        /// <summary>正在近看的東西（null＝沒有）。</summary>
        public LanternInspectable Inspecting { get; private set; }
        /// <summary>母親的筆記（Tab）：線索與提示只在這裡，平常畫面上沒有文字。</summary>
        public bool NotebookOpen { get; private set; }
        public int SecretsFound { get; private set; }
        public void ToggleNotebook() => NotebookOpen = !NotebookOpen;
        public int InspectPage { get; private set; }
        public bool InspectFlipped { get; private set; }
        private Transform inspectClone;
        private Vector2 inspectRot;
        private float inspectMoveCooldown;
        private Text inspectTitle, inspectBody, inspectHint, cluesText;
        private Image inspectPanel;
        private RawImage inspectImage;

        private float time;
        private Text debugText, hintText, promptText, progressText, readTitle, readBody, toastText, endText, shieldText;
        private Image endPanel, breathBack, breathZone, breathMarker;
        private Image tapBack, tapZone, tapMarker;
        /// <summary>畫面上平常只留一行小提示；按 H 才展開完整按鍵說明（測試用的快捷鍵也在裡面）。</summary>
        public const string ShortHint = "H 按鍵說明";
        private string fullHintText = "";
        public bool HelpOpen { get; private set; }
        public void ToggleHelp() { HelpOpen = !HelpOpen; if (hintText != null) hintText.text = HelpOpen ? fullHintText : ShortHint; }
        private Image promptBack, toastBack, notePaper;
        private Text tapText, subtitleText;
        private Image subtitleBand;
        /// <summary>畫面下方的字幕（影子殘影、之後的答錄機）；測試用。</summary>
        public string SubtitleText => subtitleText != null && subtitleText.enabled ? subtitleText.text : CurrentSubtitle();

        private string CurrentSubtitle()
        {
            var e = V2 != null ? V2.ShopEcho : null;
            if (e == null || !e.Playing || string.IsNullOrEmpty(e.VisibleText)) return "";
            return (string.IsNullOrEmpty(e.CurrentWho) ? "" : e.CurrentWho + "：") + e.VisibleText;
        }
        private bool v2PrimaryWas;
        private bool struggleHintShown;
        /// <summary>到達街區的轉場（黑屏＋地點字卡），秒數。</summary>
        public float ArrivalTimer { get; private set; }
        private Text arrivalText;
        private Image crosshair, chargeBar, chargeBack, healthBar, healthBack, energyBar, energyBack, hurtOverlay, fadeOverlay, readPanel;
        private Text downText, bossName, bossHint;
        private Image bossBack, bossBar, bossTrail;
        private float bossTrailHp;
        private GameObject canvasGo;
        private bool cursorFree;
        private readonly LegacyExploreInput input = new LegacyExploreInput();
        private Color savedAmbient;
        private UnityEngine.Rendering.AmbientMode savedMode;
        private bool savedFog;
        private SimulationMode savedSim;
        private bool simChanged;
        private float savedReflection = 1f;
        private float stepDistance;
        private Vector3 lastFeet;

        private void Start()
        {
            if (!Ready) Setup(true);
        }

        public void Setup(bool play)
        {
            if (Ready) return;
            savedAmbient = RenderSettings.ambientLight;
            savedMode = RenderSettings.ambientMode;
            savedFog = RenderSettings.fog;
            // 編輯器模式（測試、截圖）沒有物理迴圈：暫時改成手動模擬，結束時務必呼叫 Shutdown 還原（這是專案設定，會被存檔）。
            if (!Application.isPlaying)
            {
                savedSim = Physics.simulationMode;
                Physics.simulationMode = SimulationMode.Script;
                simChanged = true;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = DarkAmbient;
            RenderSettings.fog = false;
            savedReflection = RenderSettings.reflectionIntensity;
            RenderSettings.reflectionIntensity = 0.25f;
            LanternReveal.Clear();

            World = LanternGraybox.Build(transform);
            // 使用者試玩確認過的版本成為正式預設：餐廳 B（空間層次＋光照引導，48b6ecc）、廚房 B（改造＋回饋修正，db8a785）。
            // 原本的 A 只在餐廳／廚房的 A／B 對照 QA 場景裡切換得到。
            ApplyConfirmedLayouts();
            World.BuildShopInteractables();
            // 實際遊玩從新版房子（v2）門外開始；測試仍從舊房子玄關開始。
            var spawn = World.Marks[play && World.Marks.ContainsKey("V2Street") ? "V2Street" : "HouseEntry"];
            Player = FirstPersonController.Create(transform, spawn.position, spawn.eulerAngles.y);
            Player.WalkSpeedValue = WalkSpeedNow;
            Player.RunSpeedValue = BriskSpeedNow;
            Player.EyeHeightValue = 1.62f;
            Player.Acceleration = MoveAcceleration;
            Player.Deceleration = MoveDeceleration;
            Player.SmoothSpeedOnly = true;   // 只平滑速度大小，方向跟著鏡頭（轉頭不側滑）

            var camGo = new GameObject("LanternCamera");
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.nearClipPlane = 0.03f;
            Cam.farClipPlane = 80f;
            Cam.fieldOfView = Fovs[FovIndex];
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Color.black;
            Cam.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
            Post = camGo.AddComponent<Case01RebuildPost>();
            Post.Exposure = 1.0f;
            Style = new LanternStyle(Cam);
            Style.Set(true);

            Rig = LanternRig.Build(Cam, Player);
            Rig.GaitBob = true;              // 步伐式晃動（一步 0.7 m；B 鍵仍可關）
            Audio = LanternAudio.Build(camGo.transform);
            Actions = new LanternActions(Cam, Rig, World);
            Actions.Audio = Audio;
            Combat = new LanternCombat(World, Player, Rig, Audio, Actions.Particles);
            Flow = new LanternFlow(World, Player, Rig, Combat, Audio, Cam);
            Actions.Targets = AllTargets;
            Actions.OnInspect = BeginInspect;
            Combat.InShield = Actions.InShield;
            Combat.Erode = Actions.Erode;
            Combat.OnEnergy = v => Actions.AddEnergy(v);
            Combat.OnMemory = s => Flow.ShowToast(s, 4.5f);
            Flow.ResetState();
            V2 = new LanternV2Puzzles(World, Rig, Cam, Flow, Audio);
            Carry = new LanternCarry(Player, World, Flow, Audio);
            Actions.ReachTop = () => Player.transform.position.y + Player.EyeHeightValue + ReachAboveEye;
            V2.ReachTop = Actions.ReachTop;
            V2.PlayerFeet = () => Player.transform.position;
            Actions.GrabBlocked = () => V2.CoatHeld;
            Actions.Self = Player.Body;
            Carry.Blocked = () => V2.CoatHeld;
            Hold = new LanternHold();
            Sandbox = new LanternSandboxRun(World, Player, Hold, Actions, Flow, Audio);
            Market = new LanternMarketRun(World, Player, Sandbox, Flow, Audio, GoTo);
            lastFeet = Player.transform.position;
            if (play) BuildUi();
            Ready = true;
            Rig.Snap();
            Post.Apply();
            Physics.SyncTransforms();
            if (play)
            {
                Player.LookScale = LanternLookSettings.Load();
                HandleCursor(false, false, false);
            }
            if (play && World.InV2Street(Player.transform.position)) { Rig.SetOn(false); ArrivalTimer = 4.5f; }
        }

        /// <summary>正式布局（房子重建後也要再套一次）。</summary>
        private void ApplyConfirmedLayouts()
        {
            if (!UseConfirmedLayouts) return;
            World.SetDiningFocusLights(true);
            World.SetDiningLayout(true);
            World.SetKitchenLayout(true);
            World.SetShopLayout(1);   // 服飾店 A（生活機能優先，2026-11-16 試玩採用）
        }

        /// <summary>v2 開場：重建新版房子、回到門外、提燈放下。</summary>
        public void StartV2()
        {
            Actions.DropHeld(false);
            if (World.V2CoatHeld != null) ArenaArt.Kill(World.V2CoatHeld.gameObject);   // 抱著的外套掛在攝影機上，不會跟著房子一起清掉
            World.BuildHouseV2();
            ApplyConfirmedLayouts();
            World.BuildShopInteractables();
            ResetMemoryAge();
            V2.Rebind();
            Market.Bind();
            GoTo("V2Street");
            Rig.SetOn(false);
        }

        private System.Collections.Generic.IEnumerable<Component> AllTargets()
        {
            foreach (var c in Combat.Living) yield return c;
            foreach (var c in Flow.Targets) yield return c;
        }

        // ---------------- 操作 ----------------

        public void ToggleLantern() { Rig.Toggle(); Audio?.Play(LanternAudio.Toggle, 0.7f); }
        public void CycleRadius() => Rig.CycleRadius();
        public void ToggleHeadBob() => Rig.HeadBob = !Rig.HeadBob;
        public void ToggleDebug() { DebugVisible = !DebugVisible; if (debugText != null) debugText.enabled = DebugVisible; }
        public void CycleFov() => SetFov((FovIndex + 1) % Fovs.Length);

        public void SetFov(int i)
        {
            FovIndex = Mathf.Clamp(i, 0, Fovs.Length - 1);
            Cam.fieldOfView = Fovs[FovIndex];
        }

        /// <summary>切換住宅的體感放大倍率（1.25 / 1.4 / 1.6）：重建住宅，線索進度歸零，回到玄關。</summary>
        public void SetHouseScale(float scale)
        {
            Actions.DropHeld(false);
            Combat.ClearEnemies();
            World.BuildHouse(scale);
            Flow.ResetState();
            GoTo("HouseEntry");
            Flow.ShowToast("住宅放大倍率 ×" + scale.ToString("0.00"), 2.5f);
        }

        public void CycleHouseScale()
        {
            var s = LanternGraybox.HouseScales;
            int i = System.Array.IndexOf(s, World.HouseScale);
            SetHouseScale(s[(i + 1 + s.Length) % s.Length]);
        }

        // ---------------- 記憶視角的眼高（設計總綱 §10，2026-11-16） ----------------
        // 眼高跟著「記憶進度」走，不是跟著房間：
        //   幼年（0.85 m）：跨進第十七號門之後；店面、走廊、側院（從走廊的普通後門進去）都是幼年。
        //   青春期（1.12 m）：第一次從走廊進到餐廳（或廚房）。之後回到走廊、店面、側院也不會變回幼年；廚房也維持青春期。
        //   成人（1.62 m）：第一次從廚房穿過恢復的通路走進側院。之後到哪裡都是成人。
        // 廚房↔側院的通路：青春期之後第一次進到廚房才恢復（之前兩側都是完整的牆）。
        // 門外的街道與其他測試場地＝成人。只改攝影機；碰撞體、速度、提燈都不變。
        public enum MemoryAge { Child, Teen, Adult }
        /// <summary>目前的記憶年齡（只會往前，不會倒退）。</summary>
        public MemoryAge Age { get; private set; }
        private int lastArea;   // 0＝門洞或不明、1＝廚房、2＝側院、3＝主屋（店面、走廊、餐廳）

        /// <summary>重新開始（房子重建）時回到幼年。</summary>
        public void ResetMemoryAge() { Age = MemoryAge.Child; lastArea = 0; }

        private int AreaAt(Vector3 feet)
        {
            if (World == null) return 0;
            var p = feet + Vector3.up;
            if (World.V2KitchenRoom.size.sqrMagnitude > 0f && World.V2KitchenRoom.Contains(p)) return 1;
            if (World.InV2Yard(feet)) return 2;
            var d = (feet - LanternGraybox.V2Origin) / LanternGraybox.V2Scale;
            if (d.x > -0.1f && d.x < LanternGraybox.V2Width + 0.1f && d.z > 0.05f && d.z < LanternGraybox.V2Main - 0.15f) return 3;
            return 0;
        }

        private void UpdateMemoryAge(Vector3 feet, bool jumped)
        {
            if (World == null) return;
            int area = AreaAt(feet);
            var p = feet + Vector3.up;
            bool inDining = World.V2DiningRoom.size.sqrMagnitude > 0f && World.V2DiningRoom.Contains(p);
            if (Age == MemoryAge.Child && (inDining || area == 1)) Age = MemoryAge.Teen;   // 走廊→餐廳：第一次成長
            if (Age >= MemoryAge.Teen && area == 1 && !World.KitchenYardOpen) World.RevealKitchenYardPassage();   // 進廚房：通路慢慢顯現
            World.TickKitchenYardPassage();
            if (Age == MemoryAge.Teen && World.KitchenYardOpen && area == 2 && lastArea == 1 && !jumped) Age = MemoryAge.Adult;   // 廚房→側院：成人
            if (jumped || area != 0) lastArea = area;
        }
        public const float AdultEyeHeight = 1.62f;
        /// <summary>小小孩的眼高（店面／走廊／側院）。U／I 在測試模式可以調。</summary>
        public float ChildEyeHeight = 0.85f;
        /// <summary>初中時期的眼高（餐廳）。</summary>
        public float TeenEyeHeight = 1.12f;
        /// <summary>
        /// 提燈光圈跟著記憶年齡縮放（使用者 2026-11-16：小時候拿燈的光照範圍不該和大人一樣大）。
        /// 完全照身高比例（0.85／1.62≈0.52、1.12／1.62≈0.69）會小到前方只照得到 1 m 多、很難探索，
        /// 所以取折衷：幼年 0.70、青春期 0.85、成人 1.00；中間跟著眼高平順變化。
        /// </summary>
        public float ChildLanternScale = 0.70f, TeenLanternScale = 0.85f;

        /// <summary>依目前的眼高換算提燈光圈倍率（幼年眼高→幼年倍率、青春期→青春期、成人→1，中間線性）。</summary>
        public float LanternScaleForEye(float eye)
        {
            if (eye <= TeenEyeHeight)
                return Mathf.Lerp(ChildLanternScale, TeenLanternScale, Mathf.InverseLerp(ChildEyeHeight, TeenEyeHeight, eye));
            return Mathf.Lerp(TeenLanternScale, 1f, Mathf.InverseLerp(TeenEyeHeight, AdultEyeHeight, eye));
        }

        /// <summary>false＝永遠成人眼高（有些測試與工具用）。</summary>
        public bool MemoryHeights = true;
        /// <summary>測試模式 K 的強制視角：null＝自動（依房間）。</summary>
        public float? ViewOverride;
        /// <summary>目前依房間應該用的眼高。</summary>
        public float TargetEyeHeight { get; private set; } = AdultEyeHeight;
        private float eyeNow = AdultEyeHeight;
        private Vector3 lastEyeFeet;

        /// <summary>依玩家腳下的位置與目前的記憶年齡決定眼高。</summary>
        public float MemoryEyeHeightAt(Vector3 feet)
        {
            if (!MemoryHeights || World == null) return AdultEyeHeight;
            var d = feet - LanternGraybox.V2Origin;
            float x = d.x / LanternGraybox.V2Scale, z = d.z / LanternGraybox.V2Scale;
            bool inHouse = x > -0.1f && x < LanternGraybox.V2Width + 0.1f && z > 0.05f && z < LanternGraybox.V2Depth + 0.1f
                && d.y > -1f && d.y < LanternGraybox.V2Ground * LanternGraybox.V2Scale;   // 只算一樓
            if (!inHouse) return AdultEyeHeight;
            return Age == MemoryAge.Child ? ChildEyeHeight : Age == MemoryAge.Teen ? TeenEyeHeight : AdultEyeHeight;
        }

        // ---------------- 身體大小（設計總綱 §11：身體真的會隨記憶年齡改變） ----------------
        // 身高＝站立眼高 ÷ 0.9（成人 1.62 → 1.80 m、青春期 1.12 → 約 1.24 m、幼年 0.85 → 約 0.94 m）；
        // 半徑 0.24～0.35 m、直接跨上去的高度 0.15～0.35 m 跟著身高走。長大前先確認不會卡進家具，卡住就晚一點再長。
        // 手能伸到眼睛上方 0.22×身高＋0.2 m（幼年約 1.26 m、成人約 2.2 m）。
        public static float BodyHeightForEye(float eye) => eye / 0.9f;
        public static float BodyRadiusForHeight(float h) => Mathf.Lerp(0.24f, 0.35f, Mathf.InverseLerp(0.85f / 0.9f, 1.8f, h));
        public static float StepOffsetForHeight(float h) => Mathf.Clamp(h * 0.19f, 0.15f, 0.35f);
        public float ReachAboveEye => 0.22f * Player.Body.height + 0.2f;
        /// <summary>目前身體（站姿）的眼高；坐下時攝影機另外算。</summary>
        public float StandingEyeHeight => eyeNow;
        private float camEyeNow = AdultEyeHeight;

        /// <summary>坐在縫紉機凳子上時的眼高（幼年是跪在凳子上，眼睛比站著高；成人坐下比站著低）。</summary>
        public float SeatedEyeHeight
        {
            get
            {
                float seat = World.V2StoolMovable != null ? World.V2StoolMovable.TopY - LanternGraybox.V2Origin.y : 0.53f;
                float t = Mathf.InverseLerp(ChildEyeHeight, AdultEyeHeight, eyeNow);
                return seat + Mathf.Lerp(0.55f, 0.78f, t);
            }
        }

        private bool ApplyBody(float eye)
        {
            float h = BodyHeightForEye(eye);
            return Player.TrySetBody(h, BodyRadiusForHeight(h), StepOffsetForHeight(h));
        }

        private void UpdateEyeHeight(float dt)
        {
            var feet = Player.transform.position;
            bool jumped = (feet - lastEyeFeet).sqrMagnitude > 1.5f * 1.5f;   // 傳送：直接到位
            lastEyeFeet = feet;
            UpdateMemoryAge(feet, jumped);
            TargetEyeHeight = ViewOverride ?? MemoryEyeHeightAt(feet);
            float next = jumped ? TargetEyeHeight : Mathf.MoveTowards(eyeNow, TargetEyeHeight, dt * 1.3f);   // 穿過記憶門：約半秒的過渡
            if (V2 != null && V2.Seated) next = eyeNow;                 // 坐著的時候不改身體
            else if (!ApplyBody(next)) next = eyeNow;                   // 空間不夠長大：先維持
            eyeNow = next;
            float camTarget = V2 != null && V2.Seated ? SeatedEyeHeight : eyeNow;
            camEyeNow = jumped ? camTarget : Mathf.MoveTowards(camEyeNow, camTarget, dt * (V2 != null && V2.Seated ? 1.6f : 2.6f));
            if (!(V2 != null && V2.Seated) && Mathf.Abs(camEyeNow - eyeNow) > 0.35f) camEyeNow = eyeNow;   // 站起來時不要拖太久
            Player.EyeHeightValue = camEyeNow;
            Rig.RadiusScale = LanternScaleForEye(eyeNow);
            if (V2 != null) V2.SewAge = eyeNow <= ChildEyeHeight + 0.05f ? 0 : eyeNow <= TeenEyeHeight + 0.05f ? 1 : 2;
        }

        /// <summary>測試模式 K：自動 → 強制成人 → 強制小小孩 → 自動。</summary>
        public void CycleViewOverride()
        {
            if (ViewOverride == null) ViewOverride = AdultEyeHeight;
            else if (Mathf.Approximately(ViewOverride.Value, AdultEyeHeight)) ViewOverride = ChildEyeHeight;
            else { ViewOverride = null; World.SetShopToon(false); }
        }

        // ---------------- 服飾店 QA（Shift+F2）：觀察點、強制視角 ----------------
        /// <summary>服飾店測試模式（只在提燈原型裡，沒有獨立入口）。</summary>
        public bool ShopQA { get; private set; }
        /// <summary>比較用的固定觀察點（平面座標 x、z 與朝向）。</summary>
        public static readonly (string name, float x, float z, float yaw)[] ShopViews =
        {
            ("入口（店門內側）", 5.1f, 0.55f, -18f),
            ("櫃台前", 4.95f, 2.45f, -55f),
            ("工作區（縫紉機旁）", 2.55f, 1.25f, -125f),
            ("衣架後方（照片那面牆）", 1.05f, 2.45f, -95f),
            ("住宅入口（往店裡看）", 5.07f, 4.55f, 200f),
        };

        public void SetShopQA(bool on)
        {
            ShopQA = on;
            if (on) { GoToShopView(0); }
            else { ViewOverride = null; World.SetShopToon(false); }
        }

        public void GoToShopView(int i)
        {
            if (i < 0 || i >= ShopViews.Length) return;
            var v = ShopViews[i];
            Actions.DropHeld(false);
            Player.Teleport(LanternGraybox.V2Origin + new Vector3(v.x * LanternGraybox.V2Scale, 0.05f, v.z * LanternGraybox.V2Scale), v.yaw, 8f);
        }

        private GUIStyle shopLabel, shopHint;
        private void OnGUI()
        {
            if (!ShopQA || !Ready) return;
            if (shopLabel == null)
            {
                shopLabel = new GUIStyle(GUI.skin.label) { fontSize = 17, font = LanternFonts.UI };
                shopLabel.normal.textColor = new Color(1f, 0.97f, 0.9f);
                shopHint = new GUIStyle(shopLabel) { fontSize = 13 };
                shopHint.normal.textColor = new Color(1f, 1f, 1f, 0.82f);
            }
            float w = Screen.width;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(w - 470, 6, 464, 118), Texture2D.whiteTexture);
            GUI.color = Color.white;
            string mode = ViewOverride == null ? "自動（依記憶房間）" : Mathf.Approximately(ViewOverride.Value, AdultEyeHeight) ? "強制成人" : "強制小小孩";
            GUI.Label(new Rect(w - 462, 10, 450, 24), "服飾店測試（A 正式）：視角 " + mode, shopLabel);
            GUI.Label(new Rect(w - 462, 36, 450, 20), "記憶：" + (Age == MemoryAge.Child ? "幼年" : Age == MemoryAge.Teen ? "青春期" : "成人") + (World.KitchenYardOpen ? "・廚房側院通路已開" : "") + "　眼高 " + Player.EyeHeightValue.ToString("0.00") + " m・光圈 ×" + Rig.RadiusScale.ToString("0.00"), shopHint);
            GUI.Label(new Rect(w - 462, 56, 450, 20), "K 自動／成人／小小孩・U／I 小小孩眼高・T 整間卡通（" + (World.ShopToon ? "開" : "關") + "）", shopHint);
            GUI.Label(new Rect(w - 462, 76, 450, 20), "1 入口・2 櫃台前・3 工作區・4 衣架後方・5 住宅入口", shopHint);
            GUI.Label(new Rect(w - 462, 96, 450, 20), "Shift+F2 離開（回到現況、成人視角）", shopHint);
        }

        public void GoTo(string mark)
        {
            Transform t;
            if (!World.Marks.TryGetValue(mark, out t)) return;
            Actions.DropHeld(false);
            if (V2 != null && V2.Seated) V2.StandUp();   // 傳送：放棄坐著
            seatAnim = 1f;
            var f = t.forward;
            float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
            Player.Teleport(t.position, yaw, pitch);
            Rig.Snap();
        }

        // ---------------- 近看 ----------------

        /// <summary>拿起來看：文件翻頁、照片翻面、物件旋轉。關閉時才算看完。</summary>
        public void BeginInspect(LanternInspectable i)
        {
            if (i == null) return;
            EndInspect();
            Inspecting = i;
            InspectPage = 0;
            InspectFlipped = false;
            inspectRot = new Vector2(20f, -30f);
            if (i.Type == LanternInspectable.Kind.Object)
            {
                var src = i.gameObject;
                var clone = Instantiate(src, Cam.transform, true);
                foreach (var col in clone.GetComponentsInChildren<Collider>(true)) ArenaArt.Kill(col);
                foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
                foreach (var t in clone.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LanternRig.ViewLayer;
                var rs = clone.GetComponentsInChildren<Renderer>(true);
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds;
                    foreach (var r in rs) b.Encapsulate(r.bounds);
                    float s = 0.28f / Mathf.Max(0.01f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
                    clone.transform.localScale *= s;
                    var pivot = new GameObject("InspectPivot").transform;
                    pivot.SetParent(Cam.transform, false);
                    pivot.localPosition = new Vector3(0f, 0f, 0.55f);
                    pivot.gameObject.layer = LanternRig.ViewLayer;
                    clone.transform.SetParent(pivot, true);
                    var b2 = rs[0].bounds;
                    foreach (var r in rs) b2.Encapsulate(r.bounds);
                    clone.transform.position += pivot.position - b2.center;
                    inspectClone = pivot;
                }
                else ArenaArt.Kill(clone);
            }
        }

        public void EndInspect()
        {
            if (Inspecting == null) return;
            if (Inspecting.Secret && !Inspecting.Seen) { SecretsFound++; Flow.ShowToast("（找到一片記憶碎片）", 3f); }
            Inspecting.Seen = true;
            if (Inspecting.Completes != null) Inspecting.Completes.Read = true;
            Inspecting = null;
            if (inspectClone != null) ArenaArt.Kill(inspectClone.gameObject);
            inspectClone = null;
            Audio?.Play(LanternAudio.Drop, 0.4f);
        }

        public void NextPage(int dir)
        {
            if (Inspecting == null) return;
            if (Inspecting.Type == LanternInspectable.Kind.Pages)
                InspectPage = Mathf.Clamp(InspectPage + dir, 0, Mathf.Max(0, Inspecting.Pages.Length - 1));
            else InspectFlipped = !InspectFlipped;
            Audio?.Play(LanternAudio.Toggle, 0.35f);
        }

        private bool inspectInteractWas, inspectPrimaryWas;

        private void StepInspect(float dt, ExploreInput inp, LanternActionInput act)
        {
            bool eDown = act.Interact && !inspectInteractWas, pDown = act.Primary && !inspectPrimaryWas;
            inspectInteractWas = act.Interact;
            inspectPrimaryWas = act.Primary;
            if (eDown || inp.ReleaseCursor) { EndInspect(); return; }
            if (pDown) NextPage(1);
            inspectMoveCooldown -= dt;
            if (inspectMoveCooldown <= 0f && Mathf.Abs(inp.Move.x) > 0.5f) { NextPage(inp.Move.x > 0f ? 1 : -1); inspectMoveCooldown = 0.35f; }
            if (inspectClone != null)
            {
                inspectRot += new Vector2(-inp.Look.y, inp.Look.x) * 4f;
                inspectClone.localRotation = Quaternion.Euler(inspectRot.x, inspectRot.y, 0f);
            }
        }

        /// <summary>整個重新開始（住宅重建、流程歸零、回到玄關）。</summary>
        public void ResetScene()
        {
            Rig.SetOn(true);
            Rig.SetRadius(1);
            SetHouseScale(World.HouseScale);
            if (World.V2CoatHeld != null) ArenaArt.Kill(World.V2CoatHeld.gameObject);   // 抱著的外套掛在攝影機上，不會跟著房子一起清掉
            World.BuildHouseV2();
            ApplyConfirmedLayouts();
            World.BuildShopInteractables();
            ResetMemoryAge();
            V2.Rebind();
            Market.Bind();
        }

        public void Step(float dt, ExploreInput inp, LanternActionInput act = default(LanternActionInput))
        {
            if (!Ready) return;
            time += dt;
            if (ArrivalTimer > 0f) ArrivalTimer -= dt;
            bool locked = Combat.PlayerDown || Flow.Current == LanternFlow.Phase.Collapsing || Flow.Current == LanternFlow.Phase.Ending && Flow.EndingTime < 3f;
            if (locked) { inp.Move = Vector2.zero; inp.Look = Vector2.zero; act = default(LanternActionInput); if (Inspecting != null) EndInspect(); }
            if (Inspecting != null)
            {
                // 近看中：不能走、不能射擊；滑鼠轉物件，左鍵／A D 翻頁或翻面，E 放下。
                StepInspect(dt, inp, act);
                inp.Move = Vector2.zero;
                inp.Look = Vector2.zero;
                act = new LanternActionInput { Interact = true };
            }
            else { inspectInteractWas = act.Interact; inspectPrimaryWas = act.Primary; }
            Actions.Suspended = Inspecting != null || V2.InputCaptured;
            Rig.Focus = FocusHeld;
            Rig.HideHands = Inspecting != null || V2.LanternPlaced;   // 提燈放在桌上：手上沒有燈（之後換縫紉的手部動作）
            // 記憶年齡：眼高、身體大小、提燈光圈跟著記憶進度（設計總綱 §10、§11）。
            UpdateEyeHeight(dt);
            // 右手的穩定器（槍）：一樓與門外的街道用不到，收起來（使用者 2026-11-16；只有戰鬥時才拿出來）。
            var here = Player.transform.position;
            Rig.DeviceAllowed = !(World.InV2House(here) || World.InV2Street(here));
            // 坐上／離開縫紉機凳子的動作播放中：不能走、不能轉頭。
            if (seatAnim < 1f)
            {
                StepSeatAnim(dt);
                inp.Move = Vector2.zero;
                inp.Look = Vector2.zero;
            }
            // 保持平衡（取外套）：左右鍵拿來平衡，不能走。
            else if (V2.Balance.Active)
            {
                V2.BalanceInput = inp.Move.x;
                inp.Move = Vector2.zero;
            }
            // 坐在縫紉機前：不能走；往任何方向走＝站起來（放棄這次穿針）。
            else if (V2.Seated)
            {
                if (inp.Move.sqrMagnitude > 0.25f) V2.StandUp();
                inp.Move = Vector2.zero;
            }
            float carrySlow = V2.CoatHeld && V2.SewAge == 0 ? 0.75f : 1f;   // 小孩抱著外套走得慢一點
            Player.WalkSpeedValue = WalkSpeedNow * Mathf.Lerp(1f, 0.6f, Rig.FocusBlend) * carrySlow;
            Player.RunSpeedValue = BriskSpeedNow * Mathf.Lerp(1f, 0.6f, Rig.FocusBlend) * carrySlow;
            if (seatAnim >= 1f) Player.Step(dt, inp, true);
            Carry.Step(dt, inp.Move);
            var feet = Player.transform.position;
            stepDistance += Vector3.Distance(new Vector3(feet.x, 0f, feet.z), new Vector3(lastFeet.x, 0f, lastFeet.z));
            lastFeet = feet;
            if (stepDistance > StepLength) { stepDistance = 0f; Audio?.PlayStep(0.45f); }
            Audio?.Mix(World.InV2Street(feet), World.InV2House(feet) || World.HouseBounds.Contains(feet + Vector3.up), dt);
            Rig.Tick(dt, time);
            World.Tick(time);
            if (World.V2Tick(Player.transform.position, dt)) { Audio?.Play(LanternAudio.EnemyWarn, 0.45f); Rig.Shake(0.05f); }
            // 能力原型裡，Q＝「定」（取代擬態錨定）。
            bool inSandbox = World.InAbilitySpace(Player.transform.position);
            bool qDown = act.Deploy && !holdQWas;
            holdQWas = act.Deploy;
            if (inSandbox)
            {
                if (qDown)
                {
                    var msg = Hold.Use(Cam, Actions);
                    if (msg != "") Flow.ShowToast(msg, 2.5f);
                    else Audio?.Play(LanternAudio.Placed, 0.5f);
                    Rig.PokeDevice();
                }
                act.Deploy = false;
            }
            Actions.Step(dt, act);
            Sandbox.Step(dt);
            Market.Step(dt);
            bool primaryDown = act.Primary && !v2PrimaryWas;
            v2PrimaryWas = act.Primary;
            V2.Step(dt, Inspecting == null && (act.Interact || act.Primary), primaryDown);
            if (V2.SeatMoveRequested)
            {
                V2.ConsumeSeatMove();
                var seat = World.V2SeatSpot;
                var back = Quaternion.Euler(0f, World.V2SeatYaw, 0f) * Vector3.back;
                // 幼年：爬上凳子（慢一點、有一個往上撐的弧度）；青春期、成人：拉開凳子坐下。播完才開始穿針。
                if (V2.Seated) BeginSeatAnim(seat + Vector3.up * 0.02f, World.V2SeatYaw, 28f, V2.SewAge == 0 ? 1.1f : 0.75f, V2.SewAge == 0);
                else BeginSeatAnim(seat + back * 0.6f + Vector3.up * 0.02f, World.V2SeatYaw, 0f, 0.5f, false);
            }
            Combat.Step(dt);
            Flow.Step(dt);
            if (dt > 0f && simChanged) Physics.Simulate(dt);
            LanternReveal.Tick(dt);
            ApplyAmbient(dt);
            Style?.Tick();
            UpdateUi();
        }

        public static readonly Color DarkAmbient = new Color(0.055f, 0.064f, 0.085f);
        public static readonly Color WarmAmbient = new Color(0.16f, 0.13f, 0.1f);
        /// <summary>目前套用的環境光亮度（0＝暗、1＝有電的家）。</summary>
        public float AmbientLevel { get; private set; }

        /// <summary>在住宅裡：有電且燈亮著時環境光變亮、偏暖；沒電或反撲熄燈時接近全黑。</summary>
        private void ApplyAmbient(float dt)
        {
            var p = Player.transform.position;
            float want = World.HouseBounds.Contains(p + Vector3.up) ? World.HouseAmbient : 0f;
            AmbientLevel = dt > 0f ? Mathf.MoveTowards(AmbientLevel, want, dt * 3f) : want;
            var night = Color.Lerp(DarkAmbient, WarmAmbient, AmbientLevel);
            // 美術方向 v1：新房子一帶是白天（下午）。屋裡＝柔和的反射光；街上＝天空的藍白。
            bool indoor = World.InV2House(p);
            float dayWant = LanternGraybox.V2Day && (indoor || World.InV2Street(p) || World.InAbilitySpace(p)) ? 1f : 0f;
            DayLevel = dt > 0f ? Mathf.MoveTowards(DayLevel, dayWant, dt * 2f) : dayWant;
            // 褪色態的改衣店：沒電時比較暗（不是全黑），有電之後慢慢亮起來（使用者 2026-11-16：開燈＝看得見，但還是灰的）。
            bool shopDark = World.V2ShopRoom.size.sqrMagnitude > 0f && World.V2ShopRoom.Contains(p + Vector3.up) && !World.V2Powered;
            ShopDim = dt > 0f ? Mathf.MoveTowards(ShopDim, shopDark ? 1f : 0f, dt * 1.2f) : (shopDark ? 1f : 0f);
            var day = indoor ? DayIndoorAmbient * Mathf.Lerp(1f, UnpoweredShopAmbient, ShopDim) : DayStreetAmbient;
            RenderSettings.ambientLight = Color.Lerp(night, day, DayLevel);
            if (World.Moon != null) World.Moon.intensity = Mathf.Lerp(0.55f, 0f, DayLevel);
            if (World.V2Sun != null) World.V2Sun.intensity = 1.9f * DayLevel;
            Cam.backgroundColor = Color.Lerp(Color.black, DaySky, DayLevel);
            Style?.SetDay(DayLevel, indoor);
        }

        public static readonly Color DayIndoorAmbient = new Color(0.58f, 0.56f, 0.53f);
        /// <summary>沒電的改衣店：室內反射光的倍率。</summary>
        public const float UnpoweredShopAmbient = 0.5f;
        /// <summary>0＝一般、1＝沒電的改衣店（平順過渡）。</summary>
        public float ShopDim { get; private set; }
        public static readonly Color DayStreetAmbient = new Color(0.74f, 0.75f, 0.8f);
        public static readonly Color DaySky = new Color(0.62f, 0.76f, 0.92f);
        /// <summary>白天的程度（0＝夜晚、1＝白天；新房子與街上是白天）。</summary>
        public float DayLevel { get; private set; }

        public string DebugString()
        {
            var sb = new StringBuilder();
            sb.Append("位置 ").Append(Player.transform.position.ToString("F2"))
              .Append("｜FOV ").Append(Cam.fieldOfView.ToString("0"))
              .Append("｜住宅 ×").Append(World.HouseScale.ToString("0.00"))
              .Append("｜步速 ").Append(Player.LastHorizontalSpeed.ToString("0.0"))
              .Append("｜光圈 ").Append(Rig.CurrentRadius.ToString("0.0"))
              .Append("｜能量 ").Append(Actions.Energy.ToString("0"))
              .Append("｜生命 ").Append(Combat.Health.ToString("0"))
              .Append("｜流程 ").Append(Flow.Current).Append(" ").Append(Flow.Repaired).Append("/").Append(Flow.Total)
              .Append("｜失序體 ").Append(Combat.Enemies.Count)
              .Append("\n滑鼠 ").Append(Cursor.lockState).Append(Cursor.visible ? " 可見" : " 隱藏")
              .Append(CursorLock.Focused ? " 有焦點" : " 沒焦點").Append(CursorLock.LockFailing ? " 鎖定沒生效" : "")
              .Append(" 重鎖 ×").Append(CursorLock.Reapplied).Append(" 靈敏度 ×").Append(Player.LookScale.ToString("0.0"))
              .Append("\n店面：電 ").Append(V2.Powered ? "✓" : "□").Append(" 吊牌 ").Append(V2.TagRead ? "✓" : "□")
              .Append(" 紅線 ").Append(V2.HasThread ? "✓" : "□").Append(" 穿針 ").Append(V2.Sewn ? "✓" : "□")
              .Append(" 修好 ").Append(V2.ShopRepaired ? "✓" : "□")
              .Append(" 影子 ").Append(V2.ShopEcho == null ? "-" : V2.ShopEcho.Playing ? "演出中" : V2.ShopEcho.Armed ? "等你看" : "演完 ×" + V2.ShopEcho.TimesPlayed);
            return sb.ToString();
        }

        private void Update()
        {
            if (!Ready || !Application.isPlaying) return;
            if (Input.GetKeyDown(KeyCode.F)) ToggleLantern();
            if (Input.GetKeyDown(KeyCode.F1)) StartV2();
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (Input.GetKeyDown(KeyCode.F2)) { if (shift) SetShopQA(!ShopQA); else GoTo("V2Shop"); }
            if (ShopQA)
            {
                if (Input.GetKeyDown(KeyCode.K)) CycleViewOverride();
                if (Input.GetKeyDown(KeyCode.T)) { int n = World.SetShopToon(!World.ShopToon); Flow.ShowToast(World.ShopToon ? "（測試：整間店換成卡通 shader，" + n + " 個物件）" : "（測試：換回原本的材質）", 2.5f); }
                if (Input.GetKeyDown(KeyCode.U)) ChildEyeHeight = Mathf.Clamp(ChildEyeHeight - 0.05f, 0.6f, 1.4f);
                if (Input.GetKeyDown(KeyCode.I)) ChildEyeHeight = Mathf.Clamp(ChildEyeHeight + 0.05f, 0.6f, 1.4f);
                for (int k = 0; k < ShopViews.Length; k++) if (Input.GetKeyDown(KeyCode.Alpha1 + k) || Input.GetKeyDown(KeyCode.Keypad1 + k)) GoToShopView(k);
            }
            if (Input.GetKeyDown(KeyCode.F3)) GoTo("HouseEntry");
            if (Input.GetKeyDown(KeyCode.F4)) GoTo("Spawn");
            if (Input.GetKeyDown(KeyCode.F6) && !RenderStyleQA) GoTo("ArenaStart");
            if (Input.GetKeyDown(KeyCode.F8) && Flow.Current == LanternFlow.Phase.Boss && Flow.Boss != null) { foreach (var s in World.Switches) s.Hit(); }
            if (Input.GetKeyDown(KeyCode.F7)) CycleRadius();
            if (Input.GetKeyDown(KeyCode.F9)) CycleHouseScale();
            if (Input.GetKeyDown(KeyCode.F10)) Flow.SkipToBoss();
            if (Input.GetKeyDown(KeyCode.B)) ToggleHeadBob();
            if (Input.GetKeyDown(KeyCode.F5)) CycleFov();
            if (Input.GetKeyDown(KeyCode.R)) ResetScene();
            if (Input.GetKeyDown(KeyCode.F12)) ToggleDebug();
            if (Input.GetKeyDown(KeyCode.F11) && !RenderStyleQA) ToggleStyle();
            if (Input.GetKeyDown(KeyCode.Tab)) ToggleNotebook();
            if (Input.GetKeyDown(KeyCode.H)) ToggleHelp();
            if (Input.GetKeyDown(KeyCode.N)) GoToSandbox();
            if (Input.GetKeyDown(KeyCode.M)) GoToMarket();
            if (Input.GetKeyDown(KeyCode.LeftBracket)) AdjustLookScale(-LanternLookSettings.StepSize);
            if (Input.GetKeyDown(KeyCode.RightBracket)) AdjustLookScale(LanternLookSettings.StepSize);
            FocusHeld = Input.GetKey(KeyCode.Space);
            var inp = ScriptedInput ?? input.Read();
            CursorLock.ReportPointer(Input.mousePosition);
            bool clickToLock = HandleCursor(inp.ReleaseCursor, Input.GetMouseButtonDown(0), Input.GetMouseButton(0));
            if (!CursorLock.LookActive && !ScriptedInput.HasValue) inp.Look = Vector2.zero;
            var act = ScriptedAction ?? new LanternActionInput
            {
                Primary = CursorLock.PrimaryAllowed && !clickToLock && Input.GetMouseButton(0),
                Interact = Input.GetKey(KeyCode.E),
                Deploy = Input.GetKey(KeyCode.Q),
            };
            Step(Time.deltaTime, inp, act);
        }

        // ---------------- 滑鼠與游標 ----------------

        /// <summary>游標鎖定狀態（探索＝鎖定；Esc／失去焦點＝放開）。</summary>
        public LanternCursorLock CursorLock { get; } = new LanternCursorLock();

        /// <summary>
        /// 處理一個影格的游標狀態。Esc 先關近看或筆記，沒有東西可關時才放開滑鼠；
        /// 放開時點一下畫面恢復。回傳 true＝這次左鍵是用來恢復鎖定的。
        /// </summary>
        public bool HandleCursor(bool escDown, bool clickDown, bool clickHeld)
        {
            bool pause = escDown;
            if (escDown && Inspecting != null) pause = false;          // 近看：Esc 只放下東西（StepInspect 處理）
            else if (escDown && NotebookOpen) { NotebookOpen = false; pause = false; }
            CursorLock.UiNeedsCursor = CursorUiOpen;
            bool consumed = CursorLock.Tick(pause, clickDown, clickHeld);
            cursorFree = !CursorLock.WantLocked;
            UpdateCursorHint();
            return consumed;
        }

        /// <summary>需要游標指點的介面（目前沒有：筆記只是看、近看用滑鼠相對位移轉物件）。之後的選單在這裡接上。</summary>
        public bool CursorUiOpen;

        /// <summary>
        /// 舊的 QA 切換器使用過（已移除；保留旗標給之後的 QA）：F6 讓給渲染切換（平常是傳送到戰鬥場），
        /// F11 的整體風格開關也停用（避免兩套風格設定互相覆蓋）。只在獨立 QA 場景裡為 true。
        /// </summary>
        public bool RenderStyleQA;

        public void AdjustLookScale(float delta)
        {
            Player.LookScale = LanternLookSettings.Clamp(Player.LookScale + delta);
            LanternLookSettings.Save(Player.LookScale);
            Flow.ShowToast("滑鼠靈敏度 ×" + Player.LookScale.ToString("0.0") + "（[ 降低　] 提高）", 1.5f);
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!Application.isPlaying || !Ready) return;
            CursorLock.OnFocus(focus);
            cursorFree = !CursorLock.WantLocked;
            UpdateCursorHint();
        }

        private Text cursorHintText;

        private void UpdateCursorHint()
        {
            if (cursorHintText == null) return;
            bool failing = !cursorFree && CursorLock.LockFailing;
            cursorHintText.enabled = (cursorFree && !CursorUiOpen) || failing;
            cursorHintText.text = failing ? "滑鼠沒有鎖在畫面中央｜點一下畫面鎖定"
                : CursorLock.Focused ? "滑鼠已放開｜點一下畫面繼續" : "";
        }

        // ---------------- 畫面提示 ----------------

        private Image Img(string name, Vector2 anchor, Vector2 pos, Vector2 size, Color c, Transform parent = null)
        {
            var i = new GameObject(name).AddComponent<Image>();
            i.transform.SetParent(parent ?? canvasGo.transform, false);
            i.rectTransform.anchorMin = i.rectTransform.anchorMax = i.rectTransform.pivot = anchor;
            i.rectTransform.anchoredPosition = pos;
            i.rectTransform.sizeDelta = size;
            i.color = c;
            i.raycastTarget = false;
            return i;
        }

        private Image Bar(Image back, Color c)
        {
            var b = new GameObject("Fill").AddComponent<Image>();
            b.transform.SetParent(back.transform, false);
            b.rectTransform.anchorMin = new Vector2(0, 0);
            b.rectTransform.anchorMax = new Vector2(0, 1);
            b.rectTransform.pivot = new Vector2(0, 0.5f);
            b.color = c;
            b.raycastTarget = false;
            return b;
        }

        private void BuildUi()
        {
            canvasGo = new GameObject("LanternQAUI");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var font = LanternFonts.UI;

            hurtOverlay = Img("HurtOverlay", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 3000), new Color(0.55f, 0f, 0f, 0f));
            crosshair = Img("Crosshair", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6, 6), new Color(1f, 0.95f, 0.85f, 0.7f));
            chargeBack = Img("ChargeBack", new Vector2(0.5f, 0.5f), new Vector2(0, -26), new Vector2(80, 4), new Color(0f, 0f, 0f, 0.4f));
            chargeBar = Bar(chargeBack, new Color(1f, 0.85f, 0.55f, 0.9f));
            healthBack = Img("HealthBack", new Vector2(0, 0), new Vector2(24, 70), new Vector2(260, 10), new Color(0f, 0f, 0f, 0.45f));
            healthBar = Bar(healthBack, new Color(0.95f, 0.85f, 0.65f, 0.85f));
            energyBack = Img("EnergyBack", new Vector2(0, 0), new Vector2(24, 54), new Vector2(200, 6), new Color(0f, 0f, 0f, 0.45f));
            energyBar = Bar(energyBack, new Color(0.6f, 0.85f, 1f, 0.85f));
            shieldText = MakeText("Shield", font, 18, new Vector2(234, 46), TextAnchor.LowerLeft, new Vector2(0, 0));
            hintText = MakeText("Hint", font, 17, new Vector2(24, 14), TextAnchor.LowerLeft, new Vector2(0, 0));
            hintText.text = "Tab 母親的筆記　左鍵 射擊（按住蓄力）　E 互動（抓起／按住復原／打開／拿起來看）　Q 擬態錨定　按住Space 聚光　F 提燈　F1 新房子門外（重來） F2 新房子店面 F3 舊房子 F4 測試房 F6 戰鬥場　F8 最終戰全部開燈　F9 房子大小　F10 直接打最終戰　F11 風格 A／B　R 重來　F12 數值　N 能力原型　M 老街市場（Q＝定）　[ ] 滑鼠靈敏度　Esc 放開滑鼠（點畫面繼續）";
            hintText.color = new Color(1f, 1f, 1f, 0.5f);
            fullHintText = hintText.text;
            hintText.text = ShortHint;
            progressText = MakeText("Progress", font, 22, new Vector2(-30, -24), TextAnchor.UpperRight, new Vector2(1, 1));
            promptText = MakeText("Prompt", font, 22, new Vector2(0, -70), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            promptText.color = new Color(1f, 0.96f, 0.88f, 0.85f);
            toastText = MakeText("Toast", font, 24, new Vector2(0, 160), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            toastText.color = new Color(0.85f, 0.9f, 1f, 0.9f);
            debugText = MakeText("Debug", font, 18, new Vector2(24, -20), TextAnchor.UpperLeft, new Vector2(0, 1));
            debugText.enabled = false;
            readPanel = Img("ReadPanel", new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(900, 420), new Color(0.08f, 0.07f, 0.06f, 0.88f));
            readTitle = MakeText("ReadTitle", font, 28, new Vector2(0, 170), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), readPanel.transform);
            readBody = MakeText("ReadBody", font, 22, new Vector2(0, -10), TextAnchor.UpperLeft, new Vector2(0.5f, 0.5f), readPanel.transform);
            readBody.rectTransform.sizeDelta = new Vector2(820, 300);
            readBody.color = new Color(0.95f, 0.9f, 0.82f);
            readPanel.gameObject.SetActive(false);
            cluesText = MakeText("Clues", font, 17, new Vector2(-30, -56), TextAnchor.UpperRight, new Vector2(1, 1));
            cluesText.rectTransform.sizeDelta = new Vector2(900, 240);
            cluesText.color = new Color(0.9f, 0.88f, 0.8f, 0.8f);
            inspectPanel = Img("InspectPanel", new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(760, 560), new Color(0.93f, 0.89f, 0.8f, 0.97f));
            inspectTitle = MakeText("InspectTitle", font, 26, new Vector2(0, 245), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), inspectPanel.transform);
            inspectTitle.color = new Color(0.25f, 0.18f, 0.12f);
            inspectBody = MakeText("InspectBody", font, 25, new Vector2(0, -20), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), inspectPanel.transform);
            inspectBody.rectTransform.sizeDelta = new Vector2(660, 420);
            inspectBody.color = new Color(0.2f, 0.15f, 0.1f);
            Destroy(inspectBody.GetComponent<Shadow>());
            Destroy(inspectTitle.GetComponent<Shadow>());
            inspectImage = new GameObject("InspectImage").AddComponent<RawImage>();
            inspectImage.transform.SetParent(inspectPanel.transform, false);
            inspectImage.rectTransform.sizeDelta = new Vector2(600, 450);
            inspectImage.rectTransform.anchoredPosition = new Vector2(0, -10);
            inspectHint = MakeText("InspectHint", font, 18, new Vector2(0, -300), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), inspectPanel.transform);
            inspectHint.color = new Color(1f, 0.97f, 0.9f, 0.9f);
            inspectPanel.gameObject.SetActive(false);
            fadeOverlay = Img("Fade", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 3000), new Color(0f, 0f, 0f, 0f));
            arrivalText = MakeText("Arrival", font, 30, new Vector2(0, 0), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            arrivalText.rectTransform.sizeDelta = new Vector2(1400, 160);
            arrivalText.text = "（暫定）車停在街口。\n\n第十七號門｜某條街，晚上九點";
            arrivalText.enabled = false;
            downText = MakeText("Down", font, 40, new Vector2(0, 40), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            downText.text = "你倒下了…";
            downText.enabled = false;
            endText = MakeText("End", font, 34, new Vector2(0, 40), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            endText.rectTransform.sizeDelta = new Vector2(1400, 200);
            endText.text = "第十七號門　結案（暫定）\n\n真相：艾琳・維爾曾經住在這裡。\n她仍然下落不明。\n但母親手上，有了她存在過的證明。\n\nR 重新開始";
            endText.enabled = false;
            endPanel = Img("EndPanel", new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1100, 520), new Color(0.06f, 0.05f, 0.04f, 0.9f));
            endPanel.transform.SetSiblingIndex(endText.transform.GetSiblingIndex());
            endPanel.enabled = false;
            // 推門的呼吸量表：指針來回擺動，亮的那段＝可以推。
            breathBack = Img("BreathBack", new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(220, 12), new Color(0f, 0f, 0f, 0.55f));
            breathZone = new GameObject("Zone").AddComponent<Image>();
            breathZone.transform.SetParent(breathBack.transform, false);
            breathZone.rectTransform.anchorMin = new Vector2(0f, 0f);
            breathZone.rectTransform.anchorMax = new Vector2(0.38f, 1f);
            breathZone.rectTransform.offsetMin = breathZone.rectTransform.offsetMax = Vector2.zero;
            breathZone.color = new Color(1f, 0.8f, 0.45f, 0.75f);
            breathZone.raycastTarget = false;
            breathMarker = new GameObject("Marker").AddComponent<Image>();
            breathMarker.transform.SetParent(breathBack.transform, false);
            breathMarker.rectTransform.anchorMin = breathMarker.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            breathMarker.rectTransform.sizeDelta = new Vector2(5, 22);
            breathMarker.color = Color.white;
            breathMarker.raycastTarget = false;
            breathBack.gameObject.SetActive(false);
            // 穿針的點按量表：指針來回，進到亮的那段時按一下。
            tapBack = Img("TapBack", new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(320, 14), new Color(0f, 0f, 0f, 0.6f));
            tapZone = new GameObject("Zone").AddComponent<Image>();
            tapZone.transform.SetParent(tapBack.transform, false);
            tapZone.rectTransform.offsetMin = tapZone.rectTransform.offsetMax = Vector2.zero;
            tapZone.color = new Color(0.95f, 0.3f, 0.25f, 0.8f);
            tapZone.raycastTarget = false;
            tapMarker = new GameObject("Marker").AddComponent<Image>();
            tapMarker.transform.SetParent(tapBack.transform, false);
            tapMarker.rectTransform.anchorMin = tapMarker.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            tapMarker.rectTransform.sizeDelta = new Vector2(4, 26);
            tapMarker.color = Color.white;
            tapMarker.raycastTarget = false;
            tapText = MakeText("TapText", font, 20, new Vector2(0, 26), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), tapBack.transform);
            tapBack.gameObject.SetActive(false);
            // 樣板間的介面：提示和訊息有柔和的深色底，筆記是一張米色的紙。
            promptBack = Img("PromptBack", new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(200, 40), new Color(0.05f, 0.04f, 0.04f, 0.55f));
            promptBack.transform.SetSiblingIndex(promptText.transform.GetSiblingIndex());
            toastBack = Img("ToastBack", new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(200, 46), new Color(0.05f, 0.04f, 0.04f, 0.5f));
            toastBack.transform.SetSiblingIndex(toastText.transform.GetSiblingIndex());
            notePaper = Img("NotebookPaper", new Vector2(1f, 1f), new Vector2(-18, -44), new Vector2(560, 300), new Color(0.93f, 0.89f, 0.8f, 0.96f));
            notePaper.transform.SetSiblingIndex(cluesText.transform.GetSiblingIndex());
            notePaper.enabled = false;
            // 對話字幕：畫面下方中央，半透明底條（像一般遊戲的對話字幕）。
            subtitleBand = Img("SubtitleBand", new Vector2(0.5f, 0f), new Vector2(0, 120), new Vector2(1100, 64), new Color(0f, 0f, 0f, 0.45f));
            subtitleText = MakeText("Subtitle", font, 32, new Vector2(0, 120), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f));
            subtitleText.rectTransform.sizeDelta = new Vector2(1500, 64);
            subtitleText.color = new Color(1f, 0.95f, 0.86f);
            subtitleBand.enabled = subtitleText.enabled = false;
            // 最後的反撲：畫面上方中央的生命條。
            bossBack = Img("BossBack", new Vector2(0.5f, 1f), new Vector2(0, -64), new Vector2(820, 16), new Color(0f, 0f, 0f, 0.55f));
            bossTrail = Bar(bossBack, new Color(1f, 0.95f, 0.85f, 0.55f));
            bossBar = Bar(bossBack, new Color(0.85f, 0.35f, 0.5f, 0.95f));
            bossName = MakeText("BossName", font, 24, new Vector2(0, -38), TextAnchor.MiddleCenter, new Vector2(0.5f, 1f));
            bossName.text = "最後的反撲";
            bossHint = MakeText("BossHint", font, 18, new Vector2(0, -90), TextAnchor.MiddleCenter, new Vector2(0.5f, 1f));
            bossHint.color = new Color(1f, 0.92f, 0.8f, 0.85f);
            foreach (var g in new Graphic[] { bossBack, bossName, bossHint }) g.gameObject.SetActive(false);
            cursorHintText = MakeText("CursorHint", font, 24, new Vector2(0, -130), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            cursorHintText.color = new Color(1f, 0.96f, 0.88f, 0.9f);
            cursorHintText.enabled = false;
        }

        private Text MakeText(string name, Font font, int size, Vector2 offset, TextAnchor anchor, Vector2 pivot, Transform parent = null)
        {
            var t = new GameObject(name).AddComponent<Text>();
            t.transform.SetParent(parent ?? canvasGo.transform, false);
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.raycastTarget = false;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = pivot;
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(1500, 60);
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.8f);
            return t;
        }

        public string PromptText => Carry != null && !string.IsNullOrEmpty(Carry.Prompt) ? Carry.Prompt : Actions != null ? Actions.Prompt : "";
        public LanternCarry Carry { get; private set; }

        // ---------------- 坐上／離開縫紉機凳子的動作 ----------------
        // 灰盒階段：鏡頭從站的位置移到凳子上（幼年多一個往上撐、爬上去的弧度），眼高由坐姿平順過渡；之後換正式的手腳動作。
        /// <summary>1＝沒有在播；0～1＝播放進度。</summary>
        public float SeatAnimProgress => seatAnim;
        private float seatAnim = 1f, seatAnimDur = 0.75f;
        private Vector3 seatFrom, seatTo;
        private float seatFromYaw, seatToYaw, seatFromPitch, seatToPitch;
        private bool seatClimb;

        private void BeginSeatAnim(Vector3 to, float yaw, float pitch, float seconds, bool climb)
        {
            seatFrom = Player.transform.position; seatTo = to;
            seatFromYaw = Player.Yaw; seatToYaw = yaw;
            seatFromPitch = Player.Pitch; seatToPitch = pitch;
            seatAnimDur = seconds; seatClimb = climb;
            seatAnim = 0f;
        }

        private void StepSeatAnim(float dt)
        {
            seatAnim = Mathf.Min(1f, seatAnim + dt / Mathf.Max(0.05f, seatAnimDur));
            float k = Mathf.SmoothStep(0f, 1f, seatAnim);
            var p = Vector3.Lerp(seatFrom, seatTo, k);
            float yaw = Mathf.LerpAngle(seatFromYaw, seatToYaw, k);
            float pitch = Mathf.Lerp(seatFromPitch, seatToPitch, k);
            if (seatClimb) pitch -= Mathf.Sin(seatAnim * Mathf.PI) * 12f;   // 爬的時候先抬頭看凳面，再低頭看針
            Player.Teleport(p, yaw, pitch);
            lastEyeFeet = Player.transform.position;
            if (seatAnim >= 1f && V2.Seated) V2.BeginSewing();
        }

        private void UpdateUi()
        {
            if (canvasGo == null) return;
            promptText.text = Reading != null ? "" : Actions.Prompt;
            if (string.IsNullOrEmpty(promptText.text) && !Rig.On && World.InV2Street(Player.transform.position)) promptText.text = "F 舉起提燈";
            float charge = Actions.Held != null ? Actions.ThrowCharge : Actions.StruggleProgress >= 0f ? Actions.StruggleProgress : Actions.Charge;
            chargeBack.enabled = charge > 0.01f;
            chargeBar.rectTransform.sizeDelta = new Vector2(80f * charge, 0f);
            crosshair.color = Actions.Aimed != null ? new Color(1f, 0.85f, 0.5f, 0.95f) : new Color(1f, 0.95f, 0.85f, 0.55f);
            crosshair.rectTransform.sizeDelta = Actions.Aimed != null ? new Vector2(10, 10) : new Vector2(6, 6);
            healthBar.rectTransform.sizeDelta = new Vector2(260f * Combat.Health / LanternCombat.MaxHealth, 0f);
            healthBar.color = Combat.Health < 35f ? new Color(1f, 0.35f, 0.3f, 0.9f) : new Color(0.95f, 0.85f, 0.65f, 0.85f);
            energyBar.rectTransform.sizeDelta = new Vector2(200f * Actions.Energy / LanternActions.MaxEnergy, 0f);
            shieldText.text = World.InAbilitySpace(Player.transform.position) ? Hold.ChargeText()
                : "擬態錨定 " + new string('◆', Actions.ShieldChargesLeft) + new string('◇', LanternActions.ShieldCharges - Actions.ShieldChargesLeft);
            float vign = Mathf.Max(Combat.HurtFlash * 0.55f, (1f - Combat.Health / LanternCombat.MaxHealth) * 0.25f);
            hurtOverlay.color = new Color(0.55f, 0f, 0f, Combat.PlayerDown ? 0.75f : vign);
            downText.enabled = Combat.PlayerDown;
            progressText.text = Flow.Current == LanternFlow.Phase.Explore ? "修復 " + Flow.Repaired + " / " + Flow.Total + (Combat.MemoriesFound > 0 ? "　記憶碎片 " + Combat.MemoriesFound : "")
                : "";
            toastText.text = Flow.ToastTimer > 0f ? Flow.Toast : "";
            float arrive = ArrivalTimer > 0f ? Mathf.Clamp01(ArrivalTimer / 1.6f) : 0f;
            fadeOverlay.color = new Color(0f, 0f, 0f, Mathf.Max(Flow.Fade, arrive));
            arrivalText.enabled = ArrivalTimer > 0f;
            if (arrivalText.enabled) arrivalText.color = new Color(0.95f, 0.9f, 0.82f, Mathf.Clamp01(ArrivalTimer - 0.6f) * Mathf.Clamp01((4.5f - ArrivalTimer) / 0.8f));
            endText.enabled = Flow.Current == LanternFlow.Phase.Ended;
            endPanel.enabled = endText.enabled;
            var stg = Actions.Aimed as LanternStruggle;
            bool showBreath = stg != null && !stg.Done && Inspecting == null;
            breathBack.gameObject.SetActive(showBreath);
            if (showBreath)
            {
                breathZone.rectTransform.anchorMax = new Vector2(stg.LooseFraction, 1f);
                breathMarker.rectTransform.anchoredPosition = new Vector2(220f * stg.Phase, 0f);
                breathMarker.color = stg.Loose ? new Color(1f, 0.95f, 0.6f) : new Color(0.6f, 0.65f, 0.8f);
                if (!struggleHintShown) { struggleHintShown = true; Flow.ShowToast("（門的另一邊像是有人抵著……指針到亮的那一段時，按住 E 推；狂按沒有用）", 5f); }
            }
            // 提示與訊息的底：跟著字的寬度。
            promptBack.enabled = !string.IsNullOrEmpty(promptText.text);
            if (promptBack.enabled) promptBack.rectTransform.sizeDelta = new Vector2(Mathf.Min(1400f, promptText.preferredWidth + 44f), Mathf.Max(40f, promptText.preferredHeight + 14f));
            toastBack.enabled = !string.IsNullOrEmpty(toastText.text);
            if (toastBack.enabled) toastBack.rectTransform.sizeDelta = new Vector2(Mathf.Min(1400f, toastText.preferredWidth + 56f), Mathf.Max(46f, toastText.preferredHeight + 16f));
            var sub = CurrentSubtitle();
            subtitleText.text = sub;
            subtitleBand.enabled = subtitleText.enabled = sub.Length > 0;
            var tap = V2.Tap;
            var bal = V2.Balance;
            tapBack.gameObject.SetActive(tap.Active || bal.Active);
            if (bal.Active)
            {
                // 保持平衡：中間是穩定區，標記被晃來晃去。
                tapZone.rectTransform.anchorMin = new Vector2(0.5f - bal.Zone / 2f, 0f);
                tapZone.rectTransform.anchorMax = new Vector2(0.5f + bal.Zone / 2f, 1f);
                tapZone.color = new Color(0.45f, 0.8f, 0.5f, 0.8f);
                tapMarker.rectTransform.anchoredPosition = new Vector2(320f * (bal.Marker + 1f) / 2f, 0f);
                tapMarker.color = bal.InZone ? new Color(1f, 0.95f, 0.6f) : new Color(1f, 0.5f, 0.45f);
                int filled = Mathf.Clamp(Mathf.RoundToInt(bal.Hold / bal.Need * 6f), 0, 6);
                tapText.text = "站穩　" + new string('●', filled) + new string('○', 6 - filled) + "　（A／D 保持平衡）";
            }
            else if (tap.Active)
            {
                tapZone.color = new Color(0.95f, 0.3f, 0.25f, 0.8f);
                tapZone.rectTransform.anchorMin = new Vector2(tap.ZoneCenter - tap.ZoneWidth / 2f, 0f);
                tapZone.rectTransform.anchorMax = new Vector2(tap.ZoneCenter + tap.ZoneWidth / 2f, 1f);
                tapMarker.rectTransform.anchoredPosition = new Vector2(320f * tap.Pointer, 0f);
                tapMarker.color = tap.InZone ? new Color(1f, 0.95f, 0.6f) : Color.white;
                tapText.text = "穿針　" + new string('●', tap.Hits) + new string('○', tap.Need - tap.Hits) + "　（E 或左鍵）";
                promptText.text = "";
            }
            var boss = Flow.Boss;
            bool showBoss = Flow.Current == LanternFlow.Phase.Boss && boss != null;
            foreach (var g in new Graphic[] { bossBack, bossName, bossHint }) g.gameObject.SetActive(showBoss);
            if (showBoss)
            {
                float k = Mathf.Clamp01(boss.Hp / LanternBoss.MaxHp);
                bossTrailHp = Mathf.MoveTowards(Mathf.Max(bossTrailHp, k), k, Time.unscaledDeltaTime * 0.35f);
                bossBar.rectTransform.sizeDelta = new Vector2(820f * k, 0f);
                bossTrail.rectTransform.sizeDelta = new Vector2(820f * bossTrailHp, 0f);
                bossBar.color = Color.Lerp(new Color(0.85f, 0.35f, 0.5f, 0.95f), Color.white, boss.Flash);
                bossHint.text = boss.Lit ? "它在光裡——現在打得痛！" : "它在暗處：射擊亮紅點的電燈開關（" + World.SwitchesOn + " / " + World.Switches.Count + " 盞亮著），把它引進光裡";
                bossHint.color = boss.Lit ? new Color(1f, 0.9f, 0.6f, 0.95f) : new Color(0.8f, 0.85f, 1f, 0.8f);
            }
            else bossTrailHp = 1f;
            readPanel.gameObject.SetActive(false);
            var sbClues = new StringBuilder();
            bool inV2 = World.InV2House(Player.transform.position);
            if (Flow.Current == LanternFlow.Phase.Explore && NotebookOpen && World.InMarket(Player.transform.position))
            {
                sbClues.Append("── 母親的筆記（暫定）──\n");
                sbClues.Append("後門外面是一條老街。不是這座城市的街。\n\n");
                foreach (var g in Market.Goals) sbClues.Append(g.Done ? "✓ " : "□ ").Append(g.Name).Append(g.Done ? "——" + g.Method : "").Append('\n');
                sbClues.Append("\n照（F）　定（Q）　牽（E）");
            }
            else if (Flow.Current == LanternFlow.Phase.Explore && NotebookOpen && World.InSandbox(Player.transform.position))
            {
                sbClues.Append("── 能力原型 ──\n");
                sbClues.Append("照（F）：光裡的東西是實的\n定（Q）：固定一個東西 25 秒（3 次，會恢復；同時最多 2 個）\n牽（E）：抓起、放下；左鍵丟\n\n");
                foreach (var g in Sandbox.Goals) sbClues.Append(g.Done ? "✓ " : "□ ").Append(g.Name).Append(g.Done ? "——" + g.Method : "").Append('\n');
                sbClues.Append("\nN 重來");
            }
            else if (Flow.Current == LanternFlow.Phase.Explore && NotebookOpen && inV2)
            {
                sbClues.Append("── 母親的筆記（暫定）──\n");
                if (SecretsFound > 0) sbClues.Append("記憶碎片 ").Append(SecretsFound).Append("\n");
                foreach (var line in V2.Notebook) sbClues.Append("・").Append(line).Append('\n');
                sbClues.Append("店裡的事（沒有先後順序）：\n");
                foreach (var (task, done) in V2.ShopTasks) sbClues.Append(done ? "  ✓ " : "  □ ").Append(task).Append('\n');
            }
            else if (Flow.Current == LanternFlow.Phase.Explore && NotebookOpen)
            {
                sbClues.Append("── 母親的筆記（暫定）──\n");
                if (SecretsFound > 0) sbClues.Append("記憶碎片 ").Append(SecretsFound).Append("\n");
                if (!World.HousePowered && World.FuseBox != null) sbClues.Append("・房子沒電——玄關樓梯下好像有電箱\n");
                foreach (var cl in World.Clues)
                {
                    if (cl == null) continue;
                    sbClues.Append(cl.Done ? "✓ " : "□ ").Append(cl.Label);
                    if (!cl.Done) sbClues.Append("：").Append(cl.Hint);
                    sbClues.Append('\n');
                }
            }
            if (Flow.Current == LanternFlow.Phase.Explore && !NotebookOpen) sbClues.Append("Tab 母親的筆記");
            cluesText.text = Inspecting != null ? "" : sbClues.ToString();
            // 筆記打開時：米色的紙、深色的墨水字；收起來時只留一行淡淡的「Tab 母親的筆記」。
            bool paper = NotebookOpen && Inspecting == null && Flow.Current == LanternFlow.Phase.Explore;
            notePaper.enabled = paper;
            cluesText.color = paper ? new Color(0.22f, 0.16f, 0.11f) : new Color(0.9f, 0.88f, 0.8f, 0.8f);
            var sh = cluesText.GetComponent<Shadow>(); if (sh != null) sh.enabled = !paper;
            cluesText.alignment = paper ? TextAnchor.UpperLeft : TextAnchor.UpperRight;
            cluesText.lineSpacing = paper ? 1.25f : 1f;
            if (paper)
            {
                cluesText.rectTransform.sizeDelta = new Vector2(500f, 600f);
                cluesText.rectTransform.anchoredPosition = new Vector2(-48f, -64f);
                notePaper.rectTransform.sizeDelta = new Vector2(560f, Mathf.Min(900f, cluesText.preferredHeight + 48f));
            }
            else { cluesText.rectTransform.sizeDelta = new Vector2(900f, 240f); cluesText.rectTransform.anchoredPosition = new Vector2(-30f, -56f); }
            var ins = Inspecting;
            inspectPanel.gameObject.SetActive(ins != null && ins.Type != LanternInspectable.Kind.Object);
            if (ins != null)
            {
                inspectTitle.text = ins.Title;
                if (ins.Type == LanternInspectable.Kind.Pages)
                {
                    inspectImage.enabled = false;
                    inspectBody.text = ins.Pages.Length > 0 ? ins.Pages[InspectPage] : "";
                    inspectHint.text = "第 " + (InspectPage + 1) + " / " + Mathf.Max(1, ins.Pages.Length) + " 頁　左鍵或 D 下一頁　A 上一頁　E 放下";
                }
                else if (ins.Type == LanternInspectable.Kind.Image)
                {
                    bool back = InspectFlipped && ins.Back == null;
                    inspectImage.enabled = !back;
                    inspectImage.texture = InspectFlipped && ins.Back != null ? ins.Back : ins.Front;
                    inspectBody.text = back ? ins.BackText : "";
                    inspectHint.text = "左鍵 翻面　E 放下";
                }
                promptText.text = ins.Type == LanternInspectable.Kind.Object ? ins.Title + "　滑鼠旋轉　E 放下" + (string.IsNullOrEmpty(ins.BackText) ? "" : "\n" + ins.BackText) : "";
            }
            if (Reading != null) { readTitle.text = Reading.Title; readBody.text = Reading.Text + "\n\n（E 或走動關閉）"; }
            if (DebugVisible) debugText.text = DebugString();
        }

        /// <summary>還原暫時改動的全域設定（物理模擬模式、環境光、反射）。可重複呼叫。編輯器模式下不會自動呼叫 OnDestroy，測試與截圖要自己呼叫。</summary>
        public void Shutdown()
        {
            if (Style != null) { Style.Destroy(); Style = null; }
            if (simChanged)
            {
                Physics.simulationMode = savedSim;
                simChanged = false;
            }
            if (Ready)
            {
                RenderSettings.ambientLight = savedAmbient;
                RenderSettings.ambientMode = savedMode;
                RenderSettings.fog = savedFog;
                RenderSettings.reflectionIntensity = savedReflection;
            }
        }

        private void OnDestroy()
        {
            Shutdown();
            if (inspectClone != null) ArenaArt.Kill(inspectClone.gameObject);
            if (Flow != null) Flow.Destroy();
            if (Actions != null) Actions.Destroy();
            if (Combat != null) Combat.Destroy();
            if (Rig != null) Rig.Destroy();
            if (World != null) World.Destroy();
            LanternReveal.Clear();
            if (Application.isPlaying) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
    }
}
