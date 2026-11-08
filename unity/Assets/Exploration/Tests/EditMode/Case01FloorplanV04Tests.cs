using System.Collections.Generic;
using NUnit.Framework;
using Shepherd.Battle.Core;
using Shepherd.Battle.UI;
using Shepherd.Exploration.Case01;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// v0.4 Stage D：一樓格局 A/B。格局 B＝玄關↔餐廳開口封住、樓梯真的反向上行（北端起步、往南上到第三扇門旁）。
    /// 用真正的移動與碰撞走完整條路線（v0.3 校正＋FP-001＋前門出口；v0.2 修復路線），並確認原版格局與正式 QA 不受影響。
    /// </summary>
    public sealed class Case01FloorplanV04Tests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
        }

        private Case01HouseQA NewQa(Case01Floorplan plan, CombatLabScenario scenario = null, Case01Checkpoint? cp = null, Case01QaMode? mode = null)
        {
            var go = new GameObject("Case01FloorplanTest");
            roots.Add(go);
            var qa = go.AddComponent<Case01HouseQA>();
            qa.Build(false, scenario, 1, cp, mode, plan);
            return qa;
        }

        private static CombatLabScenario QuickVictory()
        {
            var s = new CombatLabScenario { Name = "quick-victory" };
            s.Enemies.Add(new EnemyState { Id = 1, Name = "E1 失序體・甲", Value = 1, Corrections = 1, AttackPattern = new[] { 12 } });
            s.Enemies.Add(new EnemyState { Id = 2, Name = "E2 失序體・乙", Value = 1, Corrections = 1, AttackPattern = new[] { 6 } });
            return s;
        }

        // ---------------- 腳本化輸入（與 Case01HouseQATests 相同的換算） ----------------

        private static void Tick(Case01HouseQA qa, Case01Input input) { qa.Tick(Dt, input); }

        private static void Run(Case01HouseQA qa, float s)
        {
            for (float t = 0; t < s; t += Dt) qa.Tick(Dt, Case01Input.None);
        }

        private static void RunUntil(Case01HouseQA qa, System.Func<bool> done, string what, float max = 20f)
        {
            for (float t = 0; t < max && !done(); t += Dt) qa.Tick(Dt, Case01Input.None);
            Assert.IsTrue(done(), "逾時：" + what + "（" + qa.Mode + "，" + qa.Player.transform.position + "）");
        }

        private static void Settle(Case01HouseQA qa) { RunUntil(qa, () => qa.Mode == Case01Mode.Exploring, "傳送結束", 5f); }

        private static Vector2 LookDelta(FirstPersonController p, Vector3 target, bool level)
        {
            var d = target - p.EyePosition;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = level ? 0f : -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            return new Vector2(Mathf.DeltaAngle(p.Yaw, yaw) / FirstPersonController.LookSensitivity, -(pitch - p.Pitch) / FirstPersonController.LookSensitivity);
        }

        /// <summary>走到目標（水平 0.25 m 內）。傳送打斷回傳 false；走不到回傳 null（被牆擋住）。</summary>
        private static bool? TryWalk(Case01HouseQA qa, float x, float z, float max = 12f)
        {
            var stuckAt = qa.Player.transform.position;
            float stuckTime = 0f;
            for (float t = 0; t < max; t += Dt)
            {
                if (qa.Mode != Case01Mode.Exploring) return false;
                var p = qa.Player.transform.position;
                if (new Vector2(x - p.x, z - p.z).magnitude < 0.25f) return true;
                Tick(qa, Case01Input.From(new ExploreInput { Move = new Vector2(0f, 1f), Look = LookDelta(qa.Player, new Vector3(x, qa.Player.EyePosition.y, z), true) }));
                if ((qa.Player.transform.position - stuckAt).magnitude > 0.05f)
                {
                    stuckAt = qa.Player.transform.position;
                    stuckTime = 0f;
                }
                else if ((stuckTime += Dt) > 1.5f) return null;
            }
            return null;
        }

        private static void WalkTo(Case01HouseQA qa, float x, float z)
        {
            var r = TryWalk(qa, x, z);
            Assert.IsTrue(r == true, "走不到 (" + x + ", " + z + ")，停在 " + qa.Player.transform.position + "，房間 " + qa.CurrentRoom);
        }

        private static void WalkThrough(Case01HouseQA qa, float x, float z, Case01Room expect)
        {
            Assert.IsTrue(TryWalk(qa, x, z) == false, "應在門洞觸發傳送（停在 " + qa.Player.transform.position + "）");
            Settle(qa);
            Assert.AreEqual(expect, qa.CurrentRoom);
        }

        private static void Interact(Case01HouseQA qa, string id)
        {
            var it = qa.Layout.Interactables[id];
            var target = it.transform.TransformPoint(it.GetComponent<BoxCollider>().center);
            for (int i = 0; i < 2; i++) Tick(qa, Case01Input.From(new ExploreInput { Look = LookDelta(qa.Player, target, false) }));
            Assert.IsNotNull(qa.CurrentTarget, "準星對到 " + id + "（" + qa.Player.transform.position + "）");
            Assert.AreEqual(id, qa.CurrentTarget.Id);
            if (qa.Hud.PanelVisible && qa.PanelSourceId == id) Tick(qa, Case01Input.From(new ExploreInput { Interact = true }));
            Tick(qa, Case01Input.From(new ExploreInput { Interact = true }));
        }

        private static void Choose(Case01HouseQA qa, int n)
        {
            Assert.IsNotNull(qa.Hud.ActiveChoice, "有選項可選");
            Tick(qa, new Case01Input { Choice = n });
        }

        private static void CloseText(Case01HouseQA qa)
        {
            if (qa.Hud.PanelVisible) Tick(qa, Case01Input.From(new ExploreInput { Interact = true }));
        }

        // ---------------- 格局 B 的路線 ----------------

        /// <summary>玄關 → 廚房 → 客廳 → 餐廳（玄關↔餐廳已封住）。</summary>
        private static void EntranceToDiningB(Case01HouseQA qa)
        {
            WalkTo(qa, 5.8f, 2.5f);
            WalkTo(qa, 3.5f, 2.5f);
            WalkTo(qa, 2.5f, 4.2f);
            WalkTo(qa, 2.5f, 6.4f);
            WalkTo(qa, 4.2f, 7.0f);
            WalkTo(qa, 4.2f, 7.5f);
            WalkTo(qa, 6.0f, 7.5f);
            WalkTo(qa, 6.4f, 6.6f);
            Assert.AreEqual(Case01Room.Dining, qa.CurrentRoom, "經客廳走進餐廳");
        }

        private static void DiningToEntranceB(Case01HouseQA qa)
        {
            WalkTo(qa, 6.0f, 7.5f);
            WalkTo(qa, 4.2f, 7.5f);
            WalkTo(qa, 4.2f, 7.0f);
            WalkTo(qa, 2.5f, 6.4f);
            WalkTo(qa, 2.5f, 4.2f);
            WalkTo(qa, 3.5f, 2.5f);
            WalkTo(qa, 5.8f, 2.5f);
            WalkTo(qa, 6.8f, 2.4f);
            Assert.AreEqual(Case01Room.Entrance, qa.CurrentRoom);
        }

        /// <summary>玄關東北角起步，往南爬上反向樓梯，到第三扇門旁的平台。</summary>
        private static void EntranceToLandingB(Case01HouseQA qa)
        {
            WalkTo(qa, 8.2f, 3.4f);
            WalkTo(qa, 8.2f, 4.5f);
            WalkTo(qa, 9.4f, 4.55f);
            WalkTo(qa, 9.4f, 1.3f);
            Assert.Greater(qa.Player.transform.position.y, 2.5f, "往南上行");
            WalkTo(qa, 9.4f, 0.5f);
            WalkTo(qa, 8.2f, 0.5f);
            WalkTo(qa, 6.95f, 1.6f);
            WalkTo(qa, 6.95f, 3.6f);
            Assert.AreEqual(Case01Room.Landing, qa.CurrentRoom, "到二樓平台");
            Assert.Greater(qa.Player.transform.position.y, 2.9f);
        }

        private static void LandingToEntranceB(Case01HouseQA qa)
        {
            WalkTo(qa, 8.2f, 0.5f);
            WalkTo(qa, 9.4f, 0.5f);
            WalkTo(qa, 9.4f, 4.5f);
            Assert.Less(qa.Player.transform.position.y, 0.3f, "往北下到一樓");
            WalkTo(qa, 8.2f, 4.5f);
            WalkTo(qa, 7.5f, 2.6f);
            Assert.AreEqual(Case01Room.Entrance, qa.CurrentRoom);
        }

        private static void LandingToStudy(Case01HouseQA qa)
        {
            WalkTo(qa, 6.95f, 4.3f);
            WalkTo(qa, 6.95f, 5.8f);
            Assert.AreEqual(Case01Room.Study, qa.CurrentRoom);
        }

        private static void StudyToLanding(Case01HouseQA qa)
        {
            WalkTo(qa, 6.95f, 6.0f);
            WalkTo(qa, 6.95f, 4.2f);
            Assert.AreEqual(Case01Room.Landing, qa.CurrentRoom);
        }

        private static void LandingToMezzanine(Case01HouseQA qa)
        {
            WalkTo(qa, 6.95f, 2.0f);
            WalkThrough(qa, 6.95f, -0.6f, Case01Room.Mezzanine);
        }

        private static void MezzTo17(Case01HouseQA qa)
        {
            WalkTo(qa, 38.5f, 0f);
            WalkTo(qa, 38.5f, 4.5f);
            WalkTo(qa, 40f, 4.6f);
            WalkThrough(qa, 40f, 6.7f, Case01Room.Entrance);
        }

        // ---------------- 幾何 ----------------

        [Test]
        public void OriginalLayout_IsUnchanged_AndStaysTheDefault()
        {
            var go = new GameObject("DefaultQa");
            roots.Add(go);
            var def = go.AddComponent<Case01HouseQA>();
            def.Build(false);
            Assert.AreEqual(Case01Floorplan.Original, def.Floorplan, "正式 QA 維持原版格局");
            Assert.IsFalse(def.FloorplanQa);
            var L = def.Layout;
            Assert.AreEqual(Case01HouseBuilder.StairZ0, L.StairBottomZ);
            Assert.AreEqual(Case01HouseBuilder.StairZ1, L.StairTopZ);
            Assert.IsFalse(Physics.Linecast(new Vector3(6.5f, 1f, 4.4f), new Vector3(6.5f, 1f, 5.6f)), "原版：玄關↔餐廳開口");
        }

        [Test]
        public void LayoutB_SealsEntranceDining_ReversesStair_KeepsRoomsPortalsHotspots()
        {
            var qa = NewQa(Case01Floorplan.V04);
            var L = qa.Layout;
            Assert.AreEqual(Case01Floorplan.V04, L.Floorplan);
            Assert.IsTrue(Physics.Linecast(new Vector3(6.5f, 1f, 4.4f), new Vector3(6.5f, 1f, 5.6f)), "玄關↔餐廳開口已用有碰撞的牆封住");
            Assert.IsFalse(Physics.Linecast(new Vector3(4.4f, 1f, 7.5f), new Vector3(5.6f, 1f, 7.5f)), "客廳↔餐廳仍相通");
            Assert.IsFalse(Physics.Linecast(new Vector3(4.4f, 1f, 2.5f), new Vector3(5.6f, 1f, 2.5f)), "廚房↔玄關仍相通");
            Assert.IsFalse(Physics.Linecast(new Vector3(2.5f, 1f, 4.4f), new Vector3(2.5f, 1f, 5.6f)), "廚房↔客廳仍相通");
            Assert.AreEqual(Case01HouseBuilder.StairZ1V04, L.StairBottomZ, "北端起步");
            Assert.AreEqual(Case01HouseBuilder.StairZ0, L.StairTopZ, "往南上到平台");
            Assert.That(L.StairAngle, Is.InRange(40f, 46f), "坡度在爬坡上限（50°）內");
            // 起步空間與平台到達區都是空的（只有地板）。
            Assert.IsFalse(Physics.CheckCapsule(new Vector3(9.4f, 0.45f, 4.55f), new Vector3(9.4f, 1.45f, 4.55f), 0.34f), "北端起步空間");
            Assert.IsFalse(Physics.CheckCapsule(new Vector3(9.4f, 3.45f, 0.5f), new Vector3(9.4f, 4.45f, 0.5f), 0.34f), "南側到達平台");
            Assert.IsTrue(Physics.Raycast(new Vector3(9.4f, 5f, 0.5f), Vector3.down, 2.2f), "到達處有樓板");
            Assert.IsFalse(Physics.Raycast(new Vector3(9.4f, 5f, 2.5f), Vector3.down, 1.9f), "樓梯井開口（樓板不擋頭）");
            Assert.AreEqual(7, L.Portals.Count, "通路不變");
            Assert.AreEqual(10, L.Rooms.Count, "房間範圍不變（沒有擴建或偷加房間）");
            foreach (var room in new[] { Case01Room.Entrance, Case01Room.Dining, Case01Room.Study, Case01Room.Landing, Case01Room.Mezzanine, Case01Room.Courtyard })
                Assert.AreEqual(room, L.RoomAt(L.Arrivals[room].Position), "到達點：" + room);
            foreach (var id in new[] { Case01Ids.Table, Case01Ids.Photo, Case01Ids.FrontDoor, Case01Ids.Map, Case01Ids.EllieNotes, Case01Ids.OldRecord, Case01Ids.StudyMark, Case01Ids.Core })
                Assert.IsTrue(L.Interactables.ContainsKey(id), id);
            StringAssert.Contains("客廳", qa.ObjectiveText(), "提示依格局改寫");
        }

        [Test]
        public void LayoutB_RoomsConnect_DirectDoorBlocked_ReversedStairUpAndDown()
        {
            var qa = NewQa(Case01Floorplan.V04);
            WalkTo(qa, 6.5f, 4.3f);
            Assert.IsNull(TryWalk(qa, 6.5f, 5.8f, 4f), "玄關不能直走進餐廳");
            Assert.AreEqual(Case01Room.Entrance, qa.CurrentRoom);
            EntranceToDiningB(qa);
            DiningToEntranceB(qa);
            EntranceToLandingB(qa);
            LandingToStudy(qa);
            StudyToLanding(qa);
            LandingToEntranceB(qa);
            // 錯接傳送到餐廳後回得來（沒有被封住）。
            var s30 = NewQa(Case01Floorplan.V04, null, Case01Checkpoint.S30);
            s30.Player.Teleport(new Vector3(Case01HouseBuilder.StudyDoorX, Case01HouseBuilder.FloorHeight + 0.05f, 4.2f), 0f);
            Run(s30, 0.1f);
            WalkThrough(s30, Case01HouseBuilder.StudyDoorX, 5.8f, Case01Room.Dining);
            DiningToEntranceB(s30);
            EntranceToLandingB(s30);
        }

        // ---------------- 完整流程 ----------------

        [Test]
        public void LayoutB_FullPlaythrough_V03Correction_FP001_FrontDoor()
        {
            var qa = NewQa(Case01Floorplan.V04, QuickVictory(), Case01Checkpoint.S00, Case01QaMode.CoreV03);
            Assert.AreEqual(Case01CorrectionMode.RevisedV03, qa.Flow.Correction);

            // 餐具（經客廳）→ 離開 → 回來只剩一副。
            EntranceToDiningB(qa);
            Interact(qa, Case01Ids.Table);
            WalkTo(qa, 8.8f, 6.0f);
            WalkTo(qa, 8.8f, 7.6f);
            Interact(qa, Case01Ids.Photo);
            CloseText(qa);
            WalkTo(qa, 8.8f, 6.0f);
            WalkTo(qa, 6.3f, 6.0f);
            DiningToEntranceB(qa);
            Run(qa, 0.1f);
            EntranceToDiningB(qa);
            Assert.IsTrue(qa.Session.Has(Case01Flags.TableRewritten));
            DiningToEntranceB(qa);

            // 反向樓梯 → 書房線索 → 錯接到餐廳。
            EntranceToLandingB(qa);
            LandingToStudy(qa);
            WalkTo(qa, 6.7f, 8.2f);
            Interact(qa, Case01Ids.Map);
            Interact(qa, Case01Ids.EllieNotes);
            WalkTo(qa, 7.5f, 7.7f);
            WalkTo(qa, 8.7f, 7.5f);
            Interact(qa, Case01Ids.OldRecord);
            CloseText(qa);
            WalkTo(qa, 7.5f, 7.0f);
            StudyToLanding(qa);
            WalkTo(qa, 6.95f, 4.3f);
            WalkThrough(qa, 6.95f, 5.8f, Case01Room.Dining);
            Assert.IsTrue(qa.Session.Has(Case01Flags.RouteMismatchSeen), "書房門錯接到餐廳");
            DiningToEntranceB(qa);
            EntranceToLandingB(qa);
            WalkTo(qa, 6.3f, 3.9f);
            Interact(qa, Case01Ids.StudyMark);
            Choose(qa, 1);
            Assert.IsTrue(qa.Flow.StairRouteRestored);
            CloseText(qa);
            LandingToMezzanine(qa);

            // 核 → 反制 → 中庭戰鬥（既有規則）。
            WalkTo(qa, 40f, -0.4f);
            Interact(qa, Case01Ids.Core);
            RunUntil(qa, () => qa.Mode == Case01Mode.Exploring && qa.CurrentRoom == Case01Room.Courtyard && qa.Manifested, "中庭與形成演出", 12f);
            Assert.IsTrue(TryWalk(qa, 40f, 46f, 15f) == false, "走近即遭遇");
            RunUntil(qa, () => qa.Mode == Case01Mode.Battle, "進入戰鬥");
            var arena = qa.Arena;
            Assert.AreEqual(7, arena.Session.Snapshot().Hand.Count, "戰鬥規則不變");
            arena.CommitButton.onClick.Invoke();
            RunUntil(qa, () => !arena.IsPlaying, "事件播放");
            Assert.AreEqual(BattleOutcome.Victory, arena.Session.Snapshot().Outcome);
            arena.BannerButton.onClick.Invoke();
            RunUntil(qa, () => qa.Mode == Case01Mode.Exploring, "返回探索");

            // v0.3 關係校正：錨點 △→書房、第二關係 17→玄關、穿 17 號門寫回。
            WalkTo(qa, 40f, 25.6f);
            WalkThrough(qa, 40f, 23.4f, Case01Room.Mezzanine);
            WalkTo(qa, 40f, -0.4f);
            Interact(qa, Case01Ids.Core);
            Choose(qa, 1);
            Assert.IsTrue(qa.Flow.V03Anchored);
            Interact(qa, Case01Ids.Core);
            Choose(qa, 1);
            Assert.IsTrue(qa.Flow.V03SecondLink);
            CloseText(qa);
            MezzTo17(qa);
            Assert.IsTrue(qa.Flow.CoreStabilized, "穿門寫回");
            Run(qa, 2f);

            // 回到夾層拿 FP-001（反向樓梯＋第三扇門），檢視後收起，再從 17 號門回玄關離開。
            EntranceToLandingB(qa);
            LandingToMezzanine(qa);
            Assert.IsTrue(qa.Flow.FamilyPhotoAvailable);
            var ev = qa.Layout.Interactables[Case01Ids.EvidencePhoto].transform.position;
            WalkTo(qa, ev.x + 1.2f, ev.z);
            Interact(qa, Case01Ids.EvidencePhoto);
            Assert.AreEqual(Case01Mode.Inspect, qa.Mode, "FP-001 檢視");
            Tick(qa, Case01Input.From(new ExploreInput { Interact = true }));
            Assert.AreEqual(Case01Mode.Exploring, qa.Mode);
            Assert.IsTrue(qa.Flow.FamilyPhotoCollected);
            MezzTo17(qa);
            Interact(qa, Case01Ids.FrontDoor);
            Choose(qa, 1);
            Assert.AreEqual(Case01Mode.Ended, qa.Mode, "前門出口");
            foreach (var flag in Case01Flags.All) Assert.IsTrue(qa.Session.Has(flag), flag);
        }

        [Test]
        public void LayoutB_V02Repair_RoutesThroughLivingAndReversedStair()
        {
            var qa = NewQa(Case01Floorplan.V04, null, Case01Checkpoint.S50);
            Assert.AreEqual(Case01CorrectionMode.LegacyV02, qa.Flow.Correction);
            WalkTo(qa, 40f, -0.4f);
            Interact(qa, Case01Ids.Core);
            Assert.IsTrue(qa.Flow.Door17Open);
            CloseText(qa);
            WalkTo(qa, 44f, 0f);
            WalkThrough(qa, 46.6f, 0f, Case01Room.Study);
            StudyToLanding(qa);
            LandingToMezzanine(qa);
            WalkTo(qa, 40f, -0.4f);
            Interact(qa, Case01Ids.Core);
            Choose(qa, 2);
            Settle(qa);
            Assert.AreEqual(Case01Room.Dining, qa.CurrentRoom, "錨點錯誤：送到餐廳");
            DiningToEntranceB(qa);
            EntranceToLandingB(qa);
            LandingToMezzanine(qa);
            WalkTo(qa, 40f, -0.4f);
            Interact(qa, Case01Ids.Core);
            Choose(qa, 1);
            CloseText(qa);
            WalkTo(qa, 36f, 0f);
            WalkThrough(qa, 33.4f, 0f, Case01Room.Study);
            StudyToLanding(qa);
            LandingToMezzanine(qa);
            MezzTo17(qa);
            EntranceToLandingB(qa);
            LandingToMezzanine(qa);
            WalkTo(qa, 40f, -0.4f);
            Interact(qa, Case01Ids.Core);
            Assert.IsTrue(qa.Flow.CoreStabilized, "v0.2 三段修復在格局 B 也能完成");
        }

        [Test]
        public void FloorplanQa_F9TogglesAB_JumpsKeepLayout_F5KeepsLayout()
        {
            var qa = NewQa(Case01Floorplan.V04);
            Assert.IsTrue(qa.FloorplanQa);
            StringAssert.Contains("F9", qa.Hud.Base.ViewMode.text);
            qa.Tick(Dt, new Case01Input { Function = 9 });
            Assert.AreEqual(Case01Floorplan.Original, qa.Floorplan);
            Assert.AreEqual(Case01HouseBuilder.StairZ0, qa.Layout.StairBottomZ, "切回原版");
            qa.Tick(Dt, new Case01Input { Function = 9 });
            Assert.AreEqual(Case01Floorplan.V04, qa.Floorplan);
            qa.Tick(Dt, new Case01Input { Jump = 3 });
            Assert.AreEqual(Case01Floorplan.V04, qa.Layout.Floorplan, "F1～F4 保留格局");
            qa.Tick(Dt, new Case01Input { Function = 5 });
            Assert.AreEqual(Case01Floorplan.V04, qa.Layout.Floorplan, "F5 切核校正模式也保留格局");
            Assert.AreEqual(Case01QaMode.CoreV03, qa.QaMode);
        }
    }
}
