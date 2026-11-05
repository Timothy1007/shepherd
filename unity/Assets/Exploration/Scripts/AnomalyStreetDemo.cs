using System.Collections.Generic;
using Shepherd.Battle.Core;
using Shepherd.Battle.UI;
using Shepherd.Battle.UI.Arena;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Shepherd.Exploration
{
    public enum DemoState
    {
        /// <summary>第一人稱自由探索。</summary>
        Exploring,
        /// <summary>遭遇提示＋鏡頭從第一人稱平順移到戰鬥鏡頭（移動已暫停）。</summary>
        EnteringBattle,
        /// <summary>卡牌戰鬥（Combat Lab HUD）。</summary>
        Battle,
        /// <summary>勝利後鏡頭回到第一人稱。</summary>
        ReturningToExplore,
    }

    /// <summary>戰鬥視角（Demo 比較用）。</summary>
    public enum BattleViewMode
    {
        /// <summary>預設：固定的第三人稱戰術鏡頭（與 CombatLab3D 相同）。</summary>
        TacticalThirdPerson,
        /// <summary>比較用：戰鬥沿用第一人稱（鏡頭在玩家眼睛高度，看向敵群，隱藏玩家模型）。</summary>
        FirstPerson,
    }

    /// <summary>
    /// 《牧羊人》3D 異域探索 Demo v0.1：第一人稱探索 → 遭遇 → 3D 卡牌戰鬥 → 返回探索。
    /// 街區、角色與 HUD 都在執行時以程式建立（Graybox）。
    /// 戰鬥直接重用 <see cref="CombatArenaView"/>（嵌入模式），結算全部交給既有的 BattleController／RoundResolver；
    /// 這裡只負責探索、遭遇判定、輸入切換與鏡頭過場。
    /// </summary>
    public sealed class AnomalyStreetDemo : MonoBehaviour
    {
        public const float EnterBlendSeconds = 1.4f;
        public const float ReturnBlendSeconds = 1.0f;
        public const float InteractDistance = 2.8f;
        public const float ExploreFieldOfView = 70f;
        public const float FirstPersonBattleFieldOfView = 68f;

        [Tooltip("戰鬥視角（Demo 比較用）。探索中按 V 也可切換。")]
        [SerializeField] private BattleViewMode battleView = BattleViewMode.TacticalThirdPerson;

        [Tooltip("固定亂數種子（與 Combat Lab 相同的預設值）。")]
        [SerializeField] private int seed = CombatLabScenario.DefaultSeed;

        private bool built;
        private bool playMode;
        private CombatLabScenario scenarioOverride;
        private Transform content;
        private StreetLayout layout;
        private Camera exploreCam;
        private Light moon;
        private FirstPersonController player;
        private ExploreHud hud;
        private EncounterZone encounter;
        private Transform encounterGroup;
        private readonly List<Transform> encounterFigures = new List<Transform>();
        private readonly List<List<Transform>> figureOrbiters = new List<List<Transform>>();
        private CombatArenaView arena;
        private float clock;
        private float blendT;
        private Vector3 blendFromPos;
        private Quaternion blendFromRot;
        private float blendFromFov;
        private bool anomalyInvestigated;
        private bool cursorCaptured;
        private bool pendingReturn;
        private bool pendingRestart;

        public IExploreInputSource InputSource { get; set; }
        public DemoState State { get; private set; }
        public FirstPersonController Player { get { return player; } }
        public Camera ExploreCamera { get { return exploreCam; } }
        public CombatArenaView Arena { get { return arena; } }
        public EncounterZone Encounter { get { return encounter; } }
        public ExploreHud Hud { get { return hud; } }
        public StreetLayout Layout { get { return layout; } }
        public bool AnomalyInvestigated { get { return anomalyInvestigated; } }
        public bool CursorCaptured { get { return cursorCaptured; } }
        public BattleOutcome? LastOutcome { get; private set; }
        /// <summary>重新開始 Demo 的次數。</summary>
        public int Restarts { get; private set; }
        /// <summary>目前畫面使用中的攝影機（探索／過場為探索攝影機，戰鬥中為戰鬥攝影機）。</summary>
        public Camera ActiveCamera
        {
            get { return arena != null && arena.ArenaCamera != null && arena.ArenaCamera.enabled ? arena.ArenaCamera : exploreCam; }
        }

        public BattleViewMode BattleView
        {
            get { return battleView; }
            set
            {
                battleView = value;
                RefreshViewLabel();
            }
        }

        public bool EncounterFiguresVisible
        {
            get { return encounterGroup != null && encounterGroup.gameObject.activeSelf; }
        }

        private void Awake()
        {
            Build(true);
        }

        private void OnDestroy()
        {
            if (playMode)
            {
                RestoreAtmosphere();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        /// <summary>
        /// 建立整個 Demo。playMode＝false 供 EditMode 測試與截圖（不建立 EventSystem、不改 RenderSettings 與滑鼠）。
        /// scenario 供測試指定戰鬥情境；預設使用 Combat Lab 的標準情境（現有數值）。
        /// </summary>
        public void Build(bool playMode, CombatLabScenario scenario = null, int? seedOverride = null)
        {
            if (built) return;
            built = true;
            this.playMode = playMode;
            scenarioOverride = scenario;
            if (seedOverride.HasValue) seed = seedOverride.Value;
            if (InputSource == null) InputSource = new LegacyExploreInput();

            if (playMode && FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }

            var camGo = new GameObject("ExploreCamera");
            camGo.transform.SetParent(transform, false);
            exploreCam = camGo.AddComponent<Camera>();
            exploreCam.clearFlags = CameraClearFlags.SolidColor;
            exploreCam.backgroundColor = ArenaArt.FogColor;
            exploreCam.fieldOfView = ExploreFieldOfView;
            exploreCam.nearClipPlane = 0.05f;
            exploreCam.farClipPlane = 120f;
            if (playMode)
            {
                camGo.tag = "MainCamera";
                camGo.AddComponent<AudioListener>();
                ApplyAtmosphere();
            }

            var moonGo = new GameObject("Moonlight");
            moonGo.transform.SetParent(transform, false);
            moonGo.transform.rotation = Quaternion.Euler(48f, -38f, 0f);
            moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.58f, 0.64f, 0.88f);
            moon.intensity = 0.5f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.8f;

            hud = new ExploreHud(transform);
            BuildContent();
        }

        private CombatLabScenario CurrentScenario()
        {
            if (scenarioOverride == null) return CombatLabScenario.Default();
            // 每次重新開始都給戰鬥一份新的情境資料（敵人狀態會在戰鬥中改變）。
            var copy = new CombatLabScenario { Name = scenarioOverride.Name };
            foreach (var e in scenarioOverride.Enemies)
            {
                copy.Enemies.Add(new EnemyState
                {
                    Id = e.Id, Name = e.Name, Value = e.Value, Links = e.Links, Corrections = e.Corrections, Bind = e.Bind,
                    AttackPattern = e.AttackPattern,
                });
            }
            return copy;
        }

        /// <summary>建立街區、玩家、遭遇區（重新開始時整組重建）。</summary>
        private void BuildContent()
        {
            content = ArenaArt.Group("DemoContent", transform, Vector3.zero);
            layout = StreetBuilder.Build(content);
            player = FirstPersonController.Create(content, layout.Spawn, layout.SpawnYaw);

            var scenario = CurrentScenario();
            int count = scenario.Enemies.Count;
            var anchor = layout.EncounterAnchor;
            encounter = new EncounterZone
            {
                Id = "plaza",
                Center = anchor.TransformPoint(CombatArenaView.EnemyGroupCenter(count)),
            };
            layout.EncounterRing.position = encounter.Center + Vector3.up * 0.03f;
            layout.EncounterRing.localScale = new Vector3(encounter.TriggerRadius * 2f, 0.005f, encounter.TriggerRadius * 2f);

            // 探索中看到的敵人：與戰鬥開始時 CombatArenaView 建立的位置、高度、朝向相同，切換時不會跳動。
            encounterGroup = ArenaArt.Group("EncounterEnemies", content, Vector3.zero);
            encounterGroup.position = anchor.position;
            encounterGroup.rotation = anchor.rotation;
            encounterFigures.Clear();
            figureOrbiters.Clear();
            for (int i = 0; i < count; i++)
            {
                var e = scenario.Enemies[i];
                var home = CombatArenaView.EnemyLocalHome(i, count);
                Renderer[] rs;
                Material core;
                List<Transform> orbiters;
                var fig = ArenaArt.BuildEnemy(encounterGroup, home, CombatArenaView.EnemyHeightFor(e.Value), i % 2, out rs, out core, out orbiters);
                fig.name = "Wanderer_" + e.Id;
                fig.localRotation = CombatArenaView.EnemyLocalRotation(home);
                encounterFigures.Add(fig);
                figureOrbiters.Add(orbiters);
            }

            State = DemoState.Exploring;
            anomalyInvestigated = false;
            LastOutcome = null;
            pendingReturn = false;
            pendingRestart = false;
            SetCursorCaptured(true);
            hud.Visible = true;
            hud.HideEncounter();
            hud.HideInspect();
            hud.SetHint(null);
            UpdateObjective();
            RefreshViewLabel();
            SnapCameraToPlayer();
            Physics.SyncTransforms();
        }

        // ---------------- 每影格 ----------------

        private void Update()
        {
            if (!built) return;
            Tick(Time.deltaTime, InputSource != null ? InputSource.Read() : ExploreInput.None);
        }

        /// <summary>推進一個影格（Update 呼叫；測試可直接以指定輸入呼叫）。</summary>
        public void Tick(float dt, ExploreInput input)
        {
            if (pendingRestart)
            {
                pendingRestart = false;
                RestartNow();
                return;
            }
            clock += dt;
            hud.Tick(dt);
            AnimateWorld(dt);
            switch (State)
            {
                case DemoState.Exploring:
                    TickExploring(dt, input);
                    break;
                case DemoState.EnteringBattle:
                    TickEnter(dt);
                    break;
                case DemoState.Battle:
                    if (pendingReturn)
                    {
                        pendingReturn = false;
                        BeginReturn();
                    }
                    break;
                case DemoState.ReturningToExplore:
                    TickReturn(dt);
                    break;
            }
            // EditMode 沒有 Update：由這裡推進戰鬥動畫與事件播放。
            if (!playMode && arena != null) arena.Tick(dt);
        }

        private void TickExploring(float dt, ExploreInput input)
        {
            if (input.ReleaseCursor) SetCursorCaptured(false);
            else if (input.Click && !cursorCaptured && !PointerOverUi()) SetCursorCaptured(true);
            if (input.ToggleView)
            {
                BattleView = battleView == BattleViewMode.TacticalThirdPerson ? BattleViewMode.FirstPerson : BattleViewMode.TacticalThirdPerson;
                hud.ShowToast("戰鬥視角（Demo 測試）：" + ViewModeName(battleView));
            }

            player.Step(dt, input, cursorCaptured);
            SnapCameraToPlayer();

            // 遭遇判定。
            var check = encounter.Check(player.transform.position);
            if (check == EncounterCheck.Trigger)
            {
                StartEncounter();
                return;
            }

            // 調查異常物件。
            bool canInspect = CanInspectAnomaly();
            if (canInspect && input.Interact)
            {
                if (hud.InspectVisible) hud.HideInspect();
                else InspectAnomaly();
            }
            if (hud.InspectVisible && !NearAnomaly()) hud.HideInspect();

            string hint = null;
            if (canInspect) hint = hud.InspectVisible ? "［E］關閉" : "［E］調查異常";
            else if (check == EncounterCheck.Near)
                hint = "<color=#C79BFF>前方的裂痕附近有 " + encounterFigures.Count + " 個失序體</color>　靠近即會進入戰鬥";
            hud.SetHint(hint);
            hud.CursorHintVisible = !cursorCaptured;
        }

        private void AnimateWorld(float dt)
        {
            for (int i = 0; i < layout.Floating.Count; i++)
            {
                var s = layout.Floating[i];
                s.localPosition = layout.FloatingHome[i] + Vector3.up * Mathf.Sin(clock * 0.7f + i) * 0.25f;
                s.Rotate(new Vector3(9f, 14f, 5f) * dt, Space.Self);
            }
            for (int f = 0; f < figureOrbiters.Count; f++)
            {
                var list = figureOrbiters[f];
                for (int k = 0; k < list.Count; k++)
                {
                    float a = clock * (0.9f + k * 0.15f) + k * 1.3f + f;
                    list[k].localPosition = new Vector3(Mathf.Cos(a) * 0.75f, 1.2f + Mathf.Sin(a * 1.7f) * 0.6f, Mathf.Sin(a) * 0.55f);
                }
            }
            if (layout.Anomaly != null) layout.Anomaly.Rotate(0f, (anomalyInvestigated ? 8f : 25f) * dt, 0f, Space.World);
            if (layout.EncounterRingMat != null && !encounter.Defeated)
            {
                var c = layout.EncounterRingMat.color;
                c.a = 0.12f + Mathf.Sin(clock * 2f) * 0.05f;
                layout.EncounterRingMat.color = c;
            }
        }

        private void SnapCameraToPlayer()
        {
            exploreCam.transform.position = player.EyePosition;
            exploreCam.transform.rotation = player.ViewRotation;
            exploreCam.fieldOfView = ExploreFieldOfView;
        }

        // ---------------- 異常物件 ----------------

        private bool NearAnomaly()
        {
            if (layout.Anomaly == null) return false;
            var d = layout.Anomaly.position - player.transform.position;
            d.y = 0f;
            return d.magnitude <= InteractDistance + 0.6f;
        }

        /// <summary>在調查距離內並大致看向異常物件。</summary>
        public bool CanInspectAnomaly()
        {
            if (layout.Anomaly == null) return false;
            var flat = layout.Anomaly.position - player.transform.position;
            flat.y = 0f;
            if (flat.magnitude > InteractDistance) return false;
            var look = player.ViewRotation * Vector3.forward;
            look.y = 0f;
            return flat.sqrMagnitude < 0.01f || Vector3.Dot(look.normalized, flat.normalized) > 0.5f;
        }

        private void InspectAnomaly()
        {
            bool first = !anomalyInvestigated;
            anomalyInvestigated = true;
            hud.ShowInspect(
                "<b><color=#C79BFF>異常物件：凝固的裂痕碎片</color></b>\n\n" +
                "碎片懸在半空，周圍的雨滴停在原處不再落下。\n" +
                "碎片的紋路指向北方的廣場──那裡的裂痕更大，失序體在附近徘徊。\n\n" +
                "<color=#9BE59B>已調查。</color>");
            if (first)
            {
                ArenaArt.SetEmission(layout.AnomalyMat, new Color(0.35f, 0.65f, 1f) * 1.6f);
                if (layout.AnomalyLight != null) layout.AnomalyLight.color = new Color(0.45f, 0.7f, 1f);
                hud.ShowToast("已調查異常物件");
                UpdateObjective();
            }
        }

        // ---------------- 遭遇 → 戰鬥 ----------------

        /// <summary>立即開始遭遇（測試或除錯用；正常流程由遭遇區距離觸發）。</summary>
        public bool StartEncounter()
        {
            if (State != DemoState.Exploring || encounter.Defeated) return false;
            encounter.MarkTriggered();
            hud.HideInspect();
            hud.SetHint(null);
            hud.CursorHintVisible = false;
            var scenario = CurrentScenario();
            var enemyNames = new List<string>();
            foreach (var e in scenario.Enemies) enemyNames.Add(e.Name);
            hud.ShowEncounter("遭遇：失序體", string.Join("、", enemyNames) + "　──　進入戰鬥");

            // 記錄目前第一人稱鏡頭，作為過場起點；玩家（不可見）移到戰鬥中的站位。
            blendFromPos = exploreCam.transform.position;
            blendFromRot = exploreCam.transform.rotation;
            blendFromFov = exploreCam.fieldOfView;
            var anchor = layout.EncounterAnchor;
            float yaw = (anchor.rotation * CombatArenaView.PlayerLocalRotation).eulerAngles.y;
            player.Teleport(anchor.TransformPoint(CombatArenaView.PlayerPosition) + Vector3.up * 0.05f, yaw);

            // 戰鬥：嵌入模式的 CombatArenaView，以遭遇區為戰場原點。先建立未啟用的物件，Build 後再啟用。
            var go = new GameObject("EncounterBattle");
            go.SetActive(false);
            go.transform.SetParent(content, false);
            go.transform.position = anchor.position;
            go.transform.rotation = anchor.rotation;
            arena = go.AddComponent<CombatArenaView>();
            var options = ArenaHostOptions.Embedded();
            options.VictoryLabel = "返回探索";
            options.OnVictory = RequestReturn;
            options.DefeatLabel = "重新開始 Demo";
            options.OnDefeat = RequestRestart;
            arena.Build(playMode, scenario, seed, options);
            arena.BattleEnded += OnBattleEnded;
            ApplyBattleView();
            arena.HudVisible = false;
            arena.InteractionEnabled = false;
            go.SetActive(true);

            // 探索中的敵人換成戰鬥中的敵人（位置相同）。
            encounterGroup.gameObject.SetActive(false);
            layout.EncounterRing.gameObject.SetActive(false);
            SetCursorCaptured(false);
            State = DemoState.EnteringBattle;
            blendT = 0f;
            return true;
        }

        private void ApplyBattleView()
        {
            if (arena == null) return;
            if (battleView == BattleViewMode.FirstPerson)
            {
                var eye = CombatArenaView.PlayerPosition + Vector3.up * FirstPersonController.EyeHeight;
                int count = arena.Session.Snapshot().Enemies.Count;
                // 視線略高於敵人胸口，讓頭頂資訊留在畫面內、手牌不遮住敵人。
                var look = CombatArenaView.EnemyGroupCenter(count) + Vector3.up * 2.3f;
                arena.SetCameraPose(eye, look, FirstPersonBattleFieldOfView);
                arena.PlayerAvatarVisible = false;
            }
            else
            {
                arena.SetCameraPose(CombatArenaView.CameraPosition, CombatArenaView.CameraLookAt, CombatArenaView.DefaultFieldOfView);
                arena.PlayerAvatarVisible = true;
            }
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private void TickEnter(float dt)
        {
            blendT += dt / EnterBlendSeconds;
            var target = arena.ArenaCamera.transform;
            float s = Smooth(blendT);
            exploreCam.transform.position = Vector3.Lerp(blendFromPos, target.position, s);
            exploreCam.transform.rotation = Quaternion.Slerp(blendFromRot, target.rotation, s);
            exploreCam.fieldOfView = Mathf.Lerp(blendFromFov, arena.ArenaCamera.fieldOfView, s);
            if (blendT < 1f) return;

            // 過場結束：切到戰鬥攝影機（姿勢相同，畫面不跳），顯示戰鬥 HUD。
            arena.ArenaCamera.enabled = true;
            exploreCam.enabled = false;
            hud.HideEncounter();
            hud.Visible = false;
            arena.HudVisible = true;
            arena.InteractionEnabled = true;
            State = DemoState.Battle;
        }

        private void OnBattleEnded(BattleOutcome outcome)
        {
            LastOutcome = outcome;
            if (outcome == BattleOutcome.Victory) encounter.MarkDefeated();
        }

        /// <summary>勝利橫幅的「返回探索」。</summary>
        private void RequestReturn()
        {
            if (State == DemoState.Battle) pendingReturn = true;
        }

        /// <summary>戰敗橫幅的「重新開始 Demo」。</summary>
        private void RequestRestart()
        {
            pendingRestart = true;
        }

        private void BeginReturn()
        {
            if (arena == null) return;
            var arenaCam = arena.ArenaCamera.transform;
            blendFromPos = arenaCam.position;
            blendFromRot = arenaCam.rotation;
            blendFromFov = arena.ArenaCamera.fieldOfView;
            exploreCam.transform.position = blendFromPos;
            exploreCam.transform.rotation = blendFromRot;
            exploreCam.fieldOfView = blendFromFov;
            exploreCam.enabled = true;
            arena.ArenaCamera.enabled = false;
            arena.HudVisible = false;
            arena.InteractionEnabled = false;
            blendT = 0f;
            State = DemoState.ReturningToExplore;
        }

        private void TickReturn(float dt)
        {
            blendT += dt / ReturnBlendSeconds;
            float s = Smooth(blendT);
            exploreCam.transform.position = Vector3.Lerp(blendFromPos, player.EyePosition, s);
            exploreCam.transform.rotation = Quaternion.Slerp(blendFromRot, player.ViewRotation, s);
            exploreCam.fieldOfView = Mathf.Lerp(blendFromFov, ExploreFieldOfView, s);
            // 鏡頭接近眼睛高度時隱藏戰鬥中的玩家模型，避免穿過模型。
            if (s > 0.7f && arena != null) arena.PlayerAvatarVisible = false;
            if (blendT < 1f) return;

            if (arena != null)
            {
                arena.BattleEnded -= OnBattleEnded;
                ArenaArt.Kill(arena.gameObject);
                arena = null;
            }
            // 已擊敗：敵人不再出現，裂痕光變暗，遭遇區不再觸發。
            if (layout.RiftLight != null) layout.RiftLight.intensity = 0.9f;
            hud.Visible = true;
            hud.ShowToast("失序體已平息，可以繼續探索街區");
            UpdateObjective();
            SetCursorCaptured(true);
            SnapCameraToPlayer();
            State = DemoState.Exploring;
        }

        // ---------------- 重新開始 ----------------

        /// <summary>重新開始 Demo：重建街區、遭遇與玩家（戰敗時由橫幅按鈕呼叫）。</summary>
        public void RestartDemo()
        {
            pendingRestart = true;
            if (!playMode) Tick(0f, ExploreInput.None);
        }

        private void RestartNow()
        {
            if (arena != null)
            {
                arena.BattleEnded -= OnBattleEnded;
                ArenaArt.Kill(arena.gameObject);
                arena = null;
            }
            if (content != null)
            {
                // Play 中 Destroy 會延到影格結束；先移出並停用，避免舊碰撞體影響新玩家。
                content.gameObject.SetActive(false);
                ArenaArt.Kill(content.gameObject);
            }
            exploreCam.enabled = true;
            Restarts++;
            BuildContent();
            hud.ShowToast("Demo 已重新開始");
        }

        // ---------------- HUD 與滑鼠 ----------------

        private void UpdateObjective()
        {
            string goal;
            if (encounter != null && encounter.Defeated)
                goal = anomalyInvestigated ? "廣場已平息。可以繼續在街區探索。" : "廣場已平息。可以去西側小巷調查異常。";
            else
                goal = "沿主街往北，前往被裂痕侵蝕的廣場" + (anomalyInvestigated ? "" : "（西側小巷有異常物件）");
            hud.Objective.text = "<b>異域街區 Demo v0.1</b>\n<color=#E6BD52>目標：</color>" + goal;
        }

        public static string ViewModeName(BattleViewMode m)
        {
            return m == BattleViewMode.FirstPerson ? "第一人稱" : "第三人稱戰術鏡頭";
        }

        private void RefreshViewLabel()
        {
            if (hud == null) return;
            hud.ViewMode.text = "戰鬥視角（Demo 測試）：<b>" + ViewModeName(battleView) + "</b>　［V］切換";
        }

        private void SetCursorCaptured(bool captured)
        {
            cursorCaptured = captured;
            if (!playMode) return;
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }

        private static bool PointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        // ---------------- 氛圍 ----------------

        private bool atmosphereSaved;
        private bool savedFog;
        private Color savedFogColor;
        private FogMode savedFogMode;
        private float savedFogDensity;
        private UnityEngine.Rendering.AmbientMode savedAmbientMode;
        private Color savedAmbient;
        private Material savedSkybox;

        /// <summary>夜霧與環境光：只改執行中的設定，離開時還原（不寫入場景或專案設定）。</summary>
        public void ApplyAtmosphere()
        {
            if (!atmosphereSaved)
            {
                atmosphereSaved = true;
                savedFog = RenderSettings.fog;
                savedFogColor = RenderSettings.fogColor;
                savedFogMode = RenderSettings.fogMode;
                savedFogDensity = RenderSettings.fogDensity;
                savedAmbientMode = RenderSettings.ambientMode;
                savedAmbient = RenderSettings.ambientLight;
                savedSkybox = RenderSettings.skybox;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.026f;
            RenderSettings.fogColor = ArenaArt.FogColor;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.19f, 0.19f, 0.26f);
            RenderSettings.skybox = null;
        }

        public void RestoreAtmosphere()
        {
            if (!atmosphereSaved) return;
            atmosphereSaved = false;
            RenderSettings.fog = savedFog;
            RenderSettings.fogColor = savedFogColor;
            RenderSettings.fogMode = savedFogMode;
            RenderSettings.fogDensity = savedFogDensity;
            RenderSettings.ambientMode = savedAmbientMode;
            RenderSettings.ambientLight = savedAmbient;
            RenderSettings.skybox = savedSkybox;
        }
    }
}
