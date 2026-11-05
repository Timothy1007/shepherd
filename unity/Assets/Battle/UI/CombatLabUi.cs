using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Shepherd.Battle.UI
{
    /// <summary>
    /// Combat Lab 的版面尺寸與字級（以參考解析度 1600×900 為單位）。
    /// 16:9 畫面下實際像素 ＝ 字級 × 畫面高度 ÷ 900，例如 1920×1080 時 ×1.2、1280×720 時 ×0.8。
    /// </summary>
    public static class CombatLabStyle
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1600f, 900f);
        public const float MatchWidthOrHeight = 0.5f;

        /// <summary>任何文字的最小字級。</summary>
        public const int MinFont = 16;
        public const int Small = 17;
        public const int Body = 19;
        public const int Section = 20;
        public const int Button = 22;
        public const int Important = 24;
        public const int Value = 28;
    }

    /// <summary>以程式建立 uGUI 元件的小工具（不使用任何外部圖片或素材）。</summary>
    internal static class CombatLabUi
    {
        public static readonly Color Background = new Color(0.09f, 0.10f, 0.12f, 1f);
        public static readonly Color PanelColor = new Color(0.15f, 0.16f, 0.19f, 1f);
        public static readonly Color ButtonColor = new Color(0.25f, 0.27f, 0.32f, 1f);
        public static readonly Color Highlight = new Color(0.90f, 0.74f, 0.32f, 1f);
        public static readonly Color TextColor = new Color(0.96f, 0.96f, 0.96f, 1f);
        public static readonly Color MutedText = new Color(0.78f, 0.80f, 0.84f, 1f);
        public static readonly Color DarkText = new Color(0.10f, 0.09f, 0.07f, 1f);

        private static Font font;

        public static Font Font
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
                if (font == null)
                {
                    try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                    catch (System.Exception) { font = null; }
                }
                return font;
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Anchor(RectTransform rt, float xMin, float yMin, float xMax, float yMax, float pad = 6f)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
        }

        public static void Stretch(RectTransform rt, float pad = 0f)
        {
            Anchor(rt, 0, 0, 1, 1, pad);
        }

        public static RectTransform Panel(string name, Transform parent, Color color)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return rt;
        }

        public static VerticalLayoutGroup Vertical(RectTransform rt, float spacing = 6f, int padding = 8)
        {
            var g = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing;
            g.padding = new RectOffset(padding, padding, padding, padding);
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = true;
            g.childForceExpandHeight = false;
            return g;
        }

        public static HorizontalLayoutGroup Horizontal(RectTransform rt, float spacing = 6f, int padding = 0)
        {
            var g = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing;
            g.padding = new RectOffset(padding, padding, padding, padding);
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = true;
            g.childForceExpandHeight = true;
            return g;
        }

        public static LayoutElement Size(Component c, float preferredHeight = -1, float flexibleHeight = -1,
            float preferredWidth = -1, float flexibleWidth = -1)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.flexibleHeight = flexibleHeight;
            le.preferredWidth = preferredWidth;
            le.flexibleWidth = flexibleWidth;
            // 指定固定尺寸而未指定彈性時明確設為 0；否則 LayoutGroup 的 childForceExpand 會讓它把多餘空間分走。
            if (preferredHeight > 0)
            {
                le.minHeight = preferredHeight;
                if (flexibleHeight < 0) le.flexibleHeight = 0;
            }
            if (preferredWidth > 0)
            {
                le.minWidth = preferredWidth;
                if (flexibleWidth < 0) le.flexibleWidth = 0;
            }
            return le;
        }

        public static Text Label(string name, Transform parent, string text, int size = CombatLabStyle.Body,
            TextAnchor align = TextAnchor.MiddleLeft, Color? color = null)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? TextColor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.text = text;
            return t;
        }

        public static Button Button(string name, Transform parent, string label, UnityAction onClick, int fontSize = CombatLabStyle.Button)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = ButtonColor;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            b.colors = colors;
            if (onClick != null) b.onClick.AddListener(onClick);
            var t = Label("Label", rt, label, fontSize, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform, 6f);
            return b;
        }

        public static void SetButton(Button b, string label, Color color, bool interactable, Color? textColor = null)
        {
            var t = b.GetComponentInChildren<Text>();
            t.text = label;
            t.color = textColor ?? (interactable ? TextColor : MutedText);
            ((Image)b.targetGraphic).color = color;
            b.interactable = interactable;
        }

        /// <summary>可捲動的文字區：回傳內容 Text 與 ScrollRect。</summary>
        public static Text ScrollText(string name, Transform parent, int fontSize, out ScrollRect scroll)
        {
            var root = Panel(name, parent, new Color(0.08f, 0.09f, 0.10f, 1f));
            scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            var viewport = Rect("Viewport", root);
            Anchor(viewport, 0, 0, 1, 1, 0);
            viewport.offsetMax = new Vector2(-18f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var vg = Vertical(content, 0f, 10);
            vg.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var text = Label("Text", content, "", fontSize, TextAnchor.UpperLeft);

            var bar = Panel("Scrollbar", root, new Color(0.2f, 0.21f, 0.24f, 1f));
            bar.anchorMin = new Vector2(1, 0);
            bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(1, 0.5f);
            bar.sizeDelta = new Vector2(16f, 0f);
            bar.anchoredPosition = Vector2.zero;
            var area = Rect("Sliding Area", bar);
            Stretch(area, 2f);
            var handle = Panel("Handle", area, new Color(0.5f, 0.52f, 0.56f, 1f));
            Stretch(handle);
            var sb = bar.gameObject.AddComponent<Scrollbar>();
            sb.handleRect = handle;
            sb.targetGraphic = handle.GetComponent<Image>();
            sb.direction = Scrollbar.Direction.BottomToTop;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.verticalScrollbar = sb;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            return text;
        }

        public static InputField Input(string name, Transform parent, string value, string placeholder)
        {
            var rt = Panel(name, parent, new Color(0.9f, 0.9f, 0.9f, 1f));
            var input = rt.gameObject.AddComponent<InputField>();
            var text = Label("Text", rt, "", CombatLabStyle.Button, TextAnchor.MiddleLeft, Color.black);
            text.supportRichText = false;
            Stretch(text.rectTransform, 8f);
            var ph = Label("Placeholder", rt, placeholder, CombatLabStyle.Body, TextAnchor.MiddleLeft, new Color(0.35f, 0.35f, 0.35f, 1f));
            ph.fontStyle = FontStyle.Italic;
            Stretch(ph.rectTransform, 8f);
            input.textComponent = text;
            input.placeholder = ph;
            input.contentType = InputField.ContentType.IntegerNumber;
            input.text = value;
            return input;
        }

        public static void Kill(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }
    }
}
