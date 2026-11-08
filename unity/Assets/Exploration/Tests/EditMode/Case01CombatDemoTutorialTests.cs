using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Battle.Core;
using Shepherd.Battle.UI;
using Shepherd.Battle.UI.Arena;
using Shepherd.Exploration.Case01;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// 首戰情境教學：四個節點（威脅→資源與學派→編排→確認）、依玩家實際操作推進、可跳過與重看、
    /// 說明只依真實播放的 BattleEvent 出現、戰鬥結束與重來不留下遮罩，且教學不改變任何結算結果。
    /// </summary>
    public sealed class Case01CombatDemoTutorialTests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();

        private Case01CombatDemoQA NewQa(bool tutorial, CombatLabScenario scenario = null, int? seed = null)
        {
            var go = new GameObject("CombatDemoTutorialTest");
            roots.Add(go);
            var qa = go.AddComponent<Case01CombatDemoQA>();
            qa.Build(false, scenario, seed, tutorial);
            return qa;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
        }

        private static void Run(Case01CombatDemoQA qa, float s)
        {
            for (float t = 0; t < s; t += Dt)
            {
                qa.Arena.Tick(Dt);
                qa.Hud.Tick(Dt);
            }
        }

        private static void PlayOut(Case01CombatDemoQA qa)
        {
            for (int i = 0; i < 2000 && qa.Arena.IsPlaying; i++)
            {
                qa.Arena.Tick(Dt);
                qa.Hud.Tick(Dt);
            }
            Assert.IsFalse(qa.Arena.IsPlaying, "播放完畢");
            qa.Hud.Tick(Dt);
        }

        private static CombatLabScenario QuickVictory()
        {
            var s = new CombatLabScenario { Name = "quick-victory" };
            s.Enemies.Add(new EnemyState { Id = 1, Name = "E1 失序體・甲", Value = 1, Corrections = 1, AttackPattern = new[] { 12 } });
            s.Enemies.Add(new EnemyState { Id = 2, Name = "E2 失序體・乙", Value = 1, Corrections = 1, AttackPattern = new[] { 6 } });
            return s;
        }

        [Test]
        public void Tutorial_FourNodes_ByContinue_ThenResultsAndWrap_ThenCloses()
        {
            var qa = NewQa(true);
            var tut = qa.Hud.Tutorial;
            qa.Hud.Tick(Dt);
            Assert.AreEqual(CombatTutorialStep.Threat, tut.Step);
            Assert.IsTrue(tut.PanelVisible);
            StringAssert.Contains("1／4", tut.Title);
            int first = qa.Arena.Session.Snapshot().Enemies.First(e => !e.Resolved).Id;
            Assert.AreSame(qa.Hud.PlateRect(first), tut.HighlightTarget, "第一節指向敵人名牌");
            Assert.IsTrue(tut.HighlightVisible);
            Assert.IsFalse(qa.Hud.ReplayVisible, "教學進行中不顯示重看");

            tut.Continue();
            Assert.AreEqual(CombatTutorialStep.Resources, tut.Step);
            Assert.AreSame(qa.Hud.SchoolsRect, tut.HighlightTarget);
            tut.Continue();
            Assert.AreEqual(CombatTutorialStep.Compose, tut.Step);
            Assert.AreSame(qa.Hud.HandRect, tut.HighlightTarget);
            tut.Continue();
            Assert.AreEqual(CombatTutorialStep.Commit, tut.Step, "沒排牌也能前往下一節（不強制唯一解）");
            Assert.AreSame(qa.Hud.CommitRect, tut.HighlightTarget);

            qa.Hud.OnCard(0);
            qa.Hud.OnCommit();
            qa.Hud.Tick(Dt);
            Assert.AreEqual(CombatTutorialStep.Results, tut.Step, "按下確認後進入結果說明");
            Assert.IsFalse(tut.PanelVisible, "播放中不擋畫面");
            PlayOut(qa);
            Assert.AreEqual(CombatTutorialStep.Wrap, tut.Step);
            Assert.IsTrue(tut.PanelVisible);
            tut.Continue();
            Assert.IsFalse(tut.Active);
            Assert.IsFalse(tut.PanelVisible);
            Assert.IsFalse(tut.HighlightVisible);
            qa.Hud.Tick(Dt);
            Assert.IsTrue(qa.Hud.ReplayVisible);
        }

        [Test]
        public void Tutorial_AdvancesFromPlayerActions_TargetSchoolAndStaging()
        {
            var qa = NewQa(true);
            var tut = qa.Hud.Tutorial;
            var s = qa.Arena.Session;
            qa.Hud.Tick(Dt);
            var other = s.Snapshot().Enemies.First(e => !e.Resolved && e.Id != s.SelectedTargetId).Id;
            qa.Hud.OnPlate(other);
            qa.Hud.Tick(Dt);
            Assert.AreEqual(CombatTutorialStep.Resources, tut.Step, "選定目標後前往資源與學派");

            var school = (SchoolKind)(((int)s.SelectedSchool + 1) % 3);
            qa.Hud.OnSchool(school);
            qa.Hud.Tick(Dt);
            Assert.AreEqual(CombatTutorialStep.Resources, tut.Step, "切換學派只給回饋，不強迫前進");
            StringAssert.Contains(CombatDemoText.T2Done, tut.Body);
            Assert.AreEqual(school, s.SelectedSchool, "學派由 Session 管理");

            qa.Hud.OnCard(0);
            qa.Hud.Tick(Dt);
            Assert.AreEqual(CombatTutorialStep.Compose, tut.Step);
            Assert.AreEqual(CombatDemoText.T3More, tut.Body, "排入第一張後補充承接與共鳴");
            Assert.AreSame(qa.Hud.SequenceRect, tut.HighlightTarget);
            qa.Hud.OnSlot(0);
            qa.Hud.OnCard(1);
            qa.Hud.OnCard(2);
            Run(qa, 0.6f);
            Assert.AreEqual(CombatTutorialStep.Commit, tut.Step, "排入兩張後指向確認施放");
            Assert.AreEqual(2, s.Staged.Count, "教學沒有替玩家排牌或移除");
        }

        [Test]
        public void Tutorial_Skip_ClosesEverything_AndReplayRestarts()
        {
            var qa = NewQa(true);
            var tut = qa.Hud.Tutorial;
            qa.Hud.Tick(Dt);
            tut.Skip();
            qa.Hud.Tick(Dt);
            Assert.IsTrue(tut.Skipped);
            Assert.IsFalse(tut.Active);
            Assert.IsFalse(tut.PanelVisible);
            Assert.IsFalse(tut.HighlightVisible);
            Assert.IsTrue(qa.Hud.ReplayVisible);

            qa.Hud.OnCard(0);
            qa.Hud.OnCard(1);
            qa.Hud.OnCard(2);
            qa.Hud.OnCommit();
            PlayOut(qa);
            Assert.AreEqual(0, tut.Notes.Count, "跳過後不再顯示結果說明");
            Assert.IsFalse(tut.NoteVisible);

            qa.Hud.ClickReplay();
            Assert.IsTrue(tut.Active);
            Assert.AreEqual(CombatTutorialStep.Threat, tut.Step, "重看從第一節開始");
            Assert.IsTrue(tut.PanelVisible);
        }

        [Test]
        public void Tutorial_Notes_ComeOnlyFromRealPlayedEvents_OncePerKind()
        {
            var qa = NewQa(true);
            var tut = qa.Hud.Tutorial;
            for (int round = 0; round < 2 && !qa.Arena.Session.IsOver; round++)
            {
                int n = Mathf.Min(3, qa.Arena.Session.Snapshot().Hand.Count);
                for (int i = 0; i < n; i++) qa.Hud.OnCard(i);
                qa.Hud.OnCommit();
                var seen = new List<string>(tut.Notes);
                PlayOut(qa);
                var ev = qa.Arena.PlayedEvents;
                bool residue = ev.Any(e => e.Source != null && e.Source.StartsWith("residue:"));
                bool resonance = ev.Any(e => e.Kind == EventKind.Resonance);
                bool enemy = ev.Any(e => e.Kind == EventKind.EnemyActed && e.Amount > 0);
                Assert.AreEqual(residue || seen.Contains(CombatDemoText.NoteResidue), tut.Notes.Contains(CombatDemoText.NoteResidue), "承接說明");
                Assert.AreEqual(resonance || seen.Contains(CombatDemoText.NoteResonance), tut.Notes.Contains(CombatDemoText.NoteResonance), "共鳴說明");
                if (!qa.Arena.Session.IsOver)
                    Assert.AreEqual(enemy || seen.Contains(CombatDemoText.NoteEnemy), tut.Notes.Contains(CombatDemoText.NoteEnemy), "敵方攻擊說明");
                if (tut.Active) tut.Continue();
            }
            Assert.AreEqual(tut.Notes.Count, tut.Notes.Distinct().Count(), "每種說明只出現一次");
            if (tut.NoteVisible)
            {
                tut.HideNote();
                Assert.IsFalse(tut.NoteVisible, "說明可以點掉");
            }
        }

        [Test]
        public void Tutorial_DoesNotChangeResolution_SameActionsSameResult()
        {
            var with = NewQa(true, null, 7);
            var without = NewQa(false, null, 7);
            foreach (var qa in new[] { with, without })
            {
                qa.Hud.Tick(Dt);
                qa.Hud.OnCard(0);
                qa.Hud.OnCard(1);
                qa.Hud.OnCommit();
                PlayOut(qa);
            }
            Assert.IsTrue(with.Hud.Tutorial.Watching);
            Assert.IsFalse(without.Hud.Tutorial.Active);
            var a = with.Arena.Session.Snapshot();
            var b = without.Arena.Session.Snapshot();
            Assert.AreEqual(b.Round, a.Round);
            Assert.AreEqual(b.Anchor, a.Anchor);
            CollectionAssert.AreEqual(b.Enemies.Select(e => e.Value).ToArray(), a.Enemies.Select(e => e.Value).ToArray());
            CollectionAssert.AreEqual(b.Hand.Select(c => c.Id).ToArray(), a.Hand.Select(c => c.Id).ToArray(), "牌庫與抽牌不受教學影響");
            CollectionAssert.AreEqual(
                without.Arena.LastCommit.Resolution.Events.Select(e => e.Kind + e.Source + e.Amount).ToArray(),
                with.Arena.LastCommit.Resolution.Events.Select(e => e.Kind + e.Source + e.Amount).ToArray(), "事件序列一致");
        }

        [Test]
        public void Tutorial_ClosesOnBattleEnd_AndRestartLeavesSingleOverlay()
        {
            var qa = NewQa(true, QuickVictory(), 1);
            var tut = qa.Hud.Tutorial;
            qa.Hud.Tick(Dt);
            for (int i = 0; i < 3; i++) qa.Hud.OnCard(i);
            qa.Hud.OnCommit();
            PlayOut(qa);
            Assert.IsTrue(qa.Arena.Session.IsOver);
            Assert.IsFalse(tut.Active, "戰鬥結束時關閉");
            Assert.IsFalse(tut.PanelVisible);
            Assert.IsFalse(tut.NoteVisible);
            Assert.IsFalse(qa.Hud.ReplayVisible, "戰鬥結束後不提供重看");
            qa.Hud.ClickReplay();
            Assert.IsFalse(tut.Active);

            qa.Restart();
            qa.Restart();
            Assert.AreEqual(1, qa.GetComponentsInChildren<RectTransform>(true).Count(r => r.name == "TutorialPanel"), "重來兩次後只有一份教學");
            Assert.AreEqual(CombatTutorialStep.Threat, qa.Hud.Tutorial.Step, "F1 重來＝新的教學狀態");
        }

        [Test]
        public void Tutorial_RapidClicks_NoDoubleCommit_AndFontsReadable()
        {
            var qa = NewQa(true);
            var tut = qa.Hud.Tutorial;
            qa.Hud.Tick(Dt);
            qa.Hud.OnCard(0);
            qa.Hud.OnCommit();
            qa.Hud.OnCommit();
            qa.Hud.Tick(Dt);
            tut.Continue();
            tut.Continue();
            PlayOut(qa);
            Assert.AreEqual(1, qa.Arena.Session.Snapshot().Round, "連點只結算一個回合");
            foreach (var t in qa.Hud.Root.GetComponentsInChildren<Text>(true))
                Assert.GreaterOrEqual(t.fontSize, 16, "字級：" + t.name);
        }
    }
}
