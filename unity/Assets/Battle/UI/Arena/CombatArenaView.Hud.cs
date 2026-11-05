using System.Collections.Generic;
using System.Text;
using Shepherd.Battle.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shepherd.Battle.UI.Arena
{
    /// <summary>
    /// 3D 戰鬥 HUD：只保留戰鬥需要的資訊。
    /// 左上：錨定值／擬態錨定／本回合威脅／AP／回合；右上：學派（含方向描述）、戰鬥紀錄（可展開）、重置；
    /// 敵人頭上：失序值、預告攻擊與實際影響、狀態圖示；下方：七張手牌（牌面直接顯示主要功能）；
    /// 右下：術式編排（承接連結、共鳴高亮）、分段預估與確認施放。
    /// </summary>
    public sealed partial class CombatArenaView
    {
        private static readonly SchoolKind[] Schools = { SchoolKind.Structure, SchoolKind.Process, SchoolKind.Relation };

        private const float HandCardWidth = 118f;
        private const float HandCardHeight = 170f;
        private const float HandSpacing = 8f;
        private const float HandOffsetX = -110f;
        private const float PlanWidth = 370f;
        private const float PlanHeight = 470f;
        private const float TooltipWidth = 520f;

        private static readonly Color ResidueColor = new Color(0.92f, 0.78f, 0.42f);
        private static readonly Color ResonanceColor = new Color(1f, 0.85f, 0.3f);

        private sealed class Plate
        {
            public RectTransform Rect;
            public Button Button;
            public Text Name;
            public Text Value;
            public Image AttackIcon;
            public Text Attack;
            public Text AttackLabel;
            public Text Impact;
            public readonly List<GameObject> StatusItems = new List<GameObject>();
            public readonly List<Text> StatusTexts = new List<Text>();
            public RectTransform Fill;
            public RectTransform Ghost;
            public Image GhostImage;
            public CanvasGroup Group;
        }

        private sealed class CardFace
        {
            public Button Button;
            public Image Background;
            public Image SchoolStripe;
            public Text Element;
            public Text Ap;
            public Image Icon;
            public readonly List<Image> SubIcons = new List<Image>();
            public Text Effect;
            public Text Main;
            public Text Role;
            public RectTransform Badge;
            public Text BadgeText;
            public List<CardRole> Roles = new List<CardRole>();
        }

        private sealed class Connector
        {
            public RectTransform Rect;
            public Text Text;
            public Image Icon;
            public string Tip;
        }

        private Canvas canvas;
        private RectTransform hudRoot;
        private RectTransform plateLayer;
        private RectTransform floaterLayer;
        private Image hurtFlash;
        private Image shieldFlash;
        private Text roundText;
        private Text anchorText;
        private RectTransform anchorFill;
        private RectTransform anchorGhost;
        private Image anchorGhostImage;
        private Text shieldText;
        private Text threatText;
        private Text apText;
        private readonly List<Image> apPips = new List<Image>();
        private readonly List<Button> schoolButtons = new List<Button>();
        private readonly List<Text> schoolTaglines = new List<Text>();
        private Button logToggle;
        private Button resetButton;
        private RectTransform logPanel;
        private Text logText;
        private ScrollRect logScroll;
        private Text toastText;
        private CanvasGroup toastGroup;
        private float toastTime;
        private string lastToast;
        private RectTransform handRow;
        private CanvasGroup handGroup;
        private readonly List<CardFace> cards = new List<CardFace>();
        private readonly List<Button> handButtons = new List<Button>();
        private readonly List<int> handIds = new List<int>();
        private RectTransform planPanel;
        private Image planIcon;
        private Text planTitle;
        private readonly List<Button> sequenceButtons = new List<Button>();
        private readonly List<Outline> sequenceOutlines = new List<Outline>();
        private readonly List<Connector> connectors = new List<Connector>();
        private RectTransform resonanceRow;
        private Text resonanceText;
        private Text forecastText;
        private Button redrawButton;
        private Button commitButton;
        private RectTransform tooltip;
        private Text tooltipText;
        private int hoveredSlot = -1;
        private int hoveredConnector = -1;
        private RectTransform banner;
        private Text bannerTitle;
        private Text bannerSubtitle;
        private readonly Dictionary<int, Plate> plates = new Dictionary<int, Plate>();
        private bool redrawMode;

        // 供測試使用的唯讀存取。
        public Canvas HudCanvas { get { return canvas; } }
        public RectTransform HudRoot { get { return hudRoot; } }
        public string StatusText
        {
            get { return roundText.text + "｜" + anchorText.text + "｜" + shieldText.text + "｜" + threatText.text + "｜" + apText.text; }
        }
        public string ThreatText { get { return threatText.text; } }
        public string ToastText { get { return toastText.text; } }
        public string ForecastText { get { return forecastText.text; } }
        public string PlanTitle { get { return planTitle.text; } }
        public IReadOnlyList<Button> SchoolButtons { get { return schoolButtons; } }
        public IReadOnlyList<Button> HandButtons { get { return handButtons; } }
        public IReadOnlyList<int> HandIds { get { return handIds; } }
        public IReadOnlyList<Button> SequenceButtons { get { return sequenceButtons; } }
        public Button CommitButton { get { return commitButton; } }
        public Button RedrawButton { get { return redrawButton; } }
        public Button LogToggle { get { return logToggle; } }
        public Button ResetButton { get { return resetButton; } }
        public bool LogVisible { get { return logPanel.gameObject.activeSelf; } }
        public string LogText { get { return logText.text; } }
        public bool TooltipVisible { get { return tooltip.gameObject.activeSelf; } }
        public string TooltipText { get { return tooltipText.text; } }
        public RectTransform Tooltip { get { return tooltip; } }
        public bool BannerVisible { get { return banner.gameObject.activeSelf; } }
        public string BannerText { get { return bannerTitle.text + "｜" + bannerSubtitle.text; } }
        public bool RedrawMode { get { return redrawMode; } }
        public float AnchorFillRatio { get { return anchorFill.anchorMax.x; } }

        public string SchoolButtonText(int i) { return schoolButtons[i].GetComponentInChildren<Text>().text + "｜" + schoolTaglines[i].text; }

        /// <summary>手牌第 slot 張牌面上的全部文字（元素｜效果｜主要數值｜功能｜AP）。</summary>
        public string CardFaceText(int slot)
        {
            var c = cards[slot];
            return c.Element.text + "｜" + c.Effect.text + "｜" + c.Main.text + "｜" + c.Role.text + "｜" + c.Ap.text
                + (c.Badge.gameObject.activeSelf ? "｜" + c.BadgeText.text : "");
        }

        public Sprite CardIcon(int slot) { return cards[slot].Icon.sprite; }
        public IReadOnlyList<CardRole> CardRoles(int slot) { return cards[slot].Roles; }
        public Color CardStripeColor(int slot) { return cards[slot].SchoolStripe.color; }

        /// <summary>第 index 個承接標記（介於第 index 張與第 index+1 張之間，0 起算）。</summary>
        public bool ConnectorVisible(int index) { return connectors[index].Rect.gameObject.activeSelf; }
        public string ConnectorText(int index) { return connectors[index].Text.text; }
        public RectTransform ConnectorRect(int index) { return connectors[index].Rect; }
        public bool ResonanceVisible { get { return resonanceRow.gameObject.activeSelf; } }
        public string ResonanceText { get { return resonanceText.text; } }
        public bool SlotHighlighted(int i) { return sequenceOutlines[i].enabled; }

        public string PlateText(int id)
        {
            var p = plates[id];
            var sb = new StringBuilder();
            sb.Append(p.Name.text).Append("｜").Append(p.Value.text).Append("｜");
            if (p.Attack.gameObject.activeSelf) sb.Append(p.AttackLabel.text).Append(' ').Append(p.Attack.text);
            sb.Append("｜").Append(p.Impact.text).Append("｜");
            foreach (var t in p.StatusTexts) if (t.gameObject.transform.parent.gameObject.activeSelf) sb.Append(t.name).Append(' ').Append(t.text).Append(' ');
            return sb.ToString();
        }

        public bool PlateAttackIconVisible(int id) { return plates[id].AttackIcon.gameObject.activeSelf; }
        public float PlateFillRatio(int id) { return plates[id].Fill.anchorMax.x; }
        public float PlateGhostRatio(int id) { return plates[id].Ghost.anchorMax.x; }
        public Button PlateButton(int id) { return plates[id].Button; }
        public RectTransform PlateRect(int id) { return plates[id].Rect; }

        /// <summary>HUD 中所有固定面板（測試檢查不重疊、不遮住戰場中央與敵人）。</summary>
        public IEnumerable<RectTransform> HudPanels()
        {
            yield return (RectTransform)roundText.transform.parent;
            yield return (RectTransform)schoolButtons[0].transform.parent;
            yield return (RectTransform)logToggle.transform.parent;
            yield return (RectTransform)toastText.transform.parent;
            yield return logPanel;
            yield return handRow;
            yield return planPanel;
        }

        // ---------------- 建立 ----------------

        private void BuildHud()
        {
            var canvasGo = new GameObject("ArenaHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = 5;
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = CombatLabStyle.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = CombatLabStyle.MatchWidthOrHeight;
            hudRoot = (RectTransform)canvasGo.transform;

            hurtFlash = FullImage("HurtFlash", new Color(0.9f, 0.08f, 0.05f, 0f));
            shieldFlash = FullImage("ShieldFlash", new Color(0.2f, 0.5f, 1f, 0f));
            plateLayer = CombatLabUi.Rect("Plates", hudRoot);
            CombatLabUi.Stretch(plateLayer);
            floaterLayer = CombatLabUi.Rect("Floaters", hudRoot);
            CombatLabUi.Stretch(floaterLayer);

            BuildStatus();
            BuildTopRight();
            BuildToast();
            BuildHand();
            BuildPlan();
            BuildTooltip();
            BuildBanner();
        }

        private Image FullImage(string name, Color c)
        {
            var rt = CombatLabUi.Panel(name, hudRoot, c);
            CombatLabUi.Stretch(rt);
            var img = rt.GetComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        private static readonly Color HudPanelColor = new Color(0.05f, 0.06f, 0.08f, 0.84f);

        private static RectTransform Corner(string name, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var rt = CombatLabUi.Panel(name, parent, color);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>在父物件內以錨點放置一個區塊（不受 LayoutGroup 控制）。</summary>
        private static RectTransform Place(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static Image IconImage(string name, Transform parent, ArenaIcon icon, Color color)
        {
            var rt = CombatLabUi.Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = ArenaIcons.Get(icon);
            img.color = color;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>水平條：底、預估（ghost）、目前值三層。</summary>
        private static RectTransform Bar(string name, Transform parent, Color fill, out RectTransform fillRect, out RectTransform ghostRect,
            out Image ghostImage)
        {
            var bg = CombatLabUi.Panel(name, parent, new Color(0.02f, 0.02f, 0.03f, 0.95f));
            var ghost = CombatLabUi.Panel("Ghost", bg, new Color(1f, 0.4f, 0.3f, 0.75f));
            ghost.anchorMin = Vector2.zero;
            ghost.anchorMax = Vector2.one;
            ghost.offsetMin = new Vector2(2f, 2f);
            ghost.offsetMax = new Vector2(-2f, -2f);
            var f = CombatLabUi.Panel("Fill", bg, fill);
            f.anchorMin = Vector2.zero;
            f.anchorMax = Vector2.one;
            f.offsetMin = new Vector2(2f, 2f);
            f.offsetMax = new Vector2(-2f, -2f);
            fillRect = f;
            ghostRect = ghost;
            ghostImage = ghost.GetComponent<Image>();
            foreach (var img in bg.GetComponentsInChildren<Image>()) img.raycastTarget = false;
            return bg;
        }

        private static readonly Color EnemyLossColor = new Color(1f, 0.42f, 0.32f, 0.85f);
        private static readonly Color AnchorLossColor = new Color(1f, 0.86f, 0.45f, 0.9f);

        /// <summary>目前值為實心、預估變化為淺色段（減少用 lossColor，增加用綠色）。</summary>
        private static void SetBar(RectTransform fill, RectTransform ghost, Image ghostImage, int now, int after, int max, Color lossColor)
        {
            float a = Mathf.Clamp01(now / (float)max), b = Mathf.Clamp01(after / (float)max);
            fill.anchorMax = new Vector2(Mathf.Min(a, b), 1f);
            ghost.anchorMax = new Vector2(Mathf.Max(a, b), 1f);
            ghostImage.color = b < a ? lossColor : new Color(0.55f, 1f, 0.6f, 0.75f);
        }

        private void BuildStatus()
        {
            var panel = Corner("Status", hudRoot, new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(410f, 206f), HudPanelColor);
            var v = CombatLabUi.Vertical(panel, 5f, 12);
            v.childForceExpandHeight = false;
            roundText = CombatLabUi.Label("Round", panel, "", CombatLabStyle.Body, TextAnchor.MiddleLeft, CombatLabUi.MutedText);
            CombatLabUi.Size(roundText, 26);

            var anchorRow = CombatLabUi.Rect("AnchorRow", panel);
            CombatLabUi.Size(anchorRow, 34);
            RectTransform ghost;
            var bar = Bar("AnchorBar", anchorRow, new Color(0.82f, 0.22f, 0.2f, 1f), out anchorFill, out ghost, out anchorGhostImage);
            anchorGhost = ghost;
            CombatLabUi.Stretch(bar);
            anchorText = CombatLabUi.Label("AnchorText", bar, "", CombatLabStyle.Button, TextAnchor.MiddleCenter);
            anchorText.fontStyle = FontStyle.Bold;
            anchorText.raycastTarget = false;
            anchorText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            CombatLabUi.Stretch(anchorText.rectTransform);

            var shieldRow = CombatLabUi.Rect("ShieldRow", panel);
            var sh = CombatLabUi.Horizontal(shieldRow, 6f);
            sh.childForceExpandWidth = false;
            CombatLabUi.Size(shieldRow, 26);
            var shieldIcon = IconImage("ShieldIcon", shieldRow, ArenaIcon.Defense, new Color(0.62f, 0.8f, 1f));
            CombatLabUi.Size(shieldIcon, 22, 0, 22, 0);
            shieldText = CombatLabUi.Label("Shield", shieldRow, "", CombatLabStyle.Section, TextAnchor.MiddleLeft, new Color(0.62f, 0.8f, 1f));
            shieldText.fontStyle = FontStyle.Bold;
            CombatLabUi.Size(shieldText, -1, -1, -1, 1);

            var threatRow = CombatLabUi.Rect("ThreatRow", panel);
            var th = CombatLabUi.Horizontal(threatRow, 6f);
            th.childForceExpandWidth = false;
            CombatLabUi.Size(threatRow, 26);
            var atk = IconImage("ThreatIcon", threatRow, ArenaIcon.Attack, new Color(1f, 0.62f, 0.5f));
            CombatLabUi.Size(atk, 22, 0, 22, 0);
            threatText = CombatLabUi.Label("Threat", threatRow, "", CombatLabStyle.MinFont, TextAnchor.MiddleLeft);
            CombatLabUi.Size(threatText, -1, -1, -1, 1);

            var apRow = CombatLabUi.Rect("ApRow", panel);
            var h = CombatLabUi.Horizontal(apRow, 8f);
            h.childForceExpandWidth = false;
            h.childAlignment = TextAnchor.MiddleLeft;
            CombatLabUi.Size(apRow, 28);
            apText = CombatLabUi.Label("ApText", apRow, "", CombatLabStyle.Section, TextAnchor.MiddleLeft, CombatLabUi.TextColor);
            apText.fontStyle = FontStyle.Bold;
            CombatLabUi.Size(apText, -1, -1, 170, 0);
            for (int i = 0; i < RuleSet.ActionPoints; i++)
            {
                var pip = CombatLabUi.Panel("Pip_" + i, apRow, CombatLabUi.Highlight);
                CombatLabUi.Size(pip, 22, 0, 22, 0);
                apPips.Add(pip.GetComponent<Image>());
            }
        }

        private void BuildTopRight()
        {
            var schools = Corner("Schools", hudRoot, new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(560f, 84f), HudPanelColor);
            CombatLabUi.Horizontal(schools, 6f, 6);
            foreach (var s in Schools)
            {
                var school = s;
                var b = CombatLabUi.Button("School_" + s, schools, CombatLabText.School(s), () => Session.SelectSchool(school),
                    CombatLabStyle.Section);
                var name = b.GetComponentInChildren<Text>();
                name.fontStyle = FontStyle.Bold;
                Place(name.rectTransform, 0f, 0.45f, 1f, 1f);
                name.rectTransform.offsetMin = new Vector2(30f, 0f);
                name.rectTransform.offsetMax = new Vector2(-4f, -2f);
                var icon = IconImage("Icon", b.transform, ArenaIcons.For(s), Color.white);
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.72f);
                icon.rectTransform.pivot = new Vector2(0f, 0.5f);
                icon.rectTransform.anchoredPosition = new Vector2(10f, 0f);
                icon.rectTransform.sizeDelta = new Vector2(22f, 22f);
                var tag = CombatLabUi.Label("Tagline", b.transform, CombatLabText.SchoolTagline(s), CombatLabStyle.MinFont,
                    TextAnchor.MiddleCenter);
                tag.raycastTarget = false;
                Place(tag.rectTransform, 0f, 0f, 1f, 0.46f);
                tag.rectTransform.offsetMin = new Vector2(2f, 3f);
                tag.rectTransform.offsetMax = new Vector2(-2f, 0f);
                schoolButtons.Add(b);
                schoolTaglines.Add(tag);
            }

            var tools = Corner("Tools", hudRoot, new Vector2(1f, 1f), new Vector2(-16f, -108f), new Vector2(330f, 46f), new Color(0, 0, 0, 0));
            var row = CombatLabUi.Horizontal(tools, 8f);
            row.childForceExpandWidth = true;
            logToggle = CombatLabUi.Button("LogToggle", tools, "戰鬥紀錄 ▸", ToggleLog, CombatLabStyle.Body);
            resetButton = CombatLabUi.Button("Reset", tools, "重置戰鬥", OnReset, CombatLabStyle.Body);

            logPanel = Corner("LogPanel", hudRoot, new Vector2(1f, 1f), new Vector2(-16f, -162f), new Vector2(420f, 250f), HudPanelColor);
            CombatLabUi.Vertical(logPanel, 6f, 8);
            logText = CombatLabUi.ScrollText("Log", logPanel, CombatLabStyle.Small, out logScroll);
            CombatLabUi.Size(logScroll, -1, 1);
            logPanel.gameObject.SetActive(false);
        }

        private void BuildToast()
        {
            var t = Corner("Toast", hudRoot, new Vector2(0.5f, 1f), new Vector2(-60f, -16f), new Vector2(480f, 64f), HudPanelColor);
            t.pivot = new Vector2(0.5f, 1f);
            toastGroup = t.gameObject.AddComponent<CanvasGroup>();
            toastGroup.blocksRaycasts = false;
            toastText = CombatLabUi.Label("Text", t, "", CombatLabStyle.Small, TextAnchor.MiddleCenter, CombatLabUi.Highlight);
            CombatLabUi.Stretch(toastText.rectTransform, 6f);
            t.gameObject.AddComponent<RectMask2D>();
        }

        private void BuildHand()
        {
            float width = RuleSet.HandSize * HandCardWidth + (RuleSet.HandSize - 1) * HandSpacing;
            handRow = CombatLabUi.Rect("Hand", hudRoot);
            handRow.anchorMin = handRow.anchorMax = new Vector2(0.5f, 0f);
            handRow.pivot = new Vector2(0.5f, 0f);
            handRow.anchoredPosition = new Vector2(HandOffsetX, 14f);
            handRow.sizeDelta = new Vector2(width, HandCardHeight);
            handGroup = handRow.gameObject.AddComponent<CanvasGroup>();
            var h = CombatLabUi.Horizontal(handRow, HandSpacing);
            h.childForceExpandWidth = true;
            for (int i = 0; i < RuleSet.HandSize; i++)
            {
                int slot = i;
                cards.Add(BuildCard(slot));
                handButtons.Add(cards[i].Button);
            }
        }

        /// <summary>
        /// 一張手牌的牌面：學派色頂條、元素名稱、AP 消耗、主要功能圖示（及次要功能小圖示）、效果名稱、
        /// 主要數值簡述與功能分類；已編排時顯示順序徽章。
        /// </summary>
        private CardFace BuildCard(int slot)
        {
            var rt = CombatLabUi.Rect("Hand_" + slot, handRow);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = CombatLabUi.ButtonColor;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
            b.colors = colors;
            b.onClick.AddListener(() => OnHandClicked(slot));
            rt.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            AddHover(b, () => ShowCardTooltip(slot), () => HideCardTooltip(slot));

            var c = new CardFace { Button = b, Background = bg };
            var stripe = CombatLabUi.Panel("SchoolStripe", rt, Color.white);
            Place(stripe, 0f, 1f, 1f, 1f);
            stripe.offsetMin = new Vector2(0f, -6f);
            c.SchoolStripe = stripe.GetComponent<Image>();
            c.SchoolStripe.raycastTarget = false;

            c.Element = CombatLabUi.Label("Element", rt, "", CombatLabStyle.Body, TextAnchor.MiddleLeft);
            c.Element.fontStyle = FontStyle.Bold;
            Place(c.Element.rectTransform, 0f, 1f, 1f, 1f);
            c.Element.rectTransform.offsetMin = new Vector2(7f, -32f);
            c.Element.rectTransform.offsetMax = new Vector2(-40f, -7f);

            var apBg = CombatLabUi.Panel("ApBadge", rt, new Color(0f, 0f, 0f, 0.55f));
            apBg.anchorMin = apBg.anchorMax = new Vector2(1f, 1f);
            apBg.pivot = new Vector2(1f, 1f);
            apBg.anchoredPosition = new Vector2(-5f, -9f);
            apBg.sizeDelta = new Vector2(36f, 22f);
            apBg.GetComponent<Image>().raycastTarget = false;
            c.Ap = CombatLabUi.Label("Ap", apBg, "", CombatLabStyle.MinFont, TextAnchor.MiddleCenter, CombatLabUi.Highlight);
            c.Ap.horizontalOverflow = HorizontalWrapMode.Overflow;
            CombatLabUi.Stretch(c.Ap.rectTransform);

            c.Icon = IconImage("RoleIcon", rt, ArenaIcon.Attack, Color.white);
            c.Icon.rectTransform.anchorMin = c.Icon.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            c.Icon.rectTransform.pivot = new Vector2(0.5f, 1f);
            c.Icon.rectTransform.anchoredPosition = new Vector2(-10f, -36f);
            c.Icon.rectTransform.sizeDelta = new Vector2(44f, 44f);
            for (int k = 0; k < 2; k++)
            {
                var sub = IconImage("SubIcon_" + k, rt, ArenaIcon.Attack, Color.white);
                sub.rectTransform.anchorMin = sub.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                sub.rectTransform.pivot = new Vector2(0f, 1f);
                sub.rectTransform.anchoredPosition = new Vector2(16f, -38f - k * 22f);
                sub.rectTransform.sizeDelta = new Vector2(19f, 19f);
                c.SubIcons.Add(sub);
            }

            c.Effect = CombatLabUi.Label("Effect", rt, "", CombatLabStyle.Section, TextAnchor.MiddleCenter);
            c.Effect.fontStyle = FontStyle.Bold;
            Place(c.Effect.rectTransform, 0f, 0f, 1f, 0f);
            c.Effect.rectTransform.offsetMin = new Vector2(2f, 64f);
            c.Effect.rectTransform.offsetMax = new Vector2(-2f, 90f);

            c.Main = CombatLabUi.Label("Main", rt, "", CombatLabStyle.MinFont, TextAnchor.MiddleCenter);
            Place(c.Main.rectTransform, 0f, 0f, 1f, 0f);
            c.Main.rectTransform.offsetMin = new Vector2(3f, 25f);
            c.Main.rectTransform.offsetMax = new Vector2(-3f, 65f);

            c.Role = CombatLabUi.Label("Role", rt, "", CombatLabStyle.MinFont, TextAnchor.MiddleCenter, CombatLabUi.MutedText);
            Place(c.Role.rectTransform, 0f, 0f, 1f, 0f);
            c.Role.rectTransform.offsetMin = new Vector2(2f, 3f);
            c.Role.rectTransform.offsetMax = new Vector2(-2f, 25f);

            c.Badge = CombatLabUi.Panel("OrderBadge", rt, CombatLabUi.Highlight);
            c.Badge.anchorMin = c.Badge.anchorMax = new Vector2(0.5f, 1f);
            c.Badge.pivot = new Vector2(0.5f, 0.5f);
            c.Badge.anchoredPosition = new Vector2(0f, -58f);
            c.Badge.sizeDelta = new Vector2(84f, 26f);
            c.Badge.GetComponent<Image>().raycastTarget = false;
            c.BadgeText = CombatLabUi.Label("Text", c.Badge, "", CombatLabStyle.Small, TextAnchor.MiddleCenter, CombatLabUi.DarkText);
            c.BadgeText.fontStyle = FontStyle.Bold;
            c.BadgeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            CombatLabUi.Stretch(c.BadgeText.rectTransform);
            c.Badge.gameObject.SetActive(false);
            foreach (var t in rt.GetComponentsInChildren<Text>(true)) t.raycastTarget = false;
            return c;
        }

        private void BuildPlan()
        {
            planPanel = Corner("Plan", hudRoot, new Vector2(1f, 0f), new Vector2(-16f, 14f), new Vector2(PlanWidth, PlanHeight), HudPanelColor);
            var v = CombatLabUi.Vertical(planPanel, 4f, 10);
            v.childForceExpandHeight = false;

            var titleRow = CombatLabUi.Rect("TitleRow", planPanel);
            var tr = CombatLabUi.Horizontal(titleRow, 6f);
            tr.childForceExpandWidth = false;
            CombatLabUi.Size(titleRow, 26);
            planIcon = IconImage("SchoolIcon", titleRow, ArenaIcon.SchoolStructure, Color.white);
            CombatLabUi.Size(planIcon, 22, 0, 22, 0);
            planTitle = CombatLabUi.Label("Title", titleRow, "", CombatLabStyle.Section, TextAnchor.MiddleLeft);
            planTitle.fontStyle = FontStyle.Bold;
            CombatLabUi.Size(planTitle, -1, -1, -1, 1);

            for (int i = 0; i < RuleSet.ActionPoints; i++)
            {
                if (i > 0) connectors.Add(BuildConnector(i - 1));
                int index = i;
                var b = CombatLabUi.Button("Slot_" + i, planPanel, "", () => Session.RemoveAt(index), CombatLabStyle.Small);
                var t = b.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                t.lineSpacing = 1.0f;
                CombatLabUi.Stretch(t.rectTransform, 0f);
                t.rectTransform.offsetMin = new Vector2(8f, 2f);
                t.rectTransform.offsetMax = new Vector2(-8f, -2f);
                CombatLabUi.Size(b, 46);
                var outline = b.gameObject.AddComponent<Outline>();
                outline.effectColor = ResonanceColor;
                outline.effectDistance = new Vector2(3f, -3f);
                outline.enabled = false;
                sequenceButtons.Add(b);
                sequenceOutlines.Add(outline);
            }

            resonanceRow = CombatLabUi.Panel("Resonance", planPanel, new Color(0.35f, 0.27f, 0.05f, 0.95f));
            var rr = CombatLabUi.Horizontal(resonanceRow, 6f, 2);
            rr.childForceExpandWidth = false;
            rr.padding = new RectOffset(8, 6, 2, 2);
            CombatLabUi.Size(resonanceRow, 26);
            var star = IconImage("Icon", resonanceRow, ArenaIcon.Resonance, ResonanceColor);
            CombatLabUi.Size(star, 20, 0, 20, 0);
            resonanceText = CombatLabUi.Label("Text", resonanceRow, "", CombatLabStyle.MinFont, TextAnchor.MiddleLeft, ResonanceColor);
            resonanceText.fontStyle = FontStyle.Bold;
            CombatLabUi.Size(resonanceText, -1, -1, -1, 1);
            resonanceRow.gameObject.SetActive(false);

            forecastText = CombatLabUi.Label("Forecast", planPanel, "", CombatLabStyle.MinFont, TextAnchor.UpperLeft);
            forecastText.lineSpacing = 1.05f;
            CombatLabUi.Size(forecastText, -1, 1);
            var actions = CombatLabUi.Rect("Actions", planPanel);
            var row = CombatLabUi.Horizontal(actions, 8f);
            row.childForceExpandWidth = false;
            CombatLabUi.Size(actions, 52);
            redrawButton = CombatLabUi.Button("Redraw", actions, "", ToggleRedrawMode, CombatLabStyle.Body);
            commitButton = CombatLabUi.Button("Commit", actions, "", OnCommitClicked, CombatLabStyle.Button);
            CombatLabUi.Size(redrawButton, -1, -1, 120, 0);
            CombatLabUi.Size(commitButton, -1, -1, -1, 1);
        }

        /// <summary>兩張相鄰牌之間的承接標記（滑鼠移入顯示說明）。</summary>
        private Connector BuildConnector(int index)
        {
            var rt = CombatLabUi.Panel("Connector_" + index, planPanel, new Color(0.18f, 0.15f, 0.08f, 0.95f));
            var h = CombatLabUi.Horizontal(rt, 6f);
            h.childForceExpandWidth = false;
            h.padding = new RectOffset(26, 6, 0, 0);
            CombatLabUi.Size(rt, 24);
            var c = new Connector { Rect = rt };
            c.Icon = IconImage("Icon", rt, ArenaIcon.Link, ResidueColor);
            CombatLabUi.Size(c.Icon, 18, 0, 18, 0);
            c.Text = CombatLabUi.Label("Text", rt, "", CombatLabStyle.MinFont, TextAnchor.MiddleLeft, ResidueColor);
            c.Text.raycastTarget = false;
            CombatLabUi.Size(c.Text, -1, -1, -1, 1);
            var trigger = rt.gameObject.AddComponent<EventTrigger>();
            var on = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            on.callback.AddListener(_ => ShowConnectorTooltip(index));
            var off = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            off.callback.AddListener(_ => HideConnectorTooltip(index));
            trigger.triggers.Add(on);
            trigger.triggers.Add(off);
            rt.gameObject.SetActive(false);
            return c;
        }

        private void BuildTooltip()
        {
            tooltip = CombatLabUi.Panel("CardTooltip", hudRoot, new Color(0.05f, 0.06f, 0.07f, 0.96f));
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
            tooltip.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            tooltipText = CombatLabUi.Label("Text", tooltip, "", CombatLabStyle.Body, TextAnchor.UpperLeft);
            tooltipText.lineSpacing = 1.1f;
            tooltipText.raycastTarget = false;
            tooltip.gameObject.SetActive(false);
        }

        private void BuildBanner()
        {
            banner = CombatLabUi.Panel("ResultBanner", hudRoot, new Color(0.02f, 0.02f, 0.03f, 0.88f));
            banner.anchorMin = new Vector2(0f, 0.5f);
            banner.anchorMax = new Vector2(1f, 0.5f);
            banner.pivot = new Vector2(0.5f, 0.5f);
            banner.anchoredPosition = new Vector2(0f, 60f);
            banner.sizeDelta = new Vector2(0f, 230f);
            var v = CombatLabUi.Vertical(banner, 8f, 18);
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childForceExpandWidth = false;
            bannerTitle = CombatLabUi.Label("Title", banner, "", 64, TextAnchor.MiddleCenter, CombatLabUi.Highlight);
            bannerTitle.fontStyle = FontStyle.Bold;
            CombatLabUi.Size(bannerTitle, 84, 0, 900, 0);
            bannerSubtitle = CombatLabUi.Label("Subtitle", banner, "", CombatLabStyle.Button, TextAnchor.MiddleCenter);
            CombatLabUi.Size(bannerSubtitle, 34, 0, 900, 0);
            var again = CombatLabUi.Button("Again", banner, "再戰一次", OnReset, CombatLabStyle.Button);
            CombatLabUi.Size(again, 52, 0, 240, 0);
            banner.gameObject.SetActive(false);
        }

        private void ClearPlates()
        {
            foreach (var p in plates.Values) if (p.Rect != null) ArenaArt.Kill(p.Rect.gameObject);
            plates.Clear();
        }

        /// <summary>
        /// 敵人頭上資訊：名稱｜失序值條｜數值｜劍形圖示＋預告攻擊（原始數字）與狀態圖示｜本回合預計影響（來自完整預覽）。
        /// </summary>
        private void BuildPlate(int id)
        {
            var rt = CombatLabUi.Panel("Plate_" + id, plateLayer, new Color(0.04f, 0.04f, 0.06f, 0.82f));
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(240f, 142f);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = rt.GetComponent<Image>();
            int enemyId = id;
            b.onClick.AddListener(() => { if (!IsPlaying) Session.SelectTarget(enemyId); });
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            var v = CombatLabUi.Vertical(rt, 2f, 8);
            v.childForceExpandHeight = false;
            var p = new Plate { Rect = rt, Button = b, Group = group };
            p.Name = CombatLabUi.Label("Name", rt, "", CombatLabStyle.Section, TextAnchor.MiddleLeft);
            p.Name.fontStyle = FontStyle.Bold;
            CombatLabUi.Size(p.Name, 24);
            Image ghostImage;
            var bar = Bar("Bar", rt, new Color(0.62f, 0.3f, 0.95f, 1f), out p.Fill, out p.Ghost, out ghostImage);
            p.GhostImage = ghostImage;
            CombatLabUi.Size(bar, 12);
            p.Value = CombatLabUi.Label("Value", rt, "", CombatLabStyle.MinFont, TextAnchor.MiddleLeft);
            CombatLabUi.Size(p.Value, 20);

            var intent = CombatLabUi.Rect("IntentRow", rt);
            var h = CombatLabUi.Horizontal(intent, 4f);
            h.childForceExpandWidth = false;
            h.childAlignment = TextAnchor.MiddleLeft;
            CombatLabUi.Size(intent, 28);
            p.AttackIcon = IconImage("AttackIcon", intent, ArenaIcon.Attack, new Color(1f, 0.55f, 0.42f));
            CombatLabUi.Size(p.AttackIcon, 24, 0, 24, 0);
            p.Attack = CombatLabUi.Label("Attack", intent, "", CombatLabStyle.Important, TextAnchor.MiddleLeft, new Color(1f, 0.7f, 0.6f));
            p.Attack.fontStyle = FontStyle.Bold;
            p.Attack.horizontalOverflow = HorizontalWrapMode.Overflow;
            CombatLabUi.Size(p.Attack, -1, -1, 34, 0);
            p.AttackLabel = CombatLabUi.Label("AttackLabel", intent, "預告攻擊", CombatLabStyle.MinFont, TextAnchor.MiddleLeft, CombatLabUi.MutedText);
            CombatLabUi.Size(p.AttackLabel, -1, -1, 70, 0);
            var statusIcons = new[] { ArenaIcon.Link, ArenaIcon.Sustain, ArenaIcon.Bind };
            var statusNames = new[] { "連結", "校正", "束縛" };
            var statusColors = new[] { new Color(0.82f, 0.62f, 1f), new Color(0.45f, 0.95f, 0.88f), new Color(0.95f, 0.9f, 0.55f) };
            for (int i = 0; i < statusIcons.Length; i++)
            {
                var item = CombatLabUi.Rect("Status_" + statusNames[i], intent);
                var ih = CombatLabUi.Horizontal(item, 1f);
                ih.childForceExpandWidth = false;
                CombatLabUi.Size(item, -1, -1, 34, 0);
                var ic = IconImage("Icon", item, statusIcons[i], statusColors[i]);
                CombatLabUi.Size(ic, 18, 0, 18, 0);
                var n = CombatLabUi.Label(statusNames[i], item, "", CombatLabStyle.MinFont, TextAnchor.MiddleLeft, statusColors[i]);
                n.horizontalOverflow = HorizontalWrapMode.Overflow;
                CombatLabUi.Size(n, -1, -1, 14, 0);
                p.StatusItems.Add(item.gameObject);
                p.StatusTexts.Add(n);
            }

            p.Impact = CombatLabUi.Label("Impact", rt, "", CombatLabStyle.MinFont, TextAnchor.UpperLeft);
            p.Impact.fontStyle = FontStyle.Bold;
            CombatLabUi.Size(p.Impact, 38);
            foreach (var t in rt.GetComponentsInChildren<Text>()) t.raycastTarget = false;
            plates[id] = p;
        }

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

        // ---------------- 操作 ----------------

        private void ToggleLog()
        {
            bool show = !logPanel.gameObject.activeSelf;
            logPanel.gameObject.SetActive(show);
            logToggle.GetComponentInChildren<Text>().text = show ? "戰鬥紀錄 ▾" : "戰鬥紀錄 ▸";
            if (show)
            {
                Canvas.ForceUpdateCanvases();
                logScroll.verticalNormalizedPosition = 0f;
            }
        }

        private void OnHandClicked(int slot)
        {
            if (IsPlaying || slot < 0 || slot >= handIds.Count) return;
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
            if (IsPlaying || Session.IsOver || Session.Snapshot().RedrawsRemaining <= 0) return;
            redrawMode = !redrawMode;
            Refresh();
        }

        // ---------------- 說明框 ----------------

        public void ShowCardTooltip(int slot)
        {
            if (tooltip == null || IsPlaying || slot < 0 || slot >= handIds.Count || !handButtons[slot].gameObject.activeSelf)
            {
                HideCardTooltip(-1);
                return;
            }
            hoveredSlot = slot;
            hoveredConnector = -1;
            tooltipText.text = CombatLabCardInfo.Tooltip(Session, handIds[slot], redrawMode, "點擊場景中的敵人可更換");
            OpenTooltipAbove((RectTransform)handButtons[slot].transform);
        }

        public void HideCardTooltip(int slot)
        {
            if (slot >= 0 && slot != hoveredSlot) return;
            hoveredSlot = -1;
            if (tooltip != null && hoveredConnector < 0) tooltip.gameObject.SetActive(false);
        }

        /// <summary>顯示第 index 個承接標記的說明（規則說明＋本次實際結果）。</summary>
        public void ShowConnectorTooltip(int index)
        {
            if (index < 0 || index >= connectors.Count || !connectors[index].Rect.gameObject.activeSelf) return;
            hoveredConnector = index;
            hoveredSlot = -1;
            tooltipText.text = connectors[index].Tip;
            tooltip.gameObject.SetActive(true);
            tooltip.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltip);
            // 放在編排區左側，與標記同高。
            var corners = new Vector3[4];
            connectors[index].Rect.GetWorldCorners(corners);
            Vector2 left = hudRoot.InverseTransformPoint((corners[0] + corners[1]) * 0.5f);
            var area = hudRoot.rect;
            float x = left.x - 12f - tooltip.rect.width * 0.5f;
            float y = Mathf.Clamp(left.y - tooltip.rect.height * 0.5f, area.yMin + 4f, area.yMax - 4f - tooltip.rect.height);
            tooltip.anchoredPosition = new Vector2(Mathf.Max(x, area.xMin + 4f + tooltip.rect.width * 0.5f), y);
        }

        public void HideConnectorTooltip(int index)
        {
            if (index >= 0 && index != hoveredConnector) return;
            hoveredConnector = -1;
            if (tooltip != null && hoveredSlot < 0) tooltip.gameObject.SetActive(false);
        }

        private void OpenTooltipAbove(RectTransform target)
        {
            tooltip.gameObject.SetActive(true);
            tooltip.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltip);
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 top = hudRoot.InverseTransformPoint((corners[1] + corners[2]) * 0.5f);
            var area = hudRoot.rect;
            float half = tooltip.rect.width * 0.5f;
            float x = Mathf.Clamp(top.x, area.xMin + half + 4f, area.xMax - half - 4f);
            float y = top.y + 8f;
            if (y + tooltip.rect.height > area.yMax - 4f) y = area.yMax - 4f - tooltip.rect.height;
            tooltip.anchoredPosition = new Vector2(x, y);
        }

        // ---------------- 更新 ----------------

        /// <summary>世界座標 → HUD 座標（相對於 HUD 中心）；在攝影機後方時回傳 null。</summary>
        public Vector2? WorldToHud(Vector3 world)
        {
            if (cam == null || hudRoot == null) return null;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) return null;
            Vector2 local;
            var uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRoot, sp, uiCam, out local)) return null;
            return local;
        }

        private void PositionPlates()
        {
            foreach (var e in enemies)
            {
                Plate p;
                if (!plates.TryGetValue(e.Id, out p) || e.Root == null) continue;
                var pos = WorldToHud(EnemyHead(e));
                if (pos.HasValue) p.Rect.anchoredPosition = pos.Value;
            }
            if (toastGroup != null && !string.IsNullOrEmpty(toastText.text))
            {
                toastTime += Time.deltaTime;
                toastGroup.alpha = toastTime < 3.5f ? 1f : Mathf.Clamp01(1f - (toastTime - 3.5f));
            }
        }

        /// <summary>依引擎快照（或播放中的顯示狀態）更新整個 HUD。</summary>
        public void Refresh()
        {
            if (roundText == null || Session == null) return;
            var snap = Session.Snapshot();
            bool over = snap.Outcome != BattleOutcome.Ongoing;
            bool playing = IsPlaying || committing;
            if (over) redrawMode = false;
            if (!playing) SyncDisplayFromSnapshot(snap);

            string msg = playing ? "第 " + Session.LastRoundNumber + " 回合結算中…" : Session.Message ?? "";
            if (msg != lastToast)
            {
                lastToast = msg;
                toastText.text = msg;
                toastTime = 0f;
                toastGroup.alpha = 1f;
            }

            RefreshStatus(snap, playing);
            RefreshSchools(snap, over, playing);
            RefreshPlates(playing);
            RefreshHand(snap, over, playing);
            RefreshPlan(snap, over, playing);
            logText.text = string.Join("\n", Session.Log);
            if (logPanel.gameObject.activeSelf)
            {
                Canvas.ForceUpdateCanvases();
                logScroll.verticalNormalizedPosition = 0f;
            }
            if (!playing) banner.gameObject.SetActive(over);
            PositionPlates();
            if (hoveredSlot >= 0) ShowCardTooltip(hoveredSlot);
            else if (hoveredConnector >= 0) ShowConnectorTooltip(hoveredConnector);
        }

        private void RefreshStatus(BattleSnapshot snap, bool playing)
        {
            string school = snap.LockedSchool.HasValue
                ? CombatLabText.School(snap.LockedSchool.Value) + "（已鎖定）"
                : CombatLabText.School(Session.SelectedSchool) + "（確認時鎖定）";
            int round = playing ? Session.LastRoundNumber : snap.Round + 1;
            roundText.text = "<b>第 " + round + " 回合</b>　" + (playing ? "結算中…" : school);
            int max = Session.Rules.MaxAnchor;
            var f = Session.Forecast;
            if (!playing && f != null)
            {
                SetBar(anchorFill, anchorGhost, anchorGhostImage, displayAnchor, f.AnchorAfter, max, AnchorLossColor);
                anchorText.text = "錨定值 " + displayAnchor + " / " + max + (f.AnchorAfter != displayAnchor ? "　→ " + f.AnchorAfter : "");
                shieldText.text = "擬態錨定 " + displayShield + (f.ShieldAfter != displayShield ? " → " + f.ShieldAfter : "")
                    + "<size=" + CombatLabStyle.MinFont + "><color=#9FB4D8>（回合後）</color></size>";
                // 威脅：預告攻擊總量、擬態吸收、錨定值損失都取自完整預覽的敵方事件，不是預告值直接相加。
                int attack = f.TotalAttack;
                var sb = new StringBuilder();
                if (attack <= 0) sb.Append("本回合敵人不會造成攻擊");
                else
                {
                    sb.Append("攻擊 <b>").Append(attack).Append("</b>：");
                    sb.Append(CombatLabText.Colored("擬態吸收 " + f.TotalShieldAbsorbed, "#8CC4FF")).Append("、");
                    sb.Append(f.TotalAnchorLoss > 0 ? CombatLabText.Colored("錨定值 −" + f.TotalAnchorLoss, CombatLabText.Bad)
                        : CombatLabText.Colored("錨定值無損", CombatLabText.Good));
                    int diverted = attack - f.TotalShieldAbsorbed - f.TotalAnchorLoss;
                    if (diverted > 0) sb.Append("、分流 ").Append(diverted);
                }
                threatText.text = sb.ToString();
            }
            else
            {
                SetBar(anchorFill, anchorGhost, anchorGhostImage, displayAnchor, displayAnchor, max, AnchorLossColor);
                anchorText.text = "錨定值 " + displayAnchor + " / " + max;
                shieldText.text = "擬態錨定 " + displayShield;
                threatText.text = playing ? "結算中…" : "";
            }
            int left = playing ? 0 : Session.ApRemaining;
            apText.text = "AP " + left + " / " + Session.ApMax;
            for (int i = 0; i < apPips.Count; i++)
                apPips[i].color = i < left ? CombatLabUi.Highlight : new Color(0.25f, 0.25f, 0.28f, 1f);
        }

        private static Color SchoolButtonColor(SchoolKind s)
        {
            return Color.Lerp(new Color(0.12f, 0.13f, 0.16f, 1f), ArenaIcons.SchoolColor(s), 0.38f);
        }

        private void RefreshSchools(BattleSnapshot snap, bool over, bool playing)
        {
            for (int i = 0; i < schoolButtons.Count; i++)
            {
                var s = Schools[i];
                bool selected = s == Session.SelectedSchool;
                bool allowed = !over && !playing && (!snap.LockedSchool.HasValue || snap.LockedSchool.Value == s);
                var color = selected ? ArenaIcons.SchoolColor(s) : SchoolButtonColor(s);
                CombatLabUi.SetButton(schoolButtons[i], CombatLabText.School(s), color, allowed,
                    selected ? CombatLabUi.DarkText : (Color?)null);
                schoolTaglines[i].color = selected ? new Color(0.12f, 0.1f, 0.08f) : CombatLabUi.MutedText;
                var icon = schoolButtons[i].transform.Find("Icon").GetComponent<Image>();
                icon.color = selected ? CombatLabUi.DarkText : ArenaIcons.SchoolColor(s);
            }
        }

        private void RefreshPlates(bool playing)
        {
            var f = Session.Forecast;
            foreach (var e in enemies)
            {
                Plate p;
                if (!plates.TryGetValue(e.Id, out p)) continue;
                DisplayEnemy d;
                if (!display.TryGetValue(e.Id, out d)) continue;
                int max = maxValues[e.Id];
                bool target = Session.SelectedTargetId.HasValue && Session.SelectedTargetId.Value == e.Id;
                p.Name.text = (target ? "◎ " : "") + e.Name + (target ? "<size=" + CombatLabStyle.MinFont + "><color=#E6BD52>　目標</color></size>" : "");
                p.Rect.GetComponent<Image>().color = target ? new Color(0.22f, 0.17f, 0.05f, 0.9f) : new Color(0.04f, 0.04f, 0.06f, 0.82f);
                for (int i = 0; i < p.StatusItems.Count; i++)
                {
                    int n = i == 0 ? d.Links : i == 1 ? d.Corrections : d.Bind;
                    p.StatusItems[i].SetActive(!d.Resolved && n > 0);
                    p.StatusTexts[i].text = n.ToString();
                }
                if (d.Resolved)
                {
                    SetBar(p.Fill, p.Ghost, p.GhostImage, 0, 0, max, EnemyLossColor);
                    p.Value.text = "已解除";
                    p.Attack.text = "";
                    p.AttackIcon.gameObject.SetActive(false);
                    p.Attack.gameObject.SetActive(false);
                    p.AttackLabel.gameObject.SetActive(false);
                    p.Impact.text = "";
                    p.Group.alpha = 0.55f;
                    p.Button.interactable = false;
                    continue;
                }
                p.Group.alpha = 1f;
                p.Button.interactable = !playing && !Session.IsOver;
                var ef = !playing && f != null ? f.Enemy(e.Id) : null;
                int after = ef != null ? ef.ValueAfter : d.Value;
                SetBar(p.Fill, p.Ghost, p.GhostImage, d.Value, after, max, EnemyLossColor);
                p.Value.text = "失序值 <b>" + d.Value + "</b> / " + max
                    + (after != d.Value ? "　→ " + CombatLabText.Colored("<b>" + after + "</b>", CombatLabText.Good)
                       + (ef != null && ef.ResolvedAfter ? CombatLabText.Colored(" 解除", CombatLabText.Good) : "") : "");

                // 播放中引擎已進入下一回合，預告值改用正在播放那一回合的敵方事件。
                int forecastAttack;
                if (playing)
                {
                    var roundEnemy = Session.LastRound != null ? Session.LastRound.Enemy(e.Id) : null;
                    forecastAttack = roundEnemy != null && roundEnemy.Acted ? roundEnemy.Attack : 0;
                }
                else
                {
                    var snapEnemy = FindSnapEnemy(e.Id);
                    forecastAttack = snapEnemy != null ? snapEnemy.ForecastAttack : 0;
                }
                p.AttackIcon.gameObject.SetActive(true);
                p.Attack.gameObject.SetActive(true);
                p.AttackLabel.gameObject.SetActive(true);
                p.Attack.text = forecastAttack.ToString();
                p.AttackLabel.text = "預告攻擊";
                p.AttackIcon.color = forecastAttack > 0 ? new Color(1f, 0.55f, 0.42f) : new Color(0.5f, 0.5f, 0.55f);
                p.Impact.text = playing ? "" : ef != null ? "預計：" + CombatLabText.EnemyImpactShort(ef, f.Outcome) : "";
            }
        }

        private EnemySnapshot FindSnapEnemy(int id)
        {
            foreach (var e in Session.Snapshot().Enemies) if (e.Id == id) return e;
            return null;
        }

        private static Color HandColor(ElementKind e)
        {
            switch (e)
            {
                case ElementKind.Ember: return new Color(0.5f, 0.22f, 0.13f, 0.96f);
                case ElementKind.Base: return new Color(0.18f, 0.29f, 0.48f, 0.96f);
                default: return new Color(0.14f, 0.38f, 0.3f, 0.96f);
            }
        }

        private static Color ElementTextColor(ElementKind e)
        {
            switch (e)
            {
                case ElementKind.Ember: return new Color(1f, 0.78f, 0.62f);
                case ElementKind.Base: return new Color(0.72f, 0.85f, 1f);
                default: return new Color(0.7f, 1f, 0.82f);
            }
        }

        private void RefreshHand(BattleSnapshot snap, bool over, bool playing)
        {
            handIds.Clear();
            handGroup.alpha = playing ? 0.35f : 1f;
            var school = Session.SelectedSchool;
            var rules = Session.Rules;
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                if (i >= snap.Hand.Count)
                {
                    c.Button.gameObject.SetActive(false);
                    continue;
                }
                c.Button.gameObject.SetActive(true);
                var card = snap.Hand[i];
                handIds.Add(card.Id);
                int order = Session.StagedIndex(card.Id);

                // 牌面資訊全部來自目前學派下的效果表（RuleSet）。
                c.Roles = CombatLabText.Roles(rules, school, card.Element);
                c.SchoolStripe.color = ArenaIcons.SchoolColor(school);
                c.Element.text = CombatLabText.Element(card.Element);
                c.Element.color = ElementTextColor(card.Element);
                c.Ap.text = CombatLabText.CardApCost + " AP";
                c.Icon.sprite = ArenaIcons.Get(ArenaIcons.For(c.Roles[0]));
                c.Icon.color = ArenaIcons.RoleColor(c.Roles[0]);
                for (int k = 0; k < c.SubIcons.Count; k++)
                {
                    bool show = k + 1 < c.Roles.Count;
                    c.SubIcons[k].gameObject.SetActive(show);
                    if (!show) continue;
                    c.SubIcons[k].sprite = ArenaIcons.Get(ArenaIcons.For(c.Roles[k + 1]));
                    c.SubIcons[k].color = ArenaIcons.RoleColor(c.Roles[k + 1]);
                }
                c.Effect.text = CombatLabText.EffectName(school, card.Element);
                c.Main.text = CombatLabText.EffectShort(rules, school, card.Element);
                var roleNames = new List<string>();
                foreach (var r in c.Roles) roleNames.Add(CombatLabText.RoleName(r));
                c.Role.text = string.Join(" ", roleNames.ToArray());

                bool staged = order >= 0;
                c.Badge.gameObject.SetActive(staged || (redrawMode && !staged));
                c.BadgeText.text = staged ? "第 " + (order + 1) + " 張" : "點擊調度";
                c.Badge.GetComponent<Image>().color = staged ? CombatLabUi.Highlight : new Color(0.95f, 0.9f, 0.7f);
                Color color = staged ? Color.Lerp(HandColor(card.Element), new Color(0.2f, 0.2f, 0.22f, 0.96f), 0.55f) : HandColor(card.Element);
                if (redrawMode && !staged) color = Color.Lerp(color, CombatLabUi.Highlight, 0.25f);
                c.Background.color = color;
                c.Button.interactable = !over && !playing && !(redrawMode && staged);
            }
        }

        private string EnemyShort(int id)
        {
            return ShortName(Session.EnemyName(id));
        }

        private void RefreshPlan(BattleSnapshot snap, bool over, bool playing)
        {
            var staged = Session.Staged;
            var school = Session.SelectedSchool;
            planIcon.sprite = ArenaIcons.Get(ArenaIcons.For(school));
            planIcon.color = ArenaIcons.SchoolColor(school);
            planTitle.text = "術式編排（" + staged.Count + " / " + Session.ApMax + " 張）";
            planTitle.color = ArenaIcons.SchoolColor(school);
            var f = playing ? null : Session.Forecast;
            bool resonance = f != null && f.Resonance;

            for (int i = 0; i < sequenceButtons.Count; i++)
            {
                if (i < staged.Count && !playing)
                {
                    var c = staged[i];
                    string target = Session.CardNeedsTarget(c.Element) && c.TargetEnemyId.HasValue
                        ? " → " + EnemyShort(c.TargetEnemyId.Value) : "";
                    string outcome;
                    var co = f != null ? f.Card(i) : null;
                    if (co != null)
                    {
                        string v = co.PrimaryValue != Session.Rules.PrimaryValue(school, c.Element)
                            ? "（數值 " + Session.Rules.PrimaryValue(school, c.Element) + "→" + co.PrimaryValue + "）" : "";
                        outcome = CombatLabText.CardOutcomeShort(co, EnemyShort) + v;
                    }
                    else outcome = f != null && f.Outcome == BattleOutcome.Victory ? "戰鬥已在前一張勝利，不結算" : "";
                    CombatLabUi.SetButton(sequenceButtons[i], (i + 1) + ". <b>" + CombatLabText.Element(c.Element) + "・"
                        + CombatLabText.EffectName(school, c.Element) + "</b>" + target + "　✕\n<size=" + CombatLabStyle.MinFont
                        + "><color=#D8DCE4>" + outcome + "</color></size>", HandColor(c.Element), !over);
                }
                else
                {
                    CombatLabUi.SetButton(sequenceButtons[i], (i + 1) + ". —", new Color(0.12f, 0.13f, 0.15f, 0.9f), false);
                }
                sequenceOutlines[i].enabled = resonance && i < staged.Count;
            }

            // 承接：相鄰兩張牌之間的標記（數值取自 residue 事件；反應以規則值表示）。
            for (int i = 0; i < connectors.Count; i++)
            {
                var r = f != null ? f.Residue(i + 1) : null;
                var conn = connectors[i];
                bool show = r != null;
                conn.Rect.gameObject.SetActive(show);
                if (!show) continue;
                conn.Text.text = "承接・" + CombatLabText.ResidueName(r.From) + "：" + CombatLabText.ResidueOutcomeShort(Session.Rules, r, EnemyShort);
                conn.Tip = "<b>承接・" + CombatLabText.ResidueName(r.From) + "</b>（第 " + (i + 1) + " 張 " + CombatLabText.Element(r.From)
                    + " → 第 " + (i + 2) + " 張）\n" + CombatLabText.ResidueHint(Session.Rules, r.From)
                    + "\n本次：" + CombatLabText.ResidueOutcomeShort(Session.Rules, r, EnemyShort)
                    + "\n<color=#C8CCD4>只影響緊接的下一張，不累積。</color>";
            }

            resonanceRow.gameObject.SetActive(resonance);
            if (resonance)
                resonanceText.text = "共鳴已觸發：" + CombatLabText.Element(f.ResonanceElement) + "×" + RuleSet.TripleCount
                    + "，主要數值×" + Session.Rules.TripleMultiplier.ToString("0.##");

            if (playing) forecastText.text = "結算中…（空白鍵或「略過動畫」可跳過）";
            else if (over) forecastText.text = "戰鬥已結束。";
            else if (f == null)
                forecastText.text = Session.LastPreview != null && !Session.LastPreview.Validation.IsValid
                    ? "無法結算：" + Session.LastPreview.Validation.Error : "";
            else forecastText.text = ForecastBreakdown(f, Session.CurrentComboGain());

            string redrawLabel = redrawMode ? "取消調度" : "調度 " + snap.RedrawsRemaining;
            CombatLabUi.SetButton(redrawButton, redrawLabel, redrawMode ? CombatLabUi.Highlight : CombatLabUi.ButtonColor,
                !over && !playing && (redrawMode || snap.RedrawsRemaining > 0), redrawMode ? CombatLabUi.DarkText : (Color?)null);
            string commitLabel = playing ? "略過動畫" : over ? "戰鬥結束" : staged.Count == 0 ? "結束回合" : "確認施放（" + staged.Count + "）";
            CombatLabUi.SetButton(commitButton, commitLabel, playing ? CombatLabUi.ButtonColor : new Color(0.2f, 0.46f, 0.3f, 1f),
                playing || !over);
        }

        /// <summary>分段預估：卡牌基礎效果｜承接收益｜共鳴收益｜持續校正｜敵方回合｜回合後。</summary>
        private string ForecastBreakdown(RoundForecast f, ComboGain g)
        {
            var sb = new StringBuilder();
            if (Session.Staged.Count == 0) sb.Append("<b>沒有編排</b>　直接結束回合\n");
            else if (g != null)
            {
                sb.Append("<b>卡牌效果</b>　").Append(Summary(g.Base, "沒有數值變化")).Append('\n');
                if (g.HasResidue) sb.Append(CombatLabText.Colored("<b>＋承接</b>　" + Summary(g.Residue, "沒有額外收益"), "#EAC76B")).Append('\n');
                if (g.HasResonance) sb.Append(CombatLabText.Colored("<b>＋共鳴</b>　" + Summary(g.Resonance, "沒有額外收益"), "#FFD84D")).Append('\n');
            }
            if (f.CorrectionDamage.Count > 0 || f.CorrectionHeal > 0)
            {
                var parts = new List<string>();
                foreach (var kv in f.CorrectionDamage) if (kv.Value > 0) parts.Add(EnemyShort(kv.Key) + " −" + kv.Value);
                if (f.CorrectionHeal > 0) parts.Add("錨定值 +" + f.CorrectionHeal);
                if (parts.Count > 0) sb.Append("<b>持續校正</b>　").Append(string.Join("、", parts.ToArray())).Append('\n');
            }
            sb.Append("<b>敵方回合</b>　").Append(CombatLabText.DamageSummary(f)).Append('\n');
            sb.Append("<b>回合後</b>　錨定值 ").Append(CombatLabText.Change(f.AnchorBefore, f.AnchorAfter))
              .Append("　擬態 ").Append(CombatLabText.Change(f.ShieldBefore, f.ShieldAfter, false));
            return sb.ToString();
        }

        private string Summary(SequenceSummary s, string empty)
        {
            if (s == null || s.IsEmpty) return empty;
            var parts = new List<string>();
            foreach (var kv in s.Damage) if (kv.Value != 0) parts.Add(EnemyShort(kv.Key) + " " + (kv.Value > 0 ? "−" + kv.Value : "+" + (-kv.Value)));
            if (s.Shield != 0) parts.Add("擬態 " + CombatLabText.Signed(s.Shield));
            if (s.Heal != 0) parts.Add("錨定值 " + CombatLabText.Signed(s.Heal));
            foreach (var kv in s.Corrections) if (kv.Value != 0) parts.Add(EnemyShort(kv.Key) + " 校正 " + CombatLabText.Signed(kv.Value));
            foreach (var kv in s.Links) if (kv.Value != 0) parts.Add(EnemyShort(kv.Key) + " 連結 " + CombatLabText.Signed(kv.Value));
            return string.Join("、", parts.ToArray());
        }

        /// <summary>敵人名稱的短稱（「失序體・甲」→「甲」）。</summary>
        private static string ShortName(string name)
        {
            int i = name.LastIndexOf('・');
            return i >= 0 && i < name.Length - 1 ? name.Substring(i + 1) : name;
        }
    }
}
