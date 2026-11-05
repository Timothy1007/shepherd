using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Battle.Core;
using E = Shepherd.Battle.Core.ElementKind;
using S = Shepherd.Battle.Core.SchoolKind;

namespace Shepherd.Battle.Tests
{
    /// <summary>規格第七節的 10 項屬性測試（P1–P10）。</summary>
    public sealed class PropertyTests
    {
        private static RuleSet Rules { get { return RuleSet.CreateDefault(); } }

        private static EnemyState[] Big(int attack = 0)
        {
            return new[] { TestBuilder.Enemy(1, 999, attack) };
        }

        // ---------------- P1 預覽一致性 ----------------

        public static IEnumerable<TestCaseData> P1Source()
        {
            foreach (var gc in GoldenCases.Cases)
            {
                yield return new TestCaseData(gc).SetName("P1_PreviewEqualsCommit_" + gc.Id);
            }
        }

        [TestCaseSource(nameof(P1Source))]
        public void P1_PreviewEqualsCommit(GoldenCase gc)
        {
            var rules = Rules;
            RoundPlan plan;
            var state = GoldenCases.BuildState(gc, rules, out plan);
            string input = state.Fingerprint();

            // Resolve 不修改輸入狀態。
            RoundResolver.Resolve(state, plan, rules);
            Assert.AreEqual(input, state.Fingerprint(), "Resolve 改動了輸入狀態");

            var c = new BattleController(state, rules, new SeededRandom(3));
            Assert.IsTrue(c.TryLockSchool(gc.School));
            string before = c.CloneState().Fingerprint();
            var preview = c.Preview(plan);
            Assert.IsTrue(preview.Validation.IsValid, preview.Validation.Error);
            Assert.AreEqual(before, c.CloneState().Fingerprint(), "Preview 改動了控制器狀態");
            Assert.AreEqual(0, c.Log.Count, "Preview 寫入了紀錄");

            var commit = c.Commit(plan);
            Assert.IsTrue(commit.Accepted);
            Assert.AreEqual(preview.Result.NewState.Fingerprint(), commit.Resolution.NewState.Fingerprint());
            Assert.AreEqual(preview.Result.Outcome, commit.Resolution.Outcome);
            CollectionAssert.AreEqual(TestBuilder.Strings(preview.Result.Events),
                TestBuilder.Strings(commit.Resolution.Events));
        }

        // ---------------- P2 調度 ----------------

        [Test]
        public void P2_Redraw_OncePerRound_AndResetsNextRound()
        {
            var c = BattleController.NewBattle(Big(), Rules, 7);
            var hand = c.CloneState().Hand;
            Assert.IsTrue(c.TryRedraw(hand[0]));
            Assert.IsFalse(c.TryRedraw(hand[1]), "同一回合第二次調度應被拒");

            Assert.IsTrue(c.Commit(RoundPlan.Empty(S.Structure)).Accepted);
            Assert.IsTrue(c.TryRedraw(c.CloneState().Hand[0]), "新回合可再調度一次");
        }

        [Test]
        public void P2_Redraw_NotAllowedAfterBattleEnds()
        {
            List<int> ids;
            var s = TestBuilder.State(30, 0, new[] { TestBuilder.Enemy(1, 1, 5) }, new[] { E.Ember }, out ids);
            var c = new BattleController(s, Rules, new SeededRandom(1));
            var r = c.Commit(TestBuilder.Plan(S.Structure, TestBuilder.Card(s, ids[0], 1)));
            Assert.AreEqual(BattleOutcome.Victory, r.Resolution.Outcome);
            Assert.IsFalse(c.TryRedraw(c.CloneState().Hand[0]), "確認且戰鬥結束後不能調度");
        }

        [Test]
        public void P2_Redraw_RejectsStagedCard()
        {
            var c = BattleController.NewBattle(Big(), Rules, 11);
            var s = c.CloneState();
            var plan = TestBuilder.FirstCards(s, S.Structure, 1, Rules);
            Assert.IsTrue(c.TryStage(plan));
            Assert.IsFalse(c.TryRedraw(plan.Cards[0].CardId), "已放進編排區的牌不能調度");
            Assert.IsTrue(c.TryRedraw(s.Hand[1]), "編排區以外的牌可以調度");
        }

        [Test]
        public void P2_Redraw_ConservesCountsAndHandSize()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var c = BattleController.NewBattle(Big(), Rules, seed);
                var s0 = c.CloneState();
                Assert.IsTrue(c.TryRedraw(s0.Hand[seed % 7]));
                var s1 = c.CloneState();
                Assert.AreEqual(7, s1.Hand.Count);
                Assert.AreEqual(27, s1.TotalCards);
                Assert.AreEqual(s0.Deck.Count, s1.Deck.Count);
                Assert.AreEqual(s0.Discard.Count, s1.Discard.Count);
                foreach (var el in new[] { E.Ember, E.Base, E.Binder })
                    Assert.AreEqual(9, TestBuilder.CountElement(s1, el));
                Assert.AreEqual(27, s1.Deck.Concat(s1.Hand).Concat(s1.Discard).Distinct().Count(), "牌不可重複或遺失");
            }
        }

        [Test]
        public void P2_Redraw_CanDrawBackOriginal()
        {
            int sameCount = 0;
            for (int seed = 0; seed < 300; seed++)
            {
                var c = BattleController.NewBattle(Big(), Rules, seed);
                int original = c.CloneState().Hand[0];
                Assert.IsTrue(c.TryRedraw(original));
                var redrawn = c.Log.Last(e => e.Kind == EventKind.Redrawn);
                Assert.AreEqual(original, redrawn.Amount);
                if (redrawn.Value == original) sameCount++;
            }
            Assert.Greater(sameCount, 0, "洗回牌庫再抽，應可能抽回原牌");
        }

        [Test]
        public void P2_Redraw_NoMaterialGuarantee_AllElementCards()
        {
            // v0.1 牌庫只有元素牌，調度不套用物資保底：抽到的牌只是隨機的元素牌。
            var c = BattleController.NewBattle(Big(), Rules, 5);
            Assert.IsTrue(c.TryRedraw(c.CloneState().Hand[0]));
            foreach (var card in c.Snapshot().Hand)
                CollectionAssert.Contains(new[] { E.Ember, E.Base, E.Binder }, card.Element);
        }

        // ---------------- P3 編排驗證 ----------------

        [Test]
        public void P3_MoreThanThreeCardsRejected()
        {
            var c = BattleController.NewBattle(Big(), Rules, 1);
            var s = c.CloneState();
            var plan = TestBuilder.FirstCards(s, S.Structure, 4, Rules);
            Assert.AreEqual(4, plan.Cards.Count);
            Assert.IsFalse(c.Validate(plan).IsValid);
            var r = c.Commit(plan);
            Assert.IsFalse(r.Accepted);
            Assert.AreEqual(s.Fingerprint(), c.CloneState().Fingerprint(), "被拒的序列不得改動狀態");
        }

        [Test]
        public void P3_TargetMustBeAliveEnemy()
        {
            List<int> ids;
            var enemies = new[] { TestBuilder.Enemy(1, 20, 0), TestBuilder.Enemy(2, 20, 0) };
            enemies[1].Value = 0;
            enemies[1].Resolved = true;
            var s = TestBuilder.State(30, 0, enemies, new[] { E.Ember, E.Base }, out ids);
            var c = new BattleController(s, Rules, new SeededRandom(1));

            Assert.IsFalse(c.Validate(TestBuilder.Plan(S.Structure, TestBuilder.Card(s, ids[0], 2))).IsValid, "已解除的敵人");
            Assert.IsFalse(c.Validate(TestBuilder.Plan(S.Structure, TestBuilder.Card(s, ids[0], 9))).IsValid, "不存在的敵人");
            Assert.IsFalse(c.Validate(TestBuilder.Plan(S.Structure, TestBuilder.Card(s, ids[0], null))).IsValid, "需要目標卻未指定");
            Assert.IsTrue(c.Validate(TestBuilder.Plan(S.Structure, TestBuilder.Card(s, ids[0], 1))).IsValid);
            Assert.IsTrue(c.Validate(TestBuilder.Plan(S.Structure, TestBuilder.Card(s, ids[1], null))).IsValid, "構形不需要目標");
        }

        [Test]
        public void P3_CardsMustBeInHandAndUnique()
        {
            List<int> ids;
            var s = TestBuilder.State(30, 0, Big(), new[] { E.Base }, out ids);
            var c = new BattleController(s, Rules, new SeededRandom(1));
            int notInHand = s.Deck[0];
            Assert.IsFalse(c.Validate(TestBuilder.Plan(S.Structure, TestBuilder.Card(s, notInHand, 1))).IsValid);
            Assert.IsFalse(c.Validate(TestBuilder.Plan(S.Structure,
                TestBuilder.Card(s, ids[0], null), TestBuilder.Card(s, ids[0], null))).IsValid);
        }

        [Test]
        public void P3_SchoolLockedAfterConfirm()
        {
            List<int> ids;
            var s = TestBuilder.State(30, 0, Big(), new[] { E.Base }, out ids);
            var c = new BattleController(s, Rules, new SeededRandom(1));
            Assert.IsTrue(c.TryLockSchool(S.Structure));
            Assert.IsFalse(c.TryLockSchool(S.Process), "同回合不得更換學派");
            Assert.IsTrue(c.TryLockSchool(S.Structure));
            Assert.AreEqual(S.Structure, c.LockedSchool);

            var other = TestBuilder.Plan(S.Process, TestBuilder.Card(s, ids[0], 1));
            Assert.IsFalse(c.Validate(other).IsValid);
            Assert.IsFalse(c.Commit(other).Accepted);
            Assert.IsFalse(c.Preview(other).Validation.IsValid);
        }

        [Test]
        public void P3_EmptySequenceIsValid()
        {
            var c = BattleController.NewBattle(Big(), Rules, 1);
            var plan = RoundPlan.Empty(S.Relation);
            Assert.IsTrue(c.Validate(plan).IsValid);
            var before = c.CloneState();
            var r = c.Commit(plan);
            Assert.IsTrue(r.Accepted);
            Assert.AreEqual(7, c.CloneState().Hand.Count);
            CollectionAssert.AreEqual(before.Hand, c.CloneState().Hand, "空序列不抽牌");
        }

        // ---------------- P4 補牌與洗牌 ----------------

        [Test]
        public void P4_RefillAndReshuffle_ConservesCards()
        {
            var rules = Rules;
            var c = BattleController.NewBattle(Big(), rules, 2024);
            bool reshuffled = false;
            for (int round = 0; round < 12; round++)
            {
                var s = c.CloneState();
                var plan = TestBuilder.FirstCards(s, S.Structure, 3, rules);
                var r = c.Commit(plan);
                Assert.IsTrue(r.Accepted, r.Validation.Error);
                Assert.AreEqual(BattleOutcome.Ongoing, r.Resolution.Outcome);

                var after = c.CloneState();
                Assert.AreEqual(7, after.Hand.Count, "確認後手牌補回 7 張");
                Assert.AreEqual(27, after.TotalCards, "27 張牌總數守恆");
                Assert.AreEqual(27, after.Deck.Concat(after.Hand).Concat(after.Discard).Distinct().Count());
                foreach (var el in new[] { E.Ember, E.Base, E.Binder })
                    Assert.AreEqual(9, TestBuilder.CountElement(after, el));
                if (r.RefillEvents.Any(e => e.Kind == EventKind.Reshuffled)) reshuffled = true;
            }
            Assert.IsTrue(reshuffled, "牌庫抽完時棄牌區應洗回牌庫");
        }

        [Test]
        public void P4_ReshuffleHappensOnlyWhenDeckEmpty()
        {
            List<int> ids;
            var s = TestBuilder.State(30, 0, Big(), new[] { E.Base, E.Base, E.Base }, out ids);
            // 牌庫只留 1 張，其餘放進棄牌區。
            s.Discard.AddRange(s.Deck.Skip(1));
            s.Deck.RemoveRange(1, s.Deck.Count - 1);
            var c = new BattleController(s, Rules, new SeededRandom(9));
            var plan = TestBuilder.Plan(S.Structure, TestBuilder.Card(s, ids[0], null),
                TestBuilder.Card(s, ids[1], null), TestBuilder.Card(s, ids[2], null));
            var r = c.Commit(plan);
            var after = c.CloneState();
            Assert.AreEqual(1, r.RefillEvents.Count(e => e.Kind == EventKind.Reshuffled));
            Assert.AreEqual(7, after.Hand.Count);
            Assert.AreEqual(27, after.TotalCards);
            Assert.AreEqual(0, after.Discard.Count, "洗回後棄牌區清空");
        }

        // ---------------- P5 起手 ----------------

        [Test]
        public void P5_OpeningHand_SevenElementCards()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var c = BattleController.NewBattle(Big(), Rules, seed);
                var s = c.CloneState();
                Assert.AreEqual(7, s.Hand.Count);
                Assert.AreEqual(20, s.Deck.Count);
                Assert.AreEqual(0, s.Discard.Count);
                Assert.AreEqual(30, s.Anchor);
                Assert.AreEqual(0, s.Shield);
                foreach (var id in s.Hand)
                    CollectionAssert.Contains(new[] { E.Ember, E.Base, E.Binder }, s.Card(id).Element);
                foreach (var el in new[] { E.Ember, E.Base, E.Binder })
                    Assert.AreEqual(9, TestBuilder.CountElement(s, el));
            }
        }

        // ---------------- P6 決定性 ----------------

        [Test]
        public void P6_SameSeedSameSequence_IdenticalEvents()
        {
            var a = Replay(42);
            var b = Replay(42);
            CollectionAssert.AreEqual(a.Key, b.Key);
            Assert.AreEqual(a.Value, b.Value);
            Assert.Greater(a.Key.Count, 0);
        }

        private static KeyValuePair<List<string>, string> Replay(int seed)
        {
            var rules = Rules;
            var enemies = new[] { TestBuilder.Enemy(1, 120, 6, 1), TestBuilder.Enemy(2, 90, 4) };
            enemies[0].AttackPattern = new[] { 6, 9, 3 };
            var c = BattleController.NewBattle(enemies, rules, seed);
            var schools = new[] { S.Structure, S.Process, S.Relation };
            for (int round = 0; round < 8 && c.Outcome == BattleOutcome.Ongoing; round++)
            {
                var school = schools[round % 3];
                c.TryLockSchool(school);
                c.TryRedraw(c.CloneState().Hand[round % 7]);
                var plan = TestBuilder.FirstCards(c.CloneState(), school, round % 4, rules);
                Assert.IsTrue(c.Commit(plan).Accepted);
            }
            return new KeyValuePair<List<string>, string>(TestBuilder.Strings(c.Log), c.CloneState().Fingerprint());
        }

        // ---------------- P7 勝利立即結束 ----------------

        [Test]
        public void P7_VictoryMidSequence_StopsImmediately()
        {
            List<int> ids;
            var s = TestBuilder.State(30, 0, new[] { TestBuilder.Enemy(1, 4, 10) },
                new[] { E.Ember, E.Base, E.Base }, out ids);
            var c = new BattleController(s, Rules, new SeededRandom(1));
            var plan = TestBuilder.Plan(S.Structure, TestBuilder.Card(s, ids[0], 1),
                TestBuilder.Card(s, ids[1], null), TestBuilder.Card(s, ids[2], null));
            var r = c.Commit(plan);
            var ns = r.Resolution.NewState;
            var ev = r.Resolution.Events;

            Assert.AreEqual(BattleOutcome.Victory, r.Resolution.Outcome);
            Assert.AreEqual(0, ns.Shield, "剩餘的基體不結算");
            Assert.AreEqual(30, ns.Anchor, "不進入敵方階段");
            Assert.IsFalse(ev.Any(e => e.Source == "card:1" || e.Source == "card:2" || e.Source.StartsWith("residue")));
            Assert.IsFalse(ev.Any(e => e.Kind == EventKind.EnemyActed));
            Assert.IsFalse(ev.Any(e => e.Source.StartsWith("phase:")));
            CollectionAssert.IsSubsetOf(ids, ns.Discard, "剩餘的牌直接棄置");
            Assert.AreEqual(0, r.RefillEvents.Count, "勝利後不補牌");
            Assert.AreEqual(27, c.CloneState().TotalCards);
        }

        [Test]
        public void P7_VictoryInCorrectionPhase_SkipsEnemyPhase()
        {
            List<int> ids;
            var s = TestBuilder.State(30, 0, new[] { TestBuilder.Enemy(1, 2, 10, 0, 2) }, new E[0], out ids);
            var c = new BattleController(s, Rules, new SeededRandom(1));
            var r = c.Commit(RoundPlan.Empty(S.Process));
            Assert.AreEqual(BattleOutcome.Victory, r.Resolution.Outcome);
            Assert.AreEqual(30, r.Resolution.NewState.Anchor, "回饋與敵方階段都不執行");
            Assert.IsFalse(r.Resolution.Events.Any(e => e.Kind == EventKind.EnemyActed));
        }

        // ---------------- P8 戰敗 ----------------

        [Test]
        public void P8_Defeat_LaterEnemiesDoNotAct()
        {
            List<int> ids;
            var enemies = new[] { TestBuilder.Enemy(1, 40, 10), TestBuilder.Enemy(2, 40, 10) };
            var s = TestBuilder.State(5, 0, enemies, new E[0], out ids);
            var c = new BattleController(s, Rules, new SeededRandom(1));
            var r = c.Commit(RoundPlan.Empty(S.Structure));
            var ev = r.Resolution.Events;

            Assert.AreEqual(BattleOutcome.Defeat, r.Resolution.Outcome);
            Assert.LessOrEqual(r.Resolution.NewState.Anchor, 0);
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.EnemyActed && e.EnemyId == 1));
            Assert.IsFalse(ev.Any(e => e.Kind == EventKind.EnemyActed && e.EnemyId == 2), "後面的敵人不再行動");
            Assert.IsFalse(ev.Any(e => e.Kind == EventKind.Pressure), "戰敗時不觸發回應");
            Assert.AreEqual(0, r.RefillEvents.Count);
            Assert.IsFalse(c.Commit(RoundPlan.Empty(S.Structure)).Accepted, "戰鬥結束後不能再確認");
            Assert.IsFalse(c.TryLockSchool(S.Structure));
        }

        [Test]
        public void P8_AnchorExactlyZeroIsDefeat()
        {
            List<int> ids;
            var s = TestBuilder.State(10, 0, new[] { TestBuilder.Enemy(1, 40, 10) }, new E[0], out ids);
            var c = new BattleController(s, Rules, new SeededRandom(1));
            var r = c.Commit(RoundPlan.Empty(S.Structure));
            Assert.AreEqual(0, r.Resolution.NewState.Anchor);
            Assert.AreEqual(BattleOutcome.Defeat, r.Resolution.Outcome);
        }

        // ---------------- P9 狀態隨敵人消失 ----------------

        [Test]
        public void P9_ResolvedEnemyLosesStatesAndLeavesPlay()
        {
            List<int> ids;
            var enemies = new[]
            {
                TestBuilder.Enemy(1, 2, 0, 4, 3, 1),
                TestBuilder.Enemy(2, 30, 3, 4),
                TestBuilder.Enemy(3, 30, 0),
            };
            var s = TestBuilder.State(30, 0, enemies, new[] { E.Ember }, out ids);
            var c = new BattleController(s, Rules, new SeededRandom(1));
            var r = c.Commit(TestBuilder.Plan(S.Relation, TestBuilder.Card(s, ids[0], 1)));
            var ns = r.Resolution.NewState;
            var ev = r.Resolution.Events.ToList();

            var e1 = ns.FindEnemy(1);
            Assert.IsTrue(e1.Resolved);
            Assert.AreEqual(0, e1.Value);
            Assert.AreEqual(0, e1.Links, "連結一併消失");
            Assert.AreEqual(0, e1.Corrections, "校正一併消失");
            Assert.AreEqual(0, e1.Bind, "束縛一併消失");

            int resolvedAt = ev.FindIndex(e => e.Kind == EventKind.EnemyResolved && e.EnemyId == 1);
            Assert.GreaterOrEqual(resolvedAt, 0);
            Assert.IsFalse(ev.Skip(resolvedAt + 1).Any(e => e.EnemyId == 1),
                "解除後不再參與傳導、校正或敵方階段\n" + GoldenCaseTests.Trace(r.Resolution));

            // E2：擊殺 E1 時受傳導 ⌊2×50%⌋=1；敵方階段分流 1 層、回流 3，且不再傳導給已解除的 E1。
            Assert.AreEqual(26, ns.FindEnemy(2).Value);
            Assert.AreEqual(2, ns.FindEnemy(2).Links);
            Assert.AreEqual(30, ns.FindEnemy(3).Value);
        }

        // ---------------- P10 數值邊界 ----------------

        [Test]
        public void P10_LinkCapIsFour()
        {
            List<int> ids;
            var s = TestBuilder.State(30, 0, new[] { TestBuilder.Enemy(1, 40, 0, 3) }, new[] { E.Binder, E.Binder }, out ids);
            var c = new BattleController(s, Rules, new SeededRandom(1));
            var r = c.Commit(TestBuilder.Plan(S.Relation, TestBuilder.Card(s, ids[0], 1), TestBuilder.Card(s, ids[1], 1)));
            Assert.IsTrue(r.Resolution.Events.Where(e => e.Kind == EventKind.LinkChanged).All(e => e.Value <= 4));
            Assert.AreEqual(3, r.Resolution.NewState.FindEnemy(1).Links, "上限 4，回合結束 −1");
        }

        [Test]
        public void P10_AnchorCapIsForty()
        {
            List<int> ids;
            var s = TestBuilder.State(39, 0, new[] { TestBuilder.Enemy(1, 40, 0) }, new[] { E.Binder }, out ids);
            var c = new BattleController(s, Rules, new SeededRandom(1));
            var r = c.Commit(TestBuilder.Plan(S.Process, TestBuilder.Card(s, ids[0], 1)));
            Assert.AreEqual(40, r.Resolution.NewState.Anchor, "回復與回饋都不超過 40");
            Assert.IsTrue(r.Resolution.Events.Where(e => e.Kind == EventKind.AnchorChanged).All(e => e.Value <= 40));
        }

        [Test]
        public void P10_DisorderNeverBelowZero()
        {
            List<int> ids;
            var s = TestBuilder.State(30, 0, new[] { TestBuilder.Enemy(1, 3, 0), TestBuilder.Enemy(2, 40, 0) },
                new[] { E.Ember }, out ids);
            var c = new BattleController(s, Rules, new SeededRandom(1));
            var r = c.Commit(TestBuilder.Plan(S.Structure, TestBuilder.Card(s, ids[0], 1)));
            var p = r.Resolution.Events.First(e => e.Kind == EventKind.Pressure);
            Assert.AreEqual(4, p.Amount, "名義量");
            Assert.AreEqual(3, p.Value, "實際量不超過剩餘失序值");
            Assert.AreEqual(0, r.Resolution.NewState.FindEnemy(1).Value);
        }

        [Test]
        public void P10_ShieldNeverNegative()
        {
            List<int> ids;
            var s = TestBuilder.State(30, 1, new[] { TestBuilder.Enemy(1, 40, 10) }, new[] { E.Ember }, out ids);
            var c = new BattleController(s, Rules, new SeededRandom(1));
            var r = c.Commit(TestBuilder.Plan(S.Structure, TestBuilder.Card(s, ids[0], 1)));
            var ns = r.Resolution.NewState;
            Assert.AreEqual(0, ns.Shield);
            Assert.AreEqual(35, ns.FindEnemy(1).Value, "轉化 1 點：壓制 4+1");
            Assert.AreEqual(20, ns.Anchor);
            Assert.IsTrue(r.Resolution.Events.Where(e => e.Kind == EventKind.ShieldChanged).All(e => e.Value >= 0));
        }
    }
}
