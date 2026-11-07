using System;
using System.Collections.Generic;

namespace Shepherd.Exploration.Case01
{
    /// <summary>〈第十七號門〉室內 QA 的遊玩階段。只能向前推進（見 <see cref="Case01Session.Advance"/>）。</summary>
    public enum Case01Stage
    {
        EnteredHome = 0,
        ObservedDining = 1,
        DiningContradiction = 2,
        StudyDiscovery = 3,
        WrongConnection = 4,
        MezzanineDiscovered = 5,
        AnomalyDefense = 6,
        Battle = 7,
        PostBattle = 8,
        LinkRestoration = 9,
        CoreStabilized = 10,
        Exited = 11,
    }

    /// <summary>任務書 §3 的旗標名稱（字串值即存檔／日誌使用的穩定 ID）。</summary>
    public static class Case01Flags
    {
        public const string TableTwo = "case01.table_two";
        public const string TableRewritten = "case01.table_rewritten";
        public const string PhotoObserved = "case01.photo_observed";
        public const string EllieStudyKnown = "case01.ellie_study_known";
        public const string MapObserved = "case01.map_observed";
        public const string OldCorrectionRecord = "case01.old_correction_record";
        public const string RouteMismatchSeen = "case01.route_mismatch_seen";
        public const string RouteAnchorSet = "case01.route_anchor_set";
        public const string BattleStarted = "case01.battle_started";
        public const string BattleWon = "case01.battle_won";
        public const string MissingLinkRestored = "case01.missing_link_restored";
        public const string CoreStabilized = "case01.core_stabilized";
        public const string EvidenceArchived = "case01.evidence_archived";

        // QA 內部補充旗標（任務書語意需要，但未列在 §3）。
        /// <summary>樓梯平台的書房門已依艾琳記號覆寫成功（S30，永久記錄於本次 QA）。</summary>
        public const string StairRouteRestored = "case01.stair_route_restored";
        /// <summary>異域反制已發生（S40，只發生一次）。</summary>
        public const string DefenseTriggered = "case01.defense_triggered";

        public static readonly string[] All =
        {
            TableTwo, TableRewritten, PhotoObserved, EllieStudyKnown, MapObserved, OldCorrectionRecord, RouteMismatchSeen,
            RouteAnchorSet, BattleStarted, BattleWon, MissingLinkRestored, CoreStabilized, EvidenceArchived,
        };
    }

    /// <summary>玩家可重讀的日誌條目。</summary>
    public sealed class JournalEntry
    {
        public string Id;
        public string Title;
        public string Text;
    }

    /// <summary>
    /// 本次 QA 的最小記錄器：旗標、階段、日誌與少量鍵值（house_state）。只存在記憶體，不寫磁碟（任務書：不做完整存檔）。
    /// 設定旗標、加入日誌都是冪等的。
    /// </summary>
    public sealed class Case01Session
    {
        public const string HouseStateKey = "house_state";
        public const string HouseNormal = "normal";
        public const string HouseTemporarilyStable = "temporarily_stable";

        private readonly HashSet<string> flags = new HashSet<string>();
        private readonly List<JournalEntry> journal = new List<JournalEntry>();
        private readonly Dictionary<string, string> values = new Dictionary<string, string>();
        private readonly Dictionary<string, int> flagSetCount = new Dictionary<string, int>();

        public Case01Stage Stage { get; private set; }
        public IList<JournalEntry> Journal { get { return journal.AsReadOnly(); } }

        /// <summary>旗標第一次被設定時觸發。</summary>
        public event Action<string> FlagSet;
        public event Action<Case01Stage> StageChanged;
        public event Action<JournalEntry> JournalAdded;

        public Case01Session()
        {
            Stage = Case01Stage.EnteredHome;
            values[HouseStateKey] = HouseNormal;
        }

        public bool Has(string flag) { return flags.Contains(flag); }

        /// <summary>設定旗標；已設定時不做任何事並回傳 false。</summary>
        public bool Set(string flag)
        {
            if (!flags.Add(flag)) return false;
            flagSetCount[flag] = 1;
            if (FlagSet != null) FlagSet(flag);
            return true;
        }

        /// <summary>旗標被設定的次數（冪等檢查用，永遠是 0 或 1）。</summary>
        public int TimesSet(string flag)
        {
            int n;
            return flagSetCount.TryGetValue(flag, out n) ? n : 0;
        }

        public IEnumerable<string> Flags { get { return flags; } }

        /// <summary>推進階段：只能往前；要求的階段不比目前晚時不變並回傳 false。</summary>
        public bool Advance(Case01Stage to)
        {
            if (to <= Stage) return false;
            Stage = to;
            if (StageChanged != null) StageChanged(to);
            return true;
        }

        public string Get(string key)
        {
            string v;
            return values.TryGetValue(key, out v) ? v : null;
        }

        public void SetValue(string key, string value) { values[key] = value; }

        private readonly List<string> evidence = new List<string>();

        /// <summary>本次 QA 收藏的實體證物（穩定 Evidence ID）。只存在記憶體：重新開始／重啟 Unity 後清空，尚無跨案件存檔。</summary>
        public IList<string> Evidence { get { return evidence.AsReadOnly(); } }

        public bool HasEvidence(string id) { return evidence.Contains(id); }

        /// <summary>收藏證物；同一 ID 只會加入一次（回傳 false 表示已收藏）。</summary>
        public bool CollectEvidence(string id)
        {
            if (evidence.Contains(id)) return false;
            evidence.Add(id);
            return true;
        }

        public bool HasJournal(string id)
        {
            foreach (var e in journal) if (e.Id == id) return true;
            return false;
        }

        public JournalEntry FindJournal(string id)
        {
            foreach (var e in journal) if (e.Id == id) return e;
            return null;
        }

        /// <summary>加入日誌；同一 ID 只會加入一次（回傳 false 表示已存在）。</summary>
        public bool AddJournal(string id, string title, string text)
        {
            if (HasJournal(id)) return false;
            var e = new JournalEntry { Id = id, Title = title, Text = text };
            journal.Add(e);
            if (JournalAdded != null) JournalAdded(e);
            return true;
        }
    }
}
