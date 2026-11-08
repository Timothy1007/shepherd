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
    /// v0.5 Phase 2：失序體形成不露出舊的黑色人形（Hidden → DebrisOnly → Assembling → Manifested → Battle），
    /// 進戰鬥後同一份碎片外殼仍在敵人身上；E 跳過／戰敗重試／QA 跳轉一致；不改敵人數量、ID 與戰鬥結算。
    /// </summary>
    public sealed class Case01EnemyContinuityTests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();

        private Case01HouseQA NewQa(Case01Checkpoint? cp = null, CombatLabScenario scenario = null, Case01QaMode? mode = null)
        {
            var go = new GameObject("Case01EnemyContinuityTest");
            roots.Add(go);
            var qa = go.AddComponent<Case01HouseQA>();
            qa.Build(false, scenario, 1, cp, mode);
            return qa;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
        }

        private static void RunUntil(Case01HouseQA qa, System.Func<bool> done, string what, float max = 12f)
        {
            for (float t = 0; t < max && !done(); t += Dt) qa.Tick(Dt, Case01Input.None);
            Assert.IsTrue(done(), "逾時：" + what + "（模式 " + qa.Mode + "）");
        }

        /// <summary>中庭裡看得見的「舊黑色人形」：啟用中、尺寸不是 0、但身上沒有聚攏的碎片外殼。</summary>
        private static Transform CourtyardGroup(Case01HouseQA qa)
        {
            return qa.Layout.Root.parent.Find("CourtyardEnemies");
        }

        private static IEnumerable<Transform> VisibleFigures(Case01HouseQA qa)
        {
            var g = CourtyardGroup(qa);
            if (g == null || !g.gameObject.activeInHierarchy) yield break;
            foreach (Transform f in g)
                if (f.gameObject.activeInHierarchy && f.localScale.y > 0.01f)
                    yield return f;
        }

        /// <summary>碰核 → 反制（移動暫停）；回傳時還沒被拉進中庭。</summary>
        private static void TouchCore(Case01HouseQA qa)
        {
            qa.Player.Teleport(Case01HouseBuilder.MezzCenter + new Vector3(0f, 0.05f, -0.4f), 0f, 22f);
            qa.Tick(Dt, Case01Input.None);
            qa.Tick(Dt, Case01Input.From(new ExploreInput { Interact = true }));
        }

        [Test]
        public void BeforeCourtyard_OnlyDebris_NoBlackFigureEverShown()
        {
            var qa = NewQa(Case01Checkpoint.S40);
            Assert.AreEqual(Case01EnemyVisual.Hidden, qa.EnemyVisual, "反制前看不到任何失序體");
            TouchCore(qa);
            // 反制、黑幕傳送、進中庭：每一影格都檢查。
            for (int i = 0; i < 600 && qa.Mode != Case01Mode.Manifesting; i++)
            {
                qa.Tick(Dt, Case01Input.None);
                if (qa.Mode == Case01Mode.Manifesting) break;
                Assert.AreEqual(0, VisibleFigures(qa).Count(), "形成前不出現人形（影格 " + i + "、模式 " + qa.Mode + "、狀態 " + qa.EnemyVisual + "）");
                if (qa.Mode == Case01Mode.Defense)
                    Assert.AreEqual(Case01EnemyVisual.Hidden, qa.EnemyVisual, "反制演出中中庭還沒有東西");
                else if (qa.Flow.CanStartBattle)
                    Assert.AreEqual(Case01EnemyVisual.DebrisOnly, qa.EnemyVisual, "被拉進中庭時地上先有碎片");
            }
            Assert.AreEqual(Case01Mode.Manifesting, qa.Mode);
            Assert.AreEqual(0, VisibleFigures(qa).Count(), "演出第 0 幀不露黑佔位");
        }

        [Test]
        public void Assembling_FigureOnlyAppearsInsideGatheredShell()
        {
            var qa = NewQa(Case01Checkpoint.S40);
            TouchCore(qa);
            RunUntil(qa, () => qa.Mode == Case01Mode.Manifesting, "開始形成演出");
            var m = qa.Manifestation;
            bool sawAssembling = false;
            for (int i = 0; i < 400 && qa.Mode == Case01Mode.Manifesting; i++)
            {
                qa.Tick(Dt, Case01Input.None);
                if (qa.EnemyVisual == Case01EnemyVisual.Assembling) sawAssembling = true;
                for (int f = 0; f < m.Figures.Count; f++)
                {
                    var fig = m.Figures[f];
                    if (!fig.gameObject.activeInHierarchy || fig.localScale.y < 0.3f) continue;
                    Assert.Less(m.MeanShellGap(f), 0.5f, "人形可見（" + fig.localScale.y.ToString("0.00") + "）時碎片已聚在輪廓上（t=" + m.Time.ToString("0.00") + "）");
                }
            }
            Assert.IsTrue(sawAssembling);
            Assert.AreEqual(Case01EnemyVisual.Manifested, qa.EnemyVisual);
            Assert.AreEqual(2, VisibleFigures(qa).Count());
            for (int f = 0; f < m.Figures.Count; f++) Assert.Less(m.MeanShellGap(f), 0.001f, "完成時外殼完整");
        }

        [Test]
        public void SkipWithE_JumpsToFinishedLook_WithoutBareFigure()
        {
            var qa = NewQa(Case01Checkpoint.S40);
            TouchCore(qa);
            RunUntil(qa, () => qa.Mode == Case01Mode.Manifesting, "開始形成演出");
            qa.Tick(Dt, Case01Input.None);
            Assert.AreEqual(0, VisibleFigures(qa).Count());
            qa.Tick(Dt, Case01Input.From(new ExploreInput { Interact = true }));
            Assert.AreEqual(Case01EnemyVisual.Manifested, qa.EnemyVisual);
            var m = qa.Manifestation;
            for (int f = 0; f < m.Figures.Count; f++) Assert.Less(m.MeanShellGap(f), 0.001f, "跳過後直接是完成外觀");
        }

        [Test]
        public void Battle_KeepsTheSameFragmentShell_OnTheSameEnemies()
        {
            var qa = NewQa(Case01Checkpoint.S40);
            TouchCore(qa);
            RunUntil(qa, () => qa.Mode == Case01Mode.Manifesting, "開始形成演出");
            qa.Tick(Dt, Case01Input.From(new ExploreInput { Interact = true }));
            var m = qa.Manifestation;
            // 探索中外殼相對於人形的位置。
            var explore = new List<List<Vector3>>();
            for (int f = 0; f < m.Figures.Count; f++)
                explore.Add(m.Shells[f].Cast<Transform>().Select(t => m.Figures[f].InverseTransformPoint(t.position)).ToList());

            Assert.IsTrue(qa.StartEncounter());
            Assert.AreEqual(Case01EnemyVisual.Battle, qa.EnemyVisual);
            var arena = qa.Arena;
            Assert.AreEqual(2, arena.EnemyCount, "敵人數量不變");
            var scenario = CombatLabScenario.Default();
            for (int i = 0; i < scenario.Enemies.Count; i++)
            {
                var root = arena.EnemyRoot(scenario.Enemies[i].Id);
                Assert.IsNotNull(root, "敵人 ID 不變");
                var shell = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Case01Shell");
                Assert.IsNotNull(shell, "戰鬥敵人身上有碎片外殼");
                var battle = shell.Cast<Transform>().Select(t => root.InverseTransformPoint(t.position)).ToList();
                Assert.AreEqual(explore[i].Count, battle.Count, "碎片數量一致");
                for (int k = 0; k < battle.Count; k++)
                    Assert.Less(Vector3.Distance(explore[i][k], battle[k]), 0.01f, "第 " + i + " 個失序體的第 " + k + " 片位置一致");
                Assert.AreEqual(0, shell.GetComponentsInChildren<Collider>().Length, "外殼不加碰撞體（不改點選判定）");
                foreach (var part in Case01EnemyShell.BareParts)
                {
                    Assert.IsFalse(root.Find("Body/" + part).GetComponent<Renderer>().enabled, "戰鬥中不露出黑色裸身：" + part);
                    Assert.IsFalse(m.Figures[i].Find("Body/" + part).GetComponent<Renderer>().enabled, "探索中也一樣：" + part);
                }
                Assert.IsNotNull(root.GetComponent<Collider>(), "點選用的碰撞體還在");
            }
            Assert.IsFalse(m.Root.gameObject.activeSelf, "探索中的外殼在戰鬥時隱藏（不重疊）");
            Assert.IsFalse(CourtyardGroup(qa).gameObject.activeSelf);
        }

        [Test]
        public void DefeatRetry_RebuildsFinishedLook_AndNextBattleHasShellAgain()
        {
            var s = new CombatLabScenario { Name = "quick-defeat" };
            s.Enemies.Add(new EnemyState { Id = 1, Name = "E1 失序體・甲", Value = 500, AttackPattern = new[] { 999 } });
            var qa = NewQa(Case01Checkpoint.S40, s);
            TouchCore(qa);
            RunUntil(qa, () => qa.Mode == Case01Mode.Manifesting, "開始形成演出");
            qa.Tick(Dt, Case01Input.From(new ExploreInput { Interact = true }));
            Assert.IsTrue(qa.StartEncounter());
            RunUntil(qa, () => qa.Mode == Case01Mode.Battle, "進入戰鬥");
            var arena = qa.Arena;
            arena.CommitButton.onClick.Invoke();
            RunUntil(qa, () => !arena.IsPlaying, "事件播放", 30f);
            arena.BannerButton.onClick.Invoke();
            qa.Tick(Dt, Case01Input.None);
            Assert.AreEqual(Case01Mode.Exploring, qa.Mode);
            Assert.AreEqual(Case01EnemyVisual.Manifested, qa.EnemyVisual, "重試：直接是完成外觀");
            Assert.AreEqual(1, VisibleFigures(qa).Count());
            Assert.Less(qa.Manifestation.MeanShellGap(0), 0.001f);
            Assert.IsTrue(qa.StartEncounter());
            Assert.IsNotNull(qa.Arena.EnemyRoot(1).GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Case01Shell"), "再戰仍有外殼");
        }

        [Test]
        public void QaJumps_AndArtPreview_NeverShowBareFigure()
        {
            var qa = NewQa(null, null, Case01QaMode.ArtPreview);
            qa.Tick(Dt, new Case01Input { Function = 7 });
            Assert.AreEqual(Case01EnemyVisual.DebrisOnly, qa.EnemyVisual, "F7 預覽：先只有碎片");
            Assert.AreEqual(0, VisibleFigures(qa).Count());
            qa.Tick(Dt, Case01Input.None);
            Assert.AreEqual(Case01Mode.Manifesting, qa.Mode);
            Assert.AreEqual(0, VisibleFigures(qa).Count());
            qa.Tick(Dt, new Case01Input { Jump = 4 });
            Assert.AreEqual(Case01EnemyVisual.Hidden, qa.EnemyVisual, "F4 戰後：沒有失序體");
            Assert.AreEqual(0, VisibleFigures(qa).Count());
            qa.Tick(Dt, new Case01Input { Jump = 3 });
            Assert.AreEqual(Case01EnemyVisual.Hidden, qa.EnemyVisual, "F3 反制前：沒有失序體");
        }

        [Test]
        public void ShellLayout_IsDeterministicPerEnemy()
        {
            var a = Case01EnemyShell.Layout(0, 1f);
            var b = Case01EnemyShell.Layout(0, 1f);
            var c = Case01EnemyShell.Layout(1, 1f);
            Assert.AreEqual(Case01EnemyShell.PiecesPerFigure, a.Count);
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i].LocalPos, b[i].LocalPos);
            Assert.AreNotEqual(a[0].LocalPos, c[0].LocalPos, "兩個個體的外殼不完全相同");
        }
    }
}
