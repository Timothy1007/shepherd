using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// v0.4 Stage C：記憶殘響。四段各在對的階段首次觸發一次；重覆進房不刷；台詞記入日誌（略過也不錯過線索）；
    /// 剪影半透明、無碰撞體、只在自己的字幕播放時出現；走開、Tab、戰鬥、跳轉都會結束並停聲；QA 可單獨回放；
    /// 對白不揭露結局、組織或校正決策。
    /// </summary>
    public sealed class Case01EchoTests
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
            var go = new GameObject("Case01EchoTest");
            roots.Add(go);
            var qa = go.AddComponent<Case01HouseQA>();
            qa.Build(false, null, null, cp, mode);
            return qa;
        }

        private static void Run(Case01HouseQA qa, float s)
        {
            for (float t = 0; t < s; t += Dt) qa.Tick(Dt, Case01Input.None);
        }

        [Test]
        public void EchoLines_AreShort_Restrained_AndJournaled()
        {
            foreach (var id in Case01Echoes.All)
            {
                var lines = Case01Texts.EchoLines(id);
                Assert.That(lines.Length, Is.InRange(1, 2), id);
                float total = lines.Sum(l => Case01Hud.MonologueSeconds(l[1]));
                Assert.That(total, Is.InRange(2.5f, 10f), id + "：短片段");
                foreach (var l in lines)
                    foreach (var banned in new[] { "死", "組織", "失蹤", "校正", "女兒", "媽" })
                        Assert.IsFalse(l[1].Contains(banned), id + " 不揭露真相：" + banned);
                StringAssert.Contains("不能當成實況的證明", Case01Texts.EchoJournal(id));
            }
        }

        [Test]
        public void Flow_EachEchoOnce_AtTheRightStage()
        {
            var f = new Case01Flow();
            Assert.AreEqual(Case01Echoes.Entrance, f.OnArrive().Echo, "第一次進屋（H0）");
            Assert.IsNull(f.OnArrive().Echo);
            Assert.IsTrue(f.Session.HasJournal(Case01Echoes.Entrance));
            Assert.IsNull(f.EnterRoom(Case01Room.Entrance, Case01Room.Study).Echo, "H0 進書房沒有書房殘響");
            Assert.AreEqual(Case01Echoes.Dining, f.EnterRoom(Case01Room.Entrance, Case01Room.Dining).Echo, "餐具改寫前的餐廳");
            Assert.IsNull(f.EnterRoom(Case01Room.Entrance, Case01Room.Dining).Echo, "重覆進房不刷");
            f.Interact(Case01Ids.Table);
            f.Interact(Case01Ids.Photo);
            f.EnterRoom(Case01Room.Dining, Case01Room.Entrance);
            f.ApplyDiningRewrite();
            f.EnterRoom(Case01Room.Entrance, Case01Room.Dining);
            Assert.AreEqual(Case01Echoes.Study, f.EnterRoom(Case01Room.Landing, Case01Room.Study).Echo, "H1 的書房");
            Assert.IsNull(f.EnterRoom(Case01Room.Landing, Case01Room.Study).Echo);
            f.Interact(Case01Ids.Map);
            f.Interact(Case01Ids.EllieNotes);
            f.Interact(Case01Ids.OldRecord);
            Assert.IsNull(f.EnterRoom(Case01Room.Study, Case01Room.Landing).Echo, "剛錯接的那一刻不搶戲");
            f.Traverse(Case01Door.StudyDoor);
            f.EnterRoom(Case01Room.Landing, Case01Room.Dining);
            f.EnterRoom(Case01Room.Dining, Case01Room.Entrance);
            Assert.AreEqual(Case01Echoes.Landing, f.EnterRoom(Case01Room.Entrance, Case01Room.Landing).Echo, "H2 回到平台：遠處的聲音");
            Assert.IsNull(f.EnterRoom(Case01Room.Entrance, Case01Room.Landing).Echo);
            foreach (var id in Case01Echoes.All) Assert.IsTrue(f.Session.HasJournal(id), "日誌：" + id);

            // 跳轉建立的狀態不會補播已經錯過階段的殘響。
            var s30 = Case01Flow.ForCheckpoint(Case01Checkpoint.S30);
            Assert.IsNull(s30.EnterRoom(Case01Room.Entrance, Case01Room.Dining).Echo, "改寫後的餐廳不再有生活片段");
        }

        [Test]
        public void StartEcho_ShowsTranslucentFigures_NoColliders_SyncedToSubtitles()
        {
            var qa = NewQa();
            var echo = qa.Echo;
            Assert.AreEqual(Case01Echoes.Entrance, echo.Active, "QA 啟動：玄關殘響");
            Assert.AreEqual(2, echo.FigureCount, "兩個生活剪影");
            var figs = qa.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("EchoFigure_")).ToList();
            Assert.AreEqual(2, figs.Count);
            foreach (var f in figs)
                Assert.AreEqual(0, f.GetComponentsInChildren<Collider>(true).Length, "剪影不擋路");
            Run(qa, 0.8f);
            Assert.AreEqual(Case01Echoes.Entrance, qa.Hud.CurrentGroup);
            StringAssert.Contains(Case01Texts.EchoElder, qa.Hud.MonologueText.text, "字幕標示殘響說話者");
            Assert.Greater(echo.Alpha, 0.5f, "字幕播放中剪影淡入");
            var mat = figs[0].GetComponentInChildren<Renderer>().sharedMaterial;
            Assert.Less(mat.color.a, 0.6f, "半透明殘像");
            Assert.AreEqual(Case01Mode.Exploring, qa.Mode, "不鎖操作");

            // 播完：淡出並移除，接著才是主角自語。
            for (int i = 0; i < 600 && echo.Active != null; i++) qa.Tick(Dt, Case01Input.None);
            Assert.IsNull(echo.Active);
            Assert.AreEqual(0, qa.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("EchoFigure_")), "沒有殘留剪影");
            CollectionAssert.Contains(echo.Played, Case01Echoes.Entrance);
            for (int i = 0; i < 60 && qa.Hud.MonologueCurrent == null; i++) qa.Tick(Dt, Case01Input.None);
            StringAssert.Contains("有人生活", qa.Hud.MonologueCurrent ?? "");
        }

        [Test]
        public void WalkingAway_Tab_Battle_Jump_AllEndTheEcho_AndSilence()
        {
            // 走開：離開玄關 → 剩下的句子取消。
            var qa = NewQa();
            Run(qa, 0.8f);
            Assert.AreEqual(Case01Echoes.Entrance, qa.Hud.CurrentGroup);
            qa.Player.Teleport(new Vector3(2.5f, 0.05f, 2.5f), 0f);
            Run(qa, 0.3f);
            Assert.AreEqual(Case01Room.Kitchen, qa.CurrentRoom);
            Assert.IsFalse(qa.Hud.HasPendingGroup(Case01Echoes.Entrance), "走開就略過");
            Run(qa, 1.2f);
            Assert.IsNull(qa.Echo.Active, "剪影淡出移除");
            Assert.IsTrue(qa.Session.HasJournal(Case01Echoes.Entrance), "台詞仍在日誌");

            // Tab：整段略過。
            var t = NewQa();
            Run(t, 0.8f);
            t.Tick(Dt, new Case01Input { SkipLine = true });
            Assert.IsFalse(t.Hud.HasPendingGroup(Case01Echoes.Entrance));
            Run(t, 1.2f);
            Assert.IsNull(t.Echo.Active);

            // 跳轉：立即結束、不殘留。
            var j = NewQa();
            Run(j, 0.5f);
            j.Tick(Dt, new Case01Input { Jump = 2 });
            Assert.IsNull(j.Echo.Active);
            Assert.IsFalse(j.Echo.VoicePlaying);
            Assert.AreEqual(0, j.GetComponentsInChildren<Transform>(true).Count(x => x.name.StartsWith("MemoryEcho_")));

            // 進入戰鬥：結束。
            var b = NewQa(Case01Checkpoint.S40, Case01QaMode.NarrativeV04);
            b.Tick(Dt, new Case01Input { Function = 10 });
            Assert.IsNotNull(b.Echo.Active);
            b.Flow.Interact(Case01Ids.Core);
            Assert.IsTrue(b.Flow.CanStartBattle);
            Assert.IsTrue(b.StartEncounter());
            b.Tick(Dt, Case01Input.None);
            Assert.IsNull(b.Echo.Active, "戰鬥時不播殘響");
        }

        [Test]
        public void Evidence_AndInteraction_StillAvailableDuringEcho()
        {
            var qa = NewQa(null, Case01QaMode.NarrativeV04);
            qa.Tick(Dt, new Case01Input { Function = 10 });
            qa.Tick(Dt, new Case01Input { Function = 10 });
            Assert.AreEqual(Case01Echoes.Dining, qa.Echo.Active);
            Run(qa, 0.6f);
            var r = qa.Flow.Interact(Case01Ids.Table);
            StringAssert.Contains("兩人份", r.Text, "殘響播放中照常調查（不鎖互動）");
            Assert.IsTrue(qa.Session.Has(Case01Flags.TableTwo));
        }

        [Test]
        public void NarrativeQa_F10_ReplaysEachEcho_WithoutReplayingThePlaythrough()
        {
            var qa = NewQa(null, Case01QaMode.NarrativeV04);
            for (int i = 0; i < Case01Echoes.All.Length; i++)
            {
                qa.Tick(Dt, new Case01Input { Function = 10 });
                Assert.AreEqual(i, qa.EchoPreview);
                Assert.AreEqual(Case01Echoes.All[i], qa.Echo.Active, "回放 " + Case01Echoes.All[i]);
                Assert.AreEqual(Case01EchoPlayer.SpotOf(Case01Echoes.All[i]).Room, qa.CurrentRoom, "站在殘響的房間");
                Run(qa, 0.8f);
                Assert.AreEqual(Case01Echoes.All[i], qa.Hud.CurrentGroup);
            }
            // 再按一次回到第一段：可以重複回放。
            qa.Tick(Dt, new Case01Input { Function = 10 });
            Assert.AreEqual(Case01Echoes.Entrance, qa.Echo.Active);
        }
    }
}
