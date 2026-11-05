using UnityEngine;

namespace Shepherd.Battle.Data
{
    /// <summary>
    /// 所有可調數值的唯一來源（規格第五節）。規則核心只讀取由 <see cref="RuleSetCompiler"/> 編譯出的唯讀 RuleSet。
    /// 預設值即整合測試版本，全部為暫定。
    /// </summary>
    [CreateAssetMenu(menuName = "Shepherd/Battle Config", fileName = "BattleConfig")]
    public sealed class BattleConfig : ScriptableObject
    {
        [Header("玩家與回合")]
        public int StartAnchor = 30;
        public int MaxAnchor = 40;
        public int LinkCap = 4;
        public int LinkDecayPerRound = 1;
        public int RedrawPerRound = 1;

        [Header("牌庫")]
        public int DeckEmber = 9;
        public int DeckBase = 9;
        public int DeckBinder = 9;

        [Header("承接與三連")]
        public float TripleMultiplier = 1.5f;
        public int ResidueEmberBonus = 1;
        public int ResidueBaseShield = 2;
        public int ResidueBinderLink = 1;

        [Header("實在論")]
        public int StructureEmberPressure = 4;
        public int StructureConvertBase = 4;
        public int StructureConvertPerLink = 2;
        public int StructureBaseShield = 7;
        public int StructureBinderShield = 3;
        public int StructureBinderThreatBonus = 2;
        public int StructureBinderLink = 1;
        public int StructureResponsePressure = 3;

        [Header("過程哲學")]
        public int ProcessEmberCorrection = 2;
        public int ProcessBaseCorrection = 2;
        public int ProcessBinderCorrection = 1;
        public int ProcessBaseShield = 3;
        public int ProcessBinderHeal = 2;
        public int ProcessBinderLink = 1;
        public int ProcessFeedbackHeal = 1;

        [Header("關係論")]
        public int RelationEmberPressure = 3;
        public int RelationSeverMaxLayers = 2;
        public int RelationSeverBonusPerLayer = 2;
        public int RelationBaseLink = 1;
        public int RelationBaseBind = 1;
        public int RelationBinderLink = 2;
        public int ConductionPercent = 50;
        public int DivertPointsPerLayer = 3;
        public int DivertBaseMaxLayers = 2;
        public float DivertReturnRatio = 1.0f;
    }
}
