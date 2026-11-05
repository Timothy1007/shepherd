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

        // ---------------- 即時預覽與預估摘要 ----------------

        /// <summary>即時預覽必須等於以目前編排直接呼叫引擎預覽的結果。</summary>
        private static void AssertLiveMatchesEngine(CombatLabSession s)
        {
            Assert.IsNotNull(s.LastPreview, "即時預覽不得為空");
            var direct = s.Controller.Preview(s.BuildPlan());
            Assert.AreEqual(direct.Validation.IsValid, s.LastPreview.Validation.IsValid);
            Assert.AreEqual(direct.Result.NewState.Fingerprint(), s.LastPreview.Result.NewState.Fingerprint());
            Assert.AreEqual(direct.Result.NewState.Anchor, s.Forecast.AnchorAfter);
            Assert.AreEqual(direct.Result.NewState.Shield, s.Forecast.ShieldAfter);
            foreach (var e in s.Forecast.Enemies)
                Assert.AreEqual(direct.Result.NewState.FindEnemy(e.Id).Value, e.ValueAfter);
        }

        [Test]
        public void LivePreview_UpdatesOnEveryChange_WithoutTouchingState()
        {
            var s = NewSession();
            string before = Fingerprint(s);
            AssertLiveMatchesEngine(s);
            Assert.AreEqual(0, s.Staged.Count, "空序列也預估");

            var hand = Hand(s);
            s.AddCard(hand[0]);
            AssertLiveMatchesEngine(s);
            s.AddCard(hand[1]);
            AssertLiveMatchesEngine(s);
            s.SelectSchool(S.Relation);
            AssertLiveMatchesEngine(s);
            s.RemoveAt(0);
            AssertLiveMatchesEngine(s);
            s.ClearPlan();
            AssertLiveMatchesEngine(s);
            s.Redraw(hand[2]);
            AssertLiveMatchesEngine(s);
            Assert.AreEqual(0, s.Controller.Snapshot().Round, "預覽不推進回合");
            Assert.AreEqual(30, s.Snapshot().Anchor, "預覽不改動錨定值");
            Assert.IsNotEmpty(before);
        }

        [Test]
        public void Forecast_EnemyAttackImpact_AddsUpToAnchorAndShieldChange()
        {
            // 實在論沒有分流，敵人攻擊只會被擬態錨定吸收或扣錨定值。
            var s = SessionWith(E.Base, 1);
            for (int pass = 0; pass < 2; pass++)
            {
                var f = s.Forecast;
                var snap = s.Snapshot();
                foreach (var e in f.Enemies)
                {
                    var es = snap.Enemies.First(x => x.Id == e.Id);
                    Assert.IsTrue(e.Acted, e.Name + " 應行動");
                    Assert.AreEqual(es.ForecastAttack, e.Attack, "攻擊值來自引擎事件");
                    Assert.AreEqual(e.Attack, e.ShieldAbsorbed + e.AnchorLoss, "實在論下攻擊＝擬態吸收＋錨定損失");
                    Assert.AreEqual(0, e.Diverted);
                }
                Assert.AreEqual(f.AnchorBefore - f.AnchorAfter, f.TotalAnchorLoss, "錨定損失總和等於錨定值變化");
                // 第二輪：先放一張構形（擬態 +7），擬態錨定應吸收部分攻擊。
                s.AddCard(HandOf(s, E.Base)[0]);
            }
            Assert.Greater(s.Forecast.TotalShieldAbsorbed, 0, "有擬態錨定時預估顯示被吸收的量");
        }

        [Test]
        public void Forecast_RelationDivert_IsReported()
        {
            var scenario = Scenario(
                new EnemyState { Id = 1, Name = "甲", Value = 40, Links = 3, AttackPattern = new[] { 9 } });
            var s = new CombatLabSession(RuleSet.CreateDefault(), scenario, 1);
            s.SelectSchool(S.Relation);
            var e = s.Forecast.Enemy(1);
            Assert.IsTrue(e.Acted);
            Assert.AreEqual(9, e.Attack);
            Assert.Greater(e.Diverted, 0, "關係論分流要顯示出來");
            Assert.AreEqual(e.Attack, e.Diverted + e.ShieldAbsorbed + e.AnchorLoss);
            StringAssert.Contains("分流 " + e.Diverted, CombatLabText.EnemyImpact(e, s.Forecast.Outcome));
        }

        [Test]
        public void ForecastWithCard_IsHypothetical_AndMatchesAddingTheCard()
        {
            var s = NewSession();
            var hand = Hand(s);
            s.AddCard(hand[0]);
            string before = Fingerprint(s);
            string reason;
            var with = s.ForecastWithCard(hand[1], out reason);
            Assert.IsNotNull(with, reason);
            Assert.AreEqual(before, Fingerprint(s), "懸停預估不改動戰鬥狀態");
            Assert.AreEqual(1, s.Staged.Count, "懸停預估不改動編排");

            s.AddCard(hand[1]);
            Assert.AreEqual(s.Forecast.AnchorAfter, with.AnchorAfter);
            Assert.AreEqual(s.Forecast.ShieldAfter, with.ShieldAfter);
            for (int i = 0; i < with.Enemies.Count; i++)
                Assert.AreEqual(s.Forecast.Enemies[i].ValueAfter, with.Enemies[i].ValueAfter);

            Assert.IsNull(s.ForecastWithCard(hand[0], out reason));
            StringAssert.Contains("已在編排第 1 張", reason);
            s.AddCard(hand[2]);
            Assert.IsNull(s.ForecastWithCard(hand[3], out reason));
            StringAssert.Contains("AP", reason);
        }

        [Test]
        public void Commit_KeepsLastRoundSummary()
        {
            var s = NewSession();
            var expected = s.Forecast;
            s.Commit();
            Assert.AreEqual(expected.AnchorAfter, s.LastRound.AnchorAfter, "確認結果與預估一致");
            Assert.AreEqual(expected.TotalAnchorLoss, s.LastRound.TotalAnchorLoss);
            StringAssert.Contains("第 1 回合：", s.Message);
            StringAssert.Contains("錨定值 " + expected.AnchorBefore + " → " + expected.AnchorAfter, s.Message);
            Assert.IsTrue(s.Log.Any(l => l.Contains("本回合結果")));
            Assert.IsTrue(s.Log.Any(l => l.Contains(s.Snapshot().Enemies[0].Name + " 的攻擊")), "紀錄以敵人名稱標示攻擊來源");
        }

        [Test]
        public void EffectTexts_UseRuleValues()
        {
            var p = new RuleParams { StructureBaseShield = 11, RelationBinderLink = 3, ProcessBinderHeal = 5 };
            var rules = new RuleSet(p);
            StringAssert.Contains("+11", CombatLabText.EffectShort(rules, S.Structure, E.Base));
            StringAssert.Contains("+11", CombatLabText.EffectDetail(rules, S.Structure, E.Base));
            StringAssert.Contains("+3", CombatLabText.EffectShort(rules, S.Relation, E.Binder));
            StringAssert.Contains("回復錨定值 5", CombatLabText.EffectDetail(rules, S.Process, E.Binder));
            foreach (S school in Enum.GetValues(typeof(S)))
            foreach (E element in Enum.GetValues(typeof(E)))
            {
                Assert.IsNotEmpty(CombatLabText.EffectShort(RuleSet.CreateDefault(), school, element));
                Assert.IsNotEmpty(CombatLabText.EffectDetail(RuleSet.CreateDefault(), school, element));
            }
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
            Assert.IsNotNull(s.LastPreview, "新回合立即以空序列重新預覽");
            Assert.IsTrue(s.LastPreview.Validation.IsValid);
            Assert.IsNotNull(s.LastRound, "保留上一回合的結算摘要");
            Assert.AreEqual(1, s.LastRoundNumber);
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
