using System.Collections.Generic;
using Shepherd.Battle.Core;
using Shepherd.Battle.UI;
using Shepherd.Battle.UI.Arena;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Shepherd.Exploration.Case01
{
    public enum Case01Mode
    {
        /// <summary>第一人稱探索（含調查面板、選項）。</summary>
        Exploring,
        /// <summary>黑幕安全傳送中（不接受輸入）。</summary>
        Transition,
        /// <summary>異域反制演出（移動暫停），結束後被拉進中庭。</summary>
        Defense,
        /// <summary>遭遇 → 鏡頭過場到戰術鏡頭。</summary>
        EnteringBattle,
        /// <summary>既有的 3D 卡牌戰鬥（Combat Lab HUD）。</summary>
        Battle,
        /// <summary>勝利後鏡頭回到第一人稱。</summary>
        ReturningToExplore,
        /// <summary>QA 結束畫面（F1～F4 可重玩）。</summary>
        Ended,
        /// <summary>3D 證物檢視（移動暫停；滑鼠旋轉、滾輪縮放、E／Esc 收起）。</summary>
        Inspect,
    }

    /// <summary>
    /// 〈第十七號門〉室內 Graybox QA（v0.1）：玄關 → 餐具改寫 → 書房 → 樓梯錯接 → 夾層 → 異域反制與既有卡牌戰鬥 → 核的三段修復 → 返回玄關。
    /// 規則都在 <see cref="Case01Flow"/>；這裡只負責第一人稱移動、準星互動、房間判定、安全傳送與戰鬥銜接。
    /// 戰鬥直接嵌入既有的 <see cref="CombatArenaView"/>（與 AnomalyStreetDemo 相同的公開接口），結算完全交給 BattleController／RoundResolver。
    /// 空間全部在執行時以程式建立；場景檔只有這一個元件。
    /// </summary>
    public sealed class Case01HouseQA : MonoBehaviour
    {
        public const float EnterBlendSeconds = 1.4f;
        public const float ReturnBlendSeconds = 1.0f;
        public const float FadeOutSeconds = 0.25f;
        public const float FadeInSeconds = 0.35f;
        public const float DefenseSeconds = 2.2f;
        public const float InteractDistance = 2.6f;
        public const float ExploreFieldOfView = 70f;
        public const int TotalFlags = 13;

        [Tooltip("固定亂數種子（與 Combat Lab 相同的預設值）。")]
        [SerializeField] private int seed = CombatLabScenario.DefaultSeed;

        [Tooltip("按 Play 時的起始檢查點（QA 調整用；正常流程為 S00）。")]
        [SerializeField] private Case01Checkpoint startCheckpoint = Case01Checkpoint.S00;

        [Header("v0.3 美術／舒適度")]
        [Tooltip("降低燈光閃爍幅度（異常期間的燈光仍會變色，但起伏較小）。")]
        [SerializeField] private bool reduceFlicker;

        [Tooltip("驚嚇強度：影響效果音音量與燈光起伏。0＝最低，1＝預設。")]
        [Range(0f, 1f)] [SerializeField] private float scareIntensity = 1f;

        [Tooltip("餐廳、客廳主燈的即時陰影（低規格可關閉）。")]
        [SerializeField] private bool lightShadows = true;

        public bool ReduceFlicker { get { return reduceFlicker; } set { reduceFlicker = value; } }
        public float ScareIntensity { get { return scareIntensity; } set { scareIntensity = Mathf.Clamp01(value); } }
        public bool LightShadowsEnabled { get { return lightShadows; } set { lightShadows = value; } }

        private Case01Palette palette;
        public Case01Palette Palette { get { return palette; } }

        private bool built;
        private bool playMode;
        private CombatLabScenario scenarioOverride;
        private Transform content;
        private Case01HouseLayout layout;
        private Camera exploreCam;
        private Light skyLight;
        private Case01Hud hud;
        private FirstPersonController player;
        private Case01Flow flow;
        private Case01Room currentRoom;
        private Vector3 inspectAnchor;
        private string panelSourceId;
        private bool keepPanelOnRoomChange;
        private bool cursorCaptured;
        private float clock;

        private EncounterZone encounter;
        private Transform encounterGroup;
        private readonly List<List<Transform>> figureOrbiters = new List<List<Transform>>();
        private CombatArenaView arena;
        private float blendT;
        private Vector3 blendFromPos;
        private Quaternion blendFromRot;
        private float blendFromFov;
        private Case01Pose returnPose;
        private Case01Response wonResponse;
        private bool pendingReturn;
        private bool pendingRetry;
        private Case01Checkpoint? pendingJump;

        private Case01Pose transitionPose;
        private float fadeT;
        private bool teleported;
        private float defenseT;
        private bool lastArmed;
        private bool lastActive;

        public ICase01InputSource InputSource { get; set; }
        public Case01Mode Mode { get; private set; }
        public Case01Flow Flow { get { return flow; } }
        public Case01Session Session { get { return flow.Session; } }
        public FirstPersonController Player { get { return player; } }
        public Case01HouseLayout Layout { get { return layout; } }
        public Case01Hud Hud { get { return hud; } }
        public Camera ExploreCamera { get { return exploreCam; } }
        public CombatArenaView Arena { get { return arena; } }
        public EncounterZone Encounter { get { return encounter; } }
        public Case01Room CurrentRoom { get { return currentRoom; } }
        public bool CursorCaptured { get { return cursorCaptured; } }
        public Case01Interactable CurrentTarget { get; private set; }
        /// <summary>目前調查面板是由哪個物件開啟的。</summary>
        public string PanelSourceId { get { return panelSourceId; } }
        public BattleOutcome? LastOutcome { get; private set; }
        public int Jumps { get; private set; }
        public int Retries { get; private set; }
        public bool EncounterFiguresVisible { get { return encounterGroup != null && encounterGroup.gameObject.activeSelf; } }

        public Camera ActiveCamera
        {
            get { return arena != null && arena.ArenaCamera != null && arena.ArenaCamera.enabled ? arena.ArenaCamera : exploreCam; }
        }

        private void Awake()
        {
            Build(true);
        }

        private void OnDestroy()
        {
            if (palette != null)
            {
                palette.Dispose();
                palette = null;
            }
            if (playMode)
            {
                RestoreAtmosphere();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        /// <summary>
        /// 建立整個 QA。playMode＝false 供 EditMode 測試與截圖（不建立 EventSystem、不改 RenderSettings 與滑鼠）。
        /// scenario 供測試指定戰鬥情境；預設使用 Combat Lab 的標準兩敵情境（現有數值）。
        /// </summary>
        public void Build(bool playMode, CombatLabScenario scenario = null, int? seedOverride = null, Case01Checkpoint? start = null)
        {
            if (built) return;
            built = true;
            this.playMode = playMode;
            scenarioOverride = scenario;
            if (seedOverride.HasValue) seed = seedOverride.Value;
            if (InputSource == null) InputSource = new LegacyCase01Input();
            // 房屋座標以世界原點為準。
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;

            if (playMode && FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }

            var camGo = new GameObject("ExploreCamera");
            camGo.transform.SetParent(transform, false);
            exploreCam = camGo.AddComponent<Camera>();
            exploreCam.clearFlags = CameraClearFlags.SolidColor;
            exploreCam.backgroundColor = new Color(0.03f, 0.03f, 0.05f);
            exploreCam.fieldOfView = ExploreFieldOfView;
            exploreCam.nearClipPlane = 0.05f;
            exploreCam.farClipPlane = 120f;
            if (playMode)
            {
                camGo.tag = "MainCamera";
                camGo.AddComponent<AudioListener>();
                ApplyAtmosphere();
            }

            var skyGo = new GameObject("SoftFill");
            skyGo.transform.SetParent(transform, false);
            skyGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            skyLight = skyGo.AddComponent<Light>();
            skyLight.type = LightType.Directional;
            skyLight.color = new Color(0.62f, 0.66f, 0.82f);
            skyLight.intensity = 0.16f;
            skyLight.shadows = LightShadows.None;

            hud = new Case01Hud(transform);
            BuildContent(start ?? startCheckpoint);
        }

        private CombatLabScenario CurrentScenario()
        {
            if (scenarioOverride == null) return CombatLabScenario.Default();
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

        public static Case01Pose CheckpointPose(Case01HouseLayout l, Case01Checkpoint cp)
        {
            switch (cp)
            {
                case Case01Checkpoint.S30: return new Case01Pose(new Vector3(7.6f, Case01HouseBuilder.FloorHeight + 0.05f, 2.4f), -12f);
                case Case01Checkpoint.S40:
                case Case01Checkpoint.S50: return new Case01Pose(Case01HouseBuilder.MezzCenter + new Vector3(0f, 0.05f, -2.2f), 0f);
                default: return l.Spawn;
            }
        }

        private void BuildContent(Case01Checkpoint cp)
        {
            EndInspect();
            content = ArenaArt.Group("Case01Content", transform, Vector3.zero);
            if (palette != null) palette.Dispose();
            palette = new Case01Palette();
            layout = Case01HouseBuilder.Build(content, palette);
            flow = Case01Flow.ForCheckpoint(cp);
            var pose = CheckpointPose(layout, cp);
            player = FirstPersonController.Create(content, pose.Position, pose.Yaw);
            BuildEncounter();

            Mode = Case01Mode.Exploring;
            LastOutcome = null;
            pendingReturn = false;
            pendingRetry = false;
            wonResponse = null;
            lastArmed = flow.WrongConnectionArmed;
            lastActive = flow.WrongConnectionActive;
            hud.Visible = true;
            hud.HidePanel();
            hud.HideRadio();
            hud.HideNote();
            hud.JournalVisible = false;
            hud.EndVisible = false;
            hud.Base.HideEncounter();
            hud.Base.SetHint(null);
            hud.Fade.color = new Color(0f, 0f, 0f, 0f);
            currentRoom = layout.RoomAt(pose.Position);
            RefreshWorld();
            UpdateDecor(true);
            inspectAnchor = pose.Position;
            SetCursorCaptured(true);
            SnapCameraToPlayer();
            Physics.SyncTransforms();
            UpdateObjective();
            UpdateQaText();
        }

        /// <summary>中庭的遭遇區與看得到的失序體（與戰鬥開始時的位置、高度、朝向相同）。</summary>
        private void BuildEncounter()
        {
            if (encounterGroup != null) ArenaArt.Kill(encounterGroup.gameObject);
            var scenario = CurrentScenario();
            int count = scenario.Enemies.Count;
            var anchor = layout.EncounterAnchor;
            encounter = new EncounterZone { Id = "case01_courtyard", Center = anchor.TransformPoint(CombatArenaView.EnemyGroupCenter(count)) };
            encounterGroup = ArenaArt.Group("CourtyardEnemies", content, Vector3.zero);
            encounterGroup.position = anchor.position;
            encounterGroup.rotation = anchor.rotation;
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
                figureOrbiters.Add(orbiters);
            }
        }

        // ---------------- 每影格 ----------------

        private void Update()
        {
            if (!built) return;
            Tick(Time.deltaTime, InputSource != null ? InputSource.Read() : Case01Input.None);
        }

        public void Tick(float dt, ExploreInput input) { Tick(dt, Case01Input.From(input)); }

        /// <summary>推進一個影格（Update 呼叫；測試可直接以指定輸入呼叫）。</summary>
        public void Tick(float dt, Case01Input input)
        {
            if (pendingJump.HasValue)
            {
                var cp = pendingJump.Value;
                pendingJump = null;
                JumpNow(cp);
                return;
            }
            if (pendingRetry)
            {
                pendingRetry = false;
                RetryBattle();
                return;
            }
            if (input.Jump >= 1 && input.Jump <= 4)
            {
                JumpNow((Case01Checkpoint)(input.Jump - 1));
                return;
            }
            clock += dt;
            hud.Tick(dt);
            Animate(dt);
            switch (Mode)
            {
                case Case01Mode.Exploring: TickExploring(dt, input); break;
                case Case01Mode.Transition: TickTransition(dt); break;
                case Case01Mode.Defense: TickDefense(dt); break;
                case Case01Mode.EnteringBattle: TickEnter(dt); break;
                case Case01Mode.Battle:
                    if (pendingReturn)
                    {
                        pendingReturn = false;
                        BeginReturn();
                    }
                    break;
                case Case01Mode.ReturningToExplore: TickReturn(dt); break;
                case Case01Mode.Inspect: TickInspect(input); break;
            }
            if (!playMode && arena != null) arena.Tick(dt);
            UpdateQaText();
        }

        private void TickExploring(float dt, Case01Input input)
        {
            var b = input.Base;
            if (hud.ActiveChoice != null)
            {
                var c = hud.ActiveChoice;
                if (input.Choice >= 1 && input.Choice <= c.Options.Length)
                {
                    hud.HidePanel();
                    Handle(flow.Choose(c.Id, input.Choice - 1));
                    return;
                }
                if (b.Interact || b.ReleaseCursor) hud.HidePanel();
                hud.Base.SetHint(null);
                // 選項開啟時只能轉視角，不移動。
                player.Step(dt, new ExploreInput { Look = b.Look }, cursorCaptured);
                SnapCameraToPlayer();
                return;
            }

            if (b.ReleaseCursor) SetCursorCaptured(false);
            else if (b.Click && !cursorCaptured && !PointerOverUi()) SetCursorCaptured(true);
            if (input.Journal)
            {
                hud.JournalVisible = !hud.JournalVisible;
                if (hud.JournalVisible) hud.RefreshJournal(flow.Session.Journal, flow.Session.Evidence);
            }
            if (input.InspectEvidence && flow.FamilyPhotoCollected)
            {
                BeginInspect(Case01Texts.EvidenceId);
                return;
            }

            var prev = player.transform.position;
            player.Step(dt, b, cursorCaptured);
            SnapCameraToPlayer();
            var cur = player.transform.position;

            // 掉出場景的保險：回到玄關。
            if (cur.y < -6f)
            {
                BeginTransition(layout.Arrivals[Case01Room.Entrance]);
                return;
            }

            foreach (var p in layout.Portals)
            {
                if (!flow.PortalActive(p.Door) || !p.Crossed(prev, cur)) continue;
                hud.HidePanel();
                Handle(flow.Traverse(p.Door));
                return;
            }

            UpdateRoom();
            UpdateDecor(false);

            // 第一次再訪餐廳前，在玩家看不到餐桌時把兩副餐具改寫成一副。
            if (flow.DiningRewritePending && currentRoom != Case01Room.Dining && !TableVisible())
            {
                flow.ApplyDiningRewrite();
                RefreshWorld();
            }

            var check = EncounterCheck.None;
            if (currentRoom == Case01Room.Courtyard && flow.CanStartBattle && encounter != null)
            {
                check = encounter.Check(cur);
                if (check == EncounterCheck.Trigger)
                {
                    StartEncounter();
                    return;
                }
            }

            CurrentTarget = FindTarget();
            if (hud.PanelVisible && Vector3.Distance(inspectAnchor, cur) > 3.5f) hud.HidePanel();
            // 面板開著時：對著同一個物件（或沒對著東西）按 E 關閉；對著另一個物件按 E 直接調查它。
            bool sameAsPanel = CurrentTarget == null || CurrentTarget.Id == panelSourceId;
            if (b.Interact)
            {
                if (hud.PanelVisible && sameAsPanel) hud.HidePanel();
                else if (CurrentTarget != null && CurrentTarget.Id == Case01Ids.TeaTin)
                {
                    // 真的可以操作的物件：打開／蓋上茶葉盒。
                    var d = layout.Decor;
                    d.SetTeaTin(!d.TeaTinOpen);
                    hud.ShowNote(d.TeaTinOpen ? Case01Texts.TeaTinOpen : Case01Texts.TeaTinClose);
                }
                else if (CurrentTarget != null)
                {
                    inspectAnchor = cur;
                    panelSourceId = CurrentTarget.Id;
                    Handle(flow.Interact(CurrentTarget.Id));
                    if (Mode != Case01Mode.Exploring) return;
                }
                sameAsPanel = true;
            }

            // 面板本身已寫「［E］關閉」；日誌開著時不顯示提示。
            string hint = null;
            if (hud.JournalVisible || (hud.PanelVisible && sameAsPanel)) hint = null;
            else if (CurrentTarget != null) hint = "［E］" + CurrentTarget.Prompt;
            else if (check == EncounterCheck.Near)
                hint = "<color=#C79BFF>中庭裡有 " + encounterGroup.childCount + " 個失序體</color>　靠近即會進入戰鬥";
            hud.Base.SetHint(hint);
            hud.Base.CursorHintVisible = !cursorCaptured;
        }

        private void UpdateRoom()
        {
            var r = layout.RoomAt(player.transform.position);
            if (r == Case01Room.None || r == currentRoom) return;
            var from = currentRoom;
            currentRoom = r;
            // 走到別的房間就收起調查面板（傳送後保留，讓玩家讀完錯誤回饋）。
            if (!keepPanelOnRoomChange && hud.ActiveChoice == null) hud.HidePanel();
            keepPanelOnRoomChange = false;
            Handle(flow.EnterRoom(from, r));
            NoteEnvironmentOnEntry(r);
        }

        // ---------------- v0.2 階段異變（H0～H3） ----------------

        /// <summary>
        /// 依流程狀態套用各房間的安靜異變。玩家不在該房間時才切換（不當面硬閃）；建立／跳轉檢查點時 force＝true 直接套用。
        /// H1＝餐具改寫後（客廳、餐廳）；H2＝樓梯錯接後（書房、樓梯平台）；H3＝核穩定後大部分歸位但保留殘留。
        /// </summary>
        private void UpdateDecor(bool force)
        {
            var d = layout.Decor;
            bool st = flow.CoreStabilized;
            bool h1 = flow.Session.Has(Case01Flags.TableRewritten);
            bool h2 = flow.WrongConnectionActive;
            if ((h1 != d.LivingChanged || st != d.LivingStable) && (force || currentRoom != Case01Room.Living)) d.ApplyLiving(h1, st);
            if ((h1 != d.DiningChanged || st != d.DiningStable) && (force || currentRoom != Case01Room.Dining)) d.ApplyDining(h1, st);
            if ((h2 != d.StudyChanged || st != d.StudyStable) && (force || currentRoom != Case01Room.Study)) d.ApplyStudy(h2, st);
            if ((h2 != d.LandingChanged || st != d.LandingStable) && (force || currentRoom != Case01Room.Landing)) d.ApplyLanding(h2, st);
        }

        /// <summary>目前房屋的異變階段（0～3，取已發生的最高階段；供 QA 面板與測試）。</summary>
        public int HouseStage
        {
            get
            {
                if (flow.CoreStabilized) return 3;
                if (flow.WrongConnectionActive) return 2;
                return flow.Session.Has(Case01Flags.TableRewritten) ? 1 : 0;
            }
        }

        /// <summary>回訪已變化的房間時，把差異與原始觀察寫入日誌（每項一次，不播放音效）。</summary>
        private void NoteEnvironmentOnEntry(Case01Room r)
        {
            var d = layout.Decor;
            bool added = false;
            if (r == Case01Room.Living && d.LivingChanged && !d.LivingStable) added |= flow.NoteEnvironment("env_living");
            if (r == Case01Room.Dining && d.DiningChanged && !d.DiningStable) added |= flow.NoteEnvironment("env_dining");
            if (r == Case01Room.Study && d.StudyChanged && !d.StudyStable) added |= flow.NoteEnvironment("env_study");
            if (r == Case01Room.Landing && d.LandingChanged && !d.LandingStable) added |= flow.NoteEnvironment("env_landing");
            if ((r == Case01Room.Living || r == Case01Room.Dining) && d.LivingStable && d.DiningStable && d.LivingChanged) added |= flow.NoteEnvironment("env_after");
            if (added && hud.JournalVisible) hud.RefreshJournal(flow.Session.Journal, flow.Session.Evidence);
        }

        // ---------------- v0.2 3D 證物檢視 ----------------

        private Transform inspectObj;
        private Light inspectLight;
        private float inspectDistance = 0.5f;

        public Transform InspectObject { get { return inspectObj; } }

        private void BeginInspect(string evidenceId)
        {
            EndInspect();
            Mode = Case01Mode.Inspect;
            hud.HidePanel();
            hud.HideNote();
            hud.JournalVisible = false;
            hud.Base.SetHint(null);
            inspectDistance = 0.5f;
            var src = layout.Decor.EvidenceModel.gameObject;
            inspectObj = Instantiate(src, exploreCam.transform, false).transform;
            inspectObj.name = "Inspect_" + evidenceId;
            inspectObj.gameObject.SetActive(true);
            inspectObj.localPosition = new Vector3(0f, 0f, inspectDistance);
            inspectObj.localRotation = Quaternion.identity;
            var lg = new GameObject("InspectLight");
            lg.transform.SetParent(exploreCam.transform, false);
            lg.transform.localPosition = new Vector3(0.15f, 0.2f, 0.05f);
            inspectLight = lg.AddComponent<Light>();
            inspectLight.type = LightType.Point;
            inspectLight.range = 2f;
            inspectLight.intensity = 0.8f;
            inspectLight.color = new Color(1f, 0.95f, 0.88f);
            hud.ShowText(Case01Texts.FamilyPhotoText() + "\n\n" + Case01Texts.InspectControls);
        }

        private void TickInspect(Case01Input input)
        {
            var b = input.Base;
            SnapCameraToPlayer();
            if (b.Interact || b.ReleaseCursor)
            {
                EndInspect();
                hud.HidePanel();
                Mode = Case01Mode.Exploring;
                return;
            }
            var l = b.Look * 4f;
            inspectObj.localRotation = Quaternion.AngleAxis(-l.x, Vector3.up) * Quaternion.AngleAxis(l.y, Vector3.right) * inspectObj.localRotation;
            inspectDistance = Mathf.Clamp(inspectDistance - input.Zoom * 0.05f, 0.3f, 0.9f);
            inspectObj.localPosition = new Vector3(0f, 0f, inspectDistance);
        }

        /// <summary>旋轉檢視中的證物（截圖用，等同滑鼠位移）。</summary>
        public void RotateInspect(float yawDegrees)
        {
            if (inspectObj != null) inspectObj.localRotation = Quaternion.AngleAxis(yawDegrees, Vector3.up) * inspectObj.localRotation;
        }

        private void EndInspect()
        {
            if (inspectObj != null) ArenaArt.Kill(inspectObj.gameObject);
            if (inspectLight != null) ArenaArt.Kill(inspectLight.gameObject);
            inspectObj = null;
            inspectLight = null;
        }

        /// <summary>玩家視線內是否看得到餐桌（用於安全的環境改寫）。</summary>
        public bool TableVisible()
        {
            var eye = exploreCam.transform.position;
            var to = layout.TablePoint - eye;
            if (Vector3.Angle(exploreCam.transform.forward, to) > 60f) return false;
            return !Physics.Linecast(eye, layout.TablePoint, ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>準星正前方、互動距離內最近的可調查物件（被牆或家具擋住則沒有）。</summary>
        private Case01Interactable FindTarget()
        {
            var hits = Physics.RaycastAll(exploreCam.transform.position, exploreCam.transform.forward, InteractDistance, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            foreach (var h in hits)
            {
                if (h.collider == player.Body) continue;
                var it = h.collider.GetComponent<Case01Interactable>();
                if (it != null) return it;
                if (!h.collider.isTrigger) return null;
            }
            return null;
        }

        /// <summary>處理一次規則結果：文字、字幕、選項、外觀刷新與傳送／演出。</summary>
        private void Handle(Case01Response r)
        {
            if (r == null) return;
            if (r.WorldChanged) RefreshWorld();
            if (r.Toast != null) hud.Base.ShowToast(r.Toast);
            if (r.Radio != null) hud.ShowRadio(r.Radio);
            if (r.Choice != null) hud.ShowChoice(r.Choice);
            else if (r.Text != null) hud.ShowText(r.Text);
            if (r.Note != null) hud.ShowNote(r.Note);
            if (hud.JournalVisible) hud.RefreshJournal(flow.Session.Journal, flow.Session.Evidence);
            UpdateObjective();
            if (r.Restart)
            {
                pendingJump = Case01Checkpoint.S00;
                return;
            }
            if (r.Exit)
            {
                EndQa();
                return;
            }
            if (r.Inspect != null)
            {
                BeginInspect(r.Inspect);
                return;
            }
            if (r.BeginDefense)
            {
                BeginDefense();
                return;
            }
            if (r.PullToCourtyard)
            {
                BeginTransition(layout.Arrivals[Case01Room.Courtyard]);
                return;
            }
            if (r.Redirect.HasValue) BeginTransition(layout.Arrivals[r.Redirect.Value]);
        }

        // ---------------- 安全傳送 ----------------

        private void BeginTransition(Case01Pose pose)
        {
            transitionPose = pose;
            fadeT = 0f;
            teleported = false;
            Mode = Case01Mode.Transition;
            hud.Base.SetHint(null);
        }

        private void TickTransition(float dt)
        {
            fadeT += dt;
            if (!teleported)
            {
                SetFade(fadeT / FadeOutSeconds);
                if (fadeT < FadeOutSeconds) return;
                teleported = true;
                fadeT = 0f;
                TeleportPlayer(transitionPose);
                return;
            }
            SetFade(1f - fadeT / FadeInSeconds);
            if (fadeT < FadeInSeconds) return;
            SetFade(0f);
            Mode = Case01Mode.Exploring;
        }

        private void SetFade(float a)
        {
            hud.Fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(a));
        }

        private void TeleportPlayer(Case01Pose pose)
        {
            player.Teleport(pose.Position, pose.Yaw);
            SnapCameraToPlayer();
            inspectAnchor = pose.Position;
            keepPanelOnRoomChange = true;
            UpdateRoom();
            keepPanelOnRoomChange = false;
        }

        // ---------------- 異域反制 ----------------

        private void BeginDefense()
        {
            Mode = Case01Mode.Defense;
            defenseT = 0f;
            hud.Base.SetHint(null);
            if (layout.Creak != null && layout.Creak.isActiveAndEnabled)
            {
                layout.Creak.pitch = 0.6f;
                layout.Creak.Play();
            }
            RefreshWorld();
        }

        private void TickDefense(float dt)
        {
            defenseT += dt;
            // 鏡頭輕微晃動，移動暫停。
            SnapCameraToPlayer();
            float k = Mathf.Clamp01(defenseT / DefenseSeconds);
            exploreCam.transform.position += new Vector3(Mathf.Sin(clock * 37f), Mathf.Sin(clock * 29f), 0f) * 0.03f * k;
            if (defenseT < DefenseSeconds) return;
            hud.HidePanel();
            BuildEncounter();
            RefreshWorld();
            BeginTransition(layout.Arrivals[Case01Room.Courtyard]);
        }

        // ---------------- 遭遇 → 戰鬥（既有 CombatArenaView 嵌入接口） ----------------

        /// <summary>立即開始中庭遭遇（測試或除錯用；正常流程由遭遇區距離觸發）。</summary>
        public bool StartEncounter()
        {
            if (Mode != Case01Mode.Exploring || !flow.CanStartBattle || encounter == null || encounter.Defeated) return false;
            encounter.MarkTriggered();
            flow.OnBattleStarted();
            hud.HidePanel();
            hud.JournalVisible = false;
            hud.Base.SetHint(null);
            hud.Base.CursorHintVisible = false;
            var scenario = CurrentScenario();
            var names = new List<string>();
            foreach (var e in scenario.Enemies) names.Add(e.Name);
            hud.Base.ShowEncounter("遭遇：失序體", string.Join("、", names.ToArray()) + "　──　進入戰鬥");

            returnPose = new Case01Pose(player.transform.position, player.Yaw);
            blendFromPos = exploreCam.transform.position;
            blendFromRot = exploreCam.transform.rotation;
            blendFromFov = exploreCam.fieldOfView;
            var anchor = layout.EncounterAnchor;
            float yaw = (anchor.rotation * CombatArenaView.PlayerLocalRotation).eulerAngles.y;
            player.Teleport(anchor.TransformPoint(CombatArenaView.PlayerPosition) + Vector3.up * 0.05f, yaw);

            var go = new GameObject("Case01Battle");
            go.SetActive(false);
            go.transform.SetParent(content, false);
            go.transform.position = anchor.position;
            go.transform.rotation = anchor.rotation;
            arena = go.AddComponent<CombatArenaView>();
            var options = ArenaHostOptions.Embedded();
            options.VictoryLabel = "返回中庭";
            options.OnVictory = () => { if (Mode == Case01Mode.Battle) pendingReturn = true; };
            options.DefeatLabel = "返回檢查點重試";
            options.OnDefeat = () => { pendingRetry = true; };
            arena.Build(playMode, scenario, seed, options);
            arena.BattleEnded += OnBattleEnded;
            arena.SetCameraPose(CombatArenaView.CameraPosition, CombatArenaView.CameraLookAt, CombatArenaView.DefaultFieldOfView);
            arena.HudVisible = false;
            arena.InteractionEnabled = false;
            go.SetActive(true);

            encounterGroup.gameObject.SetActive(false);
            SetCursorCaptured(false);
            Mode = Case01Mode.EnteringBattle;
            blendT = 0f;
            UpdateObjective();
            return true;
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
            arena.ArenaCamera.enabled = true;
            exploreCam.enabled = false;
            hud.Base.HideEncounter();
            hud.Visible = false;
            arena.HudVisible = true;
            arena.InteractionEnabled = true;
            Mode = Case01Mode.Battle;
        }

        private void OnBattleEnded(BattleOutcome outcome)
        {
            LastOutcome = outcome;
            if (outcome == BattleOutcome.Victory)
            {
                encounter.MarkDefeated();
                wonResponse = flow.OnBattleWon();
            }
            else if (outcome == BattleOutcome.Defeat)
            {
                flow.OnBattleLost();
            }
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
            // 回到遭遇前的探索位置。
            player.Teleport(returnPose.Position, returnPose.Yaw);
            blendT = 0f;
            Mode = Case01Mode.ReturningToExplore;
        }

        private void TickReturn(float dt)
        {
            blendT += dt / ReturnBlendSeconds;
            float s = Smooth(blendT);
            exploreCam.transform.position = Vector3.Lerp(blendFromPos, player.EyePosition, s);
            exploreCam.transform.rotation = Quaternion.Slerp(blendFromRot, player.ViewRotation, s);
            exploreCam.fieldOfView = Mathf.Lerp(blendFromFov, ExploreFieldOfView, s);
            if (s > 0.7f && arena != null) arena.PlayerAvatarVisible = false;
            if (blendT < 1f) return;
            KillArena();
            hud.Visible = true;
            SetCursorCaptured(true);
            SnapCameraToPlayer();
            Mode = Case01Mode.Exploring;
            var r = wonResponse;
            wonResponse = null;
            Handle(r);
        }

        /// <summary>戰敗橫幅「返回檢查點重試」：回到中庭入口，失序體重新聚集，可再戰。</summary>
        private void RetryBattle()
        {
            KillArena();
            exploreCam.enabled = true;
            hud.Visible = true;
            hud.Base.HideEncounter();
            Retries++;
            BuildEncounter();
            RefreshWorld();
            TeleportPlayer(layout.Arrivals[Case01Room.Courtyard]);
            SetCursorCaptured(true);
            Mode = Case01Mode.Exploring;
            LastOutcome = null;
            hud.Base.ShowToast("回到中庭入口。失序體重新聚集──可以再試一次。");
            UpdateObjective();
        }

        private void KillArena()
        {
            if (arena == null) return;
            arena.BattleEnded -= OnBattleEnded;
            arena.gameObject.SetActive(false);
            ArenaArt.Kill(arena.gameObject);
            arena = null;
        }

        // ---------------- QA 跳轉與結束 ----------------

        /// <summary>跳到檢查點（F1～F4）。重建整個空間與流程狀態。</summary>
        public void JumpTo(Case01Checkpoint cp)
        {
            pendingJump = cp;
            if (!playMode) Tick(0f, Case01Input.None);
        }

        private void JumpNow(Case01Checkpoint cp)
        {
            KillArena();
            if (content != null)
            {
                content.gameObject.SetActive(false);
                ArenaArt.Kill(content.gameObject);
            }
            encounterGroup = null;
            exploreCam.enabled = true;
            Jumps++;
            BuildContent(cp);
            hud.Base.ShowToast("QA：跳到 " + CheckpointName(cp));
        }

        public static string CheckpointName(Case01Checkpoint cp)
        {
            switch (cp)
            {
                case Case01Checkpoint.S30: return "S30 樓梯錯接";
                case Case01Checkpoint.S40: return "S40 夾層（反制前）";
                case Case01Checkpoint.S50: return "S50 戰後修復";
                default: return "S00 玄關";
            }
        }

        private void EndQa()
        {
            Mode = Case01Mode.Ended;
            hud.HidePanel();
            hud.JournalVisible = false;
            hud.Base.SetHint(null);
            hud.ShowEnd(Case01Texts.EndTitle, Case01Texts.EndBody);
            SetCursorCaptured(false);
            UpdateObjective();
        }

        // ---------------- 外觀 ----------------

        /// <summary>依流程狀態刷新房屋外觀（餐具、門、封口、核、燈光、失序體）。</summary>
        private void RefreshWorld()
        {
            var L = layout;
            L.DiningSecondSet.SetActive(flow.DiningShowsTwoSets);
            L.DiningTrace.SetActive(!flow.DiningShowsTwoSets);
            L.PhotoSecondFigure.sharedMaterial = flow.CoreStabilized ? L.PhotoFadedMat : L.PhotoFigureMat;
            L.ThirdDoorFrame.SetActive(flow.ThirdDoorVisible);
            L.Interactables[Case01Ids.ThirdDoor].gameObject.SetActive(flow.ThirdDoorVisible);
            foreach (var p in L.Portals)
            {
                bool active = flow.PortalActive(p.Door);
                if (p.Seal != null) p.Seal.SetActive(!active);
                p.Membrane.SetActive(active);
            }
            L.StudyMarkLabel.text = flow.StairRouteRestored ? "△\n<size=20>歸位</size>" : "△";
            L.FrontDoorLabel.text = flow.CoreStabilized ? "離開十七號" : "QA 返回／重來";
            L.Interactables[Case01Ids.FrontDoor].Prompt = flow.CoreStabilized ? "前門（離開十七號）" : "前門（QA 返回／重來）";
            L.Door17Frame.SetActive(flow.Door17Open);
            L.TearFrame.SetActive(flow.CourtyardTearOpen);
            // v0.2：FP-001 只在核穩定後、尚未拾取時出現；各房間的安靜異變。
            L.Decor.SetEvidenceVisible(flow.FamilyPhotoAvailable);
            UpdateDecor(false);
            hud.Base.Controls.text = "WASD 移動｜滑鼠 視角｜Shift 奔跑｜E 調查／互動｜1–3 選擇｜J 日誌" +
                                     (flow.FamilyPhotoCollected ? "｜I 檢視證物" : "") + "｜Esc 釋放滑鼠";

            Color coreColor = flow.CoreStabilized ? ArenaArt.LampWarm : flow.DefenseActive ? new Color(1f, 0.25f, 0.35f) : ArenaArt.Rift;
            ArenaArt.SetEmission(L.CoreMat, coreColor * (flow.CoreStabilized ? 1.2f : 2.2f));
            L.CoreLight.color = coreColor;
            var mezzColor = flow.CoreStabilized ? new Color(1f, 0.85f, 0.65f) : flow.DefenseActive ? new Color(0.95f, 0.4f, 0.5f) : new Color(0.6f, 0.55f, 0.85f);
            foreach (var l in L.MezzLights) l.color = mezzColor;
            foreach (var m in L.MezzLineMats) ArenaArt.SetEmission(m, (flow.CoreStabilized ? ArenaArt.LampWarm : ArenaArt.Rift) * 1.8f);
            if (L.CourtyardLight != null) L.CourtyardLight.intensity = flow.CourtyardTearOpen ? 0.9f : 2.2f;
            if (encounterGroup != null) encounterGroup.gameObject.SetActive(flow.CanStartBattle && encounter != null && !encounter.Defeated);

            // 樓梯平台的聲音提示（線索足夠時、門錯接時各一次）。
            if ((flow.WrongConnectionArmed && !lastArmed) || (flow.WrongConnectionActive && !lastActive))
            {
                if (L.Creak != null && L.Creak.isActiveAndEnabled)
                {
                    L.Creak.pitch = 1f;
                    L.Creak.Play();
                }
            }
            lastArmed = flow.WrongConnectionArmed;
            lastActive = flow.WrongConnectionActive;
        }

        private void Animate(float dt)
        {
            if (layout == null) return;
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
                    if (list[k] == null) continue;
                    float a = clock * (0.9f + k * 0.15f) + k * 1.3f + f;
                    list[k].localPosition = new Vector3(Mathf.Cos(a) * 0.75f, 1.2f + Mathf.Sin(a * 1.7f) * 0.6f, Mathf.Sin(a) * 0.55f);
                }
            }
            bool stable = flow.CoreStabilized;
            layout.Core.Rotate(0f, (stable ? 6f : flow.DefenseActive ? 60f : 22f) * dt, 0f, Space.World);
            for (int i = 0; i < layout.Overlaps.Count; i++)
            {
                var target = stable ? Quaternion.identity : layout.OverlapSkew[i];
                if (Mode == Case01Mode.Defense) target *= Quaternion.Euler(Mathf.Sin(clock * 9f + i) * 8f, Mathf.Sin(clock * 7f + i) * 12f, 0f);
                layout.Overlaps[i].localRotation = Quaternion.Slerp(layout.Overlaps[i].localRotation, target, 1f - Mathf.Exp(-dt * 3f));
            }
            UpdateLights();
        }

        // ---------------- v0.3 燈光（依房間基準色計算，不累積漂移；低頻、可降低閃爍） ----------------

        /// <summary>閃爍強度：關閉「降低閃爍」時 1，開啟時 0.25；再乘上驚嚇強度。</summary>
        public float FlickerScale { get { return (reduceFlicker ? 0.25f : 1f) * Mathf.Lerp(0.3f, 1f, scareIntensity); } }

        private void UpdateLights()
        {
            var L = layout;
            var d = L.Decor;
            float flick = FlickerScale;
            foreach (var kv in L.RoomLights)
            {
                var room = kv.Key;
                var light = kv.Value;
                var c = L.RoomLightColor[room];
                float i = L.RoomLightIntensity[room];
                switch (room)
                {
                    case Case01Room.Dining:
                        // H1：光色略偏、亮度慢慢起伏（週期約 10 秒）。
                        if (d.DiningChanged && !d.DiningStable)
                        {
                            c = Color.Lerp(c, new Color(0.92f, 0.86f, 0.66f), 0.45f);
                            i *= 0.82f + 0.06f * flick * Mathf.Sin(clock * 0.6f);
                        }
                        break;
                    case Case01Room.Living:
                        if (d.LivingChanged && !d.LivingStable) i *= 0.88f;
                        break;
                    case Case01Room.Study:
                        // H2：書房轉冷、變暗，但線索仍看得清楚。
                        if (d.StudyChanged && !d.StudyStable)
                        {
                            c = Color.Lerp(c, new Color(0.72f, 0.78f, 1f), 0.6f);
                            i *= 0.82f;
                        }
                        break;
                    case Case01Room.Landing:
                        bool uneasy = ((flow.WrongConnectionArmed || flow.WrongConnectionActive) && !flow.StairRouteRestored) || flow.DefenseActive;
                        if (uneasy)
                        {
                            c = Color.Lerp(c, ArenaArt.Rift, 0.5f);
                            i = 0.8f + 0.3f * flick * (Mathf.PerlinNoise(clock * 1.6f, 0.3f) - 0.5f);
                        }
                        break;
                }
                // 反制後、修復前：整屋略冷（不是全屋藍紫）。
                if (flow.DefenseActive && room != Case01Room.Landing) c = Color.Lerp(c, new Color(0.78f, 0.76f, 0.95f), 0.4f);
                light.color = c;
                light.intensity = i;
                light.shadows = lightShadows && light.shadowStrength > 0f && (room == Case01Room.Dining || room == Case01Room.Living) ? LightShadows.Soft : LightShadows.None;
            }
        }

        private void SnapCameraToPlayer()
        {
            exploreCam.transform.position = player.EyePosition;
            exploreCam.transform.rotation = player.ViewRotation;
            exploreCam.fieldOfView = ExploreFieldOfView;
        }

        // ---------------- HUD ----------------

        private void UpdateObjective()
        {
            hud.Base.Objective.text = "<b>〈第十七號門〉室內 Graybox QA v0.1</b>\n<color=#E6BD52>目標：</color>" + ObjectiveText();
        }

        public string ObjectiveText()
        {
            var s = flow.Session;
            if (Mode == Case01Mode.Ended) return "QA 結束。F1 可從玄關重玩。";
            if (!s.Has(Case01Flags.TableTwo)) return "先在一樓走走。餐廳在玄關北側。";
            if (!s.HasJournal("table_rewritten") && !s.Has(Case01Flags.EllieStudyKnown))
                return "離開餐廳，到別的房間走一圈再回來看看。（二樓：樓梯在玄關東側）";
            if (!s.Has(Case01Flags.EllieStudyKnown) || !s.Has(Case01Flags.OldCorrectionRecord)) return "上二樓，調查書房裡的資料。（樓梯在玄關東側）";
            if (!flow.WrongConnectionActive) return "離開書房，到樓梯平台看看。";
            if (!s.Has(Case01Flags.RouteMismatchSeen)) return "從樓梯平台走進書房門，親自確認它通往哪裡。";
            if (!flow.StairRouteRestored) return "回到樓梯平台，依艾琳的記號覆寫書房門框（E）。";
            if (s.Stage < Case01Stage.MezzanineDiscovered) return "走進樓梯平台外牆上打開的第三扇門。";
            if (!flow.DefenseTriggered) return "調查夾層中央的核。";
            if (!s.Has(Case01Flags.BattleWon)) return "擊退異常中庭裡的失序體。";
            string back = currentRoom == Case01Room.Mezzanine || currentRoom == Case01Room.Courtyard ? "" : "（從樓梯平台的第三扇門回夾層）";
            if (currentRoom == Case01Room.Courtyard && !s.Has(Case01Flags.MissingLinkRestored)) return "穿過南側的裂口回到夾層，再調查核。";
            if (!s.Has(Case01Flags.MissingLinkRestored))
            {
                if (!s.Has(Case01Flags.MapObserved) || !s.Has(Case01Flags.PhotoObserved)) return "回到核。若資料不足，先補看舊地圖與合照。" + back;
                return "回到核，補回被省略的 17。" + back;
            }
            if (!flow.SwapConfirmed && !s.Has(Case01Flags.RouteAnchorSet)) return "走進 △ 或 ○ 門，確認它們現在通往哪裡，再回到核。" + back;
            if (!s.Has(Case01Flags.RouteAnchorSet)) return "回到核，依艾琳的記號選擇錨點。" + back;
            if (!flow.TriangleVerified || !flow.Door17Verified)
                return "走一次 △ 門" + (flow.Door17Verified ? "" : "與 17 號門") + "確認關係，再回到核。" + back;
            if (!flow.CoreStabilized) return "回到核，補上第一次校正的缺口。" + back;
            if (!flow.FamilyPhotoCollected) return "夾層裡多了一張照片（可選）。巡查完再從玄關前門離開十七號。";
            return "可以再巡查一下房子，然後從玄關前門離開十七號。";
        }

        private void UpdateQaText()
        {
            int n = 0;
            foreach (var f in Case01Flags.All) if (flow.Session.Has(f)) n++;
            hud.QaText.text = "<b>QA</b>　" + Case01Texts.StageName(flow.Session.Stage) + "｜旗標 " + n + "/" + TotalFlags +
                              "\n位置：" + Case01Texts.RoomName(currentRoom) + "｜房屋 H" + HouseStage + "｜日誌 " + flow.Session.Journal.Count +
                              "｜證物 " + flow.Session.Evidence.Count;
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

        // ---------------- 氛圍（只改執行中的設定，離開時還原） ----------------

        private bool atmosphereSaved;
        private bool savedFog;
        private UnityEngine.Rendering.AmbientMode savedAmbientMode;
        private Color savedAmbient;
        private Material savedSkybox;

        public void ApplyAtmosphere()
        {
            if (!atmosphereSaved)
            {
                atmosphereSaved = true;
                savedFog = RenderSettings.fog;
                savedAmbientMode = RenderSettings.ambientMode;
                savedAmbient = RenderSettings.ambientLight;
                savedSkybox = RenderSettings.skybox;
                savedSky = RenderSettings.ambientSkyColor;
                savedEquator = RenderSettings.ambientEquatorColor;
                savedGround = RenderSettings.ambientGroundColor;
            }
            RenderSettings.fog = false;
            // v0.3：三色環境光（上方略亮、地面較暗），讓房間有方向對比，不再是平均的灰光。
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.27f, 0.26f, 0.29f);
            RenderSettings.ambientEquatorColor = new Color(0.2f, 0.18f, 0.17f);
            RenderSettings.ambientGroundColor = new Color(0.09f, 0.08f, 0.075f);
            RenderSettings.ambientLight = new Color(0.30f, 0.29f, 0.32f);
            RenderSettings.skybox = null;
        }

        public void RestoreAtmosphere()
        {
            if (!atmosphereSaved) return;
            atmosphereSaved = false;
            RenderSettings.fog = savedFog;
            RenderSettings.ambientMode = savedAmbientMode;
            RenderSettings.ambientLight = savedAmbient;
            RenderSettings.skybox = savedSkybox;
            RenderSettings.ambientSkyColor = savedSky;
            RenderSettings.ambientEquatorColor = savedEquator;
            RenderSettings.ambientGroundColor = savedGround;
        }

        private Color savedSky, savedEquator, savedGround;
    }
}
