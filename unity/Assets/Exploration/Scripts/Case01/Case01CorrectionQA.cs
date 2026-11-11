using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// 現實校正 × 沉浸式操作 QA（選單「Shepherd/Case01/現實校正沉浸式玩法 QA」，場景 Case01RealityCorrectionQA.unity）。
    /// 內部直接沿用 Phase 1.9 QA（R3 住宅、真門、光影、聲音、FOV／眼高／步速切換），在廚房＋後廚加上 <see cref="Case01CorrectionSlice"/>。
    /// 只有這個 QA 會建立它：正式 Demo、Phase 1.8／1.9 QA、全域玩家控制、戰鬥都不受影響。
    /// 操作：左鍵 校正（只在失序區域內）、右鍵 牽引拾取／放下、Q 取消牽引、E 調查、R 重置謎題、P 走到廚房門口、[ ] 校正秒數、H 面板；
    /// F1 回前門、F3 H0～H3、F4 聲音、F5 FOV、F6 步速、F7 眼高、F9 光影、F12 鏡頭微動（沿用 Phase 1.8／1.9）。
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class Case01CorrectionQA : MonoBehaviour
    {
        public static readonly Vector3 KitchenDoorPose = new Vector3(5.7f, 0.05f, 10.9f);

        [SerializeField] private float duration = 5f;
        public Case01ImmersionQA Host { get; private set; }
        public Case01CorrectionSlice Slice { get; private set; }
        public Case01HouseQA House { get { return Host != null ? Host.House : null; } }
        public float Duration { get { return duration; } }
        public bool PanelOn { get; private set; } = true;
        public int Rebuilds { get; private set; }

        private bool built, lastCaptured;

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
            var prevPost = Host.House.LayoutPostBuild;
            Host.House.LayoutPostBuild = l =>
            {
                if (prevPost != null) prevPost(l);
                BuildSlice(l);
            };
            var prevInteract = Host.House.CustomInteract;
            Host.House.CustomInteract = it => Interact(it) || (prevInteract != null && prevInteract(it));
            BuildSlice(Host.House.Layout);
            Host.SetPanelVisible(false);
            BuildUi();
            RefreshUi();
        }

        private void BuildSlice(Case01HouseLayout l)
        {
            if (Slice != null) Slice.Dispose();
            Slice = null;
            // 只在 Phase 1.9 R3 上建立（F2 切到 1.8 R2 時不建立）。
            if (l == null || l.Floorplan != Case01Floorplan.R3 || Host.World == null) return;
            Slice = new Case01CorrectionSlice(l, l.Palette, Host.World.Art != null ? Host.World.Art.Kit : null) { Duration = duration };
            Slice.AttachDevice(Host.House.ExploreCamera);
            Rebuilds++;
        }

        private bool Interact(Case01Interactable it)
        {
            if (Slice == null || it == null || !it.Id.StartsWith("rc_")) return false;
            var text = Slice.Describe(it.Id);
            if (text == null) return false;
            House.Hud.ShowNote(text);
            return true;
        }

        // ---------------- 操作（Play 讀滑鼠；測試直接呼叫） ----------------

        public Case01CorrectResult Correct()
        {
            if (Slice == null) return Case01CorrectResult.NotAvailable;
            var p = House.Player;
            return Slice.TryCorrect(p.EyePosition, p.ViewRotation * Vector3.forward);
        }

        public Case01TetherResult Tether()
        {
            if (Slice == null) return Case01TetherResult.NotAvailable;
            var p = House.Player;
            return Slice.TryTether(p.EyePosition, p.ViewRotation * Vector3.forward);
        }

        public void ResetPuzzle()
        {
            if (Slice != null) Slice.ResetPuzzle();
            if (House != null) House.Hud.Base.ShowToast("QA：謎題重置（櫃子倒下、椅子回到窗邊）");
        }

        public void SetDuration(float sec)
        {
            duration = Mathf.Clamp(Mathf.Round(sec * 2f) / 2f, 3f, 8f);
            if (Slice != null) Slice.Duration = duration;
        }

        public void GoToKitchenDoor()
        {
            House.Player.Teleport(KitchenDoorPose, 0f, 0f);
        }

        private void Update()
        {
            if (!built || House == null) return;
            bool captured = House.CursorCaptured;
            bool exploring = House.Mode == Case01Mode.Exploring && !House.Hud.PanelVisible;
            // 點擊畫面重新鎖定滑鼠的那一下不算校正。
            if (Slice != null && captured && lastCaptured && exploring)
            {
                if (Input.GetMouseButtonDown(0)) Correct();
                if (Input.GetMouseButtonDown(1)) Tether();
                if (Input.GetKeyDown(KeyCode.Q)) Slice.Cancel();
                if (Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f) Slice.AdjustHold(Input.mouseScrollDelta.y * 0.15f);
            }
            lastCaptured = captured;
            if (Input.GetKeyDown(KeyCode.R)) ResetPuzzle();
            if (Input.GetKeyDown(KeyCode.P)) GoToKitchenDoor();
            if (Input.GetKeyDown(KeyCode.H)) PanelOn = !PanelOn;
            if (Input.GetKeyDown(KeyCode.LeftBracket)) SetDuration(duration - 0.5f);
            if (Input.GetKeyDown(KeyCode.RightBracket)) SetDuration(duration + 0.5f);
        }

        private void LateUpdate()
        {
            if (!built || House == null) return;
            Frame(Time.deltaTime);
        }

        /// <summary>每影格（Play：LateUpdate，在 Phase 1.9 QA 之後；測試直接呼叫）。</summary>
        public void Frame(float dt)
        {
            if (Slice != null) Slice.Tick(dt, House.Player, House.ExploreCamera, House.Mode == Case01Mode.Exploring, Host.Options.AudioOn);
            House.Hud.Base.ViewMode.text = "現實校正沉浸式玩法 QA｜H 顯示／隱藏面板";
            RefreshUi();
        }

        // ---------------- UI：準星（只在失序區域）＋QA 面板 ----------------

        private Canvas canvas;
        private Text reticle, hint, panel;
        private RectTransform panelRect;

        private void BuildUi()
        {
            var cgo = new GameObject("Case01CorrectionQaCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 41;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)cgo.transform;
            var r = Case01SpatialQA.Rect(root, "Reticle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
            reticle = Case01SpatialQA.Txt(r, 26, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.85f, 0.75f));
            var hr = Case01SpatialQA.Rect(root, "AimHint", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -46f), new Vector2(420f, 34f));
            hint = Case01SpatialQA.Txt(hr, 19, TextAnchor.MiddleCenter, new Color(0.95f, 0.9f, 0.78f, 0.85f));
            panelRect = Case01SpatialQA.Rect(root, "CorrectionPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -98f), new Vector2(560f, 330f));
            Case01SpatialQA.Img(panelRect, new Color(0.03f, 0.035f, 0.045f, 0.72f));
            panel = Case01SpatialQA.Txt(panelRect, 17, TextAnchor.UpperLeft, new Color(0.88f, 0.9f, 0.92f));
            Case01SpatialQA.Inset(panel.rectTransform, 12f, 8f);
        }

        public string ReticleText { get { return reticle != null && reticle.enabled ? reticle.text : null; } }
        public string HintText { get { return hint != null && hint.enabled ? hint.text : null; } }

        private void RefreshUi()
        {
            if (panel == null || House == null || House.Player == null) return;
            bool exploring = House.Mode == Case01Mode.Exploring;
            bool zone = Slice != null && Slice.PlayerInZone && exploring;
            // 正常區域沒有任何攻擊／校正提示；進入失序區域才出現準星與按鍵提示。
            reticle.enabled = zone;
            hint.enabled = zone && Slice.AimHint != null;
            if (zone)
            {
                bool target = Slice.AimHint != null;
                reticle.text = Slice.Holding ? "◇" : target ? "◎" : "·";
                reticle.color = Slice.Holding ? (Slice.GhostOnMark ? new Color(1f, 0.82f, 0.35f) : Slice.GhostValid ? new Color(0.55f, 0.95f, 0.65f) : new Color(1f, 0.4f, 0.35f)) : target ? new Color(1f, 0.85f, 0.55f, 0.95f) : new Color(0.95f, 0.92f, 0.85f, 0.6f);
                hint.text = Slice.AimHint ?? "";
            }
            panelRect.gameObject.SetActive(PanelOn);
            if (!PanelOn) return;
            var sb = new StringBuilder();
            sb.Append("<b>現實校正沉浸式玩法 QA</b>（Phase 1.9 R3 廚房＋後廚）\n");
            if (Slice == null) sb.Append("<color=#E6A050>只在 Phase 1.9 版建立（F2 切回）</color>\n");
            else
            {
                string st;
                switch (Slice.State)
                {
                    case Case01CabinetState.Fallen: st = "倒下（擋住拱口）"; break;
                    case Case01CabinetState.Rising: st = "立起中"; break;
                    case Case01CabinetState.Upright: st = "校正中 " + Mathf.Max(0f, Slice.TimeLeft).ToString("0.0") + " 秒" + (Slice.TimeLeft <= 0f ? "（有人在倒下路徑上，等待）" : ""); break;
                    case Case01CabinetState.Falling: st = "倒下中"; break;
                    default: st = "<color=#E6BD52>局部穩定</color>"; break;
                }
                sb.Append("高櫃：").Append(st).Append("｜校正時間 ").Append(duration.ToString("0.0")).Append(" 秒（[ ] 調整）\n");
                sb.Append("椅子：").Append(Slice.Holding ? "牽引中" : Slice.ChairOnMark ? "<color=#E6BD52>在凹痕上</color>" : "不在原位").Append("｜線索：照片 ").Append(Slice.PhotoSeen ? "✓" : "—").Append("　凹痕 ").Append(Slice.MarksSeen ? "✓" : "—")
                    .Append("｜").Append(Slice.PlayerInZone ? "<color=#C79BFF>失序區域內</color>" : "正常區域").Append('\n');
            }
            sb.Append("垂直 FOV ").Append(Host.FieldOfView.ToString("0")).Append("°｜眼高 ").Append(Host.EyeHeight.ToString("0.00")).Append(" m｜").Append(Host.SpeedText()).Append('\n');
            sb.Append(Case01Cursor.Looking(House.CursorCaptured) ? "滑鼠鎖定" : "<color=#E6A050>滑鼠已釋放：點擊畫面恢復視角</color>").Append('\n');
            sb.Append("<color=#9AA4B0>WASD 走｜Shift 跑｜E 開門／調查｜左鍵 校正｜右鍵 牽引／放下｜滾輪 遠近｜Q 取消牽引｜R 重置謎題｜P 到廚房門口｜F1 回前門｜F3 H0～H3｜F4 聲音｜F5 FOV｜F6 步速｜F7 眼高｜F9 光影｜F12 鏡頭微動｜H 面板｜Esc 釋放滑鼠</color>");
            panel.text = sb.ToString();
        }

        private void OnDestroy()
        {
            if (Slice != null) Slice.Dispose();
        }
    }
}
