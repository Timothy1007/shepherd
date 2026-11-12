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
        public const float WalkSpeed = 1.9f;
        public const float BriskSpeed = 3.0f;

        public LanternGraybox World { get; private set; }
        public LanternRig Rig { get; private set; }
        public LanternActions Actions { get; private set; }
        public LanternCombat Combat { get; private set; }
        public LanternFlow Flow { get; private set; }
        public LanternAudio Audio { get; private set; }
        public FirstPersonController Player { get; private set; }
        public Camera Cam { get; private set; }
        public Case01RebuildPost Post { get; private set; }
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
        private Image crosshair, chargeBar, chargeBack, healthBar, healthBack, energyBar, energyBack, hurtOverlay, fadeOverlay, readPanel;
        private Text downText;
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
            RenderSettings.ambientLight = new Color(0.035f, 0.038f, 0.045f);
            RenderSettings.fog = false;
            savedReflection = RenderSettings.reflectionIntensity;
            RenderSettings.reflectionIntensity = 0.25f;
            LanternReveal.Clear();

            World = LanternGraybox.Build(transform);
            var spawn = World.Marks["HouseEntry"];
            Player = FirstPersonController.Create(transform, spawn.position, spawn.eulerAngles.y);
            Player.WalkSpeedValue = WalkSpeed;
            Player.RunSpeedValue = BriskSpeed;
            Player.EyeHeightValue = 1.62f;
            Player.Acceleration = 7f;

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

            Rig = LanternRig.Build(Cam, Player);
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
            lastFeet = Player.transform.position;
            if (play) BuildUi();
            Ready = true;
            Rig.Snap();
            Post.Apply();
            Physics.SyncTransforms();
            if (play) SetCursor(false);
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
        }

        public void Step(float dt, ExploreInput inp, LanternActionInput act = default(LanternActionInput))
        {
            if (!Ready) return;
            time += dt;
            bool locked = Combat.PlayerDown || Flow.Current == LanternFlow.Phase.Collapsing;
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
            Actions.Suspended = Inspecting != null;
            Rig.Focus = FocusHeld;
            Player.WalkSpeedValue = WalkSpeed * Mathf.Lerp(1f, 0.6f, Rig.FocusBlend);
            Player.RunSpeedValue = BriskSpeed * Mathf.Lerp(1f, 0.6f, Rig.FocusBlend);
            Player.Step(dt, inp, true);
            var feet = Player.transform.position;
            stepDistance += Vector3.Distance(new Vector3(feet.x, 0f, feet.z), new Vector3(lastFeet.x, 0f, lastFeet.z));
            lastFeet = feet;
            if (stepDistance > 0.8f) { stepDistance = 0f; Audio?.Play(LanternAudio.Step, 0.35f); }
            Rig.Tick(dt, time);
            World.Tick(time);
            Actions.Step(dt, act);
            Combat.Step(dt);
            Flow.Step(dt);
            if (dt > 0f && simChanged) Physics.Simulate(dt);
            LanternReveal.Tick(dt);
            UpdateUi();
        }

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
              .Append("｜失序體 ").Append(Combat.Enemies.Count);
            return sb.ToString();
        }

        private void Update()
        {
            if (!Ready || !Application.isPlaying) return;
            if (Input.GetKeyDown(KeyCode.F)) ToggleLantern();
            if (Input.GetKeyDown(KeyCode.F1)) GoTo("HouseEntry");
            if (Input.GetKeyDown(KeyCode.F2)) GoTo("HouseLiving");
            if (Input.GetKeyDown(KeyCode.F3)) GoTo("HouseUpper");
            if (Input.GetKeyDown(KeyCode.F4)) GoTo("Spawn");
            if (Input.GetKeyDown(KeyCode.F6)) GoTo("ArenaStart");
            if (Input.GetKeyDown(KeyCode.F7)) CycleRadius();
            if (Input.GetKeyDown(KeyCode.F9)) CycleHouseScale();
            if (Input.GetKeyDown(KeyCode.F10)) Flow.SkipToBoss();
            if (Input.GetKeyDown(KeyCode.B)) ToggleHeadBob();
            if (Input.GetKeyDown(KeyCode.F5)) CycleFov();
            if (Input.GetKeyDown(KeyCode.R)) ResetScene();
            if (Input.GetKeyDown(KeyCode.F12)) ToggleDebug();
            FocusHeld = Input.GetKey(KeyCode.Space);
            var inp = ScriptedInput ?? input.Read();
            if (inp.ReleaseCursor) SetCursor(true);
            bool clickToLock = cursorFree && Input.GetMouseButtonDown(0);
            if (clickToLock) SetCursor(false);
            if (cursorFree && !ScriptedInput.HasValue) inp.Look = Vector2.zero;
            var act = ScriptedAction ?? new LanternActionInput
            {
                Primary = !cursorFree && !clickToLock && Input.GetMouseButton(0),
                Interact = Input.GetKey(KeyCode.E),
                Deploy = Input.GetKey(KeyCode.Q),
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
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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
            hintText.text = "左鍵 射擊（按住蓄力）　E 互動（抓起／按住復原／打開／拿起來看）　Q 擬態錨定　按住Space 聚光　F 提燈　F1 玄關 F2 客廳 F3 二樓 F4 測試房 F6 戰鬥場　F9 房子大小　F10 直接打最終戰　R 重來　F12 數值";
            hintText.color = new Color(1f, 1f, 1f, 0.5f);
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
            downText = MakeText("Down", font, 40, new Vector2(0, 40), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            downText.text = "你倒下了…";
            downText.enabled = false;
            endText = MakeText("End", font, 34, new Vector2(0, 40), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            endText.rectTransform.sizeDelta = new Vector2(1400, 200);
            endText.text = "（暫定）真相拼回來了。\n她曾經在這裡。\n\nR 重新開始";
            endText.enabled = false;
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

        public string PromptText => Actions != null ? Actions.Prompt : "";

        private void UpdateUi()
        {
            if (canvasGo == null) return;
            promptText.text = Reading != null ? "" : Actions.Prompt;
            float charge = Actions.Held != null ? Actions.ThrowCharge : Actions.Charge;
            chargeBack.enabled = charge > 0.01f;
            chargeBar.rectTransform.sizeDelta = new Vector2(80f * charge, 0f);
            crosshair.color = Actions.Aimed != null ? new Color(1f, 0.85f, 0.5f, 0.95f) : new Color(1f, 0.95f, 0.85f, 0.55f);
            crosshair.rectTransform.sizeDelta = Actions.Aimed != null ? new Vector2(10, 10) : new Vector2(6, 6);
            healthBar.rectTransform.sizeDelta = new Vector2(260f * Combat.Health / LanternCombat.MaxHealth, 0f);
            healthBar.color = Combat.Health < 35f ? new Color(1f, 0.35f, 0.3f, 0.9f) : new Color(0.95f, 0.85f, 0.65f, 0.85f);
            energyBar.rectTransform.sizeDelta = new Vector2(200f * Actions.Energy / LanternActions.MaxEnergy, 0f);
            shieldText.text = "擬態錨定 " + new string('◆', Actions.ShieldChargesLeft) + new string('◇', LanternActions.ShieldCharges - Actions.ShieldChargesLeft);
            float vign = Mathf.Max(Combat.HurtFlash * 0.55f, (1f - Combat.Health / LanternCombat.MaxHealth) * 0.25f);
            hurtOverlay.color = new Color(0.55f, 0f, 0f, Combat.PlayerDown ? 0.75f : vign);
            downText.enabled = Combat.PlayerDown;
            progressText.text = Flow.Current == LanternFlow.Phase.Explore ? "修復 " + Flow.Repaired + " / " + Flow.Total + (Combat.MemoriesFound > 0 ? "　記憶碎片 " + Combat.MemoriesFound : "")
                : Flow.Current == LanternFlow.Phase.Boss && Flow.Boss != null ? "最後的反撲　" + Mathf.CeilToInt(Mathf.Max(0f, Flow.Boss.Hp)) + " / " + LanternBoss.MaxHp : "";
            toastText.text = Flow.ToastTimer > 0f ? Flow.Toast : "";
            fadeOverlay.color = new Color(0f, 0f, 0f, Flow.CollapseFade);
            endText.enabled = Flow.Current == LanternFlow.Phase.Ended;
            readPanel.gameObject.SetActive(false);
            var sbClues = new StringBuilder();
            if (Flow.Current == LanternFlow.Phase.Explore)
            {
                if (!World.HousePowered && World.FuseBox != null) sbClues.Append("・房子沒電——玄關樓梯下好像有電箱\n");
                foreach (var cl in World.Clues)
                {
                    if (cl == null) continue;
                    sbClues.Append(cl.Done ? "✓ " : "□ ").Append(cl.Label);
                    if (!cl.Done) sbClues.Append("：").Append(cl.Hint);
                    sbClues.Append('\n');
                }
            }
            cluesText.text = Inspecting != null ? "" : sbClues.ToString();
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
