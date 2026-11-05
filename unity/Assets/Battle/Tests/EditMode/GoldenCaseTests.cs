using System.Collections.Generic;
using NUnit.Framework;
using Shepherd.Battle.Core;

namespace Shepherd.Battle.Tests
{
    /// <summary>golden_cases.json 的 22 個案例：建立狀態 → RoundPlan → Resolve → 比對預期（補牌之前）。</summary>
    public sealed class GoldenCaseTests
    {
        public static IEnumerable<TestCaseData> Source()
        {
            foreach (var gc in GoldenCases.Cases)
            {
                yield return new TestCaseData(gc).SetName("Golden_" + gc.Id);
            }
        }

        [Test]
        public void GoldenFile_HasExpectedShape()
        {
            Assert.AreEqual("v0.1-unity-spec", GoldenCases.Version);
            Assert.AreEqual(22, GoldenCases.Cases.Count, "案例數量");
            Assert.AreEqual(10, GoldenCases.Properties.Count, "屬性測試數量");
        }

        [TestCaseSource(nameof(Source))]
        public void Golden(GoldenCase gc)
        {
            var rules = RuleSet.CreateDefault();
            RoundPlan plan;
            var state = GoldenCases.BuildState(gc, rules, out plan);

            var controller = new BattleController(state, rules, new SeededRandom(1));
            Assert.IsTrue(controller.TryLockSchool(gc.School));
            var commit = controller.Commit(plan);
            Assert.IsTrue(commit.Accepted, commit.Validation.Error);

            var s = commit.Resolution.NewState;
            string trace = Trace(commit.Resolution);
            Assert.AreEqual(gc.ExpectedAnchor, s.Anchor, gc.Id + " 錨定值\n" + trace);
            Assert.AreEqual(gc.ExpectedShield, s.Shield, gc.Id + " 擬態錨定\n" + trace);
            Assert.AreEqual(gc.ExpectedEnemies.Count, s.Enemies.Count, gc.Id + " 敵人數");
            foreach (var exp in gc.ExpectedEnemies)
            {
                var e = s.FindEnemy(GoldenCases.EnemyNumber(exp.Id));
                Assert.IsNotNull(e, gc.Id + " 找不到 " + exp.Id);
                Assert.AreEqual(exp.Value, e.Value, gc.Id + " " + exp.Id + " 失序值\n" + trace);
                Assert.AreEqual(exp.Resolved, e.Resolved, gc.Id + " " + exp.Id + " 解除\n" + trace);
                Assert.AreEqual(exp.Links, e.Links, gc.Id + " " + exp.Id + " 連結\n" + trace);
                Assert.AreEqual(exp.Corrections, e.Corrections, gc.Id + " " + exp.Id + " 校正\n" + trace);
            }
        }

        [Test]
        public void ResonanceEvent_OnlyForExactlyThreeSameElements()
        {
            var rules = RuleSet.CreateDefault();
            foreach (var gc in GoldenCases.Cases)
            {
                RoundPlan plan;
                var state = GoldenCases.BuildState(gc, rules, out plan);
                var r = RoundResolver.Resolve(state, plan, rules);
                bool triple = gc.Plan.Count == 3 && gc.Plan.TrueForAll(c => c.Element == gc.Plan[0].Element);
                int count = 0;
                foreach (var e in r.Events)
                {
                    if (e.Kind != EventKind.Resonance) continue;
                    count++;
                    Assert.AreEqual((int)gc.Plan[0].Element, e.Amount, gc.Id);
                    Assert.AreEqual(3, e.Value, gc.Id);
                }
                Assert.AreEqual(triple ? 1 : 0, count, gc.Id + " 共鳴事件數");
            }
        }

        internal static string Trace(ResolveResult r)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var e in r.Events) sb.AppendLine(e.ToString());
            return sb.ToString();
        }
    }
}
