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
    /// Case01 Demo 戰鬥 HUD：所有顯示都來自真實 Session／Core 預估，所有操作轉發給同一個 Session；
    /// 學派、手牌、序列、承接、共鳴、目標、AP 上限、調度、空序列、重複提交、目標先解除、勝負橫幅、版面與字級、舊 HUD 與 QA 入口。
    /// </summary>
    public sealed class Case01CombatDemoHudTests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();

        private Case01CombatDemoQA NewQa(CombatLabScenario scenario = null, int? seed = null)
        {
            var go = new GameObject("CombatDemoQATest");
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

        private static void Run(Case01CombatDemoQA qa, float s)
        {
            for (float t = 0; t < s; t += Dt)
            {
                qa.Arena.Tick(Dt);
                qa.Hud.Tick(Dt);
            }
        }

        private static int SlotOf(CombatDemoHud hud, ElementKind e, CombatLabSession s, int skip = 0)
        {
            var hand = s.Snapshot().Hand;
            for (int i = 0; i < hand.Count; i++)
                if (hand[i].Element == e && skip-- <= 0) return i;
            return -1;
        }

        // ---------------- 建立 ----------------

        [Test]
        public void Attach_HidesLegacyPanels_KeepsBannerAndFloaters_QaToolsOnlyInQa()
        {
            var qa = NewQa();
            var arena = qa.Arena;
            foreach (var p in arena.HudPanels()) Assert.IsFalse(p.gameObject.activeSelf, "舊 HUD 面板隱藏：" + p.name);
            Assert.IsTrue(arena.HudCanvas.enabled, "舊畫布保留（結果橫幅、浮字、畫面閃光）");
            Assert.Greater(qa.Hud.Canvas.sortingOrder, arena.HudCanvas.sortingOrder);
            Assert.IsTrue(qa.Hud.QaToolsVisible, "QA 場景顯示重置工具");
            Assert.AreEqual(7, qa.Hud.HandIds.Count);
            for (int i = 0; i < 7; i++) Assert.IsTrue(qa.Hud.CardVisible(i));
            StringAssert.Contains("錨定值 " + arena.Session.Snapshot().Anchor + " / " + arena.Session.Rules.MaxAnchor, qa.Hud.PlayerText, "錨定值有名字與數值");
            StringAssert.Contains("擬態錨定", qa.Hud.PlayerText);
            StringAssert.Contains("敵方回合", qa.Hud.PlayerText);
            Assert.AreEqual("3 / 3", qa.Hud.ApText);
            Assert.AreEqual(3, qa.Hud.ApLit);
            StringAssert.Contains("下回合攻擊 12", qa.Hud.PlateText(1), "敵人攻擊預告（真實值）");
            StringAssert.Contains("失序值 40 / 40", qa.Hud.PlateText(1));
        }

        [Test]
        public void Hand_ShowsElementSymbolAndCurrentSchoolEffect_SchoolSwitchRefreshesFromRules()
        {
            var qa = NewQa();
            var s = qa.Arena.Session;
            var hud = qa.Hud;
            var hand = s.Snapshot().Hand;
            for (int i = 0; i < hand.Count; i++)
            {
                var text = hud.CardText(i);
                StringAssert.Contains(CombatDemoTheme.ElementSymbol(hand[i].Element), text, "元素有符號，不只靠顏色");
                StringAssert.Contains(CombatLabText.EffectName(s.SelectedSchool, hand[i].Element), text);
                StringAssert.Contains(CombatLabText.EffectShort(s.Rules, s.SelectedSchool, hand[i].Element), text);
            }
            var before = hud.CardText(0);
            hud.OnSchool(SchoolKind.Relation);
            Assert.AreEqual(SchoolKind.Relation, s.SelectedSchool);
            StringAssert.Contains(CombatLabText.EffectName(SchoolKind.Relation, hand[0].Element), hud.CardText(0));
            Assert.AreNotEqual(before, hud.CardText(0), "學派改變相同元素的效果");
            StringAssert.Contains("● " + CombatLabText.School(SchoolKind.Relation), hud.SchoolText(2));
            foreach (var school in new[] { SchoolKind.Structure, SchoolKind.Process, SchoolKind.Relation })
            {
                hud.OnSchool(school);
                hud.OnClear();
                hud.OnCard(0);
                var f = s.Forecast;
                StringAssert.Contains("錨定值 " + f.AnchorBefore + "→" + f.AnchorAfter, hud.ForecastText, "預估直接取自 Core：" + school);
            }
        }

        [Test]
        public void Sequence_AddRemoveClear_AP_ResidueAndResonanceComeFromForecast()
        {
            var qa = NewQa();
            var s = qa.Arena.Session;
            var hud = qa.Hud;
            hud.OnCard(0);
            hud.OnCard(1);
            Assert.AreEqual(2, s.Staged.Count);
            Assert.AreEqual("1 / 3", hud.ApText);
            Assert.AreEqual(1, hud.ApLit);
            Assert.IsTrue(hud.CardStaged(0) && hud.CardStaged(1));
            StringAssert.Contains(CombatLabText.EffectName(s.SelectedSchool, s.Staged[0].Element), hud.SlotText(0));
            StringAssert.Contains("確認施放（2）", hud.CommitText);
            var r = s.Forecast.Residue(1);
            Assert.IsNotNull(r, "相鄰兩張必有承接");
            StringAssert.Contains("承接・" + CombatLabText.ResidueName(r.From), hud.LinkText(0));
            hud.OnCard(0);
            Assert.AreEqual(1, s.Staged.Count, "再點一次移除");
            hud.OnSlot(0);
            Assert.AreEqual(0, s.Staged.Count);
            Assert.IsFalse(hud.ClearInteractable);
            StringAssert.Contains("結束回合", hud.CommitText);

            // 三張同元素 → 共鳴（若這手牌有）。
            ElementKind? triple = null;
            foreach (ElementKind e in System.Enum.GetValues(typeof(ElementKind)))
                if (s.Snapshot().Hand.Count(c => c.Element == e) >= 3) triple = e;
            Assert.IsTrue(triple.HasValue, "標準種子的開局手牌有三張同元素");
            hud.OnCard(SlotOf(hud, triple.Value, s));
            hud.OnCard(SlotOf(hud, triple.Value, s, 1));
            StringAssert.Contains("再一張", hud.ResonanceText, "即將成立的提示");
            hud.OnCard(SlotOf(hud, triple.Value, s, 2));
            Assert.IsTrue(s.Forecast.Resonance);
            StringAssert.Contains("共鳴成立", hud.ResonanceText);
            // AP 上限：第四張被 Core 拒絕。
            int other = Enumerable.Range(0, 7).First(i => !s.IsStaged(s.Snapshot().Hand[i].Id));
            hud.OnCard(other);
            Assert.AreEqual(3, s.Staged.Count);
            StringAssert.Contains("AP 不足", hud.ToastText);
            hud.OnClear();
            Assert.AreEqual(0, s.Staged.Count);
        }

        [Test]
        public void Targets_PlateSelects_RetargetKeepsOrder_NonTargetCardsHaveNoChip()
        {
            var qa = NewQa();
            var s = qa.Arena.Session;
            var hud = qa.Hud;
            hud.OnPlate(2);
            Assert.AreEqual(2, s.SelectedTargetId);
            StringAssert.Contains("目標", hud.PlateText(2));
            int ember = SlotOf(hud, ElementKind.Ember, s);
            int baseCard = SlotOf(hud, ElementKind.Base, s);
            hud.OnCard(ember);
            hud.OnCard(baseCard);
            var first = s.Staged[0];
            Assert.AreEqual(2, first.TargetEnemyId);
            hud.OnRetarget(0);
            Assert.AreEqual(2, s.Staged.Count);
            Assert.AreEqual(first.CardId, s.Staged[0].CardId, "順序不變");
            Assert.AreEqual(1, s.Staged[0].TargetEnemyId, "目標更換");
            StringAssert.Contains("目標：甲", hud.SlotText(0));
            Assert.AreEqual(2, s.SelectedTargetId, "之後的預設目標不變");
            if (!s.CardNeedsTarget(ElementKind.Base)) StringAssert.Contains("不需目標", hud.SlotText(1));
        }

        [Test]
        public void Redraw_GoesThroughExistingLogic()
        {
            var qa = NewQa();
            var s = qa.Arena.Session;
            var hud = qa.Hud;
            int before = s.Snapshot().RedrawsRemaining;
            int id = s.Snapshot().Hand[3].Id;
            qa.Arena.RedrawButton.onClick.Invoke();
            hud.Refresh();
            Assert.IsTrue(qa.Arena.RedrawMode);
            StringAssert.Contains("取消調度", hud.RedrawText);
            hud.OnCard(3);
            Assert.IsFalse(qa.Arena.RedrawMode);
            Assert.AreEqual(before - 1, s.Snapshot().RedrawsRemaining);
            Assert.IsFalse(s.Snapshot().Hand.Any(c => c.Id == id), "洗回並重抽");
        }

        [Test]
        public void Commit_PlaysRealEvents_SecondClickSkips_NoDoubleSubmit_EmptySequenceEndsTurn()
        {
            var qa = NewQa();
            var s = qa.Arena.Session;
            var hud = qa.Hud;
            hud.OnCard(0);
            hud.OnCommit();
            Assert.IsTrue(qa.Arena.IsPlaying);
            Assert.IsTrue(qa.Arena.LastCommit.Accepted);
            Assert.AreEqual("略過動畫", hud.CommitText);
            Assert.AreEqual(1, s.LastRoundNumber);
            hud.OnCommit();
            Assert.IsFalse(qa.Arena.IsPlaying, "第二次按是略過動畫，不是再提交");
            Assert.AreEqual(1, s.LastRoundNumber);
            Run(qa, 0.1f);
            StringAssert.Contains("第 2 回合", hud.PlayerText);
            Assert.AreEqual(s.Snapshot().Anchor, qa.Arena.DisplayAnchor);
            hud.OnCommit();
            Assert.IsTrue(qa.Arena.LastCommit.Accepted, "空序列＝結束回合");
            qa.Arena.FinishPlayback();
            Assert.AreEqual(2, s.LastRoundNumber);
        }

        [Test]
        public void ResolvedTarget_PlateDisabled_VictoryAndDefeatUseExistingBanner()
        {
            var qa = NewQa(QuickVictory(), 1);
            var hud = qa.Hud;
            hud.OnCommit();
            qa.Arena.FinishPlayback();
            Run(qa, 0.1f);
            Assert.AreEqual(BattleOutcome.Victory, qa.Arena.Session.Snapshot().Outcome);
            StringAssert.Contains("已解除", hud.PlateText(1));
            Assert.IsTrue(qa.Arena.BannerVisible, "既有勝利橫幅");
            Assert.AreEqual("戰鬥結束", hud.CommitText);
            Assert.IsFalse(hud.CommitInteractable);

            var lose = NewQa(QuickDefeat(), 1);
            lose.Hud.OnCommit();
            lose.Arena.FinishPlayback();
            Assert.AreEqual(BattleOutcome.Defeat, lose.Arena.Session.Snapshot().Outcome);
            Assert.IsTrue(lose.Arena.BannerVisible);
        }

        [Test]
        public void Layout_PanelsDoNotOverlap_StayOnScreen_FontsAtLeast16()
        {
            var qa = NewQa();
            var hud = qa.Hud;
            var rects = new List<Rect>();
            foreach (var p in hud.Panels())
            {
                var r = RefRect(p, 1600f, 900f);
                Assert.GreaterOrEqual(r.xMin, 0f, p.name);
                Assert.GreaterOrEqual(r.yMin, 0f, p.name);
                Assert.LessOrEqual(r.xMax, 1600f, p.name);
                Assert.LessOrEqual(r.yMax, 900f, p.name);
                foreach (var o in rects) Assert.IsFalse(o.Overlaps(r), p.name + " 與其他面板重疊");
                rects.Add(r);
            }
            // 戰場中央（玩家與敵人所在區域）沒有固定面板。
            var center = new Rect(420f, 330f, 760f, 240f);
            foreach (var r in rects) Assert.IsFalse(r.Overlaps(center), "固定面板遮住戰場中央");
            foreach (var t in hud.Canvas.GetComponentsInChildren<Text>(true)) Assert.GreaterOrEqual(t.fontSize, 16, t.name);
        }

        /// <summary>面板在參考解析度下的矩形（左下為原點；1280×720 與 1920×1080 都是 16:9，參考座標相同）。</summary>
        private static Rect RefRect(RectTransform rt, float w, float h)
        {
            var size = rt.sizeDelta;
            float x = rt.anchorMin.x * w + rt.anchoredPosition.x - rt.pivot.x * size.x;
            float y = rt.anchorMin.y * h + rt.anchoredPosition.y - rt.pivot.y * size.y;
            return new Rect(x, y, size.x, size.y);
        }

        [Test]
        public void QaScene_RestartResetLegacyAndTutorialToggle()
        {
            var qa = NewQa();
            qa.Hud.OnCard(0);
            qa.Hud.OnCommit();
            qa.Arena.FinishPlayback();
            qa.ResetBattle();
            Assert.AreEqual(0, qa.Arena.Session.Snapshot().Round, "F9 重置戰鬥");
            var oldHud = qa.Hud;
            qa.Restart();
            Assert.AreNotSame(oldHud, qa.Hud);
            Assert.AreEqual(1, qa.Rebuilds);
            qa.ToggleLegacy();
            Assert.IsNull(qa.Hud, "F10：舊版 HUD 對照");
            Assert.IsTrue(qa.Arena.HudPanels().All(p => p.name == "LogPanel" || p.gameObject.activeSelf), "舊版面板照常顯示");
            qa.ToggleLegacy();
            Assert.IsNotNull(qa.Hud);
            bool tut = qa.TutorialEnabled;
            qa.ToggleTutorial();
            Assert.AreNotEqual(tut, qa.TutorialEnabled);
        }

        [Test]
        public void Case01HouseBattle_UsesDemoHud_WithoutQaTools()
        {
            var go = new GameObject("Case01HouseForDemoHud");
            roots.Add(go);
            var house = go.AddComponent<Case01HouseQA>();
            house.Build(false, QuickVictory(), 1, Case01Checkpoint.S40);
            house.Flow.Interact(Case01Ids.Core);
            house.Player.Teleport(new Vector3(40f, 0.05f, 40f), 0f);
            house.Tick(Dt, Case01Input.None);
            house.Tick(Dt, Case01Input.None);
            if (house.Mode == Case01Mode.Manifesting) house.Tick(Dt, Case01Input.From(new ExploreInput { Interact = true }));
            for (int i = 0; i < 120 && house.Mode == Case01Mode.Exploring; i++) house.Tick(Dt, Case01Input.From(ExploreInput.Forward()));
            for (int i = 0; i < 120 && house.Mode != Case01Mode.Battle; i++) house.Tick(Dt, Case01Input.None);
            Assert.AreEqual(Case01Mode.Battle, house.Mode);
            Assert.IsNotNull(house.DemoHud, "Case01 戰鬥使用 Demo HUD");
            Assert.IsFalse(house.DemoHud.QaToolsVisible, "正式路徑不顯示重置戰鬥");
            Assert.IsTrue(house.DemoHud.Canvas.enabled);
            house.DemoHud.OnCommit();
            for (int i = 0; i < 300 && house.Arena.IsPlaying; i++) house.Tick(Dt, Case01Input.None);
            Assert.IsTrue(house.Arena.BannerVisible);
            house.Arena.BannerButton.onClick.Invoke();
            for (int i = 0; i < 120 && house.Mode != Case01Mode.Exploring; i++) house.Tick(Dt, Case01Input.None);
            Assert.AreEqual(Case01Mode.Exploring, house.Mode, "勝利後回到探索（原流程）");
            Assert.IsTrue(house.DemoHud == null, "戰鬥物件與 HUD 一起銷毀");
        }
    }
}
