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
    /// 版面以 1600×900 為參考解析度（見 <see cref="CombatLabStyle"/>）。
    /// </summary>
    public sealed class CombatLabView : MonoBehaviour
    {
        [Tooltip("可選：指定 BattleConfig 資產調整數值；留空則使用規格預設值。")]
        [SerializeField] private BattleConfig config;

        [Tooltip("固定亂數種子，方便重現問題。")]
        [SerializeField] private int seed = CombatLabScenario.DefaultSeed;

        private static readonly SchoolKind[] Schools = { SchoolKind.Structure, SchoolKind.Process, SchoolKind.Relation };

        // 版面區塊（以畫面比例表示的錨點）。
        private const float TopBarBottom = 0.915f;
        private const float MessageBottom = 0.868f;
        private const float BottomTop = 0.315f;
        private const float LeftRight = 0.31f;
        private const float LogLeft = 0.74f;

        private bool redrawMode;
        private bool built;

        private string topSummary = "";
        private readonly Dictionary<string, Text> chipValues = new Dictionary<string, Text>();
        private readonly Dictionary<string, Text> chipLabels = new Dictionary<string, Text>();
        private RectTransform tooltip;
        private Text tooltipText;
        private int hoveredSlot = -1;
        private Text messageText;
        private Text sequenceTitle;
        private Text previewText;
        private Text logText;
        private ScrollRect logScroll;
        private ScrollRect previewScroll;
        private Canvas canvas;
        private readonly List<Button> schoolButtons = new List<Button>();
        private readonly List<Button> enemyButtons = new List<Button>();
        private readonly List<Button> sequenceButtons = new List<Button>();
        private readonly List<Button> handButtons = new List<Button>();
        private readonly List<int> handIds = new List<int>();
        private Button commitButton;
        private Button redrawButton;
        private Button clearButton;
        private Button resetButton;
        private InputField seedInput;
        private RectTransform enemyList;

        public CombatLabSession Session { get; private set; }
        public bool RedrawMode { get { return redrawMode; } }

        // 供自動化測試使用的唯讀存取。
        public Canvas Canvas { get { return canvas; } }
        public string TopText { get { return topSummary; } }
        public string MessageText { get { return messageText.text; } }
        public string PreviewText { get { return previewText.text; } }
        public string LogText { get { return logText.text; } }
        public IReadOnlyList<Button> SchoolButtons { get { return schoolButtons; } }
        public IReadOnlyList<Button> EnemyButtons { get { return enemyButtons; } }
        public IReadOnlyList<Button> SequenceButtons { get { return sequenceButtons; } }
        public IReadOnlyList<Button> HandButtons { get { return handButtons; } }
        public IReadOnlyList<int> HandIds { get { return handIds; } }
        public Button CommitButton { get { return commitButton; } }
        public Button RedrawButton { get { return redrawButton; } }
        public Button ClearButton { get { return clearButton; } }
        public Button ResetButton { get { return resetButton; } }
        public InputField SeedInput { get { return seedInput; } }
        public ScrollRect LogScroll { get { return logScroll; } }
        public ScrollRect PreviewScroll { get { return previewScroll; } }
        public RectTransform Tooltip { get { return tooltip; } }
        public bool TooltipVisible { get { return tooltip != null && tooltip.gameObject.activeSelf; } }
        public string TooltipText { get { return tooltipText.text; } }
        public string EnemyText(int index) { return enemyButtons[index].GetComponentInChildren<Text>().text; }
        public string ChipText(string key) { return chipLabels[key].text + "｜" + chipValues[key].text; }

        /// <summary>
        /// 與 CanvasScaler（Scale With Screen Size、Match Width Or Height）相同的縮放計算，
        /// 供截圖工具與測試在指定畫面尺寸下重現實際倍率。
        /// </summary>
        public static float ScaleFactorFor(CanvasScaler scaler, float screenWidth, float screenHeight)
        {
            var r = scaler.referenceResolution;
            float logW = Mathf.Log(screenWidth / r.x, 2f);
            float logH = Mathf.Log(screenHeight / r.y, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(logW, logH, scaler.matchWidthOrHeight));
        }

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
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 文字頂點對齊螢幕像素，避免舊版 Text 落在半像素上而發糊。
            canvas.pixelPerfect = true;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = CombatLabStyle.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = CombatLabStyle.MatchWidthOrHeight;
            scaler.referencePixelsPerUnit = 100f;
            var root = (RectTransform)canvasGo.transform;

            var bg = CombatLabUi.Panel("Background", root, CombatLabUi.Background);
            CombatLabUi.Stretch(bg);

            BuildTopBar(root);

            var msg = CombatLabUi.Panel("Message", root, new Color(0.12f, 0.13f, 0.15f, 1f));
            CombatLabUi.Anchor(msg, 0f, MessageBottom, LogLeft, TopBarBottom, 4f);
            messageText = CombatLabUi.Label("Text", msg, "", CombatLabStyle.Section, TextAnchor.MiddleLeft, CombatLabUi.Highlight);
            CombatLabUi.Stretch(messageText.rectTransform, 0f);
            messageText.rectTransform.offsetMin = new Vector2(12f, 0f);
            messageText.rectTransform.offsetMax = new Vector2(-12f, 0f);
            messageText.horizontalOverflow = HorizontalWrapMode.Overflow;
            msg.gameObject.AddComponent<RectMask2D>();

            BuildLeft(root);
            BuildCenter(root);
            BuildBottom(root);
            BuildLog(root);
            BuildTooltip(root);
        }

        /// <summary>手牌懸停時的說明框：放在最上層，不攔截滑鼠，避免閃爍。</summary>
        private void BuildTooltip(RectTransform root)
        {
            tooltip = CombatLabUi.Panel("CardTooltip", root, new Color(0.06f, 0.07f, 0.08f, 0.97f));
            tooltip.anchorMin = tooltip.anchorMax = new Vector2(0.5f, 0.5f);
            tooltip.pivot = new Vector2(0.5f, 0f);
            tooltip.sizeDelta = new Vector2(TooltipWidth, 100f);
            var outline = tooltip.gameObject.AddComponent<Outline>();
            outline.effectColor = CombatLabUi.Highlight;
            outline.effectDistance = new Vector2(2f, -2f);
            var group = tooltip.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            tooltip.GetComponent<Image>().raycastTarget = false;
            var v = CombatLabUi.Vertical(tooltip, 0f, 14);
            v.childForceExpandHeight = false;
            var fitter = tooltip.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            tooltipText = CombatLabUi.Label("Text", tooltip, "", CombatLabStyle.Body, TextAnchor.UpperLeft);
            tooltipText.lineSpacing = 1.1f;
            tooltipText.raycastTarget = false;
            tooltip.gameObject.SetActive(false);
        }

        private const float TooltipWidth = 560f;

        private static void AddHover(Button b, System.Action enter, System.Action exit)
        {
            var trigger = b.gameObject.AddComponent<EventTrigger>();
            var on = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            on.callback.AddListener(_ => enter());
            var off = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            off.callback.AddListener(_ => exit());
            trigger.triggers.Add(on);
            trigger.triggers.Add(off);
        }

        private void BuildTopBar(RectTransform root)
        {
            var top = CombatLabUi.Panel("BattleInfo", root, CombatLabUi.PanelColor);
            CombatLabUi.Anchor(top, 0f, TopBarBottom, 1f, 1f, 4f);
            var row = CombatLabUi.Horizontal(top, 6f, 6);
            row.childForceExpandWidth = false;

            AddChip(top, "Round", "回合", 1f);
            AddChip(top, "Anchor", "錨定值", 1.6f);
            AddChip(top, "Shield", "擬態錨定", 1.4f);
            AddChip(top, "AP", "AP（剩餘 / 上限）", 1.1f);
            AddChip(top, "School", "學派", 1.8f);
            AddChip(top, "Outcome", "戰鬥狀態", 0.9f);
            AddChip(top, "Redraw", "調度", 0.8f);
            AddChip(top, "Deck", "牌庫・棄牌", 1f);
        }

        private void AddChip(RectTransform parent, string key, string label, float flex)
        {
            var chip = CombatLabUi.Panel("Chip_" + key, parent, new Color(0.11f, 0.12f, 0.14f, 1f));
            CombatLabUi.Size(chip, -1, -1, -1, flex);
            var v = CombatLabUi.Vertical(chip, 0f, 4);
            v.childForceExpandHeight = true;
            chipLabels[key] = CombatLabUi.Label("Label", chip, label, CombatLabStyle.MinFont, TextAnchor.MiddleCenter,
                CombatLabUi.MutedText);
            var value = CombatLabUi.Label("Value", chip, "", CombatLabStyle.Important, TextAnchor.MiddleCenter);
            value.fontStyle = FontStyle.Bold;
            chipValues[key] = value;
        }

        private void BuildLeft(RectTransform root)
        {
            var left = CombatLabUi.Panel("SchoolAndEnemies", root, CombatLabUi.PanelColor);
            CombatLabUi.Anchor(left, 0f, BottomTop, LeftRight, MessageBottom, 4f);
            CombatLabUi.Vertical(left, 8f, 10);

            SectionTitle(left, "SchoolTitle", "學派（確認施放時鎖定）");
            var row = CombatLabUi.Rect("SchoolRow", left);
            CombatLabUi.Horizontal(row, 8f);
            CombatLabUi.Size(row, 56);
            foreach (var s in Schools)
            {
                var school = s;
                schoolButtons.Add(CombatLabUi.Button("School_" + s, row, CombatLabText.School(s),
                    () => Session.SelectSchool(school), CombatLabStyle.Button));
            }

            SectionTitle(left, "EnemyTitle", "敵人（點擊選為目標）");
            enemyList = CombatLabUi.Rect("EnemyList", left);
            CombatLabUi.Vertical(enemyList, 8f, 0);
            CombatLabUi.Size(enemyList, -1, 1);
            BuildEnemyButtons();
        }

        private static Text SectionTitle(RectTransform parent, string name, string text)
        {
            var t = CombatLabUi.Label(name, parent, text, CombatLabStyle.Section, TextAnchor.MiddleLeft, CombatLabUi.MutedText);
            t.fontStyle = FontStyle.Bold;
            CombatLabUi.Size(t, 28);
            return t;
        }

        private void BuildEnemyButtons()
        {
            foreach (var b in enemyButtons) CombatLabUi.Kill(b.gameObject);
            enemyButtons.Clear();
            foreach (var e in Session.Snapshot().Enemies)
            {
                int id = e.Id;
                var b = CombatLabUi.Button("Enemy_" + id, enemyList, "", () => Session.SelectTarget(id), CombatLabStyle.Body);
                var t = b.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                t.lineSpacing = 1.05f;
                CombatLabUi.Stretch(t.rectTransform, 10f);
                CombatLabUi.Size(b, 150);
                enemyButtons.Add(b);
            }
        }

        private void BuildCenter(RectTransform root)
        {
            var center = CombatLabUi.Panel("SequenceAndPreview", root, CombatLabUi.PanelColor);
            CombatLabUi.Anchor(center, LeftRight, BottomTop, LogLeft, MessageBottom, 4f);
            CombatLabUi.Vertical(center, 8f, 10);

            var header = CombatLabUi.Rect("SequenceHeader", center);
            var h = CombatLabUi.Horizontal(header, 8f);
            h.childForceExpandWidth = false;
            CombatLabUi.Size(header, 36);
            sequenceTitle = CombatLabUi.Label("SequenceTitle", header, "", CombatLabStyle.Section, TextAnchor.MiddleLeft,
                CombatLabUi.MutedText);
            sequenceTitle.fontStyle = FontStyle.Bold;
            CombatLabUi.Size(sequenceTitle, -1, -1, -1, 1);
            clearButton = CombatLabUi.Button("ClearPlan", header, "清空編排", () => Session.ClearPlan(), CombatLabStyle.Body);
            CombatLabUi.Size(clearButton, -1, -1, 130, 0);

            for (int i = 0; i < RuleSet.ActionPoints; i++)
            {
                int index = i;
                var b = CombatLabUi.Button("Slot_" + i, center, "", () => Session.RemoveAt(index), CombatLabStyle.Button);
                var t = b.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                CombatLabUi.Stretch(t.rectTransform, 12f);
                CombatLabUi.Size(b, 48);
                sequenceButtons.Add(b);
            }

            SectionTitle(center, "PreviewTitle", "本回合預估（即時，依目前編排；尚未結算）");
            previewText = CombatLabUi.ScrollText("Preview", center, CombatLabStyle.Body, out previewScroll);
            previewText.lineSpacing = 1.05f;
            CombatLabUi.Size(previewScroll, -1, 1);
        }

        private void BuildBottom(RectTransform root)
        {
            var bottom = CombatLabUi.Panel("HandAndActions", root, CombatLabUi.PanelColor);
            CombatLabUi.Anchor(bottom, 0f, 0f, LogLeft, BottomTop, 4f);
            CombatLabUi.Vertical(bottom, 8f, 10);

            SectionTitle(bottom, "HandTitle", "手牌（滑鼠移到牌上看詳細效果；點擊加入／移除編排）");
            var hand = CombatLabUi.Rect("Hand", bottom);
            CombatLabUi.Horizontal(hand, 8f);
            CombatLabUi.Size(hand, -1, 1);
            for (int i = 0; i < RuleSet.HandSize; i++)
            {
                int slot = i;
                var b = CombatLabUi.Button("Hand_" + i, hand, "", () => OnHandClicked(slot), CombatLabStyle.Value);
                var t = b.GetComponentInChildren<Text>();
                t.lineSpacing = 1.0f;
                CombatLabUi.Stretch(t.rectTransform, 4f);
                AddHover(b, () => ShowCardTooltip(slot), () => HideCardTooltip(slot));
                handButtons.Add(b);
            }

            var actions = CombatLabUi.Rect("Actions", bottom);
            var row = CombatLabUi.Horizontal(actions, 8f);
            row.childForceExpandWidth = false;
            CombatLabUi.Size(actions, 58);
            commitButton = CombatLabUi.Button("Commit", actions, "", () => Session.Commit(), CombatLabStyle.Important);
            redrawButton = CombatLabUi.Button("Redraw", actions, "", ToggleRedrawMode, CombatLabStyle.Button);
            var seedLabel = CombatLabUi.Label("SeedLabel", actions, "種子", CombatLabStyle.Body, TextAnchor.MiddleRight,
                CombatLabUi.MutedText);
            seedInput = CombatLabUi.Input("Seed", actions, Session.Seed.ToString(), "種子");
            resetButton = CombatLabUi.Button("Reset", actions, "重置戰鬥", OnReset, CombatLabStyle.Button);
            CombatLabUi.Size(commitButton, -1, -1, -1, 2.4f);
            CombatLabUi.Size(redrawButton, -1, -1, -1, 1.1f);
            CombatLabUi.Size(seedLabel, -1, -1, 50, 0);
            CombatLabUi.Size(seedInput, -1, -1, 150, 0);
            CombatLabUi.Size(resetButton, -1, -1, -1, 1f);
        }

        private void BuildLog(RectTransform root)
        {
            var right = CombatLabUi.Panel("BattleLog", root, CombatLabUi.PanelColor);
            CombatLabUi.Anchor(right, LogLeft, 0f, 1f, TopBarBottom, 4f);
            CombatLabUi.Vertical(right, 8f, 10);
            SectionTitle(right, "LogTitle", "戰鬥紀錄（來自 BattleEvent）");
            logText = CombatLabUi.ScrollText("Log", right, CombatLabStyle.Small, out logScroll);
            logText.lineSpacing = 1.05f;
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
            if (messageText == null) return;
            var snap = Session.Snapshot();
            bool over = snap.Outcome != BattleOutcome.Ongoing;
            if (over) redrawMode = false;

            RefreshTopBar(snap);
            messageText.text = Session.Message ?? "";

            for (int i = 0; i < schoolButtons.Count; i++)
            {
                var s = Schools[i];
                bool selected = s == Session.SelectedSchool;
                bool allowed = !over && (!snap.LockedSchool.HasValue || snap.LockedSchool.Value == s);
                CombatLabUi.SetButton(schoolButtons[i], (selected ? "● " : "") + CombatLabText.School(s),
                    selected ? CombatLabUi.Highlight : CombatLabUi.ButtonColor, allowed,
                    selected ? CombatLabUi.DarkText : (Color?)null);
            }

            RefreshEnemies(snap, over);
            RefreshSequence(over);
            RefreshHand(snap, over);
            RefreshActions(snap, over);
            RefreshPreview(snap);

            logText.text = string.Join("\n", Session.Log);
            Canvas.ForceUpdateCanvases();
            logScroll.verticalNormalizedPosition = 0f;

            if (hoveredSlot >= 0) ShowCardTooltip(hoveredSlot);
        }

        // ---------------- 手牌說明框 ----------------

        /// <summary>顯示手牌第 slot 張的說明（滑鼠移入時呼叫；測試可直接呼叫）。</summary>
        public void ShowCardTooltip(int slot)
        {
            if (tooltip == null || slot < 0 || slot >= handIds.Count || !handButtons[slot].gameObject.activeSelf)
            {
                HideCardTooltip(slot);
                return;
            }
            hoveredSlot = slot;
            tooltipText.text = CardTooltip(handIds[slot]);
            tooltip.gameObject.SetActive(true);
            tooltip.SetAsLastSibling();
            PlaceTooltip((RectTransform)handButtons[slot].transform);
        }

        public void HideCardTooltip(int slot)
        {
            if (slot != hoveredSlot && slot >= 0) return;
            hoveredSlot = -1;
            if (tooltip != null) tooltip.gameObject.SetActive(false);
        }

        /// <summary>把說明框放在牌的正上方，並限制在畫布內。</summary>
        private void PlaceTooltip(RectTransform card)
        {
            var root = (RectTransform)canvas.transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltip);
            var corners = new Vector3[4];
            card.GetWorldCorners(corners);
            Vector2 topCenter = root.InverseTransformPoint((corners[1] + corners[2]) * 0.5f);
            var area = root.rect;
            float half = tooltip.rect.width * 0.5f;
            float x = Mathf.Clamp(topCenter.x, area.xMin + half + 4f, area.xMax - half - 4f);
            float y = topCenter.y + 8f;
            float h = tooltip.rect.height;
            if (y + h > area.yMax - 4f) y = area.yMax - 4f - h;
            tooltip.anchoredPosition = new Vector2(x, y);
        }

        private string CardTooltip(int cardId)
        {
            return CombatLabCardInfo.Tooltip(Session, cardId, redrawMode, "左側點敵人可更換");
        }

        private void RefreshTopBar(BattleSnapshot snap)
        {
            string school = snap.LockedSchool.HasValue
                ? CombatLabText.School(snap.LockedSchool.Value) + "（已鎖定）"
                : CombatLabText.School(Session.SelectedSchool) + "（確認時鎖定）";
            string anchor = snap.Anchor + " / " + Session.Rules.MaxAnchor;
            string ap = Session.ApRemaining + " / " + Session.ApMax;
            string outcome = CombatLabText.Outcome(snap.Outcome);
            string deck = snap.DeckCount + "・" + snap.DiscardCount;

            chipValues["Round"].text = "第 " + (snap.Round + 1) + " 回合";
            var f = Session.Forecast;
            if (f != null)
            {
                chipLabels["Anchor"].text = "錨定值（現在 → 回合後）";
                chipValues["Anchor"].text = snap.Anchor + " → " + ProjectedValue(f.AnchorBefore, f.AnchorAfter, true);
                chipLabels["Shield"].text = "擬態錨定（現在 → 回合後）";
                chipValues["Shield"].text = snap.Shield + " → " + ProjectedValue(f.ShieldBefore, f.ShieldAfter, false);
            }
            else
            {
                chipLabels["Anchor"].text = "錨定值（上限 " + Session.Rules.MaxAnchor + "）";
                chipValues["Anchor"].text = snap.Anchor.ToString();
                chipLabels["Shield"].text = "擬態錨定";
                chipValues["Shield"].text = snap.Shield.ToString();
            }
            chipValues["AP"].text = ap;
            chipValues["School"].text = school;
            chipValues["Outcome"].text = outcome;
            chipValues["Outcome"].color = snap.Outcome == BattleOutcome.Victory ? new Color(0.55f, 0.90f, 0.60f, 1f)
                : snap.Outcome == BattleOutcome.Defeat ? new Color(1f, 0.55f, 0.50f, 1f) : CombatLabUi.TextColor;
            chipValues["Redraw"].text = "剩 " + snap.RedrawsRemaining;
            chipValues["Deck"].text = deck;

            topSummary = "第 " + (snap.Round + 1) + " 回合｜錨定值 " + anchor + "｜擬態錨定 " + snap.Shield
                + "｜AP " + ap + "｜學派：" + school + "｜狀態：" + outcome
                + "｜調度剩 " + snap.RedrawsRemaining + "｜牌庫・棄牌 " + deck;
        }

        private void RefreshEnemies(BattleSnapshot snap, bool over)
        {
            for (int i = 0; i < enemyButtons.Count && i < snap.Enemies.Count; i++)
            {
                var e = snap.Enemies[i];
                bool target = Session.SelectedTargetId.HasValue && Session.SelectedTargetId.Value == e.Id;
                var sb = new StringBuilder();
                sb.Append("<size=").Append(CombatLabStyle.Important).Append("><b>")
                  .Append(target ? "◎ " : "").Append(e.Name).Append("</b></size>");
                if (e.Resolved)
                {
                    sb.Append("　【已解除】");
                }
                else
                {
                    if (target) sb.Append("　<b>目標</b>");
                    var ef = Session.Forecast != null ? Session.Forecast.Enemy(e.Id) : null;
                    sb.Append("\n失序值 <b>").Append(e.Value).Append("</b>");
                    if (ef != null && ef.ValueAfter != e.Value)
                    {
                        sb.Append(" → ").Append(CombatLabText.Colored("<b>" + ef.ValueAfter + "</b>", CombatLabText.Good))
                          .Append(ef.ResolvedAfter ? CombatLabText.Colored(" 解除", CombatLabText.Good) : "");
                    }
                    sb.Append("\n");
                    if (ef != null) sb.Append("<b>").Append(CombatLabText.EnemyImpact(ef, Session.Forecast.Outcome)).Append("</b>");
                    else sb.Append("本回合攻擊 <b>").Append(e.ForecastAttack).Append("</b>");
                    sb.Append("\n<size=").Append(CombatLabStyle.Small).Append(">連結 ").Append(e.Links).Append(" / ")
                      .Append(Session.Rules.LinkCap).Append("　校正 ").Append(e.Corrections).Append("　束縛 ").Append(e.Bind)
                      .Append("</size>");
                }
                Color c = e.Resolved ? new Color(0.18f, 0.18f, 0.20f, 1f) : target ? new Color(0.46f, 0.36f, 0.14f, 1f)
                    : CombatLabUi.ButtonColor;
                CombatLabUi.SetButton(enemyButtons[i], sb.ToString(), c, !over && !e.Resolved);
            }
        }

        private void RefreshSequence(bool over)
        {
            var staged = Session.Staged;
            sequenceTitle.text = "編排序列（依序結算，點擊移除）　AP " + staged.Count + " / " + Session.ApMax;
            for (int i = 0; i < sequenceButtons.Count; i++)
            {
                if (i < staged.Count)
                {
                    var c = staged[i];
                    string target = Session.CardNeedsTarget(c.Element)
                        ? (c.TargetEnemyId.HasValue ? " → " + Session.EnemyName(c.TargetEnemyId.Value) : " → （無目標）")
                        : "　（此學派不需目標）";
                    CombatLabUi.SetButton(sequenceButtons[i],
                        (i + 1) + ". <b>" + CombatLabText.Element(c.Element) + "</b>（"
                        + CombatLabText.EffectName(Session.SelectedSchool, c.Element) + "）" + target + "　✕",
                        ElementColor(c.Element, 0.85f), !over);
                }
                else
                {
                    CombatLabUi.SetButton(sequenceButtons[i], (i + 1) + ". （空）", new Color(0.19f, 0.20f, 0.23f, 1f), false);
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
                string label = "<b>" + CombatLabText.Element(card.Element) + "</b>\n<size=" + CombatLabStyle.Button + ">"
                    + CombatLabText.EffectName(Session.SelectedSchool, card.Element) + "</size>\n<size="
                    + CombatLabStyle.Small + ">" + CombatLabText.EffectShort(Session.Rules, Session.SelectedSchool, card.Element)
                    + "</size>";
                if (order >= 0) label += "\n<size=" + CombatLabStyle.Body + "><b>【第 " + (order + 1) + " 張】</b></size>";
                else if (redrawMode) label += "\n<size=" + CombatLabStyle.Body + ">點擊調度</size>";
                Color color = order >= 0 ? new Color(0.33f, 0.33f, 0.36f, 1f) : ElementColor(card.Element, 1f);
                if (redrawMode && order < 0) color = Color.Lerp(color, CombatLabUi.Highlight, 0.30f);
                CombatLabUi.SetButton(handButtons[i], label, color, !over && !(redrawMode && order >= 0));
            }
        }

        private void RefreshActions(BattleSnapshot snap, bool over)
        {
            string commitLabel = Session.Staged.Count == 0 ? "結束回合（空序列）" : "確認施放（" + Session.Staged.Count + " 張）";
            CombatLabUi.SetButton(commitButton, commitLabel, over ? CombatLabUi.ButtonColor : new Color(0.20f, 0.46f, 0.30f, 1f),
                !over);
            string redrawLabel = redrawMode ? "取消調度" : "調度（剩 " + snap.RedrawsRemaining + "）";
            CombatLabUi.SetButton(redrawButton, redrawLabel, redrawMode ? CombatLabUi.Highlight : CombatLabUi.ButtonColor,
                !over && (redrawMode || snap.RedrawsRemaining > 0), redrawMode ? CombatLabUi.DarkText : (Color?)null);
            CombatLabUi.SetButton(resetButton, "重置戰鬥", new Color(0.48f, 0.24f, 0.24f, 1f), true);
        }

        private void RefreshPreview(BattleSnapshot snap)
        {
            var p = Session.LastPreview;
            if (p == null)
            {
                previewText.text = "戰鬥已結束，按「重置戰鬥」重新開始。";
                return;
            }
            var f = Session.Forecast;
            if (!p.Validation.IsValid || f == null)
            {
                previewText.text = "目前編排無法結算：" + p.Validation.Error;
                return;
            }

            var sb = new StringBuilder();
            sb.Append("<b>").Append(Session.Staged.Count == 0 ? "若直接結束回合（空序列）" : "若現在確認施放")
              .Append("　→ ").Append(CombatLabText.Outcome(f.Outcome)).Append("</b>\n");
            sb.Append("<size=").Append(CombatLabStyle.Button).Append(">錨定值 ").Append(CombatLabText.Change(f.AnchorBefore, f.AnchorAfter))
              .Append("　擬態錨定 ").Append(CombatLabText.Change(f.ShieldBefore, f.ShieldAfter, false)).Append("</size>\n");
            sb.Append("<b>敵方回合</b>　").Append(CombatLabText.DamageSummary(f)).Append('\n');
            foreach (var e in f.Enemies)
            {
                if (e.ResolvedBefore) continue;
                sb.Append("・").Append(e.Name).Append("：").Append(CombatLabText.EnemyImpact(e, f.Outcome)).Append('\n');
            }
            sb.Append("<b>敵人變化</b>\n");
            foreach (var e in f.Enemies)
            {
                if (e.ResolvedBefore) continue;
                sb.Append("・").Append(e.Name).Append("：失序值 ").Append(CombatLabText.Change(e.ValueBefore, e.ValueAfter, false));
                if (e.ResolvedAfter) sb.Append(CombatLabText.Colored("【解除】", CombatLabText.Good));
                if (e.LinksBefore != e.LinksAfter) sb.Append("　連結 ").Append(e.LinksBefore).Append(" → ").Append(e.LinksAfter);
                if (e.CorrectionsBefore != e.CorrectionsAfter)
                    sb.Append("　校正 ").Append(e.CorrectionsBefore).Append(" → ").Append(e.CorrectionsAfter);
                sb.Append('\n');
            }
            sb.Append("<color=#C8CCD4>—— 詳細結算順序 ——\n");
            foreach (var e in f.Events) sb.Append(Session.Describe(e)).Append('\n');
            sb.Append("</color>");
            previewText.text = sb.ToString();
            Canvas.ForceUpdateCanvases();
            previewScroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>預估值：變差用紅色、變好用綠色。</summary>
        private static string ProjectedValue(int before, int after, bool lowerIsBad)
        {
            if (before == after) return after.ToString();
            bool bad = (after < before) == lowerIsBad;
            return CombatLabText.Colored(after.ToString(), bad ? CombatLabText.Bad : CombatLabText.Good);
        }

        private static Color ElementColor(ElementKind e, float strength)
        {
            Color c;
            switch (e)
            {
                case ElementKind.Ember: c = new Color(0.60f, 0.28f, 0.18f, 1f); break;
                case ElementKind.Base: c = new Color(0.24f, 0.36f, 0.56f, 1f); break;
                default: c = new Color(0.20f, 0.46f, 0.36f, 1f); break;
            }
            return Color.Lerp(CombatLabUi.ButtonColor, c, strength);
        }
    }
}
