using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// 第十七號門｜高完成度沉浸式住宅探索 QA（選單「Shepherd/Case01/沉浸式住宅探索 QA」，場景 Case01ImmersiveHouseQA.unity）。
    /// 範圍：玄關 → 客廳 → 樓梯 → 二樓走廊（正常狀態）。沿用 Phase 1.9 R3 住宅、真門、1.8 美術與聲音，加上 <see cref="Case01ImmersiveHouse"/> 的生活痕跡與照明，
    /// 以及自然步行（三段速度、平滑加減速、輕微步態）、手電筒、合照近看。H 階段、夾層、中庭、F2 對照在這裡停用（只看正常住宅）。
    /// 正常遊玩只有畫面中央一個小點；除錯資訊用 F12 顯示。
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class Case01ImmersiveHouseQA : MonoBehaviour
    {
        public static readonly float[] WalkSpeeds = { 1.2f, 1.5f, 1.8f };
        public static readonly string[] WalkNames = { "慢走", "正常步行", "較快步行" };
        /// <summary>Shift：同一段速度的 1.45 倍（快步，不是跑）。</summary>
        public const float BriskFactor = 1.45f;
        /// <summary>加減速（m/s²）：約 0.25 秒到達步行速度、停下也一樣平順。</summary>
        public const float Acceleration = 6f;
        public static readonly float[] Brightness = { 0.65f, 1.0f, 1.35f };
        public static readonly string[] BrightnessNames = { "暗", "標準", "亮" };

        public Case01ImmersionQA Host { get; private set; }
        public Case01HouseQA House { get { return Host != null ? Host.House : null; } }
        public Case01ImmersiveHouse Dressing { get; private set; }
        public Case01Flashlight Flashlight { get; private set; }
        public Case01Inspector Inspector { get; private set; }
        public int SpeedPreset { get; private set; } = 1;
        public int BrightnessPreset { get; private set; } = 1;
        public bool HeadBob { get; private set; } = true;
        public bool DebugOn { get; private set; }
        public int PhotoVariant { get; private set; }
        public float WalkSpeed { get { return WalkSpeeds[SpeedPreset]; } }
        public Vector3 BobOffset { get; private set; }
        public int Rebuilds { get; private set; }

        private bool built;
        private float bobPhase, bobAmp;
        private HouseInput input;

        private void Awake()
        {
            if (Application.isPlaying) Setup(true);
        }

        public void Setup(bool play, int? seed = null)
        {
            if (built) return;
            built = true;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            var go = new GameObject("Phase19Host");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            Host = go.AddComponent<Case01ImmersionQA>();
            Host.Setup(play, null, seed, true, Case01QaMode.CoreV03, true);
            go.SetActive(true);
            // Phase 1.8 的鏡頭步態關掉，用這裡的版本（有起停緩和）。
            Host.Options.ReducedMotion = true;
            var prevPost = Host.House.LayoutPostBuild;
            Host.House.LayoutPostBuild = l =>
            {
                if (prevPost != null) prevPost(l);
                BuildDressing(l);
            };
            var prevInteract = Host.House.CustomInteract;
            Host.House.CustomInteract = it => Interact(it) || (prevInteract != null && prevInteract(it));
            Inspector = new Case01Inspector(Host.House.Layout.Palette, transform);
            BuildDressing(Host.House.Layout);
            input = new HouseInput(this, new LegacyCase01Input());
            Host.UseInput(input);
            Host.SetPanelVisible(false);
            BuildUi();
            ApplyPlayer();
        }

        /// <summary>測試：換成腳本輸入（仍經過這裡的過濾：檢視時不能走、F2／F3 停用）。</summary>
        public void UseInput(ICase01InputSource source)
        {
            input = new HouseInput(this, source);
            Host.UseInput(input);
        }

        private void BuildDressing(Case01HouseLayout l)
        {
            if (Flashlight != null) Flashlight.Dispose();
            Flashlight = null;
            Dressing = null;
            if (l == null || l.Floorplan != Case01Floorplan.R3) return;
            Dressing = new Case01ImmersiveHouse(l, l.Palette, Host.World);
            Dressing.SetPhotoVariant(PhotoVariant);
            Flashlight = new Case01Flashlight(Host.House.ExploreCamera, Host.World != null && Host.World.Art != null ? Host.World.Art.Kit : null, l.Palette, Dressing.LensMat);
            Rebuilds++;
        }

        private bool Interact(Case01Interactable it)
        {
            if (it == null || Dressing == null || Dressing.Photo == null) return false;
            if (it.Id != Case01Ids.Photo) return false;
            BeginInspect();
            return true;
        }

        // ---------------- 操作 ----------------

        public bool Inspecting { get { return Inspector != null && Inspector.Active; } }

        public void BeginInspect()
        {
            if (Dressing == null || Dressing.Photo == null || Inspecting) return;
            Dressing.Photo.Variant = PhotoVariant;
            Inspector.Begin(Dressing.Photo, House.ExploreCamera);
            House.Hud.HidePanel();
        }

        public void EndInspect() { if (Inspector != null) Inspector.End(); }

        public void SetSpeed(int preset)
        {
            SpeedPreset = Mathf.Clamp(preset, 0, WalkSpeeds.Length - 1);
            ApplyPlayer();
            Toast("步行速度：" + WalkNames[SpeedPreset] + " " + WalkSpeed.ToString("0.0") + " m/s（候選值）");
        }

        public void ToggleFlashlight() { if (Flashlight != null) Flashlight.Toggle(); }

        public void CycleFlashlightLevel()
        {
            if (Flashlight == null) return;
            Flashlight.CycleLevel();
            Toast("手電筒強度：" + Case01Flashlight.IntensityNames[Flashlight.Level]);
        }

        public void CycleBrightness()
        {
            BrightnessPreset = (BrightnessPreset + 1) % Brightness.Length;
            Toast("環境亮度：" + BrightnessNames[BrightnessPreset]);
        }

        public void ToggleHeadBob()
        {
            HeadBob = !HeadBob;
            Toast(HeadBob ? "鏡頭步態：開" : "鏡頭步態：關");
        }

        public void TogglePhotoVariant()
        {
            PhotoVariant = 1 - PhotoVariant;
            if (Dressing != null) Dressing.SetPhotoVariant(PhotoVariant);
            if (Inspecting) Inspector.SetVariant(PhotoVariant);
            Toast(PhotoVariant == 0 ? "合照：正常版本" : "合照：異常候選（QA 比對用，不是正式劇情）");
        }

        public void ToggleDebug() { DebugOn = !DebugOn; }

        /// <summary>R：回到前門內、手電筒關、合照放回且回到正常版本、門回到初始狀態（速度與亮度設定保留）。</summary>
        public void ResetScene()
        {
            if (Inspector != null && Inspector.Active)
            {
                Inspector.End();
                for (int i = 0; i < 20 && Inspector.Active; i++) Inspector.Tick(0.05f);
            }
            if (Flashlight != null) Flashlight.SetOn(false);
            PhotoVariant = 0;
            if (Dressing != null) Dressing.SetPhotoVariant(0);
            if (Host.World != null) Host.World.ResetDoors();
            var s = House.Layout.Spawn;
            House.Player.Teleport(s.Position, s.Yaw, 0f);
            Toast("QA：場景重置（前門內）");
        }

        private void Toast(string s)
        {
            if (House != null && House.Hud != null) House.Hud.Base.ShowToast(s);
        }

        private void ApplyPlayer()
        {
            var p = House != null ? House.Player : null;
            if (p == null) return;
            p.WalkSpeedValue = WalkSpeed;
            p.RunSpeedValue = WalkSpeed * BriskFactor;
            p.Acceleration = Acceleration;
        }

        // ---------------- 輸入過濾 ----------------

        /// <summary>
        /// 包在原本輸入外面：檢視中不能走、不能轉頭（滑鼠改成傾斜／移動相片）、E 放回；F2（1.8 對照）、F3（H 階段）停用；F6 循環三段步速；
        /// F1／F4／F5／F7／F9／F10 交給 Phase 1.9 QA；1／2／3 直接選步速（沒有選項面板時）。
        /// </summary>
        private sealed class HouseInput : ICase01InputSource
        {
            private readonly Case01ImmersiveHouseQA owner;
            private readonly ICase01InputSource inner;

            public HouseInput(Case01ImmersiveHouseQA owner, ICase01InputSource inner)
            {
                this.owner = owner;
                this.inner = inner;
            }

            public Case01Input Read() { return owner.Filter(inner.Read()); }
        }

        /// <summary>輸入過濾（Play 由輸入來源呼叫；測試也走這裡）。</summary>
        public Case01Input Filter(Case01Input r)
        {
            var owner = this;
            {
                int key = r.Jump > 0 ? r.Jump : r.Function;
                if (owner.Inspecting)
                {
                    bool drag = Application.isPlaying && UnityEngine.Input.GetMouseButton(0);
                    owner.Inspector.Feed(r.Base.Look, r.Zoom, drag);
                    if (r.Base.Interact) owner.EndInspect();
                    return Case01Input.None;
                }
                switch (key)
                {
                    case 1: owner.ResetScene(); break;
                    case 4:
                    case 5:
                    case 7:
                    case 9:
                    case 10: owner.Host.Press(key); break;
                    case 6: owner.SetSpeed((owner.SpeedPreset + 1) % WalkSpeeds.Length); break;
                }
                if (r.Choice > 0 && !owner.House.Hud.PanelVisible)
                {
                    owner.SetSpeed(r.Choice - 1);
                    r.Choice = 0;
                }
                r.Jump = 0;
                r.Function = r.Function == 8 ? 8 : 0;
                return r;
            }
        }

        private void Update()
        {
            if (!built || House == null || !Application.isPlaying) return;
            if (Inspecting)
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.Space)) Inspector.Flip();
                if (UnityEngine.Input.GetMouseButtonDown(1)) EndInspect();
                if (UnityEngine.Input.GetKeyDown(KeyCode.P)) TogglePhotoVariant();
                return;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.F)) ToggleFlashlight();
            if (UnityEngine.Input.GetKeyDown(KeyCode.X)) CycleFlashlightLevel();
            if (UnityEngine.Input.GetKeyDown(KeyCode.L)) CycleBrightness();
            if (UnityEngine.Input.GetKeyDown(KeyCode.B)) ToggleHeadBob();
            if (UnityEngine.Input.GetKeyDown(KeyCode.P)) TogglePhotoVariant();
            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) ResetScene();
            if (UnityEngine.Input.GetKeyDown(KeyCode.F12)) ToggleDebug();
        }

        private void LateUpdate()
        {
            if (!built || House == null) return;
            Frame(Time.deltaTime);
            ApplyCamera(Time.deltaTime);
        }

        /// <summary>每影格（Play：LateUpdate，在 Phase 1.9 QA 之後；測試直接呼叫）。</summary>
        public void Frame(float dt)
        {
            Host.Options.ReducedMotion = true;
            ApplyPlayer();
            if (Inspector != null) Inspector.Tick(dt);
            if (Flashlight != null) Flashlight.Tick(dt, Inspecting || House.Mode != Case01Mode.Exploring);
            // 環境亮度（QA 比較）：Case01Mood 每影格重設環境光，這裡在它之後乘上預設比例。
            float k = Brightness[BrightnessPreset];
            RenderSettings.ambientSkyColor *= k;
            RenderSettings.ambientEquatorColor *= k;
            RenderSettings.ambientGroundColor *= k;
            House.Hud.Base.ViewMode.text = "";
            RefreshUi(dt);
        }

        /// <summary>
        /// 步態：依實際水平速度推進步伐相位（約每 0.75 m 一步），上下 8 mm、左右 4 mm、側傾 0.25°；起步與停下時幅度在約 0.25 秒內淡入淡出，
        /// 停下後鏡頭完全靜止。B 可關閉。
        /// </summary>
        public void ApplyCamera(float dt)
        {
            var cam = House.ExploreCamera;
            var pl = House.Player;
            if (cam == null || pl == null) return;
            float speed = House.Mode == Case01Mode.Exploring && !Inspecting ? pl.LastHorizontalSpeed : 0f;
            bobAmp = Mathf.MoveTowards(bobAmp, HeadBob && speed > 0.15f ? Mathf.Clamp01(speed / 1.5f) : 0f, dt * 4f);
            bobPhase += dt * speed / 0.75f * Mathf.PI;
            var off = new Vector3(Mathf.Sin(bobPhase) * 0.004f, Mathf.Abs(Mathf.Sin(bobPhase)) * 0.008f - 0.004f, 0f) * bobAmp;
            BobOffset = off;
            cam.transform.position += cam.transform.rotation * off;
            cam.transform.rotation *= Quaternion.Euler(0f, 0f, Mathf.Sin(bobPhase) * 0.25f * bobAmp);
        }

        // ---------------- UI ----------------

        private Canvas canvas;
        private Text dot, debug;
        private RectTransform debugRect;
        private float fps = 60f;

        private void BuildUi()
        {
            var cgo = new GameObject("Case01ImmersiveHouseCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 41;
            var sc = cgo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)cgo.transform;
            var d = Case01SpatialQA.Rect(root, "Dot", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
            dot = Case01SpatialQA.Txt(d, 18, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.55f));
            dot.text = "·";
            debugRect = Case01SpatialQA.Rect(root, "Debug", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(620f, 300f));
            Case01SpatialQA.Img(debugRect, new Color(0.02f, 0.025f, 0.03f, 0.72f));
            debug = Case01SpatialQA.Txt(debugRect, 16, TextAnchor.UpperLeft, new Color(0.85f, 0.88f, 0.9f));
            Case01SpatialQA.Inset(debug.rectTransform, 12f, 8f);
        }

        public bool DotVisible { get { return dot != null && dot.enabled; } }
        public bool DebugVisible { get { return debugRect != null && debugRect.gameObject.activeSelf; } }

        private void RefreshUi(float dt)
        {
            if (dot == null) return;
            dot.enabled = !Inspecting && House.Mode == Case01Mode.Exploring;
            debugRect.gameObject.SetActive(DebugOn);
            if (!DebugOn) return;
            if (dt > 0f) fps = Mathf.Lerp(fps, 1f / dt, 0.05f);
            var p = House.Player;
            var sb = new StringBuilder();
            sb.Append("<b>沉浸式住宅探索 QA</b>（F12 關閉）\n");
            sb.Append("步行 ").Append(WalkNames[SpeedPreset]).Append(' ').Append(WalkSpeed.ToString("0.0")).Append(" m/s（Shift ×").Append(BriskFactor).Append("）｜實際 ").Append(p.LastHorizontalSpeed.ToString("0.00")).Append(" m/s\n");
            sb.Append("垂直 FOV ").Append(Host.FieldOfView.ToString("0")).Append("°｜眼高 ").Append(Host.EyeHeight.ToString("0.00")).Append(" m｜步態 ").Append(HeadBob ? "開" : "關").Append('\n');
            sb.Append("手電筒 ").Append(Flashlight != null && Flashlight.On ? "開 " + Case01Flashlight.IntensityNames[Flashlight.Level] + "（近距收光 " + (Flashlight.NearFactor * 100f).ToString("0") + "%）" : "關")
                .Append("｜環境亮度 ").Append(BrightnessNames[BrightnessPreset]).Append('\n');
            sb.Append("合照 ").Append(PhotoVariant == 0 ? "正常" : "異常候選").Append("｜檢視 ").Append(Inspecting ? "中（放大 " + Inspector.Zoom.ToString("0.0") + "×" + (Inspector.Flipped ? "，背面" : "") + "）" : "—").Append('\n');
            sb.Append("位置 ").Append(p.transform.position.ToString("F2")).Append("｜").Append(Case01Texts.RoomName(House.CurrentRoom)).Append("｜").Append(fps.ToString("0")).Append(" fps\n");
            sb.Append("<color=#9AA4B0>WASD 走｜Shift 快步｜1／2／3 或 F6 步速｜F 手電筒｜X 手電筒強度｜L 環境亮度｜B 鏡頭步態｜E 拿起／放回｜P 合照版本｜R 重置｜F1 回前門｜F4 聲音｜F5 FOV｜F7 眼高｜F9 光影新／舊｜F10 門全開｜Esc 釋放滑鼠\n檢視中：滑鼠 傾斜｜滾輪 放大｜左鍵拖曳 移動｜空白鍵 翻面｜E／右鍵 放回</color>");
            debug.text = sb.ToString();
        }

        private void OnDestroy()
        {
            if (Flashlight != null) Flashlight.Dispose();
            if (Inspector != null) Inspector.Dispose();
        }
    }
}
