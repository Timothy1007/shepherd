namespace Shepherd.Battle.Core
{
    /// <summary>學派（暫稱）：Structure＝實在論、Process＝過程哲學、Relation＝關係論。</summary>
    public enum SchoolKind { Structure, Process, Relation }

    /// <summary>元素：Ember＝燃素、Base＝基體、Binder＝擬合劑。</summary>
    public enum ElementKind { Ember, Base, Binder }

    public enum BattleOutcome { Ongoing, Victory, Defeat }

    /// <summary>效果指令（規格第六節「效果資料與解譯」）。</summary>
    public enum EffectOp
    {
        /// <summary>實在論．燃素的轉化：消耗 min(擬態錨定, 基數 + 每層 × 目標連結)。</summary>
        Convert,
        /// <summary>關係論．燃素的斷連：消耗目標 min(連結, 上限) 層。</summary>
        Sever,
        /// <summary>壓制：數量為 V ＋ 本張牌的轉化量 ＋ 斷連追加。</summary>
        Pressure,
        Shield,
        /// <summary>適配條件：本回合預告攻擊總量大於當前擬態錨定時再加擬態錨定。</summary>
        ThreatShield,
        Correction,
        Stabilize,
        Heal,
        Link,
        Bind
    }

    /// <summary>效果指令的數量來源。</summary>
    public enum AmountSource { Primary, Constant }

    public enum EventKind
    {
        CardResolved,
        CardFizzled,
        Pressure,
        Conduction,
        ShieldChanged,
        AnchorChanged,
        CorrectionChanged,
        LinkChanged,
        BindChanged,
        EnemyResolved,
        EnemyActed,
        Redrawn,
        Refilled,
        Reshuffled,
        Victory,
        Defeat,
        /// <summary>三連（共鳴）觸發：Amount＝元素，Value＝張數。只記錄，不改變任何數值。</summary>
        Resonance
    }
}
