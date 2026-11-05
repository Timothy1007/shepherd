using System;
using System.Collections.Generic;
using System.IO;
using Shepherd.Battle.Core;

namespace Shepherd.Battle.Tests
{
    public sealed class GoldenEnemy
    {
        public string Id;
        public int Value;
        public int Attack;
        public int Links;
        public int Corrections;
        public bool Resolved;
    }

    public sealed class GoldenCard
    {
        public ElementKind Element;
        public string Target;
    }

    public sealed class GoldenCase
    {
        public string Id;
        public string Title;
        public SchoolKind School;
        public int Round;
        public int Anchor;
        public int Shield;
        public List<GoldenEnemy> Enemies = new List<GoldenEnemy>();
        public List<GoldenCard> Plan = new List<GoldenCard>();
        public int ExpectedAnchor;
        public int ExpectedShield;
        public List<GoldenEnemy> ExpectedEnemies = new List<GoldenEnemy>();
        public string Oracle;

        public override string ToString()
        {
            return Id + " " + Title;
        }
    }

    public sealed class GoldenProperty
    {
        public string Id;
        public string Title;
        public string Rule;
    }

    /// <summary>讀取 unity/Assets/Tests/Data/golden_cases.json（唯讀，不修改檔案）。</summary>
    public static class GoldenCases
    {
        public const string RelativePath = "Assets/Tests/Data/golden_cases.json";

        private static List<GoldenCase> cases;
        private static List<GoldenProperty> properties;
        private static string version;

        public static string FilePath
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("SHEPHERD_GOLDEN_PATH");
                if (!string.IsNullOrEmpty(env)) return env;
                // Unity Editor 的工作目錄是專案根目錄（unity/）。
                return Path.Combine(Directory.GetCurrentDirectory(), RelativePath);
            }
        }

        public static IReadOnlyList<GoldenCase> Cases { get { Load(); return cases; } }
        public static IReadOnlyList<GoldenProperty> Properties { get { Load(); return properties; } }
        public static string Version { get { Load(); return version; } }

        private static void Load()
        {
            if (cases != null) return;
            string text = File.ReadAllText(FilePath, System.Text.Encoding.UTF8);
            var root = (Dictionary<string, object>)MiniJson.Parse(text);
            version = (string)root["version"];

            var list = new List<GoldenCase>();
            foreach (var o in (List<object>)root["cases"])
            {
                var c = (Dictionary<string, object>)o;
                var gc = new GoldenCase();
                gc.Id = (string)c["id"];
                gc.Title = (string)c["title"];
                gc.School = (SchoolKind)Enum.Parse(typeof(SchoolKind), (string)c["school"]);
                gc.Round = Int(c, "round");
                var player = (Dictionary<string, object>)c["player"];
                gc.Anchor = Int(player, "anchor");
                gc.Shield = Int(player, "shield");
                foreach (var eo in (List<object>)c["enemies"]) gc.Enemies.Add(ReadEnemy((Dictionary<string, object>)eo));
                foreach (var po in (List<object>)c["plan"])
                {
                    var p = (Dictionary<string, object>)po;
                    var card = new GoldenCard();
                    card.Element = (ElementKind)Enum.Parse(typeof(ElementKind), (string)p["element"]);
                    object t;
                    card.Target = p.TryGetValue("target", out t) ? (string)t : null;
                    gc.Plan.Add(card);
                }
                var exp = (Dictionary<string, object>)c["expected"];
                var ep = (Dictionary<string, object>)exp["player"];
                gc.ExpectedAnchor = Int(ep, "anchor");
                gc.ExpectedShield = Int(ep, "shield");
                foreach (var eo in (List<object>)exp["enemies"]) gc.ExpectedEnemies.Add(ReadEnemy((Dictionary<string, object>)eo));
                gc.Oracle = (string)c["oracle"];
                list.Add(gc);
            }

            var props = new List<GoldenProperty>();
            foreach (var o in (List<object>)root["properties"])
            {
                var p = (Dictionary<string, object>)o;
                props.Add(new GoldenProperty { Id = (string)p["id"], Title = (string)p["title"], Rule = (string)p["rule"] });
            }

            cases = list;
            properties = props;
        }

        private static GoldenEnemy ReadEnemy(Dictionary<string, object> d)
        {
            var e = new GoldenEnemy();
            e.Id = (string)d["id"];
            e.Value = Int(d, "value");
            e.Attack = d.ContainsKey("attack") ? Int(d, "attack") : 0;
            e.Links = Int(d, "links");
            e.Corrections = Int(d, "corrections");
            e.Resolved = d.ContainsKey("resolved") && (bool)d["resolved"];
            return e;
        }

        private static int Int(Dictionary<string, object> d, string key)
        {
            double v = (double)d[key];
            int i = (int)v;
            if (i != v) throw new FormatException(key + " 不是整數：" + v);
            return i;
        }

        /// <summary>"E1" → 1。</summary>
        public static int EnemyNumber(string id)
        {
            if (string.IsNullOrEmpty(id) || id[0] != 'E') throw new FormatException("敵人編號格式錯誤：" + id);
            return int.Parse(id.Substring(1));
        }

        /// <summary>
        /// 依案例建立狀態：標準 27 張牌庫；序列需要的牌放進手牌，其餘依編號補滿 7 張。
        /// 案例皆為回合 0，attack 欄即該敵人本回合的攻擊值。
        /// </summary>
        public static BattleState BuildState(GoldenCase gc, RuleSet rules, out RoundPlan plan)
        {
            var s = new BattleState();
            s.Round = gc.Round;
            s.Anchor = gc.Anchor;
            s.Shield = gc.Shield;
            foreach (var e in gc.Enemies)
            {
                s.Enemies.Add(new EnemyState
                {
                    Id = EnemyNumber(e.Id),
                    Name = e.Id,
                    Value = e.Value,
                    AttackPattern = new[] { e.Attack },
                    Links = e.Links,
                    Corrections = e.Corrections,
                });
            }

            s.Cards = DeckService.CreateStandardCards(rules);
            var remaining = new List<int>();
            foreach (var c in s.Cards) remaining.Add(c.Id);

            var planned = new List<PlannedCard>();
            foreach (var g in gc.Plan)
            {
                int pick = -1;
                foreach (int id in remaining)
                {
                    if (s.Card(id).Element == g.Element) { pick = id; break; }
                }
                if (pick < 0) throw new InvalidOperationException(gc.Id + "：牌庫中沒有足夠的 " + g.Element);
                remaining.Remove(pick);
                s.Hand.Add(pick);
                int? target = g.Target == null ? (int?)null : EnemyNumber(g.Target);
                planned.Add(new PlannedCard(pick, g.Element, target));
            }
            while (s.Hand.Count < RuleSet.HandSize)
            {
                s.Hand.Add(remaining[0]);
                remaining.RemoveAt(0);
            }
            s.Deck.AddRange(remaining);

            plan = new RoundPlan(gc.School, planned);
            return s;
        }
    }
}
