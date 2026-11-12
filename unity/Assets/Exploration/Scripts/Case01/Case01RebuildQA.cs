using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// 第十七號門｜全屋重製 v2 QA（選單「Shepherd/Case01/全屋重製 QA」，場景 Case01HouseRebuildQA.unity）。
    /// 第一階段只有新客廳（前廳）：全新的建築、家具、PBR 材質、烘焙環境光＋即時燈、反射探針、畫面後製。與舊 R3／Phase 1.8／1.9／沉浸式住宅 QA 完全分開。
    /// 自己的第一人稱控制（沿用共用的 FirstPersonController 元件，不改它）、手電筒、近看合照、步速／FOV／眼高／燈光／曝光預設。
    /// 正常遊玩只有中央一個小點；對著可拿起的物件時才出現「E 拿起」。除錯資訊 F12。
    /// </summary>
    [DefaultExecutionOrder(40)]
    public sealed class Case01RebuildQA : MonoBehaviour
    {
        public static readonly float[] WalkSpeeds = { 1.2f, 1.5f, 1.8f };
        public static readonly string[] WalkNames = { "慢走", "正常步行", "較快步行" };
        public static readonly float[] Fovs = { 55f, 62f, 70f };
        public static readonly float[] EyeHeights = { 1.58f, 1.65f, 1.72f };
        public static readonly float[] Exposures = { 0.75f, 1.0f, 1.3f };
        public static readonly string[] ExposureNames = { "暗", "標準", "亮" };
        public const float Acceleration = 6f, BriskFactor = 1.45f;

        public Case01RebuildRoom Room { get; private set; }
        public FirstPersonController Player { get; private set; }
        public Camera Cam { get; private set; }
        public Case01RebuildPost Post { get; private set; }
        public Case01Flashlight Flashlight { get; private set; }
        public Case01Inspector Inspector { get; private set; }
        public Case01InspectItem Photo { get; private set; }
        public int SpeedPreset { get; private set; } = 1;
        public int FovPreset { get; private set; }
        public int EyePreset { get; private set; }
        public int ExposurePreset { get; private set; } = 1;
        public bool LampsOn { get; private set; } = true;
        public bool PendantOn { get; private set; }
        public bool HeadBob { get; private set; } = true;
        public bool DebugOn { get; private set; }
        public bool AimingAtPhoto { get; private set; }
        public Vector3 BobOffset { get; private set; }
        public bool CursorLocked { get; private set; }

        private Case01Palette palette;
        private float bobPhase, bobAmp;
        private bool built, playMode;
        private int savedPixelLights;
        private UnityEngine.ShadowResolution savedShadowRes;

        private void Awake()
        {
            if (Application.isPlaying) Setup(true);
        }

        public void Setup(bool play)
        {
            if (built) return;
            built = true;
            playMode = play;
            palette = new Case01Palette();
            Room = Case01RebuildRoom.FindOrBuild(null);
            Case01RebuildRoom.ApplyAmbient(1f);
            // 這個 QA 的即時燈較多（檯燈、落地燈、壁爐、街燈、月光、手電筒）：執行期間提高逐像素燈數與陰影解析度，結束時還原。
            savedPixelLights = QualitySettings.pixelLightCount;
            savedShadowRes = QualitySettings.shadowResolution;
            QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 8);
            QualitySettings.shadowResolution = UnityEngine.ShadowResolution.High;
            Transform spawn;
            var s = Room.Marks.TryGetValue("E_Spawn", out spawn) ? spawn.position : new Vector3(3.15f, 0f, 3.5f);
            Player = FirstPersonController.Create(transform, s + Vector3.up * 0.05f, 0f);
            var camGo = new GameObject("RebuildCamera");
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.nearClipPlane = 0.03f;
            Cam.farClipPlane = 60f;
            Cam.backgroundColor = Color.black;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.allowHDR = false;
            Cam.cullingMask = ~(1 << Case01Inspector.Layer);
            camGo.AddComponent<AudioListener>();
            Post = camGo.AddComponent<Case01RebuildPost>();
            Flashlight = new Case01Flashlight(Cam, null, palette, palette.Own(Shepherd.Battle.UI.Arena.ShepherdShading.NewLit()));
            Inspector = new Case01Inspector(palette, transform);
            if (Room.PhotoPrint != null)
            {
                Photo = new Case01InspectItem
                {
                    Id = "photo",
                    Title = "壁爐架上的合照（暫用影像，待正式美術）",
                    WorldCarrier = Room.PhotoPrint,
                    Back = Resources.Load<Texture2D>(Case01ImmersiveHouse.Folder + "IH_PhotoBack"),
                    BackWriting = "海邊，週日下午",
                    Size = new Vector2(Room.PhotoPrint.bounds.size.z > Room.PhotoPrint.bounds.size.x ? Room.PhotoPrint.bounds.size.z : Room.PhotoPrint.bounds.size.x, Room.PhotoPrint.bounds.size.y),
                };
                Photo.Fronts.Add(Room.PhotoTexture);
            }
            BuildUi();
            ResetScene();
            Room.RefreshProbe();
        }

        // ---------------- 操作 ----------------

        public bool Inspecting { get { return Inspector != null && Inspector.Active; } }

        public void ResetScene()
        {
            if (Inspecting)
            {
                Inspector.End();
                for (int i = 0; i < 20 && Inspector.Active; i++) Inspector.Tick(0.05f);
            }
            Transform spawn, look;
            var s = Room.Marks.TryGetValue("E_Spawn", out spawn) ? spawn.position : new Vector3(3.15f, 0f, 3.5f);
            float yaw = 0f;
            if (Room.Marks.TryGetValue("E_FireLight", out look))
            {
                var d = look.position - s;
                yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            }
            Player.Teleport(s + Vector3.up * 0.05f, yaw, 6f);
            if (Flashlight != null) Flashlight.SetOn(false);
            SetLamps(true);
            ApplyPlayer();
            SnapCamera();
        }

        public void SetSpeed(int p) { SpeedPreset = Mathf.Clamp(p, 0, 2); ApplyPlayer(); }
        public void CycleFov() { FovPreset = (FovPreset + 1) % Fovs.Length; }
        public void SetFov(int p) { FovPreset = Mathf.Clamp(p, 0, Fovs.Length - 1); }
        public void CycleEye() { EyePreset = (EyePreset + 1) % EyeHeights.Length; ApplyPlayer(); }
        public void SetEye(int p) { EyePreset = Mathf.Clamp(p, 0, EyeHeights.Length - 1); ApplyPlayer(); }
        public void CycleExposure() { ExposurePreset = (ExposurePreset + 1) % Exposures.Length; }
        public void SetExposure(int p) { ExposurePreset = Mathf.Clamp(p, 0, Exposures.Length - 1); }
        public void ToggleFlashlight() { if (Flashlight != null) Flashlight.Toggle(playMode); }
        public void CycleFlashlightLevel() { if (Flashlight != null) Flashlight.CycleLevel(); }
        public void ToggleHeadBob() { HeadBob = !HeadBob; }
        public void ToggleDebug() { DebugOn = !DebugOn; }

        public void SetLamps(bool on)
        {
            LampsOn = on;
            Room.SetLamps(on);
        }

        public void TogglePendant()
        {
            PendantOn = !PendantOn;
            if (Room.Pendant != null) Room.Pendant.enabled = PendantOn;
        }

        public void BeginInspect()
        {
            if (Photo == null || Inspecting) return;
            Inspector.Begin(Photo, Cam);
        }

        private void ApplyPlayer()
        {
            if (Player == null) return;
            Player.WalkSpeedValue = WalkSpeeds[SpeedPreset];
            Player.RunSpeedValue = WalkSpeeds[SpeedPreset] * BriskFactor;
            Player.Acceleration = Acceleration;
            Player.EyeHeightValue = EyeHeights[EyePreset];
        }

        // ---------------- 每影格 ----------------

        /// <summary>一個影格：輸入（測試可直接給）→ 移動／檢視 → 鏡頭 → 燈 → UI。</summary>
        public void Step(float dt, ExploreInput input)
        {
            if (Inspecting)
            {
                bool drag = playMode && Input.GetMouseButton(0);
                Inspector.Feed(input.Look, input.Click ? 0f : 0f, drag);
                if (input.Interact) Inspector.End();
            }
            else
            {
                Player.Step(dt, input, CursorLocked || !playMode);
                AimingAtPhoto = AimAtPhoto();
                if (input.Interact && AimingAtPhoto) BeginInspect();
            }
            Inspector.Tick(dt);
            Flashlight.Tick(dt, Inspecting);
            Room.Tick(Time.time);
            Post.Exposure = Exposures[ExposurePreset];
            SnapCamera();
            ApplyBob(dt);
            RefreshUi(dt);
        }

        public bool AimAtPhoto()
        {
            if (Room.PhotoPrint == null) return false;
            var ray = new Ray(Player.EyePosition, Player.ViewRotation * Vector3.forward);
            RaycastHit h;
            if (Physics.Raycast(ray, out h, 2.4f, ~(1 << Case01Inspector.Layer), QueryTriggerInteraction.Collide))
                return h.collider.name == "Interact_Photo";
            return false;
        }

        private void SnapCamera()
        {
            Cam.transform.position = Player.EyePosition;
            Cam.transform.rotation = Player.ViewRotation;
            Cam.fieldOfView = Fovs[FovPreset];
        }

        private void ApplyBob(float dt)
        {
            float speed = Inspecting ? 0f : Player.LastHorizontalSpeed;
            bobAmp = Mathf.MoveTowards(bobAmp, HeadBob && speed > 0.15f ? Mathf.Clamp01(speed / 1.5f) : 0f, dt * 4f);
            bobPhase += dt * speed / 0.75f * Mathf.PI;
            var off = new Vector3(Mathf.Sin(bobPhase) * 0.004f, Mathf.Abs(Mathf.Sin(bobPhase)) * 0.008f - 0.004f, 0f) * bobAmp;
            BobOffset = off;
            Cam.transform.position += Cam.transform.rotation * off;
            Cam.transform.rotation *= Quaternion.Euler(0f, 0f, Mathf.Sin(bobPhase) * 0.25f * bobAmp);
        }

        private readonly LegacyExploreInput legacy = new LegacyExploreInput();

        /// <summary>Play 模式測試用：設定後 Update 改用這個輸入（取代鍵盤滑鼠），走的仍是同一條 Update 管線。</summary>
        public ExploreInput? ScriptedInput;

        private void Update()
        {
            if (!built || !playMode) return;
            var input = ScriptedInput ?? legacy.Read();
            if (ScriptedInput.HasValue)
            {
                Step(Time.deltaTime, input);
                return;
            }
            // 滑鼠鎖定：點擊畫面鎖定、Esc 釋放；鎖定的那一下點擊不算互動。
            if (input.ReleaseCursor) SetCursor(false);
            else if (input.Click && !CursorLocked)
            {
                SetCursor(true);
                input.Click = false;
            }
            if (Inspecting)
            {
                input.Look *= 1f;
                if (Input.GetKeyDown(KeyCode.Space)) Inspector.Flip();
                if (Input.GetMouseButtonDown(1)) Inspector.End();
                Inspector.Feed(Vector2.zero, Input.mouseScrollDelta.y, false);
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) SetSpeed(0);
                if (Input.GetKeyDown(KeyCode.Alpha2)) SetSpeed(1);
                if (Input.GetKeyDown(KeyCode.Alpha3)) SetSpeed(2);
                if (Input.GetKeyDown(KeyCode.F)) ToggleFlashlight();
                if (Input.GetKeyDown(KeyCode.X)) CycleFlashlightLevel();
                if (Input.GetKeyDown(KeyCode.L)) SetLamps(!LampsOn);
                if (Input.GetKeyDown(KeyCode.K)) TogglePendant();
                if (Input.GetKeyDown(KeyCode.G)) CycleExposure();
                if (Input.GetKeyDown(KeyCode.F5)) CycleFov();
                if (Input.GetKeyDown(KeyCode.F7)) CycleEye();
                if (Input.GetKeyDown(KeyCode.B)) ToggleHeadBob();
                if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.F1)) ResetScene();
            }
            if (Input.GetKeyDown(KeyCode.F12)) ToggleDebug();
            if (!CursorLocked) input.Look = Vector2.zero;
            Step(Time.deltaTime, input);
        }

        private void SetCursor(bool locked)
        {
            CursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus && playMode) SetCursor(false);
        }

        // ---------------- UI ----------------

        private Text dot, prompt, debug;
        private RectTransform debugRect;
        private float fps = 60f;

        private void BuildUi()
        {
            var cgo = new GameObject("Case01RebuildCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 41;
            var sc = cgo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)cgo.transform;
            dot = Case01SpatialQA.Txt(Case01SpatialQA.Rect(root, "Dot", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f)), 18, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.5f));
            dot.text = "·";
            prompt = Case01SpatialQA.Txt(Case01SpatialQA.Rect(root, "Prompt", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(400f, 30f)), 18, TextAnchor.MiddleCenter, new Color(0.95f, 0.9f, 0.8f, 0.85f));
            debugRect = Case01SpatialQA.Rect(root, "Debug", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(640f, 280f));
            Case01SpatialQA.Img(debugRect, new Color(0.02f, 0.025f, 0.03f, 0.72f));
            debug = Case01SpatialQA.Txt(debugRect, 16, TextAnchor.UpperLeft, new Color(0.85f, 0.88f, 0.9f));
            Case01SpatialQA.Inset(debug.rectTransform, 12f, 8f);
        }

        public bool DebugVisible { get { return debugRect != null && debugRect.gameObject.activeSelf; } }
        public string PromptText { get { return prompt != null && prompt.enabled ? prompt.text : null; } }

        private void RefreshUi(float dt)
        {
            if (dot == null) return;
            dot.enabled = !Inspecting;
            prompt.enabled = !Inspecting && AimingAtPhoto;
            prompt.text = "E 拿起合照";
            debugRect.gameObject.SetActive(DebugOn);
            if (!DebugOn) return;
            if (dt > 0f) fps = Mathf.Lerp(fps, 1f / dt, 0.05f);
            var sb = new StringBuilder();
            sb.Append("<b>全屋重製 QA｜新客廳</b>（F12 關閉）\n");
            sb.Append("步行 ").Append(WalkNames[SpeedPreset]).Append(' ').Append(WalkSpeeds[SpeedPreset].ToString("0.0")).Append(" m/s｜實際 ").Append(Player.LastHorizontalSpeed.ToString("0.00")).Append(" m/s\n");
            sb.Append("垂直 FOV ").Append(Fovs[FovPreset].ToString("0")).Append("°｜眼高 ").Append(EyeHeights[EyePreset].ToString("0.00")).Append(" m｜步態 ").Append(HeadBob ? "開" : "關").Append('\n');
            sb.Append("環境燈 ").Append(LampsOn ? "開" : "關").Append("｜吊燈 ").Append(PendantOn ? "開" : "關").Append("｜曝光 ").Append(ExposureNames[ExposurePreset])
                .Append("｜手電筒 ").Append(Flashlight.On ? "開 " + Case01Flashlight.IntensityNames[Flashlight.Level] : "關").Append('\n');
            sb.Append("位置 ").Append(Player.transform.position.ToString("F2")).Append("｜").Append(fps.ToString("0")).Append(" fps｜光照貼圖 ").Append(LightmapSettings.lightmaps.Length).Append('\n');
            sb.Append("<color=#9AA4B0>WASD 走｜Shift 快步｜1／2／3 步速｜F5 FOV｜F7 眼高｜F 手電筒｜X 手電筒強度｜L 環境燈｜K 吊燈｜G 曝光｜B 步態｜E 拿起／放回｜R／F1 回入口｜Esc 釋放滑鼠\n檢視：滑鼠 傾斜｜滾輪 放大｜左鍵拖曳 移動｜空白鍵 翻面｜E／右鍵 放回</color>");
            debug.text = sb.ToString();
        }

        private void OnDestroy()
        {
            if (!built) return;
            QualitySettings.pixelLightCount = savedPixelLights;
            QualitySettings.shadowResolution = savedShadowRes;
            if (Flashlight != null) Flashlight.Dispose();
            if (Inspector != null) Inspector.Dispose();
            if (playMode) SetCursor(false);
            if (palette != null) palette.Dispose();
        }
    }
}
