using System;
using System.Collections.Generic;
using Shepherd.Battle.Core;

namespace Shepherd.Battle.UI
{
    /// <summary>編排區中的一張牌：記錄放入時指定的目標。</summary>
    public sealed class StagedCard
    {
        public readonly int CardId;
        public readonly ElementKind Element;
        public readonly int? TargetEnemyId;

        public StagedCard(int cardId, ElementKind element, int? targetEnemyId)
        {
            CardId = cardId;
            Element = element;
            TargetEnemyId = targetEnemyId;
        }
    }

    /// <summary>
    /// Combat Lab 的介面狀態（Presenter）。純 C#，不含任何戰鬥公式：
    /// 所有規則判斷交給 BattleController／PlanValidator，所有數值來自 RoundResolver。
    /// 介面只保留「目前選的學派、目前目標、編排區」這些尚未確認的操作狀態。
    /// </summary>
    public sealed class CombatLabSession
    {
        public const int MaxLogLines = 400;

        private readonly RuleSet rules;
        private readonly CombatLabScenario scenario;
        private readonly List<StagedCard> staged = new List<StagedCard>();
        private readonly List<string> log = new List<string>();
        private BattleController controller;

        public event Action Changed;

        public CombatLabSession(RuleSet rules, CombatLabScenario scenario, int seed)
        {
            if (rules == null) throw new ArgumentNullException("rules");
            if (scenario == null) throw new ArgumentNullException("scenario");
            this.rules = rules;
            this.scenario = scenario;
            Reset(seed);
        }

        public RuleSet Rules { get { return rules; } }
        public int Seed { get; private set; }
        public SchoolKind SelectedSchool { get; private set; }
        public int? SelectedTargetId { get; private set; }
        /// <summary>依目前編排即時計算的預覽（每次編排、學派或調度變動後自動更新；戰鬥結束時為 null）。</summary>
        public PreviewResult LastPreview { get; private set; }
        /// <summary><see cref="LastPreview"/> 的前後對照摘要；預覽不合法或戰鬥結束時為 null。</summary>
        public RoundForecast Forecast { get; private set; }
        /// <summary>上一次確認施放的結算摘要（尚未確認過則為 null）。</summary>
        public RoundForecast LastRound { get; private set; }
        /// <summary>上一次確認的回合（1 起算）。</summary>
        public int LastRoundNumber { get; private set; }
        public string Message { get; private set; }
        public BattleController Controller { get { return controller; } }
        public IReadOnlyList<StagedCard> Staged { get { return staged.AsReadOnly(); } }
        public IReadOnlyList<string> Log { get { return log.AsReadOnly(); } }

        public int ApMax { get { return RuleSet.ActionPoints; } }
        public int ApRemaining { get { return ApMax - staged.Count; } }
        public bool IsOver { get { return controller.Outcome != BattleOutcome.Ongoing; } }

        public BattleSnapshot Snapshot()
        {
            return controller.Snapshot();
        }

        // ---------------- 戰鬥 ----------------

        public void Reset(int seed)
        {
            Seed = seed;
            controller = BattleController.NewBattle(scenario.Enemies, rules, seed);
            staged.Clear();
            log.Clear();
            LastRound = null;
            SelectedSchool = SchoolKind.Structure;
            SelectedTargetId = null;
            EnsureTarget();
            Message = "戰鬥已重置（種子 " + seed + "）";
            AddLog("══ " + scenario.Name + "，種子 " + seed + " ══");
            AddLog(HandLine());
            controller.ClearStage();
            UpdatePreview();
            Notify();
        }

        // ---------------- 學派 ----------------

        /// <summary>選擇學派。確認施放時由引擎鎖定；若引擎已鎖定其他學派則拒絕。</summary>
        public bool SelectSchool(SchoolKind school)
        {
            if (IsOver) return Fail("戰鬥已結束");
            if (controller.LockedSchool.HasValue && controller.LockedSchool.Value != school)
                return Fail("學派已鎖定為 " + CombatLabText.School(controller.LockedSchool.Value) + "，本回合不得更換");
            SelectedSchool = school;
            UpdatePreview();
            SyncStage();
            Message = "學派：" + CombatLabText.School(school);
            Notify();
            return true;
        }

        // ---------------- 目標 ----------------

        public bool SelectTarget(int enemyId)
        {
            if (IsOver) return Fail("戰鬥已結束");
            var e = FindEnemy(enemyId);
            if (e == null || e.Resolved) return Fail("只能選擇存活的敵人");
            SelectedTargetId = enemyId;
            Message = "目前目標：" + e.Name + "（之後加入編排的牌會指向它）";
            Notify();
            return true;
        }

        // ---------------- 編排 ----------------

        public bool IsStaged(int cardId)
        {
            return StagedIndex(cardId) >= 0;
        }

        public int StagedIndex(int cardId)
        {
            for (int i = 0; i < staged.Count; i++)
            {
                if (staged[i].CardId == cardId) return i;
            }
            return -1;
        }

        /// <summary>點擊手牌：未編排則加入序列尾端，已編排則移除。</summary>
        public bool ToggleCard(int cardId)
        {
            return IsStaged(cardId) ? RemoveCard(cardId) : AddCard(cardId);
        }

        public bool AddCard(int cardId)
        {
            if (IsOver) return Fail("戰鬥已結束");
            var state = controller.CloneState();
            if (!state.Hand.Contains(cardId)) return Fail("這張牌不在手牌中");
            if (IsStaged(cardId)) return Fail("同一張牌不能重複加入");
            if (staged.Count >= ApMax) return Fail("AP 不足：每回合最多 " + ApMax + " 張");

            EnsureTarget();
            var card = state.Card(cardId);
            var candidate = new List<StagedCard>(staged);
            candidate.Add(new StagedCard(cardId, card.Element, SelectedTargetId));
            var v = controller.Validate(BuildPlan(candidate, SelectedSchool));
            if (!v.IsValid) return Fail(v.Error);

            staged.Add(candidate[candidate.Count - 1]);
            UpdatePreview();
            SyncStage();
            Message = "加入第 " + staged.Count + " 張：" + CombatLabText.Element(card.Element);
            Notify();
            return true;
        }

        public bool RemoveCard(int cardId)
        {
            int i = StagedIndex(cardId);
            return i >= 0 && RemoveAt(i);
        }

        public bool RemoveAt(int index)
        {
            if (index < 0 || index >= staged.Count) return Fail("編排中沒有這個位置");
            staged.RemoveAt(index);
            UpdatePreview();
            SyncStage();
            Message = "已移除第 " + (index + 1) + " 張";
            Notify();
            return true;
        }

        public void ClearPlan()
        {
            staged.Clear();
            UpdatePreview();
            SyncStage();
            Message = "已清空編排";
            Notify();
        }

        public RoundPlan BuildPlan()
        {
            return BuildPlan(staged, SelectedSchool);
        }

        /// <summary>只有該學派下有敵方指向部分的牌才帶目標（是否需要目標由規則集判斷）。</summary>
        private RoundPlan BuildPlan(List<StagedCard> cards, SchoolKind school)
        {
            var list = new List<PlannedCard>();
            foreach (var c in cards)
            {
                int? target = rules.RequiresTarget(school, c.Element) ? c.TargetEnemyId : null;
                list.Add(new PlannedCard(c.CardId, c.Element, target));
            }
            return new RoundPlan(school, list);
        }

        public bool CardNeedsTarget(ElementKind element)
        {
            return rules.RequiresTarget(SelectedSchool, element);
        }

        // ---------------- 預覽與確認 ----------------

        /// <summary>
        /// 手動重新預覽（介面已改為即時預覽，此方法保留給測試與工具使用）。
        /// 呼叫同一個 RoundResolver，不改動真實狀態。
        /// </summary>
        public PreviewResult Preview()
        {
            var r = controller.Preview(BuildPlan());
            LastPreview = r;
            Forecast = r.Validation.IsValid ? RoundForecast.From(controller.CloneState(), r.Result) : null;
            Message = r.Validation.IsValid ? "預覽完成（尚未結算）" : "無法預覽：" + r.Validation.Error;
            Notify();
            return r;
        }

        /// <summary>
        /// 假設把這張牌加到編排尾端（目標為目前目標）時的預估結果；不改動編排與戰鬥狀態。
        /// 無法加入時回傳 null，reason 說明原因。
        /// </summary>
        public RoundForecast ForecastWithCard(int cardId, out string reason)
        {
            reason = null;
            if (IsOver) { reason = "戰鬥已結束"; return null; }
            if (IsStaged(cardId)) { reason = "已在編排第 " + (StagedIndex(cardId) + 1) + " 張"; return null; }
            if (staged.Count >= ApMax) { reason = "AP 已用完（每回合最多 " + ApMax + " 張）"; return null; }
            var state = controller.CloneState();
            if (!state.Hand.Contains(cardId)) { reason = "這張牌不在手牌中"; return null; }

            var candidate = new List<StagedCard>(staged);
            candidate.Add(new StagedCard(cardId, state.Card(cardId).Element, SelectedTargetId));
            var p = controller.Preview(BuildPlan(candidate, SelectedSchool));
            if (!p.Validation.IsValid) { reason = p.Validation.Error; return null; }
            return RoundForecast.From(state, p.Result);
        }

        /// <summary>確認施放／結束回合（空序列合法）。</summary>
        public CommitResult Commit()
        {
            var plan = BuildPlan();
            int round = controller.Snapshot().Round;
            var before = controller.CloneState();
            var r = controller.Commit(plan);
            if (!r.Accepted)
            {
                Message = "無法確認：" + r.Validation.Error;
                Notify();
                return r;
            }

            LastRound = RoundForecast.From(before, r.Resolution);
            LastRoundNumber = round + 1;
            AddLog("── 第 " + (round + 1) + " 回合 · " + CombatLabText.School(plan.School) + " · " + SequenceLine(plan) + " ──");
            var after = controller.CloneState();
            foreach (var e in r.Resolution.Events) AddLog(Describe(e, after));
            AddLog("  ＝ 本回合結果：錨定值 " + LastRound.AnchorBefore + " → " + LastRound.AnchorAfter
                + "，擬態錨定 " + LastRound.ShieldBefore + " → " + LastRound.ShieldAfter
                + "；" + CombatLabText.DamageSummary(LastRound));
            foreach (var e in r.RefillEvents) AddLog(Describe(e, after));
            if (controller.Outcome == BattleOutcome.Ongoing) AddLog(HandLine());

            staged.Clear();
            controller.ClearStage();
            EnsureTarget();
            UpdatePreview();
            string summary = "第 " + (round + 1) + " 回合：" + CombatLabText.DamageSummary(LastRound)
                + "（錨定值 " + LastRound.AnchorBefore + " → " + LastRound.AnchorAfter + "）";
            Message = controller.Outcome == BattleOutcome.Ongoing
                ? summary + "　→ 進入第 " + (round + 2) + " 回合"
                : "戰鬥結束：" + CombatLabText.Outcome(controller.Outcome) + "　" + summary;
            Notify();
            return r;
        }

        // ---------------- 調度 ----------------

        public bool Redraw(int cardId)
        {
            if (IsOver) return Fail("戰鬥已結束");
            if (IsStaged(cardId)) return Fail("已放進編排區的牌不能調度");
            int before = controller.Log.Count;
            if (!controller.TryRedraw(cardId))
                return Fail(controller.Snapshot().RedrawsRemaining <= 0 ? "本回合已調度過" : "無法調度這張牌");
            var after = controller.CloneState();
            for (int i = before; i < controller.Log.Count; i++) AddLog(Describe(controller.Log[i], after));
            UpdatePreview();
            Message = "調度完成";
            Notify();
            return true;
        }

        // ---------------- 顯示輔助 ----------------

        public string EnemyName(int id)
        {
            var e = FindEnemy(id);
            return e == null ? "E" + id : e.Name;
        }

        public string CardLabel(int cardId)
        {
            var s = controller.CloneState();
            return CombatLabText.Element(s.Card(cardId).Element) + "#" + cardId;
        }

        public string Describe(BattleEvent e)
        {
            return Describe(e, controller.CloneState());
        }

        private string Describe(BattleEvent e, BattleState s)
        {
            return CombatLabText.Event(e,
                id => { var en = s.FindEnemy(id); return en == null ? "E" + id : en.Name; },
                id => CombatLabText.Element(s.Card(id).Element) + "#" + id);
        }

        private string SequenceLine(RoundPlan plan)
        {
            if (plan.Cards.Count == 0) return "空序列";
            var parts = new List<string>();
            foreach (var c in plan.Cards)
            {
                string t = c.TargetEnemyId.HasValue ? "→" + EnemyName(c.TargetEnemyId.Value) : "";
                parts.Add(CombatLabText.Element(c.Element) + t);
            }
            return string.Join("、", parts.ToArray());
        }

        private string HandLine()
        {
            var parts = new List<string>();
            foreach (var c in controller.Snapshot().Hand) parts.Add(CombatLabText.Element(c.Element));
            return "  手牌：" + string.Join("、", parts.ToArray());
        }

        private EnemyState FindEnemy(int id)
        {
            return controller.CloneState().FindEnemy(id);
        }

        /// <summary>目前目標若不存在或已解除，改為第一個存活的敵人。</summary>
        private void EnsureTarget()
        {
            var s = controller.CloneState();
            if (SelectedTargetId.HasValue)
            {
                var e = s.FindEnemy(SelectedTargetId.Value);
                if (e != null && e.Alive) return;
            }
            SelectedTargetId = null;
            foreach (var e in s.Enemies)
            {
                if (e.Alive) { SelectedTargetId = e.Id; break; }
            }
        }

        /// <summary>即時預覽：依目前編排（含空序列）重新呼叫 RoundResolver，不改動真實狀態。</summary>
        private void UpdatePreview()
        {
            if (IsOver)
            {
                LastPreview = null;
                Forecast = null;
                return;
            }
            LastPreview = controller.Preview(BuildPlan());
            Forecast = LastPreview.Validation.IsValid ? RoundForecast.From(controller.CloneState(), LastPreview.Result) : null;
        }

        /// <summary>讓引擎的編排區與介面一致（調度會依此拒絕已編排的牌）。</summary>
        private void SyncStage()
        {
            if (staged.Count == 0)
            {
                controller.ClearStage();
                return;
            }
            // 引擎不接受時清空它的編排區；介面仍保留顯示，預覽／確認時會回報引擎給的原因。
            // 調度另外由 Redraw() 依介面編排區先行拒絕，所以兩邊不會不一致。
            if (!controller.TryStage(BuildPlan())) controller.ClearStage();
        }

        private void AddLog(string line)
        {
            log.Add(line);
            if (log.Count > MaxLogLines) log.RemoveRange(0, log.Count - MaxLogLines);
        }

        private bool Fail(string message)
        {
            Message = message;
            Notify();
            return false;
        }

        private void Notify()
        {
            var h = Changed;
            if (h != null) h();
        }
    }
}
