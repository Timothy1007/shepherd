namespace Shepherd.Battle.UI.Arena
{
    /// <summary>
    /// Demo 戰鬥 HUD 與首戰教學的文案（集中管理，不散落在畫面程式）。
    /// 術語（燃素／基體／擬合劑、實在論／過程哲學／關係論、錨定值、擬態錨定、失序值）沿用 Unity v0.1 暫稱，名稱本身取自 CombatLabText。
    /// </summary>
    public static class CombatDemoText
    {
        // ---- 玩家 ----
        public const string AnchorName = "錨定值";
        public const string AnchorHint = "（生命）";
        public const string ShieldName = "擬態錨定";
        public const string ShieldHint = "先吸收傷害";
        public const string ThreatName = "敵方回合";
        public const string Estimate = "預估";
        public const string Round = "第 {0} 回合";
        public const string Resolving = "結算中…";

        // ---- AP ----
        public const string Ap = "AP";
        public const string ApHint = "每張 1 AP，最多 3 張";

        // ---- 學派 ----
        public const string SchoolHeader = "本回合學派";
        public const string SchoolUnlocked = "確認施放時鎖定";
        public const string SchoolLocked = "本回合已鎖定";

        // ---- 敵人 ----
        public const string Target = "目標";
        public const string DisorderValue = "失序值";
        public const string Intent = "下回合攻擊";
        public const string Resolved = "已解除";
        public const string Links = "連結";
        public const string Corrections = "校正";
        public const string Bind = "束縛";

        // ---- 術式 ----
        public const string SequenceTitle = "術式編排";
        public const string SequenceHint = "點手牌加入；點槽位移除；點目標可更換";
        public const string EmptySlot = "空槽";
        public const string TargetChip = "目標：{0}　⇄";
        public const string NoTarget = "不需目標";
        public const string ResidueLink = "承接";
        public const string ResonanceReady = "共鳴成立：{0}×3（主要數值×{1}）";
        public const string ResonanceNear = "再一張{0}即成三連共鳴";
        public const string ResonanceRule = "三張同元素 → 共鳴";
        public const string YouGet = "你將得到";
        public const string TargetsGet = "目標將受到";
        public const string AfterRound = "回合後";
        public const string NothingPlanned = "沒有編排：直接結束回合";
        public const string Nothing = "無";

        // ---- 按鈕 ----
        public const string Commit = "確認施放（{0}）";
        public const string EndTurn = "結束回合（不施放）";
        public const string Skip = "略過動畫";
        public const string Over = "戰鬥結束";
        public const string Redraw = "調度（剩 {0}）";
        public const string RedrawCancel = "取消調度";
        public const string RedrawHint = "調度中：點一張手牌洗回並重抽";
        public const string Clear = "清空";
        public const string Log = "戰鬥紀錄";
        public const string QaReset = "重置戰鬥（QA）";

        // ---- 手牌 ----
        public const string CardCost = "1";
        public const string Staged = "第 {0} 張";
        public const string DetailTargetHint = "點敵人或敵人名牌可更換";
    }
}
