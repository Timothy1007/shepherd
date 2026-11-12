using System.Linq;
using NUnit.Framework;
using Shepherd.Exploration.Lantern;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// 提燈手感 QA（第一段原型）：光圈內外兩個樣子、提燈開關與光圈大小、手與身體、三個空間可以走通。
    /// 自動化測試只確認規則與結構；畫面好不好看、手感好不好要真人試玩判斷。
    /// </summary>
    public sealed class LanternPrototypeTests
    {
        private const float Dt = 1f / 30f;
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                var qa = root.GetComponent<LanternPrototypeQA>();
                if (qa != null) qa.Shutdown();
                Object.DestroyImmediate(root);
            }
            root = null;
            Assert.AreNotEqual(SimulationMode.Script, Physics.simulationMode, "物理模擬模式已還原（不會影響其他測試）");
            LanternReveal.Clear();
        }

        private LanternPrototypeQA NewQa()
        {
            root = new GameObject("LanternTest");
            var qa = root.AddComponent<LanternPrototypeQA>();
            qa.Setup(false);
            qa.Step(Dt, ExploreInput.None);
            return qa;
        }

        [Test]
        public void World_HasRealAndAnomalyVersions_InDarkAndBrightRooms()
        {
            Assert.IsNotNull(LanternReveal.Shader, "顯示真實 shader 存在");
            Assert.IsTrue(LanternReveal.Shader.isSupported, "shader 可在目前管線使用");
            var qa = NewQa();
            var w = qa.World;
            Assert.GreaterOrEqual(w.RealOnly.Count, 8, "只存在於真實的東西（衣帽架、第二副餐具、桌邊的椅子、站著的高櫃、盆栽的葉子…）");
            Assert.GreaterOrEqual(w.AnomalyOnly.Count, 8, "只存在於異域的東西（不存在的門、倒掛的椅子、倒下的高櫃、枯枝…）");
            Assert.GreaterOrEqual(w.Dual.Count, 30, "同一個東西兩種樣子（牆、地板、照片、門牌、日曆…）");
            foreach (var n in new[] { "Photo", "DoorNumber", "Calendar", "Painting" })
            {
                var r = w.Root.GetComponentsInChildren<Renderer>(true).First(x => x.name == n);
                var m = r.sharedMaterial;
                Assert.AreEqual(LanternReveal.Mode.Dual, LanternReveal.GetMode(m), n);
                Assert.AreNotSame(m.GetTexture("_BaseMap"), m.GetTexture("_AnomalyMap"), n + "：真實與異域是不同的影像");
            }
            Assert.GreaterOrEqual(w.AnomalyLights.Count, 3, "異域有自己的光源");
            Assert.IsTrue(w.Marks.ContainsKey("BrightEntry") && w.Marks.ContainsKey("LivingAlcove"));
        }

        [Test]
        public void Lantern_TogglesAndResizesTheRevealSphere()
        {
            var qa = NewQa();
            var rig = qa.Rig;
            Assert.IsTrue(rig.On);
            Assert.AreEqual(LanternRig.Radii[1], LanternReveal.Radius, 1e-3f, "預設光圈（外圈 1.9 m，越遠越淡）");
            Assert.Less(LanternRig.Radii[1], 2.2f, "光圈像真的燈：不會佔滿半個畫面");
            var ahead = rig.RevealCenter;
            Assert.IsTrue(LanternReveal.IsReal(ahead), "提燈前方顯示真實");
            Assert.IsFalse(LanternReveal.IsReal(ahead + qa.Cam.transform.forward * 2.5f), "幾步之外仍是異域");
            Assert.AreEqual(rig.RevealCenter, (Vector3)Shader.GetGlobalVector(LanternReveal.SphereId), "shader 拿到同一個光圈");

            qa.ToggleLantern();
            for (int i = 0; i < 20; i++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(0f, LanternReveal.Radius, 1e-3f, "F：提燈關掉，整個世界都是異域");
            Assert.IsFalse(rig.Light.enabled);
            Assert.IsFalse(LanternReveal.IsReal(ahead));

            qa.ToggleLantern();
            qa.CycleRadius();
            for (int i = 0; i < 20; i++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(LanternRig.Radii[2], LanternReveal.Radius, 1e-3f, "Q：光圈變大");
            Assert.IsTrue(rig.Light.enabled);
        }

        [Test]
        public void FirstPerson_HandHoldsLantern_AndBodyIsVisibleLookingDown()
        {
            var qa = NewQa();
            var rig = qa.Rig;
            Assert.IsNotNull(rig.ViewCamera, "手與提燈用疊加鏡頭（不穿牆）");
            Assert.AreEqual(0, qa.Cam.cullingMask & (1 << LanternRig.ViewLayer), "主鏡頭不畫手");
            var vp = rig.ViewCamera.WorldToViewportPoint(rig.Glass.position);
            Assert.IsTrue(vp.z > 0f && vp.x > 0f && vp.x < 0.5f && vp.y > 0f && vp.y < 0.6f, "左手的提燈在畫面左下（" + vp + "）");
            var mv = rig.ViewCamera.WorldToViewportPoint(rig.Muzzle.position);
            Assert.IsTrue(mv.z > 0f && mv.x > 0.5f && mv.x < 1f && mv.y > 0f && mv.y < 0.6f, "右手的穩定器在畫面右下（" + mv + "）");
            Assert.IsTrue(rig.ViewRenderers.All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off), "手和提燈不投影");

            var torso = rig.BodyRenderers.First(r => r.name == "Torso");
            var ahead = qa.Cam.WorldToViewportPoint(torso.bounds.center);
            Assert.IsFalse(ahead.z > 0f && ahead.y > 0f && ahead.y < 1f && ahead.x > 0f && ahead.x < 1f, "平視時看不到自己的身體");
            qa.Player.Teleport(qa.Player.transform.position, qa.Player.Yaw, 62f);
            qa.Step(Dt, ExploreInput.None);
            var down = qa.Cam.WorldToViewportPoint(torso.bounds.center);
            Assert.IsTrue(down.z > 0f && down.x > 0f && down.x < 1f && down.y > -0.2f && down.y < 1f, "低頭看得到身體（" + down + "）");
            var shoe = rig.BodyRenderers.First(r => r.name == "ShoeR");
            var sv = qa.Cam.WorldToViewportPoint(shoe.bounds.center);
            Assert.IsTrue(sv.z > 0f && sv.y > 0f && sv.y < 1f, "低頭看得到鞋子（" + sv + "）");
        }

        [Test]
        public void Walk_HallwayToLivingRoomAroundTheBookcase_IntoTheBrightRoom()
        {
            var qa = NewQa();
            var p = qa.Player;
            var route = new[] { new Vector3(1f, 0f, 5.4f), new Vector3(1.4f, 0f, 6.1f), new Vector3(3.2f, 0f, 6.1f), new Vector3(5.4f, 0f, 6.0f), new Vector3(5.8f, 0f, 4.5f), new Vector3(8.2f, 0f, 4.5f) };
            foreach (var target in route)
            {
                bool reached = false;
                for (int i = 0; i < 400 && !reached; i++)
                {
                    var d = target - p.transform.position;
                    d.y = 0f;
                    if (d.magnitude < 0.3f) { reached = true; break; }
                    float want = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                    float look = Mathf.Clamp(Mathf.DeltaAngle(p.Yaw, want), -20f, 20f) / (FirstPersonController.LookSensitivity * p.LookScale);
                    qa.Step(Dt, new ExploreInput { Move = new Vector2(0f, 1f), Look = new Vector2(look, 0f) });
                }
                Assert.IsTrue(reached, "走到 " + target + "（目前 " + p.transform.position + "）");
            }
            Assert.Greater(p.transform.position.x, 7.1f, "進到亮房間");
            Assert.Greater(p.transform.position.y, -0.2f, "沒有掉出地板");
            Assert.AreEqual(LanternPrototypeQA.WalkSpeed, p.WalkSpeedValue, 1e-3f);
        }

        private static void Aim(LanternPrototypeQA qa, Vector3 point)
        {
            var d = point - qa.Player.EyePosition;
            qa.Player.Teleport(qa.Player.transform.position, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
        }

        private static void Act(LanternPrototypeQA qa, int frames, LanternActionInput a)
        {
            for (int i = 0; i < frames; i++) qa.Step(Dt, ExploreInput.None, a);
        }

        [Test]
        public void Restore_RewindsTheFallenCabinet_AndLeavesAPermanentStableArea()
        {
            var qa = NewQa();
            qa.GoTo("LivingAlcove");
            var cab = qa.World.Restorables.First(r => r.name == "Cabinet");
            var stool = qa.World.Restorables.First(r => r.name == "Stool");
            Aim(qa, cab.GetComponent<Collider>().bounds.center);
            Act(qa, 1, new LanternActionInput { SelectMode = 3 });
            Assert.AreEqual(StabilizerMode.Restore, qa.Actions.Mode, "3：復原模式");
            Act(qa, 1, LanternActionInput.None);
            Assert.AreSame(cab, qa.Actions.Aimed, "準星對到倒下的高櫃");
            StringAssert.Contains("復原", qa.PromptText);
            Act(qa, 20, new LanternActionInput { Primary = true });
            Assert.That(cab.Progress, Is.InRange(0.2f, 0.9f), "持續施放：慢慢回到原位");
            Act(qa, 15, LanternActionInput.None);
            float after = cab.Progress;
            Act(qa, 15, LanternActionInput.None);
            Assert.Less(cab.Progress, after, "中途放開會慢慢倒回去");
            Act(qa, 70, new LanternActionInput { Primary = true });
            Assert.IsTrue(cab.Done, "復原完成");
            Assert.AreEqual(1, qa.Actions.Restored);
            Assert.Less(Vector3.Distance(cab.transform.position, cab.FixedPosition), 0.01f);
            Assert.Less(Quaternion.Angle(cab.transform.rotation, cab.FixedRotation), 0.5f, "高櫃站回原位");
            Assert.Greater(LanternReveal.Extra(LanternReveal.TempSlots).w, 0f, "附近永久變成穩定區");
            qa.Rig.SetOn(false);
            for (int i = 0; i < 20; i++) qa.Step(Dt, ExploreInput.None);
            Assert.IsTrue(LanternReveal.IsReal(cab.FixedPosition + Vector3.up * 0.6f), "提燈關掉，穩定區仍是真實");
            Assert.IsFalse(stool.Done, "另一件沒被施放的東西不受影響");
        }

        [Test]
        public void Tether_LiftsTheChairSoftly_ThenPlacingItOnTheDentsSolvesTheSpot()
        {
            var qa = NewQa();
            qa.GoTo("KitchenEntry");
            var chair = qa.World.Tetherables.First(t => t.name == "BlueChair");
            Aim(qa, chair.GetComponent<Collider>().bounds.center);
            Act(qa, 1, new LanternActionInput { SelectMode = 2 });
            Act(qa, 1, LanternActionInput.None);
            Assert.AreSame(chair, qa.Actions.Aimed, "準星對到椅子");
            Act(qa, 1, new LanternActionInput { Primary = true });
            Assert.AreSame(chair, qa.Actions.Held, "左鍵：牽引");
            float y0 = chair.transform.position.y;
            Act(qa, 30, new LanternActionInput { Primary = true });
            Assert.Greater(chair.transform.position.y, y0 + 0.3f, "椅子浮起來");
            qa.Player.Teleport(qa.Player.transform.position, qa.Player.Yaw + 40f, qa.Player.Pitch);
            Act(qa, 2, new LanternActionInput { Primary = true });
            float lag = Vector3.Distance(qa.Actions.HoldPoint, chair.GetComponent<Collider>().bounds.center);
            Assert.Greater(lag, 0.4f, "轉頭的瞬間椅子還沒跟上（不是黏在準星上）");
            Act(qa, 45, new LanternActionInput { Primary = true });
            float settled = Vector3.Distance(qa.Actions.HoldPoint, chair.GetComponent<Collider>().bounds.center);
            Assert.Less(settled, 0.6f, "幾分之一秒後慢慢跟到手前（" + lag + " → " + settled + "）");
            Assert.AreSame(chair, qa.Actions.Held, "一直抓著");
            float heldY = chair.transform.position.y;
            Act(qa, 1, LanternActionInput.None);
            Act(qa, 1, new LanternActionInput { Primary = true });
            Assert.IsNull(qa.Actions.Held, "再按一次放下");
            Assert.IsTrue(chair.Body.useGravity, "放下後恢復重力");
            Act(qa, 45, LanternActionInput.None);
            Assert.Less(chair.transform.position.y, heldY - 0.6f, "放下後落下（可能歪倒或靠在木箱上）");

            var spot = qa.World.Spots[0];
            Assert.IsFalse(spot.Solved);
            chair.Body.position = new Vector3(6.05f, 0.02f, 2.55f);
            chair.Body.rotation = Quaternion.identity;
            chair.transform.SetPositionAndRotation(chair.Body.position, chair.Body.rotation);
            chair.Body.velocity = Vector3.zero;
            Act(qa, 30, LanternActionInput.None);
            Assert.IsTrue(spot.Solved, "椅子放在凹痕上＝放對了");
            Assert.AreEqual(1, qa.Actions.Placed);
        }

        [Test]
        public void ChargedShot_OpensATemporaryBubble_ThroughTheAnomalyWall_IntoTheStoreroom()
        {
            var qa = NewQa();
            qa.GoTo("KitchenWall");
            qa.Rig.SetOn(false);
            Act(qa, 20, LanternActionInput.None);
            var blocker = qa.World.Blockers[0];
            Assert.IsFalse(blocker.Open, "提燈關著：異域牆擋住去路");
            Act(qa, 33, new LanternActionInput { Primary = true });
            Assert.Greater(qa.Actions.Charge, 0.9f, "按住蓄力");
            Act(qa, 1, LanternActionInput.None);
            Assert.AreEqual(1, qa.Actions.ShotsFired, "放開射出");
            Act(qa, 10, LanternActionInput.None);
            Assert.AreEqual(1, qa.Actions.Impacts, "打中牆");
            Assert.Greater(LanternReveal.ActiveBubbles, 0, "撞擊點打開一小塊現實");
            Assert.IsTrue(blocker.Open, "異域牆在現實裡不存在：可以走過去");
            var p = qa.Player;
            for (int i = 0; i < 60; i++) qa.Step(Dt, new ExploreInput { Move = new Vector2(0f, 1f) });
            Assert.Greater(p.transform.position.x, 7.1f, "走進儲藏室（" + p.transform.position + "）");
            for (int i = 0; i < 200; i++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(0, LanternReveal.ActiveBubbles, "撞擊泡泡幾秒後消失");
        }

        [Test]
        public void ChargedShot_HitsAndStunsThePracticeTarget_WithAimAssist()
        {
            var qa = NewQa();
            qa.GoTo("BrightTarget");
            var target = qa.World.Target;
            Aim(qa, target.transform.position + Vector3.up * 0.9f + Vector3.forward * 0.6f);
            Act(qa, 18, new LanternActionInput { Primary = true });
            Act(qa, 1, LanternActionInput.None);
            for (int i = 0; i < 30 && qa.Actions.TargetHits == 0; i++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(1, qa.Actions.TargetHits, "瞄準輔助＋追蹤：沒有完全對準也打得中");
            Assert.IsTrue(target.Stunned, "練習靶暈眩");
            var pos = target.transform.position;
            qa.Step(Dt, ExploreInput.None);
            Assert.Less(Vector3.Distance(pos, target.transform.position), 0.02f, "暈眩時停止移動");
        }

        [Test]
        public void AnomalyLightsFlicker_AndTheAnomalyClockSpins()
        {
            var qa = NewQa();
            var hall = qa.World.AnomalyLights.First(l => l.name == "HallAnomalyLight");
            var seen = new System.Collections.Generic.HashSet<int>();
            var rot0 = qa.World.ClockAnomalyHands.rotation;
            for (int i = 0; i < 90; i++)
            {
                qa.Step(Dt, ExploreInput.None);
                seen.Add(Mathf.RoundToInt(hall.intensity * 100f));
            }
            Assert.Greater(seen.Count, 5, "異域燈會閃");
            Assert.Greater(Quaternion.Angle(rot0, qa.World.ClockAnomalyHands.rotation), 10f, "異域的鐘一直轉");
            Assert.IsTrue(qa.World.RealOnly.Any(r => r.name == "HourHand"), "真實的鐘停在固定時間");
        }
    }
}
