using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Battle.Core;
using Shepherd.Battle.UI;
using Shepherd.Battle.UI.Arena;
using Shepherd.Exploration.Case01;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// 施放／命中演出：施放條張數與元素取自實際事件、逐張點亮、承接連線只在承接事件出現、三連聚合只在共鳴事件出現、
    /// 數值標籤與事件一一對應、回合結果只加總事件、勝負淡出、降低動態不位移不縮放、重來不殘留。
    /// </summary>
    public sealed class Case01CombatDemoFxTests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();

        private Case01CombatDemoQA NewQa(CombatLabScenario scenario = null, int? seed = null)
        {
            var go = new GameObject("CombatDemoFxTest");
            roots.Add(go);
            var qa = go.AddComponent<Case01CombatDemoQA>();
            qa.Build(false, scenario, seed, false);
            return qa;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
        }

        private static void Step(Case01CombatDemoQA qa)
        {
            qa.Arena.Tick(Dt);
            qa.Hud.Tick(Dt);
        }

        private static void PlayOut(Case01CombatDemoQA qa)
        {
            for (int i = 0; i < 3000 && qa.Arena.IsPlaying; i++) Step(qa);
            Assert.IsFalse(qa.Arena.IsPlaying);
            Step(qa);
        }

        private static CombatLabScenario QuickVictory()
        {
            var s = new CombatLabScenario { Name = "quick-victory" };
            s.Enemies.Add(new EnemyState { Id = 1, Name = "E1 失序體・甲", Value = 1, Corrections = 1, AttackPattern = new[] { 12 } });
            s.Enemies.Add(new EnemyState { Id = 2, Name = "E2 失序體・乙", Value = 1, Corrections = 1, AttackPattern = new[] { 6 } });
            return s;
        }

        private static CombatLabScenario QuickDefeat()
        {
            var s = new CombatLabScenario { Name = "quick-defeat" };
            s.Enemies.Add(new EnemyState { Id = 1, Name = "E1 失序體・甲", Value = 500, AttackPattern = new[] { 999 } });
            return s;
        }

        /// <summary>手牌中三張同元素的位置（找不到回傳 null）。</summary>
        private static int[] Triple(CombatLabSession s)
        {
            var hand = s.Snapshot().Hand;
            foreach (ElementKind el in System.Enum.GetValues(typeof(ElementKind)))
            {
                var idx = Enumerable.Range(0, hand.Count).Where(i => hand[i].Element == el).Take(3).ToArray();
                if (idx.Length == 3) return idx;
            }
            return null;
        }

        private static IEnumerable<string> ExpectedChips(IEnumerable<BattleEvent> events)
        {
            foreach (var ev in events)
            {
                string src = ev.Source ?? "";
                string pre = src.StartsWith("residue:") ? CombatDemoText.ChipResiduePrefix : "";
                switch (ev.Kind)
                {
                    case EventKind.Pressure: if (ev.Value > 0) yield return string.Format(CombatDemoText.ChipDisorder, "−" + ev.Value); break;
                    case EventKind.Conduction: if (ev.Value > 0) yield return string.Format(CombatDemoText.ChipConduction, "−" + ev.Value); break;
                    case EventKind.LinkChanged: if (ev.Amount != 0) yield return pre + string.Format(CombatDemoText.ChipLinks, CombatLabText.Signed(ev.Amount)); break;
                    case EventKind.CorrectionChanged: if (ev.Amount != 0) yield return string.Format(CombatDemoText.ChipCorrection, CombatLabText.Signed(ev.Amount)); break;
                    case EventKind.BindChanged: if (ev.Amount != 0) yield return string.Format(CombatDemoText.ChipBind, CombatLabText.Signed(ev.Amount)); break;
                    case EventKind.EnemyResolved: yield return CombatDemoText.ChipResolved; break;
                    case EventKind.EnemyActed: if (ev.Amount > 0) yield return string.Format(CombatDemoText.ChipAttack, ev.Amount); break;
                    case EventKind.ShieldChanged: if (ev.Amount != 0) yield return pre + string.Format(CombatDemoText.ChipShield, CombatLabText.Signed(ev.Amount)); break;
                    case EventKind.AnchorChanged: if (ev.Amount != 0) yield return string.Format(CombatDemoText.ChipAnchor, CombatLabText.Signed(ev.Amount)); break;
                }
            }
        }

        [Test]
        public void CastStrip_MatchesRealCards_LightsInOrder_ChipsMatchEvents()
        {
            var qa = NewQa();
            var hud = qa.Hud;
            var fx = hud.Fx;
            Step(qa);
            Assert.IsFalse(fx.CastVisible, "未施放時不顯示");
            hud.OnCard(0);
            hud.OnCard(1);
            var staged = qa.Arena.Session.Staged.Select(c => c.Element).ToArray();
            hud.OnCommit();
            Step(qa);
            Assert.IsTrue(fx.CastVisible);
            Assert.AreEqual(2, fx.CastCells, "張數＝實際結算的牌");
            for (int i = 0; i < 2; i++) StringAssert.Contains(CombatDemoTheme.ElementSymbol(staged[i]), fx.CellText(i));
            bool sawPartial = false;
            for (int i = 0; i < 3000 && qa.Arena.IsPlaying; i++)
            {
                Step(qa);
                if (fx.CellLit(0) && !fx.CellLit(1)) sawPartial = true;
                Assert.IsFalse(fx.CellLit(1) && !fx.CellLit(0), "依序點亮");
            }
            Step(qa);
            Assert.IsTrue(sawPartial, "播放中看得到逐張點亮");
            Assert.IsTrue(fx.CellLit(0) && fx.CellLit(1));
            var ev = qa.Arena.LastCommit.Resolution.Events;
            var card1 = ev.First(e => e.Source == "card:1" && (e.Kind == EventKind.CardResolved || e.Kind == EventKind.CardFizzled));
            StringAssert.Contains(card1.Kind == EventKind.CardFizzled ? CombatDemoText.CastFizzled : string.Format(CombatDemoText.CastValue, card1.Value), fx.CellText(1), "主要數值取自事件");
            CollectionAssert.AreEqual(ExpectedChips(ev).ToArray(), fx.ChipLog.ToArray(), "每個數值標籤對應一個實際事件");
            bool residue = ev.Any(e => e.Source == "residue:1");
            Assert.AreEqual(residue, fx.LinkText(0).StartsWith("承接 ✓"), "承接連線只在承接事件出現");
        }

        [Test]
        public void Resonance_ShowsAggregate_OnlyWhenCoreEmitsIt()
        {
            var qa = NewQa();
            var tri = Triple(qa.Arena.Session);
            Assume.That(tri != null, "這個 seed 的手牌沒有三張同元素");
            foreach (var i in tri) qa.Hud.OnCard(i);
            qa.Hud.OnCommit();
            bool aggregated = false;
            for (int i = 0; i < 3000 && qa.Arena.IsPlaying; i++)
            {
                Step(qa);
                if (qa.Hud.Fx.AggregateVisible) aggregated = true;
            }
            var ev = qa.Arena.LastCommit.Resolution.Events;
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.Resonance), "核心判定為共鳴");
            Assert.IsTrue(aggregated, "三連聚合演出");
            StringAssert.Contains("×" + qa.Arena.Session.Rules.TripleMultiplier.ToString("0.##"), qa.Hud.Fx.AggregateText, "倍率取自規則集");

            var other = NewQa(null, 99);
            other.Hud.OnCard(0);
            other.Hud.OnCommit();
            PlayOut(other);
            Assert.IsFalse(other.Hud.Fx.AggregateVisible, "一張牌不顯示共鳴");
        }

        [Test]
        public void Recap_SumsPlayedEvents_HidesWhenPlayerStartsNextPlan()
        {
            var qa = NewQa();
            qa.Hud.OnCard(0);
            qa.Hud.OnCard(1);
            qa.Hud.OnCard(2);
            qa.Hud.OnCommit();
            PlayOut(qa);
            var fx = qa.Hud.Fx;
            Assume.That(!qa.Arena.Session.IsOver);
            Assert.IsTrue(fx.RecapVisible, "回合結束顯示結果");
            var ev = qa.Arena.PlayedEvents;
            int dmg = ev.Where(e => e.Kind == EventKind.Pressure || e.Kind == EventKind.Conduction).Sum(e => e.Value);
            int shield = ev.Where(e => e.Kind == EventKind.ShieldChanged && e.Amount > 0).Sum(e => e.Amount);
            int anchor = ev.Where(e => e.Kind == EventKind.AnchorChanged).Sum(e => e.Amount);
            int residues = ev.Where(e => (e.Source ?? "").StartsWith("residue:")).Select(e => e.Source).Distinct().Count();
            StringAssert.Contains("失序值 −" + dmg, fx.RecapText);
            StringAssert.Contains("擬態錨定 +" + shield, fx.RecapText);
            StringAssert.Contains("承接 " + residues + " 次", fx.RecapText);
            StringAssert.Contains("錨定值 " + CombatLabText.Signed(anchor), fx.RecapText);
            var round = qa.Arena.Session.LastRound;
            Assert.AreEqual(round.AnchorBefore + anchor, qa.Arena.Session.Snapshot().Anchor, "事件加總與引擎結果一致");

            qa.Hud.OnCard(0);
            Step(qa);
            Assert.IsFalse(fx.RecapVisible, "開始新的編排即收起");
            Assert.IsFalse(fx.CastVisible);
        }

        [Test]
        public void Outcome_FadesHud_SoBannerIsFocus_ResetRestores()
        {
            var win = NewQa(QuickVictory(), 1);
            for (int i = 0; i < 3; i++) win.Hud.OnCard(i);
            win.Hud.OnCommit();
            PlayOut(win);
            for (int i = 0; i < 40; i++) Step(win);
            Assert.AreEqual(BattleOutcome.Victory, win.Arena.Session.Snapshot().Outcome);
            Assert.IsTrue(win.Arena.BannerVisible);
            Assert.Less(win.Hud.Fx.HudAlpha, 0.5f, "勝利時 HUD 淡出");
            Assert.IsFalse(win.Hud.Fx.RecapVisible, "勝負時不顯示回合結果，交給結果橫幅");
            win.ResetBattle();
            for (int i = 0; i < 40; i++) Step(win);
            Assert.AreEqual(1f, win.Hud.Fx.HudAlpha, 1e-3, "重置後恢復");

            var lose = NewQa(QuickDefeat(), 1);
            lose.Hud.OnCommit();
            PlayOut(lose);
            for (int i = 0; i < 40; i++) Step(lose);
            Assert.AreEqual(BattleOutcome.Defeat, lose.Arena.Session.Snapshot().Outcome);
            Assert.Less(lose.Hud.Fx.HudAlpha, 0.5f, "戰敗時 HUD 淡出");
            Assert.IsTrue(lose.Arena.BannerVisible);
        }

        [Test]
        public void ReduceMotion_NoMovementNoScale_SameInformation()
        {
            var calm = NewQa(null, 5);
            calm.ToggleReduceMotion();
            Assert.IsTrue(calm.ReduceMotion);
            Assert.IsTrue(calm.Hud.Options.ReduceMotion);
            var normal = NewQa(null, 5);
            foreach (var qa in new[] { calm, normal })
            {
                qa.Hud.OnCard(0);
                qa.Hud.OnCard(1);
                qa.Hud.OnCommit();
            }
            bool sawChip = false;
            for (int i = 0; i < 3000 && calm.Arena.IsPlaying; i++)
            {
                Step(calm);
                var fx = calm.Hud.Fx;
                for (int c = 0; c < fx.CastCells; c++) Assert.AreEqual(Vector3.one, fx.CellScale(c), "降低動態：不縮放");
                if (fx.LiveChips > 0)
                {
                    var p = fx.ChipPosition(0);
                    Step(calm);
                    if (fx.LiveChips > 0 && fx.ChipPosition(0) == p) sawChip = true;
                }
            }
            Assert.IsTrue(sawChip, "降低動態：標籤原地淡出");
            PlayOut(normal);
            Step(calm);
            CollectionAssert.AreEqual(normal.Hud.Fx.ChipLog.ToArray(), calm.Hud.Fx.ChipLog.ToArray(), "資訊量相同");
        }

        [Test]
        public void SkipAndRestart_LeaveNoChips()
        {
            var qa = NewQa(null, 3);
            qa.Hud.OnCard(0);
            qa.Hud.OnCard(1);
            qa.Hud.OnCommit();
            qa.Hud.OnCommit();
            Step(qa);
            Assert.IsFalse(qa.Arena.IsPlaying, "略過動畫");
            Assert.LessOrEqual(qa.Hud.Fx.LiveChips, 12, "一次播完也不堆滿畫面");
            for (int i = 0; i < 80; i++) Step(qa);
            Assert.AreEqual(0, qa.Hud.Fx.LiveChips, "標籤會自行消失");

            qa.Restart();
            Step(qa);
            Assert.AreEqual(0, qa.Hud.Fx.LiveChips);
            Assert.IsFalse(qa.Hud.Fx.CastVisible);
            Assert.AreEqual(1, qa.GetComponentsInChildren<RectTransform>(true).Count(r => r.name == "CastPanel"), "重來後只有一份施放條");
        }
    }
}
