using System.Collections.Generic;

namespace Shepherd.Battle.Core
{
    public sealed class PlannedCard
    {
        public int CardId;
        public ElementKind Element;
        /// <summary>目標敵人編號；沒有敵方指向部分的牌可為 null。</summary>
        public int? TargetEnemyId;

        public PlannedCard() { }

        public PlannedCard(int cardId, ElementKind element, int? targetEnemyId)
        {
            CardId = cardId;
            Element = element;
            TargetEnemyId = targetEnemyId;
        }
    }

    public sealed class RoundPlan
    {
        public SchoolKind School;
        public IReadOnlyList<PlannedCard> Cards = new PlannedCard[0];

        public RoundPlan() { }

        public RoundPlan(SchoolKind school, IReadOnlyList<PlannedCard> cards)
        {
            School = school;
            Cards = cards ?? new PlannedCard[0];
        }

        public static RoundPlan Empty(SchoolKind school)
        {
            return new RoundPlan(school, new PlannedCard[0]);
        }
    }

    public sealed class PlanValidation
    {
        public readonly bool IsValid;
        public readonly string Error;

        private PlanValidation(bool ok, string error)
        {
            IsValid = ok;
            Error = error;
        }

        public static readonly PlanValidation Ok = new PlanValidation(true, null);

        public static PlanValidation Fail(string error)
        {
            return new PlanValidation(false, error);
        }
    }

    /// <summary>編排驗證（規格第三節回合流程第 2 步、屬性測試 P3）。</summary>
    public static class PlanValidator
    {
        public static PlanValidation Validate(BattleState state, RoundPlan plan, RuleSet rules)
        {
            if (state == null || plan == null || rules == null) return PlanValidation.Fail("缺少參數");
            if (state.Outcome != BattleOutcome.Ongoing) return PlanValidation.Fail("戰鬥已結束");
            if (plan.Cards == null) return PlanValidation.Fail("序列為 null");
            if (plan.Cards.Count > RuleSet.ActionPoints)
                return PlanValidation.Fail("超過 " + RuleSet.ActionPoints + " 張（AP 不足）");

            var used = new HashSet<int>();
            for (int i = 0; i < plan.Cards.Count; i++)
            {
                var c = plan.Cards[i];
                if (c == null) return PlanValidation.Fail("第 " + i + " 張為 null");
                if (!state.Hand.Contains(c.CardId)) return PlanValidation.Fail("牌 " + c.CardId + " 不在手牌");
                if (!used.Add(c.CardId)) return PlanValidation.Fail("牌 " + c.CardId + " 重複編排");
                if (state.Card(c.CardId).Element != c.Element)
                    return PlanValidation.Fail("牌 " + c.CardId + " 的元素不符");

                bool needsTarget = rules.RequiresTarget(plan.School, c.Element);
                if (needsTarget && !c.TargetEnemyId.HasValue)
                    return PlanValidation.Fail("第 " + i + " 張需要指定目標");
                if (c.TargetEnemyId.HasValue)
                {
                    var e = state.FindEnemy(c.TargetEnemyId.Value);
                    if (e == null || !e.Alive)
                        return PlanValidation.Fail("第 " + i + " 張的目標必須是存活的敵人");
                }
            }
            return PlanValidation.Ok;
        }
    }
}
