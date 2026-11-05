using System;
using System.Linq;
using NUnit.Framework;
using Shepherd.Battle.Core;
using Shepherd.Battle.UI;
using E = Shepherd.Battle.Core.ElementKind;
using S = Shepherd.Battle.Core.SchoolKind;

namespace Shepherd.Battle.Tests
{
    /// <summary>Combat Lab 介面狀態（CombatLabSession）與 BattleController 的同步與流程。</summary>
    public sealed class CombatLabSessionTests
    {
        private static CombatLabSession NewSession(int seed = CombatLabScenario.DefaultSeed)
        {
            return new CombatLabSession(RuleSet.CreateDefault(), CombatLabScenario.Default(), seed);
        }

        private static CombatLabScenario Scenario(params EnemyState[] enemies)
        {
            var s = new CombatLabScenario { Name = "test" };
            s.Enemies.AddRange(enemies);
            return s;
        }

        private static int[] Hand(CombatLabSession s)
        {
            return s.Snapshot().Hand.Select(c => c.Id).ToArray();
        }

        private static string Fingerprint(CombatLabSession s)
        {
            return s.Controller.CloneState().Fingerprint();
        }

        /// <summary>找出起手含至少 n 張指定元素的種子。</summary>
        private static CombatLabSession SessionWith(E element, int n, CombatLabScenario scenario = null)
        {
            for (int seed = 1; seed < 500; seed++)
            {
                var s = new CombatLabSession(RuleSet.CreateDefault(), scenario ?? CombatLabScenario.Default(), seed);
                if (s.Snapshot().Hand.Count(c => c.Element == element) >= n) return s;
            }
            throw new InvalidOperationException("找不到符合的種子");
        }

        private static int[] HandOf(CombatLabSession s, E element)
        {
            return s.Snapshot().Hand.Where(c => c.Element == element).Select(c => c.Id).ToArray();
        }

        [Test]
        public void Initial_StateComesFromBattleState()
        {
            var s = NewSession();
            var snap = s.Snapshot();
            Assert.AreEqual(7, snap.Hand.Count);
            Assert.AreEqual(0, snap.Round);
            Assert.AreEqual(30, snap.Anchor);
            Assert.AreEqual(3, s.ApMax);
            Assert.AreEqual(3, s.ApRemaining);
            Assert.AreEqual(S.Structure, s.SelectedSchool);
            Assert.AreEqual(1, s.SelectedTargetId);
            Assert.AreEqual(2, snap.Enemies.Count);
            Assert.IsNull(s.Controller.LockedSchool, "確認前引擎尚未鎖定學派");
            Assert.Greater(s.Log.Count, 0);
        }

        [Test]
        public void Staging_OrderDuplicateAndApLimit()
        {
            var s = NewSession();
            var hand = Hand(s);
            Assert.IsTrue(s.ToggleCard(hand[0]));
            Assert.IsTrue(s.AddCard(hand[1]));
            Assert.IsFalse(s.AddCard(hand[1]), "同一張牌不能重複加入");
            Assert.AreEqual(2, s.Staged.Count);
            Assert.IsTrue(s.AddCard(hand[2]));
            Assert.IsFalse(s.AddCard(hand[3]), "超過 AP 被拒");
            Assert.AreEqual(0, s.ApRemaining);
            CollectionAssert.AreEqual(new[] { hand[0], hand[1], hand[2] }, s.Staged.Select(c => c.CardId).ToArray());

            Assert.IsTrue(s.RemoveAt(1));
            CollectionAssert.AreEqual(new[] { hand[0], hand[2] }, s.Staged.Select(c => c.CardId).ToArray(), "移除後保留順序");
            Assert.IsTrue(s.ToggleCard(hand[0]), "再點一次已編排的牌即移除");
            CollectionAssert.AreEqual(new[] { hand[2] }, s.Staged.Select(c => c.CardId).ToArray());
            s.ClearPlan();
            Assert.AreEqual(0, s.Staged.Count);
            Assert.AreEqual(3, s.ApRemaining);
            CollectionAssert.AreEqual(hand, Hand(s), "編排不改變手牌");
        }

        [Test]
        public void Preview_DoesNotChangeState_AndMatchesCommit()
        {
            var s = NewSession();
            var hand = Hand(s);
            s.AddCard(hand[0]);
            s.AddCard(hand[1]);
            string before = Fingerprint(s);
            int logBefore = s.Log.Count;

            var preview = s.Preview();
            Assert.IsTrue(preview.Validation.IsValid, preview.Validation.Error);
            Assert.AreEqual(before, Fingerprint(s), "預覽不得修改 BattleState");
            Assert.AreEqual(2, s.Staged.Count, "預覽不清除編排");
            Assert.AreEqual(logBefore, s.Log.Count, "預覽不寫入戰鬥紀錄");
            Assert.AreEqual(0, s.Controller.Log.Count);

            var commit = s.Commit();
            Assert.IsTrue(commit.Accepted);
            Assert.AreEqual(preview.Result.NewState.Fingerprint(), commit.Resolution.NewState.Fingerprint(),
                "預覽與正式結算使用同一個核心");
            CollectionAssert.AreEqual(TestBuilder.Strings(preview.Result.Events), TestBuilder.Strings(commit.Resolution.Events));
        }

        [Test]
        public void Commit_AdvancesRound_AndKeepsUiInSync()
        {
            var s = NewSession();
            var hand = Hand(s);
            s.AddCard(hand[0]);
            var r = s.Commit();
            Assert.IsTrue(r.Accepted);
            var snap = s.Snapshot();
            Assert.AreEqual(1, snap.Round);
            Assert.AreEqual(0, s.Staged.Count, "確認後清空編排");
            Assert.AreEqual(7, snap.Hand.Count);
            Assert.IsNull(s.LastPreview);
            Assert.IsNull(snap.LockedSchool, "新回合可重新選擇學派");
            Assert.IsTrue(s.Log.Any(l => l.Contains("第 1 回合") && l.Contains("實在論")));
            foreach (var e in r.Resolution.Events) CollectionAssert.Contains(s.Log, s.Describe(e));
            Assert.IsTrue(s.Log.Any(l => l.Contains("行動：攻擊")), "紀錄包含敵方行動");
        }

        [Test]
        public void EmptySequence_CanBeConfirmed()
        {
            var s = NewSession();
            var hand = Hand(s);
            var r = s.Commit();
            Assert.IsTrue(r.Accepted, r.Validation.Error);
            Assert.AreEqual(1, s.Snapshot().Round);
            CollectionAssert.AreEqual(hand, Hand(s), "空序列不抽牌");
            Assert.IsTrue(s.Log.Any(l => l.Contains("空序列")));
        }

        [Test]
        public void SchoolSwitch_BeforeConfirm_UsesEngineTargetRules()
        {
            var s = SessionWith(E.Base, 1);
            int baseCard = HandOf(s, E.Base)[0];
            s.AddCard(baseCard);

            Assert.IsTrue(s.SelectSchool(S.Structure));
            Assert.IsNull(s.BuildPlan().Cards[0].TargetEnemyId, "實在論基體沒有敵方指向部分");
            Assert.IsTrue(s.SelectSchool(S.Process));
            Assert.AreEqual(1, s.BuildPlan().Cards[0].TargetEnemyId, "過程哲學基體需要目標");
            Assert.AreEqual(S.Process, s.BuildPlan().School);
            Assert.AreEqual(1, s.Staged.Count, "切換學派保留編排");

            var r = s.Commit();
            Assert.IsTrue(r.Accepted);
            Assert.IsTrue(s.Log.Any(l => l.Contains("過程哲學")));
        }

        [Test]
        public void SchoolSwitch_RejectedWhenEngineLocked()
        {
            var s = NewSession();
            Assert.IsTrue(s.Controller.TryLockSchool(S.Relation));
            Assert.IsFalse(s.SelectSchool(S.Structure), "引擎已鎖定時不得更換");
            Assert.IsTrue(s.SelectSchool(S.Relation));
        }

        [Test]
        public void Redraw_OncePerRound_NotForStagedCards_HandStaysInSync()
        {
            var s = NewSession();
            var hand = Hand(s);
            s.AddCard(hand[0]);
            Assert.IsFalse(s.Redraw(hand[0]), "已編排的牌不能調度");
            Assert.IsTrue(s.Redraw(hand[1]));
            Assert.IsFalse(s.Redraw(hand[2]), "每回合一次");
            Assert.AreEqual(7, s.Snapshot().Hand.Count);
            Assert.IsTrue(Hand(s).Contains(hand[0]), "已編排的牌仍在手牌");
            Assert.AreEqual(hand[0], s.Staged[0].CardId);
            Assert.AreEqual(27, s.Controller.CloneState().TotalCards);
            Assert.IsTrue(s.Log.Any(l => l.Contains("調度")));

            s.Commit();
            Assert.AreEqual(1, s.Snapshot().RedrawsRemaining, "新回合恢復調度次數");
            Assert.IsTrue(s.Redraw(Hand(s)[0]));
        }

        [Test]
        public void Target_MustBeAlive_AndMovesWhenEnemyResolved()
        {
            var scenario = Scenario(TestBuilder.Enemy(1, 1, 5), TestBuilder.Enemy(2, 40, 0));
            var s = SessionWith(E.Ember, 2, scenario);
            var embers = HandOf(s, E.Ember);
            Assert.IsTrue(s.SelectTarget(1));
            s.AddCard(embers[0]);
            s.AddCard(embers[1]);
            var r = s.Commit();
            Assert.IsTrue(r.Accepted);
            Assert.IsTrue(r.Resolution.Events.Any(e => e.Kind == EventKind.CardFizzled), "第二張目標已解除，由核心判定失效");
            Assert.IsTrue(s.Log.Any(l => l.Contains("失效")));
            Assert.IsTrue(s.Snapshot().Enemies[0].Resolved);
            Assert.AreEqual(2, s.SelectedTargetId, "目標解除後改為存活的敵人");
            Assert.IsFalse(s.SelectTarget(1), "不能選擇已解除的敵人");
            Assert.IsFalse(s.SelectTarget(9), "不能選擇不存在的敵人");
        }

        [Test]
        public void Resonance_ShownInLog()
        {
            var s = SessionWith(E.Base, 3);
            foreach (var id in HandOf(s, E.Base).Take(3)) Assert.IsTrue(s.AddCard(id));
            var r = s.Commit();
            Assert.IsTrue(r.Resolution.Events.Any(e => e.Kind == EventKind.Resonance));
            Assert.IsTrue(s.Log.Any(l => l.Contains("共鳴")));
        }

        [Test]
        public void Victory_EndsBattle_AndBlocksActions()
        {
            var s = SessionWith(E.Ember, 1, Scenario(TestBuilder.Enemy(1, 2, 5)));
            s.AddCard(HandOf(s, E.Ember)[0]);
            s.Commit();
            Assert.IsTrue(s.IsOver);
            Assert.AreEqual(BattleOutcome.Victory, s.Snapshot().Outcome);
            Assert.IsTrue(s.Log.Any(l => l.Contains("勝利")));
            Assert.IsFalse(s.AddCard(Hand(s)[0]));
            Assert.IsFalse(s.Commit().Accepted);
            Assert.IsFalse(s.SelectSchool(S.Process));
        }

        [Test]
        public void Defeat_EndsBattle()
        {
            var s = NewSessionWith(Scenario(TestBuilder.Enemy(1, 40, 50), TestBuilder.Enemy(2, 40, 5)));
            s.Commit();
            Assert.AreEqual(BattleOutcome.Defeat, s.Snapshot().Outcome);
            Assert.IsTrue(s.Log.Any(l => l.Contains("戰敗")));
            Assert.IsFalse(s.Log.Any(l => l.Contains("E2") && l.Contains("行動")), "戰敗後後面的敵人不行動");
        }

        private static CombatLabSession NewSessionWith(CombatLabScenario scenario)
        {
            return new CombatLabSession(RuleSet.CreateDefault(), scenario, 1);
        }

        [Test]
        public void Reset_SameSeedIsReproducible()
        {
            var a = NewSession(77);
            var b = NewSession(77);
            CollectionAssert.AreEqual(Hand(a), Hand(b));
            a.AddCard(Hand(a)[0]);
            b.AddCard(Hand(b)[0]);
            a.Commit();
            b.Commit();
            CollectionAssert.AreEqual(a.Log, b.Log);

            a.Reset(77);
            Assert.AreEqual(0, a.Snapshot().Round);
            Assert.AreEqual(0, a.Staged.Count);
            CollectionAssert.AreEqual(Hand(NewSession(77)), Hand(a));
        }

        [Test]
        public void EveryEventKind_HasChineseText()
        {
            foreach (EventKind kind in Enum.GetValues(typeof(EventKind)))
            {
                var e = new BattleEvent(kind, "card:0", 1, 1, 2);
                string text = CombatLabText.Event(e, id => "E" + id, id => "#" + id);
                Assert.IsFalse(string.IsNullOrEmpty(text), kind.ToString());
                Assert.AreNotEqual(e.ToString(), text, kind + " 沒有對應的中文紀錄");
            }
        }
    }
}
