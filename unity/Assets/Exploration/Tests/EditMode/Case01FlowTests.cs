using System.Linq;
using NUnit.Framework;
using Shepherd.Exploration.Case01;

namespace Shepherd.Exploration.Tests
{
    /// <summary>〈第十七號門〉室內 QA 的純邏輯：冪等、線索先決條件、門的連結映射、核不能跳過戰鬥、錯誤可恢復。</summary>
    public sealed class Case01FlowTests
    {
        private static Case01Flow StudyDone()
        {
            var f = new Case01Flow();
            f.Interact(Case01Ids.Map);
            f.Interact(Case01Ids.EllieNotes);
            f.Interact(Case01Ids.OldRecord);
            f.EnterRoom(Case01Room.Study, Case01Room.Landing);
            return f;
        }

        // ---------------- 記錄器 ----------------

        [Test]
        public void Session_StageOnlyMovesForward_FlagsAndJournalAreIdempotent()
        {
            var s = new Case01Session();
            Assert.AreEqual(Case01Stage.EnteredHome, s.Stage);
            Assert.IsTrue(s.Advance(Case01Stage.StudyDiscovery));
            Assert.IsFalse(s.Advance(Case01Stage.ObservedDining), "不能往回");
            Assert.AreEqual(Case01Stage.StudyDiscovery, s.Stage);
            Assert.IsTrue(s.Set(Case01Flags.TableTwo));
            Assert.IsFalse(s.Set(Case01Flags.TableTwo));
            Assert.AreEqual(1, s.TimesSet(Case01Flags.TableTwo));
            Assert.IsTrue(s.AddJournal("a", "t", "x"));
            Assert.IsFalse(s.AddJournal("a", "t", "x"));
            Assert.AreEqual(1, s.Journal.Count);
            Assert.AreEqual(13, Case01Flags.All.Length, "任務書 §3 的 13 個旗標");
            Assert.AreEqual(Case01Session.HouseNormal, s.Get(Case01Session.HouseStateKey));
        }

        [Test]
        public void RepeatedInteract_DoesNotDuplicateFlagsJournalOrEvents()
        {
            var f = new Case01Flow();
            for (int i = 0; i < 5; i++)
            {
                f.Interact(Case01Ids.Table);
                f.Interact(Case01Ids.Photo);
                f.Interact(Case01Ids.Map);
            }
            Assert.AreEqual(1, f.Session.TimesSet(Case01Flags.TableTwo));
            Assert.AreEqual(1, f.Session.Journal.Count(e => e.Id == "table_two"));
            Assert.AreEqual(1, f.Session.Journal.Count(e => e.Id == "photo_two"));
            Assert.AreEqual(1, f.Session.Journal.Count(e => e.Id == "map"));
            Assert.AreEqual(Case01Stage.ObservedDining, f.Session.Stage);
        }

        // ---------------- S10 餐具 ----------------

        [Test]
        public void Dining_RewriteOnlyAfterLeaving_AndRevisitKeepsOriginalObservation()
        {
            var f = new Case01Flow();
            Assert.IsFalse(f.DiningRewritePending, "還沒看過");
            f.EnterRoom(Case01Room.Entrance, Case01Room.Dining);
            f.Interact(Case01Ids.Table);
            Assert.IsFalse(f.DiningRewritePending, "還在餐廳裡");
            Assert.IsFalse(f.ApplyDiningRewrite());
            f.EnterRoom(Case01Room.Dining, Case01Room.Entrance);
            Assert.IsTrue(f.DiningRewritePending);
            Assert.IsTrue(f.ApplyDiningRewrite());
            Assert.IsFalse(f.ApplyDiningRewrite(), "只改寫一次");
            Assert.IsFalse(f.DiningShowsTwoSets);

            var r = f.EnterRoom(Case01Room.Entrance, Case01Room.Dining);
            Assert.AreEqual(Case01Texts.RadioOneSet, r.Radio, "同伴：我這裡的記錄只有一副");
            Assert.AreEqual(Case01Stage.DiningContradiction, f.Session.Stage);
            Assert.IsTrue(f.Session.HasJournal("table_two"), "原始觀測（兩副）仍在日誌");
            Assert.IsTrue(f.Session.HasJournal("table_rewritten"));
            Assert.IsNull(f.EnterRoom(Case01Room.Dining, Case01Room.Entrance).Radio);
            Assert.IsNull(f.EnterRoom(Case01Room.Entrance, Case01Room.Dining).Radio, "通訊只播一次");
            StringAssert.Contains("一副", f.Interact(Case01Ids.Table).Text);
        }

        // ---------------- S20／S30 樓梯錯接 ----------------

        [Test]
        public void WrongConnection_RequiresEllieNotesAndRecord_ActivatesOnLeavingStudy()
        {
            var f = new Case01Flow();
            f.Interact(Case01Ids.Map);
            f.Interact(Case01Ids.EllieNotes);
            f.EnterRoom(Case01Room.Study, Case01Room.Landing);
            Assert.IsFalse(f.WrongConnectionArmed, "缺校正記錄");
            Assert.IsFalse(f.PortalActive(Case01Door.StudyDoor));
            Assert.AreEqual(Case01Room.Study, f.Destination(Case01Door.StudyDoor), "初始樓梯連通書房");

            f.EnterRoom(Case01Room.Landing, Case01Room.Study);
            var r = f.Interact(Case01Ids.OldRecord);
            Assert.IsTrue(f.WrongConnectionArmed);
            Assert.IsNotNull(r.Toast, "先給視覺／聲音提示");
            Assert.IsFalse(f.WrongConnectionActive, "還在書房裡，不立刻錯接");
            Assert.IsTrue(f.ThirdDoorVisible);
            Assert.IsFalse(f.ThirdDoorOpen);

            f.EnterRoom(Case01Room.Study, Case01Room.Landing);
            Assert.IsTrue(f.WrongConnectionActive);
            Assert.AreEqual(Case01Stage.WrongConnection, f.Session.Stage);
            Assert.AreEqual(Case01Room.Dining, f.Destination(Case01Door.StudyDoor), "書房門錯接到餐廳");
            Assert.IsTrue(f.PortalActive(Case01Door.StudyDoor));
            Assert.IsFalse(f.MatchesMark(Case01Door.StudyDoor));
        }

        [Test]
        public void StudyDoorOverride_NeedsFirsthandCheck_WrongMarksAreRecoverable_TriangleOpensMezzanine()
        {
            var f = StudyDone();
            var r = f.Interact(Case01Ids.StudyMark);
            Assert.IsNull(r.Choice, "還沒親自走過，不能覆寫");
            Assert.AreEqual(Case01Texts.StudyMarkUnverified, r.Text);

            r = f.Traverse(Case01Door.StudyDoor);
            Assert.AreEqual(Case01Room.Dining, r.Redirect, "錯誤通路送到安全的既有房間");
            Assert.IsTrue(f.Session.Has(Case01Flags.RouteMismatchSeen));
            Assert.AreEqual(Case01Texts.Mismatch, f.Session.FindJournal("route_mismatch").Text, "日誌：路徑與標記不符");
            Assert.AreEqual(Case01Room.Dining, f.LastSeen(Case01Door.StudyDoor));

            Assert.IsNotNull(f.Interact(Case01Ids.StudyMark).Choice);
            f.Choose(Case01Ids.ChoiceOverride, 2);
            Assert.AreEqual(Case01Room.Entrance, f.Destination(Case01Door.StudyDoor), "寫 □：通往玄關（安全）");
            Assert.IsFalse(f.StairRouteRestored);
            Assert.IsFalse(f.PortalActive(Case01Door.ThirdDoor));
            f.Choose(Case01Ids.ChoiceOverride, 1);
            Assert.AreEqual(Case01Room.Dining, f.Destination(Case01Door.StudyDoor));
            Assert.IsFalse(f.StairRouteRestored);

            Assert.IsNotNull(f.Interact(Case01Ids.StudyMark).Choice, "可以再試");
            f.Choose(Case01Ids.ChoiceOverride, 0);
            Assert.IsTrue(f.StairRouteRestored);
            Assert.AreEqual(Case01Room.Study, f.Destination(Case01Door.StudyDoor));
            Assert.IsFalse(f.PortalActive(Case01Door.StudyDoor), "回到實體連通");
            Assert.IsTrue(f.PortalActive(Case01Door.ThirdDoor), "夾層入口打開");
            Assert.AreEqual(Case01Room.Mezzanine, f.Traverse(Case01Door.ThirdDoor).Redirect);
            Assert.IsNull(f.Interact(Case01Ids.StudyMark).Choice, "成功後不再出現覆寫選項");
        }

        // ---------------- S40 反制與戰鬥 ----------------

        [Test]
        public void Core_FirstTouchTriggersDefenseOnce_CannotRepairBeforeBattleWon()
        {
            var f = Case01Flow.ForCheckpoint(Case01Checkpoint.S40);
            Assert.IsFalse(f.CanStartBattle, "威脅核之前不會開戰");
            var r = f.Interact(Case01Ids.Core);
            Assert.IsTrue(r.BeginDefense);
            Assert.AreEqual(Case01Stage.AnomalyDefense, f.Session.Stage);
            Assert.AreEqual(Case01Room.Dining, f.Destination(Case01Door.StudyDoor), "反制：已歸位的門重新連錯");
            Assert.IsTrue(f.StairRouteRestored, "成功的覆寫仍記錄著");
            Assert.IsTrue(f.PortalActive(Case01Door.ThirdDoor), "第三扇門不會被關上（不軟鎖）");
            Assert.IsTrue(f.CanStartBattle);

            for (int i = 0; i < 3; i++)
            {
                r = f.Interact(Case01Ids.Core);
                Assert.IsFalse(r.BeginDefense, "反制只發生一次");
                Assert.IsTrue(r.PullToCourtyard);
                Assert.IsFalse(f.Session.Has(Case01Flags.MissingLinkRestored), "戰鬥前不能修復");
                Assert.IsFalse(f.CoreStabilized);
            }
        }

        [Test]
        public void Battle_WonOnlyOnce_LossAllowsRetry_VictoryDoesNotStabilizeCore()
        {
            var f = Case01Flow.ForCheckpoint(Case01Checkpoint.S40);
            f.Interact(Case01Ids.Core);
            f.OnBattleStarted();
            f.OnBattleLost();
            Assert.IsTrue(f.CanStartBattle, "戰敗可重試");
            Assert.IsFalse(f.Session.Has(Case01Flags.BattleWon));
            f.OnBattleStarted();
            Assert.IsNotNull(f.OnBattleWon().Toast);
            Assert.AreSame(Case01Response.None, f.OnBattleWon(), "battle_won 只設定一次");
            Assert.AreEqual(1, f.Session.TimesSet(Case01Flags.BattleWon));
            Assert.IsFalse(f.CanStartBattle, "敵人不重生");
            Assert.AreEqual(Case01Stage.PostBattle, f.Session.Stage);
            Assert.IsFalse(f.CoreStabilized, "戰鬥勝利不能直接完成核修復");
            Assert.IsTrue(f.CourtyardTearOpen);
        }

        // ---------------- S50 核的三段修復 ----------------

        [Test]
        public void CoreStep1_RequiresMapAndPhoto_ThenOpensDoor17AndSwapsLinks()
        {
            // 沒看舊地圖與合照的路線（線索順序自由）。
            var f = new Case01Flow();
            f.Interact(Case01Ids.EllieNotes);
            f.Interact(Case01Ids.OldRecord);
            f.EnterRoom(Case01Room.Study, Case01Room.Landing);
            f.Traverse(Case01Door.StudyDoor);
            f.Interact(Case01Ids.StudyMark);
            f.Choose(Case01Ids.ChoiceOverride, 0);
            f.EnterRoom(Case01Room.Landing, Case01Room.Mezzanine);
            f.Interact(Case01Ids.Core);
            f.OnBattleStarted();
            f.OnBattleWon();

            var r = f.Interact(Case01Ids.Core);
            StringAssert.Contains(Case01Texts.CoreNeedMap, r.Text);
            StringAssert.Contains(Case01Texts.CoreNeedPhoto, r.Text);
            Assert.IsFalse(f.Door17Open);
            f.Interact(Case01Ids.Map);
            StringAssert.DoesNotContain(Case01Texts.CoreNeedMap, f.Interact(Case01Ids.Core).Text);
            f.Interact(Case01Ids.Photo);
            r = f.Interact(Case01Ids.Core);
            Assert.IsTrue(f.Door17Open, "原本無門的牆出現真正的門");
            Assert.IsTrue(f.Session.Has(Case01Flags.MissingLinkRestored));
            Assert.AreEqual(Case01Stage.LinkRestoration, f.Session.Stage);
            Assert.AreEqual(Case01Room.Dining, f.Destination(Case01Door.MezzTriangle), "核把通往書房的門導向餐廳");
            Assert.AreEqual(Case01Room.Entrance, f.Destination(Case01Door.Mezz17));
        }

        [Test]
        public void CoreStep2_AnchorNeedsConfirmation_WrongAnchorRedirectsSafely_Retry()
        {
            var f = Case01Flow.ForCheckpoint(Case01Checkpoint.S50);
            f.Interact(Case01Ids.Core);
            var r = f.Interact(Case01Ids.Core);
            Assert.IsNull(r.Choice, "必須先親自走進通道確認");
            Assert.AreEqual(Case01Texts.Step2NeedConfirm, r.Text);
            Assert.AreSame(Case01Response.None, f.Choose(Case01Ids.ChoiceAnchor, 0), "未確認時選擇無效");

            Assert.AreEqual(Case01Room.Dining, f.Traverse(Case01Door.MezzTriangle).Redirect);
            Assert.IsTrue(f.SwapConfirmed);
            r = f.Interact(Case01Ids.Core);
            Assert.IsNotNull(r.Choice);
            r = f.Choose(Case01Ids.ChoiceAnchor, 1);
            Assert.AreEqual(Case01Room.Dining, r.Redirect, "錯誤錨點：送回安全房間");
            Assert.IsFalse(f.Session.Has(Case01Flags.RouteAnchorSet));
            Assert.IsTrue(f.Session.HasJournal("anchor_wrong"));
            Assert.IsNotNull(f.Interact(Case01Ids.Core).Choice, "可以重試");
            f.Choose(Case01Ids.ChoiceAnchor, 0);
            Assert.IsTrue(f.Session.Has(Case01Flags.RouteAnchorSet));
            Assert.AreEqual(Case01Room.Study, f.Destination(Case01Door.MezzTriangle), "△ 錨定到書房");
            Assert.IsTrue(f.MatchesMark(Case01Door.MezzTriangle));
            // 另一路仍可變動。
            var a = f.Traverse(Case01Door.MezzCircle).Redirect;
            var b = f.Traverse(Case01Door.MezzCircle).Redirect;
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void CoreStep3_RequiresBothVerifiedLinks_ThenStabilizesTemporarily()
        {
            var f = Case01Flow.ForCheckpoint(Case01Checkpoint.S50);
            f.Interact(Case01Ids.Core);
            f.Traverse(Case01Door.MezzCircle);
            f.Interact(Case01Ids.Core);
            f.Choose(Case01Ids.ChoiceAnchor, 0);

            var r = f.Interact(Case01Ids.Core);
            StringAssert.Contains(Case01Texts.Step3NeedTriangle, r.Text);
            StringAssert.Contains(Case01Texts.Step3NeedDoor17, r.Text);
            f.Traverse(Case01Door.MezzTriangle);
            r = f.Interact(Case01Ids.Core);
            StringAssert.DoesNotContain(Case01Texts.Step3NeedTriangle, r.Text);
            Assert.IsFalse(f.CoreStabilized);
            f.Traverse(Case01Door.Mezz17);
            r = f.Interact(Case01Ids.Core);
            Assert.IsTrue(f.CoreStabilized);
            Assert.IsTrue(r.WorldChanged);
            Assert.AreEqual(Case01Session.HouseTemporarilyStable, f.Session.Get(Case01Session.HouseStateKey));
            Assert.AreEqual(Case01Stage.CoreStabilized, f.Session.Stage);
            Assert.AreEqual(Case01Room.Study, f.Destination(Case01Door.StudyDoor), "書房門恢復");
            Assert.AreEqual(Case01Texts.CoreStable, f.Interact(Case01Ids.Core).Text, "再按 E 不重複演出");

            // S60：出口與證物。
            r = f.Interact(Case01Ids.Photo);
            Assert.AreEqual(Case01Texts.PhotoFaded, r.Text, "照片最後的改變");
            Assert.IsTrue(f.Session.Has(Case01Flags.EvidenceArchived));
            Assert.IsTrue(f.Session.HasJournal("photo_two"), "案卷保留原本有兩人的觀察");
            r = f.Choose(Case01Ids.ChoiceFrontDoor, 0);
            Assert.IsTrue(r.Exit);
            Assert.AreEqual(Case01Stage.Exited, f.Session.Stage);
            foreach (var flag in Case01Flags.All) Assert.IsTrue(f.Session.Has(flag), "完整流程設定 " + flag);
        }

        [Test]
        public void FrontDoor_BeforeStabilization_OffersRestartNotExit()
        {
            var f = new Case01Flow();
            var r = f.Interact(Case01Ids.FrontDoor);
            Assert.AreEqual(Case01Ids.ChoiceFrontDoor, r.Choice.Id);
            Assert.IsTrue(f.Choose(Case01Ids.ChoiceFrontDoor, 0).Restart);
            Assert.AreSame(Case01Response.None, f.Choose(Case01Ids.ChoiceFrontDoor, 1));
            Assert.AreNotEqual(Case01Stage.Exited, f.Session.Stage);
        }

        [Test]
        public void Checkpoints_ReplayTheSameFlow()
        {
            var s30 = Case01Flow.ForCheckpoint(Case01Checkpoint.S30);
            Assert.AreEqual(Case01Stage.WrongConnection, s30.Session.Stage);
            Assert.IsTrue(s30.WrongConnectionActive);
            Assert.IsFalse(s30.DiningShowsTwoSets);
            Assert.IsTrue(s30.Session.HasJournal("table_two"));
            var s40 = Case01Flow.ForCheckpoint(Case01Checkpoint.S40);
            Assert.AreEqual(Case01Stage.MezzanineDiscovered, s40.Session.Stage);
            Assert.IsTrue(s40.StairRouteRestored);
            Assert.IsFalse(s40.DefenseTriggered);
            var s50 = Case01Flow.ForCheckpoint(Case01Checkpoint.S50);
            Assert.AreEqual(Case01Stage.PostBattle, s50.Session.Stage);
            Assert.IsTrue(s50.Session.Has(Case01Flags.BattleWon));
            Assert.IsFalse(s50.CoreStabilized);
        }
    }
}
