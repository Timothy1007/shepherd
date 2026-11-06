using System.Collections.Generic;
using System.Text;
using Shepherd.Battle.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// 室內 QA 的 HUD：沿用探索 HUD（準心、目標、E 提示、訊息、調查面板、遭遇橫幅），
    /// 再加上通訊字幕、可重讀日誌、選項、QA 跳轉面板、轉場黑幕與結束畫面。
    /// </summary>
    public sealed class Case01Hud
    {
        private static readonly Color Panel = new Color(0.05f, 0.06f, 0.08f, 0.86f);
        private static readonly Color Gold = new Color(0.90f, 0.74f, 0.32f, 1f);
        public const float SubtitleSeconds = 7f;

        public ExploreHud Base { get; private set; }
        public Text Subtitle { get; private set; }
        public Text JournalText { get; private set; }
        public Text QaText { get; private set; }
        public Text EndText { get; private set; }
        public Image Fade { get; private set; }
        private RectTransform subtitlePanel;
        private RectTransform journalPanel;
        private RectTransform endPanel;
        private float subtitleTime = -1f;

        public Case01Choice ActiveChoice { get; private set; }

        private static Font font;

        public static Font UiFont
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

        public Case01Hud(Transform parent)
        {
            Base = new ExploreHud(parent);
            Base.Canvas.gameObject.name = "Case01Hud";
            Base.Controls.text = "WASD 移動｜滑鼠 視角｜Shift 奔跑｜E 調查／互動｜1–3 選擇｜J 日誌｜Esc 釋放滑鼠";
            Base.ViewMode.text = "QA 跳轉：F1 S00 玄關｜F2 S30 樓梯錯接｜F3 S40 夾層｜F4 S50 戰後修復";
            var help = (RectTransform)Base.Controls.transform.parent;
            help.sizeDelta = new Vector2(700f, 84f);
            // 較長的調查文字（手記、選項）需要更大的面板。
            Base.InspectPanel.sizeDelta = new Vector2(600f, 280f);
            var obj = (RectTransform)Base.Objective.transform.parent;
            obj.sizeDelta = new Vector2(560f, 92f);
            // 訊息放在目標框下方（避免與左上目標、右上 QA 面板重疊）。
            ((RectTransform)Base.Toast.transform.parent).anchoredPosition = new Vector2(0f, -122f);

            var root = Base.Root;

            // 通訊字幕：E 提示上方。
            subtitlePanel = Box(root, "Subtitle", new Vector2(0.5f, 0f), new Vector2(0f, 216f), new Vector2(900f, 60f));
            subtitlePanel.pivot = new Vector2(0.5f, 0f);
            Subtitle = Label(subtitlePanel, "", CombatLabStyle.Section, TextAnchor.MiddleCenter, new Color(0.75f, 0.9f, 1f));
            Fill(Subtitle.rectTransform, 14f, 6f);
            subtitlePanel.gameObject.SetActive(false);

            // QA 狀態：右上。
            var qa = Box(root, "QaPanel", new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(430f, 74f));
            QaText = Label(qa, "", CombatLabStyle.Small, TextAnchor.MiddleLeft, new Color(0.8f, 0.85f, 0.9f));
            Fill(QaText.rectTransform, 12f, 6f);

            // 日誌：左側。
            journalPanel = Box(root, "Journal", new Vector2(0f, 0.5f), new Vector2(16f, -34f), new Vector2(640f, 570f));
            JournalText = Label(journalPanel, "", CombatLabStyle.Body, TextAnchor.UpperLeft, Color.white);
            Fill(JournalText.rectTransform, 18f, 16f);
            JournalText.verticalOverflow = VerticalWrapMode.Truncate;
            journalPanel.gameObject.SetActive(false);

            // 結束畫面。
            endPanel = Box(root, "EndPanel", new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(720f, 300f));
            EndText = Label(endPanel, "", CombatLabStyle.Important, TextAnchor.MiddleCenter, Color.white);
            Fill(EndText.rectTransform, 20f, 16f);
            endPanel.gameObject.SetActive(false);

            // 轉場黑幕（最上層）。
            var fade = Rect("Fade", root);
            fade.anchorMin = Vector2.zero;
            fade.anchorMax = Vector2.one;
            fade.offsetMin = fade.offsetMax = Vector2.zero;
            Fade = fade.gameObject.AddComponent<Image>();
            Fade.color = new Color(0f, 0f, 0f, 0f);
            Fade.raycastTarget = false;
        }

        public bool Visible
        {
            get { return Base.Visible; }
            set { Base.Visible = value; }
        }

        public void Tick(float dt)
        {
            Base.Tick(dt);
            if (subtitleTime < 0f) return;
            subtitleTime += dt;
            if (subtitleTime > SubtitleSeconds)
            {
                subtitlePanel.gameObject.SetActive(false);
                subtitleTime = -1f;
            }
        }

        // ---------------- 字幕 ----------------

        public void ShowRadio(string text)
        {
            Subtitle.text = "<color=#E6BD52>" + Case01Texts.Companion + "：</color>" + text;
            subtitlePanel.gameObject.SetActive(true);
            subtitleTime = 0f;
        }

        public bool SubtitleVisible { get { return subtitlePanel.gameObject.activeSelf; } }

        public void HideRadio()
        {
            subtitlePanel.gameObject.SetActive(false);
            subtitleTime = -1f;
        }

        // ---------------- 調查與選項 ----------------

        public void ShowText(string text)
        {
            ActiveChoice = null;
            Base.ShowInspect(text + "\n\n<color=#8E98A6>［E］關閉</color>");
        }

        public void ShowChoice(Case01Choice choice)
        {
            ActiveChoice = choice;
            var sb = new StringBuilder();
            sb.Append(choice.Prompt).Append("\n\n");
            for (int i = 0; i < choice.Options.Length; i++)
                sb.Append("<color=#E6BD52>［").Append(i + 1).Append("］</color>").Append(choice.Options[i]).Append('\n');
            sb.Append("\n<color=#8E98A6>［E］／［Esc］取消</color>");
            Base.ShowInspect(sb.ToString());
        }

        public void HidePanel()
        {
            ActiveChoice = null;
            Base.HideInspect();
        }

        public bool PanelVisible { get { return Base.InspectVisible; } }

        // ---------------- 日誌 ----------------

        public bool JournalVisible
        {
            get { return journalPanel.gameObject.activeSelf; }
            set
            {
                journalPanel.gameObject.SetActive(value);
                // 日誌開著時收起其他會重疊的面板（字幕內容已在日誌裡）。
                if (value)
                {
                    HidePanel();
                    HideRadio();
                    Base.SetHint(null);
                }
            }
        }

        /// <summary>顯示日誌：由新到舊排列，放不下的舊條目省略（日誌本身保留全部）。</summary>
        public void RefreshJournal(IList<JournalEntry> entries)
        {
            var sb = new StringBuilder();
            sb.Append("<b><color=#E6BD52>調查日誌</color></b>　<color=#8E98A6>（J 關閉｜共 ").Append(entries.Count).Append(" 筆，新的在上）</color>\n");
            int budget = 900;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                int len = e.Title.Length + e.Text.Length;
                if (len > budget && i < entries.Count - 1)
                {
                    sb.Append("\n<color=#8E98A6>……較早的 ").Append(i + 1).Append(" 筆已省略</color>");
                    break;
                }
                budget -= len;
                sb.Append("\n<b>").Append(e.Title).Append("</b>\n").Append(e.Text).Append('\n');
            }
            if (entries.Count == 0) sb.Append("\n（還沒有記錄）");
            JournalText.text = sb.ToString();
        }

        // ---------------- 結束 ----------------

        public bool EndVisible
        {
            get { return endPanel.gameObject.activeSelf; }
            set { endPanel.gameObject.SetActive(value); }
        }

        public void ShowEnd(string title, string body)
        {
            EndText.text = "<b><color=#E6BD52>" + title + "</color></b>\n\n" + body + "\n\n<size=19><color=#8E98A6>［F1］從玄關重玩　［F2～F4］跳到其他檢查點</color></size>";
            endPanel.gameObject.SetActive(true);
        }

        // ---------------- 小工具 ----------------

        private static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = Rect(name, parent);
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

        private static Text Label(Transform parent, string text, int size, TextAnchor align, Color color)
        {
            var rt = Rect("Text", parent);
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

        /// <summary>世界空間的小標籤（門框記號、出口標示）。</summary>
        public static Text WorldLabel(Transform parent, Vector3 localPos, float yaw, string text, int size, Color color, float width = 160f)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Canvas));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.localPosition = localPos;
            rt.localRotation = Quaternion.Euler(0f, yaw, 0f);
            rt.sizeDelta = new Vector2(width, 60f);
            rt.localScale = Vector3.one * 0.01f;
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            var t = Label(rt, text, size, TextAnchor.MiddleCenter, color);
            Fill(t.rectTransform, 0f, 0f);
            t.fontStyle = FontStyle.Bold;
            return t;
        }
    }
}
