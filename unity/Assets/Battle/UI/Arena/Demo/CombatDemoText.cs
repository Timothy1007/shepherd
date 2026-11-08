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

        // ---- 首戰教學（四個節點，每節只說當下要做的一件事） ----
        public const string TutorialTitle = "教學 {0}／4・{1}";
        public const string TutorialContinue = "繼續";
        public const string TutorialSkip = "跳過教學";
        public const string TutorialClose = "關閉";
        public const string TutorialReplay = "？教學";
        public const string TutorialFreeHint = "可以直接操作，不必照順序。";
        public const string T1Name = "看清威脅";
        public const string T1Body = "這是兩個失序體。名牌上的「失序值」歸零就會解除；「下回合攻擊」是它下一回合會打你的量。\n點名牌或敵人本身，可以選定目標。";
        public const string T2Name = "資源與學派";
        public const string T2Body = "每回合有 3 AP，每張牌 1 AP，所以一回合最多排 3 張。\n右上的學派會改變同一元素的效果──切換看看，手牌上的效果會跟著變。學派在確認施放時才鎖定。";
        public const string T2Done = "牌面已依新學派更新。哪個學派都可以，沒有固定答案。";
        public const string T3Name = "編排術式";
        public const string T3Body = "點一張手牌，它會放進第 1 槽。需要目標的牌會指向目前的目標（槽位上的「目標」可以更換）。\n下方的預估就是實際會結算的結果。";
        public const string T3More = "順序有差：後一張會「承接」前一張的餘波；三張同元素會觸發「共鳴」。第一次不必湊齊，想好就好。";
        public const string T4Name = "確認施放";
        public const string T4Body = "準備好了就按「確認施放」。結算會照你排的順序逐張發生，接著是失序體的回合。\n錨定值是你的生命；擬態錨定會先吸收傷害。";
        public const string TutorialDoneTitle = "教學完成";
        public const string TutorialDone ="教學完成。之後每一回合由你判斷──先看敵人的下回合攻擊，再決定攻守。";
        public const string NoteResidue = "承接生效：前一張牌的餘波影響了下一張。";
        public const string NoteResonance = "共鳴生效：三張同元素，主要數值放大。";
        public const string NoteEnemy = "失序體行動了：攻擊先打在擬態錨定，剩下的才扣錨定值。";

        // ---- 手牌 ----
        public const string CardCost = "1";
        public const string Staged = "第 {0} 張";
        public const string DetailTargetHint = "點敵人或敵人名牌可更換";
    }
}
