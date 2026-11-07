using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Battle.UI;
using Shepherd.Exploration.Case01;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// v0.2 室內擴充：生活細節物件、H0～H3 階段異變（玩家不在房間時才切換）、
    /// 核穩定後才出現的實體證物 FP-001（拾取一次、3D 檢視、F1 重置後可再取得）。
    /// </summary>
    public sealed class Case01EnvironmentTests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();

        private Case01HouseQA NewQa(Case01Checkpoint? cp = null)
        {
            var go = new GameObject("Case01EnvTest");
            roots.Add(go);
            var qa = go.AddComponent<Case01HouseQA>();
            qa.Build(false, null, 1, cp);
            return qa;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
        }

        private static void Tick(Case01HouseQA qa, Case01Input i) { qa.Tick(Dt, i); }

        private static void Run(Case01HouseQA qa, float s)
        {
            for (float t = 0; t < s; t += Dt) qa.Tick(Dt, Case01Input.None);
        }

        private static Vector2 LookDelta(FirstPersonController p, Vector3 target, bool level)
        {
            var d = target - p.EyePosition;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = level ? 0f : -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            return new Vector2(Mathf.DeltaAngle(p.Yaw, yaw), -(pitch - p.Pitch)) / FirstPersonController.LookSensitivity;
        }

        private static void WalkTo(Case01HouseQA qa, float x, float z, float max = 12f)
        {
            for (float t = 0; t < max; t += Dt)
            {
                Assert.AreEqual(Case01Mode.Exploring, qa.Mode, "走路途中不應觸發其他狀態");
                var p = qa.Player.transform.position;
                if (new Vector2(x - p.x, z - p.z).magnitude < 0.25f) return;
                Tick(qa, Case01Input.From(new ExploreInput
                {
                    Move = new Vector2(0f, 1f), Look = LookDelta(qa.Player, new Vector3(x, qa.Player.EyePosition.y, z), true),
                }));
            }
            Assert.Fail("走不到 (" + x + ", " + z + ")，卡在 " + qa.Player.transform.position);
        }

        private static void Interact(Case01HouseQA qa, string id)
        {
            var it = qa.Layout.Interactables[id];
            var target = it.transform.TransformPoint(it.GetComponent<BoxCollider>().center);
            for (int i = 0; i < 2; i++) Tick(qa, Case01Input.From(new ExploreInput { Look = LookDelta(qa.Player, target, false) }));
            Assert.IsNotNull(qa.CurrentTarget, "準星對到 " + id + "（位置 " + qa.Player.transform.position + "）");
            Assert.AreEqual(id, qa.CurrentTarget.Id);
            if (qa.Hud.PanelVisible && qa.PanelSourceId == id) Tick(qa, Case01Input.From(new ExploreInput { Interact = true }));
            Tick(qa, Case01Input.From(new ExploreInput { Interact = true }));
        }

        private static void PressE(Case01HouseQA qa) { Tick(qa, Case01Input.From(new ExploreInput { Interact = true })); }

        /// <summary>從 S50 完成核的三段修復（前兩段直接走規則，第三段在場景中按 E）。</summary>
        private static void Stabilize(Case01HouseQA qa)
        {
            var f = qa.Flow;
            f.Interact(Case01Ids.Core);
            f.Traverse(Case01Door.MezzCircle);
            f.Interact(Case01Ids.Core);
            f.Choose(Case01Ids.ChoiceAnchor, 0);
            f.Traverse(Case01Door.MezzTriangle);
            f.Traverse(Case01Door.Mezz17);
            qa.Player.Teleport(Case01HouseBuilder.MezzCenter + new Vector3(0f, 0.05f, -0.4f), 0f);
            Run(qa, 0.1f);
            Interact(qa, Case01Ids.Core);
            Assert.IsTrue(f.CoreStabilized);
        }

        private static int ActiveRenderers(Transform t)
        {
            return t.GetComponentsInChildren<Renderer>(false).Count(r => r.gameObject.activeInHierarchy);
        }

        // ---------------- A：生活細節 ----------------

        [Test]
        public void LifeDetails_EachRoomHasVariedObjects_AndNewInvestigations()
        {
            var qa = NewQa();
            var life = qa.Layout.Root.Find("LifeDetails");
            Assert.IsNotNull(life);
            foreach (var room in new[] { "Entrance", "Kitchen", "Living", "Dining", "Bedroom", "Study", "Landing" })
            {
                var g = life.Find(room);
                Assert.IsNotNull(g, room);
                Assert.GreaterOrEqual(ActiveRenderers(g), 8, room + " 至少 8 件可見物件");
                var shapes = new HashSet<string>(g.GetComponentsInChildren<MeshFilter>().Select(m => m.sharedMesh.name));
                Assert.GreaterOrEqual(shapes.Count, 2, room + " 不只長方體（圓柱、球、膠囊等）");
            }
            foreach (var id in new[]
                     {
                         Case01Ids.TeaTin, Case01Ids.LivingBookNote, Case01Ids.FriendCard, Case01Ids.ShoppingList, Case01Ids.TravelRoute,
                         Case01Ids.JobLetter, Case01Ids.RepairKit, Case01Ids.FileBoxes, Case01Ids.EvidencePhoto,
                     })
                Assert.IsTrue(qa.Layout.Interactables.ContainsKey(id), id);
            // 原有主線熱區完全保留。
            foreach (var id in new[] { Case01Ids.Table, Case01Ids.Photo, Case01Ids.Map, Case01Ids.EllieNotes, Case01Ids.OldRecord, Case01Ids.StudyMark, Case01Ids.Core })
                Assert.IsTrue(qa.Layout.Interactables[id].gameObject.activeInHierarchy, id);
        }

        [Test]
        public void LifeDetails_AreReachable_AndShowShortNotesNotSidePanels()
        {
            var qa = NewQa();
            // 客廳書架的便箋。
            WalkTo(qa, 5.8f, 2.5f);
            WalkTo(qa, 3.5f, 2.5f);
            WalkTo(qa, 2.5f, 4.2f);
            WalkTo(qa, 2.0f, 6.4f);
            WalkTo(qa, 1.2f, 8.0f);
            Interact(qa, Case01Ids.LivingBookNote);
            Assert.IsTrue(qa.Hud.NoteVisible, "短觀察顯示在準星下方");
            Assert.IsFalse(qa.Hud.PanelVisible, "生活細節不開側邊面板");
            StringAssert.Contains("便箋", qa.Hud.NoteText.text);
            Interact(qa, Case01Ids.FriendCard);
            Assert.IsTrue(qa.Session.HasJournal("friend_card"), "有意義的生活線索可在日誌重讀");
            // 廚房茶葉盒：真的可以打開／蓋上。
            WalkTo(qa, 2.5f, 6.0f);
            WalkTo(qa, 2.5f, 4.2f);
            WalkTo(qa, 1.4f, 2.6f);
            var lid = qa.Layout.Decor.TeaTinLid;
            var closed = lid.localPosition;
            Interact(qa, Case01Ids.TeaTin);
            Assert.IsTrue(qa.Layout.Decor.TeaTinOpen);
            Assert.AreNotEqual(closed, lid.localPosition, "蓋子實際移開");
            Interact(qa, Case01Ids.TeaTin);
            Assert.IsFalse(qa.Layout.Decor.TeaTinOpen);
            Assert.AreEqual(closed, lid.localPosition);
            Assert.AreEqual(Case01Stage.EnteredHome, qa.Session.Stage, "生活細節不推進主線");
        }

        [Test]
        public void Bedroom_TwoClues_SeparateEllieWishAndResearch()
        {
            var f = new Case01Flow();
            var a = f.Interact(Case01Ids.TravelRoute);
            var b = f.Interact(Case01Ids.JobLetter);
            StringAssert.Contains("帶媽去", a.Note, "個人願望");
            StringAssert.Contains("十七號", b.Note, "主動研究");
            Assert.IsTrue(f.Session.HasJournal("travel_route"));
            Assert.IsTrue(f.Session.HasJournal("job_letter"));
            StringAssert.DoesNotContain("死", a.Note + b.Note);
        }

        // ---------------- B：H0～H3 ----------------

        [Test]
        public void H0_StartsWarmAndComplete()
        {
            var qa = NewQa();
            var d = qa.Layout.Decor;
            Assert.AreEqual(0, qa.HouseStage);
            Assert.IsTrue(d.LivingMissingBooks.All(b => b.activeSelf));
            Assert.AreEqual(2, d.LivingMissingBooks.Count);
            Assert.IsTrue(d.LivingFramePhoto.activeSelf);
            Assert.IsFalse(d.LivingFrameBlank.activeSelf);
            Assert.IsTrue(d.DiningRunner.activeSelf);
            Assert.IsTrue(d.TraySecondCup.activeSelf);
            Assert.AreEqual(d.ChairHomePos, qa.Layout.DiningChairNorth.localPosition);
            Assert.IsFalse(d.StudyDuplicateBook.activeSelf);
            Assert.IsFalse(d.LandingExtraBoxes.activeSelf);
            Assert.AreEqual(d.PictureHomePos, d.LandingPicture.localPosition);
            for (int i = 0; i < d.FileBoxes.Count; i++) Assert.AreEqual(d.FileBoxHome[i], d.FileBoxes[i].localPosition);
            Assert.IsFalse(d.EvidenceRoot.activeSelf, "證物只在核穩定後出現");
        }

        [Test]
        public void H1_LivingAndDiningChangeOnlyWhileOutOfTheRoom_AndRevisitWritesJournal()
        {
            var qa = NewQa();
            var d = qa.Layout.Decor;
            qa.Player.Teleport(new Vector3(6.6f, 0.05f, 6.2f), 0f);
            Run(qa, 0.1f);
            Interact(qa, Case01Ids.Table);
            // 從餐廳走進客廳，一直看著書架：餐具改寫（看不到餐桌），但客廳不當面改變。
            WalkTo(qa, 6.0f, 7.5f);
            WalkTo(qa, 3.6f, 7.2f);
            Assert.AreEqual(Case01Room.Living, qa.CurrentRoom);
            qa.Player.Teleport(qa.Player.transform.position, -90f);
            Run(qa, 0.3f);
            Assert.IsTrue(qa.Session.Has(Case01Flags.TableRewritten));
            Assert.IsTrue(d.DiningChanged, "人不在餐廳：餐廳已變");
            Assert.IsFalse(d.LivingChanged, "人在客廳：客廳不硬閃");
            Assert.IsTrue(d.LivingMissingBooks.All(b => b.activeSelf));
            Assert.IsFalse(d.DiningRunner.activeSelf, "布巾不見");
            Assert.IsFalse(d.TraySecondCup.activeSelf, "托盤只剩一個茶杯");
            Assert.AreNotEqual(d.ChairHomePos, qa.Layout.DiningChairNorth.localPosition, "椅子被推開");
            // 走到廚房：客廳改變；回到客廳：日誌記下差異與原始觀察。
            WalkTo(qa, 2.5f, 6.4f);
            WalkTo(qa, 2.5f, 4.0f);
            Assert.AreEqual(Case01Room.Kitchen, qa.CurrentRoom);
            Assert.IsTrue(d.LivingChanged);
            Assert.AreEqual(0, d.LivingMissingBooks.Count(b => b.activeSelf), "書架少了兩本書");
            Assert.IsTrue(d.LivingFrameBlank.activeSelf);
            Assert.IsFalse(qa.Session.HasJournal("env_living"));
            WalkTo(qa, 2.5f, 6.4f);
            Assert.IsTrue(qa.Session.HasJournal("env_living"));
            StringAssert.Contains("原始觀察", qa.Session.FindJournal("env_living").Text);
            Assert.AreEqual(1, qa.HouseStage);
        }

        [Test]
        public void H2_StudyAndLandingChange_KeyHotspotsStay_AndCheckpointsRebuildConsistently()
        {
            foreach (var cp in new[] { Case01Checkpoint.S30, Case01Checkpoint.S40, Case01Checkpoint.S50 })
            {
                var qa = NewQa(cp);
                var d = qa.Layout.Decor;
                Assert.AreEqual(2, qa.HouseStage, cp.ToString());
                Assert.IsTrue(d.LivingChanged && d.DiningChanged, cp + "：H1 也已套用");
                Assert.IsTrue(d.StudyDuplicateBook.activeSelf, cp + "：同一本書出現兩次");
                Assert.IsTrue(d.LandingExtraBoxes.activeSelf, cp + "：多了紙箱");
                Assert.AreNotEqual(d.PictureHomePos, d.LandingPicture.localPosition, cp + "：掛畫移位");
                Assert.IsTrue(Enumerable.Range(0, 3).Any(i => d.FileBoxes[i].localPosition != d.FileBoxHome[i]), cp + "：檔案盒重排");
                foreach (var id in new[] { Case01Ids.Map, Case01Ids.EllieNotes, Case01Ids.OldRecord, Case01Ids.StudyMark })
                    Assert.IsTrue(qa.Layout.Interactables[id].gameObject.activeInHierarchy, cp + " " + id);
                Assert.IsFalse(d.EvidenceRoot.activeSelf, cp + "：尚未修復");
                StringAssert.Contains("H2", qa.Hud.QaText.text);
            }
        }

        [Test]
        public void H2_LandingChangesWaitUntilPlayerLeaves_AndStudyRevisitIsJournaled()
        {
            var qa = NewQa();
            var f = qa.Flow;
            f.Interact(Case01Ids.Map);
            f.Interact(Case01Ids.EllieNotes);
            f.Interact(Case01Ids.OldRecord);
            qa.Player.Teleport(new Vector3(6.95f, 3.05f, 6.0f), 180f);
            Run(qa, 0.1f);
            Assert.AreEqual(Case01Room.Study, qa.CurrentRoom);
            WalkTo(qa, 6.95f, 3.6f);
            Assert.IsTrue(f.WrongConnectionActive);
            Assert.IsTrue(qa.Layout.Decor.StudyChanged, "書房已在玩家背後改變");
            Assert.IsFalse(qa.Layout.Decor.LandingChanged, "人在平台上，平台不當面改變");
            // 走錯接的門到餐廳：平台改變。
            WalkTo(qa, 6.95f, 4.5f);
            for (int i = 0; i < 90 && qa.CurrentRoom != Case01Room.Dining; i++)
                Tick(qa, Case01Input.From(new ExploreInput { Move = new Vector2(0f, 1f) }));
            Assert.AreEqual(Case01Room.Dining, qa.CurrentRoom, "穿過錯接的書房門（位置 " + qa.Player.transform.position + "，模式 " + qa.Mode + "）");
            Run(qa, 0.5f);
            Assert.IsTrue(qa.Layout.Decor.LandingChanged);
            Assert.IsTrue(qa.Layout.Decor.LandingExtraBoxes.activeSelf);
        }

        [Test]
        public void H3_MostThingsReturn_ButResiduesRemain_AndEvidenceAppears()
        {
            var qa = NewQa(Case01Checkpoint.S50);
            var d = qa.Layout.Decor;
            Stabilize(qa);
            Assert.AreEqual(3, qa.HouseStage);
            Assert.IsTrue(d.LivingMissingBooks.All(b => b.activeSelf), "書回到書架");
            Assert.IsTrue(d.DiningRunner.activeSelf, "布巾回來");
            Assert.AreEqual(d.ChairHomePos, qa.Layout.DiningChairNorth.localPosition, "椅子歸位");
            Assert.IsFalse(d.StudyDuplicateBook.activeSelf);
            Assert.IsFalse(d.LandingExtraBoxes.activeSelf);
            Assert.AreEqual(d.PictureHomePos, d.LandingPicture.localPosition);
            // 殘留。
            Assert.IsTrue(d.LivingFrameBlank.activeSelf, "殘留：客廳相框仍空白");
            Assert.IsFalse(d.TraySecondCup.activeSelf, "殘留：托盤仍只有一個茶杯");
            Assert.IsFalse(qa.Layout.DiningSecondSet.activeSelf, "餐桌仍只有一副");
            Assert.IsTrue(d.EvidenceRoot.activeSelf, "FP-001 在夾層顯現");
            Assert.AreEqual(Case01Mode.Exploring, qa.Mode, "沒有被結束畫面擋住，可以繼續巡查");
            Assert.IsFalse(qa.Hud.EndVisible);
        }

        // ---------------- C／D：FP-001 ----------------

        [Test]
        public void FP001_PickedUpOnce_Inspectable_BackHasRealDetail_AndResetsWithF1()
        {
            var qa = NewQa(Case01Checkpoint.S50);
            Stabilize(qa);
            PressE(qa);
            var d = qa.Layout.Decor;
            WalkTo(qa, 38.3f, 0.6f);
            Interact(qa, Case01Ids.EvidencePhoto);
            Assert.AreEqual(Case01Mode.Inspect, qa.Mode, "拿到鏡頭前檢視");
            Assert.IsFalse(d.EvidenceRoot.activeSelf, "實物已從 3D 世界取走");
            Assert.IsNotNull(qa.InspectObject);
            Assert.AreEqual(1, qa.Session.Evidence.Count);
            Assert.AreEqual(Case01Texts.EvidenceId, qa.Session.Evidence[0]);
            Assert.IsTrue(qa.Session.Has(Case01Texts.EvidenceKey));
            Assert.IsTrue(qa.Session.HasJournal("evidence_fp001"));
            StringAssert.Contains("FP-001", qa.Hud.Base.InspectText.text);
            StringAssert.Contains("餐廳的原件仍留在原處", qa.Hud.Base.InspectText.text, "區分前期調查來源與最後的實體證物");
            StringAssert.DoesNotContain("死亡", qa.Hud.Base.InspectText.text);
            // 背面是真的幾何與文字。
            Assert.IsTrue(qa.InspectObject.GetComponentsInChildren<Text>().Any(t => t.text.Contains("週日")));
            Assert.IsNotNull(qa.InspectObject.Find("TornCorner"));
            // 滑鼠轉動、滾輪縮放、移動暫停。
            var rot = qa.InspectObject.localRotation;
            var pos = qa.Player.transform.position;
            Tick(qa, new Case01Input { Base = new ExploreInput { Look = new Vector2(10f, 0f), Move = new Vector2(0f, 1f) }, Zoom = 2f });
            Assert.Greater(Quaternion.Angle(rot, qa.InspectObject.localRotation), 10f);
            Assert.Less(qa.InspectObject.localPosition.z, 0.5f, "滾輪拉近");
            Assert.AreEqual(pos, qa.Player.transform.position, "檢視時不移動");
            Tick(qa, new Case01Input { Choice = 1 });
            Assert.AreEqual(Case01Mode.Inspect, qa.Mode, "1–3 不會干擾檢視");
            PressE(qa);
            Assert.AreEqual(Case01Mode.Exploring, qa.Mode);
            Assert.IsNull(qa.InspectObject);
            // 只能拿一次；I 可以再看。
            Assert.AreSame(Case01Response.None, qa.Flow.Interact(Case01Ids.EvidencePhoto));
            Assert.AreEqual(1, qa.Session.Evidence.Count);
            Tick(qa, new Case01Input { InspectEvidence = true });
            Assert.AreEqual(Case01Mode.Inspect, qa.Mode);
            Tick(qa, Case01Input.From(new ExploreInput { ReleaseCursor = true }));
            Assert.AreEqual(Case01Mode.Exploring, qa.Mode);
            Assert.IsTrue(qa.CursorCaptured, "Esc 收起證物，不釋放滑鼠");
            StringAssert.Contains("I 檢視證物", qa.Hud.Base.Controls.text);
            Tick(qa, new Case01Input { Journal = true });
            StringAssert.Contains("FP-001", qa.Hud.JournalText.text, "日誌的證物收藏");
            Tick(qa, new Case01Input { Journal = true });

            // F1：重新開始後可以再取得。
            Tick(qa, new Case01Input { Jump = 1 });
            Assert.AreEqual(0, qa.Session.Evidence.Count);
            Assert.IsFalse(qa.Layout.Decor.EvidenceRoot.activeSelf);
            Tick(qa, new Case01Input { Jump = 4 });
            Stabilize(qa);
            Assert.IsTrue(qa.Layout.Decor.EvidenceRoot.activeSelf, "F4 → 修復後再次出現");
        }

        [Test]
        public void FP001_IsOptional_ExitStillWorks_AndPromptMentionsIt()
        {
            var f = Case01Flow.ForCheckpoint(Case01Checkpoint.S50);
            f.Interact(Case01Ids.Core);
            f.Traverse(Case01Door.MezzCircle);
            f.Interact(Case01Ids.Core);
            f.Choose(Case01Ids.ChoiceAnchor, 0);
            f.Traverse(Case01Door.MezzTriangle);
            f.Traverse(Case01Door.Mezz17);
            Assert.AreSame(Case01Response.None, new Case01Flow().Interact(Case01Ids.EvidencePhoto), "修復前拿不到");
            f.Interact(Case01Ids.Core);
            Assert.IsTrue(f.FamilyPhotoAvailable);
            StringAssert.Contains("夾層", f.Interact(Case01Ids.FrontDoor).Choice.Prompt);
            Assert.IsTrue(f.Choose(Case01Ids.ChoiceFrontDoor, 0).Exit, "不拿證物也能結束");
            foreach (var flag in Case01Flags.All) Assert.IsTrue(f.Session.Has(flag), flag);
        }
    }
}
