using System;
using Shepherd.Battle.Core;

namespace Shepherd.Battle.Data
{
    /// <summary>把 BattleConfig（ScriptableObject）編譯成規則核心使用的唯讀 RuleSet。</summary>
    public static class RuleSetCompiler
    {
        public static RuleSet Compile(BattleConfig c)
        {
            if (c == null) throw new ArgumentNullException("c");
            var p = new RuleParams
            {
                StartAnchor = c.StartAnchor,
                MaxAnchor = c.MaxAnchor,
                LinkCap = c.LinkCap,
                LinkDecayPerRound = c.LinkDecayPerRound,
                RedrawPerRound = c.RedrawPerRound,

                DeckEmber = c.DeckEmber,
                DeckBase = c.DeckBase,
                DeckBinder = c.DeckBinder,

                // float → double 以十進位字串轉換，避免 1.5f 之類的值產生二進位誤差。
                TripleMultiplier = ToDouble(c.TripleMultiplier),
                ResidueEmberBonus = c.ResidueEmberBonus,
                ResidueBaseShield = c.ResidueBaseShield,
                ResidueBinderLink = c.ResidueBinderLink,

                StructureEmberPressure = c.StructureEmberPressure,
                StructureConvertBase = c.StructureConvertBase,
                StructureConvertPerLink = c.StructureConvertPerLink,
                StructureBaseShield = c.StructureBaseShield,
                StructureBinderShield = c.StructureBinderShield,
                StructureBinderThreatBonus = c.StructureBinderThreatBonus,
                StructureBinderLink = c.StructureBinderLink,
                StructureResponsePressure = c.StructureResponsePressure,

                ProcessEmberCorrection = c.ProcessEmberCorrection,
                ProcessBaseCorrection = c.ProcessBaseCorrection,
                ProcessBinderCorrection = c.ProcessBinderCorrection,
                ProcessBaseShield = c.ProcessBaseShield,
                ProcessBinderHeal = c.ProcessBinderHeal,
                ProcessBinderLink = c.ProcessBinderLink,
                ProcessFeedbackHeal = c.ProcessFeedbackHeal,

                RelationEmberPressure = c.RelationEmberPressure,
                RelationSeverMaxLayers = c.RelationSeverMaxLayers,
                RelationSeverBonusPerLayer = c.RelationSeverBonusPerLayer,
                RelationBaseLink = c.RelationBaseLink,
                RelationBaseBind = c.RelationBaseBind,
                RelationBinderLink = c.RelationBinderLink,
                ConductionPercent = c.ConductionPercent,
                DivertPointsPerLayer = c.DivertPointsPerLayer,
                DivertBaseMaxLayers = c.DivertBaseMaxLayers,
                DivertReturnRatio = ToDouble(c.DivertReturnRatio),
            };
            return new RuleSet(p);
        }

        private static double ToDouble(float f)
        {
            return double.Parse(f.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
