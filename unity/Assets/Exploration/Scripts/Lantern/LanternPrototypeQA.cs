using System.Text;
using Shepherd.Battle.UI.Arena;
using Shepherd.Exploration.Case01;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 提燈手感 QA（第一段原型，灰盒）：第一人稱的手、提燈、身體；提燈光圈內顯示真實、光圈外顯示異域。
    /// 不接舊卡牌戰鬥、不修改正式 Demo 或其他 QA。
    /// 操作：WASD 移動、滑鼠視角、Shift 快走、F 提燈開關、Q 光圈大小、1/2/3 傳送到三個空間、B 晃動、F5 FOV、R 重置、F12 數值、Esc 釋放滑鼠。
    /// </summary>
    [DefaultExecutionOrder(40)]
    public sealed class LanternPrototypeQA : MonoBehaviour
    {
        public static readonly float[] Fovs = { 55f, 62f, 70f };
        public const float WalkSpeed = 1.7f;
        public const float BriskSpeed = 2.6f;

        public LanternGraybox World { get; private set; }
        public LanternRig Rig { get; private set; }
        public FirstPersonController Player { get; private set; }
        public Camera Cam { get; private set; }
        public Case01RebuildPost Post { get; private set; }
        public int FovIndex { get; private set; } = 1;
        public bool DebugVisible { get; private set; }
        public bool Ready { get; private set; }
        public ExploreInput? ScriptedInput;

        private float time;
        private Text debugText, hintText;
        private GameObject canvasGo;
        private bool cursorFree;
        private readonly LegacyExploreInput input = new LegacyExploreInput();
        private Color savedAmbient;
        private UnityEngine.Rendering.AmbientMode savedMode;
        private bool savedFog;

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
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.035f, 0.038f, 0.045f);
            RenderSettings.fog = false;

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
            if (play) BuildUi();
            Ready = true;
            Rig.Snap();
            Post.Apply();
            if (play) SetCursor(false);
        }

        // ---------------- 操作 ----------------

        public void ToggleLantern() => Rig.Toggle();
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
        public void Step(float dt, ExploreInput inp)
        {
            if (!Ready) return;
            time += dt;
            Player.Step(dt, inp, true);
            Rig.Tick(dt, time);
            World.Tick(time);
            if (DebugVisible && debugText != null) debugText.text = DebugString();
        }

        public string DebugString()
        {
            var sb = new StringBuilder();
            sb.Append("位置 ").Append(Player.transform.position.ToString("F2"))
              .Append("｜FOV ").Append(Cam.fieldOfView.ToString("0"))
              .Append("｜眼高 ").Append(Player.EyeHeightValue.ToString("0.00"))
              .Append("｜步速 ").Append(Player.LastHorizontalSpeed.ToString("0.0"))
              .Append("｜提燈 ").Append(Rig.On ? "開" : "關")
              .Append("｜光圈 ").Append(Rig.CurrentRadius.ToString("0.0")).Append(" m");
            return sb.ToString();
        }

        private void Update()
        {
            if (!Ready || !Application.isPlaying) return;
            if (Input.GetKeyDown(KeyCode.F)) ToggleLantern();
            if (Input.GetKeyDown(KeyCode.Q)) CycleRadius();
            if (Input.GetKeyDown(KeyCode.Alpha1)) GoTo("Spawn");
            if (Input.GetKeyDown(KeyCode.Alpha2)) GoTo("LivingEntry");
            if (Input.GetKeyDown(KeyCode.Alpha3)) GoTo("BrightEntry");
            if (Input.GetKeyDown(KeyCode.B)) ToggleHeadBob();
            if (Input.GetKeyDown(KeyCode.F5)) CycleFov();
            if (Input.GetKeyDown(KeyCode.R)) ResetScene();
            if (Input.GetKeyDown(KeyCode.F12)) ToggleDebug();
            var inp = ScriptedInput ?? input.Read();
            if (inp.ReleaseCursor) SetCursor(true);
            if (cursorFree && Input.GetMouseButtonDown(0)) SetCursor(false);
            if (cursorFree && !ScriptedInput.HasValue) inp.Look = Vector2.zero;
            Step(Time.deltaTime, inp);
        }

        private void SetCursor(bool free)
        {
            cursorFree = free;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
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
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var dot = new GameObject("Crosshair").AddComponent<Image>();
            dot.transform.SetParent(canvasGo.transform, false);
            dot.rectTransform.sizeDelta = new Vector2(5, 5);
            dot.color = new Color(1f, 0.95f, 0.85f, 0.7f);

            hintText = MakeText("Hint", font, 20, new Vector2(24, 20), TextAnchor.LowerLeft);
            hintText.text = "F 提燈開關　Q 光圈大小　1/2/3 走廊／客廳／亮房間　B 晃動　F5 視野　R 重置　F12 數值　Esc 釋放滑鼠";
            hintText.color = new Color(1f, 1f, 1f, 0.55f);
            debugText = MakeText("Debug", font, 20, new Vector2(24, -20), TextAnchor.UpperLeft);
            debugText.enabled = false;
        }

        private Text MakeText(string name, Font font, int size, Vector2 offset, TextAnchor anchor)
        {
            var t = new GameObject(name).AddComponent<Text>();
            t.transform.SetParent(canvasGo.transform, false);
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            var rt = t.rectTransform;
            bool bottom = anchor == TextAnchor.LowerLeft;
            rt.anchorMin = rt.anchorMax = rt.pivot = bottom ? new Vector2(0, 0) : new Vector2(0, 1);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(1600, 60);
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.8f);
            return t;
        }

        private void OnDestroy()
        {
            if (Rig != null) Rig.Destroy();
            if (World != null) World.Destroy();
            if (Ready)
            {
                RenderSettings.ambientLight = savedAmbient;
                RenderSettings.ambientMode = savedMode;
                RenderSettings.fog = savedFog;
            }
            if (Application.isPlaying) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
    }
}
