using System.Text;
using Shepherd.Battle.UI.Arena;
using Shepherd.Exploration.Case01;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 提燈手感 QA（灰盒原型）：左手提燈（光圈內顯示真實）、右手穩定器（射擊／牽引／復原）、低頭看得到身體。
    /// 不接舊卡牌戰鬥、不修改正式 Demo 或其他 QA。
    /// 操作：WASD 移動、滑鼠視角、Shift 快走、F 提燈、Q 光圈大小、1/2/3 或滾輪切換穩定器模式、左鍵使用、右鍵蓄力丟出（牽引時）、
    /// F1–F4 傳送、B 晃動、F5 FOV、R 重置、F12 數值、Esc 釋放滑鼠。
    /// </summary>
    [DefaultExecutionOrder(40)]
    public sealed class LanternPrototypeQA : MonoBehaviour
    {
        public static readonly float[] Fovs = { 55f, 62f, 70f };
        public const float WalkSpeed = 1.7f;
        public const float BriskSpeed = 2.6f;
        private static readonly string[] ModeNames = { "射擊", "牽引", "復原" };

        public LanternGraybox World { get; private set; }
        public LanternRig Rig { get; private set; }
        public LanternActions Actions { get; private set; }
        public LanternCombat Combat { get; private set; }
        public LanternAudio Audio { get; private set; }
        /// <summary>按住聚光（Space）：把提燈舉高，光變成往前的細長光束；聚光時走路變慢。</summary>
        public bool FocusHeld;
        public FirstPersonController Player { get; private set; }
        public Camera Cam { get; private set; }
        public Case01RebuildPost Post { get; private set; }
        public int FovIndex { get; private set; } = 1;
        public bool DebugVisible { get; private set; }
        public bool Ready { get; private set; }
        public ExploreInput? ScriptedInput;
        public LanternActionInput? ScriptedAction;

        private float time;
        private Text debugText, hintText, modeText, promptText;
        private Image crosshair, chargeBar, chargeBack, healthBar, healthBack, hurtOverlay;
        private Text downText;
        private float stepDistance;
        private Vector3 lastFeet;
        private GameObject canvasGo;
        private bool cursorFree;
        private readonly LegacyExploreInput input = new LegacyExploreInput();
        private Color savedAmbient;
        private UnityEngine.Rendering.AmbientMode savedMode;
        private bool savedFog;
        private SimulationMode savedSim;
        private bool simChanged;
        private float savedReflection = 1f;

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
            // 實際遊玩時維持 Unity 正常的物理更新，不動全域設定。
            if (!Application.isPlaying)
            {
                savedSim = Physics.simulationMode;
                Physics.simulationMode = SimulationMode.Script;
                simChanged = true;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.035f, 0.038f, 0.045f);
            RenderSettings.fog = false;
            savedReflection = RenderSettings.reflectionIntensity;
            RenderSettings.reflectionIntensity = 0.25f;
            LanternReveal.Clear();

            World = LanternGraybox.Build(transform);
            var spawn = World.Marks["Spawn"];
            Player = FirstPersonController.Create(transform, spawn.position, spawn.eulerAngles.y);
            Player.WalkSpeedValue = WalkSpeed;
            Player.RunSpeedValue = BriskSpeed;
            Player.EyeHeightValue = 1.62f;
            Player.Acceleration = 7f;

            var camGo = new GameObject("LanternCamera");
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.nearClipPlane = 0.03f;
            Cam.farClipPlane = 60f;
            Cam.fieldOfView = Fovs[FovIndex];
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Color.black;
            Cam.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
            Post = camGo.AddComponent<Case01RebuildPost>();
            Post.Exposure = 1.0f;

            Rig = LanternRig.Build(Cam, Player);
            Audio = LanternAudio.Build(camGo.transform);
            Actions = new LanternActions(Cam, Rig, World);
            Actions.Audio = Audio;
            Combat = new LanternCombat(World, Player, Rig, Audio, Actions.Particles);
            Actions.Targets = () => Combat.Living;
            lastFeet = Player.transform.position;
            if (play) BuildUi();
            Ready = true;
            Rig.Snap();
            Post.Apply();
            Physics.SyncTransforms();
            if (play) SetCursor(false);
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

        /// <summary>傳送到某個標記，朝向標記的方向。</summary>
        public void GoTo(string mark)
        {
            Transform t;
            if (!World.Marks.TryGetValue(mark, out t)) return;
            Actions.DropHeld(false);
            var f = t.forward;
            float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
            Player.Teleport(t.position, yaw, pitch);
            Rig.Snap();
        }

        public void ResetScene()
        {
            Rig.SetOn(true);
            Rig.SetRadius(1);
            GoTo("Spawn");
        }

        /// <summary>推進一個影格（Update 或測試呼叫）。</summary>
        public void Step(float dt, ExploreInput inp, LanternActionInput act = default(LanternActionInput))
        {
            if (!Ready) return;
            time += dt;
            if (Combat.PlayerDown) { inp.Move = Vector2.zero; inp.Look = Vector2.zero; act = default(LanternActionInput); }
            Rig.Focus = FocusHeld;
            Player.WalkSpeedValue = WalkSpeed * Mathf.Lerp(1f, 0.6f, Rig.FocusBlend);
            Player.RunSpeedValue = BriskSpeed * Mathf.Lerp(1f, 0.6f, Rig.FocusBlend);
            Player.Step(dt, inp, true);
            var feet = Player.transform.position;
            stepDistance += Vector3.Distance(new Vector3(feet.x, 0f, feet.z), new Vector3(lastFeet.x, 0f, lastFeet.z));
            lastFeet = feet;
            if (stepDistance > 0.75f) { stepDistance = 0f; Audio?.Play(LanternAudio.Step, 0.35f); }
            Rig.Tick(dt, time);
            World.Tick(time);
            Actions.Step(dt, act);
            Combat.Step(dt);
            if (dt > 0f && simChanged) Physics.Simulate(dt);
            LanternReveal.Tick(dt);
            UpdateUi();
        }

        public string DebugString()
        {
            var sb = new StringBuilder();
            sb.Append("位置 ").Append(Player.transform.position.ToString("F2"))
              .Append("｜FOV ").Append(Cam.fieldOfView.ToString("0"))
              .Append("｜步速 ").Append(Player.LastHorizontalSpeed.ToString("0.0"))
              .Append("｜提燈 ").Append(Rig.On ? "開" : "關")
              .Append("｜光圈 ").Append(Rig.CurrentRadius.ToString("0.0")).Append(" m")
              .Append("｜模式 ").Append(ModeNames[(int)Actions.Mode])
              .Append("｜現實泡泡 ").Append(LanternReveal.ActiveBubbles)
              .Append("｜生命 ").Append(Combat.Health.ToString("0"))
              .Append("｜失序體 ").Append(Combat.Enemies.Count > 0 && Combat.Enemies[0] != null ? Combat.Enemies[0].Current + " " + Combat.Enemies[0].Hp.ToString("0.0") : "-");
            return sb.ToString();
        }

        private void Update()
        {
            if (!Ready || !Application.isPlaying) return;
            if (Input.GetKeyDown(KeyCode.F)) ToggleLantern();
            if (Input.GetKeyDown(KeyCode.Q)) CycleRadius();
            if (Input.GetKeyDown(KeyCode.F1)) GoTo("Spawn");
            if (Input.GetKeyDown(KeyCode.F2)) GoTo("LivingEntry");
            if (Input.GetKeyDown(KeyCode.F3)) GoTo("KitchenEntry");
            if (Input.GetKeyDown(KeyCode.F4)) GoTo("BrightEntry");
            if (Input.GetKeyDown(KeyCode.F6)) GoTo("ArenaStart");
            FocusHeld = Input.GetKey(KeyCode.Space);
            if (Input.GetKeyDown(KeyCode.B)) ToggleHeadBob();
            if (Input.GetKeyDown(KeyCode.F5)) CycleFov();
            if (Input.GetKeyDown(KeyCode.R)) ResetScene();
            if (Input.GetKeyDown(KeyCode.F12)) ToggleDebug();
            var inp = ScriptedInput ?? input.Read();
            if (inp.ReleaseCursor) SetCursor(true);
            bool clickToLock = cursorFree && Input.GetMouseButtonDown(0);
            if (clickToLock) SetCursor(false);
            if (cursorFree && !ScriptedInput.HasValue) inp.Look = Vector2.zero;
            var act = ScriptedAction ?? new LanternActionInput
            {
                Primary = !cursorFree && !clickToLock && Input.GetMouseButton(0),
                Secondary = !cursorFree && Input.GetMouseButton(1),
                SelectMode = Input.GetKeyDown(KeyCode.Alpha1) ? 1 : Input.GetKeyDown(KeyCode.Alpha2) ? 2 : Input.GetKeyDown(KeyCode.Alpha3) ? 3 : 0,
                Scroll = Input.mouseScrollDelta.y,
            };
            Step(Time.deltaTime, inp, act);
        }

        private void SetCursor(bool free)
        {
            cursorFree = free;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        // ---------------- 畫面提示 ----------------

        private void BuildUi()
        {
            canvasGo = new GameObject("LanternQAUI");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            crosshair = new GameObject("Crosshair").AddComponent<Image>();
            crosshair.transform.SetParent(canvasGo.transform, false);
            crosshair.rectTransform.sizeDelta = new Vector2(6, 6);
            crosshair.color = new Color(1f, 0.95f, 0.85f, 0.7f);

            chargeBack = new GameObject("ChargeBack").AddComponent<Image>();
            chargeBack.transform.SetParent(canvasGo.transform, false);
            chargeBack.rectTransform.sizeDelta = new Vector2(80, 4);
            chargeBack.rectTransform.anchoredPosition = new Vector2(0, -26);
            chargeBack.color = new Color(0f, 0f, 0f, 0.4f);
            chargeBar = new GameObject("ChargeBar").AddComponent<Image>();
            chargeBar.transform.SetParent(chargeBack.transform, false);
            chargeBar.rectTransform.anchorMin = new Vector2(0, 0);
            chargeBar.rectTransform.anchorMax = new Vector2(0, 1);
            chargeBar.rectTransform.pivot = new Vector2(0, 0.5f);
            chargeBar.color = new Color(1f, 0.85f, 0.55f, 0.9f);

            hintText = MakeText("Hint", font, 18, new Vector2(24, 20), TextAnchor.LowerLeft, new Vector2(0, 0));
            hintText.text = "F 提燈　按住Space 聚光　Q 光圈　1/2/3 或滾輪 換模式　左鍵 使用　右鍵 蓄力丟出　F1–F4 走廊／客廳／廚房／亮房間　F6 戰鬥場　B 晃動　F5 視野　R 重置　F12 數值　Esc 滑鼠";
            hintText.color = new Color(1f, 1f, 1f, 0.5f);
            modeText = MakeText("Mode", font, 26, new Vector2(-30, 24), TextAnchor.LowerRight, new Vector2(1, 0));
            promptText = MakeText("Prompt", font, 22, new Vector2(0, -70), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            promptText.color = new Color(1f, 0.96f, 0.88f, 0.85f);
            debugText = MakeText("Debug", font, 18, new Vector2(24, -20), TextAnchor.UpperLeft, new Vector2(0, 1));
            hurtOverlay = new GameObject("HurtOverlay").AddComponent<Image>();
            hurtOverlay.transform.SetParent(canvasGo.transform, false);
            hurtOverlay.rectTransform.anchorMin = Vector2.zero;
            hurtOverlay.rectTransform.anchorMax = Vector2.one;
            hurtOverlay.rectTransform.sizeDelta = Vector2.zero;
            hurtOverlay.raycastTarget = false;
            hurtOverlay.color = new Color(0.6f, 0f, 0f, 0f);
            hurtOverlay.transform.SetAsFirstSibling();
            healthBack = new GameObject("HealthBack").AddComponent<Image>();
            healthBack.transform.SetParent(canvasGo.transform, false);
            healthBack.rectTransform.anchorMin = healthBack.rectTransform.anchorMax = healthBack.rectTransform.pivot = new Vector2(0, 0);
            healthBack.rectTransform.anchoredPosition = new Vector2(24, 56);
            healthBack.rectTransform.sizeDelta = new Vector2(260, 10);
            healthBack.color = new Color(0f, 0f, 0f, 0.45f);
            healthBar = new GameObject("HealthBar").AddComponent<Image>();
            healthBar.transform.SetParent(healthBack.transform, false);
            healthBar.rectTransform.anchorMin = new Vector2(0, 0);
            healthBar.rectTransform.anchorMax = new Vector2(0, 1);
            healthBar.rectTransform.pivot = new Vector2(0, 0.5f);
            healthBar.color = new Color(0.95f, 0.85f, 0.65f, 0.85f);
            downText = MakeText("Down", font, 40, new Vector2(0, 40), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            downText.text = "你倒下了…";
            downText.enabled = false;
            debugText.enabled = false;
        }

        private Text MakeText(string name, Font font, int size, Vector2 offset, TextAnchor anchor, Vector2 pivot)
        {
            var t = new GameObject(name).AddComponent<Text>();
            t.transform.SetParent(canvasGo.transform, false);
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = pivot;
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(1500, 60);
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.8f);
            return t;
        }

        public string PromptText => Actions != null ? Actions.Prompt : "";

        private void UpdateUi()
        {
            if (canvasGo == null) return;
            int m = (int)Actions.Mode;
            var sb = new StringBuilder();
            for (int i = 0; i < 3; i++)
            {
                if (i > 0) sb.Append("　");
                sb.Append(i == m ? "【" : " ").Append(i + 1).Append(' ').Append(ModeNames[i]).Append(i == m ? "】" : " ");
            }
            modeText.text = sb.ToString();
            promptText.text = Actions.Prompt;
            float charge = Actions.Mode == StabilizerMode.Tether ? Actions.ThrowCharge : Actions.Charge;
            chargeBack.enabled = charge > 0.01f;
            chargeBar.rectTransform.sizeDelta = new Vector2(80f * charge, 0f);
            crosshair.color = Actions.Aimed != null ? new Color(1f, 0.85f, 0.5f, 0.95f) : new Color(1f, 0.95f, 0.85f, 0.55f);
            crosshair.rectTransform.sizeDelta = Actions.Aimed != null ? new Vector2(10, 10) : new Vector2(6, 6);
            if (DebugVisible) debugText.text = DebugString();
            healthBar.rectTransform.sizeDelta = new Vector2(260f * Combat.Health / LanternCombat.MaxHealth, 0f);
            healthBar.color = Combat.Health < 35f ? new Color(1f, 0.35f, 0.3f, 0.9f) : new Color(0.95f, 0.85f, 0.65f, 0.85f);
            float vign = Mathf.Max(Combat.HurtFlash * 0.55f, (1f - Combat.Health / LanternCombat.MaxHealth) * 0.25f);
            hurtOverlay.color = new Color(0.55f, 0f, 0f, Combat.PlayerDown ? 0.75f : vign);
            downText.enabled = Combat.PlayerDown;
        }

        /// <summary>還原暫時改動的全域設定（物理模擬模式、環境光）。可重複呼叫。編輯器模式下不會自動呼叫 OnDestroy，測試與截圖要自己呼叫。</summary>
        public void Shutdown()
        {
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
            if (Actions != null) Actions.Destroy();
            if (Combat != null) Combat.Destroy();
            if (Rig != null) Rig.Destroy();
            if (World != null) World.Destroy();
            LanternReveal.Clear();
            if (Application.isPlaying) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
    }
}
