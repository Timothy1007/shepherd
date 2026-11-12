using System.Linq;
using NUnit.Framework;
using Shepherd.Exploration.Lantern;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// 提燈手感 QA（灰盒原型）：光圈內外兩個樣子、雙手、情境操作（左鍵射擊／E 互動／Q 擬態錨定）、能量、
    /// 體感放大的兩層樓住宅、修復線索與異域反撲、場景崩壞後的最終戰。
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
            LanternReveal.Clear();
            Assert.AreNotEqual(SimulationMode.Script, Physics.simulationMode, "物理模擬模式已還原（不會影響其他測試）");
        }

        private LanternPrototypeQA NewQa()
        {
            root = new GameObject("LanternTest");
            var qa = root.AddComponent<LanternPrototypeQA>();
            qa.Setup(false);
            qa.Step(Dt, ExploreInput.None);
            return qa;
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

        private static bool WalkTo(LanternPrototypeQA qa, Vector3 target, int maxFrames = 500, float tolerance = 0.35f)
        {
            var p = qa.Player;
            for (int i = 0; i < maxFrames; i++)
            {
                var d = target - p.transform.position;
                d.y = 0f;
                if (d.magnitude < tolerance) return true;
                float want = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                float look = Mathf.Clamp(Mathf.DeltaAngle(p.Yaw, want), -20f, 20f) / (FirstPersonController.LookSensitivity * p.LookScale);
                qa.Step(Dt, new ExploreInput { Move = new Vector2(0f, 1f), Look = new Vector2(look, 0f) });
            }
            return false;
        }

        // ---------------- 光與視覺 ----------------

        [Test]
        public void World_HasRealAndAnomalyVersions_InDarkAndBrightRooms()
        {
            Assert.IsNotNull(LanternReveal.Shader, "顯示真實 shader 存在");
            Assert.IsTrue(LanternReveal.Shader.isSupported, "shader 可在目前管線使用");
            var qa = NewQa();
            var w = qa.World;
            Assert.GreaterOrEqual(w.RealOnly.Count, 8);
            Assert.GreaterOrEqual(w.AnomalyOnly.Count, 8);
            Assert.GreaterOrEqual(w.Dual.Count, 30);
            foreach (var n in new[] { "Photo", "DoorNumber", "Calendar", "Painting" })
            {
                var m = w.Root.GetComponentsInChildren<Renderer>(true).First(x => x.name == n).sharedMaterial;
                Assert.AreEqual(LanternReveal.Mode.Dual, LanternReveal.GetMode(m), n);
                Assert.AreNotSame(m.GetTexture("_BaseMap"), m.GetTexture("_AnomalyMap"), n + "：真實與異域是不同的影像");
            }
            Assert.GreaterOrEqual(w.AnomalyLights.Count, 3, "異域有自己的光源");
        }

        [Test]
        public void Lantern_TogglesAndResizesTheRevealSphere()
        {
            var qa = NewQa();
            var rig = qa.Rig;
            Assert.AreEqual(LanternRig.Radii[1], LanternReveal.Radius, 1e-3f);
            var ahead = rig.RevealCenter;
            Assert.IsTrue(LanternReveal.IsReal(ahead));
            Assert.IsFalse(LanternReveal.IsReal(ahead + qa.Cam.transform.forward * 3.5f), "幾步之外仍是異域");
            qa.ToggleLantern();
            for (int i = 0; i < 20; i++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(0f, LanternReveal.Radius, 1e-3f, "提燈關掉，整個世界都是異域");
            Assert.IsFalse(rig.Light.enabled);
            qa.ToggleLantern();
            qa.CycleRadius();
            for (int i = 0; i < 20; i++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(LanternRig.Radii[2], LanternReveal.Radius, 1e-3f);
        }

        [Test]
        public void LanternLight_IsAForwardEgg_AndFocusMakesACone()
        {
            var qa = NewQa();
            qa.Step(Dt, ExploreInput.None);
            var c = qa.Rig.RevealCenter;
            var f = qa.Cam.transform.forward;
            var right = qa.Cam.transform.right;
            Assert.IsTrue(LanternReveal.IsReal(c + f * 2.2f), "前方較遠");
            Assert.IsFalse(LanternReveal.IsReal(c + right * 1.3f), "側面較短");
            Assert.IsFalse(LanternReveal.IsReal(c - f * 1.0f), "身後較短");
            float walk = qa.Player.WalkSpeedValue;
            qa.FocusHeld = true;
            for (int i = 0; i < 20; i++) qa.Step(Dt, ExploreInput.None);
            c = qa.Rig.RevealCenter;
            f = qa.Cam.transform.forward;
            right = qa.Cam.transform.right;
            Assert.IsTrue(LanternReveal.IsReal(c + f * 5f), "聚光照到 5 m 外");
            Assert.IsFalse(LanternReveal.IsReal(c + right * 0.7f), "身邊變暗");
            Assert.IsTrue(LanternReveal.IsReal(c + f * 4.5f + right * 0.6f), "越遠越寬（錐形）");
            Assert.Less(qa.Player.WalkSpeedValue, walk * 0.7f, "聚光時走路變慢");
        }

        [Test]
        public void FirstPerson_LanternLeft_StabilizerRight_BodyVisibleLookingDown()
        {
            var qa = NewQa();
            var rig = qa.Rig;
            Assert.AreEqual(qa.Cam.fieldOfView, rig.ViewCamera.fieldOfView, 1e-3f, "手的鏡頭和主鏡頭視野一致（裝置位置不會看起來偏掉）");
            var vp = rig.ViewCamera.WorldToViewportPoint(rig.Glass.position);
            Assert.IsTrue(vp.z > 0f && vp.x > 0f && vp.x < 0.5f && vp.y > 0f && vp.y < 0.6f, "左手提燈在左下（" + vp + "）");
            var mv = rig.ViewCamera.WorldToViewportPoint(rig.Muzzle.position);
            Assert.IsTrue(mv.z > 0f && mv.x > 0.5f && mv.x < 1f && mv.y > 0f && mv.y < 0.6f, "右手穩定器在右下（" + mv + "）");
            var torso = rig.BodyRenderers.First(r => r.name == "Torso");
            qa.Player.Teleport(qa.Player.transform.position, qa.Player.Yaw, 62f);
            qa.Step(Dt, ExploreInput.None);
            var down = qa.Cam.WorldToViewportPoint(torso.bounds.center);
            Assert.IsTrue(down.z > 0f && down.x > 0f && down.x < 1f && down.y > -0.2f && down.y < 1f, "低頭看得到身體（" + down + "）");
        }

        [Test]
        public void Disorder_IsAShadowInTheDark_AndActionsMakeSounds()
        {
            var qa = NewQa();
            var e = qa.Combat.Enemies[0];
            var shell = e.Parts[0].sharedMaterial;
            Assert.Greater(shell.GetColor("_BaseColor").grayscale, shell.GetColor("_AnomalyColor").grayscale * 8f, "光裡看得清楚、暗處幾乎是黑影");
            Assert.Less(shell.GetFloat("_AnomalySmoothness"), 0.2f);
            foreach (var clip in new[] { LanternAudio.Shot, LanternAudio.Impact, LanternAudio.Grab, LanternAudio.RestoreDone, LanternAudio.EnemyWarn, LanternAudio.Hurt, LanternAudio.AmbientDrone })
                Assert.IsTrue(clip != null && clip.length > 0.05f);
            int before = qa.Audio.OneShotsPlayed;
            qa.Actions.Fire(0.5f);
            Assert.Greater(qa.Audio.OneShotsPlayed, before, "射擊有聲音");
        }

        [Test]
        public void AnomalyLightsFlicker_AndTheAnomalyClockSpins()
        {
            var qa = NewQa();
            var hall = qa.World.AnomalyLights.First(l => l.name == "HallAnomalyLight");
            var seen = new System.Collections.Generic.HashSet<int>();
            var rot0 = qa.World.ClockAnomalyHands.rotation;
            for (int i = 0; i < 90; i++) { qa.Step(Dt, ExploreInput.None); seen.Add(Mathf.RoundToInt(hall.intensity * 100f)); }
            Assert.Greater(seen.Count, 5, "異域燈會閃");
            Assert.Greater(Quaternion.Angle(rot0, qa.World.ClockAnomalyHands.rotation), 10f);
        }

        // ---------------- 操作 ----------------

        [Test]
        public void Shots_LandOnTheCrosshair_NotWhereTheHandIs()
        {
            var qa = NewQa();
            qa.GoTo("Spawn");
            qa.Step(Dt, ExploreInput.None);
            Physics.SyncTransforms();
            RaycastHit hit;
            Assert.IsTrue(Physics.Raycast(qa.Cam.transform.position, qa.Cam.transform.forward, out hit, 30f));
            qa.Actions.Fire(0f);
            for (int i = 0; i < 30 && qa.Actions.Impacts == 0; i++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(1, qa.Actions.Impacts);
            Assert.Less(Vector3.Distance(hit.point, qa.Actions.LastImpact), 0.2f, "打在準心對準的地方");
            // 近距離也一樣準（以前從手射出會偏）。
            qa.Player.Teleport(qa.Player.transform.position, qa.Player.Yaw + 90f, 0f);
            qa.Step(Dt, ExploreInput.None);
            Physics.SyncTransforms();
            Assert.IsTrue(Physics.Raycast(qa.Cam.transform.position, qa.Cam.transform.forward, out hit, 30f));
            qa.Actions.Fire(0f);
            for (int i = 0; i < 30 && qa.Actions.Impacts == 1; i++) qa.Step(Dt, ExploreInput.None);
            Assert.Less(Vector3.Distance(hit.point, qa.Actions.LastImpact), 0.2f, "近距離牆面也打在準心上（" + hit.distance.ToString("F2") + " m）");
        }

        [Test]
        public void Energy_LimitsSpamming_AndRegenerates()
        {
            var qa = NewQa();
            qa.GoTo("Spawn");
            int fired = 0;
            for (int i = 0; i < 20; i++)
            {
                Act(qa, 1, new LanternActionInput { Primary = true });
                Act(qa, 1, LanternActionInput.None);
                fired = qa.Actions.ShotsFired;
            }
            Assert.Less(fired, 13, "能量有限：不能無限連發（射了 " + fired + " 發）");
            Assert.Less(qa.Actions.Energy, LanternActions.ShotCost(0f));
            for (int i = 0; i < 150; i++) qa.Step(Dt, ExploreInput.None);
            Assert.Greater(qa.Actions.Energy, 25f, "停手後慢慢回復");
        }

        [Test]
        public void E_GrabsTheChair_LeftClickThrows_EDrops_AndPlacementSolves()
        {
            var qa = NewQa();
            qa.GoTo("KitchenEntry");
            var chair = qa.World.Tetherables.First(t => t.name == "BlueChair");
            Aim(qa, chair.GetComponent<Collider>().bounds.center);
            Act(qa, 1, LanternActionInput.None);
            Assert.AreSame(chair, qa.Actions.Aimed);
            StringAssert.Contains("E 抓起", qa.PromptText);
            Act(qa, 1, new LanternActionInput { Interact = true });
            Assert.AreSame(chair, qa.Actions.Held, "E 抓起（不用切模式）");
            Act(qa, 30, LanternActionInput.None);
            Assert.Greater(chair.transform.position.y, 0.3f, "浮起來");
            int shots = qa.Actions.ShotsFired;
            Act(qa, 15, new LanternActionInput { Primary = true });
            Act(qa, 1, LanternActionInput.None);
            Assert.IsNull(qa.Actions.Held, "抓著時左鍵＝丟出");
            Assert.AreEqual(shots, qa.Actions.ShotsFired, "丟出時不會同時射擊");
            Assert.Greater(chair.Body.velocity.magnitude, 4f);
            Act(qa, 60, LanternActionInput.None);
            Aim(qa, chair.GetComponent<Collider>().bounds.center);
            Act(qa, 1, LanternActionInput.None);
            Act(qa, 1, new LanternActionInput { Interact = true });
            Act(qa, 1, LanternActionInput.None);
            if (qa.Actions.Held != null)
            {
                Act(qa, 1, new LanternActionInput { Interact = true });
                Assert.IsNull(qa.Actions.Held, "再按一次 E 放下");
            }
            var spot = qa.World.Spots.First(s => s.name == "ChairDents");
            chair.Body.position = new Vector3(6.05f, 0.02f, 2.55f);
            chair.Body.rotation = Quaternion.identity;
            chair.transform.SetPositionAndRotation(chair.Body.position, chair.Body.rotation);
            chair.Body.velocity = Vector3.zero;
            Act(qa, 30, LanternActionInput.None);
            Assert.IsTrue(spot.Solved, "放在凹痕上＝放對了");
        }

        [Test]
        public void HoldingE_RestoresTheCabinet_ReleasingRewindsBack()
        {
            var qa = NewQa();
            qa.GoTo("LivingAlcove");
            var cab = qa.World.Restorables.First(r => r.name == "Cabinet");
            Aim(qa, cab.GetComponent<Collider>().bounds.center);
            Act(qa, 1, LanternActionInput.None);
            StringAssert.Contains("按住 E 復原", qa.PromptText);
            Act(qa, 20, new LanternActionInput { Interact = true });
            Assert.That(cab.Progress, Is.InRange(0.2f, 0.9f));
            Act(qa, 15, LanternActionInput.None);
            float after = cab.Progress;
            Act(qa, 15, LanternActionInput.None);
            Assert.Less(cab.Progress, after, "放開會倒回去");
            Act(qa, 70, new LanternActionInput { Interact = true });
            Assert.IsTrue(cab.Done);
            Assert.Greater(LanternReveal.Extra(LanternReveal.TempSlots).w, 0f, "附近永久變成穩定區");
        }

        [Test]
        public void ChargedShot_OpensABubble_ThroughTheAnomalyWall()
        {
            var qa = NewQa();
            qa.GoTo("KitchenWall");
            qa.Rig.SetOn(false);
            Act(qa, 20, LanternActionInput.None);
            var blocker = qa.World.Blockers.First(b => b.name == "AnomalyWall" && !b.transform.IsChildOf(qa.World.HouseRoot));
            Assert.IsFalse(blocker.Open);
            Act(qa, 33, new LanternActionInput { Primary = true });
            Act(qa, 1, LanternActionInput.None);
            Act(qa, 10, LanternActionInput.None);
            Assert.AreEqual(1, qa.Actions.Impacts);
            Assert.IsTrue(blocker.Open, "打開一小塊現實：牆暫時不存在");
            for (int i = 0; i < 60; i++) qa.Step(Dt, new ExploreInput { Move = new Vector2(0f, 1f) });
            Assert.Greater(qa.Player.transform.position.x, 7.1f, "走進儲藏室");
        }

        [Test]
        public void MimicAnchor_MakesAStableField_SlowsEnemies_AndStopsOrbs()
        {
            var qa = NewQa();
            qa.GoTo("ArenaStart");
            qa.Player.Teleport(qa.Player.transform.position, qa.Player.Yaw, 25f);
            Act(qa, 2, LanternActionInput.None);
            Act(qa, 1, new LanternActionInput { Deploy = true });
            Assert.AreEqual(1, qa.Actions.Shields.Count, "Q 展開擬態錨定");
            Assert.AreEqual(LanternActions.ShieldCharges - 1, qa.Actions.ShieldChargesLeft);
            var center = qa.Actions.Shields[0].Center;
            Act(qa, 10, LanternActionInput.None);
            Assert.IsTrue(LanternReveal.IsReal(center + Vector3.up * 0.8f), "場內是真實");
            var e = qa.Combat.Enemies[0];
            e.transform.position = center + Vector3.right * 0.5f;
            Physics.SyncTransforms();
            qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(0.3f, e.SpeedScale, 1e-3f, "失序體在場內變慢");
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            Object.DestroyImmediate(orb.GetComponent<Collider>());
            orb.SetParent(qa.World.Root, true);
            orb.position = center + Vector3.up * 1f + Vector3.left * 0.3f;
            qa.Combat.Orbs.Add(new LanternCombat.Orb { T = orb, Vel = Vector3.left * 2f, Life = 3f });
            qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(0, qa.Combat.Orbs.Count, "光球進不了穩定場");
            for (int i = 0; i < 300; i++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(0, qa.Actions.Shields.Count, "幾秒後消失");
        }

        // ---------------- 戰鬥 ----------------

        private static LanternEnemy PlaceEnemy(LanternPrototypeQA qa, float distance)
        {
            qa.GoTo("ArenaStart");
            qa.Step(Dt, ExploreInput.None);
            var e = qa.Combat.Enemies.First(x => x != null && x.Alive);
            var p = qa.Player.transform.position;
            e.transform.position = new Vector3(p.x + distance, 0f, p.z);
            Physics.SyncTransforms();
            Aim(qa, e.transform.position + Vector3.up * 1.2f);
            return e;
        }

        [Test]
        public void Disorder_ChasesThenWarns_ThenLungesForDamage()
        {
            var qa = NewQa();
            var e = PlaceEnemy(qa, 4.2f);
            int frames = 0;
            while (e.Current != LanternEnemy.State.WindUp && frames++ < 120) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(LanternEnemy.State.WindUp, e.Current, "靠近時先出現攻擊前兆");
            int warn = 0;
            while (e.Current == LanternEnemy.State.WindUp && warn++ < 60) qa.Step(Dt, ExploreInput.None);
            Assert.GreaterOrEqual(warn * Dt, 0.6f);
            for (int i = 0; i < 12; i++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(100f - LanternEnemy.LungeDamage, qa.Combat.Health, 1e-3f, "站著不動會被撲中");
        }

        [Test]
        public void Disorder_StunnedByAShot_DoubleDamage_DropsEnergy_AndRespawnsInTheArena()
        {
            var qa = NewQa();
            var e = PlaceEnemy(qa, 5f);
            Act(qa, 33, new LanternActionInput { Primary = true });
            Act(qa, 1, LanternActionInput.None);
            for (int i = 0; i < 20 && qa.Actions.EnemyHits == 0; i++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(1, qa.Actions.EnemyHits, "瞄準它就打得中");
            Assert.IsTrue(e.Stunned);
            Aim(qa, e.transform.position + Vector3.up * 1.2f);
            Act(qa, 33, new LanternActionInput { Primary = true });
            Act(qa, 1, LanternActionInput.None);
            for (int i = 0; i < 20 && qa.Actions.EnemyHits < 2; i++) qa.Step(Dt, ExploreInput.None);
            Assert.IsFalse(e.Alive, "暈眩中傷害加倍：消散");
            Assert.Greater(qa.Combat.Drops.Count, 0, "掉出能量（和記憶碎片）");
            var dropAt = qa.Combat.Drops[0].position;
            float energy = qa.Actions.Energy;
            qa.Player.Teleport(new Vector3(dropAt.x, 0.05f, dropAt.z), qa.Player.Yaw, 0f);
            for (int i = 0; i < 3; i++) qa.Step(Dt, ExploreInput.None);
            Assert.Greater(qa.Combat.EnergyPickups + qa.Combat.MemoriesFound, 0, "走過去撿起來");
            for (int i = 0; i < 220; i++) qa.Step(Dt, ExploreInput.None);
            Assert.IsTrue(qa.Combat.Enemies.Exists(x => x != null && x.Alive), "戰鬥場會補下一隻");
        }

        [Test]
        public void Player_GoesDown_AndRespawnsWithFullHealth()
        {
            var qa = NewQa();
            qa.GoTo("ArenaStart");
            qa.Step(Dt, ExploreInput.None);
            qa.Combat.Damage(200f);
            Assert.IsTrue(qa.Combat.PlayerDown);
            for (int i = 0; i < 70; i++) qa.Step(Dt, ExploreInput.None);
            Assert.IsFalse(qa.Combat.PlayerDown);
            Assert.AreEqual(LanternCombat.MaxHealth, qa.Combat.Health, 1e-3f);
            Assert.Less(Vector3.Distance(qa.Player.transform.position, qa.World.Marks["ArenaStart"].position), 0.3f);
        }

        // ---------------- 住宅 ----------------

        private static float RoomWidth(LanternPrototypeQA qa, Vector3 from, Vector3 axis)
        {
            RaycastHit a, b;
            Physics.SyncTransforms();
            Assert.IsTrue(Physics.Raycast(from, axis, out a, 20f));
            Assert.IsTrue(Physics.Raycast(from, -axis, out b, 20f));
            return a.distance + b.distance;
        }

        [Test]
        public void House_IsScaledUpForFeel_FurnitureStaysNearRealSize_AndScaleCanBeSwitched()
        {
            var qa = NewQa();
            var w = qa.World;
            Assert.AreEqual(LanternGraybox.HouseDefaultScale, w.HouseScale, 1e-3f);
            var living = w.Marks["HouseLiving"].position + Vector3.up * 2.4f;
            float depth = RoomWidth(qa, new Vector3(living.x - 1.5f, living.y, living.z), Vector3.forward);
            Assert.Greater(depth, 4.2f * 1.25f, "客廳比實際的 4.2 m 深（體感放大）：" + depth.ToString("F2"));
            var hall = w.Marks["HouseEntry"].position + Vector3.up * 1.4f;
            float hallWidth = RoomWidth(qa, hall, Vector3.right);
            Assert.Greater(hallWidth, 1.25f, "玄關走廊比實際 1.05 m 寬：" + hallWidth.ToString("F2"));
            Physics.SyncTransforms();
            RaycastHit up;
            Assert.IsTrue(Physics.Raycast(hall, Vector3.up, out up, 10f));
            Assert.Greater(up.distance + 1.4f, 3.2f, "天花板比實際高");
            var sofaRoot = w.HouseRoot.Find("Sofa");
            Assert.IsNotNull(sofaRoot, "客廳有沙發");
            var sofaRends = sofaRoot.GetComponentsInChildren<Renderer>(true);
            Assert.Greater(sofaRends.Length, 0);
            var sb = sofaRends[0].bounds;
            foreach (var r in sofaRends) sb.Encapsulate(r.bounds);
            Assert.Less(sb.size.y, 1.05f, "沙發維持接近真實的高度（家具不跟著大幅放大）：" + sb.size.y.ToString("F2"));
            Assert.AreEqual(LanternReveal.Mode.Dual, LanternReveal.GetMode(sofaRends[0].sharedMaterial), "家具模型也有真實／異域兩個樣子");
            qa.SetHouseScale(1.6f);
            living = w.Marks["HouseLiving"].position + Vector3.up * 2.4f;
            float bigger = RoomWidth(qa, new Vector3(living.x - 1.5f, living.y, living.z), Vector3.forward);
            Assert.Greater(bigger, depth + 0.5f, "F9：切換房子大小（×1.6）");
            Assert.AreEqual(5, w.Clues.Count, "重建後線索仍是 5 個");
        }

        [Test]
        public void House_StairsLeadUpToTheSecondFloor()
        {
            var qa = NewQa();
            var w = qa.World;
            float S = w.HouseScale;
            var o = LanternGraybox.HouseOrigin;
            Assert.IsTrue(WalkTo(qa, o + new Vector3(4.25f * S, 0f, 3.6f * S)), "走到樓梯口");
            Assert.IsTrue(WalkTo(qa, o + new Vector3(4.25f * S, 0f, 7.85f * S), 600), "爬上樓梯（" + qa.Player.transform.position + "）");
            Assert.Greater(qa.Player.transform.position.y, w.Floor2Y - 0.3f, "到了二樓");
            Assert.IsTrue(WalkTo(qa, o + new Vector3(3.15f * S, 0f, 7.85f * S), 300), "從樓梯頂轉進走廊（" + qa.Player.transform.position + "）");
            Assert.IsTrue(WalkTo(qa, o + new Vector3(3.15f * S, 0f, 5.5f * S), 400), "沿著樓梯井的走廊往前（" + qa.Player.transform.position + "）");
            Assert.Greater(qa.Player.transform.position.y, w.Floor2Y - 0.3f, "沒有掉下樓梯井");
        }

        [Test]
        public void FocusingOnThePhoto_RepairsIt()
        {
            var qa = NewQa();
            var photo = qa.World.Clues.First(c => c.Id == "photo");
            qa.GoTo("HouseLiving");
            Aim(qa, photo.transform.position);
            qa.FocusHeld = true;
            for (int i = 0; i < 90 && !photo.Done; i++) qa.Step(Dt, ExploreInput.None);
            Assert.IsTrue(photo.Done, "聚光照著全家福，真相被固定下來");
            Assert.AreEqual(1, qa.Flow.Repaired);
        }

        [Test]
        public void EleanorsNotebook_OpensUpClose_TurnsPages_CompletesWhenPutDown()
        {
            var qa = NewQa();
            var nb = qa.World.Clues.First(c => c.Id == "notebook");
            var p = nb.transform.position;
            qa.Player.Teleport(new Vector3(p.x + 0.9f, qa.World.Floor2Y + 0.05f, p.z - 0.2f), 0f, 0f);
            Aim(qa, p);
            Act(qa, 1, LanternActionInput.None);
            StringAssert.Contains("拿起來看", qa.PromptText);
            Act(qa, 1, new LanternActionInput { Interact = true });
            Assert.IsNotNull(qa.Inspecting, "拿到眼前近看");
            Assert.AreEqual(LanternInspectable.Kind.Pages, qa.Inspecting.Type);
            StringAssert.Contains("暫定", qa.Inspecting.Pages[0], "內容標明是暫定文字");
            var pos = qa.Player.transform.position;
            for (int i = 0; i < 10; i++) qa.Step(Dt, new ExploreInput { Move = new Vector2(0f, 1f) }, LanternActionInput.None);
            Assert.Less(Vector3.Distance(pos, qa.Player.transform.position), 0.01f, "近看時不能走");
            Act(qa, 1, new LanternActionInput { Primary = true });
            Act(qa, 1, LanternActionInput.None);
            Assert.AreEqual(1, qa.InspectPage, "左鍵翻下一頁");
            Assert.AreEqual(0, qa.Actions.ShotsFired, "近看時左鍵不會射擊");
            Act(qa, 3, LanternActionInput.None);
            Assert.IsFalse(nb.Done, "還拿在手上：還沒算看完");
            Act(qa, 1, new LanternActionInput { Interact = true });
            Act(qa, 1, LanternActionInput.None);
            Assert.IsNull(qa.Inspecting, "E 放下");
            Assert.IsTrue(nb.Done, "放下時才算看完");
            Assert.AreEqual(0, qa.Flow.Backlashes, "放下的瞬間還沒反撲（先預警）");
            Assert.Greater(qa.Flow.WarningTimer, 0f, "燈開始閃（預警）");
            for (int i = 0; i < 60; i++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(1, qa.Flow.Backlashes, "預警之後才反撲");
        }

        [Test]
        public void FuseBox_RestoresPower_AndEverydayThingsCanBeOpened()
        {
            var qa = NewQa();
            var w = qa.World;
            Assert.IsFalse(w.HousePowered, "一開始沒電");
            qa.GoTo("HouseEntry");
            Aim(qa, w.FuseBox.GetComponent<Collider>().bounds.center);
            Act(qa, 1, LanternActionInput.None);
            StringAssert.Contains("推回", qa.PromptText);
            Act(qa, 50, new LanternActionInput { Interact = true });
            Act(qa, 2, LanternActionInput.None);
            Assert.IsTrue(w.HousePowered, "按住 E 推回電箱拉桿：整棟燈亮起");
            Assert.AreEqual(0, qa.Flow.Repaired, "電箱不算線索");
            var fridge = w.Toggles.First(t => t.Label == "冰箱門");
            qa.GoTo("HouseKitchen");
            Aim(qa, fridge.GetComponent<Collider>().bounds.center);
            Act(qa, 1, LanternActionInput.None);
            Assert.AreSame(fridge, qa.Actions.Aimed);
            Act(qa, 1, new LanternActionInput { Interact = true });
            Act(qa, 20, LanternActionInput.None);
            Assert.IsTrue(fridge.IsOpen, "E 打開冰箱門");
            Assert.IsTrue(fridge.Lights[0].enabled, "冰箱裡的燈亮了");
            Assert.AreEqual(0, qa.Flow.Backlashes, "生活互動不會召喚失序體");
        }

        [Test]
        public void HoldingTheBlueChair_MakesItsSpotGlow_AndThingsFallingOutComeBack()
        {
            var qa = NewQa();
            var w = qa.World;
            var blue = w.Tetherables.First(t => t.name == "HouseBlueChair");
            var spot = w.Spots.First(s => s.Expected == blue);
            qa.GoTo("HouseScullery");
            Aim(qa, blue.GetComponent<Collider>().bounds.center);
            Act(qa, 1, LanternActionInput.None);
            Act(qa, 1, new LanternActionInput { Interact = true });
            Act(qa, 2, LanternActionInput.None);
            Assert.AreSame(blue, qa.Actions.Held);
            Assert.IsTrue(spot.Highlight && spot.Glow.enabled, "抓著藍坐墊椅子時，廚房的凹痕會發光");
            StringAssert.Contains("廚房高櫃前", qa.PromptText, "提示該放哪");
            Act(qa, 1, new LanternActionInput { Interact = true });
            Act(qa, 2, LanternActionInput.None);
            Assert.IsFalse(spot.Glow.enabled, "放下後不再發光");
            blue.Body.position = new Vector3(0f, -6f, 0f);
            blue.transform.position = blue.Body.position;
            qa.Step(Dt, ExploreInput.None);
            Assert.Less(Vector3.Distance(blue.transform.position, blue.HomePosition), 0.2f, "掉出世界的東西回到原位");
        }

        [Test]
        public void InspectingThePhoto_Flips_AndTheClock_Rotates()
        {
            var qa = NewQa();
            var photo = qa.World.Inspectables.First(i => i.Title == "床頭的合照");
            qa.BeginInspect(photo);
            Assert.AreEqual(LanternInspectable.Kind.Image, qa.Inspecting.Type);
            Act(qa, 1, new LanternActionInput { Primary = true });
            Act(qa, 1, LanternActionInput.None);
            Assert.IsTrue(qa.InspectFlipped, "左鍵翻面");
            qa.EndInspect();
            var clock = qa.World.Inspectables.First(i => i.Title == "壁爐上的鐘");
            qa.BeginInspect(clock);
            Assert.AreEqual(LanternInspectable.Kind.Object, qa.Inspecting.Type);
            qa.Step(Dt, new ExploreInput { Look = new Vector2(3f, 0f) }, LanternActionInput.None);
            qa.EndInspect();
            Assert.IsTrue(clock.Seen);
        }

        [Test]
        public void F10_SkipsStraightToTheFinalBattle()
        {
            var qa = NewQa();
            qa.Flow.SkipToBoss();
            for (int i = 0; i < 100; i++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(LanternFlow.Phase.Boss, qa.Flow.Current);
            Assert.IsTrue(qa.Flow.Boss != null && qa.Flow.Boss.Alive);
        }

        [Test]
        public void RepairingAClue_TriggersBacklash_FinishingAllCollapsesIntoTheFinalBattle()
        {
            var qa = NewQa();
            var w = qa.World;
            var chair = w.Clues.First(c => c.Id == "chair");
            qa.GoTo("HouseDining");
            Aim(qa, chair.GetComponent<Collider>().bounds.center);
            Act(qa, 1, LanternActionInput.None);
            Act(qa, 70, new LanternActionInput { Interact = true });
            Act(qa, 2, LanternActionInput.None);
            Assert.IsTrue(chair.Done, "按住 E 把艾琳的椅子復原");
            Assert.AreEqual(1, qa.Flow.Repaired);
            for (int i = 0; i < 60; i++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(1, qa.Flow.Backlashes, "修好一處（預警後）＝異域反撲");
            Assert.IsTrue(qa.Combat.Enemies.Any(e => e != null && e.Alive && w.HouseBounds.Contains(e.transform.position + Vector3.up)), "失序體在住宅裡出現");
            Act(qa, 30, LanternActionInput.None);
            Assert.Less(w.HouseLightScale, 0.6f, "住宅熄燈");
            foreach (var c in w.Clues) if (!c.Done) c.ForceComplete();
            for (int i = 0; i < 45; i++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(LanternFlow.Phase.Collapsing, qa.Flow.Current, "最後一個完成（預警後）＝場景崩壞");
            for (int i = 0; i < 100; i++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(LanternFlow.Phase.Boss, qa.Flow.Current);
            Assert.IsTrue(w.ArenaBounds.Contains(qa.Player.transform.position + Vector3.up), "被拉進異域深處");
            Assert.IsTrue(qa.Flow.Boss != null && qa.Flow.Boss.Alive, "最後的反撲");
            Assert.IsFalse(qa.Combat.Enemies.Any(e => e != null && e.Alive), "小怪清空");
        }

        [Test]
        public void Boss_ThrowsHurtfulWords_ShootingOneRevealsAWarmMemory_AndDefeatEndsTheCase()
        {
            var qa = NewQa();
            foreach (var c in qa.World.Clues) c.ForceComplete();
            qa.Flow.BeginCollapse();
            for (int i = 0; i < 100; i++) qa.Step(Dt, ExploreInput.None);
            var boss = qa.Flow.Boss;
            Assert.IsNotNull(boss);
            for (int i = 0; i < 150 && qa.Flow.Phrases.Count == 0; i++) qa.Step(Dt, ExploreInput.None);
            Assert.Greater(qa.Flow.Phrases.Count, 0, "丟出傷人的話");
            var phrase = qa.Flow.Phrases[0];
            Assert.IsTrue(qa.Actions.Targets().Contains(phrase), "話語可以被瞄準");
            phrase.Shatter();
            Assert.AreEqual(1, qa.Flow.WarmLinesShown, "射碎後浮現溫暖的另一面");
            for (int i = 0; i < 20; i++) boss.Hit(1f, true);
            Assert.IsFalse(boss.Alive);
            for (int i = 0; i < 90; i++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(LanternFlow.Phase.Ended, qa.Flow.Current, "擊倒＝結束");
        }
    }
}
