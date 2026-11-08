using System.Collections.Generic;
using System.Text;
using Shepherd.Battle.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shepherd.Battle.UI.Arena
{
    public sealed class CombatDemoOptions
    {
        /// <summary>顯示 QA 工具（重置戰鬥）。Case01 正式路徑不顯示。</summary>
        public bool ShowQaTools;
        /// <summary>首戰情境教學（第二階段）。</summary>
        public bool Tutorial;
        /// <summary>降低動態（微交互縮短、不做脈動）。</summary>
        public bool ReduceMotion;
    }

    /// <summary>
    /// Case01 公開 Demo 的戰鬥 HUD（覆蓋式 view）：掛在既有 <see cref="CombatArenaView"/> 上，
    /// 只讀 <see cref="CombatLabSession"/> 的快照／預估與 CombatArenaView 的播放狀態，所有操作轉發給同一個 Session 與既有按鈕邏輯，
    /// 不建立第二套 BattleController、不自己計算任何數值。舊 HUD 物件保留（隱藏），其公開存取與測試照舊。
    /// 視線順序：敵人威脅 → AP → 手牌 → 術式序列 → 預估 → 確認。
    /// </summary>
    public sealed class CombatDemoHud : MonoBehaviour
    {
        private static readonly SchoolKind[] Schools = { SchoolKind.Structure, SchoolKind.Process, SchoolKind.Relation };

        private sealed class CardView
        {
            public Button Button;
            public RectTransform Rect;
            public Image Body;
            public Image Band;
            public Outline Frame;
            public Text Symbol;
            public Text Element;
            public Text Cost;
            public Text Name;
            public Text Effect;
            public Text Role;
            public RectTransform Badge;
            public Text BadgeText;
            public CanvasGroup Group;
        }

        private sealed class SlotView
        {
            public Button Button;
            public Image Body;
            public Outline Frame;
            public Text Index;
            public Text Title;
            public Text Detail;
            public Button TargetChip;
            public Text TargetText;
        }

        private sealed class PlateView
        {
            public int Id;
            public RectTransform Rect;
            public Button Button;
            public Image Frame;
            public Text Name;
            public Text Value;
            public RectTransform Fill;
            public RectTransform Ghost;
            public Image GhostImage;
            public Text Intent;
            public Text Status;
            public CanvasGroup Group;
        }

        private CombatArenaView arena;
        private CombatDemoOptions options;
        private Canvas canvas;
        private RectTransform root;
        private bool dirty = true;
        private bool wasPlaying;

        private Text roundText, anchorText, shieldText, threatText;
        private RectTransform anchorFill, anchorGhost;
        private Image anchorGhostImage;
        private readonly List<Image> apGems = new List<Image>();
        private Text apText;
        private Text toastText;
        private CanvasGroup toastGroup;
        private string lastToast;
        private float toastTime;
        private Text schoolHeader;
        private readonly List<Button> schoolTabs = new List<Button>();
        private readonly List<Text> schoolNames = new List<Text>();
        private readonly List<Text> schoolTags = new List<Text>();
        private Button logButton, qaResetButton, replayButton;
        private CombatDemoTutorial tutorial;
        private RectTransform logPanel;
        private Text logText;
        private RectTransform handRow;
        private readonly List<CardView> cards = new List<CardView>();
        private readonly List<int> handIds = new List<int>();
        private RectTransform sequencePanel;
        private Text sequenceTitle;
        private readonly List<SlotView> slots = new List<SlotView>();
        private readonly List<Text> links = new List<Text>();
        private Text resonanceText;
        private Text forecastText;
        private Button redrawButton, clearButton, commitButton;
        private RectTransform detail;
        private Text detailText;
        private int hovered = -1;
        private readonly Dictionary<int, PlateView> plates = new Dictionary<int, PlateView>();
        private readonly Dictionary<int, int> maxValues = new Dictionary<int, int>();

        // ---------------- 建立 ----------------

        /// <summary>把 Demo HUD 掛到戰鬥上（隱藏舊 HUD 的面板，保留舊的結果橫幅、浮字與畫面閃光）。</summary>
        public static CombatDemoHud Attach(CombatArenaView arena, CombatDemoOptions options = null)
        {
            var hud = arena.gameObject.AddComponent<CombatDemoHud>();
            hud.Init(arena, options ?? new CombatDemoOptions());
            return hud;
        }

        public CombatArenaView Arena { get { return arena; } }
        public CombatDemoOptions Options { get { return options; } }
        private CombatLabSession S { get { return arena.Session; } }

        private void Init(CombatArenaView a, CombatDemoOptions o)
        {
            arena = a;
            options = o;
            foreach (var p in arena.HudPanels()) p.gameObject.SetActive(false);
            if (arena.Tooltip != null) arena.Tooltip.gameObject.SetActive(false);
            foreach (var e in S.Snapshot().Enemies) maxValues[e.Id] = e.Value;
            BuildCanvas();
            tutorial = new CombatDemoTutorial(this, arena, root);
            S.Changed += OnSessionChanged;
            Refresh();
            if (options.Tutorial) tutorial.Start();
        }

        /// <summary>「？教學」：任何時候重看（戰鬥結束後不提供）。</summary>
        public void ReplayTutorial()
        {
            if (tutorial == null || S.IsOver) return;
            HideDetail(-1);
            tutorial.Start();
        }

        private void OnDestroy()
        {
            if (tutorial != null) tutorial.Stop();
            if (arena != null && arena.Session != null) arena.Session.Changed -= OnSessionChanged;
            if (canvas != null) CombatLabUi.Kill(canvas.gameObject);
        }

        private void OnSessionChanged() { dirty = true; }

        private void BuildCanvas()
        {
            var go = new GameObject("DemoCombatHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = 5;
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = CombatLabStyle.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = CombatLabStyle.MatchWidthOrHeight;
            root = (RectTransform)go.transform;

            var plateLayer = CombatLabUi.Rect("Plates", root);
            CombatLabUi.Stretch(plateLayer);
            foreach (var e in S.Snapshot().Enemies) BuildPlate(plateLayer, e.Id);
            BuildPlayer();
            BuildAp();
            BuildSchools();
            BuildHand();
            BuildSequence();
            BuildDetail();
        }

        // ---------------- 小工具 ----------------

        private static RectTransform Box(string name, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color, bool frame = true)
        {
            var rt = CombatLabUi.Panel(name, parent, color);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            rt.GetComponent<Image>().raycastTarget = false;
            if (frame)
            {
                var o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(CombatDemoTheme.BrassDim.r, CombatDemoTheme.BrassDim.g, CombatDemoTheme.BrassDim.b, 0.8f);
                o.effectDistance = new Vector2(1.5f, -1.5f);
            }
            return rt;
        }

        private static Text Txt(string name, Transform parent, string text, int size, TextAnchor align, Color color, bool bold = false)
        {
            var t = CombatLabUi.Label(name, parent, text, size, align, color);
            t.raycastTarget = false;
            if (bold) t.fontStyle = FontStyle.Bold;
            return t;
        }

        private static RectTransform At(RectTransform rt, float xMin, float yMin, float xMax, float yMax, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        private static Button Btn(string name, Transform parent, string label, UnityEngine.Events.UnityAction onClick, int size)
        {
            var b = CombatLabUi.Button(name, parent, label, onClick, size);
            var o = b.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.6f);
            return b;
        }

        private static void Bar(Transform parent, Color fill, out RectTransform fillRect, out RectTransform ghostRect, out Image ghostImage)
        {
            var bg = CombatLabUi.Panel("Bar", parent, new Color(0.02f, 0.02f, 0.025f, 0.95f));
            CombatLabUi.Stretch(bg);
            var ghost = CombatLabUi.Panel("Ghost", bg, CombatDemoTheme.Danger);
            At(ghost, 0, 0, 1, 1, 2, 2, 2, 2);
            var f = CombatLabUi.Panel("Fill", bg, fill);
            At(f, 0, 0, 1, 1, 2, 2, 2, 2);
            fillRect = f;
            ghostRect = ghost;
            ghostImage = ghost.GetComponent<Image>();
            foreach (var img in bg.GetComponentsInChildren<Image>()) img.raycastTarget = false;
        }

        private static void SetBar(RectTransform fill, RectTransform ghost, Image ghostImage, int now, int after, int max)
        {
            float a = Mathf.Clamp01(now / (float)Mathf.Max(1, max)), b = Mathf.Clamp01(after / (float)Mathf.Max(1, max));
            fill.anchorMax = new Vector2(Mathf.Min(a, b), 1f);
            ghost.anchorMax = new Vector2(Mathf.Max(a, b), 1f);
            ghostImage.color = b < a ? new Color(1f, 0.55f, 0.42f, 0.8f) : new Color(0.55f, 0.95f, 0.6f, 0.8f);
        }

        private static void Hover(Button b, System.Action enter, System.Action exit)
        {
            var et = b.gameObject.AddComponent<EventTrigger>();
            var e1 = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            e1.callback.AddListener(_ => enter());
            et.triggers.Add(e1);
            var e2 = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            e2.callback.AddListener(_ => exit());
            et.triggers.Add(e2);
        }

        /// <summary>敵人的一字代號（「失序體・甲」→「甲」），用在窄欄位。</summary>
        private string Tag(int enemyId)
        {
            var n = Short(enemyId);
            int i = n.LastIndexOf('・');
            return i >= 0 && i < n.Length - 1 ? n.Substring(i + 1) : n;
        }

        private string Short(int enemyId)
        {
            var n = S.EnemyName(enemyId);
            int i = n.IndexOf(' ');
            return i > 0 ? n.Substring(i + 1) : n;
        }

        // ---------------- 左上：玩家狀態 ----------------

        private void BuildPlayer()
        {
            var p = Box("Player", root, new Vector2(0f, 1f), new Vector2(CombatDemoTheme.Margin, -CombatDemoTheme.Margin), new Vector2(440f, 132f), CombatDemoTheme.Panel);
            roundText = Txt("Round", p, "", CombatDemoTheme.FontBody, TextAnchor.MiddleLeft, CombatDemoTheme.Brass, true);
            At(roundText.rectTransform, 0, 1, 1, 1, 14, -30, 14, 6);
            var barRow = CombatLabUi.Rect("AnchorBar", p);
            At(barRow, 0, 1, 1, 1, 14, -66, 14, 34);
            Bar(barRow, CombatDemoTheme.Anchor, out anchorFill, out anchorGhost, out anchorGhostImage);
            anchorText = Txt("Anchor", barRow, "", CombatDemoTheme.FontLabel, TextAnchor.MiddleCenter, Color.white, true);
            CombatLabUi.Stretch(anchorText.rectTransform);
            anchorText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);
            shieldText = Txt("Shield", p, "", CombatDemoTheme.FontBody, TextAnchor.MiddleLeft, CombatDemoTheme.Shield, true);
            At(shieldText.rectTransform, 0, 1, 1, 1, 14, -94, 14, 68);
            threatText = Txt("Threat", p, "", CombatDemoTheme.FontSmall, TextAnchor.MiddleLeft, CombatDemoTheme.Ivory);
            At(threatText.rectTransform, 0, 1, 1, 1, 14, -126, 14, 96);
        }

        // ---------------- 上方中央：AP 與訊息 ----------------

        private void BuildAp()
        {
            var p = Box("Ap", root, new Vector2(0.5f, 1f), new Vector2(-60f, -CombatDemoTheme.Margin), new Vector2(300f, 62f), CombatDemoTheme.Panel);
            p.pivot = new Vector2(0.5f, 1f);
            var label = Txt("Label", p, CombatDemoText.Ap, CombatDemoTheme.FontTitle, TextAnchor.MiddleLeft, CombatDemoTheme.Brass, true);
            At(label.rectTransform, 0, 0, 0, 1, 14, 18, -64, 0);
            for (int i = 0; i < RuleSet.ActionPoints; i++)
            {
                var gem = CombatLabUi.Panel("Gem_" + i, p, CombatDemoTheme.Brass);
                gem.anchorMin = gem.anchorMax = new Vector2(0f, 0.62f);
                gem.sizeDelta = new Vector2(20f, 20f);
                gem.anchoredPosition = new Vector2(80f + i * 32f, 0f);
                gem.localRotation = Quaternion.Euler(0f, 0f, 45f);
                gem.GetComponent<Image>().raycastTarget = false;
                apGems.Add(gem.GetComponent<Image>());
            }
            apText = Txt("Count", p, "", CombatDemoTheme.FontLabel, TextAnchor.MiddleRight, CombatDemoTheme.Ivory, true);
            At(apText.rectTransform, 0, 0.4f, 1, 1, 0, 0, 14, 0);
            var hint = Txt("Hint", p, CombatDemoText.ApHint, CombatDemoTheme.FontSmall, TextAnchor.MiddleCenter, CombatDemoTheme.Muted);
            At(hint.rectTransform, 0, 0, 1, 0.42f, 0, 2, 0, 0);

            // 訊息放在玩家狀態下方（不擋敵人名牌）。
            var t = Box("Toast", root, new Vector2(0f, 1f), new Vector2(CombatDemoTheme.Margin, -154f), new Vector2(440f, 36f), CombatDemoTheme.Charcoal, false);
            toastGroup = t.gameObject.AddComponent<CanvasGroup>();
            toastGroup.blocksRaycasts = false;
            toastText = Txt("Text", t, "", CombatDemoTheme.FontSmall, TextAnchor.MiddleLeft, CombatDemoTheme.Brass);
            CombatLabUi.Stretch(toastText.rectTransform, 4f);
        }

        // ---------------- 右上：學派 ----------------

        private void BuildSchools()
        {
            var p = Box("Schools", root, new Vector2(1f, 1f), new Vector2(-CombatDemoTheme.Margin, -CombatDemoTheme.Margin), new Vector2(600f, 112f), CombatDemoTheme.Panel);
            schoolHeader = Txt("Header", p, "", CombatDemoTheme.FontSmall, TextAnchor.MiddleLeft, CombatDemoTheme.Muted);
            At(schoolHeader.rectTransform, 0, 1, 1, 1, 12, -26, 12, 4);
            for (int i = 0; i < Schools.Length; i++)
            {
                var s = Schools[i];
                var b = Btn("School_" + s, p, "", () => OnSchool(s), CombatDemoTheme.FontLabel);
                var rt = (RectTransform)b.transform;
                At(rt, i / 3f, 0, (i + 1) / 3f, 1, i == 0 ? 10 : 5, 10, i == 2 ? 10 : 5, 30);
                var label = b.GetComponentInChildren<Text>();
                label.fontStyle = FontStyle.Bold;
                At(label.rectTransform, 0, 0.5f, 1, 1, 6, 0, 6, 2);
                var tag = Txt("Tag", b.transform, CombatLabText.SchoolTagline(s), CombatDemoTheme.FontSmall, TextAnchor.MiddleCenter, CombatDemoTheme.Muted);
                At(tag.rectTransform, 0, 0, 1, 0.52f, 4, 4, 4, 0);
                schoolTabs.Add(b);
                schoolNames.Add(label);
                schoolTags.Add(tag);
            }

            var tools = CombatLabUi.Rect("Tools", root);
            tools.anchorMin = tools.anchorMax = tools.pivot = new Vector2(1f, 1f);
            tools.anchoredPosition = new Vector2(-CombatDemoTheme.Margin, -134f);
            tools.sizeDelta = new Vector2(340f, 36f);
            logButton = Btn("Log", tools, CombatDemoText.Log + " ▸", ToggleLog, CombatDemoTheme.FontSmall);
            At((RectTransform)logButton.transform, 0.5f, 0, 1, 1, 4, 0, 0, 0);
            qaResetButton = Btn("QaReset", tools, CombatDemoText.QaReset, () => arena.ResetButton.onClick.Invoke(), CombatDemoTheme.FontSmall);
            At((RectTransform)qaResetButton.transform, 0, 0, 0.5f, 1, 0, 0, 4, 0);
            qaResetButton.gameObject.SetActive(options.ShowQaTools);
            replayButton = Btn("TutorialReplay", root, CombatDemoText.TutorialReplay, ReplayTutorial, CombatDemoTheme.FontSmall);
            var rr = (RectTransform)replayButton.transform;
            rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(1f, 1f);
            rr.anchoredPosition = new Vector2(-CombatDemoTheme.Margin - 340f - 8f, -134f);
            rr.sizeDelta = new Vector2(96f, 36f);

            logPanel = Box("LogPanel", root, new Vector2(1f, 1f), new Vector2(-CombatDemoTheme.Margin, -176f), new Vector2(440f, 230f), CombatDemoTheme.Charcoal);
            CombatLabUi.Vertical(logPanel, 4f, 8);
            ScrollRect scroll;
            logText = CombatLabUi.ScrollText("Text", logPanel, CombatDemoTheme.FontSmall, out scroll);
            CombatLabUi.Size(scroll, -1, 1);
            logPanel.gameObject.SetActive(false);
        }

        // ---------------- 敵人名牌 ----------------

        private void BuildPlate(RectTransform layer, int id)
        {
            var rt = CombatLabUi.Panel("Plate_" + id, layer, CombatDemoTheme.Charcoal);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(CombatDemoTheme.PlateWidth, 118f);
            var p = new PlateView { Id = id, Rect = rt, Frame = rt.GetComponent<Image>() };
            p.Button = rt.gameObject.AddComponent<Button>();
            p.Button.targetGraphic = p.Frame;
            p.Button.onClick.AddListener(() => OnPlate(id));
            p.Group = rt.gameObject.AddComponent<CanvasGroup>();
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.7f);
            p.Name = Txt("Name", rt, "", CombatDemoTheme.FontBody, TextAnchor.MiddleLeft, CombatDemoTheme.Ivory, true);
            At(p.Name.rectTransform, 0, 1, 1, 1, 10, -26, 10, 4);
            var bar = CombatLabUi.Rect("ValueBar", rt);
            At(bar, 0, 1, 1, 1, 10, -50, 10, 28);
            Bar(bar, CombatDemoTheme.Disorder, out p.Fill, out p.Ghost, out p.GhostImage);
            p.Value = Txt("Value", bar, "", CombatDemoTheme.FontSmall, TextAnchor.MiddleCenter, Color.white, true);
            CombatLabUi.Stretch(p.Value.rectTransform);
            p.Value.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);
            p.Intent = Txt("Intent", rt, "", CombatDemoTheme.FontSmall, TextAnchor.MiddleLeft, CombatDemoTheme.Danger, true);
            At(p.Intent.rectTransform, 0, 1, 1, 1, 10, -76, 10, 54);
            p.Intent.horizontalOverflow = HorizontalWrapMode.Overflow;
            p.Status = Txt("Status", rt, "", CombatDemoTheme.FontSmall, TextAnchor.MiddleLeft, CombatDemoTheme.Muted);
            At(p.Status.rectTransform, 0, 1, 1, 1, 10, -100, 10, 78);
            p.Status.horizontalOverflow = HorizontalWrapMode.Overflow;
            plates[id] = p;
        }

        // ---------------- 下方左：手牌 ----------------

        private void BuildHand()
        {
            float w = 7 * CombatDemoTheme.CardWidth + 6 * CombatDemoTheme.CardSpacing;
            handRow = CombatLabUi.Rect("Hand", root);
            handRow.anchorMin = handRow.anchorMax = handRow.pivot = new Vector2(0f, 0f);
            handRow.anchoredPosition = new Vector2(CombatDemoTheme.Margin + 4f, CombatDemoTheme.Margin);
            handRow.sizeDelta = new Vector2(w, CombatDemoTheme.CardHeight + 22f);
            for (int i = 0; i < 7; i++)
            {
                int slot = i;
                var c = new CardView();
                var rt = CombatLabUi.Panel("Card_" + i, handRow, CombatDemoTheme.PanelSoft);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(CombatDemoTheme.CardWidth, CombatDemoTheme.CardHeight);
                rt.anchoredPosition = new Vector2(i * (CombatDemoTheme.CardWidth + CombatDemoTheme.CardSpacing), 0f);
                c.Rect = rt;
                c.Body = rt.GetComponent<Image>();
                c.Button = rt.gameObject.AddComponent<Button>();
                c.Button.targetGraphic = c.Body;
                c.Button.onClick.AddListener(() => OnCard(slot));
                c.Frame = rt.gameObject.AddComponent<Outline>();
                c.Frame.effectDistance = new Vector2(2f, -2f);
                c.Group = rt.gameObject.AddComponent<CanvasGroup>();
                var band = CombatLabUi.Panel("Band", rt, Color.white);
                At(band, 0, 1, 1, 1, 0, -30, 0, 0);
                c.Band = band.GetComponent<Image>();
                c.Band.raycastTarget = false;
                c.Symbol = Txt("Symbol", band, "", CombatDemoTheme.FontBody, TextAnchor.MiddleLeft, Color.white, true);
                At(c.Symbol.rectTransform, 0, 0, 0, 1, 6, 0, -30, 0);
                c.Element = Txt("Element", band, "", CombatDemoTheme.FontSmall, TextAnchor.MiddleLeft, Color.white, true);
                At(c.Element.rectTransform, 0, 0, 1, 1, 26, 0, 30, 0);
                var cost = CombatLabUi.Panel("Cost", rt, CombatDemoTheme.Charcoal);
                cost.anchorMin = cost.anchorMax = cost.pivot = new Vector2(1f, 1f);
                cost.anchoredPosition = new Vector2(-4f, -4f);
                cost.sizeDelta = new Vector2(24f, 22f);
                cost.GetComponent<Image>().raycastTarget = false;
                c.Cost = Txt("Text", cost, CombatDemoText.CardCost, CombatDemoTheme.FontSmall, TextAnchor.MiddleCenter, CombatDemoTheme.Brass, true);
                CombatLabUi.Stretch(c.Cost.rectTransform);
                c.Name = Txt("Name", rt, "", CombatDemoTheme.FontLabel, TextAnchor.MiddleCenter, CombatDemoTheme.Ivory, true);
                At(c.Name.rectTransform, 0, 1, 1, 1, 4, -66, 4, 34);
                c.Effect = Txt("Effect", rt, "", CombatDemoTheme.FontSmall, TextAnchor.UpperCenter, CombatDemoTheme.Ivory);
                At(c.Effect.rectTransform, 0, 0, 1, 1, 6, 30, 6, 70);
                c.Role = Txt("Role", rt, "", CombatDemoTheme.FontSmall, TextAnchor.MiddleCenter, CombatDemoTheme.Muted);
                At(c.Role.rectTransform, 0, 0, 1, 0, 4, 4, 4, -26);
                var badge = CombatLabUi.Panel("Badge", rt, CombatDemoTheme.Brass);
                badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(0.5f, 1f);
                badge.anchoredPosition = new Vector2(0f, 20f);
                badge.sizeDelta = new Vector2(76f, 22f);
                badge.GetComponent<Image>().raycastTarget = false;
                c.Badge = badge;
                c.BadgeText = Txt("Text", badge, "", CombatDemoTheme.FontSmall, TextAnchor.MiddleCenter, new Color(0.1f, 0.08f, 0.05f), true);
                CombatLabUi.Stretch(c.BadgeText.rectTransform);
                Hover(c.Button, () => ShowDetail(slot), () => HideDetail(slot));
                cards.Add(c);
            }
        }

        // ---------------- 下方右：術式編排、預估、確認 ----------------

        private void BuildSequence()
        {
            sequencePanel = Box("Sequence", root, new Vector2(1f, 0f), new Vector2(-CombatDemoTheme.Margin, CombatDemoTheme.Margin),
                new Vector2(CombatDemoTheme.SequenceWidth, CombatDemoTheme.SequenceHeight), CombatDemoTheme.Panel);
            sequenceTitle = Txt("Title", sequencePanel, "", CombatDemoTheme.FontLabel, TextAnchor.MiddleLeft, CombatDemoTheme.Brass, true);
            At(sequenceTitle.rectTransform, 0, 1, 1, 1, 14, -30, 14, 4);
            var hint = Txt("Hint", sequencePanel, CombatDemoText.SequenceHint, CombatDemoTheme.FontSmall, TextAnchor.MiddleRight, CombatDemoTheme.Muted);
            At(hint.rectTransform, 0, 1, 1, 1, 220, -30, 14, 4);

            float gap = (CombatDemoTheme.SequenceWidth - 28f - 3 * CombatDemoTheme.SlotWidth) / 2f;
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var s = new SlotView();
                var rt = CombatLabUi.Panel("Slot_" + i, sequencePanel, CombatDemoTheme.PanelSoft);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(CombatDemoTheme.SlotWidth, CombatDemoTheme.SlotHeight);
                rt.anchoredPosition = new Vector2(14f + i * (CombatDemoTheme.SlotWidth + gap), -36f);
                s.Body = rt.GetComponent<Image>();
                s.Button = rt.gameObject.AddComponent<Button>();
                s.Button.targetGraphic = s.Body;
                s.Button.onClick.AddListener(() => OnSlot(idx));
                s.Frame = rt.gameObject.AddComponent<Outline>();
                s.Frame.effectDistance = new Vector2(2f, -2f);
                s.Index = Txt("Index", rt, (i + 1).ToString(), CombatDemoTheme.FontBig, TextAnchor.UpperLeft, CombatDemoTheme.BrassDim, true);
                At(s.Index.rectTransform, 0, 0, 0, 1, 8, 0, -32, 2);
                s.Title = Txt("Title", rt, "", CombatDemoTheme.FontBody, TextAnchor.UpperLeft, CombatDemoTheme.Ivory, true);
                At(s.Title.rectTransform, 0, 1, 1, 1, 34, -30, 6, 6);
                s.Detail = Txt("Detail", rt, "", CombatDemoTheme.FontSmall, TextAnchor.UpperLeft, CombatDemoTheme.Muted);
                At(s.Detail.rectTransform, 0, 0, 1, 1, 34, 30, 6, 32);
                s.TargetChip = Btn("Target", rt, "", () => OnRetarget(idx), CombatDemoTheme.FontSmall);
                At((RectTransform)s.TargetChip.transform, 0, 0, 1, 0, 34, 4, 6, -28);
                s.TargetText = s.TargetChip.GetComponentInChildren<Text>();
                CombatLabUi.Stretch(s.TargetText.rectTransform, 1f);
                s.TargetText.horizontalOverflow = HorizontalWrapMode.Overflow;
                slots.Add(s);
                if (i < 2)
                {
                    var l = Txt("Link_" + i, sequencePanel, "", CombatDemoTheme.FontSmall, TextAnchor.MiddleCenter, CombatDemoTheme.Brass, true);
                    var lr = l.rectTransform;
                    lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0f, 1f);
                    lr.sizeDelta = new Vector2(gap + 16f, 60f);
                    lr.anchoredPosition = new Vector2(14f + (i + 1) * CombatDemoTheme.SlotWidth + i * gap - 8f, -60f);
                    links.Add(l);
                }
            }
            resonanceText = Txt("Resonance", sequencePanel, "", CombatDemoTheme.FontSmall, TextAnchor.MiddleLeft, CombatDemoTheme.Brass, true);
            At(resonanceText.rectTransform, 0, 1, 1, 1, 14, -170, 14, 146);
            forecastText = Txt("Forecast", sequencePanel, "", CombatDemoTheme.FontSmall, TextAnchor.UpperLeft, CombatDemoTheme.Ivory);
            At(forecastText.rectTransform, 0, 0, 1, 1, 14, 58, 14, 172);
            forecastText.verticalOverflow = VerticalWrapMode.Truncate;

            redrawButton = Btn("Redraw", sequencePanel, "", () => arena.RedrawButton.onClick.Invoke(), CombatDemoTheme.FontBody);
            At((RectTransform)redrawButton.transform, 0, 0, 0, 0, 14, 10, -164, -50);
            clearButton = Btn("Clear", sequencePanel, CombatDemoText.Clear, OnClear, CombatDemoTheme.FontBody);
            At((RectTransform)clearButton.transform, 0, 0, 0, 0, 174, 10, -274, -50);
            commitButton = Btn("Commit", sequencePanel, "", OnCommit, CombatDemoTheme.FontTitle);
            At((RectTransform)commitButton.transform, 1, 0, 1, 0, -300, 8, 14, -52);
            commitButton.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
        }

        private void BuildDetail()
        {
            detail = Box("Detail", root, new Vector2(0f, 0f), Vector2.zero, new Vector2(CombatDemoTheme.DetailWidth, 10f), CombatDemoTheme.Charcoal);
            detail.pivot = new Vector2(0.5f, 0f);
            var fit = detail.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var v = CombatLabUi.Vertical(detail, 4f, 12);
            v.childForceExpandHeight = false;
            detailText = Txt("Text", detail, "", CombatDemoTheme.FontSmall, TextAnchor.UpperLeft, CombatDemoTheme.Ivory);
            detail.gameObject.SetActive(false);
        }

        // ---------------- 操作（全部轉發給 Session／既有按鈕） ----------------

        public void OnSchool(SchoolKind s)
        {
            if (arena.IsPlaying) return;
            S.SelectSchool(s);
            Refresh();
        }

        public void OnCard(int slot)
        {
            if (arena.IsPlaying || slot < 0 || slot >= handIds.Count) return;
            // 調度模式由既有邏輯處理（洗回並重抽）；一般情況加入／移除編排。
            if (arena.RedrawMode) arena.HandButtons[slot].onClick.Invoke();
            else S.ToggleCard(handIds[slot]);
            Refresh();
        }

        public void OnSlot(int index)
        {
            if (arena.IsPlaying || index >= S.Staged.Count) return;
            S.RemoveAt(index);
            Refresh();
        }

        /// <summary>更換已排入那張牌的目標：依原順序重排一次（只用 Session 的公開操作，數值仍由 Core 驗證與預估）。</summary>
        public void OnRetarget(int index)
        {
            if (arena.IsPlaying || index >= S.Staged.Count) return;
            var staged = new List<StagedCard>(S.Staged);
            if (!S.CardNeedsTarget(staged[index].Element)) return;
            var alive = new List<int>();
            foreach (var e in S.Snapshot().Enemies) if (!e.Resolved) alive.Add(e.Id);
            if (alive.Count < 2) return;
            int cur = staged[index].TargetEnemyId ?? alive[0];
            int next = alive[(alive.IndexOf(cur) + 1) % alive.Count];
            var keep = S.SelectedTargetId;
            S.ClearPlan();
            for (int i = 0; i < staged.Count; i++)
            {
                int? t = i == index ? next : staged[i].TargetEnemyId;
                if (t.HasValue) S.SelectTarget(t.Value);
                S.AddCard(staged[i].CardId);
            }
            if (keep.HasValue) S.SelectTarget(keep.Value);
            Refresh();
        }

        public void OnPlate(int id)
        {
            if (arena.IsPlaying) return;
            S.SelectTarget(id);
            Refresh();
        }

        public void OnClear()
        {
            if (arena.IsPlaying) return;
            S.ClearPlan();
            Refresh();
        }

        /// <summary>確認施放／略過動畫：使用既有的確認流程（Session.Commit → 事件播放），不會重複提交。</summary>
        public void OnCommit()
        {
            HideDetail(-1);
            arena.CommitButton.onClick.Invoke();
            Refresh();
        }

        private void ToggleLog()
        {
            logPanel.gameObject.SetActive(!logPanel.gameObject.activeSelf);
            Refresh();
        }

        public void ShowDetail(int slot)
        {
            if (arena.IsPlaying || slot < 0 || slot >= handIds.Count) return;
            hovered = slot;
            detailText.text = CombatLabCardInfo.Tooltip(S, handIds[slot], arena.RedrawMode, CombatDemoText.DetailTargetHint);
            detail.gameObject.SetActive(true);
            detail.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(detail);
            var card = cards[slot].Rect;
            float x = handRow.anchoredPosition.x + card.anchoredPosition.x + CombatDemoTheme.CardWidth * 0.5f;
            x = Mathf.Clamp(x, CombatDemoTheme.DetailWidth * 0.5f + 8f, root.rect.width - CombatDemoTheme.DetailWidth * 0.5f - 8f);
            detail.anchoredPosition = new Vector2(x, handRow.anchoredPosition.y + CombatDemoTheme.CardHeight + 30f);
        }

        public void HideDetail(int slot)
        {
            if (slot >= 0 && slot != hovered) return;
            hovered = -1;
            if (detail != null) detail.gameObject.SetActive(false);
        }

        // ---------------- 更新 ----------------

        private void LateUpdate()
        {
            if (arena == null) return;
            Tick(Time.deltaTime);
        }

        /// <summary>每影格：跟隨戰鬥 HUD 的顯示狀態、名牌位置、播放中的數值（測試可直接呼叫）。</summary>
        public void Tick(float dt)
        {
            canvas.enabled = arena.HudVisible;
            bool playing = arena.IsPlaying;
            if (dirty || playing || playing != wasPlaying) Refresh();
            wasPlaying = playing;
            PositionPlates();
            if (tutorial != null)
            {
                tutorial.Tick(dt);
                replayButton.gameObject.SetActive(!S.IsOver && !tutorial.Active);
            }
            toastTime += dt;
            toastGroup.alpha = string.IsNullOrEmpty(toastText.text) ? 0f : toastTime < 3.5f ? 1f : Mathf.Clamp01(1f - (toastTime - 3.5f));
        }

        private void PositionPlates()
        {
            foreach (var kv in plates)
            {
                var legacy = arena.PlateRect(kv.Key);
                if (legacy == null) continue;
                // 舊名牌只用來取得位置（同一個參考解析度），本身隱藏。
                if (legacy.gameObject.activeSelf) legacy.gameObject.SetActive(false);
                kv.Value.Rect.anchoredPosition = legacy.anchoredPosition + new Vector2(0f, 6f);
            }
        }

        public void Refresh()
        {
            if (arena == null || S == null || roundText == null) return;
            dirty = false;
            wasPlaying = arena.IsPlaying;
            var snap = S.Snapshot();
            bool over = snap.Outcome != BattleOutcome.Ongoing;
            bool playing = arena.IsPlaying;
            var f = playing ? null : S.Forecast;
            RefreshPlayer(snap, f, playing);
            RefreshAp(playing);
            RefreshSchools(snap, over, playing);
            RefreshPlates(snap, f, playing);
            RefreshHand(snap, over, playing);
            RefreshSequence(snap, f, over, playing);
            string msg = playing ? CombatDemoText.Resolving : S.Message ?? "";
            if (arena.RedrawMode && !playing) msg = CombatDemoText.RedrawHint;
            if (msg != lastToast)
            {
                lastToast = msg;
                toastText.text = msg;
                toastTime = 0f;
            }
            if (logPanel.gameObject.activeSelf) logText.text = string.Join("\n", S.Log);
            if (hovered >= 0 && !playing) ShowDetail(hovered);
            else if (playing) HideDetail(-1);
        }

        private void RefreshPlayer(BattleSnapshot snap, RoundForecast f, bool playing)
        {
            int round = playing ? S.LastRoundNumber : snap.Round + 1;
            roundText.text = string.Format(CombatDemoText.Round, round) + (playing ? "　" + CombatDemoText.Resolving : "");
            int max = S.Rules.MaxAnchor;
            int anchor = arena.DisplayAnchor, shield = arena.DisplayShield;
            if (f != null)
            {
                SetBar(anchorFill, anchorGhost, anchorGhostImage, anchor, f.AnchorAfter, max);
                anchorText.text = CombatDemoText.AnchorName + " " + anchor + " / " + max + (f.AnchorAfter != anchor ? "　→ " + f.AnchorAfter : "");
                shieldText.text = CombatDemoText.ShieldName + " " + shield + (f.ShieldAfter != shield ? " → " + f.ShieldAfter : "")
                                  + "<size=" + (CombatDemoTheme.FontSmall) + "><color=" + CombatDemoTheme.Hex(CombatDemoTheme.Muted) + ">　" + CombatDemoText.ShieldHint + "</color></size>";
                int attack = f.TotalAttack;
                var sb = new StringBuilder();
                sb.Append("<color=").Append(CombatDemoTheme.Hex(CombatDemoTheme.Danger)).Append(">⚔ ").Append(CombatDemoText.ThreatName).Append("</color>　");
                if (attack <= 0) sb.Append("敵人這回合不攻擊");
                else
                {
                    sb.Append("攻擊 ").Append(attack).Append(" → 擬態吸收 ").Append(f.TotalShieldAbsorbed).Append("、錨定值 ");
                    sb.Append(f.TotalAnchorLoss > 0 ? "<color=" + CombatDemoTheme.Hex(CombatDemoTheme.Danger) + ">−" + f.TotalAnchorLoss + "</color>" : "無損");
                }
                sb.Append("<color=").Append(CombatDemoTheme.Hex(CombatDemoTheme.Muted)).Append(">（").Append(CombatDemoText.Estimate).Append("）</color>");
                threatText.text = sb.ToString();
            }
            else
            {
                SetBar(anchorFill, anchorGhost, anchorGhostImage, anchor, anchor, max);
                anchorText.text = CombatDemoText.AnchorName + " " + anchor + " / " + max;
                shieldText.text = CombatDemoText.ShieldName + " " + shield;
                threatText.text = playing ? CombatDemoText.Resolving : "";
            }
        }

        private void RefreshAp(bool playing)
        {
            int left = playing ? 0 : S.ApRemaining;
            for (int i = 0; i < apGems.Count; i++) apGems[i].color = i < left ? CombatDemoTheme.Brass : new Color(0.2f, 0.2f, 0.22f, 1f);
            apText.text = left + " / " + S.ApMax;
        }

        private void RefreshSchools(BattleSnapshot snap, bool over, bool playing)
        {
            schoolHeader.text = CombatDemoText.SchoolHeader + "　<color=" + CombatDemoTheme.Hex(CombatDemoTheme.Faint) + ">"
                                + (snap.LockedSchool.HasValue ? CombatDemoText.SchoolLocked : CombatDemoText.SchoolUnlocked) + "</color>";
            for (int i = 0; i < Schools.Length; i++)
            {
                var s = Schools[i];
                bool selected = s == S.SelectedSchool;
                bool allowed = !over && !playing && (!snap.LockedSchool.HasValue || snap.LockedSchool.Value == s);
                var color = selected ? Color.Lerp(CombatDemoTheme.Primary, ArenaIcons.SchoolColor(s), 0.25f) : CombatDemoTheme.PanelSoft;
                CombatLabUi.SetButton(schoolTabs[i], (selected ? "● " : "") + CombatLabText.School(s), color, allowed,
                    selected ? new Color(0.1f, 0.08f, 0.05f) : (Color?)null);
                schoolTags[i].color = selected ? new Color(0.15f, 0.12f, 0.08f) : CombatDemoTheme.Muted;
            }
        }

        private void RefreshPlates(BattleSnapshot snap, RoundForecast f, bool playing)
        {
            foreach (var e in snap.Enemies)
            {
                PlateView p;
                if (!plates.TryGetValue(e.Id, out p)) continue;
                int max;
                if (!maxValues.TryGetValue(e.Id, out max)) maxValues[e.Id] = max = e.Value;
                int value = arena.DisplayValue(e.Id);
                if (value < 0) value = e.Value;
                bool resolved = playing ? arena.DisplayResolved(e.Id) : e.Resolved;
                bool target = S.SelectedTargetId.HasValue && S.SelectedTargetId.Value == e.Id && !resolved;
                p.Name.text = (target ? "◎ " : "") + e.Name + (target ? "<size=" + (CombatDemoTheme.FontSmall) + "><color=" + CombatDemoTheme.Hex(CombatDemoTheme.Brass) + ">　" + CombatDemoText.Target + "</color></size>" : "");
                p.Frame.color = target ? new Color(0.24f, 0.19f, 0.09f, 0.95f) : CombatDemoTheme.Charcoal;
                if (resolved)
                {
                    SetBar(p.Fill, p.Ghost, p.GhostImage, 0, 0, max);
                    p.Value.text = CombatDemoText.Resolved;
                    p.Intent.text = "";
                    p.Status.text = "";
                    p.Group.alpha = 0.5f;
                    p.Button.interactable = false;
                    continue;
                }
                p.Group.alpha = 1f;
                p.Button.interactable = !playing && !S.IsOver;
                var ef = f != null ? f.Enemy(e.Id) : null;
                int after = ef != null ? ef.ValueAfter : value;
                SetBar(p.Fill, p.Ghost, p.GhostImage, value, after, max);
                p.Value.text = CombatDemoText.DisorderValue + " " + value + " / " + max + (after != value ? "　→ " + after : "");
                p.Intent.text = "⚔ " + CombatDemoText.Intent + " " + e.ForecastAttack;
                var st = new List<string>();
                if (ef != null && f != null) st.Add(CombatLabText.EnemyImpactShort(ef, f.Outcome));
                if (e.Links > 0) st.Add(CombatDemoText.Links + " " + e.Links);
                if (e.Corrections > 0) st.Add(CombatDemoText.Corrections + " " + e.Corrections);
                if (e.Bind > 0) st.Add(CombatDemoText.Bind + " " + e.Bind);
                p.Status.text = string.Join("｜", st.ToArray());
            }
        }

        private void RefreshHand(BattleSnapshot snap, bool over, bool playing)
        {
            handIds.Clear();
            var school = S.SelectedSchool;
            var rules = S.Rules;
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                if (i >= snap.Hand.Count)
                {
                    c.Rect.gameObject.SetActive(false);
                    continue;
                }
                c.Rect.gameObject.SetActive(true);
                var card = snap.Hand[i];
                handIds.Add(card.Id);
                int order = S.StagedIndex(card.Id);
                var ec = CombatDemoTheme.ElementColor(card.Element);
                c.Band.color = ec;
                c.Symbol.text = CombatDemoTheme.ElementSymbol(card.Element);
                c.Element.text = CombatLabText.Element(card.Element);
                c.Name.text = CombatLabText.EffectName(school, card.Element);
                c.Effect.text = CombatLabText.EffectShort(rules, school, card.Element);
                var roles = CombatLabText.Roles(rules, school, card.Element);
                var names = new List<string>();
                foreach (var r in roles) names.Add(CombatLabText.RoleName(r));
                c.Role.text = string.Join("・", names.ToArray());
                bool staged = order >= 0;
                c.Badge.gameObject.SetActive(staged || arena.RedrawMode);
                c.BadgeText.text = staged ? string.Format(CombatDemoText.Staged, order + 1) : "調度？";
                c.Body.color = staged ? Color.Lerp(CombatDemoTheme.CardBody(card.Element), CombatDemoTheme.Charcoal, 0.5f) : CombatDemoTheme.CardBody(card.Element);
                c.Frame.effectColor = staged ? CombatDemoTheme.Brass : new Color(0f, 0f, 0f, 0.7f);
                // 已排入的牌稍微抬起，像被拿到術式上。
                c.Rect.anchoredPosition = new Vector2(c.Rect.anchoredPosition.x, staged ? 14f : 0f);
                c.Group.alpha = playing ? 0.35f : staged ? 0.85f : 1f;
                c.Button.interactable = !over && !playing && !(arena.RedrawMode && staged);
            }
        }

        private void RefreshSequence(BattleSnapshot snap, RoundForecast f, bool over, bool playing)
        {
            var staged = S.Staged;
            var school = S.SelectedSchool;
            sequenceTitle.text = CombatDemoText.SequenceTitle + "　<color=" + CombatDemoTheme.Hex(CombatDemoTheme.Muted) + ">"
                                 + CombatLabText.School(school) + "｜" + staged.Count + " / " + S.ApMax + "</color>";
            bool resonance = f != null && f.Resonance;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (i < staged.Count && !playing)
                {
                    var c = staged[i];
                    var ec = CombatDemoTheme.ElementColor(c.Element);
                    s.Body.color = CombatDemoTheme.CardBody(c.Element);
                    s.Frame.effectColor = resonance ? new Color(1f, 0.85f, 0.35f, 1f) : ec;
                    s.Index.color = ec;
                    s.Title.text = CombatDemoTheme.ElementSymbol(c.Element) + " " + CombatLabText.EffectName(school, c.Element);
                    var co = f != null ? f.Card(i) : null;
                    s.Detail.text = co != null ? CombatLabText.CardOutcomeShort(co, Short) : (f != null && f.Outcome == BattleOutcome.Victory ? "前一張已勝利" : "");
                    bool needs = S.CardNeedsTarget(c.Element) && c.TargetEnemyId.HasValue;
                    s.TargetChip.gameObject.SetActive(true);
                    CombatLabUi.SetButton(s.TargetChip, needs ? string.Format(CombatDemoText.TargetChip, Tag(c.TargetEnemyId.Value)) : CombatDemoText.NoTarget,
                        CombatDemoTheme.Charcoal, needs && !over, CombatDemoTheme.Brass);
                    s.Button.interactable = !over;
                }
                else
                {
                    s.Body.color = new Color(0.06f, 0.065f, 0.075f, 0.9f);
                    s.Frame.effectColor = new Color(CombatDemoTheme.BrassDim.r, CombatDemoTheme.BrassDim.g, CombatDemoTheme.BrassDim.b, 0.5f);
                    s.Index.color = CombatDemoTheme.BrassDim;
                    s.Title.text = "<color=" + CombatDemoTheme.Hex(CombatDemoTheme.Faint) + ">" + CombatDemoText.EmptySlot + "</color>";
                    s.Detail.text = "";
                    s.TargetChip.gameObject.SetActive(false);
                    s.Button.interactable = false;
                }
            }
            for (int i = 0; i < links.Count; i++)
            {
                var r = f != null ? f.Residue(i + 1) : null;
                links[i].text = r != null
                    ? "→\n" + CombatDemoText.ResidueLink + "・" + CombatLabText.ResidueName(r.From) + "\n<color=" + CombatDemoTheme.Hex(CombatDemoTheme.Ivory) + ">" + CombatLabText.ResidueOutcomeShort(S.Rules, r, Short) + "</color>"
                    : (i + 1 < staged.Count ? "→" : "<color=" + CombatDemoTheme.Hex(CombatDemoTheme.Faint) + ">→</color>");
            }
            resonanceText.text = ResonanceLine(staged, f);
            if (playing) forecastText.text = CombatDemoText.Resolving;
            else if (over) forecastText.text = CombatDemoText.Over;
            else if (f == null)
                forecastText.text = S.LastPreview != null && !S.LastPreview.Validation.IsValid ? "無法結算：" + S.LastPreview.Validation.Error : "";
            else forecastText.text = Forecast(f, staged.Count);

            CombatLabUi.SetButton(redrawButton, arena.RedrawMode ? CombatDemoText.RedrawCancel : string.Format(CombatDemoText.Redraw, snap.RedrawsRemaining),
                arena.RedrawMode ? CombatDemoTheme.Brass : CombatDemoTheme.PanelSoft, !over && !playing && (arena.RedrawMode || snap.RedrawsRemaining > 0),
                arena.RedrawMode ? new Color(0.1f, 0.08f, 0.05f) : (Color?)null);
            CombatLabUi.SetButton(clearButton, CombatDemoText.Clear, CombatDemoTheme.PanelSoft, !over && !playing && staged.Count > 0);
            string commit = playing ? CombatDemoText.Skip : over ? CombatDemoText.Over : staged.Count == 0 ? CombatDemoText.EndTurn : string.Format(CombatDemoText.Commit, staged.Count);
            CombatLabUi.SetButton(commitButton, commit, playing ? CombatDemoTheme.PanelSoft : CombatDemoTheme.Primary, playing || !over,
                playing ? (Color?)null : new Color(0.08f, 0.06f, 0.03f));
        }

        private string ResonanceLine(IReadOnlyList<StagedCard> staged, RoundForecast f)
        {
            if (f != null && f.Resonance)
                return "✦ " + string.Format(CombatDemoText.ResonanceReady, CombatLabText.Element(f.ResonanceElement), S.Rules.TripleMultiplier.ToString("0.##"));
            if (staged.Count == 2 && staged[0].Element == staged[1].Element)
                return "◇ " + string.Format(CombatDemoText.ResonanceNear, CombatLabText.Element(staged[0].Element));
            return "<color=" + CombatDemoTheme.Hex(CombatDemoTheme.Faint) + ">◇ " + CombatDemoText.ResonanceRule + "</color>";
        }

        /// <summary>預估：你將得到 → 目標將受到 → 敵方回合 → 回合後。全部取自 Core 的 RoundForecast，不另算。</summary>
        private string Forecast(RoundForecast f, int stagedCount)
        {
            var sb = new StringBuilder();
            string muted = CombatDemoTheme.Hex(CombatDemoTheme.Muted);
            if (stagedCount == 0) sb.Append("<color=").Append(muted).Append(">").Append(CombatDemoText.NothingPlanned).Append("</color>\n");
            var gain = new List<string>();
            int shieldGain = 0, heal = 0;
            foreach (var c in f.Cards)
            {
                shieldGain += c.ShieldGain;
                heal += c.Heal;
            }
            foreach (var r in f.Residues) shieldGain += r.Shield;
            if (shieldGain > 0) gain.Add(CombatDemoText.ShieldName + " +" + shieldGain);
            if (heal > 0) gain.Add(CombatDemoText.AnchorName + " +" + heal);
            if (f.CorrectionHeal > 0) gain.Add("校正回復 +" + f.CorrectionHeal);
            sb.Append("<b>").Append(CombatDemoText.YouGet).Append("</b>　").Append(gain.Count > 0 ? string.Join("、", gain.ToArray()) : CombatDemoText.Nothing).Append('\n');
            var hit = new List<string>();
            foreach (var e in f.Enemies)
            {
                if (e.ResolvedBefore) continue;
                var parts = new List<string>();
                if (e.ValueAfter != e.ValueBefore) parts.Add("失序值 " + e.ValueBefore + "→" + e.ValueAfter + (e.ResolvedAfter ? " 解除" : ""));
                if (e.LinksAfter != e.LinksBefore) parts.Add("連結 " + CombatLabText.Signed(e.LinksAfter - e.LinksBefore));
                if (e.CorrectionsAfter != e.CorrectionsBefore) parts.Add("校正 " + CombatLabText.Signed(e.CorrectionsAfter - e.CorrectionsBefore));
                if (parts.Count > 0) hit.Add(Short(e.Id) + " " + string.Join("、", parts.ToArray()));
            }
            sb.Append("<b>").Append(CombatDemoText.TargetsGet).Append("</b>　").Append(hit.Count > 0 ? string.Join("；", hit.ToArray()) : CombatDemoText.Nothing).Append('\n');
            sb.Append("<b>").Append(CombatDemoText.ThreatName).Append("</b>　").Append(CombatLabText.DamageSummary(f)).Append('\n');
            sb.Append("<b>").Append(CombatDemoText.AfterRound).Append("</b>　").Append(CombatDemoText.AnchorName).Append(' ').Append(f.AnchorBefore).Append("→").Append(f.AnchorAfter)
              .Append("　").Append(CombatDemoText.ShieldName).Append(' ').Append(f.ShieldBefore).Append("→").Append(f.ShieldAfter)
              .Append("<color=").Append(muted).Append(">（").Append(CombatDemoText.Estimate).Append("）</color>");
            return sb.ToString();
        }

        // ---------------- 測試用唯讀存取 ----------------

        public Canvas Canvas { get { return canvas; } }
        public RectTransform Root { get { return root; } }
        public IReadOnlyList<int> HandIds { get { return handIds; } }
        public string CardText(int slot) { var c = cards[slot]; return c.Symbol.text + c.Element.text + "｜" + c.Name.text + "｜" + c.Effect.text + "｜" + c.Role.text; }
        public bool CardStaged(int slot) { return cards[slot].Badge.gameObject.activeSelf && cards[slot].BadgeText.text.StartsWith("第"); }
        public bool CardVisible(int slot) { return cards[slot].Rect.gameObject.activeSelf; }
        public string SlotText(int i) { return slots[i].Title.text + "｜" + slots[i].Detail.text + "｜" + (slots[i].TargetChip.gameObject.activeSelf ? slots[i].TargetText.text : ""); }
        public string LinkText(int i) { return links[i].text; }
        public string ResonanceText { get { return resonanceText.text; } }
        public string ForecastText { get { return forecastText.text; } }
        public string PlayerText { get { return roundText.text + "｜" + anchorText.text + "｜" + shieldText.text + "｜" + threatText.text; } }
        public string ApText { get { return apText.text; } }
        public int ApLit { get { int n = 0; foreach (var g in apGems) if (g.color == CombatDemoTheme.Brass) n++; return n; } }
        public string SchoolText(int i) { return schoolNames[i].text + "｜" + schoolTags[i].text; }
        public bool SchoolInteractable(int i) { return schoolTabs[i].interactable; }
        public string PlateText(int id) { var p = plates[id]; return p.Name.text + "｜" + p.Value.text + "｜" + p.Intent.text + "｜" + p.Status.text; }
        public RectTransform PlateRect(int id) { return plates[id].Rect; }
        public string CommitText { get { return commitButton.GetComponentInChildren<Text>().text; } }
        public bool CommitInteractable { get { return commitButton.interactable; } }
        public string RedrawText { get { return redrawButton.GetComponentInChildren<Text>().text; } }
        public bool ClearInteractable { get { return clearButton.interactable; } }
        public bool QaToolsVisible { get { return qaResetButton.gameObject.activeSelf; } }
        public bool DetailVisible { get { return detail.gameObject.activeSelf; } }
        public string DetailText { get { return detailText.text; } }
        public string ToastText { get { return toastText.text; } }
        public bool LogVisible { get { return logPanel.gameObject.activeSelf; } }
        public void ClickLog() { ToggleLog(); }

        /// <summary>固定面板（測試檢查 1280×720 下不重疊、不出界、不遮住戰場中央）。</summary>
        public IEnumerable<RectTransform> Panels()
        {
            yield return (RectTransform)roundText.transform.parent;
            yield return (RectTransform)apText.transform.parent;
            yield return (RectTransform)schoolHeader.transform.parent;
            yield return handRow;
            yield return sequencePanel;
        }

        // ---- 教學用的高亮目標 ----
        public RectTransform ApRect { get { return (RectTransform)apText.transform.parent; } }
        public RectTransform SchoolsRect { get { return (RectTransform)schoolHeader.transform.parent; } }
        public RectTransform HandRect { get { return handRow; } }
        public RectTransform SequenceRect { get { return sequencePanel; } }
        public RectTransform CommitRect { get { return (RectTransform)commitButton.transform; } }
        public CombatDemoTutorial Tutorial { get { return tutorial; } }
        public bool ReplayVisible { get { return replayButton != null && replayButton.gameObject.activeSelf; } }
        public void ClickReplay() { ReplayTutorial(); }
    }
}
