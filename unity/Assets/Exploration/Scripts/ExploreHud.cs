using Shepherd.Battle.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration
{
    /// <summary>
    /// 探索時的精簡 HUD（uGUI）：準心、目標、互動／遭遇提示、遭遇橫幅、調查面板、操作說明與戰鬥視角選項。
    /// 進入戰鬥時隱藏，改由 Combat Lab 的戰鬥 HUD 接手。
    /// </summary>
    public sealed class ExploreHud
    {
        private static readonly Color Panel = new Color(0.05f, 0.06f, 0.08f, 0.82f);
        private static readonly Color Gold = new Color(0.90f, 0.74f, 0.32f, 1f);
        private static readonly Color Rift = new Color(0.78f, 0.55f, 1f, 1f);

        public Canvas Canvas { get; private set; }
        public RectTransform Root { get; private set; }
        public Text Objective { get; private set; }
        public Text Hint { get; private set; }
        public Text Toast { get; private set; }
        public Text ViewMode { get; private set; }
        public Text Controls { get; private set; }
        public Text CursorHint { get; private set; }
        public RectTransform EncounterBanner { get; private set; }
        public Text EncounterTitle { get; private set; }
        public Text EncounterSubtitle { get; private set; }
        public RectTransform InspectPanel { get; private set; }
        public Text InspectText { get; private set; }
        public Image Crosshair { get; private set; }
        private RectTransform hintPanel;
        private RectTransform toastPanel;
        private float toastTime = -1f;

        private static Font font;

        private static Font UiFont
        {
            get
            {
                if (font != null) return font;
                try
                {
                    font = Font.CreateDynamicFontFromOSFont(new[]
                    {
                        "Microsoft JhengHei UI", "Microsoft JhengHei", "Microsoft YaHei UI", "Microsoft YaHei",
                        "PingFang TC", "Noto Sans CJK TC", "Noto Sans TC", "Arial"
                    }, 18);
                }
                catch (System.Exception)
                {
                    font = null;
                }
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public ExploreHud(Transform parent)
        {
            var go = new GameObject("ExploreHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            Canvas = go.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 5;
            Canvas.pixelPerfect = true;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = CombatLabStyle.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = CombatLabStyle.MatchWidthOrHeight;
            Root = (RectTransform)go.transform;

            // 準心。
            var cross = Rect("Crosshair", Root);
            cross.anchorMin = cross.anchorMax = new Vector2(0.5f, 0.5f);
            cross.sizeDelta = new Vector2(6f, 6f);
            Crosshair = cross.gameObject.AddComponent<Image>();
            Crosshair.color = new Color(1f, 1f, 1f, 0.75f);
            Crosshair.raycastTarget = false;

            // 左上：場景名稱與目標。
            var obj = Box("Objective", new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(470f, 78f));
            Objective = Label("Text", obj, "", CombatLabStyle.Body, TextAnchor.MiddleLeft, Color.white);
            Fill(Objective.rectTransform, 12f, 6f);

            // 下方中央：互動／遭遇提示。
            hintPanel = Box("Hint", new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(640f, 56f));
            hintPanel.pivot = new Vector2(0.5f, 0f);
            Hint = Label("Text", hintPanel, "", CombatLabStyle.Section, TextAnchor.MiddleCenter, Color.white);
            Fill(Hint.rectTransform, 10f, 4f);

            // 上方中央：訊息。
            toastPanel = Box("Toast", new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(560f, 56f));
            toastPanel.pivot = new Vector2(0.5f, 1f);
            Toast = Label("Text", toastPanel, "", CombatLabStyle.Body, TextAnchor.MiddleCenter, Gold);
            Fill(Toast.rectTransform, 10f, 4f);
            toastPanel.gameObject.SetActive(false);

            // 左下：操作說明與 Demo 視角選項。
            var help = Box("Controls", new Vector2(0f, 0f), new Vector2(16f, 16f), new Vector2(560f, 96f));
            var v = help.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(12, 12, 8, 8);
            v.spacing = 4f;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = true;
            Controls = Label("Keys", help, "WASD 移動｜滑鼠 視角｜Shift 奔跑｜E 調查｜Esc 釋放滑鼠",
                CombatLabStyle.Small, TextAnchor.MiddleLeft, new Color(0.85f, 0.87f, 0.9f));
            ViewMode = Label("ViewMode", help, "", CombatLabStyle.Small, TextAnchor.MiddleLeft, new Color(0.7f, 0.85f, 1f));

            // 滑鼠未鎖定提示。
            var ch = Box("CursorHint", new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(420f, 48f));
            CursorHint = Label("Text", ch, "點擊畫面以鎖定滑鼠、繼續探索", CombatLabStyle.Body, TextAnchor.MiddleCenter, Color.white);
            Fill(CursorHint.rectTransform, 8f, 4f);
            ch.gameObject.SetActive(false);

            // 遭遇橫幅。
            EncounterBanner = Rect("EncounterBanner", Root);
            EncounterBanner.anchorMin = new Vector2(0f, 0.5f);
            EncounterBanner.anchorMax = new Vector2(1f, 0.5f);
            EncounterBanner.sizeDelta = new Vector2(0f, 150f);
            EncounterBanner.anchoredPosition = new Vector2(0f, 120f);
            var bimg = EncounterBanner.gameObject.AddComponent<Image>();
            bimg.color = new Color(0.04f, 0.01f, 0.07f, 0.8f);
            bimg.raycastTarget = false;
            EncounterTitle = Label("Title", EncounterBanner, "遭遇", 52, TextAnchor.MiddleCenter, Rift);
            EncounterTitle.fontStyle = FontStyle.Bold;
            Place(EncounterTitle.rectTransform, 0f, 0.42f, 1f, 1f);
            EncounterSubtitle = Label("Subtitle", EncounterBanner, "", CombatLabStyle.Section, TextAnchor.MiddleCenter, Color.white);
            Place(EncounterSubtitle.rectTransform, 0f, 0f, 1f, 0.42f);
            EncounterBanner.gameObject.SetActive(false);

            // 調查面板。
            InspectPanel = Box("InspectPanel", new Vector2(1f, 0.5f), new Vector2(-24f, 40f), new Vector2(460f, 230f));
            InspectPanel.pivot = new Vector2(1f, 0.5f);
            InspectText = Label("Text", InspectPanel, "", CombatLabStyle.Body, TextAnchor.UpperLeft, Color.white);
            Fill(InspectText.rectTransform, 16f, 14f);
            InspectPanel.gameObject.SetActive(false);
        }

        public bool Visible
        {
            get { return Canvas.enabled; }
            set { Canvas.enabled = value; }
        }

        public bool CursorHintVisible
        {
            get { return CursorHint.transform.parent.gameObject.activeSelf; }
            set { CursorHint.transform.parent.gameObject.SetActive(value); }
        }

        public bool HintVisible { get { return hintPanel.gameObject.activeSelf; } }

        public void SetHint(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            hintPanel.gameObject.SetActive(show);
            if (show) Hint.text = text;
        }

        public void ShowToast(string text)
        {
            Toast.text = text;
            toastPanel.gameObject.SetActive(true);
            toastTime = 0f;
        }

        public string ToastText { get { return toastPanel.gameObject.activeSelf ? Toast.text : ""; } }

        public void Tick(float dt)
        {
            if (toastTime < 0f) return;
            toastTime += dt;
            if (toastTime > 4.5f)
            {
                toastPanel.gameObject.SetActive(false);
                toastTime = -1f;
            }
        }

        public void ShowEncounter(string title, string subtitle)
        {
            EncounterTitle.text = title;
            EncounterSubtitle.text = subtitle;
            EncounterBanner.gameObject.SetActive(true);
        }

        public void HideEncounter() { EncounterBanner.gameObject.SetActive(false); }

        public bool EncounterVisible { get { return EncounterBanner.gameObject.activeSelf; } }

        public void ShowInspect(string text)
        {
            InspectText.text = text;
            InspectPanel.gameObject.SetActive(true);
        }

        public void HideInspect() { InspectPanel.gameObject.SetActive(false); }

        public bool InspectVisible { get { return InspectPanel.gameObject.activeSelf; } }

        // ---------------- 小工具 ----------------

        private RectTransform Box(string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = Rect(name, Root);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = Panel;
            img.raycastTarget = false;
            return rt;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static Text Label(string name, Transform parent, string text, int size, TextAnchor align, Color color)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UiFont;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        private static void Fill(RectTransform rt, float padX, float padY)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }

        private static void Place(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
