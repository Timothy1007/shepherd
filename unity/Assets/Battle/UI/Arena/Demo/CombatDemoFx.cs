using System.Collections.Generic;
using Shepherd.Battle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Battle.UI.Arena
{
    /// <summary>
    /// Demo HUD 的施放／命中演出（UI 層）：施放條逐張點亮、承接連線、三連聚合、名牌與玩家面板的數值變化標籤、
    /// 回合結果回顧、勝負時淡出 HUD 讓結果橫幅成為焦點。
    /// 全部依「實際播放到的」BattleEvent 驅動；回顧只把事件的數值加總，不重算任何公式。3D 戰場的既有特效照舊。
    /// 降低動態（ReduceMotion）時：不位移、不縮放、不脈動，標籤原地淡出、聚合直接到位。
    /// </summary>
    public sealed class CombatDemoFx
    {
        private sealed class Cell
        {
            public RectTransform Rect;
            public Image Body;
            public Outline Frame;
            public Text Title;
            public Text Detail;
            public ElementKind Element;
            public bool Lit;
            public float LitTime = -1f;
        }

        private sealed class Chip
        {
            public RectTransform Rect;
            public Text Text;
            public Vector2 From;
            public float Age;
        }

        private sealed class Flyer
        {
            public RectTransform Rect;
            public Vector2 From, To;
        }

        private const float ChipLife = 1.6f;
        private const float RecapLife = 9f;
        private const float OutcomeAlpha = 0.2f;

        private readonly CombatDemoHud hud;
        private readonly CombatArenaView arena;
        private readonly RectTransform root;
        private readonly CanvasGroup rootGroup;
        private RectTransform castPanel;
        private Text castTitle;
        private readonly List<Cell> cells = new List<Cell>();
        private readonly List<Text> links = new List<Text>();
        private Text aggregate;
        private readonly List<Flyer> flyers = new List<Flyer>();
        private float aggregateTime = -1f;
        private Text recapText;
        private RectTransform chipLayer;
        private readonly List<Chip> chips = new List<Chip>();
        private readonly Dictionary<int, int> chipStack = new Dictionary<int, int>();
        private CommitResult seenCommit;
        private int seenEvents;
        private ElementKind currentElement;
        private bool resonance;
        private float recapTime = -1f;
        private int recapRound = -1;
        private float playerFlash = -1f;
        private Color playerFlashColor;
        private readonly Dictionary<int, float> plateFlash = new Dictionary<int, float>();
        private readonly Dictionary<int, Color> plateFlashColor = new Dictionary<int, Color>();
        private float clock;

        /// <summary>本次確認產生過的所有標籤文字（測試用，依播放順序）。</summary>
        public readonly List<string> ChipLog = new List<string>();
        public bool CastVisible { get { return castPanel.gameObject.activeSelf; } }
        public int CastCells { get { int n = 0; foreach (var c in cells) if (c.Rect.gameObject.activeSelf) n++; return n; } }
        public bool CellLit(int i) { return cells[i].Lit; }
        public string CellText(int i) { return cells[i].Title.text + "｜" + cells[i].Detail.text; }
        public string LinkText(int i) { return links[i].text; }
        public bool AggregateVisible { get { return aggregate.gameObject.activeSelf; } }
        public string AggregateText { get { return aggregate.text; } }
        public bool RecapVisible { get { return castPanel.gameObject.activeSelf && recapText.gameObject.activeSelf; } }
        public string RecapText { get { return recapText.text; } }
        public int LiveChips { get { return chips.Count; } }
        public float HudAlpha { get { return rootGroup.alpha; } }
        public Vector2 ChipPosition(int i) { return chips[i].Rect.anchoredPosition; }
        public Vector3 CellScale(int i) { return cells[i].Rect.localScale; }

        public CombatDemoFx(CombatDemoHud hud, CombatArenaView arena, RectTransform root, RectTransform sequencePanel)
        {
            this.hud = hud;
            this.arena = arena;
            this.root = root;
            rootGroup = root.gameObject.GetComponent<CanvasGroup>();
            if (rootGroup == null) rootGroup = root.gameObject.AddComponent<CanvasGroup>();
            Build(sequencePanel);
            seenCommit = arena.LastCommit;
        }

        private CombatLabSession S { get { return arena.Session; } }
        private bool Calm { get { return hud.Options.ReduceMotion; } }

        // ---------------- 建立 ----------------

        private void Build(RectTransform sequencePanel)
        {
            var solid = CombatDemoTheme.Panel;
            solid.a = 1f;
            castPanel = CombatLabUi.Panel("CastPanel", sequencePanel, solid);
            castPanel.anchorMin = Vector2.zero;
            castPanel.anchorMax = Vector2.one;
            castPanel.offsetMin = new Vector2(10f, 54f);
            castPanel.offsetMax = new Vector2(-10f, -34f);
            var btn = castPanel.gameObject.AddComponent<Button>();
            btn.targetGraphic = castPanel.GetComponent<Image>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(DismissRecap);

            castTitle = Label("CastTitle", castPanel, CombatDemoText.CastTitle, CombatDemoTheme.FontSmall, TextAnchor.MiddleRight, CombatDemoTheme.Muted);
            Place(castTitle.rectTransform, new Vector2(1f, 1f), new Vector2(-4f, 2f), new Vector2(200f, 22f));

            float w = CombatDemoTheme.SequenceWidth - 20f;
            float gap = (w - 8f - 3 * CombatDemoTheme.SlotWidth) / 2f;
            for (int i = 0; i < 3; i++)
            {
                var c = new Cell();
                c.Rect = CombatLabUi.Panel("Cast_" + i, castPanel, CombatDemoTheme.PanelSoft);
                Place(c.Rect, new Vector2(0f, 1f), new Vector2(4f + i * (CombatDemoTheme.SlotWidth + gap), -2f), new Vector2(CombatDemoTheme.SlotWidth, CombatDemoTheme.SlotHeight));
                c.Rect.pivot = new Vector2(0.5f, 0.5f);
                c.Rect.anchoredPosition += new Vector2(CombatDemoTheme.SlotWidth * 0.5f, -CombatDemoTheme.SlotHeight * 0.5f);
                c.Body = c.Rect.GetComponent<Image>();
                c.Body.raycastTarget = false;
                c.Frame = c.Rect.gameObject.AddComponent<Outline>();
                c.Frame.effectDistance = new Vector2(2f, -2f);
                c.Title = Label("Title", c.Rect, "", CombatDemoTheme.FontBody, TextAnchor.UpperLeft, CombatDemoTheme.Ivory);
                c.Title.fontStyle = FontStyle.Bold;
                CombatLabUi.Stretch(c.Title.rectTransform, 8f);
                c.Detail = Label("Detail", c.Rect, "", CombatDemoTheme.FontSmall, TextAnchor.LowerLeft, CombatDemoTheme.Muted);
                CombatLabUi.Stretch(c.Detail.rectTransform, 8f);
                cells.Add(c);
                if (i < 2)
                {
                    var l = Label("CastLink_" + i, castPanel, "", CombatDemoTheme.FontSmall, TextAnchor.MiddleCenter, CombatDemoTheme.Brass);
                    l.fontStyle = FontStyle.Bold;
                    Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(4f + (i + 1) * CombatDemoTheme.SlotWidth + i * gap - 6f, -24f), new Vector2(gap + 12f, 60f));
                    links.Add(l);
                }
            }
            aggregate = Label("Aggregate", castPanel, "", CombatDemoTheme.FontBody, TextAnchor.MiddleCenter, CombatDemoTheme.Brass);
            aggregate.fontStyle = FontStyle.Bold;
            Place(aggregate.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -CombatDemoTheme.SlotHeight - 6f), new Vector2(w - 20f, 26f));
            aggregate.rectTransform.pivot = new Vector2(0.5f, 1f);
            aggregate.gameObject.SetActive(false);
            for (int i = 0; i < 3; i++)
            {
                var f = new Flyer();
                var t = Label("Flyer_" + i, castPanel, "", CombatDemoTheme.FontTitle, TextAnchor.MiddleCenter, CombatDemoTheme.Brass);
                t.fontStyle = FontStyle.Bold;
                f.Rect = t.rectTransform;
                Place(f.Rect, new Vector2(0f, 1f), Vector2.zero, new Vector2(40f, 40f));
                f.Rect.pivot = new Vector2(0.5f, 0.5f);
                f.Rect.gameObject.SetActive(false);
                flyers.Add(f);
            }
            recapText = Label("Recap", castPanel, "", CombatDemoTheme.FontSmall, TextAnchor.UpperLeft, CombatDemoTheme.Ivory);
            recapText.supportRichText = true;
            recapText.rectTransform.anchorMin = new Vector2(0f, 0f);
            recapText.rectTransform.anchorMax = new Vector2(1f, 0f);
            recapText.rectTransform.pivot = new Vector2(0.5f, 0f);
            recapText.rectTransform.offsetMin = new Vector2(8f, 4f);
            recapText.rectTransform.offsetMax = new Vector2(-8f, 82f);
            recapText.gameObject.SetActive(false);
            castPanel.gameObject.SetActive(false);

            chipLayer = CombatLabUi.Rect("FxChips", root);
            CombatLabUi.Stretch(chipLayer);
        }

        private static Text Label(string name, Transform parent, string text, int size, TextAnchor align, Color color)
        {
            var t = CombatLabUi.Label(name, parent, text, size, align, color);
            t.raycastTarget = false;
            return t;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        // ---------------- 每影格 ----------------

        public void Tick(float dt)
        {
            clock += dt;
            if (!ReferenceEquals(arena.LastCommit, seenCommit) && arena.LastCommit != null && arena.LastCommit.Accepted)
                BeginCast(arena.LastCommit);
            var events = arena.PlayedEvents;
            if (ReferenceEquals(arena.LastCommit, seenCommit))
                for (; seenEvents < events.Count; seenEvents++) OnEvent(events[seenEvents], seenEvents);

            // 播放結束：回顧（戰鬥仍進行時）；開始新編排、點擊或逾時就收起。
            if (castPanel.gameObject.activeSelf && !arena.IsPlaying)
            {
                if (recapTime < 0f)
                {
                    if (S.IsOver) HideCast();
                    else ShowRecap();
                }
                else
                {
                    recapTime += dt;
                    if (recapTime > RecapLife || S.Staged.Count > 0 || S.Snapshot().Round != recapRound) HideCast();
                }
            }

            AnimateCells(dt);
            AnimateChips(dt);
            AnimateFlash(dt);
            // 勝負：HUD 淡出，讓結果橫幅成為焦點；重置後恢復。
            float target = S.IsOver && !arena.IsPlaying ? OutcomeAlpha : 1f;
            rootGroup.alpha = Calm ? target : Mathf.MoveTowards(rootGroup.alpha, target, dt / 0.6f);
            rootGroup.blocksRaycasts = !(S.IsOver && !arena.IsPlaying);
        }

        private void BeginCast(CommitResult r)
        {
            seenCommit = r;
            seenEvents = 0;
            resonance = false;
            ChipLog.Clear();
            chipStack.Clear();
            recapTime = -1f;
            recapText.gameObject.SetActive(false);
            aggregate.gameObject.SetActive(false);
            aggregateTime = -1f;
            foreach (var f in flyers) f.Rect.gameObject.SetActive(false);
            foreach (var l in links) l.text = "→";
            // 施放條：張數與元素取自本次結算的實際事件（順序即結算順序）。
            int n = 0;
            var school = S.SelectedSchool;
            foreach (var ev in r.Resolution.Events)
            {
                if ((ev.Kind != EventKind.CardResolved && ev.Kind != EventKind.CardFizzled) || n >= cells.Count) continue;
                var c = cells[n++];
                c.Element = (ElementKind)ev.Amount;
                c.Lit = false;
                c.LitTime = -1f;
                c.Rect.gameObject.SetActive(true);
                c.Rect.localScale = Vector3.one;
                c.Title.text = CombatDemoTheme.ElementSymbol(c.Element) + " " + CombatLabText.EffectName(school, c.Element);
                c.Detail.text = CombatDemoText.CastWaiting;
                Dim(c);
            }
            for (int i = n; i < cells.Count; i++) cells[i].Rect.gameObject.SetActive(false);
            for (int i = 0; i < links.Count; i++) links[i].gameObject.SetActive(i + 1 < n);
            castTitle.text = CombatDemoText.CastTitle;
            castPanel.gameObject.SetActive(n > 0);
            castPanel.SetAsLastSibling();
        }

        private static void Dim(Cell c)
        {
            var col = CombatDemoTheme.ElementColor(c.Element);
            c.Body.color = Color.Lerp(CombatDemoTheme.PanelSoft, col, 0.12f);
            c.Frame.effectColor = new Color(col.r, col.g, col.b, 0.35f);
            c.Title.color = CombatDemoTheme.Muted;
        }

        private void Light(Cell c, bool fizzled, int value)
        {
            var col = CombatDemoTheme.ElementColor(c.Element);
            c.Lit = true;
            c.LitTime = 0f;
            c.Body.color = fizzled ? CombatDemoTheme.PanelSoft : Color.Lerp(CombatDemoTheme.PanelSoft, col, 0.45f);
            c.Frame.effectColor = fizzled ? CombatDemoTheme.Faint : (resonance ? CombatDemoTheme.Brass : col);
            c.Title.color = fizzled ? CombatDemoTheme.Muted : CombatDemoTheme.Ivory;
            c.Detail.text = fizzled ? CombatDemoText.CastFizzled : string.Format(CombatDemoText.CastValue, value);
            c.Detail.color = fizzled ? CombatDemoTheme.Muted : CombatDemoTheme.Ivory;
        }

        // ---------------- 事件 ----------------

        private void OnEvent(BattleEvent ev, int index)
        {
            string src = ev.Source ?? "";
            bool residue = src.StartsWith("residue:");
            string prefix = residue ? CombatDemoText.ChipResiduePrefix : "";
            switch (ev.Kind)
            {
                case EventKind.Resonance:
                    resonance = true;
                    StartAggregate((ElementKind)ev.Amount, ev.Value);
                    break;
                case EventKind.CardResolved:
                case EventKind.CardFizzled:
                {
                    int i = CardIndex(src);
                    currentElement = (ElementKind)ev.Amount;
                    if (i >= 0 && i < cells.Count) Light(cells[i], ev.Kind == EventKind.CardFizzled, ev.Value);
                    break;
                }
                case EventKind.Pressure:
                    if (ev.Value > 0) PlateChip(ev.EnemyId, string.Format(CombatDemoText.ChipDisorder, "−" + ev.Value), CombatDemoTheme.ElementColor(currentElement), true);
                    break;
                case EventKind.Conduction:
                    if (ev.Value > 0) PlateChip(ev.EnemyId, string.Format(CombatDemoText.ChipConduction, "−" + ev.Value), CombatDemoTheme.ElementColor(ElementKind.Binder), true);
                    break;
                case EventKind.LinkChanged:
                    if (ev.Amount != 0) PlateChip(ev.EnemyId, prefix + string.Format(CombatDemoText.ChipLinks, CombatLabText.Signed(ev.Amount)), CombatDemoTheme.ElementColor(ElementKind.Binder), false);
                    break;
                case EventKind.CorrectionChanged:
                    if (ev.Amount != 0) PlateChip(ev.EnemyId, string.Format(CombatDemoText.ChipCorrection, CombatLabText.Signed(ev.Amount)), CombatDemoTheme.Good, false);
                    break;
                case EventKind.BindChanged:
                    if (ev.Amount != 0) PlateChip(ev.EnemyId, string.Format(CombatDemoText.ChipBind, CombatLabText.Signed(ev.Amount)), CombatDemoTheme.Primary, false);
                    break;
                case EventKind.EnemyResolved:
                    PlateChip(ev.EnemyId, CombatDemoText.ChipResolved, CombatDemoTheme.Good, true);
                    break;
                case EventKind.EnemyActed:
                    if (ev.Amount > 0) PlateChip(ev.EnemyId, string.Format(CombatDemoText.ChipAttack, ev.Amount), CombatDemoTheme.Danger, true);
                    break;
                case EventKind.ShieldChanged:
                    if (ev.Amount != 0) PlayerChip(prefix + string.Format(CombatDemoText.ChipShield, CombatLabText.Signed(ev.Amount)), CombatDemoTheme.Shield, ev.Amount > 0);
                    break;
                case EventKind.AnchorChanged:
                    if (ev.Amount != 0) PlayerChip(string.Format(CombatDemoText.ChipAnchor, CombatLabText.Signed(ev.Amount)), ev.Amount < 0 ? CombatDemoTheme.Danger : CombatDemoTheme.Good, true);
                    break;
            }
            if (residue)
            {
                int i = CardIndex(src) - 1;
                if (i >= 0 && i < links.Count)
                {
                    string what = ev.Kind == EventKind.ShieldChanged ? string.Format(CombatDemoText.ChipShield, CombatLabText.Signed(ev.Amount))
                        : ev.Kind == EventKind.LinkChanged ? string.Format(CombatDemoText.ChipLinks, CombatLabText.Signed(ev.Amount)) : "";
                    links[i].text = string.Format(CombatDemoText.ResidueFired, what.Replace("擬態錨定", "擬態"));
                    links[i].color = CombatDemoTheme.Good;
                }
            }
        }

        private static int CardIndex(string src)
        {
            int c = src.IndexOf(':');
            int i;
            return c >= 0 && int.TryParse(src.Substring(c + 1), out i) ? i : -1;
        }

        private void StartAggregate(ElementKind element, int count)
        {
            aggregate.text = string.Format(CombatDemoText.ResonanceFired, CombatLabText.Element(element), S.Rules.TripleMultiplier.ToString("0.##"));
            aggregate.color = CombatDemoTheme.Brass;
            aggregate.gameObject.SetActive(true);
            aggregateTime = 0f;
            var to = aggregate.rectTransform.anchoredPosition;
            var panel = castPanel.rect;
            for (int i = 0; i < flyers.Count; i++)
            {
                var f = flyers[i];
                bool on = i < count && i < cells.Count && cells[i].Rect.gameObject.activeSelf;
                f.Rect.gameObject.SetActive(on && !Calm);
                if (!on) continue;
                var t = f.Rect.GetComponent<Text>();
                t.text = CombatDemoTheme.ElementSymbol(element);
                t.color = CombatDemoTheme.ElementColor(element);
                // 從各張牌的中心聚向共鳴標籤（castPanel 左上為原點）。
                var cell = cells[i].Rect.anchoredPosition;
                f.From = cell;
                f.To = new Vector2(panel.width * 0.5f, to.y - 13f);
                f.Rect.anchoredPosition = f.From;
            }
            foreach (var c in cells) if (c.Rect.gameObject.activeSelf) c.Frame.effectColor = CombatDemoTheme.Brass;
        }

        private void PlateChip(int enemyId, string text, Color color, bool flash)
        {
            var plate = hud.PlateRect(enemyId);
            if (plate == null) return;
            int n;
            chipStack.TryGetValue(enemyId, out n);
            chipStack[enemyId] = n + 1;
            // 名牌下半部（下回合攻擊／狀態列之上，短暫覆蓋），兩格交替，避免同一時間的標籤重疊；不會跑出名牌撞到上方面板。
            var pos = plate.anchoredPosition + new Vector2(0f, 32f + (n % 2) * 30f);
            AddChip(text, color, pos);
            if (flash)
            {
                plateFlash[enemyId] = 0f;
                plateFlashColor[enemyId] = color;
            }
        }

        private void PlayerChip(string text, Color color, bool flash)
        {
            int n;
            chipStack.TryGetValue(0, out n);
            chipStack[0] = n + 1;
            // 玩家面板內右下（擬態錨定／敵方回合列），兩格交替。
            var pos = new Vector2(CombatDemoTheme.Margin + 440f - 10f, -CombatDemoTheme.Margin - 110f + (n % 2) * 30f);
            AddChip(text, color, pos, TextAnchor.MiddleRight, new Vector2(0f, 1f));
            if (flash)
            {
                playerFlash = 0f;
                playerFlashColor = color;
            }
        }

        /// <summary>anchor：名牌標籤用畫面中心（與名牌圖層相同），玩家標籤用左上角（與玩家面板相同），畫布尺寸改變時位置不偏。</summary>
        private void AddChip(string text, Color color, Vector2 pos, TextAnchor align = TextAnchor.MiddleCenter, Vector2? anchor = null)
        {
            ChipLog.Add(text);
            // 深色小底板＋文字（標籤蓋在面板上時仍清楚）。
            var rt = CombatLabUi.Panel("Chip", chipLayer, new Color(0.05f, 0.045f, 0.04f, 0.92f));
            rt.GetComponent<Image>().raycastTarget = false;
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(color.r, color.g, color.b, 0.9f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
            var t = Label("Text", rt, text, CombatDemoTheme.FontBody, TextAnchor.MiddleCenter, color);
            t.fontStyle = FontStyle.Bold;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            CombatLabUi.Stretch(t.rectTransform);
            rt.anchorMin = rt.anchorMax = anchor ?? new Vector2(0.5f, 0.5f);
            rt.pivot = align == TextAnchor.MiddleRight ? new Vector2(1f, 0.5f) : align == TextAnchor.MiddleLeft ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Mathf.Ceil(t.preferredWidth) + 20f, 28f);
            rt.anchoredPosition = pos;
            chips.Add(new Chip { Rect = rt, Text = t, From = pos, Age = 0f });
            // 略過動畫時一次播完：只保留最新的一批，避免畫面堆滿。
            while (chips.Count > 12)
            {
                CombatLabUi.Kill(chips[0].Rect.gameObject);
                chips.RemoveAt(0);
            }
        }

        private void ShowRecap()
        {
            int dmg = 0, resolved = 0, shield = 0, correction = 0, linkGain = 0, absorbed = 0, anchor = 0;
            bool res = false;
            var residues = new HashSet<string>();
            foreach (var ev in arena.PlayedEvents)
            {
                string src = ev.Source ?? "";
                switch (ev.Kind)
                {
                    case EventKind.Pressure:
                    case EventKind.Conduction: dmg += ev.Value; break;
                    case EventKind.EnemyResolved: resolved++; break;
                    case EventKind.ShieldChanged:
                        if (ev.Amount > 0) shield += ev.Amount;
                        else if (src.StartsWith("enemy:")) absorbed -= ev.Amount;
                        break;
                    case EventKind.CorrectionChanged: if (ev.Amount > 0) correction += ev.Amount; break;
                    case EventKind.LinkChanged: if (ev.Amount > 0) linkGain += ev.Amount; break;
                    case EventKind.AnchorChanged: anchor += ev.Amount; break;
                    case EventKind.Resonance: res = true; break;
                }
                if (src.StartsWith("residue:")) residues.Add(src);
            }
            recapText.text = "<b><color=" + CombatDemoTheme.Hex(CombatDemoTheme.Brass) + ">" + CombatDemoText.RecapTitle + "</color></b>　"
                + "<color=" + CombatDemoTheme.Hex(CombatDemoTheme.Muted) + ">" + CombatDemoText.RecapHint + "</color>\n"
                + string.Format(CombatDemoText.RecapLine1, dmg, resolved, shield, correction, linkGain) + "\n"
                + string.Format(CombatDemoText.RecapLine2, residues.Count, res ? CombatDemoText.RecapYes : CombatDemoText.RecapNo, absorbed, CombatLabText.Signed(anchor));
            recapText.gameObject.SetActive(true);
            castTitle.text = CombatDemoText.RecapTitle;
            recapTime = 0f;
            recapRound = S.Snapshot().Round;
        }

        public void DismissRecap()
        {
            if (!arena.IsPlaying) HideCast();
        }

        private void HideCast()
        {
            castPanel.gameObject.SetActive(false);
            recapText.gameObject.SetActive(false);
            recapTime = -1f;
        }

        // ---------------- 動畫 ----------------

        private void AnimateCells(float dt)
        {
            foreach (var c in cells)
            {
                if (c.LitTime < 0f) continue;
                c.LitTime += dt;
                // 點亮瞬間輕微放大再回到原尺寸（降低動態時不縮放）。
                float s = Calm ? 1f : 1f + 0.08f * Mathf.Max(0f, 1f - c.LitTime / 0.35f);
                c.Rect.localScale = new Vector3(s, s, 1f);
                if (c.LitTime > 0.35f) c.LitTime = -1f;
            }
            if (aggregateTime >= 0f)
            {
                aggregateTime += dt;
                float k = Mathf.Clamp01(aggregateTime / 0.5f);
                float e = k * k * (3f - 2f * k);
                foreach (var f in flyers)
                {
                    if (!f.Rect.gameObject.activeSelf) continue;
                    f.Rect.anchoredPosition = Vector2.Lerp(f.From, f.To, e);
                    if (k >= 1f) f.Rect.gameObject.SetActive(false);
                }
                if (!Calm) aggregate.color = Color.Lerp(Color.white, CombatDemoTheme.Brass, k);
                if (k >= 1f) aggregateTime = -1f;
            }
        }

        private void AnimateChips(float dt)
        {
            for (int i = chips.Count - 1; i >= 0; i--)
            {
                var c = chips[i];
                c.Age += dt;
                if (c.Age >= ChipLife || c.Rect == null)
                {
                    if (c.Rect != null) CombatLabUi.Kill(c.Rect.gameObject);
                    chips.RemoveAt(i);
                    continue;
                }
                // 輕微上浮（留在面板內）；降低動態時原地淡出。
                if (!Calm) c.Rect.anchoredPosition = c.From + new Vector2(0f, 8f * (c.Age / ChipLife));
                float a = c.Age < ChipLife - 0.45f ? 1f : (ChipLife - c.Age) / 0.45f;
                var g = c.Rect.GetComponent<CanvasGroup>();
                if (g == null) g = c.Rect.gameObject.AddComponent<CanvasGroup>();
                g.alpha = a;
            }
        }

        private const float FlashLife = 0.4f;

        /// <summary>名牌與玩家面板的命中閃光（0.4 秒淡出；降低動態時是固定淡色、不閃）。</summary>
        private void AnimateFlash(float dt)
        {
            var keys = new List<int>(plateFlash.Keys);
            foreach (var id in keys)
            {
                float t = plateFlash[id] + dt;
                bool done = t > FlashLife;
                hud.SetPlateFlash(id, plateFlashColor[id], done ? 0f : FlashAmount(t));
                if (done) plateFlash.Remove(id);
                else plateFlash[id] = t;
            }
            if (playerFlash >= 0f)
            {
                playerFlash += dt;
                bool done = playerFlash > FlashLife;
                hud.SetPlayerFlash(playerFlashColor, done ? 0f : FlashAmount(playerFlash));
                if (done) playerFlash = -1f;
            }
        }

        private float FlashAmount(float t) { return Calm ? 0.5f : 1f - t / FlashLife; }
    }
}
