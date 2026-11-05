using System.Collections.Generic;
using System.Text;
using Shepherd.Battle.Core;
using Shepherd.Battle.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shepherd.Battle.UI
{
    /// <summary>
    /// Combat Lab v0.1 介面：只下命令給 <see cref="CombatLabSession"/>，並顯示快照、預覽與事件紀錄。
    /// 不含任何戰鬥公式。整個介面在執行時以內建 uGUI 建立，不使用外部素材。
    /// </summary>
    public sealed class CombatLabView : MonoBehaviour
    {
        [Tooltip("可選：指定 BattleConfig 資產調整數值；留空則使用規格預設值。")]
        [SerializeField] private BattleConfig config;

        [Tooltip("固定亂數種子，方便重現問題。")]
        [SerializeField] private int seed = CombatLabScenario.DefaultSeed;

        private static readonly SchoolKind[] Schools = { SchoolKind.Structure, SchoolKind.Process, SchoolKind.Relation };

        private bool redrawMode;
        private bool built;

        private Text topText;
        private Text messageText;
        private Text sequenceTitle;
        private Text previewText;
        private Text logText;
        private ScrollRect logScroll;
        private ScrollRect previewScroll;
        private readonly List<Button> schoolButtons = new List<Button>();
        private readonly List<Button> enemyButtons = new List<Button>();
        private readonly List<Button> sequenceButtons = new List<Button>();
        private readonly List<Button> handButtons = new List<Button>();
        private readonly List<int> handIds = new List<int>();
        private Button previewButton;
        private Button commitButton;
        private Button redrawButton;
        private Button clearButton;
        private Button resetButton;
        private InputField seedInput;
        private RectTransform enemyList;

        public CombatLabSession Session { get; private set; }
        public bool RedrawMode { get { return redrawMode; } }

        // 供自動化測試使用的唯讀存取。
        public string TopText { get { return topText.text; } }
        public string MessageText { get { return messageText.text; } }
        public string PreviewText { get { return previewText.text; } }
        public string LogText { get { return logText.text; } }
        public IReadOnlyList<Button> SchoolButtons { get { return schoolButtons; } }
        public IReadOnlyList<Button> EnemyButtons { get { return enemyButtons; } }
        public IReadOnlyList<Button> SequenceButtons { get { return sequenceButtons; } }
        public IReadOnlyList<Button> HandButtons { get { return handButtons; } }
        public IReadOnlyList<int> HandIds { get { return handIds; } }
        public Button PreviewButton { get { return previewButton; } }
        public Button CommitButton { get { return commitButton; } }
        public Button RedrawButton { get { return redrawButton; } }
        public Button ClearButton { get { return clearButton; } }
        public Button ResetButton { get { return resetButton; } }
        public InputField SeedInput { get { return seedInput; } }

        private void Awake()
        {
            Build(true);
        }

        private void OnDestroy()
        {
            if (Session != null) Session.Changed -= Refresh;
        }

        /// <summary>建立介面與戰鬥。createEventSystem＝false 供 EditMode 測試使用。</summary>
        public void Build(bool createEventSystem)
        {
            if (built) return;
            built = true;

            var rules = config != null ? RuleSetCompiler.Compile(config) : RuleSet.CreateDefault();
            Session = new CombatLabSession(rules, CombatLabScenario.Default(), seed);
            Session.Changed += Refresh;

            if (createEventSystem && FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }

            BuildLayout();
            Refresh();
        }

        // ---------------- 版面 ----------------

        private void BuildLayout()
        {
            var canvasGo = new GameObject("CombatLabCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasGo.layer = 5;
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;

            var bg = CombatLabUi.Panel("Background", root, CombatLabUi.Background);
            CombatLabUi.Stretch(bg);

            // 戰鬥資訊列
            var top = CombatLabUi.Panel("BattleInfo", root, CombatLabUi.PanelColor);
            CombatLabUi.Anchor(top, 0f, 0.93f, 1f, 1f);
            topText = CombatLabUi.Label("Info", top, "", 22, TextAnchor.MiddleLeft);
            CombatLabUi.Stretch(topText.rectTransform, 10f);

            var msg = CombatLabUi.Panel("Message", root, new Color(0.13f, 0.14f, 0.16f, 1f));
            CombatLabUi.Anchor(msg, 0f, 0.885f, 0.68f, 0.93f);
            messageText = CombatLabUi.Label("Text", msg, "", 18, TextAnchor.MiddleLeft, CombatLabUi.Highlight);
            CombatLabUi.Stretch(messageText.rectTransform, 10f);

            BuildLeft(root);
            BuildCenter(root);
            BuildBottom(root);
            BuildLog(root);
        }

        private void BuildLeft(RectTransform root)
        {
            var left = CombatLabUi.Panel("SchoolAndEnemies", root, CombatLabUi.PanelColor);
            CombatLabUi.Anchor(left, 0f, 0.34f, 0.30f, 0.885f);
            CombatLabUi.Vertical(left);

            CombatLabUi.Size(CombatLabUi.Label("SchoolTitle", left, "學派（確認施放時鎖定）", 18, TextAnchor.MiddleLeft,
                CombatLabUi.MutedText), 26);
            var row = CombatLabUi.Rect("SchoolRow", left);
            CombatLabUi.Horizontal(row);
            CombatLabUi.Size(row, 52);
            foreach (var s in Schools)
            {
                var school = s;
                schoolButtons.Add(CombatLabUi.Button("School_" + s, row, CombatLabText.School(s),
                    () => Session.SelectSchool(school), 20));
            }

            CombatLabUi.Size(CombatLabUi.Label("EnemyTitle", left, "敵人（點擊選為目標）", 18, TextAnchor.MiddleLeft,
                CombatLabUi.MutedText), 26);
            enemyList = CombatLabUi.Rect("EnemyList", left);
            CombatLabUi.Vertical(enemyList, 6f, 0);
            CombatLabUi.Size(enemyList, -1, 1);
            BuildEnemyButtons();
        }

        private void BuildEnemyButtons()
        {
            foreach (var b in enemyButtons) CombatLabUi.Kill(b.gameObject);
            enemyButtons.Clear();
            foreach (var e in Session.Snapshot().Enemies)
            {
                int id = e.Id;
                var b = CombatLabUi.Button("Enemy_" + id, enemyList, "", () => Session.SelectTarget(id), 17);
                var t = b.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                CombatLabUi.Size(b, 104);
                enemyButtons.Add(b);
            }
        }

        private void BuildCenter(RectTransform root)
        {
            var center = CombatLabUi.Panel("SequenceAndPreview", root, CombatLabUi.PanelColor);
            CombatLabUi.Anchor(center, 0.30f, 0.34f, 0.68f, 0.885f);
            CombatLabUi.Vertical(center);

            sequenceTitle = CombatLabUi.Label("SequenceTitle", center, "", 18, TextAnchor.MiddleLeft, CombatLabUi.MutedText);
            CombatLabUi.Size(sequenceTitle, 26);
            for (int i = 0; i < RuleSet.ActionPoints; i++)
            {
                int index = i;
                var b = CombatLabUi.Button("Slot_" + i, center, "", () => Session.RemoveAt(index), 18);
                b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                CombatLabUi.Size(b, 44);
                sequenceButtons.Add(b);
            }
            clearButton = CombatLabUi.Button("ClearPlan", center, "清空編排", () => Session.ClearPlan(), 16);
            CombatLabUi.Size(clearButton, 34);

            CombatLabUi.Size(CombatLabUi.Label("PreviewTitle", center, "預覽（呼叫戰鬥核心，不改動真實狀態）", 18,
                TextAnchor.MiddleLeft, CombatLabUi.MutedText), 26);
            previewText = CombatLabUi.ScrollText("Preview", center, 16, out previewScroll);
            CombatLabUi.Size(previewScroll, -1, 1);
        }

        private void BuildBottom(RectTransform root)
        {
            var bottom = CombatLabUi.Panel("HandAndActions", root, CombatLabUi.PanelColor);
            CombatLabUi.Anchor(bottom, 0f, 0f, 0.68f, 0.34f);
            CombatLabUi.Vertical(bottom);

            CombatLabUi.Size(CombatLabUi.Label("HandTitle", bottom, "手牌（點擊加入／移除編排；調度模式下點擊要洗回的牌）", 18,
                TextAnchor.MiddleLeft, CombatLabUi.MutedText), 26);
            var hand = CombatLabUi.Rect("Hand", bottom);
            CombatLabUi.Horizontal(hand, 8f);
            CombatLabUi.Size(hand, -1, 1);
            for (int i = 0; i < RuleSet.HandSize; i++)
            {
                int slot = i;
                var b = CombatLabUi.Button("Hand_" + i, hand, "", () => OnHandClicked(slot), 18);
                handButtons.Add(b);
            }

            var actions = CombatLabUi.Rect("Actions", bottom);
            CombatLabUi.Horizontal(actions, 8f);
            CombatLabUi.Size(actions, 58);
            previewButton = CombatLabUi.Button("Preview", actions, "預覽", () => Session.Preview(), 20);
            commitButton = CombatLabUi.Button("Commit", actions, "", () => Session.Commit(), 20);
            redrawButton = CombatLabUi.Button("Redraw", actions, "", ToggleRedrawMode, 18);
            seedInput = CombatLabUi.Input("Seed", actions, Session.Seed.ToString(), "種子");
            resetButton = CombatLabUi.Button("Reset", actions, "重置戰鬥", OnReset, 20);
            CombatLabUi.Size(commitButton, -1, -1, -1, 1.6f);
        }

        private void BuildLog(RectTransform root)
        {
            var right = CombatLabUi.Panel("BattleLog", root, CombatLabUi.PanelColor);
            CombatLabUi.Anchor(right, 0.68f, 0f, 1f, 0.93f);
            CombatLabUi.Vertical(right);
            CombatLabUi.Size(CombatLabUi.Label("LogTitle", right, "戰鬥紀錄（來自 BattleEvent）", 18, TextAnchor.MiddleLeft,
                CombatLabUi.MutedText), 26);
            logText = CombatLabUi.ScrollText("Log", right, 15, out logScroll);
            CombatLabUi.Size(logScroll, -1, 1);
        }

        // ---------------- 操作 ----------------

        private void OnHandClicked(int slot)
        {
            if (slot < 0 || slot >= handIds.Count) return;
            int id = handIds[slot];
            if (redrawMode)
            {
                redrawMode = false;
                Session.Redraw(id);
                return;
            }
            Session.ToggleCard(id);
        }

        private void ToggleRedrawMode()
        {
            if (Session.IsOver || Session.Snapshot().RedrawsRemaining <= 0) return;
            redrawMode = !redrawMode;
            Refresh();
        }

        private void OnReset()
        {
            int s;
            if (!int.TryParse(seedInput.text, out s)) s = seed;
            redrawMode = false;
            Session.Reset(s);
            if (enemyButtons.Count != Session.Snapshot().Enemies.Count)
            {
                BuildEnemyButtons();
                Refresh();
            }
        }

        // ---------------- 顯示 ----------------

        public void Refresh()
        {
            if (topText == null) return;
            var snap = Session.Snapshot();
            bool over = snap.Outcome != BattleOutcome.Ongoing;
            if (over) redrawMode = false;

            string schoolState = snap.LockedSchool.HasValue
                ? CombatLabText.School(snap.LockedSchool.Value) + "（已鎖定）"
                : CombatLabText.School(Session.SelectedSchool) + "（確認時鎖定）";
            topText.text = "第 " + (snap.Round + 1) + " 回合　｜　錨定值 " + snap.Anchor + " / " + Session.Rules.MaxAnchor
                + "　｜　擬態錨定 " + snap.Shield
                + "　｜　AP " + Session.ApRemaining + " / " + Session.ApMax
                + "　｜　學派：" + schoolState
                + "　｜　狀態：" + CombatLabText.Outcome(snap.Outcome)
                + "　｜　調度剩 " + snap.RedrawsRemaining
                + "　｜　牌庫 " + snap.DeckCount + "・棄牌 " + snap.DiscardCount;
            messageText.text = Session.Message ?? "";

            for (int i = 0; i < schoolButtons.Count; i++)
            {
                var s = Schools[i];
                bool selected = s == Session.SelectedSchool;
                bool allowed = !over && (!snap.LockedSchool.HasValue || snap.LockedSchool.Value == s);
                CombatLabUi.SetButton(schoolButtons[i], (selected ? "● " : "") + CombatLabText.School(s),
                    selected ? CombatLabUi.Highlight : CombatLabUi.ButtonColor, allowed);
            }

            RefreshEnemies(snap, over);
            RefreshSequence(over);
            RefreshHand(snap, over);
            RefreshActions(snap, over);
            RefreshPreview(snap);

            logText.text = string.Join("\n", Session.Log);
            Canvas.ForceUpdateCanvases();
            logScroll.verticalNormalizedPosition = 0f;
        }

        private void RefreshEnemies(BattleSnapshot snap, bool over)
        {
            for (int i = 0; i < enemyButtons.Count && i < snap.Enemies.Count; i++)
            {
                var e = snap.Enemies[i];
                bool target = Session.SelectedTargetId.HasValue && Session.SelectedTargetId.Value == e.Id;
                var sb = new StringBuilder();
                sb.Append(target ? "◎ 目標　" : "　　　").Append(e.Name);
                if (e.Resolved)
                {
                    sb.Append("　【已解除】");
                }
                else
                {
                    sb.Append("\n　　　失序值 ").Append(e.Value).Append("　本回合預告攻擊 ").Append(e.ForecastAttack);
                    sb.Append("\n　　　連結 ").Append(e.Links).Append(" / ").Append(Session.Rules.LinkCap)
                      .Append("　校正 ").Append(e.Corrections).Append("　束縛 ").Append(e.Bind);
                }
                Color c = e.Resolved ? new Color(0.2f, 0.2f, 0.22f, 1f) : target ? new Color(0.45f, 0.36f, 0.18f, 1f)
                    : CombatLabUi.ButtonColor;
                CombatLabUi.SetButton(enemyButtons[i], sb.ToString(), c, !over && !e.Resolved);
            }
        }

        private void RefreshSequence(bool over)
        {
            var staged = Session.Staged;
            sequenceTitle.text = "編排序列（依序結算，點擊移除）　已用 AP " + staged.Count + " / " + Session.ApMax;
            for (int i = 0; i < sequenceButtons.Count; i++)
            {
                if (i < staged.Count)
                {
                    var c = staged[i];
                    string target = Session.CardNeedsTarget(c.Element)
                        ? (c.TargetEnemyId.HasValue ? " → " + Session.EnemyName(c.TargetEnemyId.Value) : " → （無目標）")
                        : "　（此學派不需目標）";
                    CombatLabUi.SetButton(sequenceButtons[i],
                        (i + 1) + ". " + CombatLabText.Element(c.Element) + "（"
                        + CombatLabText.EffectName(Session.SelectedSchool, c.Element) + "）" + target + "　✕",
                        ElementColor(c.Element, 0.8f), !over);
                }
                else
                {
                    CombatLabUi.SetButton(sequenceButtons[i], (i + 1) + ". （空）", new Color(0.2f, 0.21f, 0.24f, 1f), false);
                }
            }
            CombatLabUi.SetButton(clearButton, "清空編排", CombatLabUi.ButtonColor, !over && staged.Count > 0);
        }

        private void RefreshHand(BattleSnapshot snap, bool over)
        {
            handIds.Clear();
            for (int i = 0; i < handButtons.Count; i++)
            {
                if (i >= snap.Hand.Count)
                {
                    handButtons[i].gameObject.SetActive(false);
                    continue;
                }
                handButtons[i].gameObject.SetActive(true);
                var card = snap.Hand[i];
                handIds.Add(card.Id);
                int order = Session.StagedIndex(card.Id);
                string label = CombatLabText.Element(card.Element) + "\n<size=15>"
                    + CombatLabText.EffectName(Session.SelectedSchool, card.Element) + "　#" + card.Id + "</size>";
                if (order >= 0) label += "\n<b>【第 " + (order + 1) + " 張】</b>";
                else if (redrawMode) label += "\n<size=14>點擊調度</size>";
                Color color = order >= 0 ? new Color(0.35f, 0.35f, 0.38f, 1f) : ElementColor(card.Element, 1f);
                if (redrawMode && order < 0) color = Color.Lerp(color, CombatLabUi.Highlight, 0.35f);
                CombatLabUi.SetButton(handButtons[i], label, color, !over && !(redrawMode && order >= 0));
            }
        }

        private void RefreshActions(BattleSnapshot snap, bool over)
        {
            CombatLabUi.SetButton(previewButton, "預覽", CombatLabUi.ButtonColor, !over);
            string commitLabel = Session.Staged.Count == 0 ? "結束回合（空序列）" : "確認施放（" + Session.Staged.Count + " 張）";
            CombatLabUi.SetButton(commitButton, commitLabel, over ? CombatLabUi.ButtonColor : new Color(0.25f, 0.45f, 0.30f, 1f),
                !over);
            string redrawLabel = redrawMode ? "取消調度" : "調度（剩 " + snap.RedrawsRemaining + "）";
            CombatLabUi.SetButton(redrawButton, redrawLabel, redrawMode ? CombatLabUi.Highlight : CombatLabUi.ButtonColor,
                !over && (redrawMode || snap.RedrawsRemaining > 0));
            CombatLabUi.SetButton(resetButton, "重置戰鬥", new Color(0.45f, 0.25f, 0.25f, 1f), true);
        }

        private void RefreshPreview(BattleSnapshot snap)
        {
            var p = Session.LastPreview;
            if (p == null)
            {
                previewText.text = "尚未預覽。編排完成後按「預覽」，會用同一個戰鬥核心計算結果，但不改動真實狀態。";
                return;
            }
            if (!p.Validation.IsValid)
            {
                previewText.text = "無法預覽：" + p.Validation.Error;
                return;
            }

            var r = p.Result;
            var ns = r.NewState;
            var sb = new StringBuilder();
            sb.Append("<b>預覽結果（尚未結算）</b>　").Append(CombatLabText.Outcome(r.Outcome)).Append('\n');
            sb.Append("錨定值 ").Append(snap.Anchor).Append(" → ").Append(ns.Anchor)
              .Append("　擬態錨定 ").Append(snap.Shield).Append(" → ").Append(ns.Shield).Append('\n');
            foreach (var before in snap.Enemies)
            {
                var after = ns.FindEnemy(before.Id);
                if (after == null) continue;
                sb.Append(before.Name).Append("：失序值 ").Append(before.Value).Append(" → ").Append(after.Value)
                  .Append("　連結 ").Append(before.Links).Append(" → ").Append(after.Links)
                  .Append("　校正 ").Append(before.Corrections).Append(" → ").Append(after.Corrections);
                if (after.Resolved && !before.Resolved) sb.Append("　【解除】");
                sb.Append('\n');
            }
            sb.Append("—— 預覽事件 ——\n");
            foreach (var e in r.Events) sb.Append(Session.Describe(e)).Append('\n');
            previewText.text = sb.ToString();
            Canvas.ForceUpdateCanvases();
            previewScroll.verticalNormalizedPosition = 1f;
        }

        private static Color ElementColor(ElementKind e, float strength)
        {
            Color c;
            switch (e)
            {
                case ElementKind.Ember: c = new Color(0.62f, 0.30f, 0.20f, 1f); break;
                case ElementKind.Base: c = new Color(0.28f, 0.38f, 0.55f, 1f); break;
                default: c = new Color(0.25f, 0.50f, 0.40f, 1f); break;
            }
            return Color.Lerp(CombatLabUi.ButtonColor, c, strength);
        }
    }
}
