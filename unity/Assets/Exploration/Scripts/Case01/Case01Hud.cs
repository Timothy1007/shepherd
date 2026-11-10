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
        public Text NoteText { get; private set; }
        private RectTransform notePanel;
        private float noteTime = -1f;
        public const float NoteSeconds = 6f;
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
            Base.Controls.text = "WASD 移動｜滑鼠 視角｜Shift 奔跑｜E 調查／互動｜1–3 選擇｜J 日誌｜Tab 略過自語｜Esc 釋放滑鼠";
            Base.ViewMode.text = "QA 跳轉：F1 S00 玄關｜F2 S30 樓梯錯接｜F3 S40 夾層｜F4 S50 戰後修復";
            var help = (RectTransform)Base.Controls.transform.parent;
            help.sizeDelta = new Vector2(800f, 84f);
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

            // v0.4 主角自語：低位字幕（與通訊字幕同位置但不同樣式；不遮準心、E 提示與面板）。
            // E 提示列之上（不與「［E］…」重疊）；短觀察出現時暫停。
            monologuePanel = Box(root, "Monologue", new Vector2(0.5f, 0f), new Vector2(0f, 196f), new Vector2(820f, 52f));
            monologuePanel.pivot = new Vector2(0.5f, 0f);
            monologuePanel.GetComponent<Image>().color = new Color(0.03f, 0.035f, 0.045f, 0.72f);
            MonologueText = Label(monologuePanel, "", CombatLabStyle.Body, TextAnchor.MiddleCenter, new Color(0.93f, 0.92f, 0.88f));
            Fill(MonologueText.rectTransform, 16f, 4f);
            MonologueText.fontStyle = FontStyle.Italic;
            monologueGroup = monologuePanel.gameObject.AddComponent<CanvasGroup>();
            monologuePanel.gameObject.SetActive(false);

            // 短觀察：準星下方的小字（生活細節不開側邊面板）。
            notePanel = Box(root, "Note", new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(760f, 64f));
            NoteText = Label(notePanel, "", CombatLabStyle.Body, TextAnchor.MiddleCenter, new Color(0.95f, 0.93f, 0.88f));
            Fill(NoteText.rectTransform, 14f, 6f);
            notePanel.gameObject.SetActive(false);

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

        // ---------------- v0.4 主角自語（隊列） ----------------

        public Text MonologueText { get; private set; }
        private RectTransform monologuePanel;
        private CanvasGroup monologueGroup;
        /// <summary>一句字幕：說話者（主角自語或記憶殘響）、內容、群組（同一段殘響的句子；自語為 null）。</summary>
        private struct Line
        {
            public string Speaker;
            public string Text;
            public string Group;
        }

        private readonly List<Line> monologueQueue = new List<Line>();
        private float monologueTime = -1f;
        private float monologueDuration;
        private float monologueGap;
        public const int MonologueQueueMax = 5;
        public const float MonologueGapSeconds = 0.5f;
        /// <summary>目前這句所屬的殘響群組（自語或沒有字幕時為 null）。</summary>
        public string CurrentGroup { get; private set; }

        public bool HasPendingGroup(string group)
        {
            foreach (var l in monologueQueue) if (l.Group == group) return true;
            return false;
        }

        /// <summary>本次 QA 送進隊列的所有句子（測試用；依序）。</summary>
        public readonly List<string> MonologueLog = new List<string>();
        /// <summary>暫停（面板、日誌、選項、檢視、轉場時由場景設定）：計時停止、字幕隱藏，之後接著播。</summary>
        public bool MonologueHold { get; set; }
        public bool MonologueVisible { get { return monologuePanel.gameObject.activeSelf; } }
        public int MonologuePending { get { return monologueQueue.Count; } }
        public string MonologueCurrent { get; private set; }

        /// <summary>每句 2～5 秒（依字數）。</summary>
        public static float MonologueSeconds(string line)
        {
            return Mathf.Clamp(1.6f + (line != null ? line.Length : 0) * 0.09f, 2.5f, 5f);
        }

        public void EnqueueMonologue(string line)
        {
            EnqueueLine(Case01Texts.MonologueSpeaker, line, null);
        }

        /// <summary>加入一句字幕（記憶殘響用 group 綁定同一段演出）。</summary>
        public void EnqueueLine(string speaker, string line, string group)
        {
            if (string.IsNullOrEmpty(line)) return;
            MonologueLog.Add(line);
            monologueQueue.Add(new Line { Speaker = speaker, Text = line, Group = group });
            // 同時觸發太多句時只留最新的幾句，避免一直佔住畫面。
            while (monologueQueue.Count > MonologueQueueMax) monologueQueue.RemoveAt(0);
        }

        /// <summary>略過目前這句（Tab）。目前是殘響時，整段殘響一起略過。</summary>
        public void SkipMonologue()
        {
            if (monologueTime < 0f) return;
            if (CurrentGroup != null) DropGroup(CurrentGroup);
            else EndMonologueLine();
        }

        /// <summary>移除某段殘響還沒播的句子；正在播的也結束（玩家走開、略過）。</summary>
        public void DropGroup(string group)
        {
            monologueQueue.RemoveAll(l => l.Group == group);
            if (CurrentGroup == group && monologueTime >= 0f) EndMonologueLine();
        }

        /// <summary>清除全部（戰鬥、跳轉、結束）。</summary>
        public void ClearMonologue()
        {
            monologueQueue.Clear();
            monologueTime = -1f;
            monologueGap = 0f;
            MonologueCurrent = null;
            CurrentGroup = null;
            monologuePanel.gameObject.SetActive(false);
        }

        private void EndMonologueLine()
        {
            monologueTime = -1f;
            monologueGap = MonologueGapSeconds;
            MonologueCurrent = null;
            CurrentGroup = null;
            monologuePanel.gameObject.SetActive(false);
        }

        private void TickMonologue(float dt)
        {
            bool hold = MonologueHold || !Base.Visible || NoteVisible;
            if (hold)
            {
                if (monologuePanel.gameObject.activeSelf) monologuePanel.gameObject.SetActive(false);
                return;
            }
            if (monologueTime >= 0f)
            {
                if (!monologuePanel.gameObject.activeSelf) monologuePanel.gameObject.SetActive(true);
                monologueTime += dt;
                // 淡入 0.2 秒、淡出 0.3 秒。
                monologueGroup.alpha = Mathf.Min(Mathf.Clamp01(monologueTime / 0.2f), Mathf.Clamp01((monologueDuration - monologueTime) / 0.3f));
                if (monologueTime >= monologueDuration) EndMonologueLine();
                return;
            }
            if (monologueGap > 0f)
            {
                monologueGap -= dt;
                return;
            }
            if (monologueQueue.Count == 0) return;
            var line = monologueQueue[0];
            monologueQueue.RemoveAt(0);
            MonologueCurrent = line.Text;
            CurrentGroup = line.Group;
            bool echo = line.Speaker != Case01Texts.MonologueSpeaker;
            // 自語：斜體、灰色標籤；殘響：正體、淡藍標籤（不冒充 NPC 通訊）。
            MonologueText.fontStyle = echo ? FontStyle.Normal : FontStyle.Italic;
            MonologueText.color = echo ? new Color(0.82f, 0.87f, 0.97f) : new Color(0.93f, 0.92f, 0.88f);
            MonologueText.text = "<color=" + (echo ? "#8FA3C8" : "#9AA6B8") + ">〔" + line.Speaker + "〕</color>　" + (echo ? "「" + line.Text + "」" : line.Text);
            monologueDuration = MonologueSeconds(MonologueCurrent);
            monologueTime = 0f;
            monologueGroup.alpha = 0f;
            monologuePanel.gameObject.SetActive(true);
        }

        public void Tick(float dt)
        {
            Base.Tick(dt);
            TickMonologue(dt);
            if (noteTime >= 0f)
            {
                noteTime += dt;
                if (noteTime > NoteSeconds) HideNote();
            }
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

        public void ShowNote(string text)
        {
            NoteText.text = text;
            notePanel.gameObject.SetActive(true);
            noteTime = 0f;
        }

        public void HideNote()
        {
            notePanel.gameObject.SetActive(false);
            noteTime = -1f;
        }

        public bool NoteVisible { get { return notePanel.gameObject.activeSelf; } }

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
            FitPanel();
        }

        /// <summary>調查面板依文字長度調整高度（短文字不留大片空白，長文字不溢出）。</summary>
        private void FitPanel()
        {
            var t = Base.InspectText;
            var settings = t.GetGenerationSettings(new Vector2(600f - 32f, 0f));
            float h = t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, settings) / t.pixelsPerUnit;
            Base.InspectPanel.sizeDelta = new Vector2(600f, Mathf.Clamp(h * 1.08f + 64f, 150f, 600f));
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
            FitPanel();
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
                    HideNote();
                    Base.SetHint(null);
                }
            }
        }

        /// <summary>顯示日誌：由新到舊排列，放不下的舊條目省略（日誌本身保留全部）。</summary>
        public void RefreshJournal(IList<JournalEntry> entries, IList<string> evidence = null)
        {
            var sb = new StringBuilder();
            sb.Append("<b><color=#E6BD52>調查日誌</color></b>　<color=#8E98A6>（J 關閉｜共 ").Append(entries.Count).Append(" 筆，新的在上）</color>\n");
            int budget = 900;
            if (evidence != null && evidence.Count > 0)
            {
                sb.Append("\n<b><color=#9BE59B>證物收藏（本次 QA）</color></b>");
                foreach (var id in evidence)
                    sb.Append("\n").Append(id == Case01Texts.EvidenceId ? Case01Texts.FamilyPhotoTitle : id).Append("　<color=#8E98A6>［I］檢視</color>");
                sb.Append("\n");
                budget -= 120;
            }
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

        /// <summary>
        /// Phase 1.5 P0：讓世界標籤的「實際文字範圍」落在牌面裡（maxW × maxH 公尺，標籤父物件縮放為 1）。
        /// 依 Text 的偏好尺寸設定畫布大小，再把縮放調到裝得下（不會比原本 0.01 更大）。回傳實際的世界尺寸。
        /// </summary>
        public static Vector2 FitLabel(Text t, float maxW, float maxH)
        {
            var rt = (RectTransform)t.canvas.transform;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            float pw = Mathf.Max(1f, t.preferredWidth) + 4f, ph = Mathf.Max(1f, t.preferredHeight) + 2f;
            rt.sizeDelta = new Vector2(pw, ph);
            float s = Mathf.Min(0.01f, Mathf.Min(maxW / pw, maxH / ph));
            rt.localScale = Vector3.one * s;
            return new Vector2(pw * s, ph * s);
        }
    }
}
