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
    /// 左上：錨定值／擬態錨定／AP／回合；右上：學派、戰鬥紀錄（可展開）、重置；
    /// 敵人頭上：失序值、攻擊意圖、狀態；下方：七張手牌；右下：術式編排、預估與確認施放。
    /// </summary>
    public sealed partial class CombatArenaView
    {
        private static readonly SchoolKind[] Schools = { SchoolKind.Structure, SchoolKind.Process, SchoolKind.Relation };

        private const float HandCardWidth = 108f;
        private const float HandCardHeight = 150f;
        private const float HandSpacing = 8f;
        private const float HandOffsetX = -110f;
        private const float PlanWidth = 360f;
        private const float PlanHeight = 370f;
        private const float TooltipWidth = 520f;

        private sealed class Plate
        {
            public RectTransform Rect;
            public Button Button;
            public Text Name;
            public Text Value;
            public Text Intent;
            public Text Status;
            public RectTransform Fill;
            public RectTransform Ghost;
            public Image GhostImage;
            public CanvasGroup Group;
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
        private Text apText;
        private readonly List<Image> apPips = new List<Image>();
        private readonly List<Button> schoolButtons = new List<Button>();
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
        private readonly List<Button> handButtons = new List<Button>();
        private readonly List<int> handIds = new List<int>();
        private Text planTitle;
        private readonly List<Button> sequenceButtons = new List<Button>();
        private Text forecastText;
        private Button redrawButton;
        private Button commitButton;
        private RectTransform tooltip;
        private Text tooltipText;
        private int hoveredSlot = -1;
        private RectTransform banner;
        private Text bannerTitle;
        private Text bannerSubtitle;
        private readonly Dictionary<int, Plate> plates = new Dictionary<int, Plate>();
        private bool redrawMode;

        // 供測試使用的唯讀存取。
        public Canvas HudCanvas { get { return canvas; } }
        public RectTransform HudRoot { get { return hudRoot; } }
        public string StatusText { get { return roundText.text + "｜" + anchorText.text + "｜" + shieldText.text + "｜" + apText.text; } }
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

        public string PlateText(int id)
        {
            var p = plates[id];
            return p.Name.text + "｜" + p.Value.text + "｜" + p.Intent.text + "｜" + p.Status.text;
        }

        public float PlateFillRatio(int id) { return plates[id].Fill.anchorMax.x; }
        public float PlateGhostRatio(int id) { return plates[id].Ghost.anchorMax.x; }
        public Button PlateButton(int id) { return plates[id].Button; }
        public RectTransform PlateRect(int id) { return plates[id].Rect; }

        /// <summary>HUD 中所有主要面板（測試檢查不重疊、不遮住戰場中央）。</summary>
        public IEnumerable<RectTransform> HudPanels()
        {
            yield return (RectTransform)roundText.transform.parent;
            yield return (RectTransform)schoolButtons[0].transform.parent;
            yield return (RectTransform)logToggle.transform.parent;
            yield return (RectTransform)toastText.transform.parent;
            yield return logPanel;
            yield return handRow;
            yield return (RectTransform)planTitle.transform.parent;
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
            var panel = Corner("Status", hudRoot, new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(410f, 176f), HudPanelColor);
            var v = CombatLabUi.Vertical(panel, 6f, 12);
            v.childForceExpandHeight = false;
            roundText = CombatLabUi.Label("Round", panel, "", CombatLabStyle.Body, TextAnchor.MiddleLeft, CombatLabUi.MutedText);
            CombatLabUi.Size(roundText, 26);

            var anchorRow = CombatLabUi.Rect("AnchorRow", panel);
            CombatLabUi.Size(anchorRow, 36);
            RectTransform ghost;
            var bar = Bar("AnchorBar", anchorRow, new Color(0.82f, 0.22f, 0.2f, 1f), out anchorFill, out ghost, out anchorGhostImage);
            anchorGhost = ghost;
            CombatLabUi.Stretch(bar);
            anchorText = CombatLabUi.Label("AnchorText", bar, "", CombatLabStyle.Button, TextAnchor.MiddleCenter);
            anchorText.fontStyle = FontStyle.Bold;
            anchorText.raycastTarget = false;
            anchorText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            CombatLabUi.Stretch(anchorText.rectTransform);

            shieldText = CombatLabUi.Label("Shield", panel, "", CombatLabStyle.Section, TextAnchor.MiddleLeft, new Color(0.62f, 0.8f, 1f));
            shieldText.fontStyle = FontStyle.Bold;
            CombatLabUi.Size(shieldText, 28);

            var apRow = CombatLabUi.Rect("ApRow", panel);
            var h = CombatLabUi.Horizontal(apRow, 8f);
            h.childForceExpandWidth = false;
            h.childAlignment = TextAnchor.MiddleLeft;
            CombatLabUi.Size(apRow, 30);
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
            var schools = Corner("Schools", hudRoot, new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(500f, 64f), HudPanelColor);
            CombatLabUi.Horizontal(schools, 6f, 6);
            foreach (var s in Schools)
            {
                var school = s;
                schoolButtons.Add(CombatLabUi.Button("School_" + s, schools, CombatLabText.School(s), () => Session.SelectSchool(school),
                    CombatLabStyle.Button));
            }

            var tools = Corner("Tools", hudRoot, new Vector2(1f, 1f), new Vector2(-16f, -88f), new Vector2(330f, 48f), new Color(0, 0, 0, 0));
            var row = CombatLabUi.Horizontal(tools, 8f);
            row.childForceExpandWidth = true;
            logToggle = CombatLabUi.Button("LogToggle", tools, "戰鬥紀錄 ▸", ToggleLog, CombatLabStyle.Body);
            resetButton = CombatLabUi.Button("Reset", tools, "重置戰鬥", OnReset, CombatLabStyle.Body);

            logPanel = Corner("LogPanel", hudRoot, new Vector2(1f, 1f), new Vector2(-16f, -146f), new Vector2(420f, 340f), HudPanelColor);
            CombatLabUi.Vertical(logPanel, 6f, 8);
            logText = CombatLabUi.ScrollText("Log", logPanel, CombatLabStyle.Small, out logScroll);
            CombatLabUi.Size(logScroll, -1, 1);
            logPanel.gameObject.SetActive(false);
        }

        private void BuildToast()
        {
            var t = Corner("Toast", hudRoot, new Vector2(0.5f, 1f), new Vector2(-40f, -16f), new Vector2(540f, 64f), HudPanelColor);
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
                var b = CombatLabUi.Button("Hand_" + i, handRow, "", () => OnHandClicked(slot), 26);
                var t = b.GetComponentInChildren<Text>();
                t.lineSpacing = 1.0f;
                CombatLabUi.Stretch(t.rectTransform, 4f);
                b.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
                AddHover(b, () => ShowCardTooltip(slot), () => HideCardTooltip(slot));
                handButtons.Add(b);
            }
        }

        private void BuildPlan()
        {
            var panel = Corner("Plan", hudRoot, new Vector2(1f, 0f), new Vector2(-16f, 14f), new Vector2(PlanWidth, PlanHeight), HudPanelColor);
            var v = CombatLabUi.Vertical(panel, 6f, 10);
            v.childForceExpandHeight = false;
            planTitle = CombatLabUi.Label("Title", panel, "", CombatLabStyle.Section, TextAnchor.MiddleLeft, CombatLabUi.MutedText);
            planTitle.fontStyle = FontStyle.Bold;
            CombatLabUi.Size(planTitle, 28);
            for (int i = 0; i < RuleSet.ActionPoints; i++)
            {
                int index = i;
                var b = CombatLabUi.Button("Slot_" + i, panel, "", () => Session.RemoveAt(index), CombatLabStyle.Small);
                var t = b.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                CombatLabUi.Stretch(t.rectTransform, 10f);
                CombatLabUi.Size(b, 36);
                sequenceButtons.Add(b);
            }
            forecastText = CombatLabUi.Label("Forecast", panel, "", CombatLabStyle.Small, TextAnchor.UpperLeft);
            forecastText.lineSpacing = 1.05f;
            CombatLabUi.Size(forecastText, -1, 1);
            var actions = CombatLabUi.Rect("Actions", panel);
            var row = CombatLabUi.Horizontal(actions, 8f);
            row.childForceExpandWidth = false;
            CombatLabUi.Size(actions, 56);
            redrawButton = CombatLabUi.Button("Redraw", actions, "", ToggleRedrawMode, CombatLabStyle.Body);
            commitButton = CombatLabUi.Button("Commit", actions, "", OnCommitClicked, CombatLabStyle.Button);
            CombatLabUi.Size(redrawButton, -1, -1, 120, 0);
            CombatLabUi.Size(commitButton, -1, -1, -1, 1);
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

        private void BuildPlate(int id)
        {
            var rt = CombatLabUi.Panel("Plate_" + id, plateLayer, new Color(0.04f, 0.04f, 0.06f, 0.8f));
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(240f, 150f);
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
            CombatLabUi.Size(p.Name, 26);
            Image ghostImage;
            var bar = Bar("Bar", rt, new Color(0.62f, 0.3f, 0.95f, 1f), out p.Fill, out p.Ghost, out ghostImage);
            p.GhostImage = ghostImage;
            CombatLabUi.Size(bar, 14);
            p.Value = CombatLabUi.Label("Value", rt, "", CombatLabStyle.Small, TextAnchor.MiddleLeft);
            CombatLabUi.Size(p.Value, 22);
            p.Intent = CombatLabUi.Label("Intent", rt, "", CombatLabStyle.Small, TextAnchor.MiddleLeft, new Color(1f, 0.75f, 0.68f));
            p.Intent.fontStyle = FontStyle.Bold;
            CombatLabUi.Size(p.Intent, 42);
            p.Status = CombatLabUi.Label("Status", rt, "", CombatLabStyle.MinFont, TextAnchor.MiddleLeft, CombatLabUi.MutedText);
            CombatLabUi.Size(p.Status, 20);
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
            tooltipText.text = CombatLabCardInfo.Tooltip(Session, handIds[slot], redrawMode, "點擊場景中的敵人可更換");
            tooltip.gameObject.SetActive(true);
            tooltip.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltip);
            var corners = new Vector3[4];
            ((RectTransform)handButtons[slot].transform).GetWorldCorners(corners);
            Vector2 top = hudRoot.InverseTransformPoint((corners[1] + corners[2]) * 0.5f);
            var area = hudRoot.rect;
            float half = tooltip.rect.width * 0.5f;
            float x = Mathf.Clamp(top.x, area.xMin + half + 4f, area.xMax - half - 4f);
            float y = top.y + 8f;
            if (y + tooltip.rect.height > area.yMax - 4f) y = area.yMax - 4f - tooltip.rect.height;
            tooltip.anchoredPosition = new Vector2(x, y);
        }

        public void HideCardTooltip(int slot)
        {
            if (slot >= 0 && slot != hoveredSlot) return;
            hoveredSlot = -1;
            if (tooltip != null) tooltip.gameObject.SetActive(false);
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
                anchorText.text = "錨定值 " + displayAnchor + " / " + max
                    + (f.AnchorAfter != displayAnchor ? "　→ " + f.AnchorAfter : "");
                shieldText.text = "◆ 擬態錨定 " + displayShield + (f.ShieldAfter != displayShield ? " → " + f.ShieldAfter : "")
                    + "<size=" + CombatLabStyle.MinFont + "><color=#9FB4D8>　（回合後預估）</color></size>";
            }
            else
            {
                SetBar(anchorFill, anchorGhost, anchorGhostImage, displayAnchor, displayAnchor, max, AnchorLossColor);
                anchorText.text = "錨定值 " + displayAnchor + " / " + max;
                shieldText.text = "◆ 擬態錨定 " + displayShield;
            }
            int left = playing ? 0 : Session.ApRemaining;
            apText.text = "AP " + left + " / " + Session.ApMax;
            for (int i = 0; i < apPips.Count; i++)
                apPips[i].color = i < left ? CombatLabUi.Highlight : new Color(0.25f, 0.25f, 0.28f, 1f);
        }

        private static Color SchoolColor(SchoolKind s)
        {
            switch (s)
            {
                case SchoolKind.Structure: return new Color(0.27f, 0.34f, 0.48f, 1f);
                case SchoolKind.Process: return new Color(0.16f, 0.4f, 0.37f, 1f);
                default: return new Color(0.38f, 0.25f, 0.52f, 1f);
            }
        }

        private void RefreshSchools(BattleSnapshot snap, bool over, bool playing)
        {
            for (int i = 0; i < schoolButtons.Count; i++)
            {
                var s = Schools[i];
                bool selected = s == Session.SelectedSchool;
                bool allowed = !over && !playing && (!snap.LockedSchool.HasValue || snap.LockedSchool.Value == s);
                CombatLabUi.SetButton(schoolButtons[i], (selected ? "● " : "") + CombatLabText.School(s),
                    selected ? CombatLabUi.Highlight : SchoolColor(s), allowed, selected ? CombatLabUi.DarkText : (Color?)null);
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
                p.Rect.GetComponent<Image>().color = target ? new Color(0.22f, 0.17f, 0.05f, 0.88f) : new Color(0.04f, 0.04f, 0.06f, 0.8f);
                if (d.Resolved)
                {
                    SetBar(p.Fill, p.Ghost, p.GhostImage, 0, 0, max, EnemyLossColor);
                    p.Value.text = "已解除";
                    p.Intent.text = "";
                    p.Status.text = "";
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
                if (ef != null) p.Intent.text = "意圖：" + CombatLabText.EnemyImpact(ef, f.Outcome);
                else
                {
                    var snapEnemy = FindSnapEnemy(e.Id);
                    p.Intent.text = playing ? "" : "意圖：攻擊 " + (snapEnemy != null ? snapEnemy.ForecastAttack : 0);
                }
                var sb = new StringBuilder();
                if (d.Links > 0) sb.Append("連結 ").Append(d.Links).Append("　");
                if (d.Corrections > 0) sb.Append("校正 ").Append(d.Corrections).Append("　");
                if (d.Bind > 0) sb.Append("束縛 ").Append(d.Bind);
                p.Status.text = sb.ToString();
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
                case ElementKind.Ember: return new Color(0.55f, 0.24f, 0.14f, 0.95f);
                case ElementKind.Base: return new Color(0.2f, 0.32f, 0.52f, 0.95f);
                default: return new Color(0.16f, 0.42f, 0.32f, 0.95f);
            }
        }

        private void RefreshHand(BattleSnapshot snap, bool over, bool playing)
        {
            handIds.Clear();
            handGroup.alpha = playing ? 0.35f : 1f;
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
                string label = "<b>" + CombatLabText.Element(card.Element) + "</b>\n<size=" + CombatLabStyle.Body + ">"
                    + CombatLabText.EffectName(Session.SelectedSchool, card.Element) + "</size>";
                if (order >= 0) label += "\n<size=" + CombatLabStyle.Small + "><b>【第 " + (order + 1) + " 張】</b></size>";
                else if (redrawMode) label += "\n<size=" + CombatLabStyle.Small + ">點擊調度</size>";
                Color color = order >= 0 ? new Color(0.3f, 0.3f, 0.33f, 0.95f) : HandColor(card.Element);
                if (redrawMode && order < 0) color = Color.Lerp(color, CombatLabUi.Highlight, 0.3f);
                CombatLabUi.SetButton(handButtons[i], label, color, !over && !playing && !(redrawMode && order >= 0));
                var rt = (RectTransform)handButtons[i].transform;
                rt.localPosition = new Vector3(rt.localPosition.x, rt.localPosition.y, 0f);
            }
        }

        private void RefreshPlan(BattleSnapshot snap, bool over, bool playing)
        {
            var staged = Session.Staged;
            planTitle.text = "術式編排（" + staged.Count + " / " + Session.ApMax + " 張）";
            for (int i = 0; i < sequenceButtons.Count; i++)
            {
                if (i < staged.Count && !playing)
                {
                    var c = staged[i];
                    string target = Session.CardNeedsTarget(c.Element) && c.TargetEnemyId.HasValue
                        ? " → " + ShortName(Session.EnemyName(c.TargetEnemyId.Value)) : "";
                    CombatLabUi.SetButton(sequenceButtons[i], (i + 1) + ". <b>" + CombatLabText.Element(c.Element) + "</b>・"
                        + CombatLabText.EffectName(Session.SelectedSchool, c.Element) + target + "　✕", HandColor(c.Element), !over);
                }
                else
                {
                    CombatLabUi.SetButton(sequenceButtons[i], (i + 1) + ". —", new Color(0.12f, 0.13f, 0.15f, 0.9f), false);
                }
            }

            var f = Session.Forecast;
            if (playing) forecastText.text = "結算中…（空白鍵或「略過動畫」可跳過）";
            else if (over) forecastText.text = "戰鬥已結束。";
            else if (f == null)
                forecastText.text = Session.LastPreview != null && !Session.LastPreview.Validation.IsValid
                    ? "無法結算：" + Session.LastPreview.Validation.Error : "";
            else
            {
                var sb = new StringBuilder();
                sb.Append("<b>").Append(staged.Count == 0 ? "直接結束回合" : "施放後").Append("預估</b>\n");
                sb.Append("錨定值 ").Append(CombatLabText.Change(f.AnchorBefore, f.AnchorAfter))
                  .Append("　擬態 ").Append(CombatLabText.Change(f.ShieldBefore, f.ShieldAfter, false)).Append('\n');
                bool any = false;
                foreach (var e in f.Enemies)
                {
                    if (e.ResolvedBefore) continue;
                    if (any) sb.Append("　");
                    any = true;
                    sb.Append(ShortName(e.Name)).Append(" ").Append(CombatLabText.Change(e.ValueBefore, e.ValueAfter, false));
                    if (e.ResolvedAfter) sb.Append(CombatLabText.Colored("✕", CombatLabText.Good));
                }
                sb.Append('\n').Append(CombatLabText.DamageSummary(f));
                forecastText.text = sb.ToString();
            }

            string redrawLabel = redrawMode ? "取消調度" : "調度 " + snap.RedrawsRemaining;
            CombatLabUi.SetButton(redrawButton, redrawLabel, redrawMode ? CombatLabUi.Highlight : CombatLabUi.ButtonColor,
                !over && !playing && (redrawMode || snap.RedrawsRemaining > 0), redrawMode ? CombatLabUi.DarkText : (Color?)null);
            string commitLabel = playing ? "略過動畫" : over ? "戰鬥結束" : staged.Count == 0 ? "結束回合" : "確認施放（" + staged.Count + "）";
            CombatLabUi.SetButton(commitButton, commitLabel, playing ? CombatLabUi.ButtonColor : new Color(0.2f, 0.46f, 0.3f, 1f),
                playing || !over);
        }

        /// <summary>敵人名稱的短稱（「失序體・甲」→「甲」）。</summary>
        private static string ShortName(string name)
        {
            int i = name.LastIndexOf('・');
            return i >= 0 && i < name.Length - 1 ? name.Substring(i + 1) : name;
        }
    }
}
