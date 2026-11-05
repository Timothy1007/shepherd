using System;
using System.Collections.Generic;

namespace Shepherd.Battle.Core
{
    /// <summary>牌庫操作。整個 Core 只有這裡使用亂數（規格第六節）。</summary>
    public static class DeckService
    {
        /// <summary>v0.1 試玩牌庫：燃素、基體、擬合劑各 9 張（張數來自規則集）。</summary>
        public static List<CardDef> CreateStandardCards(RuleSet rules)
        {
            var cards = new List<CardDef>();
            int id = 0;
            for (int i = 0; i < rules.DeckEmber; i++) cards.Add(new CardDef(id++, ElementKind.Ember));
            for (int i = 0; i < rules.DeckBase; i++) cards.Add(new CardDef(id++, ElementKind.Base));
            for (int i = 0; i < rules.DeckBinder; i++) cards.Add(new CardDef(id++, ElementKind.Binder));
            return cards;
        }

        /// <summary>建立新戰鬥：洗牌並抽起手 7 張。</summary>
        public static BattleState NewBattle(IList<EnemyState> enemies, RuleSet rules, IRandom rng)
        {
            if (enemies == null || enemies.Count == 0) throw new ArgumentException("至少需要一個敵人");
            var s = new BattleState();
            s.Round = 0;
            s.Anchor = rules.StartAnchor;
            s.Shield = 0;
            for (int i = 0; i < enemies.Count; i++) s.Enemies.Add(enemies[i].Clone());
            s.Cards = CreateStandardCards(rules);
            for (int i = 0; i < s.Cards.Count; i++) s.Deck.Add(s.Cards[i].Id);
            Shuffle(s.Deck, rng);
            Refill(s, rng, rules);
            return s;
        }

        public static void Shuffle(List<int> cards, IRandom rng)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = cards[i];
                cards[i] = cards[j];
                cards[j] = t;
            }
        }

        /// <summary>
        /// 補牌至 7 張：優先抽牌庫；牌庫抽完時棄牌區洗回牌庫。
        /// v0.1 牌庫全是元素牌，物資保底自動滿足，因此不另外實作保底。
        /// </summary>
        public static IReadOnlyList<BattleEvent> Refill(BattleState state, IRandom rng, RuleSet rules)
        {
            var events = new List<BattleEvent>();
            int drawn = 0;
            while (state.Hand.Count < RuleSet.HandSize)
            {
                if (state.Deck.Count == 0)
                {
                    if (state.Discard.Count == 0) break;
                    int moved = state.Discard.Count;
                    state.Deck.AddRange(state.Discard);
                    state.Discard.Clear();
                    Shuffle(state.Deck, rng);
                    events.Add(new BattleEvent(EventKind.Reshuffled, "deck", 0, moved, state.Deck.Count));
                }
                int top = state.Deck.Count - 1;
                state.Hand.Add(state.Deck[top]);
                state.Deck.RemoveAt(top);
                drawn++;
            }
            events.Add(new BattleEvent(EventKind.Refilled, "deck", 0, drawn, state.Hand.Count));
            return events;
        }

        /// <summary>
        /// 調度：把手牌中的一張洗回牌庫，再抽一張（可能抽回原牌）。不套用物資保底，牌的總數不變。
        /// 次數與時機限制由 <see cref="BattleController"/> 管理。
        /// </summary>
        public static bool TryRedraw(BattleState state, int cardId, IRandom rng, out int drawnCardId)
        {
            drawnCardId = -1;
            int index = state.Hand.IndexOf(cardId);
            if (index < 0) return false;

            state.Hand.RemoveAt(index);
            state.Deck.Add(cardId);
            Shuffle(state.Deck, rng);
            int top = state.Deck.Count - 1;
            drawnCardId = state.Deck[top];
            state.Deck.RemoveAt(top);
            state.Hand.Insert(index, drawnCardId);
            return true;
        }
    }
}
