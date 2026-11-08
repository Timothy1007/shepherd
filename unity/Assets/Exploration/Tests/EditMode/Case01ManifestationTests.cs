using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Battle.Core;
using Shepherd.Battle.UI;
using Shepherd.Exploration.Case01;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>v0.3 Art Pass B：夾層散落物、失序體由碎片凝聚成形（一次、可跳過、靜音不影響）、音量分級、美術 QA 模式。</summary>
    public sealed class Case01ManifestationTests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();

        private Case01HouseQA NewQa(Case01Checkpoint? cp = null, CombatLabScenario scenario = null, Case01QaMode? mode = null)
        {
            var go = new GameObject("Case01ManifestTest");
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

        private static void Run(Case01HouseQA qa, float s, Case01Input? input = null)
        {
            for (float t = 0; t < s; t += Dt) qa.Tick(Dt, input ?? Case01Input.None);
        }

        private static void RunUntil(Case01HouseQA qa, System.Func<bool> done, string what, float max = 12f)
        {
            for (float t = 0; t < max && !done(); t += Dt) qa.Tick(Dt, Case01Input.None);
            Assert.IsTrue(done(), "逾時：" + what + "（模式 " + qa.Mode + "）");
        }

        /// <summary>S40：碰核 → 反制 → 被拉進中庭，停在形成演出開始。</summary>
        private static void PullIntoCourtyard(Case01HouseQA qa)
        {
            qa.Player.Teleport(Case01HouseBuilder.MezzCenter + new Vector3(0f, 0.05f, -0.4f), 0f, 22f);
            qa.Tick(Dt, Case01Input.None);
            Assert.AreEqual(Case01Ids.Core, qa.CurrentTarget != null ? qa.CurrentTarget.Id : null);
            qa.Tick(Dt, Case01Input.From(new ExploreInput { Interact = true }));
            RunUntil(qa, () => qa.Mode == Case01Mode.Manifesting, "開始形成演出");
        }

        private static void WalkForwardUntilBattle(Case01HouseQA qa)
        {
            qa.Player.Teleport(qa.Player.transform.position, 0f);
            for (int i = 0; i < 600 && qa.Mode == Case01Mode.Exploring; i++) qa.Tick(Dt, Case01Input.From(ExploreInput.Forward()));
        }

        [Test]
        public void Mezzanine_HasScatteredBooksPhotosPapers_AwayFromHotspotsAndPaths()
        {
            var qa = NewQa();
            var scatter = qa.Layout.Root.Find("LifeDetails/MezzanineScatter");
            Assert.IsNotNull(scatter);
            Assert.GreaterOrEqual(scatter.childCount, 9);
            Assert.AreEqual(0, scatter.GetComponentsInChildren<Collider>().Length, "只是外觀，不擋路");
            Assert.Greater(scatter.GetComponentsInChildren<Transform>().Count(t => t.name == "PhotoCard"), 6, "有照片");
            foreach (Transform pile in scatter)
            {
                foreach (var id in new[] { Case01Ids.Core, Case01Ids.MezzTriangleMark, Case01Ids.MezzCircleMark, Case01Ids.Wall17, Case01Ids.EvidencePhoto })
                {
                    var h = qa.Layout.Interactables[id].transform.position;
                    Assert.Greater(Vector2.Distance(new Vector2(pile.position.x, pile.position.z), new Vector2(h.x, h.z)), 0.9f, pile.name + " 離 " + id);
                }
            }
        }

        [Test]
        public void Manifestation_PlaysOnce_InFourBeats_ThenExistingEncounterStartsBattle()
        {
            var qa = NewQa(Case01Checkpoint.S40);
            PullIntoCourtyard(qa);
            var m = qa.Manifestation;
            Assert.IsNotNull(m);
            Assert.AreEqual(2 * Case01Manifestation.FragmentsPerFigure, m.FragmentCount, "兩個個體各自成形");
            Assert.AreEqual(2, m.Figures.Count);
            Assert.IsFalse(m.Figures[0].gameObject.activeSelf, "一開始還沒有實體（不是兩個方塊直接升起）");
            Assert.IsFalse(qa.Session.Has(Case01Flags.BattleStarted), "演出中不偷偷開戰");
            var start = qa.Player.transform.position;
            Run(qa, 1.5f, Case01Input.From(ExploreInput.Forward()));
            Assert.AreEqual(1, m.Phase);
            var moved = qa.Player.transform.position - start;
            Assert.Less(new Vector2(moved.x, moved.z).magnitude, 0.01f, "演出中不能移動（也不追玩家）");
            Run(qa, 1.6f);
            Assert.AreEqual(2, m.Phase);
            RunUntil(qa, () => qa.Mode == Case01Mode.Exploring, "演出結束");
            Assert.IsTrue(m.Done);
            Assert.IsFalse(m.Skipped);
            Assert.IsTrue(m.Figures.All(f => f.gameObject.activeSelf && f.localScale.y > 0.5f), "陰影凝實");
            CollectionAssert.AreEqual(new[] { "Case01Rustle", "Case01Whoosh", "Case01Assemble", "Case01Sting", "Case01Lock" }, qa.Audio.Played.ToArray());
            // 碎片留在身上成為外殼。
            var shell = m.Shells[0].GetChild(0).position;
            Assert.Less(Vector3.Distance(shell, m.Figures[0].position), 2.6f, "碎片貼在人形輪廓上");

            WalkForwardUntilBattle(qa);
            Assert.AreEqual(Case01Mode.EnteringBattle, qa.Mode, "依原本的遭遇距離開戰");
            Assert.IsFalse(m.Root.gameObject.activeSelf, "戰鬥中外殼隱藏");
            Assert.AreEqual(1, qa.Encounter.TriggerCount);
        }

        [Test]
        public void Manifestation_CanBeSkipped_AndMutedAudioDoesNotBlockFlow()
        {
            var qa = NewQa(Case01Checkpoint.S40);
            qa.SetVolumes(0f, 0f, 0f);
            PullIntoCourtyard(qa);
            Run(qa, 0.3f);
            qa.Tick(Dt, Case01Input.From(new ExploreInput { Interact = true }));
            Assert.IsTrue(qa.Manifestation.Done);
            Assert.IsTrue(qa.Manifestation.Skipped);
            Assert.AreEqual(Case01Mode.Exploring, qa.Mode);
            CollectionAssert.DoesNotContain(qa.Audio.Played, "Case01Whoosh", "跳過不補播中間的聲音");
            Assert.AreEqual("Case01Lock", qa.Audio.Played.Last(), "只播扣合聲");
            Assert.AreEqual(0f, qa.Audio.EffectGain);
            WalkForwardUntilBattle(qa);
            Assert.AreEqual(Case01Mode.EnteringBattle, qa.Mode, "靜音也能正常開戰");
        }

        [Test]
        public void Manifestation_NotReplayedOnDefeatRetry_AndClearedByF1()
        {
            var s = new CombatLabScenario { Name = "quick-defeat" };
            s.Enemies.Add(new EnemyState { Id = 1, Name = "E1 失序體・甲", Value = 500, AttackPattern = new[] { 999 } });
            var qa = NewQa(Case01Checkpoint.S40, s);
            PullIntoCourtyard(qa);
            qa.Tick(Dt, Case01Input.From(new ExploreInput { Interact = true }));
            WalkForwardUntilBattle(qa);
            RunUntil(qa, () => qa.Mode == Case01Mode.Battle, "進入戰鬥");
            var arena = qa.Arena;
            arena.CommitButton.onClick.Invoke();
            RunUntil(qa, () => !arena.IsPlaying, "事件播放", 30f);
            arena.BannerButton.onClick.Invoke();
            qa.Tick(Dt, Case01Input.None);
            int played = qa.Audio.Played.Count;
            Run(qa, 0.5f);
            Assert.AreEqual(Case01Mode.Exploring, qa.Mode, "重試時不重播形成");
            Assert.IsTrue(qa.Manifestation.Done);
            Assert.IsTrue(qa.Manifestation.Root.gameObject.activeSelf, "失序體與外殼都在");
            Assert.AreEqual(played, qa.Audio.Played.Count, "重試不播聲音");

            qa.Tick(Dt, new Case01Input { Jump = 1 });
            Assert.IsNull(qa.Manifestation);
            Assert.IsFalse(qa.Manifested);
            Assert.AreEqual(0, Object.FindObjectsOfType<Transform>().Count(t => t.name == "Manifestation"), "F1 不殘留碎片");
            Assert.IsFalse(qa.Audio.Music.isPlaying);
        }

        [Test]
        public void ArtPreviewMode_CyclesHouseStages_AndPreviewsManifestationWithoutBattle()
        {
            var qa = NewQa(null, null, Case01QaMode.ArtPreview);
            StringAssert.Contains("F6", qa.Hud.Base.ViewMode.text);
            Assert.AreEqual(0, qa.HouseStage);
            for (int h = 1; h <= 3; h++)
            {
                qa.Tick(Dt, new Case01Input { Function = 6 });
                Assert.AreEqual(h, qa.ArtStage);
                Assert.AreEqual(h, qa.HouseStage, "F6 → H" + h);
                Assert.AreEqual(Case01Room.Living, qa.CurrentRoom);
            }
            Assert.IsTrue(qa.Layout.Decor.LivingFrameBlank.activeSelf, "H3 殘留");
            qa.Tick(Dt, new Case01Input { Function = 6 });
            Assert.AreEqual(0, qa.HouseStage);

            qa.Tick(Dt, new Case01Input { Function = 7 });
            Assert.AreEqual(Case01Room.Courtyard, qa.CurrentRoom);
            qa.Tick(Dt, Case01Input.None);
            Assert.AreEqual(Case01Mode.Manifesting, qa.Mode);
            RunUntil(qa, () => qa.Mode == Case01Mode.Exploring, "演出結束");
            WalkForwardUntilBattle(qa);
            Assert.AreEqual(Case01Mode.Exploring, qa.Mode, "美術 QA 不開戰");
            Assert.IsFalse(qa.Session.Has(Case01Flags.BattleStarted));
            qa.Tick(Dt, new Case01Input { Function = 7 });
            qa.Tick(Dt, Case01Input.None);
            Assert.AreEqual(Case01Mode.Manifesting, qa.Mode, "可重複預覽");
        }

        [Test]
        public void ReduceFlickerKey_And_StandardModeKeepsArtKeysOff()
        {
            var qa = NewQa();
            qa.Tick(Dt, new Case01Input { Function = 8 });
            Assert.IsTrue(qa.ReduceFlicker);
            qa.Tick(Dt, new Case01Input { Function = 6 });
            Assert.AreEqual(0, qa.ArtStage, "一般模式不處理美術 QA 鍵");
            Assert.AreEqual(Case01Stage.EnteredHome, qa.Session.Stage);
        }
    }
}
