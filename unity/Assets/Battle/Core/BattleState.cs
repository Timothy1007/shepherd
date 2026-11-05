using System;
using System.Collections.Generic;
using System.Text;

namespace Shepherd.Battle.Core
{
    /// <summary>一張元素牌（v0.1 牌庫只有元素牌）。</summary>
    public sealed class CardDef
    {
        public readonly int Id;
        public readonly ElementKind Element;

        public CardDef(int id, ElementKind element)
        {
            Id = id;
            Element = element;
        }
    }

    public sealed class EnemyState
    {
        /// <summary>敵人編號（1 起算，E1＝1）。結算依編號順序。</summary>
        public int Id;
        public string Name;
        /// <summary>失序值。</summary>
        public int Value;
        /// <summary>攻擊序列；第 r 回合的攻擊值為 AttackPattern[r % 長度]。</summary>
        public int[] AttackPattern = new int[0];
        public int Links;
        public int Corrections;
        public int Bind;
        public bool Resolved;

        public bool Alive { get { return !Resolved; } }

        public int AttackForRound(int round)
        {
            if (AttackPattern == null || AttackPattern.Length == 0) return 0;
            int i = round % AttackPattern.Length;
            if (i < 0) i += AttackPattern.Length;
            return AttackPattern[i];
        }

        public EnemyState Clone()
        {
            var e = (EnemyState)MemberwiseClone();
            e.AttackPattern = AttackPattern == null ? new int[0] : (int[])AttackPattern.Clone();
            return e;
        }
    }

    /// <summary>戰鬥狀態。Clone 為深複製；規則核心不修改輸入狀態。</summary>
    public sealed class BattleState
    {
        public int Round;
        /// <summary>錨定值（玩家生命值）。</summary>
        public int Anchor;
        /// <summary>擬態錨定（護盾）。</summary>
        public int Shield;
        public BattleOutcome Outcome = BattleOutcome.Ongoing;

        public List<EnemyState> Enemies = new List<EnemyState>();

        /// <summary>所有牌的定義，以 Id 為索引。</summary>
        public List<CardDef> Cards = new List<CardDef>();
        /// <summary>牌庫，最後一個元素為頂端。</summary>
        public List<int> Deck = new List<int>();
        public List<int> Hand = new List<int>();
        public List<int> Discard = new List<int>();

        public BattleState Clone()
        {
            var s = new BattleState();
            s.Round = Round;
            s.Anchor = Anchor;
            s.Shield = Shield;
            s.Outcome = Outcome;
            for (int i = 0; i < Enemies.Count; i++) s.Enemies.Add(Enemies[i].Clone());
            s.Cards = new List<CardDef>(Cards);
            s.Deck = new List<int>(Deck);
            s.Hand = new List<int>(Hand);
            s.Discard = new List<int>(Discard);
            return s;
        }

        public CardDef Card(int id)
        {
            for (int i = 0; i < Cards.Count; i++)
            {
                if (Cards[i].Id == id) return Cards[i];
            }
            throw new ArgumentException("找不到牌 " + id);
        }

        public EnemyState FindEnemy(int id)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                if (Enemies[i].Id == id) return Enemies[i];
            }
            return null;
        }

        public bool AllEnemiesResolved()
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                if (!Enemies[i].Resolved) return false;
            }
            return true;
        }

        public int TotalCards { get { return Deck.Count + Hand.Count + Discard.Count; } }

        /// <summary>完整、確定性的文字描述，用於比對兩個狀態是否相同。</summary>
        public string Fingerprint()
        {
            var sb = new StringBuilder();
            sb.Append("R").Append(Round)
              .Append(" A").Append(Anchor)
              .Append(" S").Append(Shield)
              .Append(" O").Append(Outcome);
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                sb.Append(" |E").Append(e.Id)
                  .Append(" v").Append(e.Value)
                  .Append(" l").Append(e.Links)
                  .Append(" c").Append(e.Corrections)
                  .Append(" b").Append(e.Bind)
                  .Append(e.Resolved ? " X" : " -");
            }
            sb.Append(" |D[").Append(Join(Deck)).Append("]");
            sb.Append(" H[").Append(Join(Hand)).Append("]");
            sb.Append(" G[").Append(Join(Discard)).Append("]");
            return sb.ToString();
        }

        private static string Join(List<int> ids)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(ids[i]);
            }
            return sb.ToString();
        }
    }
}
