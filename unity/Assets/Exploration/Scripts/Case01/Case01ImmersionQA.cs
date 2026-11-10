using System.Collections.Generic;
using System.Text;
using Shepherd.Battle.UI;
using Shepherd.Battle.UI.Arena;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// Phase 1.8 沉浸式住宅與失序空間 QA（選單「Shepherd/Case01/Phase 1.8 沉浸式住宅與失序空間 QA」）。
    /// R2 格局，從前門開始真實走動：可切「原 R2 測試屋」↔「沉浸版」（同比例、同眼高、同 FOV、同步速、同位置），
    /// 沉浸版有真門、逐房光影、3D 環境聲與腳步、輕互動、第一人稱手勢、H2 空間異變。劇情、互動、戰鬥、核校正照舊。
    /// </summary>
    public sealed class Case01ImmersionQA : MonoBehaviour
    {
        public static readonly float[] Fovs = { 55f, 60f, 65f, 70f };
        public static readonly float[] EyeHeights = { 1.58f, 1.65f };
        public static readonly Vector2[] Speeds = { new Vector2(2.2f, 3.8f), new Vector2(2.6f, 4.4f), new Vector2(1.8f, 3.2f), new Vector2(FirstPersonController.WalkSpeed, FirstPersonController.RunSpeed) };

        public Case01HouseQA House { get; private set; }
        public Case01ImmersionWorld World { get; private set; }
        public readonly Case01ImmersionWorld.Options Options = new Case01ImmersionWorld.Options();
        public bool Immersive { get; private set; } = true;
        public int FovIndex { get; private set; }
        public int EyeIndex { get; private set; }
        public int SpeedIndex { get; private set; }
        public bool DoorsAllOpen { get; private set; }
        public int Stage { get; private set; }
        public string LastToast { get; private set; }
        public float RunClock { get; private set; }
        public readonly List<KeyValuePair<Case01Room, float>> Arrivals = new List<KeyValuePair<Case01Room, float>>();

        public float FieldOfView { get { return Fovs[FovIndex]; } }
        public float EyeHeight { get { return EyeHeights[EyeIndex]; } }
        public Vector2 Speed { get { return Speeds[SpeedIndex]; } }

        private bool built, playMode;
        private Case01HouseLayout seenLayout;
        private FirstPersonController seenPlayer;
        private Case01Room lastRoom;

        private void Awake()
        {
            if (Application.isPlaying) Setup(true);
        }

        public void Setup(bool play, CombatLabScenario scenario = null, int? seed = null, bool immersive = true, Case01QaMode mode = Case01QaMode.CoreV03)
        {
            if (built) return;
            built = true;
            playMode = play;
            Immersive = immersive;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            var go = new GameObject("Case01HouseQA");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            House = go.AddComponent<Case01HouseQA>();
            House.InputSource = new Filter(new LegacyCase01Input(), this);
            House.LayoutPostBuild = l =>
            {
                if (World != null) World.Dispose();
                World = Immersive ? Case01ImmersionWorld.Build(l, l.Palette, Options) : null;
            };
            House.CustomInteract = it => World != null && World.Interact(it, House);
            House.Build(play, scenario, seed, Case01Checkpoint.S00, mode, Case01Floorplan.R2);
            go.SetActive(true);
            BuildUi();
            AfterRebuild();
            ResetRun();
        }

        public void UseInput(ICase01InputSource source)
        {
            House.InputSource = new Filter(source, this);
        }

        private sealed class Filter : ICase01InputSource
        {
            private readonly ICase01InputSource inner;
            private readonly Case01ImmersionQA owner;

            public Filter(ICase01InputSource inner, Case01ImmersionQA owner)
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
                    else if (Input.GetKeyDown(KeyCode.G)) key = 13;
                }
                if (key > 0 && key != 8) owner.Press(key);
                return r;
            }
        }

        // ---------------- 按鍵 ----------------

        /// <summary>1～12＝F1～F12，13＝G（錨點痕跡）。</summary>
        public void Press(int key)
        {
            switch (key)
            {
                case 1:
                    Place(House.Layout.Spawn.Position, House.Layout.Spawn.Yaw, 0f);
                    ResetRun();
                    Toast("QA：回到前門內（劇情進度不變；路線計時重新開始）");
                    break;
                case 2:
                    Immersive = !Immersive;
                    RebuildHere(House.Flow);
                    Toast(Immersive ? "QA：沉浸版（真門、光影、聲音、生活痕跡、空間異變）" : "QA：原 R2 測試屋（Phase 1.7，同位置同視角）");
                    break;
                case 3:
                    Stage = (Stage + 1) % 4;
                    RebuildHere(Case01Flow.ForHouseStage(Stage));
                    Toast("QA：房屋 H" + Stage + "（站在原地）");
                    break;
                case 4:
                    Options.AudioOn = !Options.AudioOn;
                    Toast(Options.AudioOn ? "QA：聲音　開" : "QA：聲音　關");
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
                    Toast("QA：眼高 " + EyeHeight.ToString("0.00") + " m");
                    break;
                case 9:
                    Options.NewLighting = !Options.NewLighting;
                    RebuildHere(House.Flow);
                    Toast(Options.NewLighting ? "QA：新光影" : "QA：舊光影（Phase 1.7 的吊燈）");
                    break;
                case 10:
                    DoorsAllOpen = !DoorsAllOpen;
                    if (World != null)
                    {
                        if (DoorsAllOpen) World.OpenAllDoors();
                        else World.ResetDoors();
                    }
                    Toast(DoorsAllOpen ? "QA：門全部打開（診斷用）" : "QA：門回到正常初始（關／半掩）");
                    break;
                case 11:
                    Options.Anomaly = !Options.Anomaly;
                    Toast(Options.Anomaly ? "QA：空間異變　新版（H2 時走廊那扇門改通往廚房）" : "QA：空間異變　原版（房子不變形）");
                    break;
                case 12:
                    Options.ReducedMotion = !Options.ReducedMotion;
                    Toast(Options.ReducedMotion ? "QA：減少鏡頭微動（關閉步態與手勢）" : "QA：鏡頭步態與手勢　開");
                    break;
                case 13:
                    if (World != null)
                    {
                        World.ToggleAnchor(House.Player.transform.position);
                        Toast(World.AnchorOn ? "QA：在這裡固定錨點（原型）——走過的路會留下一道淡線" : "QA：錨點痕跡清除");
                    }
                    break;
            }
            ApplyPlayer();
            RefreshUi();
        }

        public string SpeedText()
        {
            var s = Speed;
            return "步行 " + s.x.ToString("0.0") + "／奔跑 " + s.y.ToString("0.0") + " m/s" + (SpeedIndex == Speeds.Length - 1 ? "（目前正式值）" : "（候選，未定案）");
        }

        private void RebuildHere(Case01Flow f)
        {
            var p = House.Player;
            var pos = p.transform.position;
            float yaw = p.Yaw, pitch = p.Pitch;
            House.QaView(f, new Case01Pose(pos, yaw), pitch);
            AfterRebuild();
            if (!Free(pos)) Nudge(pos);
        }

        private void Place(Vector3 pos, float yaw, float pitch)
        {
            House.Player.Teleport(pos, yaw, pitch);
            SnapCamera();
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

        private void Nudge(Vector3 pos)
        {
            for (float rad = 0.15f; rad <= 1.6f; rad += 0.15f)
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI / 8f;
                var c = pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rad;
                if (House.Layout.RoomAt(c) == House.Layout.RoomAt(pos) && Free(c))
                {
                    Place(c, House.Player.Yaw, House.Player.Pitch);
                    return;
                }
            }
        }

        private void SnapCamera()
        {
            var cam = House.ExploreCamera;
            cam.transform.position = House.Player.EyePosition;
            cam.transform.rotation = House.Player.ViewRotation;
            cam.fieldOfView = FieldOfView;
        }

        private void ApplyPlayer()
        {
            var p = House.Player;
            if (p == null) return;
            p.WalkSpeedValue = Speed.x;
            p.RunSpeedValue = Speed.y;
            p.EyeHeightValue = EyeHeight;
            p.Acceleration = Options.ReducedMotion ? 0f : 12f;
        }

        private void AfterRebuild()
        {
            seenLayout = House.Layout;
            seenPlayer = House.Player;
            if (World != null)
            {
                World.Attach(House.ExploreCamera);
                if (DoorsAllOpen) World.OpenAllDoors();
            }
            ApplyPlayer();
            SnapCamera();
            RefreshUi();
        }

        private void ResetRun()
        {
            Arrivals.Clear();
            RunClock = 0f;
            lastRoom = House.Layout.RoomAt(House.Player.transform.position);
            Arrivals.Add(new KeyValuePair<Case01Room, float>(lastRoom, 0f));
        }

        private void Toast(string s)
        {
            LastToast = s;
            if (House != null && House.Hud != null) House.Hud.Base.ShowToast(s);
        }

        // ---------------- 每影格 ----------------

        private void LateUpdate()
        {
            if (!built || House == null || House.Layout == null) return;
            Frame(Time.deltaTime);
            if (World != null) World.LateCamera(House.ExploreCamera, House.Player, House.Mode == Case01Mode.Exploring);
        }

        /// <summary>每影格（Play：LateUpdate；測試可直接呼叫）：重建偵測、玩家參數、FOV、沉浸層、計時、面板。</summary>
        public void Frame(float dt)
        {
            if (House.Layout != seenLayout || House.Player != seenPlayer) AfterRebuild();
            var cam = House.ExploreCamera;
            if (House.Mode == Case01Mode.Exploring && cam.enabled) cam.fieldOfView = FieldOfView;
            if (World != null) World.Tick(dt, House);
            House.Hud.Base.ViewMode.text = "Phase 1.8 沉浸式住宅 QA｜按鍵見右側面板";
            if (House.Mode == Case01Mode.Exploring)
            {
                RunClock += dt;
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

        // ---------------- UI ----------------

        private Canvas canvas;
        private Text panelText;

        public Canvas QaCanvas { get { return canvas; } }

        private void BuildUi()
        {
            var cgo = new GameObject("Case01ImmersionQaCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var panel = Case01SpatialQA.Rect((RectTransform)cgo.transform, "ImmersionPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -98f), new Vector2(580f, 340f));
            Case01SpatialQA.Img(panel, new Color(0.03f, 0.035f, 0.045f, 0.72f));
            panelText = Case01SpatialQA.Txt(panel, 17, TextAnchor.UpperLeft, new Color(0.88f, 0.9f, 0.92f));
            Case01SpatialQA.Inset(panelText.rectTransform, 12f, 8f);
        }

        public void SetPanelVisible(bool on) { if (canvas != null) canvas.enabled = on; }

        private void RefreshUi()
        {
            if (panelText == null || House == null || House.Player == null) return;
            var p = House.Player;
            var room = House.Layout.RoomAt(p.transform.position);
            var sb = new StringBuilder();
            sb.Append("<b>Phase 1.8 沉浸式住宅與失序空間 QA</b>\n<color=#E6BD52>").Append(Immersive ? "沉浸版（R2）" : "原 R2 測試屋（Phase 1.7）").Append("</color>｜H").Append(House.HouseStage)
                .Append("｜").Append(Case01Texts.RoomName(room)).Append('\n');
            sb.Append("垂直 FOV ").Append(FieldOfView.ToString("0")).Append("°｜眼高 ").Append(EyeHeight.ToString("0.00")).Append(" m｜").Append(SpeedText()).Append('\n');
            sb.Append("聲音 ").Append(Options.AudioOn ? "開" : "關").Append("｜光影 ").Append(Options.NewLighting ? "新" : "舊").Append("｜門 ").Append(DoorsAllOpen ? "全開" : "正常")
                .Append("｜異變 ").Append(Options.Anomaly ? "新版" : "原版").Append("｜微動 ").Append(Options.ReducedMotion ? "關" : "開").Append('\n');
            if (World != null)
            {
                sb.Append("房子：").Append(World.TopologyH2 ? "<color=#C79BFF>走廊那扇門現在通往廚房</color>" : "原本的連接").Append("｜腳步：").Append(World.LastStepSurface ?? "—");
                if (World.AnchorOn) sb.Append("｜錨點痕跡 ").Append(World.TracePoints);
                sb.Append('\n');
            }
            sb.Append(Case01Cursor.Looking(House.CursorCaptured) ? "滑鼠鎖定" : "<color=#E6A050>滑鼠已釋放：點擊畫面恢復視角</color>").Append("｜這一趟 ").Append(RunClock.ToString("0")).Append(" 秒\n");
            sb.Append("<color=#9AA4B0>E 開關門／調查｜F1 回前門｜F2 原版↔沉浸版｜F3 H0～H3｜F4 聲音｜F5 FOV｜F6 步速｜F7 眼高｜F8 降低閃爍｜F9 光影新／舊｜F10 門全開／正常｜F11 異變新／原版｜F12 鏡頭微動｜G 錨點痕跡（原型）｜Esc 釋放滑鼠</color>");
            panelText.text = sb.ToString();
        }

        private void OnDestroy()
        {
            if (World != null) World.Dispose();
        }
    }
}
