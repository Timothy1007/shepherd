using System.Collections.Generic;
using Shepherd.Battle.Core;
using Shepherd.Battle.UI;
using Shepherd.Battle.UI.Arena;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        /// <summary>失序體由環境碎片凝聚成形（移動暫停，可轉視角；E 跳過）。</summary>
        Manifesting,
    }

    /// <summary>QA 啟動模式：一般流程、美術 QA（自由切 H0～H3、預覽形成演出、不開戰）、核校正 v0.3。</summary>
    public enum Case01QaMode
    {
        Standard,
        ArtPreview,
        CoreV03,
        /// <summary>v0.4 空間敘事 QA：F6 H0～H3、F7 下一句自語預覽（之後的階段加上失序門與記憶殘響）。</summary>
        NarrativeV04,
        /// <summary>v0.5 Portal QA：F6 切失序門狀態、F7 切 v0.4／v0.5 外觀、F8 降低閃爍（站在同一扇門前比較）。</summary>
        PortalV05,
        /// <summary>v0.6 客廳美術標準間 QA：方案 A、直接站在客廳入口；F6 H0／H2／H3、F7 新舊美術切換。</summary>
        LivingArt,
        /// <summary>v0.5 Demo 切片中的正式遊玩：沒有 QA 快捷鍵與 QA 狀態列；前門回到街區（由 Case01DemoSlice 接手）。</summary>
        Demo,
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

        [Header("v0.3 音量（Case01 區域）")]
        [Range(0f, 1f)] [SerializeField] private float ambientVolume = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float effectsVolume = 0.8f;
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.7f;

        [Tooltip("QA 模式：一般／美術 QA／核校正 v0.3。選單啟動時會自動設定。")]
        [SerializeField] private Case01QaMode qaMode = Case01QaMode.Standard;

        /// <summary>選單「美術 QA」「核校正 v0.3 QA」進入 Play 前寫入、Awake 讀取後刪除的一次性啟動參數。</summary>
        public const string LaunchModeKey = "Shepherd.Case01.LaunchMode";

        public Case01QaMode QaMode { get { return qaMode; } }

        [Tooltip("v0.4 一樓格局（A/B 實驗）。正式流程維持 Original；「一樓反向樓梯 v0.4 QA」選單使用 V04。")]
        [SerializeField] private Case01Floorplan floorplan = Case01Floorplan.Original;
        private bool floorplanQa;
        private bool residentialQa;
        /// <summary>由「住宅 v0.5 A/B QA」啟動（F9 依序切 v0.5 A／B／原版／v0.4 B）。</summary>
        public bool ResidentialQa { get { return residentialQa; } }

        public Case01Floorplan Floorplan { get { return floorplan; } }

        [Tooltip("v0.5 失序門外觀：V05＝彩色訊號破損＋流動邊界（預設）；V04＝舊版單色雪花。只改外觀，不動路由。")]
        [SerializeField] private Case01PortalStyle portalStyle = Case01PortalStyle.V05;
        public Case01PortalStyle PortalStyle { get { return portalStyle; } set { portalStyle = value; } }
        /// <summary>由「一樓反向樓梯 v0.4 QA」啟動（F9 可切換新舊格局比較）。</summary>
        public bool FloorplanQa { get { return floorplanQa; } }
        /// <summary>核修復玩法：一般／美術 QA 使用 v0.2；核校正 v0.3 QA 使用新規則。</summary>
        public Case01CorrectionMode Correction { get { return qaMode == Case01QaMode.CoreV03 ? Case01CorrectionMode.RevisedV03 : Case01CorrectionMode.LegacyV02; } }
        public Case01Audio Audio { get { return sfx; } }
        public Case01Manifestation Manifestation { get { return manifestation; } }
        public bool Manifested { get { return manifested; } }
        public int ArtStage { get; private set; }

        private Case01Audio sfx;
        private Case01EchoPlayer echo;
        /// <summary>v0.4 記憶殘響的演出（測試用）。</summary>
        public Case01EchoPlayer Echo { get { return echo; } }

        [Tooltip("v0.4 記憶殘響（占位低語）的音量。")]
        [Range(0f, 1f)] [SerializeField] private float echoVolume = 0.5f;
        public float EchoVolume { get { return echoVolume; } set { echoVolume = Mathf.Clamp01(value); } }
        private Case01Manifestation manifestation;
        private bool manifested;

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
        /// <summary>目前戰鬥使用的 Demo 戰鬥 HUD（沒有戰鬥時為 null）。</summary>
        public CombatDemoHud DemoHud { get; private set; }
        /// <summary>是否啟用首戰教學（QA 可關閉）。</summary>
        public bool TutorialEnabled = true;
        /// <summary>首戰教學已出現過（存在戰鬥規則與快照之外）。</summary>
        public bool TutorialShown { get; set; }
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
            var launch = PlayerPrefs.GetString(LaunchModeKey, "");
            if (launch.Length > 0)
            {
                PlayerPrefs.DeleteKey(LaunchModeKey);
                if (launch == "art") qaMode = Case01QaMode.ArtPreview;
                else if (launch == "v03") qaMode = Case01QaMode.CoreV03;
                else if (launch == "v04") qaMode = Case01QaMode.NarrativeV04;
                else if (launch == "portal") qaMode = Case01QaMode.PortalV05;
                else if (launch == "living") qaMode = Case01QaMode.LivingArt;
                else if (launch == "res")
                {
                    floorplan = Case01Floorplan.V05A;
                    floorplanQa = true;
                    residentialQa = true;
                }
                else if (launch == "floor")
                {
                    floorplan = Case01Floorplan.V04;
                    floorplanQa = true;
                }
            }
            Build(true);
        }

        private void OnDestroy()
        {
            if (palette != null)
            {
                palette.Dispose();
                palette = null;
            }
            if (sfx != null) sfx.Dispose();
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
        public void Build(bool playMode, CombatLabScenario scenario = null, int? seedOverride = null, Case01Checkpoint? start = null, Case01QaMode? mode = null,
            Case01Floorplan? plan = null)
        {
            if (built) return;
            built = true;
            if (plan.HasValue)
            {
                floorplan = plan.Value;
                floorplanQa = plan.Value != Case01Floorplan.Original;
                residentialQa = Case01HouseBuilder.IsV05(plan.Value);
            }
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
            // v0.5：窗外夜空（深藍，不是 Unity 的空白背景）。
            exploreCam.backgroundColor = new Color(0.045f, 0.055f, 0.095f);
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

            if (mode.HasValue) qaMode = mode.Value;
            // 客廳美術標準間 QA 只用方案 A。
            if (qaMode == Case01QaMode.LivingArt) floorplan = Case01Floorplan.V05A;
            hud = new Case01Hud(transform);
            sfx = new Case01Audio(transform);
            echo = new Case01EchoPlayer(hud, sfx);
            ApplyVolumes();
            if (qaMode == Case01QaMode.Demo && demoFlow != null) BuildContent(demoFlow, Case01Checkpoint.S00, null);
            else BuildContent(start ?? (qaMode == Case01QaMode.CoreV03 ? Case01Checkpoint.S50 : startCheckpoint));
            // 剛進門的第一句自語（只在 QA 啟動時；F1～F5 跳轉不重播）。
            Handle(flow.OnArrive());
            // Portal v0.5 QA：直接站到錯接的書房門前。
            if (qaMode == Case01QaMode.PortalV05) PreviewGlitch(0);
        }

        // ---------------- v0.5 Demo 切片 ----------------

        private Case01Flow demoFlow;

        /// <summary>Demo：玩家在前門選擇離開（回到街區）。流程物件不變，由 Case01RunState 保存。</summary>
        public event System.Action DemoExitRequested;

        /// <summary>Demo：在 Build 之前指定要沿用的屋內流程（第一次進屋或再次進屋都從玄關開始）。</summary>
        public void ConfigureDemo(Case01Flow flow, Case01Floorplan plan)
        {
            demoFlow = flow;
            floorplan = plan;
            qaMode = Case01QaMode.Demo;
        }

        public bool IsDemo { get { return qaMode == Case01QaMode.Demo; } }

        /// <summary>Phase 1.6：每次建好房屋（含重建、檢查點跳轉）後、建立玩家之前呼叫；只有空間結構灰盒 QA 會設定。</summary>
        public System.Action<Case01HouseLayout> LayoutPostBuild;

        private string demoBanner;
        /// <summary>Demo 模式左下第二行（正式 Demo 為空；Phase 1 品質回歸 QA 用來顯示快捷鍵）。</summary>
        public string DemoBanner
        {
            get { return demoBanner; }
            set
            {
                demoBanner = value;
                if (hud != null && qaMode == Case01QaMode.Demo) hud.Base.ViewMode.text = value ?? "";
            }
        }

        /// <summary>Demo（方案 A）屋內的 v0.6／v0.7 美術新舊切換（品質回歸 QA 的 F7）；重建屋內時沿用。</summary>
        public void SetDemoArt(bool on)
        {
            livingArtOn = on;
            if (layout == null) return;
            if (layout.LivingArt != null) layout.LivingArt.SetActive(on);
            if (layout.HouseArt != null) layout.HouseArt.SetActive(on);
            Physics.SyncTransforms();
        }

        /// <summary>品質回歸 QA：以指定流程狀態重建屋內並站到指定視點（視角不變的同角度檢查用）。</summary>
        public void QaView(Case01Flow f, Case01Pose pose, float pitch)
        {
            KillArena();
            if (content != null)
            {
                content.gameObject.SetActive(false);
                ArenaArt.Kill(content.gameObject);
            }
            encounterGroup = null;
            exploreCam.enabled = true;
            BuildContent(f, Case01Checkpoint.S00, pose);
            player.Teleport(pose.Position, pose.Yaw, pitch);
            SnapCameraToPlayer();
            if (echo != null) echo.Stop();
            hud.ClearMonologue();
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

        /// <summary>客廳美術 QA 的起點：玄關→客廳開口內側，面向壁爐與沙發。</summary>
        public static readonly Case01Pose LivingEntrancePose = new Case01Pose(new Vector3(4.75f, 0.05f, 1.35f), -78f);

        private bool livingArtOn = true;
        /// <summary>客廳美術 QA：目前是否顯示新美術（F7 切換）。</summary>
        public bool LivingArtOn { get { return layout != null && layout.LivingArt != null && layout.LivingArt.Active; } }

        public static Case01Pose CheckpointPose(Case01HouseLayout l, Case01Checkpoint cp)
        {
            if (cp == Case01Checkpoint.S00 && l.StartInLiving) return LivingEntrancePose;
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
            BuildContent(Case01Flow.ForCheckpoint(cp, Correction), cp, null);
        }

        private void BuildContent(Case01Flow f, Case01Checkpoint cp, Case01Pose? poseOverride)
        {
            if (echo != null) echo.Stop();
            EndInspect();
            manifestation = null;
            manifested = false;
            pendingCommit = null;
            snapPieces.Clear();
            snapFrom.Clear();
            snapTo.Clear();
            snapRotFrom.Clear();
            snapLight = null;
            snapT = -1f;
            if (sfx != null) sfx.StopAll();
            content = ArenaArt.Group("Case01Content", transform, Vector3.zero);
            if (palette != null) palette.Dispose();
            palette = new Case01Palette();
            ResetMood();
            layout = Case01HouseBuilder.Build(content, palette, floorplan);
            // Phase 1.6：空間結構灰盒 QA 在這裡套用 A／B／C 方案（其他 QA 與 Demo 沒有設定，不受影響）。
            if (LayoutPostBuild != null) LayoutPostBuild(layout);
            // v0.6：客廳美術標準間（只在客廳美術 QA 建立；其他 QA 與 Demo 不受影響）。
            // v0.7：Demo（方案 A）也套用客廳 v0.6 與其他房間的 Phase 1 美術；其他 QA 維持原樣。
            if ((qaMode == Case01QaMode.LivingArt || qaMode == Case01QaMode.Demo) && floorplan == Case01Floorplan.V05A)
            {
                layout.StartInLiving = qaMode == Case01QaMode.LivingArt;
                var art = Case01LivingArt.Build(layout, palette);
                if (art != null) art.SetActive(livingArtOn);
                var house = Case01HouseArt.Build(layout, palette);
                if (house != null) house.SetActive(livingArtOn);
            }
            BuildPortalHums();
            flow = f;
            flow.PhotoPlace = layout.PhotoPlace;
            var pose = poseOverride ?? CheckpointPose(layout, cp);
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
            hud.ClearMonologue();
            hud.HideNote();
            hud.JournalVisible = false;
            hud.EndVisible = false;
            hud.Base.HideEncounter();
            hud.Base.SetHint(null);
            hud.Fade.color = new Color(0f, 0f, 0f, 0f);
            currentRoom = layout.RoomAt(pose.Position);
            exploreCam.cullingMask = Case01HouseBuilder.CullingMaskFor(currentRoom);
            RefreshWorld();
            UpdateDecor(true);
            inspectAnchor = pose.Position;
            SetCursorCaptured(true);
            SnapCameraToPlayer();
            Physics.SyncTransforms();
            UpdateObjective();
            UpdateQaText();
            UpdateModeText();
        }

        /// <summary>左下第二行：依 QA 模式顯示可用的功能鍵（只改 Case01 自己的 HUD）。</summary>
        private void UpdateModeText()
        {
            if (qaMode == Case01QaMode.Demo)
            {
                // 正式 Demo 不顯示；只有 Phase 1 品質回歸 QA 會設定這行說明。
                hud.Base.ViewMode.text = DemoBanner ?? "";
                return;
            }
            switch (qaMode)
            {
                case Case01QaMode.ArtPreview:
                    hud.Base.ViewMode.text = "美術 QA：F6 房屋 H0～H3｜F7 失序體形成（不開戰）｜F8 降低閃爍｜F1～F4 檢查點";
                    break;
                case Case01QaMode.CoreV03:
                    hud.Base.ViewMode.text = "核校正 v0.3 QA：F4 戰後核前｜F5 切回 v0.2｜F1～F3 檢查點｜F8 降低閃爍";
                    break;
                case Case01QaMode.NarrativeV04:
                    hud.Base.ViewMode.text = "空間敘事 v0.4 QA：F6 H0～H3｜F7 自語｜F9 失序門｜F10 殘響｜F8 降低閃爍｜F1～F4";
                    break;
                case Case01QaMode.LivingArt:
                    hud.Base.ViewMode.text = "客廳美術標準間 QA（" + (LivingArtOn ? "新美術 v0.6" : "舊版 v0.5") + "｜H" + ArtStage + "）：F7 新／舊切換｜F6 H0→H2→H3｜F1 回客廳入口｜F8 降低閃爍" +
                                             (layout != null && layout.LivingArt == null ? "｜<color=#E67E6E>找不到美術資產</color>" : "");
                    break;
                case Case01QaMode.PortalV05:
                    hud.Base.ViewMode.text = "Portal v0.5 QA（外觀：" + (portalStyle == Case01PortalStyle.V05 ? "v0.5 彩色" : "v0.4 單色") + (reduceFlicker ? "｜降低閃爍" : "") +
                                             "）：F6 門的狀態｜F7 切 v0.4／v0.5｜F8 降低閃爍｜可以直接走過門";
                    break;
                default:
                    hud.Base.ViewMode.text = "QA 跳轉：F1 S00 玄關｜F2 S30 樓梯錯接｜F3 S40 夾層｜F4 S50 戰後修復｜F5 核校正 v0.3｜F8 降低閃爍";
                    break;
            }
            if (floorplanQa)
                hud.Base.ViewMode.text = (residentialQa ? "住宅 v0.5 A/B QA（目前：" + FloorplanName(floorplan) + "）：F9 切換 A → B → 原版 → v0.4 B" : "一樓格局 v0.4 QA（目前：" + (floorplan == Case01Floorplan.V04 ? "B 反向樓梯" : "A 原版") + "）：F9 切換 A/B") + "｜F1～F4 檢查點｜F5 核校正 v0.2／v0.3";
        }

        /// <summary>F9 的切換順序：v0.5 A → v0.5 B → 原版 → v0.4 B → v0.5 A。</summary>
        public static Case01Floorplan NextFloorplan(Case01Floorplan p)
        {
            switch (p)
            {
                case Case01Floorplan.V05A: return Case01Floorplan.V05B;
                case Case01Floorplan.V05B: return Case01Floorplan.Original;
                case Case01Floorplan.Original: return Case01Floorplan.V04;
                default: return Case01Floorplan.V05A;
            }
        }

        public static string FloorplanName(Case01Floorplan p)
        {
            switch (p)
            {
                case Case01Floorplan.V05A: return "v0.5 方案 A：客廳→餐廳→廚房，樓梯在廚房側";
                case Case01Floorplan.V05B: return "v0.5 方案 B：原分區＋樓梯下收納";
                case Case01Floorplan.V04: return "v0.4 B 反向樓梯";
                default: return "原版";
            }
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
            // v0.5：先關閉再建人形（建立的那一影格也不會出現沒有外殼的黑色人形）。
            encounterGroup.gameObject.SetActive(false);
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
                fig.gameObject.SetActive(false);
                // 黑色裸身換成碎片外殼（只留發光的核與裂紋）；戰鬥中的同一個敵人也做相同處理。
                Case01EnemyShell.HideBareBody(fig);
                figureOrbiters.Add(orbiters);
            }
            Case01HouseBuilder.SetLayer(encounterGroup, Case01HouseBuilder.AnomalyLayer);
            // v0.5：可以開戰時，碎片（DebrisOnly）就已經散在中庭地上；第一次進入中庭才開始凝聚。戰敗重試直接是完成外觀。
            if (manifestation != null)
            {
                ArenaArt.Kill(manifestation.Root.gameObject);
                manifestation = null;
            }
            if (flow != null && flow.CanStartBattle) RebuildManifestation(manifested);
        }

        /// <summary>v0.5 失序體目前的可見狀態（見 <see cref="Case01EnemyVisual"/>）。</summary>
        public Case01EnemyVisual EnemyVisual
        {
            get
            {
                if (arena != null) return Case01EnemyVisual.Battle;
                if (flow == null || !flow.CanStartBattle || encounter == null || encounter.Defeated || manifestation == null) return Case01EnemyVisual.Hidden;
                return manifestation.Visual;
            }
        }

        /// <summary>中庭人形群組是否該顯示：只有凝聚開始之後（人形本身仍由演出控制可見度）。</summary>
        private bool FiguresShouldShow
        {
            get
            {
                var v = EnemyVisual;
                return v == Case01EnemyVisual.Assembling || v == Case01EnemyVisual.Manifested;
            }
        }

        // ---------------- 每影格 ----------------

        private void Update()
        {
            if (!built) return;
            Tick(Time.deltaTime, InputSource != null ? InputSource.Read() : Case01Input.None);
            // Phase 1.5 P0：每個影格把實際游標鎖定對齊想要的狀態（避免游標自由時滑鼠碰到螢幕邊緣就轉不動）。
            if (playMode) Case01Cursor.Maintain(cursorCaptured);
        }

        public void Tick(float dt, ExploreInput input) { Tick(dt, Case01Input.From(input)); }

        /// <summary>推進一個影格（Update 呼叫；測試可直接以指定輸入呼叫）。</summary>
        public void Tick(float dt, Case01Input input)
        {
            // Demo：QA 快捷鍵（F1～F10、檢查點跳轉）不作用。
            if (qaMode == Case01QaMode.Demo)
            {
                input.Function = 0;
                input.Jump = 0;
            }
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
            if (input.Function == 8)
            {
                reduceFlicker = !reduceFlicker;
                hud.Base.ShowToast(reduceFlicker ? "QA：降低閃爍　開" : "QA：降低閃爍　關");
                UpdateModeText();
            }
            if (qaMode == Case01QaMode.LivingArt && input.Function == 6)
            {
                // 站在原地比較 H0 → H2 → H3（視角不變）。
                SetLivingStage(ArtStage == 0 ? 2 : ArtStage == 2 ? 3 : 0);
                return;
            }
            if (qaMode == Case01QaMode.LivingArt && input.Function == 7)
            {
                livingArtOn = !livingArtOn;
                if (layout.LivingArt != null) layout.LivingArt.SetActive(livingArtOn);
                if (layout.HouseArt != null) layout.HouseArt.SetActive(livingArtOn);
                Physics.SyncTransforms();
                UpdateModeText();
                hud.Base.ShowToast(livingArtOn ? "QA：客廳　新美術（v0.6 標準間）" : "QA：客廳　舊版（v0.5 程序方塊）");
                return;
            }
            if (qaMode == Case01QaMode.PortalV05 && input.Function == 6)
            {
                PreviewGlitch((GlitchPreview + 1) % GlitchPreviewCount);
                return;
            }
            if (qaMode == Case01QaMode.PortalV05 && input.Function == 7)
            {
                portalStyle = portalStyle == Case01PortalStyle.V05 ? Case01PortalStyle.V04 : Case01PortalStyle.V05;
                UpdateModeText();
                hud.Base.ShowToast(portalStyle == Case01PortalStyle.V05 ? "QA：失序門 v0.5（彩色訊號破損）" : "QA：失序門 v0.4（單色雪花）");
                return;
            }
            if (input.Function == 5)
            {
                // A/B 比較：在 v0.2 與 v0.3 核校正之間切換，直接回到戰後核前（S50）。
                qaMode = qaMode == Case01QaMode.CoreV03 ? Case01QaMode.Standard : Case01QaMode.CoreV03;
                JumpNow(Case01Checkpoint.S50);
                hud.Base.ShowToast(qaMode == Case01QaMode.CoreV03 ? "QA：核校正 v0.3（關係校正）" : "QA：核校正 v0.2（原三段式）");
                return;
            }
            if ((qaMode == Case01QaMode.ArtPreview || qaMode == Case01QaMode.NarrativeV04) && input.Function == 6)
            {
                SetArtStage((ArtStage + 1) % 4);
                return;
            }
            if (qaMode == Case01QaMode.NarrativeV04 && input.Function == 7)
            {
                PreviewNextMonologue();
                return;
            }
            if (floorplanQa && input.Function == 9)
            {
                // A/B：切換新舊一樓格局（回到玄關起點）。
                floorplan = residentialQa ? NextFloorplan(floorplan) : floorplan == Case01Floorplan.V04 ? Case01Floorplan.Original : Case01Floorplan.V04;
                JumpNow(Case01Checkpoint.S00);
                UpdateModeText();
                hud.Base.ShowToast("QA：一樓格局　" + FloorplanName(floorplan));
                return;
            }
            if (qaMode == Case01QaMode.NarrativeV04 && input.Function == 9)
            {
                PreviewGlitch((GlitchPreview + 1) % GlitchPreviewCount);
                return;
            }
            if (qaMode == Case01QaMode.NarrativeV04 && input.Function == 10)
            {
                PreviewEcho((EchoPreview + 1) % Case01Echoes.All.Length);
                return;
            }
            if (qaMode == Case01QaMode.ArtPreview && input.Function == 7)
            {
                PreviewManifestation();
                return;
            }
            if (input.Jump >= 1 && input.Jump <= 4)
            {
                JumpNow((Case01Checkpoint)(input.Jump - 1));
                return;
            }
            clock += dt;
            if (input.SkipLine) hud.SkipMonologue();
            // 自語在面板／日誌／選項／檢視／轉場／演出時暫停，回到自由探索才接著播。
            hud.MonologueHold = Mode != Case01Mode.Exploring || hud.PanelVisible || hud.JournalVisible || hud.ActiveChoice != null;
            hud.Tick(dt);
            if (echo != null && player != null)
            {
                echo.EchoVolume = echoVolume;
                echo.ReduceMotion = reduceFlicker;
                bool echoAllowed = Mode == Case01Mode.Exploring || Mode == Case01Mode.Transition || Mode == Case01Mode.Inspect;
                echo.Tick(dt, player.transform.position, currentRoom, echoAllowed, clock);
            }
            Animate(dt);
            if (layout != null && layout.LivingArt != null && layout.LivingArt.Stage != HouseStage) layout.LivingArt.ApplyStage(HouseStage);
            // v0.7：屋內不畫異常空間、異常空間不畫街景（避免窗外或敞頂中庭看到不該出現的世界）。
            if (exploreCam != null) exploreCam.cullingMask = Case01HouseBuilder.CullingMaskFor(currentRoom);
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
                case Case01Mode.Manifesting: TickManifesting(dt, input); break;
            }
            if (!playMode && arena != null)
            {
                arena.Tick(dt);
                if (DemoHud != null) DemoHud.Tick(dt);
            }
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
                player.Step(dt, new ExploreInput { Look = b.Look }, Case01Cursor.Looking(cursorCaptured));
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
            player.Step(dt, b, Case01Cursor.Looking(cursorCaptured));
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
            if (currentRoom == Case01Room.Courtyard && flow.CanStartBattle && encounter != null && !manifested)
            {
                BeginManifestation();
                return;
            }
            if (currentRoom == Case01Room.Courtyard && flow.CanStartBattle && encounter != null && qaMode != Case01QaMode.ArtPreview)
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
                else if (CurrentTarget != null && CurrentTarget.Id == Case01Ids.FrontDoor && qaMode == Case01QaMode.Demo)
                {
                    // Demo：前門＝回到街上（不是 QA 重來／結束畫面）。進度留在同一個流程物件裡。
                    if (echo != null) echo.Stop();
                    hud.ClearMonologue();
                    if (DemoExitRequested != null) DemoExitRequested();
                    return;
                }
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
            hud.Base.CursorHintVisible = !Case01Cursor.Looking(cursorCaptured);
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
            if (sfx != null) sfx.SetAmbient(r == Case01Room.Mezzanine || r == Case01Room.Courtyard);
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
            UpdateAging(force);
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
            // 殘響先排（生活片段），自語接在後面。
            if (r.Echo != null && echo != null) echo.Play(r.Echo, content, palette, layout);
            if (r.Monologue != null)
                foreach (var id in r.Monologue) hud.EnqueueMonologue(Case01Texts.SayLine(id));
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
            if (r.Redirect.HasValue)
            {
                if (r.CommitEffect)
                {
                    pendingCommit = r.Redirect.Value;
                    BeginTransition(CommitPose(r.Redirect.Value));
                }
                else BeginTransition(layout.Arrivals[r.Redirect.Value]);
            }
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
            if (pendingCommit.HasValue)
            {
                SpawnDoorSnap(pendingCommit.Value);
                if (sfx != null) sfx.PlayEffect(sfx.Settle, 1f);
                pendingCommit = null;
            }
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
            if (echo != null) echo.Stop();
            hud.ClearMonologue();
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
            // v0.5：戰鬥中的失序體掛上與探索中相同的碎片外殼（在戰鬥物件啟用前，第一個畫面就有）。
            var shellMats = Case01EnemyShell.Materials(palette);
            for (int i = 0; i < scenario.Enemies.Count; i++)
            {
                var enemyRoot = arena.EnemyRoot(scenario.Enemies[i].Id);
                Case01EnemyShell.AttachToEnemy(enemyRoot, i, CombatArenaView.EnemyHeightFor(scenario.Enemies[i].Value), shellMats);
                Case01EnemyShell.HideBareBody(enemyRoot);
            }
            // Case01 公開 Demo 的戰鬥 HUD（舊 HUD 面板隱藏；結果橫幅、浮字照舊）。
            // 首戰教學只在這個 QA 執行階段的第一場戰鬥出現；戰敗重試不再自動出現（可按「？教學」重看）。
            DemoHud = CombatDemoHud.Attach(arena, new CombatDemoOptions { ShowQaTools = false, Tutorial = TutorialEnabled && !TutorialShown, ReduceMotion = reduceFlicker });
            TutorialShown = true;
            arena.SetCameraPose(CombatArenaView.CameraPosition, CombatArenaView.CameraLookAt, CombatArenaView.DefaultFieldOfView);
            arena.HudVisible = false;
            arena.InteractionEnabled = false;
            go.SetActive(true);

            encounterGroup.gameObject.SetActive(false);
            if (manifestation != null) manifestation.SetVisible(false);
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
            // 已形成過：BuildEncounter 直接以完成外觀（外殼＋人形）重建，不重播、不閃黑模型。
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
            if (echo != null) echo.Stop();
            hud.ClearMonologue();
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
            if (L.PhotoSecondFigureAlt != null) L.PhotoSecondFigureAlt.sharedMaterial = flow.CoreStabilized ? L.PhotoFadedMatAlt : L.PhotoFigureMatAlt;
            L.ThirdDoorFrame.SetActive(flow.ThirdDoorVisible);
            L.Interactables[Case01Ids.ThirdDoor].gameObject.SetActive(flow.ThirdDoorVisible);
            foreach (var p in L.Portals)
            {
                bool active = flow.PortalActive(p.Door);
                if (p.Seal != null) p.Seal.SetActive(!active);
                p.Membrane.SetActive(active);
                if (p.Backdrop != null) p.Backdrop.SetActive(active);
            }
            L.StudyMarkLabel.text = flow.StairRouteRestored ? "△\n<size=20>歸位</size>" : "△";
            L.FrontDoorLabel.text = flow.CoreStabilized ? "離開十七號" : "QA 返回／重來";
            L.Interactables[Case01Ids.FrontDoor].Prompt = flow.CoreStabilized ? "前門（離開十七號）" : "前門（QA 返回／重來）";
            if (qaMode == Case01QaMode.Demo)
            {
                L.FrontDoorLabel.text = "";
                L.Interactables[Case01Ids.FrontDoor].Prompt = flow.CoreStabilized ? "前門（離開十七號，回到街上）" : "前門（先回到街上）";
            }
            L.Door17Frame.SetActive(flow.Door17Open);
            L.TearFrame.SetActive(flow.CourtyardTearOpen);
            // v0.2：FP-001 只在核穩定後、尚未拾取時出現；各房間的安靜異變。
            L.Decor.SetEvidenceVisible(flow.FamilyPhotoAvailable);
            UpdateDecor(false);
            hud.Base.Controls.text = "WASD 移動｜滑鼠 視角｜Shift 奔跑｜E 調查／互動｜1–3 選擇｜J 日誌" +
                                     (flow.FamilyPhotoCollected ? "｜I 檢視證物" : "") + "｜Tab 略過自語｜Esc 釋放滑鼠";

            // v0.4：校正後是冷白（穩定），不是暖光。
            Color coreColor = flow.CoreStabilized ? Case01Mood.CoreStable : flow.DefenseActive ? new Color(1f, 0.25f, 0.35f) : ArenaArt.Rift;
            ArenaArt.SetEmission(L.CoreMat, coreColor * (flow.CoreStabilized ? 1.2f : 2.2f));
            L.CoreLight.color = coreColor;
            var mezzColor = flow.CoreStabilized ? Case01Mood.MezzStable : flow.DefenseActive ? new Color(0.95f, 0.4f, 0.5f) : new Color(0.6f, 0.55f, 0.85f);
            foreach (var l in L.MezzLights) l.color = mezzColor;
            foreach (var m in L.MezzLineMats) ArenaArt.SetEmission(m, (flow.CoreStabilized ? Case01Mood.CoreStable : ArenaArt.Rift) * 1.8f);
            if (L.CourtyardLight != null) L.CourtyardLight.intensity = flow.CourtyardTearOpen ? 0.9f : 2.2f;
            RefreshMezzLabels();
            bool enemiesVisible = flow.CanStartBattle && encounter != null && !encounter.Defeated && arena == null;
            // v0.5：人形只在凝聚開始後顯示；在那之前中庭只有散落的碎片。
            if (encounterGroup != null) encounterGroup.gameObject.SetActive(enemiesVisible && FiguresShouldShow);
            if (manifestation != null) manifestation.SetVisible(enemiesVisible);
            ApplyVolumes();

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
            UpdateLights(dt);
            UpdatePortalFx(dt);
            AnimateDoorSnap(dt);
            if (flow.PreviewConflict != 0)
            {
                layout.CoreLight.color = new Color(1f, 0.3f, 0.35f);
                layout.CoreLight.intensity = 1.6f + 0.6f * Mathf.Sin(clock * 3f) * FlickerScale;
            }
            else if (layout.CoreLight.intensity != 2.0f && !flow.CoreStabilized)
            {
                layout.CoreLight.intensity = 2.0f;
            }
        }

        // ---------------- v0.4 失序門的訊號故障（只改外觀；傳送仍由 Flow 與 Portal.Crossed 決定） ----------------

        [Tooltip("v0.4 失序門電流雜音的音量。")]
        [Range(0f, 1f)] [SerializeField] private float portalVolume = 0.35f;

        public float PortalVolume { get { return portalVolume; } set { portalVolume = Mathf.Clamp01(value); } }

        private float glitchClock;
        private float portalShaderClock;

        /// <summary>門的路由狀態 → 故障外觀等級。</summary>
        public Case01PortalState PortalState(Case01Door door)
        {
            if (flow == null || !flow.PortalActive(door)) return Case01PortalState.Dormant;
            if (flow.CoreStabilized) return Case01PortalState.Corrected;
            switch (door)
            {
                case Case01Door.StudyDoor:
                    return Case01PortalState.Wrong;
                case Case01Door.MezzTriangle:
                    if (!flow.MatchesMark(door)) return Case01PortalState.Wrong;
                    return flow.V03Anchored || flow.Session.Has(Case01Flags.RouteAnchorSet) ? Case01PortalState.Anchored : Case01PortalState.Unstable;
                case Case01Door.MezzCircle:
                    if (!flow.MatchesMark(door)) return Case01PortalState.Wrong;
                    return flow.V03SecondLink ? Case01PortalState.Anchored : Case01PortalState.Unstable;
                case Case01Door.Mezz17:
                    return flow.V03SecondLink ? Case01PortalState.Anchored : Case01PortalState.Unstable;
                default:
                    return Case01PortalState.Unstable;
            }
        }

        private void BuildPortalHums()
        {
            if (sfx == null || layout == null) return;
            foreach (var p in layout.Portals)
                if (p.Membrane != null) p.Hum = sfx.AddSpatialLoop(p.Membrane.transform, sfx.Static, 6f);
        }

        private void UpdatePortalFx(float dt)
        {
            if (layout == null) return;
            glitchClock += dt;
            // v0.5 膜的動畫時間：降低閃爍時以 1/4 速度累加（切換時不跳格）。
            portalShaderClock += dt * (reduceFlicker ? 0.25f : 1f);
            // 每 0.1 秒一個「破訊」時間片。
            int slice = Mathf.FloorToInt(glitchClock * 10f);
            bool v05 = portalStyle == Case01PortalStyle.V05;
            foreach (var p in layout.Portals)
            {
                var state = PortalState(p.Door);
                var look = Case01Glitch.For(state, reduceFlicker);
                Case01Glitch.Apply(p.MembraneMat, look);
                Case01Glitch.ApplyV05(p.MembraneMatV05, look, Case01Glitch.SaturationFor(state, reduceFlicker),
                    portalShaderClock * Case01Glitch.For(state, false).Speed);
                // v0.5：同一個膜物件換材質；邊界框只在 v0.5 顯示（v0.4 門框改為不渲染，物件的開關邏輯不變）。
                var memR = p.Membrane.GetComponent<Renderer>();
                var want = v05 && p.MembraneMatV05 != null ? p.MembraneMatV05 : p.MembraneMat;
                if (memR.sharedMaterial != want) memR.sharedMaterial = want;
                if (p.Edge != null) p.Edge.Tick(glitchClock, state, reduceFlicker, v05 && p.Membrane.activeInHierarchy);
                // 門框物件仍照流程開關（第三扇門浮現的提示不變）；v0.5 邊界出現時才不畫舊門框，避免重疊。
                bool frameHidden = v05 && p.Membrane.activeInHierarchy;
                if (p.FrameRenderers != null)
                    foreach (var fr in p.FrameRenderers)
                        if (fr.enabled == frameHidden) fr.enabled = !frameHidden;
                if (!frameHidden && p.Frame != null && p.Frame.gameObject.activeInHierarchy)
                {
                    float r = Mathf.Abs(Mathf.Sin(slice * 12.9898f + (int)p.Door * 78.233f) * 43758.5453f) % 1f;
                    float jump = r < look.JitterChance ? (r / Mathf.Max(0.0001f, look.JitterChance) - 0.5f) * 2f * look.JitterAmount : 0f;
                    p.Frame.localPosition = p.FrameHome + p.Frame.localRotation * Vector3.right * jump;
                    // 黑灰門框上斷續的冷白光（降低閃爍時固定亮度）。
                    float pulse = reduceFlicker ? 0.7f : 0.55f + 0.45f * Mathf.PerlinNoise(glitchClock * 3f, (int)p.Door);
                    ArenaArt.SetEmission(p.FrameMat, new Color(0.62f, 0.66f, 0.74f) * look.FrameGlow * pulse);
                }
                if (sfx != null && p.Hum != null)
                {
                    bool audible = state != Case01PortalState.Dormant && p.Membrane.activeInHierarchy && Mode != Case01Mode.Battle && Mode != Case01Mode.EnteringBattle;
                    sfx.DriveLoop(p.Hum, audible ? portalVolume * look.Hum * Mathf.Lerp(0.5f, 1f, scareIntensity) * 0.5f : 0f);
                }
            }
        }

        // ---------------- v0.3 核校正：標籤、提交演出 ----------------

        private Case01Room? pendingCommit;
        private readonly List<Transform> snapPieces = new List<Transform>();
        private readonly List<Vector3> snapFrom = new List<Vector3>();
        private readonly List<Vector3> snapTo = new List<Vector3>();
        private readonly List<Quaternion> snapRotFrom = new List<Quaternion>();
        private Light snapLight;
        private float snapT = -1f;
        public const float DoorSnapSeconds = 1.6f;

        /// <summary>v0.3 提交演出是否正在播放（測試用）。</summary>
        public bool DoorSnapPlaying { get { return snapT >= 0f && snapT < DoorSnapSeconds; } }
        public int DoorSnapPieces { get { return snapPieces.Count; } }

        /// <summary>寫回校正後的到達位置：站在門內、轉身面向剛走過的門。</summary>
        public static Case01Pose CommitPose(Case01Room room)
        {
            if (room == Case01Room.Study) return new Case01Pose(new Vector3(Case01HouseBuilder.StudyDoorX, Case01HouseBuilder.FloorHeight + 0.05f, 6.3f), 180f);
            return new Case01Pose(new Vector3(7.1f, 0.05f, 1.9f), 180f);
        }

        /// <summary>門框歸位：幾片發光的門框從錯位處扣回門洞，伴隨暖光脈動（約 1.6 秒，不閃爍）。</summary>
        private void SpawnDoorSnap(Case01Room room)
        {
            ClearDoorSnap();
            Vector3 c = room == Case01Room.Study ? new Vector3(Case01HouseBuilder.StudyDoorX, Case01HouseBuilder.FloorHeight, 5.1f) : new Vector3(7.1f, 0f, 0.12f);
            var g = ArenaArt.Group("DoorSnap", content, c);
            var glow = palette.Get("lamp_shade");
            float w = Case01HouseBuilder.DoorWidth, h = Case01HouseBuilder.DoorHeight;
            Vector3[] pos = { new Vector3(-w * 0.5f - 0.05f, h * 0.5f, 0f), new Vector3(w * 0.5f + 0.05f, h * 0.5f, 0f), new Vector3(0f, h + 0.05f, 0f) };
            Vector3[] scale = { new Vector3(0.07f, h, 0.05f), new Vector3(0.07f, h, 0.05f), new Vector3(w + 0.2f, 0.07f, 0.05f) };
            Vector3[] off = { new Vector3(-0.8f, 0.3f, 0.2f), new Vector3(0.8f, -0.25f, 0.25f), new Vector3(0.1f, 0.6f, 0.25f) };
            for (int i = 0; i < pos.Length; i++)
            {
                var p = ArenaArt.Prim(PrimitiveType.Cube, g, pos[i] + off[i], scale[i], glow, null, "SnapPiece", false, false);
                p.localRotation = Quaternion.Euler(off[i].y * 25f, off[i].x * 20f, off[i].x * 15f);
                snapPieces.Add(p);
                snapFrom.Add(p.localPosition);
                snapTo.Add(pos[i]);
                snapRotFrom.Add(p.localRotation);
            }
            snapLight = ArenaArt.PointLight(g, new Vector3(0f, 1.2f, 0.6f), Case01Mood.CoreStable, 5f, 0f, false);
            snapT = 0f;
        }

        private void AnimateDoorSnap(float dt)
        {
            if (snapT < 0f) return;
            snapT += dt;
            float k = Mathf.Clamp01(snapT / DoorSnapSeconds);
            float s = k * k * (3f - 2f * k);
            for (int i = 0; i < snapPieces.Count; i++)
            {
                if (snapPieces[i] == null) continue;
                snapPieces[i].localPosition = Vector3.Lerp(snapFrom[i], snapTo[i], s);
                snapPieces[i].localRotation = Quaternion.Slerp(snapRotFrom[i], Quaternion.identity, s);
            }
            if (snapLight != null) snapLight.intensity = 2.2f * Mathf.Sin(Mathf.PI * k);
            if (snapT > DoorSnapSeconds + 1.5f) ClearDoorSnap();
        }

        private void ClearDoorSnap()
        {
            if (snapPieces.Count > 0 && snapPieces[0] != null) ArenaArt.Kill(snapPieces[0].parent.gameObject);
            snapPieces.Clear();
            snapFrom.Clear();
            snapTo.Clear();
            snapRotFrom.Clear();
            snapLight = null;
            snapT = -1f;
        }

        /// <summary>夾層門框上方的記號：v0.3 已校正的關係變綠並加上房間名稱（不只靠顏色）。</summary>
        private void RefreshMezzLabels()
        {
            var normal = new Color(0.95f, 0.92f, 1f);
            var fixedColor = new Color(0.6f, 1f, 0.65f);
            bool v03 = flow.Correction == Case01CorrectionMode.RevisedV03;
            Text t;
            if (layout.MezzLabels.TryGetValue(Case01Door.MezzTriangle, out t))
            {
                bool on = v03 && flow.V03Anchored;
                t.text = on ? "△ 書房" : "△";
                t.color = on ? fixedColor : normal;
            }
            if (layout.MezzLabels.TryGetValue(Case01Door.MezzCircle, out t))
            {
                bool on = v03 && flow.V03SecondLink;
                t.text = on ? "○ 餐廳" : "○";
                t.color = on ? fixedColor : normal;
            }
            if (layout.MezzLabels.TryGetValue(Case01Door.Mezz17, out t))
            {
                bool on = v03 && flow.V03SecondLink;
                t.text = on ? "□ 17 玄關" : "□ 17";
                t.color = on ? fixedColor : normal;
            }
        }

        // ---------------- v0.3 失序體形成演出 ----------------

        private void RebuildManifestation(bool complete)
        {
            if (manifestation != null) ArenaArt.Kill(manifestation.Root.gameObject);
            var figs = new List<Transform>();
            var heights = new List<float>();
            var scenario = CurrentScenario();
            for (int i = 0; i < encounterGroup.childCount; i++)
            {
                figs.Add(encounterGroup.GetChild(i));
                heights.Add(CombatArenaView.EnemyHeightFor(i < scenario.Enemies.Count ? scenario.Enemies[i].Value : 40));
            }
            manifestation = new Case01Manifestation(content, palette, figs, heights);
            Case01HouseBuilder.SetLayer(manifestation.Root, Case01HouseBuilder.AnomalyLayer);
            if (complete) manifestation.CompleteSilently();
            else manifestation.PhaseStarted += OnManifestationPhase;
        }

        /// <summary>第一次進入中庭時開始形成演出（只發生一次；戰敗重試不重播）。</summary>
        private void BeginManifestation()
        {
            // 地上的碎片已經在（DebrisOnly）；從同一份碎片開始凝聚，不重建、不閃現。
            if (manifestation == null || manifestation.Visual != Case01EnemyVisual.DebrisOnly) RebuildManifestation(false);
            manifested = true;
            manifestation.SetVisible(true);
            encounterGroup.gameObject.SetActive(true);
            // 視線轉向兩個失序體成形的位置。
            var center = encounter.Center + Vector3.up * 1.1f - player.EyePosition;
            player.Teleport(player.transform.position, Mathf.Atan2(center.x, center.z) * Mathf.Rad2Deg,
                -Mathf.Atan2(center.y, new Vector2(center.x, center.z).magnitude) * Mathf.Rad2Deg);
            Mode = Case01Mode.Manifesting;
            hud.HidePanel();
            hud.Base.SetHint("<color=#C79BFF>夾層的書頁與照片被吸進中庭……</color>　［E］跳過");
        }

        private void OnManifestationPhase(int phase)
        {
            if (sfx == null) return;
            switch (phase)
            {
                case 0: sfx.PlayEffect(sfx.Rustle, 0.8f); break;
                case 1: sfx.PlayEffect(sfx.Whoosh); break;
                case 2:
                    sfx.PlayEffect(sfx.Assemble);
                    sfx.PlayMusic(sfx.Sting);
                    break;
                case 3: sfx.PlayEffect(sfx.Lock, 1.1f); break;
            }
        }

        private void TickManifesting(float dt, Case01Input input)
        {
            var b = input.Base;
            player.Step(dt, new ExploreInput { Look = b.Look }, Case01Cursor.Looking(cursorCaptured));
            SnapCameraToPlayer();
            if (b.Interact) manifestation.Skip();
            else manifestation.Tick(dt);
            // 演出期間鏡頭拉近（玩家位置不變），結束前 0.6 秒拉回。
            float mt = manifestation.Time;
            float zoom = Mathf.Min(Mathf.Clamp01(mt / 0.8f), Mathf.Clamp01((Case01Manifestation.Duration - mt) / 0.6f));
            exploreCam.fieldOfView = Mathf.Lerp(ExploreFieldOfView, 30f, zoom * zoom * (3f - 2f * zoom));
            if (!manifestation.Done) return;
            hud.Base.SetHint(null);
            Mode = Case01Mode.Exploring;
        }

        // ---------------- v0.3 美術 QA ----------------

        /// <summary>美術 QA：切換房屋 H0～H3（重建狀態並站到客廳），不需要跑完整流程。</summary>
        public void SetArtStage(int h)
        {
            ArtStage = Mathf.Clamp(h, 0, 3);
            KillArena();
            if (content != null)
            {
                content.gameObject.SetActive(false);
                ArenaArt.Kill(content.gameObject);
            }
            encounterGroup = null;
            exploreCam.enabled = true;
            BuildContent(Case01Flow.ForHouseStage(ArtStage), Case01Checkpoint.S00, layout != null ? layout.ArtPose : new Case01Pose(new Vector3(3.9f, 0.05f, 6.3f), -55f));
            hud.Base.ShowToast("美術 QA：房屋 H" + ArtStage);
        }

        /// <summary>客廳美術 QA：切換房屋階段，玩家留在原地、視角不變（同角度比較）。</summary>
        public void SetLivingStage(int h)
        {
            var pos = player.transform.position;
            float yaw = player.Yaw, pitch = player.Pitch;
            ArtStage = Mathf.Clamp(h, 0, 3);
            KillArena();
            if (content != null)
            {
                content.gameObject.SetActive(false);
                ArenaArt.Kill(content.gameObject);
            }
            encounterGroup = null;
            exploreCam.enabled = true;
            BuildContent(Case01Flow.ForHouseStage(ArtStage), Case01Checkpoint.S00, new Case01Pose(pos, yaw));
            player.Teleport(pos, yaw, pitch);
            SnapCameraToPlayer();
            UpdateModeText();
            hud.Base.ShowToast("QA：客廳 H" + ArtStage);
        }

        public const int GlitchPreviewCount = 5;
        /// <summary>目前的失序門預覽（-1＝沒有）。</summary>
        public int GlitchPreview { get; private set; } = -1;

        /// <summary>空間敘事 QA：失序門的五種狀態（錯接的書房門、夾層未定、△ 錯接、△ 已錨定、校正後），站在門前。</summary>
        public void PreviewGlitch(int n)
        {
            GlitchPreview = Mathf.Clamp(n, 0, GlitchPreviewCount - 1);
            KillArena();
            if (content != null)
            {
                content.gameObject.SetActive(false);
                ArenaArt.Kill(content.gameObject);
            }
            encounterGroup = null;
            exploreCam.enabled = true;
            var mezzFacingTriangle = new Case01Pose(Case01HouseBuilder.MezzCenter + new Vector3(-3.2f, 0.05f, 0f), -90f);
            Case01Flow f;
            Case01Pose pose;
            string label;
            switch (GlitchPreview)
            {
                case 0:
                    f = Case01Flow.ForCheckpoint(Case01Checkpoint.S30);
                    pose = new Case01Pose(new Vector3(Case01HouseBuilder.StudyDoorX, Case01HouseBuilder.FloorHeight + 0.05f, 3.2f), 0f);
                    label = "錯接：書房門通往餐廳";
                    break;
                case 1:
                    f = Case01Flow.ForCheckpoint(Case01Checkpoint.S40);
                    pose = mezzFacingTriangle;
                    label = "夾層 △ 門：狀態未定";
                    break;
                case 2:
                    f = Case01Flow.ForCheckpoint(Case01Checkpoint.S50, Case01CorrectionMode.RevisedV03);
                    pose = mezzFacingTriangle;
                    label = "△ 門錯接（戰後）";
                    break;
                case 3:
                    f = Case01Flow.ForCheckpoint(Case01Checkpoint.S50, Case01CorrectionMode.RevisedV03);
                    f.Traverse(Case01Door.MezzTriangle);
                    f.Interact(Case01Ids.Core);
                    f.Choose(Case01IdsV03.ChoiceAnchor, 0);
                    pose = mezzFacingTriangle;
                    label = "△ 門已錨定";
                    break;
                default:
                    f = Case01Flow.ForHouseStage(3);
                    pose = mezzFacingTriangle;
                    label = "校正後：故障停止";
                    break;
            }
            BuildContent(f, Case01Checkpoint.S00, pose);
            hud.Base.ShowToast("失序門預覽 " + (GlitchPreview + 1) + "／" + GlitchPreviewCount + "：" + label);
        }

        /// <summary>目前的殘響預覽（-1＝沒有）。</summary>
        public int EchoPreview { get; private set; } = -1;

        /// <summary>空間敘事 QA：在對應的房屋階段與位置單獨回放一段殘響（不需重新破關；不改流程的去重狀態）。</summary>
        public void PreviewEcho(int n)
        {
            EchoPreview = Mathf.Clamp(n, 0, Case01Echoes.All.Length - 1);
            var id = Case01Echoes.All[EchoPreview];
            KillArena();
            if (content != null)
            {
                content.gameObject.SetActive(false);
                ArenaArt.Kill(content.gameObject);
            }
            encounterGroup = null;
            exploreCam.enabled = true;
            float y = Case01HouseBuilder.FloorHeight + 0.05f;
            Case01Flow f;
            Case01Pose pose;
            switch (id)
            {
                case Case01Echoes.Entrance:
                    f = Case01Flow.ForHouseStage(0);
                    pose = layout != null ? layout.Spawn : new Case01Pose(Case01HouseBuilder.SpawnPoint, 0f);
                    break;
                case Case01Echoes.Dining:
                    f = Case01Flow.ForHouseStage(0);
                    pose = layout != null && Case01HouseBuilder.IsV05(floorplan) ? layout.Arrivals[Case01Room.Dining] : new Case01Pose(new Vector3(6.5f, 0.05f, 5.6f), 25f);
                    break;
                case Case01Echoes.Study:
                    f = Case01Flow.ForHouseStage(1);
                    pose = new Case01Pose(new Vector3(Case01HouseBuilder.StudyDoorX, y, 5.7f), 15f);
                    break;
                default:
                    f = Case01Flow.ForCheckpoint(Case01Checkpoint.S30);
                    pose = new Case01Pose(new Vector3(7.6f, y, 2.4f), 70f);
                    break;
            }
            BuildContent(f, Case01Checkpoint.S00, pose);
            hud.ClearMonologue();
            echo.Play(id, content, palette, layout);
            hud.Base.ShowToast("殘響預覽 " + (EchoPreview + 1) + "／" + Case01Echoes.All.Length + "：" + Case01Texts.EchoRoomName(id) + Case01Texts.EchoPlaceholderNote);
        }

        private int previewLine = -1;

        /// <summary>空間敘事 QA：依序預覽下一句自語（只顯示，不改流程或去重狀態）。</summary>
        public void PreviewNextMonologue()
        {
            previewLine = (previewLine + 1) % Case01Say.All.Length;
            var id = Case01Say.All[previewLine];
            hud.ClearMonologue();
            hud.EnqueueMonologue(Case01Texts.SayLine(id));
            hud.Base.ShowToast("自語預覽 " + (previewLine + 1) + "／" + Case01Say.All.Length + "：" + id);
        }

        /// <summary>美術 QA：直接到中庭播放失序體形成（不開戰，可重複）。</summary>
        public void PreviewManifestation()
        {
            KillArena();
            if (content != null)
            {
                content.gameObject.SetActive(false);
                ArenaArt.Kill(content.gameObject);
            }
            encounterGroup = null;
            exploreCam.enabled = true;
            var f = Case01Flow.ForCheckpoint(Case01Checkpoint.S40);
            f.Interact(Case01Ids.Core);
            BuildContent(f, Case01Checkpoint.S40, layout != null ? layout.Arrivals[Case01Room.Courtyard] : (Case01Pose?)null);
            hud.Base.ShowToast("美術 QA：失序體形成（不開戰）");
        }

        private void ApplyVolumes()
        {
            if (sfx == null) return;
            sfx.AmbientVolume = ambientVolume;
            sfx.EffectsVolume = effectsVolume;
            sfx.MusicVolume = musicVolume;
            sfx.ScareIntensity = scareIntensity;
            if (layout != null && layout.Creak != null) layout.Creak.volume = sfx.EffectGain;
        }

        public void SetVolumes(float ambient, float effects, float music)
        {
            ambientVolume = Mathf.Clamp01(ambient);
            effectsVolume = Mathf.Clamp01(effects);
            musicVolume = Mathf.Clamp01(music);
            ApplyVolumes();
        }

        private void OnApplicationFocus(bool focus)
        {
            if (playMode && sfx != null) sfx.Pause(!focus);
        }

        // ---------------- v0.3 燈光（依房間基準色計算，不累積漂移；低頻、可降低閃爍） ----------------

        /// <summary>閃爍強度：關閉「降低閃爍」時 1，開啟時 0.25；再乘上驚嚇強度。</summary>
        public float FlickerScale { get { return (reduceFlicker ? 0.25f : 1f) * Mathf.Lerp(0.3f, 1f, scareIntensity); } }

        // ---------------- v0.4 H0→H3 光色（Case01Mood；H3 不回暖；階段之間約 1.5 秒漸變） ----------------

        public const float MoodBlendRate = 2.2f;
        private bool moodSnap = true;
        private readonly Dictionary<Light, Color> moodColor = new Dictionary<Light, Color>();
        private readonly Dictionary<Light, float> moodIntensity = new Dictionary<Light, float>();
        private readonly Dictionary<Light, Color> lampBaseColor = new Dictionary<Light, Color>();
        private readonly Dictionary<Light, float> lampBaseIntensity = new Dictionary<Light, float>();
        private Material[] bulbMats;
        private Material[] shadeMats;
        private int bulbStage = -1;
        private Color moodSky, moodEquator, moodGround, moodWindow;
        private float moodWindowI;

        /// <summary>目前套用到房屋的光色階段（測試用）。</summary>
        public int LightStage { get { return HouseStage; } }

        private void ResetMood()
        {
            moodSnap = true;
            moodColor.Clear();
            moodIntensity.Clear();
            lampBaseColor.Clear();
            lampBaseIntensity.Clear();
            bulbMats = null;
            shadeMats = null;
            bulbStage = -1;
        }

        private void Blend(Light light, Color target, float targetI, float k, out Color c, out float i)
        {
            Color pc;
            float pi;
            if (moodSnap || !moodColor.TryGetValue(light, out pc)) pc = target;
            if (moodSnap || !moodIntensity.TryGetValue(light, out pi)) pi = targetI;
            c = Color.Lerp(pc, target, k);
            i = Mathf.Lerp(pi, targetI, k);
            moodColor[light] = c;
            moodIntensity[light] = i;
        }

        private void UpdateMoodExtras(int stage, float k)
        {
            var L = layout;
            // 小燈：H1 失溫、H2 失效或不合常理、H3 不再亮回來。
            for (int n = 0; n < L.LampLights.Count; n++)
            {
                var lamp = L.LampLights[n];
                if (lamp == null) continue;
                if (!lampBaseColor.ContainsKey(lamp))
                {
                    lampBaseColor[lamp] = lamp.color;
                    lampBaseIntensity[lamp] = lamp.intensity;
                }
                Color tc;
                float ti;
                Case01Mood.Lamp(stage, n, lampBaseColor[lamp], lampBaseIntensity[lamp], out tc, out ti);
                Color c;
                float i;
                Blend(lamp, tc, ti, k, out c, out i);
                lamp.color = c;
                lamp.intensity = i;
            }
            // 吊燈燈泡的自發光（材質也要冷下來，不只是光源）。
            if (bulbStage != stage)
            {
                bulbStage = stage;
                if (bulbMats == null && palette != null)
                {
                    bulbMats = new Material[4];
                    shadeMats = new Material[4];
                    for (int s = 1; s < 4; s++)
                    {
                        bulbMats[s] = palette.Instance("glow");
                        var e = Case01Mood.BulbEmission(s);
                        bulbMats[s].color = e;
                        ArenaArt.SetEmission(bulbMats[s], e);
                        // 燈罩：H1 微暖、H2／H3 灰冷，不再是透著暖光的橘色。
                        shadeMats[s] = palette.Instance("lamp_shade");
                        var shadeColor = Case01Mood.ShadeColor(s);
                        shadeMats[s].color = shadeColor;
                        ArenaArt.SetEmission(shadeMats[s], shadeColor * Case01Mood.ShadeGlow(s));
                    }
                }
                foreach (var b in L.Bulbs)
                    if (b != null) b.sharedMaterial = stage == 0 || bulbMats == null ? palette.Get("glow") : bulbMats[stage];
                foreach (var sh in L.Shades)
                    if (sh != null) sh.sharedMaterial = stage == 0 || shadeMats == null ? palette.Get("lamp_shade") : shadeMats[stage];
            }
            // 窗邊冷光（方向光）。
            Color wc;
            float wi;
            Case01Mood.WindowLight(stage, out wc, out wi);
            if (moodSnap)
            {
                moodWindow = wc;
                moodWindowI = wi;
            }
            moodWindow = Color.Lerp(moodWindow, wc, k);
            moodWindowI = Mathf.Lerp(moodWindowI, wi, k);
            if (skyLight != null)
            {
                skyLight.color = moodWindow;
                skyLight.intensity = moodWindowI;
            }
            // 環境光（只在已套用房屋氛圍時；離開時由 RestoreAtmosphere 還原）。
            if (atmosphereSaved)
            {
                Color sky, eq, gr;
                Case01Mood.Ambient(stage, out sky, out eq, out gr);
                if (moodSnap)
                {
                    moodSky = sky;
                    moodEquator = eq;
                    moodGround = gr;
                }
                moodSky = Color.Lerp(moodSky, sky, k);
                moodEquator = Color.Lerp(moodEquator, eq, k);
                moodGround = Color.Lerp(moodGround, gr, k);
                RenderSettings.ambientSkyColor = moodSky;
                RenderSettings.ambientEquatorColor = moodEquator;
                RenderSettings.ambientGroundColor = moodGround;
            }
        }

        /// <summary>舊化痕跡：玩家不在該房間時才切換（不當面硬閃）；重建時直接套用。</summary>
        private void UpdateAging(bool force)
        {
            var a = layout.Aging;
            if (a == null) return;
            int stage = HouseStage;
            foreach (var room in a.Rooms)
            {
                int level = Case01Mood.AgingLevel(stage, room);
                if (level == a.LevelOf(room)) continue;
                if (force || room != currentRoom) a.Apply(room, level);
            }
        }

        private void UpdateLights(float dt)
        {
            var L = layout;
            var d = L.Decor;
            float flick = FlickerScale;
            int stage = HouseStage;
            float k = moodSnap ? 1f : 1f - Mathf.Exp(-dt * MoodBlendRate);
            foreach (var kv in L.RoomLights)
            {
                var room = kv.Key;
                var light = kv.Value;
                Color tc;
                float ti;
                Case01Mood.RoomLight(stage, room, L.RoomLightColor[room], L.RoomLightIntensity[room], out tc, out ti);
                Color c;
                float i;
                Blend(light, tc, ti, k, out c, out i);
                switch (room)
                {
                    case Case01Room.Dining:
                        // H1～H2：亮度慢慢起伏（週期約 10 秒）；H3 起停止。
                        if (d.DiningChanged && !d.DiningStable) i *= 1f + 0.06f * flick * Mathf.Sin(clock * 0.6f);
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
            UpdateMoodExtras(stage, k);
            moodSnap = false;
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
            hud.Base.Objective.text = (qaMode == Case01QaMode.Demo ? "<b>〈第十七號門〉十七號屋內</b>" : "<b>〈第十七號門〉室內 Graybox QA v0.1</b>") +
                                      "\n<color=#E6BD52>目標：</color>" + ObjectiveText();
        }

        public string ObjectiveText()
        {
            var s = flow.Session;
            if (Mode == Case01Mode.Ended) return "QA 結束。F1 可從玄關重玩。";
            bool b = floorplan == Case01Floorplan.V04;
            bool a5 = floorplan == Case01Floorplan.V05A;
            string stairs = a5 ? "經餐廳進廚房，樓梯在廚房東南角，往南上樓" : b ? "樓梯在玄關東北角，往南上樓" : "樓梯在玄關東側";
            if (!s.Has(Case01Flags.TableTwo))
                return a5 ? "先在一樓走走。客廳在玄關左手邊，餐廳在客廳裡面。" : b ? "先在一樓走走。餐廳在客廳東側（從玄關經廚房、客廳過去）。" : "先在一樓走走。餐廳在玄關北側。";
            if (!s.HasJournal("table_rewritten") && !s.Has(Case01Flags.EllieStudyKnown))
                return "離開餐廳，到別的房間走一圈再回來看看。（二樓：" + stairs + "）";
            if (!s.Has(Case01Flags.EllieStudyKnown) || !s.Has(Case01Flags.OldCorrectionRecord)) return "上二樓，調查書房裡的資料。（" + stairs + "）";
            if (!flow.WrongConnectionActive) return "離開書房，到樓梯平台看看。";
            if (!s.Has(Case01Flags.RouteMismatchSeen)) return "從樓梯平台走進書房門，親自確認它通往哪裡。";
            if (!flow.StairRouteRestored) return "回到樓梯平台，依艾琳的記號覆寫書房門框（E）。";
            if (s.Stage < Case01Stage.MezzanineDiscovered) return "走進樓梯平台外牆上打開的第三扇門。";
            if (!flow.DefenseTriggered) return "調查夾層中央的核。";
            if (!s.Has(Case01Flags.BattleWon)) return "擊退異常中庭裡的失序體。";
            string back = currentRoom == Case01Room.Mezzanine || currentRoom == Case01Room.Courtyard ? "" : "（從樓梯平台的第三扇門回夾層）";
            if (currentRoom == Case01Room.Courtyard && !flow.CoreStabilized && !s.Has(Case01Flags.MissingLinkRestored)) return "穿過南側的裂口回到夾層，再調查核。";
            if (flow.Correction == Case01CorrectionMode.RevisedV03 && flow.CoreStabilized)
            {
                if (!flow.FamilyPhotoCollected) return "夾層裡多了一張照片（可選）。巡查完再從玄關前門離開十七號。";
                return "可以再巡查一下房子，然後從玄關前門離開十七號。";
            }
            if (flow.Correction == Case01CorrectionMode.RevisedV03 && !flow.CoreStabilized)
            {
                if (!flow.V03EvidenceReady) return "用核旁的校正裝置（E）。資料不足時，先補看舊地圖與合照。" + back;
                if (flow.PreviewConflict != 0) return "裝置在預演矛盾：撤銷後重新判斷。" + back;
                if (!flow.V03Anchored) return "用核旁的校正裝置（E）比對現況與記錄，選一條可信的關係當錨點。" + back;
                if (!flow.V03SecondLink) return "△ 已固定。用裝置處理仍在跳動的 ○。" + back;
                return "走過 △ 門或北牆的 17 號門，把校正寫回。";
            }
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
            if (qaMode == Case01QaMode.Demo)
            {
                // Demo：不顯示 QA 狀態列。
                var panel = hud.QaText.transform.parent.gameObject;
                if (panel.activeSelf) panel.SetActive(false);
                return;
            }
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
            // v0.4：環境光接著依 H0～H3 套用（直接到位）。
            moodSnap = true;
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
