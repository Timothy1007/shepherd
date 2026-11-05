using System;
using System.Collections.Generic;

namespace Shepherd.Battle.Core
{
    /// <summary>
    /// 可調數值（規格第五節）。預設值即整合測試版本，全部為暫定。
    /// 這是可變的設定載體；規則核心只讀取由它建立的唯讀 <see cref="RuleSet"/>。
    /// </summary>
    public sealed class RuleParams
    {
        // 玩家與回合
        public int StartAnchor = 30;
        public int MaxAnchor = 40;
        public int LinkCap = 4;
        public int LinkDecayPerRound = 1;
        public int RedrawPerRound = 1;

        // 牌庫
        public int DeckEmber = 9;
        public int DeckBase = 9;
        public int DeckBinder = 9;

        // 承接與三連
        public double TripleMultiplier = 1.5;
        public int ResidueEmberBonus = 1;
        public int ResidueBaseShield = 2;
        public int ResidueBinderLink = 1;

        // 實在論
        public int StructureEmberPressure = 4;
        public int StructureConvertBase = 4;
        public int StructureConvertPerLink = 2;
        public int StructureBaseShield = 7;
        public int StructureBinderShield = 3;
        public int StructureBinderThreatBonus = 2;
        /// <summary>適配 ③ 連結 +1（規格第四節效果表的固定值）。</summary>
        public int StructureBinderLink = 1;
        public int StructureResponsePressure = 3;

        // 過程哲學
        public int ProcessEmberCorrection = 2;
        public int ProcessBaseCorrection = 2;
        public int ProcessBinderCorrection = 1;
        public int ProcessBaseShield = 3;
        public int ProcessBinderHeal = 2;
        /// <summary>轉移 ③ 連結 +1（規格第四節效果表的固定值）。</summary>
        public int ProcessBinderLink = 1;
        public int ProcessFeedbackHeal = 1;

        // 關係論
        public int RelationEmberPressure = 3;
        public int RelationSeverMaxLayers = 2;
        public int RelationSeverBonusPerLayer = 2;
        public int RelationBaseLink = 1;
        public int RelationBaseBind = 1;
        public int RelationBinderLink = 2;
        public int ConductionPercent = 50;
        public int DivertPointsPerLayer = 3;
        public int DivertBaseMaxLayers = 2;
        public double DivertReturnRatio = 1.0;

        public RuleParams Clone()
        {
            return (RuleParams)MemberwiseClone();
        }
    }

    /// <summary>效果表中的一條指令。</summary>
    public sealed class EffectStep
    {
        public readonly EffectOp Op;
        public readonly bool EnemyDirected;
        public readonly AmountSource Source;
        public readonly int Constant;

        public EffectStep(EffectOp op, bool enemyDirected, AmountSource source, int constant)
        {
            Op = op;
            EnemyDirected = enemyDirected;
            Source = source;
            Constant = constant;
        }

        public static EffectStep Enemy(EffectOp op, AmountSource source = AmountSource.Primary, int constant = 0)
        {
            return new EffectStep(op, true, source, constant);
        }

        public static EffectStep Self(EffectOp op, AmountSource source = AmountSource.Primary, int constant = 0)
        {
            return new EffectStep(op, false, source, constant);
        }
    }

    /// <summary>
    /// 唯讀規則集。手牌 7 張、每回合 3 AP、每回合一次確認、依序結算為不可調（規格第五節）。
    /// </summary>
    public sealed class RuleSet
    {
        public const int HandSize = 7;
        public const int ActionPoints = 3;
        public const int TripleCount = 3;

        private readonly RuleParams p;
        private readonly Dictionary<int, IReadOnlyList<EffectStep>> table;
        private readonly Dictionary<int, int> primary;

        public RuleSet(RuleParams source)
        {
            if (source == null) throw new ArgumentNullException("source");
            p = source.Clone();
            Validate(p);
            table = BuildEffectTable(p);
            primary = BuildPrimaryTable(p);
        }

        public static RuleSet CreateDefault()
        {
            return new RuleSet(new RuleParams());
        }

        /// <summary>取得數值的唯讀複本（修改複本不影響此規則集）。</summary>
        public RuleParams Params { get { return p.Clone(); } }

        public int StartAnchor { get { return p.StartAnchor; } }
        public int MaxAnchor { get { return p.MaxAnchor; } }
        public int LinkCap { get { return p.LinkCap; } }
        public int LinkDecayPerRound { get { return p.LinkDecayPerRound; } }
        public int RedrawPerRound { get { return p.RedrawPerRound; } }
        public int DeckEmber { get { return p.DeckEmber; } }
        public int DeckBase { get { return p.DeckBase; } }
        public int DeckBinder { get { return p.DeckBinder; } }
        public double TripleMultiplier { get { return p.TripleMultiplier; } }
        public int ResidueEmberBonus { get { return p.ResidueEmberBonus; } }
        public int ResidueBaseShield { get { return p.ResidueBaseShield; } }
        public int ResidueBinderLink { get { return p.ResidueBinderLink; } }
        public int StructureConvertBase { get { return p.StructureConvertBase; } }
        public int StructureConvertPerLink { get { return p.StructureConvertPerLink; } }
        public int StructureResponsePressure { get { return p.StructureResponsePressure; } }
        public int ProcessFeedbackHeal { get { return p.ProcessFeedbackHeal; } }
        public int RelationSeverMaxLayers { get { return p.RelationSeverMaxLayers; } }
        public int RelationSeverBonusPerLayer { get { return p.RelationSeverBonusPerLayer; } }
        public int ConductionPercent { get { return p.ConductionPercent; } }
        public int DivertPointsPerLayer { get { return p.DivertPointsPerLayer; } }
        public int DivertBaseMaxLayers { get { return p.DivertBaseMaxLayers; } }
        public double DivertReturnRatio { get { return p.DivertReturnRatio; } }

        /// <summary>主要數值 V（三連倍率與承接反應只作用在這個數字上）。</summary>
        public int PrimaryValue(SchoolKind school, ElementKind element)
        {
            return primary[Key(school, element)];
        }

        public IReadOnlyList<EffectStep> Effects(SchoolKind school, ElementKind element)
        {
            return table[Key(school, element)];
        }

        /// <summary>這張牌在此學派下是否有敵方指向部分（需要指定目標）。</summary>
        public bool RequiresTarget(SchoolKind school, ElementKind element)
        {
            var steps = Effects(school, element);
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i].EnemyDirected) return true;
            }
            return false;
        }

        private static int Key(SchoolKind school, ElementKind element)
        {
            return (int)school * 10 + (int)element;
        }

        private static void Validate(RuleParams r)
        {
            if (r.MaxAnchor <= 0 || r.StartAnchor <= 0 || r.StartAnchor > r.MaxAnchor)
                throw new ArgumentException("StartAnchor/MaxAnchor 不合法");
            if (r.LinkCap < 0) throw new ArgumentException("LinkCap 不可為負");
            if (r.DeckEmber < 0 || r.DeckBase < 0 || r.DeckBinder < 0)
                throw new ArgumentException("牌庫張數不可為負");
            if (r.DeckEmber + r.DeckBase + r.DeckBinder < HandSize)
                throw new ArgumentException("牌庫總數不可少於手牌上限");
            if (r.TripleMultiplier < 0 || r.DivertReturnRatio < 0 || r.ConductionPercent < 0)
                throw new ArgumentException("倍率不可為負");
            if (r.DivertPointsPerLayer <= 0) throw new ArgumentException("DivertPointsPerLayer 必須大於 0");
        }

        private static Dictionary<int, int> BuildPrimaryTable(RuleParams r)
        {
            var d = new Dictionary<int, int>();
            d[Key(SchoolKind.Structure, ElementKind.Ember)] = r.StructureEmberPressure;
            d[Key(SchoolKind.Structure, ElementKind.Base)] = r.StructureBaseShield;
            d[Key(SchoolKind.Structure, ElementKind.Binder)] = r.StructureBinderShield;
            d[Key(SchoolKind.Process, ElementKind.Ember)] = r.ProcessEmberCorrection;
            d[Key(SchoolKind.Process, ElementKind.Base)] = r.ProcessBaseCorrection;
            d[Key(SchoolKind.Process, ElementKind.Binder)] = r.ProcessBinderCorrection;
            d[Key(SchoolKind.Relation, ElementKind.Ember)] = r.RelationEmberPressure;
            d[Key(SchoolKind.Relation, ElementKind.Base)] = r.RelationBaseLink;
            d[Key(SchoolKind.Relation, ElementKind.Binder)] = r.RelationBinderLink;
            return d;
        }

        /// <summary>規格第四節效果表；複合效果的內部順序固定。敵方指向屬性見第三節 A 段分類表。</summary>
        private static Dictionary<int, IReadOnlyList<EffectStep>> BuildEffectTable(RuleParams r)
        {
            var d = new Dictionary<int, IReadOnlyList<EffectStep>>();

            // 實在論．燃素（施壓）：① 轉化 → ② 壓制（V＋轉化量）。整體為敵方指向。
            d[Key(SchoolKind.Structure, ElementKind.Ember)] = new[]
            {
                EffectStep.Enemy(EffectOp.Convert),
                EffectStep.Enemy(EffectOp.Pressure),
            };
            // 實在論．基體（構形）：擬態錨定 +V。
            d[Key(SchoolKind.Structure, ElementKind.Base)] = new[]
            {
                EffectStep.Self(EffectOp.Shield),
            };
            // 實在論．擬合劑（適配）：① 擬態錨定 +V → ② 條件 +2 → ③ 連結 +1。
            d[Key(SchoolKind.Structure, ElementKind.Binder)] = new[]
            {
                EffectStep.Self(EffectOp.Shield),
                EffectStep.Self(EffectOp.ThreatShield, AmountSource.Constant, r.StructureBinderThreatBonus),
                EffectStep.Enemy(EffectOp.Link, AmountSource.Constant, r.StructureBinderLink),
            };
            // 過程哲學．燃素（啟動）：① 校正 +V → ② 壓制 V。
            d[Key(SchoolKind.Process, ElementKind.Ember)] = new[]
            {
                EffectStep.Enemy(EffectOp.Correction),
                EffectStep.Enemy(EffectOp.Pressure),
            };
            // 過程哲學．基體（穩定）：① 校正 +V → ② 本回合不衰減 → ③ 擬態錨定 +3。
            d[Key(SchoolKind.Process, ElementKind.Base)] = new[]
            {
                EffectStep.Enemy(EffectOp.Correction),
                EffectStep.Self(EffectOp.Stabilize),
                EffectStep.Self(EffectOp.Shield, AmountSource.Constant, r.ProcessBaseShield),
            };
            // 過程哲學．擬合劑（轉移）：① 校正 +V → ② 回復錨定值 2 → ③ 連結 +1。
            d[Key(SchoolKind.Process, ElementKind.Binder)] = new[]
            {
                EffectStep.Enemy(EffectOp.Correction),
                EffectStep.Self(EffectOp.Heal, AmountSource.Constant, r.ProcessBinderHeal),
                EffectStep.Enemy(EffectOp.Link, AmountSource.Constant, r.ProcessBinderLink),
            };
            // 關係論．燃素（激化）：① 斷連 → ② 壓制（V＋2×消耗層數）。
            d[Key(SchoolKind.Relation, ElementKind.Ember)] = new[]
            {
                EffectStep.Enemy(EffectOp.Sever),
                EffectStep.Enemy(EffectOp.Pressure),
            };
            // 關係論．基體（固化）：① 連結 +V → ② 束縛 +1。
            d[Key(SchoolKind.Relation, ElementKind.Base)] = new[]
            {
                EffectStep.Enemy(EffectOp.Link),
                EffectStep.Enemy(EffectOp.Bind, AmountSource.Constant, r.RelationBaseBind),
            };
            // 關係論．擬合劑（連結）：連結 +V。
            d[Key(SchoolKind.Relation, ElementKind.Binder)] = new[]
            {
                EffectStep.Enemy(EffectOp.Link),
            };
            return d;
        }
    }
}
