using System.Collections.Generic;

namespace Shepherd.Battle.Core
{
    public sealed class PreviewResult
    {
        public readonly PlanValidation Validation;
        /// <summary>驗證失敗時為 null。</summary>
        public readonly ResolveResult Result;

        public PreviewResult(PlanValidation validation, ResolveResult result)
        {
            Validation = validation;
            Result = result;
        }
    }

    public sealed class CommitResult
    {
        public readonly PlanValidation Validation;
        /// <summary>與預覽相同的結算結果（補牌之前）；驗證失敗時為 null。</summary>
        public readonly ResolveResult Resolution;
        public readonly IReadOnlyList<BattleEvent> RefillEvents;

        public CommitResult(PlanValidation validation, ResolveResult resolution, IReadOnlyList<BattleEvent> refillEvents)
        {
            Validation = validation;
            Resolution = resolution;
            RefillEvents = refillEvents;
        }

        public bool Accepted { get { return Validation.IsValid; } }
    }

    public sealed class EnemySnapshot
    {
        public int Id;
        public string Name;
        public int Value;
        public int Links;
        public int Corrections;
        public int Bind;
        public bool Resolved;
        public int ForecastAttack;
    }

    /// <summary>給 UI 的唯讀快照。</summary>
    public sealed class BattleSnapshot
    {
        public int Round;
        public int Anchor;
        public int Shield;
        public BattleOutcome Outcome;
        public SchoolKind? LockedSchool;
        public int RedrawsRemaining;
        public IReadOnlyList<CardDef> Hand;
        public int DeckCount;
        public int DiscardCount;
        public IReadOnlyList<EnemySnapshot> Enemies;
    }

    /// <summary>
    /// 介面唯一的入口：介面只下命令，並顯示快照與事件；所有數值計算只發生在 RoundResolver。
    /// </summary>
    public sealed class BattleController
    {
        private readonly RuleSet rules;
        private readonly IRandom rng;
        private readonly List<BattleEvent> log = new List<BattleEvent>();
        private BattleState state;
        private SchoolKind? lockedSchool;
        private int redrawsUsed;
        private RoundPlan staged;

        public BattleController(BattleState initialState, RuleSet rules, IRandom rng)
        {
            state = initialState.Clone();
            this.rules = rules;
            this.rng = rng;
        }

        public static BattleController NewBattle(IList<EnemyState> enemies, RuleSet rules, int seed)
        {
            var rng = new SeededRandom(seed);
            var s = DeckService.NewBattle(enemies, rules, rng);
            return new BattleController(s, rules, rng);
        }

        public RuleSet Rules { get { return rules; } }
        public SchoolKind? LockedSchool { get { return lockedSchool; } }
        public BattleOutcome Outcome { get { return state.Outcome; } }
        public IReadOnlyList<BattleEvent> Log { get { return log.AsReadOnly(); } }

        /// <summary>目前狀態的深複製（修改它不影響控制器）。</summary>
        public BattleState CloneState()
        {
            return state.Clone();
        }

        /// <summary>選擇學派並鎖定；同一回合不得更換（再次鎖定相同學派視為成功）。</summary>
        public bool TryLockSchool(SchoolKind school)
        {
            if (state.Outcome != BattleOutcome.Ongoing) return false;
            if (lockedSchool.HasValue) return lockedSchool.Value == school;
            lockedSchool = school;
            return true;
        }

        public PlanValidation Validate(RoundPlan plan)
        {
            if (plan != null && lockedSchool.HasValue && plan.School != lockedSchool.Value)
                return PlanValidation.Fail("學派已鎖定為 " + lockedSchool.Value + "，同回合不得更換");
            return PlanValidator.Validate(state, plan, rules);
        }

        /// <summary>放入編排區（調度不能用於已放進編排區的牌）。</summary>
        public bool TryStage(RoundPlan plan)
        {
            if (!Validate(plan).IsValid) return false;
            staged = plan;
            return true;
        }

        public void ClearStage()
        {
            staged = null;
        }

        /// <summary>預覽：呼叫同一個 Resolve，只顯示結果，不改動狀態。</summary>
        public PreviewResult Preview(RoundPlan plan)
        {
            var v = Validate(plan);
            if (!v.IsValid) return new PreviewResult(v, null);
            return new PreviewResult(v, RoundResolver.Resolve(state, plan, rules));
        }

        /// <summary>確認：採用 Resolve 回傳的新狀態，再呼叫 DeckService 補牌。</summary>
        public CommitResult Commit(RoundPlan plan)
        {
            var v = Validate(plan);
            if (!v.IsValid) return new CommitResult(v, null, new BattleEvent[0]);

            lockedSchool = plan.School;
            var result = RoundResolver.Resolve(state, plan, rules);
            state = result.NewState.Clone();
            log.AddRange(result.Events);

            IReadOnlyList<BattleEvent> refill = new BattleEvent[0];
            if (state.Outcome == BattleOutcome.Ongoing)
            {
                refill = DeckService.Refill(state, rng, rules);
                log.AddRange(refill);
            }

            // 進入下一回合。
            lockedSchool = null;
            redrawsUsed = 0;
            staged = null;
            return new CommitResult(v, result, refill);
        }

        /// <summary>調度：每回合一次、0 AP、只能在確認前使用，不能用於已放進編排區的牌。</summary>
        public bool TryRedraw(int cardId)
        {
            if (state.Outcome != BattleOutcome.Ongoing) return false;
            if (redrawsUsed >= rules.RedrawPerRound) return false;
            if (staged != null)
            {
                for (int i = 0; i < staged.Cards.Count; i++)
                {
                    if (staged.Cards[i].CardId == cardId) return false;
                }
            }

            int drawn;
            if (!DeckService.TryRedraw(state, cardId, rng, out drawn)) return false;
            redrawsUsed++;
            log.Add(new BattleEvent(EventKind.Redrawn, "player", 0, cardId, drawn));
            return true;
        }

        public BattleSnapshot Snapshot()
        {
            var hand = new List<CardDef>();
            for (int i = 0; i < state.Hand.Count; i++) hand.Add(state.Card(state.Hand[i]));

            var enemies = new List<EnemySnapshot>();
            for (int i = 0; i < state.Enemies.Count; i++)
            {
                var e = state.Enemies[i];
                enemies.Add(new EnemySnapshot
                {
                    Id = e.Id,
                    Name = e.Name,
                    Value = e.Value,
                    Links = e.Links,
                    Corrections = e.Corrections,
                    Bind = e.Bind,
                    Resolved = e.Resolved,
                    ForecastAttack = e.Resolved ? 0 : e.AttackForRound(state.Round),
                });
            }

            return new BattleSnapshot
            {
                Round = state.Round,
                Anchor = state.Anchor,
                Shield = state.Shield,
                Outcome = state.Outcome,
                LockedSchool = lockedSchool,
                RedrawsRemaining = rules.RedrawPerRound - redrawsUsed,
                Hand = hand.AsReadOnly(),
                DeckCount = state.Deck.Count,
                DiscardCount = state.Discard.Count,
                Enemies = enemies.AsReadOnly(),
            };
        }
    }
}
