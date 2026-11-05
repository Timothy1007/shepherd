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

    /// <summary>序列中一張牌本身造成的結果（依 BattleEvent 的來源 "card:i" 彙整，含其引發的傳導）。</summary>
    public sealed class CardOutcome
    {
        public int Index;
        public ElementKind Element;
        public bool Fizzled;
        /// <summary>核心結算時使用的主要數值 V（CardResolved 事件；已含共鳴倍率與反應）。</summary>
        public int PrimaryValue;
        public int TargetId;
        /// <summary>各敵人受到的實際壓制（含傳導）。</summary>
        public readonly Dictionary<int, int> Damage = new Dictionary<int, int>();
        public readonly Dictionary<int, int> Links = new Dictionary<int, int>();
        public readonly Dictionary<int, int> Corrections = new Dictionary<int, int>();
        public readonly Dictionary<int, int> Bind = new Dictionary<int, int>();
        public int ShieldGain;
        public int ShieldSpent;
        public int Heal;
    }

    /// <summary>第 Index 張牌從前一張承接到的附帶結果（BattleEvent 來源 "residue:i"）。</summary>
    public sealed class ResidueOutcome
    {
        /// <summary>接受承接的牌（0 起算，至少為 1）。</summary>
        public int Index;
        public ElementKind From;
        public int Shield;
        public int LinkEnemyId;
        public int Links;
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

        /// <summary>序列中每張牌的結果（依出牌順序）。</summary>
        public readonly List<CardOutcome> Cards = new List<CardOutcome>();
        /// <summary>承接附帶的結果（只有第 2 張以後才有）。</summary>
        public readonly List<ResidueOutcome> Residues = new List<ResidueOutcome>();
        /// <summary>核心是否發出共鳴事件。</summary>
        public bool Resonance;
        public ElementKind ResonanceElement;
        /// <summary>持續校正階段對各敵人的實際壓制（含傳導）。</summary>
        public readonly Dictionary<int, int> CorrectionDamage = new Dictionary<int, int>();
        /// <summary>持續校正階段（回饋）回復的錨定值。</summary>
        public int CorrectionHeal;

        public CardOutcome Card(int index)
        {
            foreach (var c in Cards) if (c.Index == index) return c;
            return null;
        }

        public ResidueOutcome Residue(int index)
        {
            foreach (var r in Residues) if (r.Index == index) return r;
            return null;
        }

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
            ParseSections(f, result.Events);
            return f;
        }

        /// <summary>
        /// 依事件來源與順序把結算分段：每張牌（card:i，含其引發的傳導）、承接（residue:i）、持續校正。
        /// 只是彙整事件，不重算任何規則。
        /// </summary>
        private static void ParseSections(RoundForecast f, IReadOnlyList<BattleEvent> events)
        {
            CardOutcome card = null;
            ResidueOutcome res = null;
            int section = 0;
            foreach (var ev in events)
            {
                string src = ev.Source ?? "";
                int idx;
                if (ev.Kind == EventKind.Resonance)
                {
                    f.Resonance = true;
                    f.ResonanceElement = (ElementKind)ev.Amount;
                    continue;
                }
                if (src.StartsWith("card:") && int.TryParse(src.Substring(5), out idx))
                {
                    card = f.Card(idx);
                    if (card == null) f.Cards.Add(card = new CardOutcome { Index = idx });
                    section = 1;
                }
                else if (src.StartsWith("residue:") && int.TryParse(src.Substring(8), out idx))
                {
                    res = f.Residue(idx);
                    if (res == null) f.Residues.Add(res = new ResidueOutcome { Index = idx });
                    section = 2;
                }
                else if (src == "phase:Correction" || src == "passive:Feedback" || src == "passive:Continuation") section = 3;
                else if (src.StartsWith("enemy:") || src == "passive:Divert" || src == "passive:Response" || src == "phase:RoundEnd") section = 4;

                if (section == 1) ApplyCard(card, ev);
                else if (section == 2) ApplyResidue(res, ev);
                else if (section == 3)
                {
                    if (ev.Kind == EventKind.Pressure || ev.Kind == EventKind.Conduction) Add(f.CorrectionDamage, ev.EnemyId, ev.Value);
                    else if (ev.Kind == EventKind.AnchorChanged && ev.Amount > 0) f.CorrectionHeal += ev.Amount;
                }
            }

            // 每一對相鄰的已結算牌都有承接關係（即使這次沒有產生數值變化）。
            foreach (var c in f.Cards)
            {
                if (c.Index < 1) continue;
                var prev = f.Card(c.Index - 1);
                if (prev == null) continue;
                var r = f.Residue(c.Index);
                if (r == null) f.Residues.Add(r = new ResidueOutcome { Index = c.Index });
                r.From = prev.Element;
            }
            f.Cards.Sort((a, b) => a.Index.CompareTo(b.Index));
            f.Residues.Sort((a, b) => a.Index.CompareTo(b.Index));
        }

        private static void Add(Dictionary<int, int> d, int key, int amount)
        {
            int v;
            d.TryGetValue(key, out v);
            d[key] = v + amount;
        }

        private static void ApplyCard(CardOutcome c, BattleEvent ev)
        {
            switch (ev.Kind)
            {
                case EventKind.CardResolved:
                case EventKind.CardFizzled:
                    c.Element = (ElementKind)ev.Amount;
                    c.PrimaryValue = ev.Value;
                    c.TargetId = ev.EnemyId;
                    c.Fizzled = ev.Kind == EventKind.CardFizzled;
                    break;
                case EventKind.Pressure:
                case EventKind.Conduction:
                    Add(c.Damage, ev.EnemyId, ev.Value);
                    break;
                case EventKind.ShieldChanged:
                    if (ev.Amount > 0) c.ShieldGain += ev.Amount;
                    else c.ShieldSpent += -ev.Amount;
                    break;
                case EventKind.AnchorChanged:
                    if (ev.Amount > 0) c.Heal += ev.Amount;
                    break;
                case EventKind.LinkChanged:
                    Add(c.Links, ev.EnemyId, ev.Amount);
                    break;
                case EventKind.CorrectionChanged:
                    Add(c.Corrections, ev.EnemyId, ev.Amount);
                    break;
                case EventKind.BindChanged:
                    Add(c.Bind, ev.EnemyId, ev.Amount);
                    break;
            }
        }

        private static void ApplyResidue(ResidueOutcome r, BattleEvent ev)
        {
            if (ev.Kind == EventKind.ShieldChanged) r.Shield += ev.Amount;
            else if (ev.Kind == EventKind.LinkChanged)
            {
                r.LinkEnemyId = ev.EnemyId;
                r.Links += ev.Amount;
            }
        }
    }


    /// <summary>序列階段（A 段：所有牌與承接）的合計結果，依事件彙整。</summary>
    public sealed class SequenceSummary
    {
        /// <summary>各敵人受到的實際壓制（含傳導）。</summary>
        public readonly Dictionary<int, int> Damage = new Dictionary<int, int>();
        /// <summary>擬態錨定淨變化（增加減去轉化消耗）。</summary>
        public int Shield;
        public int Heal;
        public readonly Dictionary<int, int> Links = new Dictionary<int, int>();
        public readonly Dictionary<int, int> Corrections = new Dictionary<int, int>();

        public bool IsEmpty
        {
            get
            {
                if (Shield != 0 || Heal != 0) return false;
                foreach (var v in Damage.Values) if (v != 0) return false;
                foreach (var v in Links.Values) if (v != 0) return false;
                foreach (var v in Corrections.Values) if (v != 0) return false;
                return true;
            }
        }

        private static void Add(Dictionary<int, int> d, int key, int amount)
        {
            if (amount == 0) return;
            int v;
            d.TryGetValue(key, out v);
            d[key] = v + amount;
        }

        public static SequenceSummary From(RoundForecast f)
        {
            var s = new SequenceSummary();
            foreach (var c in f.Cards)
            {
                foreach (var kv in c.Damage) Add(s.Damage, kv.Key, kv.Value);
                foreach (var kv in c.Links) Add(s.Links, kv.Key, kv.Value);
                foreach (var kv in c.Corrections) Add(s.Corrections, kv.Key, kv.Value);
                s.Shield += c.ShieldGain - c.ShieldSpent;
                s.Heal += c.Heal;
            }
            foreach (var r in f.Residues)
            {
                s.Shield += r.Shield;
                if (r.Links != 0) Add(s.Links, r.LinkEnemyId, r.Links);
            }
            return s;
        }

        /// <summary>a − b（只保留非零差異）。</summary>
        public static SequenceSummary Diff(SequenceSummary a, SequenceSummary b)
        {
            var s = new SequenceSummary { Shield = a.Shield - b.Shield, Heal = a.Heal - b.Heal };
            DiffInto(s.Damage, a.Damage, b.Damage);
            DiffInto(s.Links, a.Links, b.Links);
            DiffInto(s.Corrections, a.Corrections, b.Corrections);
            return s;
        }

        private static void DiffInto(Dictionary<int, int> into, Dictionary<int, int> a, Dictionary<int, int> b)
        {
            foreach (var kv in a)
            {
                int o;
                b.TryGetValue(kv.Key, out o);
                Add(into, kv.Key, kv.Value - o);
            }
            foreach (var kv in b) if (!a.ContainsKey(kv.Key)) Add(into, kv.Key, -kv.Value);
        }
    }

    /// <summary>
    /// 把序列的結果拆成「卡牌基礎效果」「承接收益」「共鳴收益」。
    /// 核心事件不會單獨標出承接的反應（+1）與共鳴倍率（×1.5）各占主要數值多少，
    /// 所以以同一個 RoundResolver 對同一狀態與編排做反事實結算：
    /// 基礎＝承接與共鳴參數歸零；承接收益＝只關閉共鳴 − 基礎；共鳴收益＝實際 − 只關閉共鳴。
    /// 除了規則參數之外不做任何計算。
    /// </summary>
    public sealed class ComboGain
    {
        public SequenceSummary Base;
        public SequenceSummary Residue;
        public SequenceSummary Resonance;
        /// <summary>序列中有相鄰的牌（承接成立）。</summary>
        public bool HasResidue;
        /// <summary>核心發出了共鳴事件。</summary>
        public bool HasResonance;

        public static ComboGain From(RoundForecast none, RoundForecast residueOnly, RoundForecast actual)
        {
            var sNone = SequenceSummary.From(none);
            var sResidue = SequenceSummary.From(residueOnly);
            var sActual = SequenceSummary.From(actual);
            return new ComboGain
            {
                Base = sNone,
                Residue = SequenceSummary.Diff(sResidue, sNone),
                Resonance = SequenceSummary.Diff(sActual, sResidue),
                HasResidue = actual.Residues.Count > 0,
                HasResonance = actual.Resonance,
            };
        }

        /// <summary>只有一張牌或沒有組合時：基礎效果就是實際結果。</summary>
        public static ComboGain Plain(RoundForecast actual)
        {
            return new ComboGain
            {
                Base = SequenceSummary.From(actual),
                Residue = new SequenceSummary(),
                Resonance = new SequenceSummary(),
            };
        }
    }
}