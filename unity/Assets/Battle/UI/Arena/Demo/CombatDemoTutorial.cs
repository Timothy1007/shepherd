using System.Collections.Generic;
using Shepherd.Battle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Battle.UI.Arena
{
    public enum CombatTutorialStep
    {
        Threat = 0,
        Resources = 1,
        Compose = 2,
        Commit = 3,
        /// <summary>結算播放中：第一次承接／共鳴／敵方攻擊真正發生時顯示短說明。</summary>
        Results = 4,
        /// <summary>回合結束後的收尾一句。</summary>
        Wrap = 5,
        Done = 6,
    }

    /// <summary>
    /// 首戰情境教學（overlay）：四個節點，每節只指出當下要做的一件事；玩家可以直接操作、跳過、重看。
    /// 只「觀察」Session 與播放中的真實 BattleEvent 來推進，不進入 RoundResolver、不改快照、牌庫、AI 或數值，也不鎖住任何操作。
    /// </summary>
    public sealed class CombatDemoTutorial
    {
        private readonly CombatDemoHud hud;
        private readonly CombatArenaView arena;
        private readonly RectTransform root;
        private RectTransform overlay;
        private RectTransform frame;
        private Text arrow;
        private RectTransform panel;
        private Text title;
        private Text body;
        private Button continueButton;
        private Button skipButton;
        private RectTransform note;
        private Text noteText;
        private float noteTime = -1f;
        private float clock;
        private float stepTime;
        private int seenEvents;
        private CommitResult seenCommit;
        /// <summary>教學開始後（未跳過）整場戰鬥都會在第一次發生時給短說明，即使四個節點已結束。</summary>
        private bool watching;
        private SchoolKind startSchool;
        private int? startTarget;
        private bool composeMore;
        private bool schoolNoted;

        public CombatTutorialStep Step { get; private set; }
        public bool Active { get { return Step != CombatTutorialStep.Done; } }
        public bool Skipped { get; private set; }
        /// <summary>顯示過的事件說明（每種只顯示一次）。</summary>
        public readonly List<string> Notes = new List<string>();
        private bool Shown(RectTransform rt) { return rt != null && overlay.gameObject.activeSelf && rt.gameObject.activeSelf; }
        public bool PanelVisible { get { return Shown(panel); } }
        public bool HighlightVisible { get { return Shown(frame); } }
        public bool NoteVisible { get { return Shown(note); } }
        public string Title { get { return title != null ? title.text : ""; } }
        public string Body { get { return body != null ? body.text : ""; } }
        public string NoteText { get { return noteText != null ? noteText.text : ""; } }
        /// <summary>目前高亮的目標（測試用）。</summary>
        public RectTransform HighlightTarget { get; private set; }

        public CombatDemoTutorial(CombatDemoHud hud, CombatArenaView arena, RectTransform root)
        {
            this.hud = hud;
            this.arena = arena;
            this.root = root;
            Build();
            Close();
        }

        private CombatLabSession S { get { return arena.Session; } }

        // ---------------- 建立 ----------------

        private void Build()
        {
            overlay = CombatLabUi.Rect("Tutorial", root);
            CombatLabUi.Stretch(overlay);
            var fr = CombatLabUi.Panel("Highlight", overlay, new Color(0f, 0f, 0f, 0f));
            fr.anchorMin = fr.anchorMax = new Vector2(0.5f, 0.5f);
            fr.GetComponent<Image>().raycastTarget = false;
            // 空心框（四條邊），不遮住被指向的內容。
            Edge(fr, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 4f));
            Edge(fr, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f));
            Edge(fr, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(4f, 0f));
            Edge(fr, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(4f, 0f));
            frame = fr;
            arrow = CombatLabUi.Label("Arrow", overlay, "▼", CombatDemoTheme.FontBig, TextAnchor.MiddleCenter, CombatDemoTheme.Brass);
            arrow.raycastTarget = false;
            arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            arrow.rectTransform.sizeDelta = new Vector2(40f, 40f);

            panel = CombatLabUi.Panel("TutorialPanel", overlay, CombatDemoTheme.Charcoal);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 0.5f);
            panel.anchoredPosition = new Vector2(CombatDemoTheme.Margin, 40f);
            panel.sizeDelta = new Vector2(450f, 10f);
            var po = panel.gameObject.AddComponent<Outline>();
            po.effectColor = CombatDemoTheme.Brass;
            po.effectDistance = new Vector2(2f, -2f);
            var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var v = CombatLabUi.Vertical(panel, 8f, 14);
            v.childForceExpandHeight = false;
            title = CombatLabUi.Label("Title", panel, "", CombatDemoTheme.FontLabel, TextAnchor.MiddleLeft, CombatDemoTheme.Brass);
            title.fontStyle = FontStyle.Bold;
            title.raycastTarget = false;
            body = CombatLabUi.Label("Body", panel, "", CombatDemoTheme.FontBody, TextAnchor.UpperLeft, CombatDemoTheme.Ivory);
            body.raycastTarget = false;
            var hint = CombatLabUi.Label("Hint", panel, CombatDemoText.TutorialFreeHint, CombatDemoTheme.FontSmall, TextAnchor.MiddleLeft, CombatDemoTheme.Muted);
            hint.raycastTarget = false;
            var row = CombatLabUi.Rect("Buttons", panel);
            CombatLabUi.Size(row, 40);
            var h = CombatLabUi.Horizontal(row, 10f);
            h.childForceExpandWidth = true;
            skipButton = CombatLabUi.Button("Skip", row, CombatDemoText.TutorialSkip, Skip, CombatDemoTheme.FontBody);
            continueButton = CombatLabUi.Button("Continue", row, CombatDemoText.TutorialContinue, Continue, CombatDemoTheme.FontBody);
            ((Image)continueButton.targetGraphic).color = CombatDemoTheme.Primary;
            continueButton.GetComponentInChildren<Text>().color = new Color(0.08f, 0.06f, 0.03f);

            // 戰場下方（手牌與術式面板之上），不壓到敵人名牌。
            note = CombatLabUi.Panel("TutorialNote", overlay, CombatDemoTheme.Charcoal);
            note.anchorMin = note.anchorMax = note.pivot = new Vector2(0.5f, 0f);
            note.anchoredPosition = new Vector2(-60f, 330f);
            note.sizeDelta = new Vector2(560f, 46f);
            var no = note.gameObject.AddComponent<Outline>();
            no.effectColor = CombatDemoTheme.Brass;
            noteText = CombatLabUi.Label("Text", note, "", CombatDemoTheme.FontBody, TextAnchor.MiddleCenter, CombatDemoTheme.Ivory);
            CombatLabUi.Stretch(noteText.rectTransform, 6f);
            noteText.raycastTarget = false;
            var noteButton = note.gameObject.AddComponent<Button>();
            noteButton.targetGraphic = note.GetComponent<Image>();
            noteButton.onClick.AddListener(HideNote);
            note.gameObject.SetActive(false);
        }

        private static void Edge(RectTransform parent, Vector2 min, Vector2 max, Vector2 pivot, Vector2 size)
        {
            var e = CombatLabUi.Panel("Edge", parent, CombatDemoTheme.Brass);
            e.anchorMin = min;
            e.anchorMax = max;
            e.pivot = pivot;
            e.anchoredPosition = Vector2.zero;
            e.sizeDelta = size;
            e.GetComponent<Image>().raycastTarget = false;
        }

        // ---------------- 控制 ----------------

        /// <summary>從第一節開始（首戰自動觸發，或玩家按「？教學」重看）。</summary>
        public void Start()
        {
            Skipped = false;
            Notes.Clear();
            watching = true;
            seenCommit = arena.LastCommit;
            seenEvents = arena.PlayedEvents.Count;
            composeMore = false;
            schoolNoted = false;
            overlay.gameObject.SetActive(true);
            overlay.SetAsLastSibling();
            Enter(arena.IsPlaying ? CombatTutorialStep.Results : CombatTutorialStep.Threat);
        }

        public void Skip()
        {
            Skipped = true;
            watching = false;
            Close();
        }

        /// <summary>完全停止（戰鬥結束或 HUD 銷毀）：不再顯示任何說明。</summary>
        public void Stop()
        {
            watching = false;
            Close();
        }

        public bool Watching { get { return watching; } }

        /// <summary>「繼續」：前往下一節（任何時候都能按，不必先完成動作）。</summary>
        public void Continue()
        {
            switch (Step)
            {
                case CombatTutorialStep.Threat: Enter(CombatTutorialStep.Resources); break;
                case CombatTutorialStep.Resources: Enter(CombatTutorialStep.Compose); break;
                case CombatTutorialStep.Compose:
                    if (!composeMore && S.Staged.Count > 0) ShowComposeMore();
                    else Enter(CombatTutorialStep.Commit);
                    break;
                case CombatTutorialStep.Commit: Enter(CombatTutorialStep.Results); break;
                case CombatTutorialStep.Wrap: Close(); break;
                default: break;
            }
        }

        /// <summary>完全關閉（跳過、完成、戰鬥結束、HUD 銷毀前）：不留下任何遮罩或提示。</summary>
        public void Close()
        {
            Step = CombatTutorialStep.Done;
            HighlightTarget = null;
            if (panel != null) panel.gameObject.SetActive(false);
            if (frame != null) frame.gameObject.SetActive(false);
            if (arrow != null) arrow.gameObject.SetActive(false);
            if (overlay != null) overlay.gameObject.SetActive(false);
            if (note != null) note.gameObject.SetActive(false);
            noteTime = -1f;
        }

        private void Enter(CombatTutorialStep step)
        {
            Step = step;
            stepTime = 0f;
            startSchool = S.SelectedSchool;
            startTarget = S.SelectedTargetId;
            panel.gameObject.SetActive(true);
            continueButton.gameObject.SetActive(true);
            skipButton.gameObject.SetActive(true);
            continueButton.GetComponentInChildren<Text>().text = CombatDemoText.TutorialContinue;
            switch (step)
            {
                case CombatTutorialStep.Threat:
                    Show(1, CombatDemoText.T1Name, CombatDemoText.T1Body, FirstAlivePlate());
                    break;
                case CombatTutorialStep.Resources:
                    Show(2, CombatDemoText.T2Name, CombatDemoText.T2Body, hud.SchoolsRect);
                    break;
                case CombatTutorialStep.Compose:
                    Show(3, CombatDemoText.T3Name, CombatDemoText.T3Body, hud.HandRect);
                    if (S.Staged.Count > 0) ShowComposeMore();
                    break;
                case CombatTutorialStep.Commit:
                    Show(4, CombatDemoText.T4Name, CombatDemoText.T4Body, hud.CommitRect);
                    break;
                case CombatTutorialStep.Results:
                    // 播放中只在真正發生時顯示短說明，不擋畫面。
                    panel.gameObject.SetActive(false);
                    HighlightTarget = null;
                    break;
                case CombatTutorialStep.Wrap:
                    title.text = CombatDemoText.TutorialDoneTitle;
                    body.text = CombatDemoText.TutorialDone;
                    HighlightTarget = null;
                    continueButton.GetComponentInChildren<Text>().text = CombatDemoText.TutorialClose;
                    skipButton.gameObject.SetActive(false);
                    break;
            }
            Position();
        }

        private void Show(int n, string name, string text, RectTransform target)
        {
            title.text = string.Format(CombatDemoText.TutorialTitle, n, name);
            body.text = text;
            HighlightTarget = target;
        }

        private void ShowComposeMore()
        {
            composeMore = true;
            body.text = CombatDemoText.T3More;
            HighlightTarget = hud.SequenceRect;
            Position();
        }

        private RectTransform FirstAlivePlate()
        {
            foreach (var e in S.Snapshot().Enemies) if (!e.Resolved) return hud.PlateRect(e.Id);
            return hud.ApRect;
        }

        // ---------------- 每影格 ----------------

        public void Tick(float dt)
        {
            clock += dt;
            if (noteTime >= 0f)
            {
                noteTime += dt;
                if (noteTime > 4.5f) HideNote();
            }
            if (watching) WatchEvents();
            if (S.IsOver && !arena.IsPlaying)
            {
                // 戰鬥結束：不與結果橫幅重疊，也不留下任何說明。
                if (watching || Active || NoteVisible) Stop();
                return;
            }
            if (!Active)
            {
                // 節點結束後，短說明仍需要 overlay 顯示。
                if (overlay.gameObject.activeSelf != NoteVisible) overlay.gameObject.SetActive(NoteVisible);
                return;
            }
            stepTime += dt;
            // 任何時候按下確認：直接進入結果說明（不鎖操作、不倒退）。
            if (arena.IsPlaying && Step < CombatTutorialStep.Results) Enter(CombatTutorialStep.Results);
            switch (Step)
            {
                case CombatTutorialStep.Threat:
                    if (S.Staged.Count > 0) Enter(CombatTutorialStep.Compose);
                    else if (S.SelectedTargetId != startTarget) Enter(CombatTutorialStep.Resources);
                    break;
                case CombatTutorialStep.Resources:
                    if (S.Staged.Count > 0) Enter(CombatTutorialStep.Compose);
                    else if (S.SelectedSchool != startSchool && !schoolNoted)
                    {
                        schoolNoted = true;
                        body.text = CombatDemoText.T2Body + "\n<color=" + CombatDemoTheme.Hex(CombatDemoTheme.Good) + ">" + CombatDemoText.T2Done + "</color>";
                        HighlightTarget = hud.HandRect;
                    }
                    break;
                case CombatTutorialStep.Compose:
                    if (!composeMore && S.Staged.Count > 0) ShowComposeMore();
                    else if (composeMore && S.Staged.Count >= 2 && stepTime > 0.5f) Enter(CombatTutorialStep.Commit);
                    break;
                case CombatTutorialStep.Results:
                    if (!arena.IsPlaying) Enter(CombatTutorialStep.Wrap);
                    break;
            }
            Position();
        }

        /// <summary>第一次真正發生的承接／共鳴／敵方攻擊：依實際播放的 BattleEvent 顯示一次。</summary>
        private void WatchEvents()
        {
            if (!ReferenceEquals(arena.LastCommit, seenCommit))
            {
                seenCommit = arena.LastCommit;
                seenEvents = 0;
            }
            var events = arena.PlayedEvents;
            for (; seenEvents < events.Count; seenEvents++)
            {
                var e = events[seenEvents];
                if (e.Source != null && e.Source.StartsWith("residue:")) ShowNote(CombatDemoText.NoteResidue);
                else if (e.Kind == EventKind.Resonance) ShowNote(CombatDemoText.NoteResonance);
                else if (e.Kind == EventKind.EnemyActed && e.Amount > 0) ShowNote(CombatDemoText.NoteEnemy);
            }
        }

        private void ShowNote(string text)
        {
            if (Notes.Contains(text)) return;
            Notes.Add(text);
            noteText.text = text;
            overlay.gameObject.SetActive(true);
            overlay.SetAsLastSibling();
            note.gameObject.SetActive(true);
            noteTime = 0f;
        }

        public void HideNote()
        {
            if (note != null) note.gameObject.SetActive(false);
            noteTime = -1f;
        }

        /// <summary>高亮框與箭頭跟著目標（名牌會隨敵人移動）。降低動態時不脈動。</summary>
        private void Position()
        {
            if (HighlightTarget == null || !HighlightTarget.gameObject.activeInHierarchy || !panel.gameObject.activeSelf)
            {
                frame.gameObject.SetActive(false);
                arrow.gameObject.SetActive(false);
                return;
            }
            var corners = new Vector3[4];
            HighlightTarget.GetWorldCorners(corners);
            Vector2 a = root.InverseTransformPoint(corners[0]), b = root.InverseTransformPoint(corners[2]);
            float pulse = hud.Options.ReduceMotion ? 0f : Mathf.Sin(clock * 3f) * 3f;
            frame.gameObject.SetActive(true);
            frame.anchoredPosition = (a + b) * 0.5f;
            frame.sizeDelta = new Vector2(Mathf.Abs(b.x - a.x) + 16f + pulse, Mathf.Abs(b.y - a.y) + 16f + pulse);
            // 箭頭指向目標：上半部的目標從下方指上去，下半部的目標從上方指下來。
            bool upper = (a.y + b.y) * 0.5f > 0f;
            arrow.gameObject.SetActive(true);
            arrow.text = upper ? "▲" : "▼";
            float y = upper ? Mathf.Min(a.y, b.y) - 26f - pulse : Mathf.Max(a.y, b.y) + 26f + pulse;
            arrow.rectTransform.anchoredPosition = new Vector2((a.x + b.x) * 0.5f, y);
        }
    }
}
