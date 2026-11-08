using System.Collections.Generic;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// 核修復 v0.3「關係校正」：證據門檻、錯誤選擇只預演矛盾且可撤銷、△→書房＋17→玄關同時成立、只要走一次門寫回、
    /// 回退與再次進門不卡死、F1～F5 初始化、FP-001 不重複、v0.2 舊模式不受影響。
    /// </summary>
    public sealed class Case01CorrectionV03Tests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();

        private static Case01Flow PostBattleV03()
        {
            return Case01Flow.ForCheckpoint(Case01Checkpoint.S50, Case01CorrectionMode.RevisedV03);
        }

        private Case01HouseQA NewQa(Case01QaMode mode, Case01Checkpoint? cp = null)
        {
            var go = new GameObject("Case01V03Test");
            roots.Add(go);
            var qa = go.AddComponent<Case01HouseQA>();
            qa.Build(false, null, 1, cp, mode);
            return qa;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
        }

        // ---------------- 純邏輯 ----------------

        [Test]
        public void PostBattleState_TriangleMiswired_CircleFlips_No17()
        {
            var f = PostBattleV03();
            Assert.AreEqual(Case01CorrectionMode.RevisedV03, f.Correction);
            Assert.AreEqual(Case01Room.Dining, f.Destination(Case01Door.MezzTriangle), "△ 現在錯連餐廳");
            Assert.IsFalse(f.PortalActive(Case01Door.Mezz17), "17 不存在");
            var a = f.Traverse(Case01Door.MezzCircle).Redirect;
            var b = f.Traverse(Case01Door.MezzCircle).Redirect;
            Assert.AreNotEqual(a, b, "○ 週期性切換");
            StringAssert.Contains("跳動（已證實）", f.DeviceReadout());
            Assert.IsFalse(f.CoreStabilized);
        }

        [Test]
        public void Device_ShowsObservedVsRecorded_WithoutRevealingUnobservedAnswers()
        {
            // 沒看過舊地圖與合照（不同調查順序）。
            var f = new Case01Flow(Case01CorrectionMode.RevisedV03);
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

            var readout = f.DeviceReadout();
            StringAssert.Contains("○餐廳門</b>\n　現況：未知", readout, "還沒走過 ○ 門");
            StringAssert.Contains("□17</b>\n　現況：不存在", readout);
            StringAssert.Contains("記錄：未知（舊地圖在二樓書房、合照在一樓餐廳）", readout, "沒看過證據就不推定");
            var r = f.Interact(Case01Ids.Core);
            Assert.IsNull(r.Choice, "證據不足不能判斷");
            StringAssert.Contains("街區舊地圖（二樓書房）", r.Text);
            StringAssert.Contains("合照（一樓餐廳）", r.Text);
            f.Interact(Case01Ids.Map);
            f.Interact(Case01Ids.Photo);
            Assert.IsNotNull(f.Interact(Case01Ids.Core).Choice, "補齊證據後可以判斷");
            StringAssert.Contains("推定：舊地圖補畫的一格＋合照", f.DeviceReadout());
        }

        [Test]
        public void WrongAnchors_PreviewContradiction_NoRouteChange_UndoRecovers()
        {
            var f = PostBattleV03();
            var choice = f.Interact(Case01Ids.Core).Choice;
            Assert.AreEqual(Case01IdsV03.ChoiceAnchor, choice.Id);
            CollectionAssert.AreEqual(new[] { "△→書房", "○→餐廳", "17→不存在" }, choice.Options);
            foreach (int wrong in new[] { 1, 2 })
            {
                var r = f.Choose(Case01IdsV03.ChoiceAnchor, wrong);
                Assert.AreEqual(Case01IdsV03.ChoiceUndo, r.Choice.Id, "可以一鍵撤銷");
                StringAssert.Contains("矛盾", r.Choice.Prompt, "看得到反證");
                Assert.IsNull(r.Redirect, "不傳送、不罰跑");
                Assert.AreNotEqual(0, f.PreviewConflict);
                Assert.IsFalse(f.V03Anchored);
                Assert.AreEqual(Case01Room.Dining, f.Destination(Case01Door.MezzTriangle), "預演不改路由");
                Assert.AreSame(Case01Response.None, f.Choose(Case01IdsV03.ChoiceAnchor, 0), "預演中不能直接改選");
                Assert.AreEqual(Case01IdsV03.ChoiceUndo, f.Interact(Case01Ids.Core).Choice.Id, "再按 E 仍是預演");
                var back = f.Choose(Case01IdsV03.ChoiceUndo, 0);
                Assert.AreEqual(0, f.PreviewConflict);
                Assert.AreEqual(Case01IdsV03.ChoiceAnchor, back.Choice.Id, "撤銷後回到裝置");
            }
            var ok = f.Choose(Case01IdsV03.ChoiceAnchor, 0);
            Assert.IsTrue(f.V03Anchored);
            Assert.IsTrue(f.Session.Has(Case01Flags.RouteAnchorSet));
            Assert.AreEqual(Case01Room.Study, f.Destination(Case01Door.MezzTriangle));
            Assert.IsNull(ok.Redirect);
            Assert.IsFalse(f.CoreStabilized, "錨點不等於修好");
        }

        [Test]
        public void SecondLink_WrongChoicesPreview_CorrectMakesBothRelationsHold()
        {
            var f = PostBattleV03();
            f.Interact(Case01Ids.Core);
            f.Choose(Case01IdsV03.ChoiceAnchor, 0);
            var c = f.Interact(Case01Ids.Core).Choice;
            Assert.AreEqual(Case01IdsV03.ChoiceSecond, c.Id);
            foreach (int wrong in new[] { 1, 2 })
            {
                var r = f.Choose(Case01IdsV03.ChoiceSecond, wrong);
                Assert.AreEqual(Case01IdsV03.ChoiceUndo, r.Choice.Id);
                Assert.IsFalse(f.V03SecondLink);
                Assert.IsFalse(f.PortalActive(Case01Door.Mezz17));
                f.Choose(Case01IdsV03.ChoiceUndo, 0);
            }
            f.Choose(Case01IdsV03.ChoiceSecond, 0);
            Assert.IsTrue(f.V03SecondLink);
            Assert.IsTrue(f.Session.Has(Case01Flags.MissingLinkRestored));
            Assert.AreEqual(Case01Room.Study, f.Destination(Case01Door.MezzTriangle), "△→書房");
            Assert.IsTrue(f.PortalActive(Case01Door.Mezz17));
            Assert.AreEqual(Case01Room.Entrance, f.Destination(Case01Door.Mezz17), "17→玄關 同時成立");
            var circle = f.Destination(Case01Door.MezzCircle);
            f.Traverse(Case01Door.MezzCircle);
            f.Traverse(Case01Door.MezzCircle);
            Assert.AreEqual(Case01Room.Dining, f.Destination(Case01Door.MezzCircle), "○ 停止跳動");
            Assert.AreEqual(Case01Room.Dining, circle);
            Assert.IsFalse(f.CoreStabilized, "還沒寫回");
        }

        [Test]
        public void OneTraversal_CommitsCorrection_EitherLinkWorks()
        {
            foreach (var door in new[] { Case01Door.MezzTriangle, Case01Door.Mezz17 })
            {
                var f = PostBattleV03();
                f.Interact(Case01Ids.Core);
                f.Choose(Case01IdsV03.ChoiceAnchor, 0);
                f.Interact(Case01Ids.Core);
                f.Choose(Case01IdsV03.ChoiceSecond, 0);
                var r = f.Traverse(door);
                Assert.IsTrue(r.CommitEffect, door + " 寫回");
                Assert.IsTrue(f.CoreStabilized, "不必再回核按 E");
                Assert.IsTrue(f.V03Committed);
                Assert.AreEqual(Case01Session.HouseTemporarilyStable, f.Session.Get(Case01Session.HouseStateKey));
                Assert.AreEqual(Case01Room.Study, f.Destination(Case01Door.StudyDoor), "走廊錯接終止");
                Assert.AreEqual(door == Case01Door.MezzTriangle ? Case01Room.Study : Case01Room.Entrance, r.Redirect);
                // 再次進門：一般通路，不重複寫回、不卡死。
                var again = f.Traverse(door);
                Assert.IsFalse(again.CommitEffect);
                Assert.IsNotNull(again.Redirect);
                Assert.IsNotNull(f.Traverse(Case01Door.MezzCircle).Redirect);
                Assert.IsNotNull(f.Traverse(Case01Door.MezzBack).Redirect);
                Assert.AreEqual(Case01Texts.CoreStable, f.Interact(Case01Ids.Core).Text);
                // FP-001 與出口照舊；所有旗標都能達成。
                Assert.IsTrue(f.FamilyPhotoAvailable);
                f.Interact(Case01Ids.EvidencePhoto);
                Assert.AreSame(Case01Response.None, f.Interact(Case01Ids.EvidencePhoto), "FP-001 不重複");
                Assert.IsTrue(f.Choose(Case01Ids.ChoiceFrontDoor, 0).Exit);
                foreach (var flag in Case01Flags.All) Assert.IsTrue(f.Session.Has(flag), flag);
            }
        }

        [Test]
        public void TraversalBeforeBothLinks_DoesNotCommit()
        {
            var f = PostBattleV03();
            f.Interact(Case01Ids.Core);
            f.Choose(Case01IdsV03.ChoiceAnchor, 0);
            var r = f.Traverse(Case01Door.MezzTriangle);
            Assert.IsFalse(r.CommitEffect);
            Assert.AreEqual(Case01Room.Study, r.Redirect);
            Assert.IsFalse(f.CoreStabilized);
        }

        [Test]
        public void LegacyMode_IsUnchanged()
        {
            var f = Case01Flow.ForCheckpoint(Case01Checkpoint.S50);
            Assert.AreEqual(Case01CorrectionMode.LegacyV02, f.Correction);
            f.Interact(Case01Ids.Core);
            Assert.IsTrue(f.Door17Open, "v0.2 第一段：補回 17");
            Assert.AreEqual(Case01Room.Dining, f.Destination(Case01Door.MezzTriangle), "v0.2 的互換");
            Assert.IsNull(f.Interact(Case01Ids.Core).Choice, "v0.2 仍需先走門確認");
        }

        // ---------------- 場景 ----------------

        [Test]
        public void V03Mode_StartsAtPostBattleCore_AndF5TogglesWithLegacy()
        {
            var qa = NewQa(Case01QaMode.CoreV03);
            Assert.AreEqual(Case01CorrectionMode.RevisedV03, qa.Flow.Correction);
            Assert.AreEqual(Case01Stage.PostBattle, qa.Session.Stage, "直接從戰後核前開始");
            Assert.AreEqual(Case01Room.Mezzanine, qa.CurrentRoom);
            StringAssert.Contains("v0.3", qa.Hud.Base.ViewMode.text);
            qa.Tick(Dt, new Case01Input { Function = 5 });
            Assert.AreEqual(Case01QaMode.Standard, qa.QaMode);
            Assert.AreEqual(Case01CorrectionMode.LegacyV02, qa.Flow.Correction);
            Assert.AreEqual(Case01Stage.PostBattle, qa.Session.Stage);
            qa.Tick(Dt, new Case01Input { Function = 5 });
            Assert.AreEqual(Case01CorrectionMode.RevisedV03, qa.Flow.Correction);
            qa.Tick(Dt, new Case01Input { Jump = 1 });
            Assert.AreEqual(Case01CorrectionMode.RevisedV03, qa.Flow.Correction, "F1 保留目前模式");
            Assert.AreEqual(Case01Stage.EnteredHome, qa.Session.Stage);
            var std = NewQa(Case01QaMode.Standard);
            Assert.AreEqual(Case01CorrectionMode.LegacyV02, std.Flow.Correction, "預設仍是 v0.2");
        }

        private static void Aim(Case01HouseQA qa, string id)
        {
            var it = qa.Layout.Interactables[id];
            var target = it.transform.TransformPoint(it.GetComponent<BoxCollider>().center);
            for (int i = 0; i < 2; i++)
            {
                var d = target - qa.Player.EyePosition;
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
                qa.Tick(Dt, Case01Input.From(new ExploreInput
                {
                    Look = new Vector2(Mathf.DeltaAngle(qa.Player.Yaw, yaw), -(pitch - qa.Player.Pitch)) / FirstPersonController.LookSensitivity,
                }));
            }
        }

        private static void PressE(Case01HouseQA qa) { qa.Tick(Dt, Case01Input.From(new ExploreInput { Interact = true })); }

        [Test]
        public void V03Scene_FullCorrection_WithOneWalk_ShowsVisibleFeedback()
        {
            var qa = NewQa(Case01QaMode.CoreV03);
            var c = Case01HouseBuilder.MezzCenter;
            qa.Player.Teleport(c + new Vector3(0f, 0.05f, -0.4f), 0f);
            qa.Tick(Dt, Case01Input.None);
            Aim(qa, Case01Ids.Core);
            PressE(qa);
            Assert.IsNotNull(qa.Hud.ActiveChoice, "核旁的校正裝置");
            StringAssert.Contains("校正裝置", qa.Hud.Base.InspectText.text);
            qa.Tick(Dt, new Case01Input { Choice = 2 });
            Assert.AreEqual(Case01IdsV03.ChoiceUndo, qa.Hud.ActiveChoice.Id, "選錯：看到反證");
            qa.Tick(Dt, Case01Input.None);
            Assert.AreEqual(new Color(1f, 0.3f, 0.35f), qa.Layout.CoreLight.color, "核光轉紅（預演）");
            Assert.AreEqual(Case01Room.Mezzanine, qa.CurrentRoom, "沒有被傳走");
            qa.Tick(Dt, new Case01Input { Choice = 1 });
            Assert.AreEqual(Case01IdsV03.ChoiceAnchor, qa.Hud.ActiveChoice.Id, "撤銷回到裝置");
            qa.Tick(Dt, new Case01Input { Choice = 1 });
            Assert.IsTrue(qa.Flow.V03Anchored);
            Assert.AreEqual("△ 書房", qa.Layout.MezzLabels[Case01Door.MezzTriangle].text, "門框標籤同步（文字＋顏色）");
            PressE(qa);
            Aim(qa, Case01Ids.Core);
            PressE(qa);
            Assert.AreEqual(Case01IdsV03.ChoiceSecond, qa.Hud.ActiveChoice.Id);
            qa.Tick(Dt, new Case01Input { Choice = 1 });
            Assert.IsTrue(qa.Flow.V03SecondLink);
            Assert.IsFalse(qa.Layout.Portal(Case01Door.Mezz17).Seal.activeSelf, "北牆出現 17 號門");
            Assert.AreEqual("□ 17 玄關", qa.Layout.MezzLabels[Case01Door.Mezz17].text);
            PressE(qa);

            // 只走一次門：17 號門 → 玄關。
            qa.Player.Teleport(c + new Vector3(0f, 0.05f, 4.6f), 0f);
            for (int i = 0; i < 90 && qa.CurrentRoom != Case01Room.Entrance; i++)
                qa.Tick(Dt, Case01Input.From(ExploreInput.Forward()));
            Assert.AreEqual(Case01Room.Entrance, qa.CurrentRoom);
            Assert.IsTrue(qa.Flow.CoreStabilized, "走一次就寫回");
            Assert.Greater(qa.DoorSnapPieces, 0, "門框歸位演出");
            Assert.AreEqual(180f, qa.Player.Yaw, 1f, "轉身面向剛走過的門");
            CollectionAssert.Contains(qa.Audio.Played, "Case01Settle", "低頻接合聲");
            for (int i = 0; i < 40; i++) qa.Tick(Dt, Case01Input.None);
            Assert.AreEqual(3, qa.HouseStage);
            Assert.AreEqual("離開十七號", qa.Layout.FrontDoorLabel.text, "出口改變");
            Assert.IsTrue(qa.Layout.Decor.LivingFrameBlank.activeSelf, "H3 殘留");
            Assert.IsTrue(qa.Layout.Decor.EvidenceRoot.activeSelf, "FP-001 出現");
            for (int i = 0; i < 90; i++) qa.Tick(Dt, Case01Input.None);
            Assert.AreEqual(0, qa.DoorSnapPieces, "演出結束後清除");
        }
    }
}
