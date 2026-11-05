using System;
using System.Collections.Generic;
using Shepherd.Battle.Core;

namespace Shepherd.Battle.Tests
{
    /// <summary>屬性測試用的狀態建構工具。</summary>
    internal static class TestBuilder
    {
        public static EnemyState Enemy(int id, int value, int attack, int links = 0, int corrections = 0, int bind = 0)
        {
            return new EnemyState
            {
                Id = id,
                Name = "E" + id,
                Value = value,
                AttackPattern = new[] { attack },
                Links = links,
                Corrections = corrections,
                Bind = bind,
            };
        }

        /// <summary>
        /// 標準 27 張牌；手牌先放入指定元素（依序），再以編號最小的牌補滿 7 張，其餘進牌庫。
        /// 回傳的 handIds 與 handElements 同序。
        /// </summary>
        public static BattleState State(int anchor, int shield, EnemyState[] enemies, ElementKind[] handElements,
            out List<int> handIds)
        {
            var rules = RuleSet.CreateDefault();
            var s = new BattleState();
            s.Anchor = anchor;
            s.Shield = shield;
            foreach (var e in enemies) s.Enemies.Add(e.Clone());
            s.Cards = DeckService.CreateStandardCards(rules);

            var remaining = new List<int>();
            foreach (var c in s.Cards) remaining.Add(c.Id);
            handIds = new List<int>();
            foreach (var el in handElements)
            {
                int pick = remaining.Find(id => s.Card(id).Element == el);
                if (!remaining.Remove(pick) || s.Card(pick).Element != el)
                    throw new InvalidOperationException("沒有足夠的 " + el);
                s.Hand.Add(pick);
                handIds.Add(pick);
            }
            while (s.Hand.Count < RuleSet.HandSize)
            {
                s.Hand.Add(remaining[0]);
                remaining.RemoveAt(0);
            }
            s.Deck.AddRange(remaining);
            return s;
        }

        public static RoundPlan Plan(SchoolKind school, params PlannedCard[] cards)
        {
            return new RoundPlan(school, cards);
        }

        public static PlannedCard Card(BattleState s, int cardId, int? target)
        {
            return new PlannedCard(cardId, s.Card(cardId).Element, target);
        }

        public static int CountElement(BattleState s, ElementKind element)
        {
            int n = 0;
            foreach (var list in new[] { s.Deck, s.Hand, s.Discard })
            {
                foreach (int id in list)
                {
                    if (s.Card(id).Element == element) n++;
                }
            }
            return n;
        }

        public static List<string> Strings(IEnumerable<BattleEvent> events)
        {
            var list = new List<string>();
            foreach (var e in events) list.Add(e.ToString());
            return list;
        }

        /// <summary>選手牌前 n 張組成序列；需要目標的牌都指向第一個存活敵人。</summary>
        public static RoundPlan FirstCards(BattleState s, SchoolKind school, int n, RuleSet rules)
        {
            int target = 0;
            foreach (var e in s.Enemies)
            {
                if (e.Alive) { target = e.Id; break; }
            }
            var cards = new List<PlannedCard>();
            for (int i = 0; i < n && i < s.Hand.Count; i++)
            {
                var el = s.Card(s.Hand[i]).Element;
                int? t = rules.RequiresTarget(school, el) ? target : (int?)null;
                cards.Add(new PlannedCard(s.Hand[i], el, t));
            }
            return new RoundPlan(school, cards);
        }
    }
}
