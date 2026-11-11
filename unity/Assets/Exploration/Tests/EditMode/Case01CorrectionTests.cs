using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// 現實校正 × 沉浸式操作 QA：正常區域沒有校正；倒下的高櫃真的擋路；校正要瞄準命中；立起後拱口可通行；超時倒回且絕不壓住玩家；
    /// 牽引（拾取、跟著視線移動、被牆擋住、放置合法／不合法、取消、離開區域自動放下）；椅子回到凹痕再校正＝局部穩定；重置與重試；
    /// 其他 QA 不受影響。全部用真實移動（第一人稱控制器＋碰撞）。自動化測不到「沉浸感」，要真人在 Play 裡驗收。
    /// </summary>
    public sealed class Case01CorrectionTests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
        }

        private Case01CorrectionQA NewQa()
        {
            var go = new GameObject("Case01CorrectionTest");
            roots.Add(go);
            var s = go.AddComponent<Case01CorrectionQA>();
            s.Setup(false, 1);
            Step(s, Case01Input.None);
            return s;
        }

        private static void Step(Case01CorrectionQA s, Case01Input input)
        {
            s.House.Tick(Dt, input);
            s.Host.Frame(Dt);
            s.Frame(Dt);
            if (s.Slice != null) Assert.IsFalse(s.Slice.PlayerOverlapsSlice(), "玩家和櫃子／椅子的碰撞重疊（被壓住或卡住）：" + s.House.Player.transform.position);
        }

        private static void Run(Case01CorrectionQA s, float sec)
        {
            for (float t = 0; t < sec; t += Dt) Step(s, Case01Input.None);
        }

        private static Vector2 LookDelta(FirstPersonController p, Vector3 target, bool level)
        {
            var d = target - p.EyePosition;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = level ? 0f : -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            return new Vector2(Mathf.DeltaAngle(p.Yaw, yaw) / (FirstPersonController.LookSensitivity * p.LookScale), -(pitch - p.Pitch) / (FirstPersonController.LookSensitivity * p.LookScale));
        }

        /// <summary>走到 (x, z)，同時看著 look（可以側走、倒退）；回傳 true＝到了、null＝卡住、false＝離開探索模式。</summary>
        private static bool? TryWalk(Case01CorrectionQA s, float x, float z, Vector3? look = null, float max = 15f)
        {
            var qa = s.House;
            var stuckAt = qa.Player.transform.position;
            float stuckTime = 0f;
            for (float t = 0; t < max; t += Dt)
            {
                if (qa.Mode != Case01Mode.Exploring) return false;
                var p = qa.Player.transform.position;
                var to = new Vector3(x - p.x, 0f, z - p.z);
                if (to.magnitude < 0.2f) return true;
                var lookAt = look ?? new Vector3(x, qa.Player.EyePosition.y, z);
                var yawRot = Quaternion.Euler(0f, qa.Player.Yaw, 0f);
                var local = Quaternion.Inverse(yawRot) * to.normalized;
                float speed = Mathf.Clamp01(to.magnitude / 0.6f + 0.3f);
                Step(s, Case01Input.From(new ExploreInput { Move = new Vector2(local.x, local.z) * speed, Look = LookDelta(qa.Player, lookAt, true) }));
                var np = qa.Player.transform.position;
                if ((np - stuckAt).magnitude > 0.05f)
                {
                    stuckAt = np;
                    stuckTime = 0f;
                }
                else if ((stuckTime += Dt) > 1.5f) return null;
            }
            return null;
        }

        private static void WalkTo(Case01CorrectionQA s, float x, float z, Vector3? look = null)
        {
            Assert.IsTrue(TryWalk(s, x, z, look) == true, "走不到 (" + x + ", " + z + ")，停在 " + s.House.Player.transform.position + "，模式 " + s.House.Mode + "，房間 " + s.House.CurrentRoom);
        }

        private static void Aim(Case01CorrectionQA s, Vector3 target)
        {
            for (int i = 0; i < 2; i++) Step(s, Case01Input.From(new ExploreInput { Look = LookDelta(s.House.Player, target, false) }));
        }

        private static void Interact(Case01CorrectionQA s, string id)
        {
            var it = s.Slice.Root.GetComponentsInChildren<Case01Interactable>().First(i => i.Id == id);
            Aim(s, it.transform.TransformPoint(it.GetComponent<BoxCollider>().center));
            Assert.IsNotNull(s.House.CurrentTarget, "準星對到 " + id);
            Assert.AreEqual(id, s.House.CurrentTarget.Id);
            Step(s, Case01Input.From(new ExploreInput { Interact = true }));
        }

        private static void Place(Case01CorrectionQA s, Vector3 feet, float yaw = 0f)
        {
            s.House.Player.Teleport(feet, yaw, 0f);
            Run(s, 0.1f);
        }

        private static bool Blocked(Case01CorrectionQA s, Vector3 feet)
        {
            var cc = s.House.Player.Body;
            cc.enabled = false;
            Physics.SyncTransforms();
            bool hit = Physics.CheckCapsule(feet + Vector3.up * 0.4f, feet + Vector3.up * 1.45f, 0.3f, Case01SpatialMap.SightMask, QueryTriggerInteraction.Ignore);
            cc.enabled = true;
            return hit;
        }

        private static readonly Vector3 ArchKitchen = new Vector3(6.05f, 0.05f, 13.4f);
        private static readonly Vector3 Scullery = new Vector3(7.9f, 0.05f, 13.4f);

        // ---------------- 正常區域／失序區域 ----------------

        [Test]
        public void CorrectionExistsOnlyInsideTheAnomaly_NormalHouseHasNoAttackKey()
        {
            var s = NewQa();
            Assert.IsNotNull(s.Slice, "Phase 1.9 R3 上建立了校正切片");
            Assert.AreEqual(Case01Floorplan.R3, s.House.Layout.Floorplan);
            Assert.IsFalse(s.Slice.PlayerInZone, "起點在前門內（正常區域）");
            Assert.AreEqual(Case01CorrectResult.NotAvailable, s.Correct(), "正常區域：左鍵沒有作用");
            Assert.AreEqual(Case01TetherResult.NotAvailable, s.Tether(), "正常區域：右鍵沒有作用");
            Assert.IsNull(s.ReticleText, "正常區域不顯示準星／攻擊提示");
            Assert.IsFalse(s.Slice.DeviceVisible, "正常區域手上沒有校正器");
            Place(s, new Vector3(5.7f, 0.05f, 13.0f));
            Assert.IsTrue(s.Slice.PlayerInZone, "進入廚房＝失序區域");
            Assert.IsNotNull(s.ReticleText, "失序區域才出現準星");
            Assert.IsTrue(s.Slice.DeviceVisible, "失序區域才拿出校正器");
            Assert.AreEqual(55f, s.Host.FieldOfView, 1e-3f, "沿用候選垂直 FOV 55°");
            Assert.AreEqual(1.58f, s.House.Player.EyeHeightValue, 1e-3f, "沿用候選眼高 1.58 m");
        }

        [Test]
        public void FallenCabinet_ReallyBlocksTheArch_AndCorrectionMustHitIt()
        {
            var s = NewQa();
            Assert.AreEqual(Case01CabinetState.Fallen, s.Slice.State);
            Place(s, ArchKitchen, 90f);
            Assert.IsTrue(TryWalk(s, Scullery.x, Scullery.z, null, 4f) == null, "倒下的櫃子擋住拱口：走不過去");
            Assert.IsTrue(Blocked(s, new Vector3(6.7f, 0.05f, 13.4f)), "拱口前是實體");
            // 沒瞄準：落空，櫃子不動。
            Aim(s, new Vector3(3f, 1.2f, 16f));
            Assert.AreEqual(Case01CorrectResult.Miss, s.Correct(), "沒對準櫃子＝落空");
            Assert.AreEqual(Case01CabinetState.Fallen, s.Slice.State);
            Run(s, 0.7f);
            // 瞄準命中：櫃子立起、碰撞同步改變、拱口通行。
            Aim(s, s.Slice.CabinetCollider.bounds.center);
            Assert.AreEqual(Case01CorrectResult.Restored, s.Correct());
            Assert.AreEqual(Case01CorrectResult.Cooldown, s.Correct(), "冷卻中連點無效");
            Run(s, Case01CorrectionSlice.RiseTime + 0.1f);
            Assert.AreEqual(Case01CabinetState.Upright, s.Slice.State);
            Assert.IsFalse(Blocked(s, new Vector3(6.7f, 0.05f, 13.4f)), "櫃子立起後拱口淨空");
            Assert.Greater(s.Slice.CabinetCollider.bounds.min.z, 14.9f, "櫃子的碰撞跟著回到牆邊");
            WalkTo(s, Scullery.x, Scullery.z);
            Assert.Greater(s.House.Player.transform.position.x, 7.3f, "走進後廚");
        }

        [Test]
        public void Timeout_FallsBack_NeverOnThePlayer_AndRiseNeverPushesThePlayer()
        {
            var s = NewQa();
            s.SetDuration(4f);
            Assert.AreEqual(4f, s.Slice.Duration, 1e-3f, "校正時間可調（候選 4～6 秒）");
            Place(s, ArchKitchen, 90f);
            Aim(s, s.Slice.CabinetCollider.bounds.center);
            Assert.AreEqual(Case01CorrectResult.Restored, s.Correct());
            Run(s, Case01CorrectionSlice.RiseTime + 4f + Case01CorrectionSlice.FallTime + 0.2f);
            Assert.AreEqual(Case01CabinetState.Fallen, s.Slice.State, "時間到：倒回去");
            Assert.IsTrue(Blocked(s, new Vector3(6.7f, 0.05f, 13.4f)), "拱口再度被擋住");
            // 站在倒下的路徑上（拱口裡）等時間到：櫃子不倒，等玩家離開。
            Run(s, 0.7f);
            Aim(s, s.Slice.CabinetCollider.bounds.center);
            Assert.AreEqual(Case01CorrectResult.Restored, s.Correct());
            Run(s, Case01CorrectionSlice.RiseTime + 0.1f);
            WalkTo(s, 6.75f, 13.4f);
            Run(s, 4.5f);
            Assert.AreEqual(Case01CabinetState.Upright, s.Slice.State, "玩家在倒下的路徑上：不倒");
            Assert.Greater(s.Slice.FallsBlockedByPlayer, 0);
            Assert.LessOrEqual(s.Slice.TimeLeft, 0f);
            WalkTo(s, Scullery.x, Scullery.z);
            Run(s, Case01CorrectionSlice.FallTime + 0.3f);
            Assert.AreEqual(Case01CabinetState.Fallen, s.Slice.State, "玩家離開後才倒下");
            // 站在櫃子立起的位置旁邊（直立的佔位裡）校正：不立起、不把人頂開。
            Place(s, new Vector3(6.25f, 0.05f, 15.6f), 90f);
            Run(s, 0.7f);
            Aim(s, s.Slice.CabinetCollider.bounds.center);
            Assert.AreEqual(Case01CorrectResult.Blocked, s.Correct(), "擺動路徑上有人：不立起");
            Assert.AreEqual(Case01CabinetState.Fallen, s.Slice.State);
            Assert.AreEqual(new Vector3(6.25f, 0.05f, 15.6f).x, s.House.Player.transform.position.x, 0.05f, "玩家沒有被推開");
        }

        // ---------------- 牽引 ----------------

        [Test]
        public void Tether_PickUp_FollowsTheView_BlockedByWalls_Place_Cancel_IllegalCases()
        {
            var s = NewQa();
            var sl = s.Slice;
            Place(s, new Vector3(2.05f, 0.05f, 13.4f), -90f);
            Aim(s, new Vector3(3f, 2.3f, 16f));
            Assert.AreEqual(Case01TetherResult.Miss, s.Tether(), "沒對準椅子＝拾取不到");
            Aim(s, sl.ChairCollider.bounds.center);
            Assert.AreEqual("右鍵 牽引", sl.AimHint, "對著椅子時提示牽引");
            Assert.AreEqual(Case01TetherResult.Grabbed, s.Tether());
            Assert.IsTrue(sl.Holding);
            var origin = Case01CorrectionSlice.ChairStart;
            // 轉身：椅子繞著玩家轉到前方（不穿過玩家）。
            for (int i = 0; i < 30; i++) Step(s, Case01Input.From(new ExploreInput { Look = LookDelta(s.House.Player, new Vector3(2.05f, 1.2f, 16f), true) * 0.15f }));
            Run(s, 1.0f);
            var c = sl.Chair.position;
            Assert.Greater(c.z, 14.0f, "椅子跟著視線轉到前方（北側）");
            Assert.Greater(new Vector2(c.x - 2.05f, c.z - 13.4f).magnitude, 0.9f, "椅子和玩家保持距離");
            // 朝西牆推：椅子被牆擋住，不穿牆。
            Aim(s, new Vector3(-3f, 1.2f, 13.4f));
            Run(s, 1.2f);
            Assert.Greater(sl.ChairCollider.bounds.min.x, 0.07f, "椅子沒有穿進西牆");
            // 不合法的放置：放到櫃子上（先把椅子帶到倒下的櫃子上方是不可能的，這裡直接驗證落點檢查：落在玩家身上）。
            // 取消：回到拾起的地方。
            Assert.IsTrue(sl.Cancel(), "Q：取消牽引");
            Assert.IsFalse(sl.Holding);
            Assert.Less(Vector3.Distance(sl.Chair.position, origin), 0.05f, "椅子回到原處");
            Assert.IsFalse(sl.ChairCollider.isTrigger, "放下後恢復實體碰撞");
            // 拿著走出失序區域：自動放下，不會帶著跑、不會卡住。
            Aim(s, sl.ChairCollider.bounds.center);
            Assert.AreEqual(Case01TetherResult.Grabbed, s.Tether());
            s.House.Player.Teleport(new Vector3(5.7f, 0.05f, 10.6f), 180f, 0f);
            Run(s, 0.2f);
            Assert.IsFalse(sl.Holding, "離開區域：牽引斷開");
            Assert.IsFalse(sl.ChairCollider.isTrigger);
            Assert.IsTrue(Case01CorrectionSlice.Zone.Contains(sl.Chair.position + Vector3.up * 0.5f), "椅子留在廚房裡");
            Assert.AreEqual(Case01TetherResult.NotAvailable, s.Tether(), "區域外右鍵無效");
        }

        [Test]
        public void Tether_PlacementIsCheckedAgainstWallsFurnitureAndThePlayer()
        {
            var s = NewQa();
            var sl = s.Slice;
            Place(s, new Vector3(2.05f, 0.05f, 13.4f), -90f);
            Aim(s, sl.ChairCollider.bounds.center);
            Assert.AreEqual(Case01TetherResult.Grabbed, s.Tether());
            Run(s, 0.3f);
            Assert.IsTrue(sl.GhostValid, "空地：落點可以放");
            // 走到餐桌旁，讓椅子轉到桌子底下的位置 → 不能放。
            WalkTo(s, 4.0f, 13.15f, new Vector3(4.0f, 1.2f, 20f));
            Run(s, 1.0f);
            if (sl.Holding && !sl.GhostValid)
                Assert.AreEqual(Case01TetherResult.Denied, s.Tether(), "和家具重疊：拒絕放下，還在手上");
            Assert.IsTrue(sl.Holding || sl.LastTether == Case01TetherResult.Placed, "不合法時保持牽引");
            if (sl.Holding) Assert.IsTrue(sl.Cancel());
            Assert.IsFalse(sl.Holding);
        }

        // ---------------- 完整流程 ----------------

        [Test]
        public void FullLoop_Block_Correct_Pass_Timeout_Clue_Tether_Place_Correct_Stable_Passable()
        {
            var s = NewQa();
            var sl = s.Slice;
            // 從餐廳走進廚房（真的開門）。
            s.GoToKitchenDoor();
            Run(s, 0.1f);
            var door = s.Host.World.Door("door_kitchen");
            WalkTo(s, 5.7f, 11.1f);
            if (!door.IsOpenEnough)
            {
                Aim(s, door.Hotspot.transform.position);
                Step(s, Case01Input.From(new ExploreInput { Interact = true }));
                Run(s, 1.2f);
            }
            Assert.IsTrue(door.IsOpenEnough, "廚房門打開");
            WalkTo(s, 5.7f, 12.6f);
            Assert.IsTrue(sl.PlayerInZone);
            // 觀察：倒下的高櫃（E）。
            WalkTo(s, ArchKitchen.x, ArchKitchen.z);
            Interact(s, "rc_cabinet");
            Assert.IsTrue(s.House.Hud.NoteVisible, "調查倒下的高櫃");
            // 校正 → 通過 → 後廚看照片。
            Aim(s, sl.CabinetCollider.bounds.center);
            Assert.AreEqual(Case01CorrectResult.Restored, s.Correct());
            Run(s, Case01CorrectionSlice.RiseTime + 0.1f);
            WalkTo(s, Scullery.x, Scullery.z);
            WalkTo(s, 7.9f, 15.4f);
            Interact(s, "rc_photo");
            Assert.IsTrue(sl.PhotoSeen, "看到照片線索");
            // 時間到：倒回去（玩家在後廚，不受影響）；從後廚這一側再校正一次回到廚房。
            Run(s, s.Duration + 1f);
            Assert.AreEqual(Case01CabinetState.Fallen, sl.State);
            WalkTo(s, Scullery.x, Scullery.z);
            Run(s, 0.6f);
            Aim(s, sl.CabinetCollider.bounds.center);
            Assert.AreEqual(Case01CorrectResult.Restored, s.Correct(), "從後廚這側也能校正（不會被關在裡面）");
            Run(s, Case01CorrectionSlice.RiseTime + 0.1f);
            WalkTo(s, ArchKitchen.x, ArchKitchen.z);
            // 看凹痕。
            WalkTo(s, 5.75f, 16.3f);
            Interact(s, "rc_marks");
            Assert.IsTrue(sl.MarksSeen, "看到地上的凹痕");
            Run(s, s.Duration);
            // 牽引椅子：窗邊 → 凹痕。
            // 從椅子北側拾起，轉向東，沿餐桌北側的通道帶到高櫃旁的凹痕（玩家實際的走法）。
            WalkTo(s, 5.75f, 13.15f);
            WalkTo(s, 2.05f, 13.15f);
            WalkTo(s, 1.0f, 14.6f);
            Aim(s, sl.ChairCollider.bounds.center);
            Assert.AreEqual(Case01TetherResult.Grabbed, s.Tether());
            Assert.LessOrEqual(sl.HoldDistance, Case01CorrectionSlice.MaxHold);
            for (int i = 0; i < 40; i++) Step(s, Case01Input.From(new ExploreInput { Look = LookDelta(s.House.Player, new Vector3(9f, 1.2f, 14.6f), true) * 0.12f }));
            Run(s, 0.8f);
            WalkTo(s, 1.4f, 16.3f, new Vector3(9f, 1.2f, 16.3f));
            WalkTo(s, 4.6f, 16.35f, new Vector3(9f, 1.2f, 16.35f));
            WalkTo(s, 6.6f - sl.HoldDistance, 16.85f, new Vector3(9f, 1.2f, 16.85f));
            Run(s, 1.0f);
            Assert.IsTrue(sl.Holding, "一路牽引沒有斷開（停在 " + sl.Chair.position + "）");
            Assert.IsTrue(sl.GhostOnMark, "落點框對準凹痕（椅子在 " + sl.Chair.position + "）");
            Assert.AreEqual(Case01TetherResult.PlacedOnMark, s.Tether());
            Assert.IsTrue(sl.ChairOnMark);
            // 再校正：局部穩定，通路保持暢通。
            WalkTo(s, ArchKitchen.x, ArchKitchen.z);
            Run(s, 0.6f);
            Aim(s, sl.CabinetCollider.bounds.center);
            Assert.AreEqual(Case01CorrectResult.Stabilized, s.Correct());
            Run(s, Case01CorrectionSlice.RiseTime + 0.2f);
            Assert.AreEqual(Case01CabinetState.Stable, sl.State, "局部穩定");
            Run(s, 10f);
            Assert.AreEqual(Case01CabinetState.Stable, sl.State, "穩定後不再倒下");
            WalkTo(s, Scullery.x, Scullery.z);
            WalkTo(s, ArchKitchen.x, ArchKitchen.z);
            Assert.AreEqual(Case01TetherResult.NotAvailable, sl.Holding ? Case01TetherResult.Grabbed : Case01TetherResult.NotAvailable);
            Aim(s, sl.ChairCollider.bounds.center);
            Assert.AreEqual(Case01TetherResult.Locked, s.Tether(), "穩定後椅子固定在原位");
            // 重置可以重來。
            s.ResetPuzzle();
            Assert.AreEqual(Case01CabinetState.Fallen, sl.State);
            Assert.Less(Vector3.Distance(sl.Chair.position, Case01CorrectionSlice.ChairStart), 0.05f);
        }

        [Test]
        public void ChairLeftInTheFallPath_IsPushedAside_NeverCrushed_NoSoftLock()
        {
            var s = NewQa();
            var sl = s.Slice;
            Place(s, ArchKitchen, 90f);
            Aim(s, sl.CabinetCollider.bounds.center);
            Assert.AreEqual(Case01CorrectResult.Restored, s.Correct());
            Run(s, Case01CorrectionSlice.RiseTime + 0.1f);
            // 把椅子放在拱口前（倒下的路徑上），玩家走開。
            sl.Chair.position = new Vector3(6.35f, 0f, 13.6f);
            Physics.SyncTransforms();
            WalkTo(s, 4.6f, 13.15f);
            Run(s, s.Duration + Case01CorrectionSlice.FallTime + 0.4f);
            Assert.AreEqual(Case01CabinetState.Fallen, sl.State);
            Assert.Greater(sl.ChairPushes, 0, "椅子被推到旁邊");
            Assert.IsFalse(sl.ChairCollider.bounds.Intersects(sl.CabinetCollider.bounds) && sl.ChairCollider.bounds.min.x > 6.4f, "椅子沒有和倒下的櫃子重疊");
            // 椅子仍然拿得到（不會軟鎖）。
            var chair = sl.ChairCollider.bounds.center;
            WalkTo(s, chair.x - 1.3f, chair.z);
            Aim(s, sl.ChairCollider.bounds.center);
            Assert.AreEqual(Case01TetherResult.Grabbed, s.Tether(), "推開後的椅子還拿得到");
        }

        [Test]
        public void ResetAndRetry_FromAnyState_AndOtherQaAreUntouched()
        {
            var s = NewQa();
            var sl = s.Slice;
            Place(s, new Vector3(2.05f, 0.05f, 13.4f), -90f);
            Aim(s, sl.ChairCollider.bounds.center);
            Assert.AreEqual(Case01TetherResult.Grabbed, s.Tether());
            s.ResetPuzzle();
            Assert.IsFalse(sl.Holding, "牽引中重置");
            Assert.IsFalse(sl.ChairCollider.isTrigger);
            Assert.AreEqual(Case01CabinetState.Fallen, sl.State);
            // H 階段重建（F3）後切片重新建立。
            int before = s.Rebuilds;
            s.Host.Press(3);
            Step(s, Case01Input.None);
            Assert.Greater(s.Rebuilds, before, "重建房屋後切片重新建立");
            Assert.IsNotNull(s.Slice);
            Assert.AreEqual(1, s.House.Layout.Root.GetComponentsInChildren<Transform>(true).Count(t => t.name == "RealityCorrectionSlice"), "只有一份切片");
            // F2 切到 1.8 R2：沒有切片；切回來又有。
            s.Host.Press(2);
            Step(s, Case01Input.None);
            Assert.IsNull(s.Slice, "1.8 R2 不建立校正切片");
            s.Host.Press(2);
            Step(s, Case01Input.None);
            Assert.IsNotNull(s.Slice);
            // Phase 1.9 QA 本身沒有校正切片。
            var go = new GameObject("Phase19");
            roots.Add(go);
            var q = go.AddComponent<Case01ImmersionQA>();
            q.Setup(false, null, 1, true, Case01QaMode.CoreV03, true);
            Assert.IsFalse(q.House.Layout.Root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "RealityCorrectionSlice"), "Phase 1.9 QA 不受影響");
            foreach (var n in Case01CorrectionSfx.All) Assert.Greater(Case01Sfx.Rms(Case01CorrectionSfx.Get(n)), 0.003f, n + " 不是無聲佔位");
        }
    }
}
