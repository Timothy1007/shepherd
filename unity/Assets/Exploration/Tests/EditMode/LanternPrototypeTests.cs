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

        /// <summary>從某個點位出發，沿直線走到目標前 dist 公尺（E 互動要走近才有）。</summary>
        private static void Approach(LanternPrototypeQA qa, string mark, Vector3 target, float dist = 1.2f)
        {
            qa.GoTo(mark);
            var p = qa.Player.transform.position;
            var d = target - p; d.y = 0f;
            if (d.magnitude > dist) qa.Player.Teleport(p + d.normalized * (d.magnitude - dist), 0f, 0f);
            qa.Step(Dt, ExploreInput.None);
            Aim(qa, target);
        }

        private static Vector3 ChairTarget(LanternClue chair)
        {
            var col = chair.Restorable != null ? chair.Restorable.GetComponent<Collider>() : chair.GetComponent<Collider>();
            return col.bounds.center;
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
            // 平常畫面乾淨：提燈和穩定器都收在畫面外。
            for (int i = 0; i < 60; i++) qa.Step(Dt, ExploreInput.None);
            Assert.Less(rig.ViewCamera.WorldToViewportPoint(rig.Glass.position).y, 0f, "平常提燈收在畫面下方外面");
            Assert.Less(rig.ViewCamera.WorldToViewportPoint(rig.Muzzle.position).y, 0f, "平常穩定器也收起來");
            // 聚光時提燈舉進畫面左下；做事（按住左鍵蓄力）時穩定器伸進右下。
            qa.FocusHeld = true;
            Act(qa, 30, new LanternActionInput { Primary = true });
            qa.FocusHeld = false;
            var vp = rig.ViewCamera.WorldToViewportPoint(rig.Glass.position);
            Assert.IsTrue(vp.z > 0f && vp.x > 0f && vp.x < 0.5f && vp.y > 0f && vp.y < 0.6f, "聚光時左手提燈在左下（" + vp + "）");
            var mv = rig.ViewCamera.WorldToViewportPoint(rig.Muzzle.position);
            Assert.IsTrue(mv.z > 0f && mv.x > 0.5f && mv.x < 1f && mv.y > 0f && mv.y < 0.6f, "做事時右手穩定器在右下（" + mv + "）");
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
            Assert.AreEqual(LanternReveal.Mode.RealOnly, LanternReveal.GetMode(sofaRends[0].sharedMaterial), "沙發只存在於真實（有人住的家）");
            var sheet = w.HouseRoot.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "DustSheet").ToList();
            Assert.Greater(sheet.Count, 5, "廢墟裡的家具蓋著防塵布");
            Assert.AreEqual(LanternReveal.Mode.AnomalyOnly, LanternReveal.GetMode(sheet[0].sharedMaterial));
            Assert.IsNotNull(sofaRoot.GetComponent<Collider>(), "暗處也不會穿過沙發");
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
            Approach(qa, "HouseEntry", w.FuseBox.GetComponent<Collider>().bounds.center);
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
            Approach(qa, "HouseDining", ChairTarget(chair));
            Act(qa, 1, LanternActionInput.None);
            string firstAim = (qa.Actions.Aimed != null ? qa.Actions.Aimed.name : "null") + "/" + qa.PromptText + "/eye " + qa.Cam.transform.position.ToString("F2") + " chair " + ChairTarget(chair).ToString("F2") + " home " + chair.transform.position.ToString("F2");
            Act(qa, 70, new LanternActionInput { Interact = true });
            Act(qa, 2, LanternActionInput.None);
            if (!chair.Done) Assert.Fail("對準：" + firstAim);
            Assert.IsTrue(chair.Done, "按住 E 把艾琳的椅子復原（提示：" + qa.PromptText + "，距離 " + Vector3.Distance(qa.Cam.transform.position, ChairTarget(chair)).ToString("F2") + "）");
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
            Assert.IsTrue(w.BossBounds.Contains(qa.Player.transform.position + Vector3.up), "被拉進她的記憶深處（放大的客廳）");
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
            for (int i = 0; i < 60 && boss.Alive; i++) { boss.Lit = true; boss.Hit(1f, true); }
            Assert.IsFalse(boss.Alive);
            for (int i = 0; i < 90; i++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(LanternFlow.Phase.Ending, qa.Flow.Current, "擊倒＝結局演出");
            for (int i = 0; i < (int)(LanternFlow.EndingSeconds / Dt) + 10; i++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(LanternFlow.Phase.Ended, qa.Flow.Current, "結案");
        }

        // ---------------- v0.6：廢墟與家、燈光、找路、最終戰 ----------------

        [Test]
        public void RepairingAClue_MakesTheWholeRoomReal_SpreadingFromTheClue()
        {
            var qa = NewQa();
            var w = qa.World;
            var chair = w.Clues.First(c => c.Id == "chair");
            var corner = LanternGraybox.HouseOrigin + new Vector3(0.4f * w.HouseScale, 1.2f, 4.6f * w.HouseScale);
            qa.Rig.SetOn(false);
            Act(qa, 5, LanternActionInput.None);
            Assert.IsFalse(LanternReveal.IsReal(corner), "修好之前，餐廳的角落是廢墟");
            qa.Rig.SetOn(true);
            Approach(qa, "HouseDining", ChairTarget(chair));
            Act(qa, 1, LanternActionInput.None);
            Act(qa, 70, new LanternActionInput { Interact = true });
            Assert.IsTrue(chair.Done);
            Assert.AreEqual(1, LanternReveal.ActiveRooms, "整個房間被標記成永久真實");
            qa.Rig.SetOn(false);
            Act(qa, 60, LanternActionInput.None);
            Assert.IsTrue(LanternReveal.IsReal(corner), "提燈關著，餐廳角落也已經是真實的家");
            var living = LanternGraybox.HouseOrigin + new Vector3(1.0f * w.HouseScale, 1.2f, 2.0f * w.HouseScale);
            Assert.IsFalse(LanternReveal.IsReal(living), "別的房間還是廢墟");
        }

        [Test]
        public void Power_IsAStrongContrast_BacklashBlacksOutUntilTheWaveIsDefeated()
        {
            var qa = NewQa();
            var w = qa.World;
            Act(qa, 10, LanternActionInput.None);
            float dark = 0f;
            for (int i = 0; i < w.HouseLamps.Count; i++) dark = Mathf.Max(dark, w.LampIntensity(i));
            w.FuseBox.ForceDone();
            Act(qa, 60, LanternActionInput.None);
            Assert.IsTrue(w.HousePowered);
            float bright = w.LampIntensity(0);
            Assert.Greater(bright, dark * 6f, "有電＝很亮（" + bright.ToString("F2") + "），沒電幾乎全黑（" + dark.ToString("F2") + "）");
            Assert.Greater(qa.AmbientLevel, 0.8f, "有電時環境光也變亮");
            qa.Combat.Invulnerable = true;
            qa.Flow.Backlash();
            Act(qa, 30, LanternActionInput.None);
            Assert.IsTrue(qa.Flow.WaveActive);
            Assert.Less(w.LampIntensity(0), 0.01f, "反撲：燈全部熄滅");
            Assert.Less(qa.AmbientLevel, 0.2f);
            Act(qa, 300, LanternActionInput.None);
            Assert.IsTrue(qa.Flow.WaveActive, "過了 10 秒還是黑的：要打倒這一波燈才會回來");
            foreach (var e in qa.Combat.Enemies) if (e != null && e.Alive) for (int k = 0; k < 6 && e.Alive; k++) e.Hit(1f, true);
            Act(qa, 60, LanternActionInput.None);
            Assert.IsFalse(qa.Flow.WaveActive);
            Assert.AreEqual(1, qa.Flow.WavesCleared);
            Assert.Greater(w.LampIntensity(0), bright * 0.8f, "打倒之後燈回來了");
        }

        [Test]
        public void Backlash_SpawnsNearThePlayer_ButOutOfView()
        {
            var qa = NewQa();
            var w = qa.World;
            qa.GoTo("HouseDining");
            Act(qa, 2, LanternActionInput.None);
            qa.Flow.Backlash();
            var e = qa.Combat.Enemies.First(x => x != null && x.Alive && w.HouseBounds.Contains(x.transform.position + Vector3.up));
            var corners = new System.Collections.Generic.List<Vector3>();
            Assert.IsTrue(LanternNav.Path(e.transform.position, qa.Player.transform.position, corners), "走得到玩家身邊");
            float len = LanternNav.Length(corners);
            Assert.That(len, Is.InRange(5f, 16.5f), "出現在附近（路徑 " + len.ToString("F1") + " m）");
            var vp = qa.Cam.WorldToViewportPoint(e.transform.position + Vector3.up * 1.1f);
            bool onScreen = vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;
            bool blocked = Physics.Linecast(qa.Cam.transform.position, e.transform.position + Vector3.up * 1.1f, ~(1 << LanternRig.ViewLayer), QueryTriggerInteraction.Ignore);
            Assert.IsTrue(!onScreen || blocked, "出現的瞬間看不到它");
        }

        [Test]
        public void Disorder_FindsItsWayThroughTheHouse_AndUpTheStairs()
        {
            var qa = NewQa();
            var w = qa.World;
            Assert.IsTrue(LanternNav.Has("house"), "住宅有 NavMesh");
            var corners = new System.Collections.Generic.List<Vector3>();
            Assert.IsTrue(LanternNav.Path(w.Marks["HouseKitchen"].position, w.Marks["HouseEleanor"].position, corners), "從廚房走得到二樓艾琳的房間（經過樓梯）");
            qa.GoTo("HouseLiving");
            Act(qa, 2, LanternActionInput.None);
            qa.Combat.Invulnerable = true;
            var e = qa.Combat.Spawn(w.Marks["HouseScullery"].position);
            float start = Vector3.Distance(e.transform.position, qa.Player.transform.position);
            bool reached = false;
            for (int i = 0; i < 600 && !reached; i++)
            {
                qa.Step(Dt, ExploreInput.None);
                if (Vector3.Distance(e.transform.position, qa.Player.transform.position) < LanternEnemy.LungeRange + 0.6f) reached = true;
            }
            Assert.IsTrue(reached, "從後廚繞過家具與門口，追到客廳（起點 " + start.ToString("F1") + " m，現在 " + Vector3.Distance(e.transform.position, qa.Player.transform.position).ToString("F1") + " m）");
            Assert.AreEqual(0, qa.Combat.Relocations, "一路上沒有卡住");
        }

        [Test]
        public void StuckDisorder_ReappearsSomewhereElse()
        {
            var qa = NewQa();
            var w = qa.World;
            qa.GoTo("HouseLiving");
            Act(qa, 2, LanternActionInput.None);
            // 放在一個走不到的地方（櫃子頂上）。
            var e = qa.Combat.Spawn(w.HouseRoot.Find("Wardrobe").position + Vector3.up * 1.6f);
            for (int i = 0; i < 200 && qa.Combat.Relocations == 0; i++) qa.Step(Dt, ExploreInput.None);
            Assert.Greater(qa.Combat.Relocations, 0, "卡住的失序體換個地方重新出現（state " + e.Current + " pos " + e.transform.position + " stuck " + e.StuckCount + " path " + e.UsingPath + " spawn " + qa.Combat.LastSpawnDebug + " area " + w.HouseInterior + "）");
        }

        [Test]
        public void FinalBattle_DarkIsImmune_ShootingASwitchLightsAnArea_ThresholdsBlackOut()
        {
            var qa = NewQa();
            qa.Flow.SkipToBoss();
            for (int i = 0; i < 100; i++) qa.Step(Dt, ExploreInput.None);
            var boss = qa.Flow.Boss;
            var w = qa.World;
            Assert.AreEqual(4, w.Switches.Count, "四個角落有電燈開關");
            Assert.AreEqual(0, w.SwitchesOn, "一開始全暗");
            float hp = boss.Hp;
            boss.Hit(1f, false);
            Assert.Less(hp - boss.Hp, 0.4f, "暗處幾乎打不痛");
            Assert.Greater(qa.Flow.DamageNumbers, 0, "顯示「無效」");
            Assert.Greater(qa.Flow.DarkHintShown, 0, "提示要開燈");
            // 射擊開關。
            var sw = w.Switches[3];
            qa.Player.Teleport(sw.Zone + Vector3.back * 0.5f, 0f, 0f);
            qa.Step(Dt, ExploreInput.None);
            Aim(qa, sw.GetComponent<Collider>().bounds.center);
            Act(qa, 1, LanternActionInput.None);
            Act(qa, 2, new LanternActionInput { Primary = true });
            Act(qa, 1, LanternActionInput.None);
            for (int i = 0; i < 30 && !sw.On; i++) qa.Step(Dt, ExploreInput.None);
            Assert.IsTrue(sw.On, "射中開關：燈亮了");
            Assert.IsTrue(sw.Lights[0].enabled && sw.Lights[0].intensity > 1f);
            Assert.IsTrue(LanternReveal.IsReal(sw.Zone + Vector3.up * 1.2f), "燈照到的地方恢復成真實的客廳");
            boss.transform.position = sw.Zone + Vector3.right * 1.5f;
            qa.Step(Dt, ExploreInput.None);
            Assert.IsTrue(boss.Lit);
            hp = boss.Hp;
            boss.Hit(1f, false);
            Assert.Greater(hp - boss.Hp, 2.5f, "在光裡打得痛");
            while (boss.Alive && boss.Hp > LanternBoss.MaxHp * 2f / 3f) { boss.Hit(1f, false); if (!sw.On) break; }
            Assert.AreEqual(1, qa.Flow.Blackouts, "生命掉到 2/3：它把燈全部熄掉");
            Assert.AreEqual(0, w.SwitchesOn);
        }

        [Test]
        public void Ending_ReturnsToTheRealHouse_ThePhotoIsWhole_ThenTheCaseCloses()
        {
            var qa = NewQa();
            qa.Flow.SkipToBoss();
            for (int i = 0; i < 100; i++) qa.Step(Dt, ExploreInput.None);
            var boss = qa.Flow.Boss;
            foreach (var s in qa.World.Switches) s.Hit();
            boss.transform.position = qa.World.Switches[0].Zone;
            qa.Step(Dt, ExploreInput.None);
            while (boss.Alive) { foreach (var s in qa.World.Switches) s.Hit(); boss.Lit = true; boss.Hit(1f, true); }
            for (int i = 0; i < 120; i++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(LanternFlow.Phase.Ending, qa.Flow.Current);
            Assert.IsTrue(qa.World.HouseBounds.Contains(qa.Player.transform.position + Vector3.up), "回到家裡");
            Assert.IsTrue(qa.World.HousePowered, "有電、燈亮著");
            for (int i = 0; i < 150; i++) qa.Step(Dt, ExploreInput.None);
            var photo = qa.World.Clues.First(c => c.Id == "photo");
            Assert.IsTrue(LanternReveal.IsReal(photo.transform.position), "全家福恢復（女兒回到照片裡）");
            var study = qa.World.Marks["HouseStudy"].position + Vector3.up * 1.2f;
            for (int i = 0; i < 90; i++) qa.Step(Dt, ExploreInput.None);
            Assert.IsTrue(LanternReveal.IsReal(study), "所有房間都恢復真實");
            Assert.GreaterOrEqual(qa.Flow.EndingLine, 0, "出現結局的幾行字");
            for (int i = 0; i < (int)(LanternFlow.EndingSeconds / Dt); i++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(LanternFlow.Phase.Ended, qa.Flow.Current, "結案卡");
        }

        // ---------------- v0.7a：月光、安全據點、客廳樣板 ----------------

        [Test]
        public void Windows_AreRealOpenings_AndMoonlightComesIn()
        {
            var qa = NewQa();
            var w = qa.World;
            Assert.IsNotNull(w.Moon, "有月光");
            Assert.AreEqual(LightType.Directional, w.Moon.type);
            Assert.AreNotEqual(LightShadows.None, w.Moon.shadows, "月光有影子（窗洞投出光斑）");
            Assert.GreaterOrEqual(w.WindowPanes.Count, 7, "每個主要房間都有窗");
            Assert.IsTrue(w.WindowPanes.All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off), "玻璃不擋光");
            // 從客廳裡往前窗看出去：穿過窗洞，打不到牆。
            var o = LanternGraybox.HouseOrigin;
            float S = w.HouseScale, Hs = w.HouseHeightScale;
            var inside = o + new Vector3(1.8f * S, 1.5f * Hs, 1.0f * S);
            Physics.SyncTransforms();
            var hits = Physics.RaycastAll(inside, Vector3.back, 3f);
            Assert.IsFalse(hits.Any(h => h.collider.name == "Wall" || h.collider.name == "Lintel"), "前窗是真的洞");
            var boards = w.HouseRoot.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "WindowBoard").ToList();
            Assert.Greater(boards.Count, 10);
            Assert.AreEqual(LanternReveal.Mode.AnomalyOnly, LanternReveal.GetMode(boards[0].sharedMaterial), "廢墟裡窗戶被釘死");
        }

        [Test]
        public void RepairedRoom_KeepsItsWarmLight_AndDisordersCannotEnter()
        {
            var qa = NewQa();
            var w = qa.World;
            qa.Combat.Invulnerable = true;
            var chair = w.Clues.First(c => c.Id == "chair");
            chair.ForceComplete();
            Act(qa, 70, LanternActionInput.None);
            Assert.AreEqual(1, w.SafeRooms.Count, "餐廳變成安全據點");
            Assert.IsNotNull(chair.RoomLamp);
            qa.GoTo("HouseDining");
            Act(qa, 2, LanternActionInput.None);
            Assert.IsTrue(w.InSafeRoom(qa.Player.transform.position));
            qa.Flow.Backlash();
            bool entered = false;
            for (int i = 0; i < 300; i++)
            {
                qa.Step(Dt, ExploreInput.None);
                foreach (var e in qa.Combat.Enemies) if (e != null && e.Alive && w.InSafeRoom(e.transform.position)) entered = true;
            }
            Assert.IsTrue(qa.Flow.WaveActive, "反撲中");
            Assert.Greater(chair.RoomLamp.intensity, 1f, "修好的房間在反撲時燈仍亮著");
            Assert.IsFalse(entered, "失序體進不了修好的房間");
            Assert.Less(w.LampIntensity(0), 0.01f, "玄關還是黑的");
        }

        [Test]
        public void LivingRoom_IsDense_LampNeedsPower_SecretBehindTheFireScreen()
        {
            var qa = NewQa();
            var w = qa.World;
            Assert.GreaterOrEqual(w.LivingInteractables, 8, "客廳至少 8 個能碰的東西（" + w.LivingInteractables + "）");
            foreach (var n in new[] { "UnfinishedScarf", "MugA", "MugB" })
            {
                var r = w.HouseRoot.Find(n).GetComponentInChildren<Renderer>();
                Assert.AreEqual(LanternReveal.Mode.RealOnly, LanternReveal.GetMode(r.sharedMaterial), n + " 只在光裡看得到");
            }
            var lamp = w.Toggles.First(t => t.Label == "立燈");
            lamp.Toggle();
            Act(qa, 3, LanternActionInput.None);
            Assert.IsFalse(lamp.Lights[0].enabled, "沒電：立燈不會亮");
            w.FuseBox.ForceDone();
            Act(qa, 60, LanternActionInput.None);
            Assert.IsTrue(lamp.Lights[0].enabled, "有電：立燈亮了");
            var screen = w.Toggles.First(t => t.Label == "壁爐的防火屏");
            screen.Toggle();
            Act(qa, 30, LanternActionInput.None);
            var secret = w.Inspectables.First(i => i.Secret);
            qa.BeginInspect(secret);
            qa.EndInspect();
            Assert.AreEqual(1, qa.SecretsFound, "找到記憶碎片");
            Assert.IsFalse(qa.NotebookOpen, "平常不顯示提示（Tab 才打開母親的筆記）");
        }

        // ---------------- v2：新版房子（英式聯排屋＋改衣店）灰盒 ----------------

        [Test]
        public void V2_FrontDoor_OnlyExistsInLanternLight_AndSealsBehindYou()
        {
            var qa = NewQa();
            var w = qa.World;
            Assert.IsNotNull(w.V2Root, "新版房子存在");
            qa.GoTo("V2Alley");
            qa.Rig.SetOn(false);
            Act(qa, 20, LanternActionInput.None);
            Assert.IsFalse(w.V2FrontDoor.Open, "提燈放下：門是釘死的");
            for (int i = 0; i < 90; i++) qa.Step(Dt, new ExploreInput { Move = new Vector2(0f, 1f) });
            Assert.Less(qa.Player.transform.position.z - LanternGraybox.V2Origin.z, 0.1f, "暗處走不進去");
            qa.Rig.SetOn(true);
            bool sealedNow = false;
            for (int i = 0; i < 150 && !sealedNow; i++) { qa.Step(Dt, new ExploreInput { Move = new Vector2(0f, 1f) }); sealedNow = w.V2Sealed; }
            Assert.IsTrue(sealedNow, "舉起提燈：門出現，走進去之後封起來");
            Assert.IsTrue(w.V2SealWall.gameObject.activeSelf);
            var inside = qa.Player.transform.position;
            for (int i = 0; i < 90; i++) qa.Step(Dt, new ExploreInput { Move = new Vector2(0f, -1f) });
            Assert.Greater(qa.Player.transform.position.z - LanternGraybox.V2Origin.z, 0.2f, "回不去了");
        }

        [Test]
        public void V2_GroundFloor_IsLargerAndLoops_DoorsOpen()
        {
            var qa = NewQa();
            var w = qa.World;
            var o = LanternGraybox.V2Origin;
            float S = LanternGraybox.V2Scale;
            Physics.SyncTransforms();
            var shopMid = o + new Vector3(2.0f * S, 2.3f, 2.5f * S); // 高過衣架與人台，量牆到牆
            float width = RoomWidth(qa, shopMid, Vector3.right);
            Assert.Greater(width, 7.5f, "店面比舊客廳寬（" + width.ToString("F2") + " m）");
            var corners = new System.Collections.Generic.List<Vector3>();
            Assert.IsTrue(LanternNav.Path(w.Marks["V2Shop"].position, w.Marks["V2Kitchen"].position, corners), "店面走得到廚房");
            Assert.IsTrue(LanternNav.Path(w.Marks["V2Kitchen"].position, w.Marks["V2Yard"].position, corners), "廚房走得到側院（回圈）");
            Assert.IsTrue(LanternNav.Path(w.Marks["V2Yard"].position, w.Marks["V2Hall"].position, corners), "側院從後門回到走廊");
            Assert.GreaterOrEqual(w.V2Doors.Count, 4, "有真的門");
            var door = w.V2Doors[0];
            door.Toggle();
            Act(qa, 30, LanternActionInput.None);
            Assert.IsTrue(door.IsOpen);
            Assert.Greater(Quaternion.Angle(Quaternion.identity, door.Moving.localRotation), 80f, "門轉開了");
            Assert.GreaterOrEqual(w.WindowPanes.Count(r => r.transform.IsChildOf(w.V2Root)), 2, "廚房的兩扇窗（門面藏在巷底，兩側被鄰居擋住）");
            Assert.IsTrue(w.Marks.ContainsKey("V2Alley"), "16 號和 18 號之間有巷子");
            Assert.GreaterOrEqual(w.Inspectables.Count(i => i.transform.IsChildOf(w.V2Root) && i.transform.position.z < LanternGraybox.V2Origin.z - 5f), 3, "街上有可以互動的東西（對講機、公告欄、信箱）");
        }

        // ---------------- v2 一樓密室：第一批（店面＋側院紅線） ----------------

        /// <summary>站到目標前方 dist 公尺（朝 toward 那一側），對準它。</summary>
        private static void StandAt(LanternPrototypeQA qa, Vector3 target, Vector3 toward, float dist)
        {
            var d = toward - target; d.y = 0f;
            var pos = target + d.normalized * dist;
            pos.y = LanternGraybox.V2Origin.y + 0.05f;
            qa.Player.Teleport(pos, 0f, 0f);
            qa.Step(Dt, ExploreInput.None);
            Aim(qa, target);
            Act(qa, 1, LanternActionInput.None);
        }

        [Test]
        public void V2Shop_Batch1_PowerTagThreadSewing_RepairsTheShop_AndPlaysTheWallEcho()
        {
            var qa = NewQa();
            var w = qa.World;
            var v2 = qa.V2;
            Assert.IsNotNull(v2, "謎題控制器存在");
            Assert.IsNotNull(w.V2SewingMachine, "縫紉機模型存在");
            var center = w.V2ShopRoom.center;
            qa.Rig.SetOn(true);

            // 沒電、沒線時縫紉機不能用。
            var sew = w.V2SewingMachine.GetComponent<LanternUsable>();
            Assert.IsNotNull(sew);
            sew.Use();
            Assert.IsFalse(v2.Tap.Active, "沒電不能穿針");

            // S1 電箱：按住 E 推回拉桿。
            var lever = w.V2PowerLever;
            StandAt(qa, lever.GetComponent<Collider>().bounds.center, center, 0.7f);
            StringAssert.Contains("按住 E", qa.PromptText);
            Act(qa, 70, new LanternActionInput { Interact = true });
            Assert.IsTrue(lever.Done, "拉桿推回去了");
            Act(qa, 1, LanternActionInput.None);
            Assert.IsTrue(v2.Powered && w.V2Powered, "有電了");
            for (int i = LanternReveal.TempSlots; i < LanternReveal.TempSlots + 4; i++)
                Assert.LessOrEqual(LanternReveal.Extra(i).w, 0f, "電箱上方不留穩定區光圈");

            // S2 吊牌：聚光照著它。
            StandAt(qa, w.V2CoatTag.position, center, 1.4f);
            qa.FocusHeld = true;
            Act(qa, 75, LanternActionInput.None);
            qa.FocusHeld = false;
            Assert.IsTrue(v2.TagRead, "聚光看清吊牌（" + v2.TagFocus.ToString("F2") + "）");
            Assert.IsTrue(v2.Notebook.Any(l => l.Contains("只有這件沒有")), "筆記記下一行");

            // 有電沒線。
            sew.Use();
            Assert.IsFalse(v2.Tap.Active, "沒線不能穿針");

            // Y1 側院的紅線。
            var spool = w.V2ThreadSpool;
            StandAt(qa, spool.position, w.Marks["V2Yard"].position, 0.9f);
            StringAssert.Contains("紅線", qa.PromptText);
            Act(qa, 1, new LanternActionInput { Interact = true });
            Act(qa, 1, LanternActionInput.None);
            Assert.IsTrue(v2.HasThread, "拿到紅線");
            Assert.IsFalse(spool.gameObject.activeSelf, "紅線從籃子裡拿走了");

            // S4 穿針（點按判定）：亂按不會過；在亮的範圍裡按才算。
            StandAt(qa, w.V2SewingMachine.GetComponent<Collider>().bounds.center, center, 1.0f);
            StringAssert.Contains("穿針", qa.PromptText);
            Act(qa, 1, new LanternActionInput { Interact = true });
            Assert.IsTrue(v2.Tap.Active, "開始穿針");
            Assert.AreEqual(0, v2.Tap.Hits, "開始那一下不算");
            Act(qa, 1, LanternActionInput.None);
            int frames = 0;
            bool pressed = false;
            while (v2.Tap.Active && frames++ < 600)
            {
                bool press = !pressed && v2.Tap.InZone;
                qa.Step(Dt, ExploreInput.None, new LanternActionInput { Interact = press });
                pressed = press;
            }
            Assert.IsTrue(v2.Sewn, "穿好針（" + (frames * Dt).ToString("F1") + " 秒，失誤 " + v2.Tap.Misses + "）");
            Assert.Less(frames * Dt, 15f);

            // 修復時刻：真實擴散、安全據點、暖燈常亮、牆上的影子殘影＋逐字字幕。
            Assert.IsTrue(v2.ShopRepaired, "吊牌＋穿針 → 店面修好");
            Assert.IsTrue(w.InSafeRoom(center), "店面變成安全據點");
            Assert.Greater(w.V2SafeLamps.Count, 0, "店裡的燈常亮");
            var echo = v2.ShopEcho;
            Assert.IsNotNull(echo);
            Assert.IsTrue(echo.Armed && !echo.Playing, "牆先亮起來，等玩家看過去");
            Assert.Greater(echo.WallLight.intensity, 0.5f, "牆上有一片暖光");
            Aim(qa, echo.transform.position);
            Act(qa, 1, LanternActionInput.None);
            Assert.IsTrue(echo.Playing, "看過去，影子開始演出");
            Act(qa, 30, LanternActionInput.None);
            Assert.GreaterOrEqual(echo.LinesShown, 1);
            Assert.That(echo.VisibleText.Length, Is.InRange(1, echo.CurrentLine.Length), "字一個一個出現");
            StringAssert.StartsWith("媽媽：", qa.SubtitleText, "台詞顯示在畫面下方的字幕，前面有說話的人");
            Assert.IsFalse(echo.GetComponentInChildren<TextMesh>().GetComponent<MeshRenderer>().enabled, "牆上不打字");
            Act(qa, 25 * 30, LanternActionInput.None);
            Assert.IsTrue(echo.Finished, "演完會結束");
            Assert.AreEqual(6, echo.LinesShown);
            echo.Replay.Use();
            Assert.IsTrue(echo.Playing, "可以再看一次");
            Assert.AreEqual(2, echo.TimesPlayed);
        }

        [Test]
        public void V2Shop_NoShrine_NoDressForm_CoatHangsOnTheWall()
        {
            var qa = NewQa();
            var names = qa.World.V2Root.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToList();
            Assert.IsFalse(names.Any(n => n.Contains("Shrine")), "店裡沒有神桌");
            Assert.IsFalse(names.Any(n => n.Contains("Mannequin") || n == "v2_dress_form"), "沒有人台");
            Assert.IsFalse(names.Any(n => n.Contains("FittingCurtain")), "沒有那片紅色的簾子");
            Assert.IsTrue(names.Contains("V2HangingCoat"), "外套掛在牆上");
            Assert.IsTrue(names.Contains("V2ShopWindowGlass"), "店面有窗");
        }

        [Test]
        public void Interaction_OnlyPromptsWhenYouWalkClose()
        {
            var qa = NewQa();
            var w = qa.World;
            var sew = w.V2SewingMachine.GetComponent<Collider>().bounds.center;
            var center = w.V2ShopRoom.center;
            StandAt(qa, sew, center, 3.6f);
            Assert.AreEqual("", qa.PromptText, "站在 3 m 外對準，不會出現 E");
            StandAt(qa, sew, center, 1.2f);
            StringAssert.Contains("E", qa.PromptText, "走近才出現");
        }

        [Test]
        public void TapJudge_MashingOutsideTheZone_LosesProgress()
        {
            var t = new LanternTapJudge();
            t.Begin();
            int hits = 0;
            for (int i = 0; i < 300 && t.Active; i++)
            {
                bool farFromZone = Mathf.Abs(t.Pointer - t.ZoneCenter) > t.ZoneWidth; // 留一格指針移動的餘量
                t.Step(Dt, farFromZone && i % 2 == 0);
                hits = Mathf.Max(hits, t.Hits);
            }
            Assert.IsFalse(t.Succeeded, "只在範圍外按，永遠不會過");
            Assert.Greater(t.Misses, 10);
            Assert.AreEqual(0, hits);
        }

        // ---------------- 能力原型：照、定、牽 ----------------

        private static LanternMemory Mem(LanternPrototypeQA qa, string name) => qa.World.Memories.First(m => m != null && m.name == name);

        /// <summary>站在目標前 dist 公尺（從 from 那一側）、提燈開著、對準它。</summary>
        private static void FaceIt(LanternPrototypeQA qa, Vector3 target, Vector3 from, float dist)
        {
            var d = from - target; d.y = 0f;
            var pos = target + d.normalized * dist;
            pos.y = LanternGraybox.SandboxOrigin.y + 0.05f;
            qa.Player.Teleport(pos, 0f, 0f);
            qa.Step(Dt, ExploreInput.None);
            Aim(qa, target);
            qa.Step(Dt, ExploreInput.None); // 鏡頭跟上新的朝向
        }

        [Test]
        public void Sandbox_MemoryIsSolidOnlyInLight_HoldKeepsIt_ThenItFades()
        {
            var qa = NewQa();
            qa.GoToSandbox();
            var plank = Mem(qa, "SBPlank0");
            var o = LanternGraybox.SandboxOrigin;
            qa.Rig.SetOn(false);
            Act(qa, 20, LanternActionInput.None);
            Assert.IsFalse(plank.Present, "提燈關著：記憶中的木板不在");
            qa.Rig.SetOn(true);
            FaceIt(qa, plank.transform.position, o + new Vector3(7f, 0f, 3.3f), 1.4f);
            Act(qa, 5, LanternActionInput.None);
            Assert.IsTrue(plank.Present, "照到：木板是實的");
            Assert.AreEqual("", qa.Hold.Use(qa.Cam, qa.Actions), "定住它");
            Assert.IsTrue(plank.Held);
            Assert.AreEqual(LanternHold.MaxCharges - 1, Mathf.FloorToInt(qa.Hold.Charges));
            qa.Rig.SetOn(false);
            Act(qa, 60, LanternActionInput.None);
            Assert.IsTrue(plank.Present, "光拿開了，定住的木板還在");
            Act(qa, Mathf.CeilToInt(LanternHold.HoldSeconds / Dt), LanternActionInput.None);
            Assert.IsFalse(plank.Held, "時間到解開");
            Assert.IsFalse(plank.Present, "沒有光就消失");
        }

        [Test]
        public void Sandbox_AtMostTwoHolds_BiasedMemoryCannotBeHeld()
        {
            var qa = NewQa();
            qa.GoToSandbox();
            var a = Mem(qa, "SBPlank0"); var b = Mem(qa, "SBPlank1"); var c = Mem(qa, "SBPlank3");
            Assert.IsTrue(a.TryHold(25f) && b.TryHold(25f));
            Assert.IsFalse(Mem(qa, "SBPlank2").TryHold(25f), "記憶有偏差的木板定不住");
            // 用「定」系統：第三個會讓最早的解開。
            var o = LanternGraybox.SandboxOrigin;
            foreach (var m in new[] { a, b, c })
            {
                FaceIt(qa, m.transform.position, o + new Vector3(7f, 0f, m.transform.position.z - o.z), 1.3f);
                Act(qa, 3, LanternActionInput.None);
                qa.Hold.Use(qa.Cam, qa.Actions);
            }
            Assert.AreEqual(LanternHold.MaxActive, qa.Hold.ActiveCount, "同時最多定住 2 個");
        }

        [Test]
        public void Sandbox_HeldCrateCannotBeGrabbed_PlateOpensTheDoor_ByCrateOrHeldSack()
        {
            var qa = NewQa();
            qa.GoToSandbox();
            var w = qa.World;
            var crate = w.SandboxCrateSmall;
            // 定住的箱子抓不動。
            FaceIt(qa, crate.transform.position + Vector3.up * 0.15f, crate.transform.position + new Vector3(0f, 0f, -2f), 1.2f);
            qa.Actions.Grab(crate);
            Assert.AreEqual("", qa.Hold.Use(qa.Cam, qa.Actions));
            Act(qa, 2, LanternActionInput.None);
            Assert.IsNull(qa.Actions.Held, "被定住的東西抓不動");
            Assert.IsTrue(crate.Body.isKinematic);
            qa.Hold.Clear();

            // 把箱子放到壓板上：門打開。
            Act(qa, 1, LanternActionInput.None);
            w.SandboxSack.Release();
            qa.Rig.SetOn(false);
            crate.Body.position = w.SandboxPlate + Vector3.up * 0.1f;
            crate.transform.position = crate.Body.position;
            Physics.SyncTransforms();
            Act(qa, 60, LanternActionInput.None);
            Assert.IsTrue(qa.Sandbox.DoorOpen, "壓板上有箱子：門開（" + qa.Sandbox.PlateBy + "）");
            StringAssert.Contains("小木箱", qa.Sandbox.PlateBy);
            crate.Body.position = crate.HomePosition;
            crate.transform.position = crate.HomePosition;
            Physics.SyncTransforms();
            Act(qa, 60, LanternActionInput.None);
            Assert.IsFalse(qa.Sandbox.DoorOpen, "拿走就關上");

            // 記憶中的米袋：定住後沒有光也壓著壓板。
            Assert.IsTrue(w.SandboxSack.TryHold(25f));
            Act(qa, 60, LanternActionInput.None);
            Assert.IsTrue(qa.Sandbox.DoorOpen, "定住的米袋壓著壓板：門開");
            StringAssert.Contains("定住", qa.Sandbox.PlateBy);
        }

        [Test]
        public void Sandbox_ReachingThePlatform_RecordsTheMethod()
        {
            var qa = NewQa();
            qa.GoToSandbox();
            var o = LanternGraybox.SandboxOrigin;
            // 站在第一階（真的）→ 定住記憶中的兩階 → 走上高台。
            Assert.IsTrue(Mem(qa, "SBStep2").TryHold(25f) && Mem(qa, "SBStep3").TryHold(25f));
            qa.Player.Teleport(o + new Vector3(5.2f, 0.05f, 4.4f), 0f, 0f);
            Act(qa, 3, LanternActionInput.None);
            for (int i = 0; i < 120 && !qa.Sandbox.Goals[0].Done; i++) qa.Step(Dt, new ExploreInput { Move = new Vector2(0f, 1f) });
            Assert.IsTrue(qa.Sandbox.Goals[0].Done, "走上高台（" + qa.Player.transform.position + "）");
            StringAssert.Contains("記憶中的樓梯", qa.Sandbox.Goals[0].Method);
        }

        // ---------------- 推門（拉鋸） ----------------

        private static LanternStruggle AimAtDiningDoor(LanternPrototypeQA qa)
        {
            var w = qa.World;
            var st = w.V2DiningStruggle;
            qa.GoTo("V2Hall");
            var c = st.GetComponent<Collider>().bounds.center;
            qa.Player.Teleport(c + new Vector3(1.3f, -c.y + 0.05f, 0f), 0f, 0f);
            qa.Step(Dt, ExploreInput.None);
            Aim(qa, c);
            Act(qa, 1, LanternActionInput.None);
            Assert.AreSame(st, qa.Actions.Aimed, "對準了餐廳的門");
            StringAssert.Contains("推", qa.PromptText);
            return st;
        }

        [Test]
        public void StruggleDoor_ForcingItOrMashing_DoesNotOpen()
        {
            var qa = NewQa();
            var st = AimAtDiningDoor(qa);
            Act(qa, 240, new LanternActionInput { Interact = true });
            Assert.IsFalse(st.Done, "一直硬推，推不開（" + st.Progress.ToString("F2") + "）");
            for (int i = 0; i < 60; i++) { Act(qa, 1, new LanternActionInput { Interact = true }); Act(qa, 1, LanternActionInput.None); }
            Assert.IsFalse(st.Done, "狂點也推不開");
            Assert.Greater(st.Slams, 0, "狂點會讓門猛地推回來");
        }

        [Test]
        public void StruggleDoor_PushingWhenItLoosens_OpensIt_ThenItIsANormalDoor()
        {
            var qa = NewQa();
            var st = AimAtDiningDoor(qa);
            int frames = 0;
            while (!st.Done && frames++ < 450) qa.Step(Dt, ExploreInput.None, new LanternActionInput { Interact = st.Loose });
            Assert.IsTrue(st.Done, "在門鬆開時推，推得開（" + (frames * Dt).ToString("F1") + " 秒）");
            Assert.Less(frames * Dt, 12f, "不會太久");
            Act(qa, 30, LanternActionInput.None);
            Assert.IsTrue(st.Door.IsOpen, "門開了");
            Assert.AreEqual(1, qa.Actions.StrugglesOpened);
            // 門推開後，門板中心會有一半轉進牆裡；對準看得到的那一部分（離眼睛最近的那一點）。
            var dc = st.Door.GetComponent<Collider>();
            var doorway = st.GetComponent<Collider>().bounds.center;
            qa.Player.Teleport(new Vector3(doorway.x + 0.35f, qa.Player.transform.position.y, doorway.z), 0f, 0f);
            qa.Step(Dt, ExploreInput.None);
            Aim(qa, dc.bounds.center);
            Act(qa, 1, LanternActionInput.None);
            Assert.AreSame(st.Door, qa.Actions.Aimed, "推開之後就是普通的門（E 開關）");
        }
    }
}