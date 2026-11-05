using System.Collections.Generic;
using Shepherd.Battle.Core;

namespace Shepherd.Battle.UI
{
    /// <summary>單一敵人在一次結算中的前後變化與它的攻擊對玩家造成的影響。</summary>
    public sealed class EnemyForecast
    {
        public int Id;
        public string Name;
        public int ValueBefore;
        public int ValueAfter;
        public int LinksBefore;
        public int LinksAfter;
        public int CorrectionsBefore;
        public int CorrectionsAfter;
        public bool ResolvedBefore;
        public bool ResolvedAfter;

        /// <summary>敵方回合中是否輪到它行動（行動前已解除或戰鬥已結束則為 false）。</summary>
        public bool Acted;
        /// <summary>預告攻擊值（EnemyActed 事件）。</summary>
        public int Attack;
        /// <summary>被擬態錨定吸收的量。</summary>
        public int ShieldAbsorbed;
        /// <summary>扣掉的錨定值。</summary>
        public int AnchorLoss;
        /// <summary>沒有打到擬態錨定或錨定值的量（關係論分流）。</summary>
        public int Diverted;
        /// <summary>實在論回應對它造成的實際壓制。</summary>
        public int Counter;
    }

    /// <summary>
    /// 把一次結算（預覽或正式確認）整理成「前 → 後」的摘要，供介面直接顯示。
    /// 只讀取 RoundResolver 產生的新狀態與 BattleEvent，不自行計算任何規則。
    /// </summary>
    public sealed class RoundForecast
    {
        public BattleOutcome Outcome;
        public int AnchorBefore;
        public int AnchorAfter;
        public int ShieldBefore;
        public int ShieldAfter;
        public readonly List<EnemyForecast> Enemies = new List<EnemyForecast>();
        public IReadOnlyList<BattleEvent> Events = new BattleEvent[0];

        public int AnchorDelta { get { return AnchorAfter - AnchorBefore; } }
        public int ShieldDelta { get { return ShieldAfter - ShieldBefore; } }

        public int TotalAttack { get { return Sum(e => e.Acted ? e.Attack : 0); } }
        public int TotalShieldAbsorbed { get { return Sum(e => e.ShieldAbsorbed); } }
        public int TotalAnchorLoss { get { return Sum(e => e.AnchorLoss); } }

        public EnemyForecast Enemy(int id)
        {
            foreach (var e in Enemies)
            {
                if (e.Id == id) return e;
            }
            return null;
        }

        private int Sum(System.Func<EnemyForecast, int> f)
        {
            int n = 0;
            foreach (var e in Enemies) n += f(e);
            return n;
        }

        /// <summary>由結算前的狀態與結算結果建立摘要；result 為 null（驗證失敗）時回傳 null。</summary>
        public static RoundForecast From(BattleState before, ResolveResult result)
        {
            if (before == null || result == null) return null;
            var after = result.NewState;
            var f = new RoundForecast
            {
                Outcome = result.Outcome,
                AnchorBefore = before.Anchor,
                AnchorAfter = after.Anchor,
                ShieldBefore = before.Shield,
                ShieldAfter = after.Shield,
                Events = result.Events,
            };

            foreach (var b in before.Enemies)
            {
                var a = after.FindEnemy(b.Id) ?? b;
                f.Enemies.Add(new EnemyForecast
                {
                    Id = b.Id,
                    Name = b.Name,
                    ValueBefore = b.Value,
                    ValueAfter = a.Value,
                    LinksBefore = b.Links,
                    LinksAfter = a.Links,
                    CorrectionsBefore = b.Corrections,
                    CorrectionsAfter = a.Corrections,
                    ResolvedBefore = b.Resolved,
                    ResolvedAfter = a.Resolved,
                });
            }

            foreach (var ev in result.Events)
            {
                var e = f.Enemy(ev.EnemyId);
                string src = ev.Source ?? "";
                int sourceEnemy;
                if (src.StartsWith("enemy:") && int.TryParse(src.Substring(6), out sourceEnemy))
                {
                    var attacker = f.Enemy(sourceEnemy);
                    if (attacker == null) continue;
                    switch (ev.Kind)
                    {
                        case EventKind.EnemyActed:
                            attacker.Acted = true;
                            attacker.Attack = ev.Amount;
                            break;
                        case EventKind.ShieldChanged:
                            if (ev.Amount < 0) attacker.ShieldAbsorbed += -ev.Amount;
                            break;
                        case EventKind.AnchorChanged:
                            if (ev.Amount < 0) attacker.AnchorLoss += -ev.Amount;
                            break;
                    }
                    continue;
                }
                if (e != null && ev.Kind == EventKind.Pressure && src == "passive:Response") e.Counter += ev.Value;
            }

            foreach (var e in f.Enemies)
            {
                if (e.Acted) e.Diverted = System.Math.Max(0, e.Attack - e.ShieldAbsorbed - e.AnchorLoss);
            }
            return f;
        }
    }
}
