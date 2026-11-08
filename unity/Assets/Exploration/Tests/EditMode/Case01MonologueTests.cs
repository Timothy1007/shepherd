using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// v0.4 Stage A：主角自語取代第一章的同伴解題通訊。事件觸發、每個 ID 只說一次、不洩漏解法、
    /// 字幕 2～5 秒可略過、面板／檢視／轉場時暫停、戰鬥與 F1～F5 跳轉清除。
    /// </summary>
    public sealed class Case01MonologueTests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
        }

        private Case01HouseQA NewQa(Case01Checkpoint? cp = null, Case01QaMode? mode = null)
        {
            var go = new GameObject("Case01MonologueTest");
            roots.Add(go);
            var qa = go.AddComponent<Case01HouseQA>();
            qa.Build(false, null, null, cp, mode);
            return qa;
        }

        private static List<string> Ids(Case01Response r) { return r.Monologue ?? new List<string>(); }

        [Test]
        public void EveryLine_Exists_IsShort_AndNeverGivesTheAnswer()
        {
            foreach (var id in Case01Say.All)
            {
                var line = Case01Texts.SayLine(id);
                Assert.IsFalse(string.IsNullOrEmpty(line), id);
                Assert.LessOrEqual(line.Length, 40, "1～2 句：" + id);
                // 不報解法：不出現記號、箭頭、「選／錨定到」等直接指示。
                foreach (var banned in new[] { "△", "○", "□", "→", "選", "錨定到", "同伴" })
                    Assert.IsFalse(line.Contains(banned), id + " 不應包含「" + banned + "」");
                float s = Case01Hud.MonologueSeconds(line);
                Assert.That(s, Is.InRange(2.5f, 5f), "每句 2～5 秒");
            }
            Assert.AreEqual(Case01Say.All.Length, Case01Say.All.Distinct().Count());
        }

        [Test]
        public void Flow_EventLines_FireOnce_InCausalOrder_NoCompanionRadio()
        {
            var f = new Case01Flow();
            CollectionAssert.AreEqual(new[] { Case01Say.EnterHome }, Ids(f.OnArrive()));
            Assert.AreEqual(0, Ids(f.OnArrive()).Count, "進門只說一次");

            f.EnterRoom(Case01Room.Entrance, Case01Room.Dining);
            f.Interact(Case01Ids.Table);
            f.Interact(Case01Ids.Photo);
            f.EnterRoom(Case01Room.Dining, Case01Room.Entrance);
            f.ApplyDiningRewrite();
            var back = f.EnterRoom(Case01Room.Entrance, Case01Room.Dining);
            CollectionAssert.Contains(Ids(back), Case01Say.TableRewritten);
            Assert.IsNull(back.Radio);
            CollectionAssert.Contains(Ids(f.EnterRoom(Case01Room.Dining, Case01Room.Living)), Case01Say.H1Cold, "H1 冷落感");

            f.Interact(Case01Ids.Map);
            f.Interact(Case01Ids.EllieNotes);
            var armed = f.Interact(Case01Ids.OldRecord);
            Assert.IsTrue(f.WrongConnectionArmed);
            CollectionAssert.Contains(Ids(armed), Case01Say.Armed);
            Assert.IsNull(armed.Radio, "樓梯異常提示改由主角說");
            Assert.IsFalse(f.Session.HasJournal("radio_armed"));

            var landing = f.EnterRoom(Case01Room.Study, Case01Room.Landing);
            CollectionAssert.IsSubsetOf(new[] { Case01Say.H2Landing, Case01Say.ThirdDoor }, Ids(landing));
            var wrong1 = f.Traverse(Case01Door.StudyDoor);
            CollectionAssert.Contains(Ids(wrong1), Case01Say.WrongFirst, "第一次錯接");
            Assert.IsTrue(f.Session.HasJournal("say_" + Case01Say.WrongFirst), "重要自語記入日誌");
            f.EnterRoom(Case01Room.Landing, Case01Room.Dining);
            var wrong2 = f.Traverse(Case01Door.StudyDoor);
            CollectionAssert.Contains(Ids(wrong2), Case01Say.WrongSecond, "第二次錯接");
            Assert.AreEqual(0, Ids(f.Traverse(Case01Door.StudyDoor)).Count, "之後穿門不再重播");
        }

        [Test]
        public void Core_AfterBattle_SaysBattleIsNotTheFix_AndAsksToCompareRecords_BothModes()
        {
            foreach (var mode in new[] { Case01CorrectionMode.LegacyV02, Case01CorrectionMode.RevisedV03 })
            {
                var f = Case01Flow.ForCheckpoint(Case01Checkpoint.S40, mode);
                var d = f.Interact(Case01Ids.Core);
                CollectionAssert.Contains(Ids(d), Case01Say.Defense, mode + "：反制");
                Assert.IsNull(d.Radio);
                f.OnBattleStarted();
                f.OnBattleWon();
                var c = f.Interact(Case01Ids.Core);
                CollectionAssert.Contains(Ids(c), Case01Say.CoreAfterBattle, mode + "：戰鬥≠解除");
                Assert.AreEqual(0, Ids(f.Interact(Case01Ids.Core)).Count(i => i == Case01Say.CoreAfterBattle), "只說一次");
            }

            // 缺證據：先比對記錄（不報答案）。
            var g = new Case01Flow(Case01CorrectionMode.RevisedV03);
            g.Interact(Case01Ids.Map);
            g.Interact(Case01Ids.EllieNotes);
            g.Interact(Case01Ids.OldRecord);
            g.EnterRoom(Case01Room.Study, Case01Room.Landing);
            g.Traverse(Case01Door.StudyDoor);
            g.Choose(Case01Ids.ChoiceOverride, 0);
            g.Traverse(Case01Door.ThirdDoor);
            g.EnterRoom(Case01Room.Landing, Case01Room.Mezzanine);
            g.Interact(Case01Ids.Core);
            g.OnBattleStarted();
            g.OnBattleWon();
            var need = g.Interact(Case01Ids.Core);
            StringAssert.Contains("記錄還推不出來", need.Text);
            CollectionAssert.Contains(Ids(need), Case01Say.NeedEvidence);
            Assert.IsTrue(g.Session.HasJournal("say_" + Case01Say.NeedEvidence));
        }

        [Test]
        public void V03_GuidanceLines_WrongPreview_Anchor_Second_Commit_ThenH3()
        {
            var f = Case01Flow.ForCheckpoint(Case01Checkpoint.S50, Case01CorrectionMode.RevisedV03);
            f.Traverse(Case01Door.MezzTriangle);
            f.Interact(Case01Ids.Core);
            var wrong = f.Choose(Case01IdsV03.ChoiceAnchor, 1);
            CollectionAssert.Contains(Ids(wrong), Case01Say.V03Wrong);
            f.Choose(Case01IdsV03.ChoiceUndo, 0);
            CollectionAssert.Contains(Ids(f.Choose(Case01IdsV03.ChoiceAnchor, 0)), Case01Say.V03Anchor);
            CollectionAssert.Contains(Ids(f.Choose(Case01IdsV03.ChoiceSecond, 0)), Case01Say.V03Second);
            var commit = f.Traverse(Case01Door.Mezz17);
            Assert.IsTrue(commit.CommitEffect);
            CollectionAssert.Contains(Ids(commit), Case01Say.V03Commit);
            CollectionAssert.Contains(Ids(f.EnterRoom(Case01Room.Mezzanine, Case01Room.Entrance)), Case01Say.H3Return, "H3 回屋");

            // v0.2 不套 v0.3 的教學句。
            var legacy = Case01Flow.ForHouseStage(3);
            foreach (var id in new[] { Case01Say.V03Wrong, Case01Say.V03Anchor, Case01Say.V03Second, Case01Say.V03Commit })
                Assert.IsFalse(legacy.HasSaid(id), "v0.2：" + id);
        }

        [Test]
        public void Hud_Queue_ShowsInOrder_HoldsUnderPanels_SkipsWithTab_ClearsOnJump()
        {
            var qa = NewQa();
            var hud = qa.Hud;
            Assert.AreEqual(1, hud.MonologueLog.Count, "啟動時：進門的一句");
            qa.Tick(Dt, Case01Input.None);
            Assert.IsTrue(hud.MonologueVisible);
            StringAssert.Contains("有人生活", hud.MonologueText.text);
            StringAssert.Contains(Case01Texts.MonologueSpeaker, hud.MonologueText.text, "標示主角自語");

            // 面板開啟：暫停並隱藏，計時不前進。
            qa.Flow.Interact(Case01Ids.Calendar);
            hud.ShowText("測試面板");
            for (int i = 0; i < 300; i++) qa.Tick(Dt, Case01Input.None);
            Assert.IsFalse(hud.MonologueVisible, "面板開著時不顯示");
            Assert.IsNotNull(hud.MonologueCurrent, "暫停而不是丟掉");
            hud.HidePanel();
            qa.Tick(Dt, Case01Input.None);
            Assert.IsTrue(hud.MonologueVisible, "面板關閉後接著播");

            // Tab 略過；隊列依序。
            hud.EnqueueMonologue("第二句");
            hud.EnqueueMonologue("第三句");
            qa.Tick(Dt, new Case01Input { SkipLine = true });
            Assert.IsFalse(hud.MonologueVisible, "略過目前這句");
            for (int i = 0; i < 30; i++) qa.Tick(Dt, Case01Input.None);
            Assert.AreEqual("第二句", hud.MonologueCurrent);
            for (int i = 0; i < 200 && hud.MonologueCurrent != "第三句"; i++) qa.Tick(Dt, Case01Input.None);
            Assert.AreEqual("第三句", hud.MonologueCurrent);
            for (int i = 0; i < 200 && hud.MonologueVisible; i++) qa.Tick(Dt, Case01Input.None);
            Assert.IsFalse(hud.MonologueVisible, "播完自動消失（2～5 秒）");

            // 同時太多句：只留最新幾句。
            for (int i = 0; i < 6; i++) hud.EnqueueMonologue("句" + i);
            Assert.LessOrEqual(hud.MonologuePending, Case01Hud.MonologueQueueMax);

            // 跳轉：清除且不重播進門句。
            int logged = hud.MonologueLog.Count;
            for (int j = 1; j <= 4; j++)
            {
                qa.Tick(Dt, new Case01Input { Jump = j });
                Assert.AreEqual(0, hud.MonologuePending, "F" + j + " 清空隊列");
                Assert.IsFalse(hud.MonologueVisible);
            }
            qa.Tick(Dt, new Case01Input { Function = 5 });
            Assert.IsFalse(hud.MonologueVisible, "F5 也不殘留");
            Assert.AreEqual(logged, hud.MonologueLog.Count, "跳轉不重播自語");
        }

        [Test]
        public void NarrativeQa_PreviewsEachLine_WithoutTouchingFlow()
        {
            var qa = NewQa(null, Case01QaMode.NarrativeV04);
            Assert.AreEqual(Case01QaMode.NarrativeV04, qa.QaMode);
            var stage = qa.Session.Stage;
            for (int i = 0; i < Case01Say.All.Length; i++)
            {
                qa.Tick(Dt, new Case01Input { Function = 7 });
                Assert.AreEqual(Case01Texts.SayLine(Case01Say.All[i]), qa.Hud.MonologueLog[qa.Hud.MonologueLog.Count - 1]);
            }
            Assert.AreEqual(stage, qa.Session.Stage, "預覽不改流程");
            qa.Tick(Dt, new Case01Input { Function = 6 });
            Assert.AreEqual(1, qa.ArtStage, "F6 切換 H0～H3");
        }
    }
}
