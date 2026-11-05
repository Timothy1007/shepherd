using System.Text;

namespace Shepherd.Battle.Core
{
    /// <summary>
    /// 結算事件。同時驅動預覽文字、動畫、紀錄匯出與測試斷言。
    /// Source 記錄來源，例如 "card:0"、"residue:1"、"passive:Conduction"、"phase:Correction"。
    /// </summary>
    public sealed class BattleEvent
    {
        public readonly EventKind Kind;
        public readonly string Source;
        /// <summary>相關敵人編號；與敵人無關時為 0。</summary>
        public readonly int EnemyId;
        /// <summary>主要數量（壓制為名義量；變化類事件為變化量）。</summary>
        public readonly int Amount;
        /// <summary>次要數量（壓制為實際減少量；變化類事件為變化後的值）。</summary>
        public readonly int Value;

        public BattleEvent(EventKind kind, string source, int enemyId, int amount, int value)
        {
            Kind = kind;
            Source = source;
            EnemyId = enemyId;
            Amount = amount;
            Value = value;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Kind).Append('@').Append(Source);
            if (EnemyId != 0) sb.Append(" E").Append(EnemyId);
            sb.Append(' ').Append(Amount).Append('/').Append(Value);
            return sb.ToString();
        }

        public override bool Equals(object obj)
        {
            var o = obj as BattleEvent;
            if (o == null) return false;
            return Kind == o.Kind && Source == o.Source && EnemyId == o.EnemyId
                && Amount == o.Amount && Value == o.Value;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int h = (int)Kind;
                h = h * 31 + (Source == null ? 0 : Source.GetHashCode());
                h = h * 31 + EnemyId;
                h = h * 31 + Amount;
                h = h * 31 + Value;
                return h;
            }
        }
    }
}
