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
        public void V2Shop_SewingNeedsNoPower_PlaceLanternThenSit_TasksUnordered_FinishKeepsTheShopFaded()
        {
            var qa = NewQa();
            var w = qa.World;
            var v2 = qa.V2;
            qa.ViewOverride = LanternPrototypeQA.AdultEyeHeight;   // 成人（身體也是成人）
            var center = w.V2ShopRoom.center;
            qa.Rig.SetOn(true);
            Assert.IsFalse(LanternReveal.ActiveRooms > 0, "一開始沒有恢復真實的房間");

            // 沒線：只能檢查。
            var sew = w.V2SewingMachine.GetComponent<LanternUsable>();
            sew.Use();
            Assert.IsFalse(v2.Tap.Active || v2.LanternPlaced, "沒線不能縫");

            // 紅線（不需要先開燈）。
            TakeThread(qa);
            Assert.IsFalse(v2.Powered, "還沒開燈");

            // 放燈 → 拉開凳子坐下 → 穿針：沒電也可以。
            StandAt(qa, w.V2SewingMachine.GetComponent<Collider>().bounds.center, center, 1.0f);
            StringAssert.Contains("把提燈放在縫紉機桌上", qa.PromptText);
            Act(qa, 1, new LanternActionInput { Interact = true });
            Act(qa, 1, LanternActionInput.None);
            Assert.IsTrue(v2.LanternPlaced, "提燈放在桌上");
            Assert.IsTrue(qa.Rig.PlacedAt.HasValue, "光從桌上的提燈發出");
            Assert.Less(Vector3.Distance(LanternReveal.Center, w.V2SewLanternSpot), 0.3f, "顯示真實的範圍以桌上的提燈為中心");
            Assert.IsTrue(w.V2PlacedLantern.gameObject.activeInHierarchy, "桌上看得到提燈");
            StringAssert.Contains("拉開凳子坐下", qa.PromptText, "成人：拉開凳子坐下");
            Act(qa, 1, new LanternActionInput { Interact = true });
            Assert.IsTrue(v2.Seated, "按下去：開始坐下");
            Act(qa, 2, LanternActionInput.None);
            Assert.IsFalse(v2.Tap.Active, "坐下的動作播完之前，不會開始判定");
            Assert.Less(qa.SeatAnimProgress, 1f, "正在坐下");
            for (int k = 0; k < 40 && !v2.Tap.Active; k++) qa.Step(Dt, ExploreInput.None);
            Assert.IsTrue(v2.Tap.Active, "坐好了，開始穿針");
            Assert.AreEqual(0.17f, v2.Tap.ZoneWidth, 1e-4f, "成人、沒電：判定寬度");
            Assert.AreEqual(3, v2.Tap.Need);
            for (int k = 0; k < 40; k++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(qa.SeatedEyeHeight, qa.Player.EyeHeightValue, 0.02f, "坐下：眼睛比站著低（" + qa.Player.EyeHeightValue.ToString("0.00") + "）");
            Assert.Less(qa.Player.EyeHeightValue, LanternPrototypeQA.AdultEyeHeight - 0.15f);
            int frames = 0; bool pressed = false;
            while (v2.Tap.Active && frames++ < 900)
            {
                bool press = !pressed && v2.Tap.InZone;
                qa.Step(Dt, ExploreInput.None, new LanternActionInput { Interact = press });
                pressed = press;
            }
            Assert.IsTrue(v2.Sewn, "沒電也縫好了");
            Act(qa, 2, LanternActionInput.None);
            Assert.IsFalse(v2.Seated, "縫好就站起來");
            for (int k = 0; k < 40 && qa.SeatAnimProgress < 1f; k++) qa.Step(Dt, ExploreInput.None);
            Assert.IsFalse(qa.Rig.PlacedAt.HasValue || v2.LanternPlaced, "提燈拿回手上");
            Assert.IsFalse(v2.ShopRepaired, "還沒開燈、還沒看吊牌：店裡的事還沒做完");

            // 吊牌（聚光）。
            StandAt(qa, w.V2CoatTag.position, center, 1.4f);
            qa.FocusHeld = true;
            Act(qa, 75, LanternActionInput.None);
            qa.FocusHeld = false;
            Assert.IsTrue(v2.TagRead, "聚光看清吊牌");
            Assert.IsFalse(v2.ShopRepaired, "還差開燈");

            // 最後才開燈（順序不限）：成人站在地上就搆得到。
            var shopLamp = w.V2ShopLamps[0];
            PowerS1(qa);
            Act(qa, 2, LanternActionInput.None);
            Assert.IsTrue(w.V2SewingLamp.enabled, "縫紉機的小燈亮了");
            Assert.Greater(shopLamp.color.b, shopLamp.color.r, "有電了，燈仍是冷白光（褪色態）");
            Assert.IsTrue(v2.ShopRepaired, "三件事都做完（順序不限）");
            Assert.AreEqual(0, LanternReveal.ActiveRooms, "完成後店裡不會永久恢復真實");
            Assert.AreEqual(0, w.V2SafeLamps.Count, "燈不會變成常亮的暖燈");
            Assert.IsTrue(v2.ShopTasks.All(t => t.done), "任務清單全部打勾");
            Assert.IsTrue(w.InSafeRoom(center), "店面仍是安全據點");

            // 牆上的影子殘影＋逐字字幕。
            var echo = v2.ShopEcho;
            Assert.IsNotNull(echo);
            Assert.IsTrue(echo.Armed && !echo.Playing, "牆先亮起來，等玩家看過去");
            Aim(qa, echo.transform.position);
            Act(qa, 1, LanternActionInput.None);
            Assert.IsTrue(echo.Playing, "看過去，影子開始演出");
            Act(qa, 30, LanternActionInput.None);
            StringAssert.StartsWith("媽媽：", qa.SubtitleText, "台詞顯示在字幕");
            Act(qa, 25 * 30, LanternActionInput.None);
            Assert.IsTrue(echo.Finished, "演完會結束");
            echo.Replay.Use();
            Assert.IsTrue(echo.Playing, "可以再看一次");
        }

        /// <summary>用牽引把紙箱從紙箱堆拿下來、移到電箱前放下（幼年）。</summary>
        private static void TetherBoxToBreaker(LanternPrototypeQA qa)
        {
            var w = qa.World;
            var box = w.V2MovableBox;
            qa.Player.Teleport(PV(1.35f, 3.3f), 0f, 0f);
            Settle(qa);
            Aim(qa, box.Box.bounds.center);
            Act(qa, 1, LanternActionInput.None);
            StringAssert.Contains("抓起紙箱", qa.PromptText, "對著紙箱：E 抓起（牽引）");
            Act(qa, 1, new LanternActionInput { Interact = true });
            Act(qa, 1, LanternActionInput.None);
            Assert.AreSame(box.Tether, qa.Actions.Held, "牽引抓起紙箱");
            qa.Player.Teleport(qa.Player.transform.position, 0f, 0f);
            for (int k = 0; k < 20; k++) qa.Step(Dt, ExploreInput.None);
            for (int k = 0; k < 150 && (qa.Player.transform.position.x - LanternGraybox.V2Origin.x) / LanternGraybox.V2Scale < 1.85f; k++)
                qa.Step(Dt, new ExploreInput { Move = new Vector2(1f, 0f) });   // 面向電箱、往右橫移
            for (int k = 0; k < 20; k++) qa.Step(Dt, ExploreInput.None);
            Assert.AreSame(box.Tether, qa.Actions.Held, "一路都牽著");
            var held = box.Box.bounds.center - qa.Cam.transform.position; held.y = 0f;
            Assert.Less(held.magnitude, 1.5f, "牽引時紙箱在身前不遠（" + held.magnitude.ToString("0.00") + " m）");
            Act(qa, 1, new LanternActionInput { Interact = true });
            Act(qa, 1, LanternActionInput.None);
            Assert.IsNull(qa.Actions.Held, "E 放下");
            for (int k = 0; k < 60; k++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(LanternGraybox.V2Origin.y, box.Box.bounds.min.y, 0.04f, "紙箱落在地上（放在哪裡都可以）");
        }

        [Test]
        public void V2Shop_Child_CannotReachTheBreaker_TethersTheBoxOver_ClimbsUp_AndTapsThePowerOn()
        {
            var qa = NewQa();
            var w = qa.World;
            var v2 = qa.V2;
            var lever = w.V2PowerLever;
            Assert.IsNotNull(w.V2MovableBox, "紙箱堆最上面那個箱子可以搬");
            Assert.IsNotNull(w.V2StoolMovable, "凳子可以搬");
            Assert.IsTrue(w.Tetherables.Contains(w.V2MovableBox.Tether) && w.Tetherables.Contains(w.V2StoolMovable.Tether), "和其他箱子一樣用牽引");
            qa.Rig.SetOn(true);
            qa.Player.Teleport(PV(1.85f, 4.2f), 0f, 0f);
            Settle(qa);
            Aim(qa, lever.GetComponent<Collider>().bounds.center);
            Act(qa, 1, LanternActionInput.None);
            Assert.AreEqual(LanternActions.TooHighPrompt, qa.PromptText, "幼年：太高了，搆不到");
            Act(qa, 70, new LanternActionInput { Interact = true });
            Assert.IsFalse(lever.Done, "幼年在地上拉不到");
            TetherBoxToBreaker(qa);
            int f = 0;
            while (qa.Carry.Climbs == 0 && f++ < 150) qa.Step(Dt, new ExploreInput { Move = new Vector2(0f, 1f) });
            Assert.AreEqual(1, qa.Carry.Climbs, "頂著紙箱往前走：爬上去");
            Settle(qa);
            Assert.Greater(qa.Player.transform.position.y, LanternGraybox.V2Origin.y + 0.25f, "站在紙箱上");
            Aim(qa, lever.GetComponent<Collider>().bounds.center);
            Act(qa, 1, LanternActionInput.None);
            StringAssert.Contains("E 拉下電箱的拉桿", qa.PromptText, "墊高之後搆得到；點一下就好");
            Act(qa, 1, new LanternActionInput { Interact = true });
            Act(qa, 50, LanternActionInput.None);
            Assert.IsTrue(lever.Done && v2.Powered, "點一下 E，拉桿自己拉下去，有電了");
        }
        [Test]
        public void V2Shop_Child_ClimbsOntoTheStool_HardestTap_StoolMustBeAtTheMachine()
        {
            var qa = NewQa();
            var w = qa.World;
            var v2 = qa.V2;
            TakeThread(qa);
            var stool = w.V2StoolMovable;
            var home = stool.transform.position;
            stool.transform.position = home + new Vector3(1.4f, 0f, 0.6f);   // 凳子被搬走了
            Physics.SyncTransforms();
            StandAt(qa, w.V2SewingMachine.GetComponent<Collider>().bounds.center, w.V2ShopRoom.center, 1.0f);
            Settle(qa);
            Assert.AreEqual(0, v2.SewAge, "幼年");
            Act(qa, 1, new LanternActionInput { Interact = true });   // 放燈
            Act(qa, 1, LanternActionInput.None);
            Assert.IsTrue(v2.LanternPlaced);
            Aim(qa, w.V2SewingMachine.GetComponent<Collider>().bounds.center);
            Act(qa, 1, LanternActionInput.None);
            StringAssert.Contains("凳子不在", qa.PromptText, "凳子被搬走：要先搬回來");
            stool.transform.position = home;
            Physics.SyncTransforms();
            Act(qa, 1, LanternActionInput.None);
            StringAssert.Contains("爬上凳子", qa.PromptText, "幼年：爬上凳子");
            Act(qa, 1, new LanternActionInput { Interact = true });
            Assert.IsTrue(v2.Seated && !v2.Tap.Active, "先爬上凳子，還沒開始判定");
            int climbFrames = 0;
            while (!v2.Tap.Active && climbFrames++ < 60) qa.Step(Dt, ExploreInput.None);
            Assert.IsTrue(v2.Tap.Active, "爬好了才開始");
            Assert.Greater(climbFrames * Dt, 0.9f, "幼年爬上去比較慢（" + (climbFrames * Dt).ToString("0.0") + " 秒）");
            Assert.AreEqual(0.10f, v2.Tap.ZoneWidth, 1e-4f, "幼年、沒電：判定最窄");
            Assert.AreEqual(4, v2.Tap.Need, "幼年要多穿一次");
            for (int k = 0; k < 40; k++) qa.Step(Dt, ExploreInput.None);
            Assert.Greater(qa.Player.EyeHeightValue, qa.ChildEyeHeight + 0.1f, "跪在凳子上，眼睛比站著高");
            // 往旁邊走＝站起來（放棄這次），提燈拿回來。
            qa.Step(Dt, new ExploreInput { Move = new Vector2(1f, 0f) });
            Act(qa, 1, LanternActionInput.None);
            Assert.IsFalse(v2.Seated || v2.Tap.Active, "走開＝站起來");
            Assert.IsFalse(v2.LanternPlaced, "提燈拿回手上");
            Assert.IsTrue(stool.Box.enabled, "凳子恢復碰撞");
        }

        [Test]
        public void V2House_TheGunStaysAway_TetherBeamComesFromTheLantern()
        {
            var qa = NewQa();
            qa.GoTo("V2Shop"); Settle(qa);
            Assert.IsFalse(qa.Rig.DeviceAllowed, "一樓：右手的穩定器收起來");
            qa.Rig.PokeDevice(2f);
            for (int k = 0; k < 20; k++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(0f, qa.Rig.DeviceShown, 1e-4f, "互動時也不會拿出來");
            Assert.Less(Vector3.Distance(qa.Rig.BeamOrigin, qa.Rig.Light.transform.position), 1e-3f, "牽引的光束從提燈發出");
            qa.GoTo("V2Street"); Settle(qa);
            Assert.IsFalse(qa.Rig.DeviceAllowed, "門外的街道也不用");
        }

        [Test]
        public void ShopMovables_CanBeDroppedAnywhere_AndPickedUpAgain()
        {
            var qa = NewQa();
            var w = qa.World;
            foreach (var m in new[] { w.V2StoolMovable, w.V2MovableBox })
            {
                qa.Player.Teleport(PV(3.2f, 1.6f), 0f, 0f);
                Settle(qa);
                m.Tether.Body.position = PV(3.2f, 2.6f) + Vector3.up * 0.3f;
                m.transform.position = m.Tether.Body.position;
                Physics.SyncTransforms();
                for (int k = 0; k < 40; k++) qa.Step(Dt, ExploreInput.None);
                Aim(qa, m.Box.bounds.center);
                Act(qa, 1, new LanternActionInput { Interact = true });
                Act(qa, 1, LanternActionInput.None);
                Assert.AreSame(m.Tether, qa.Actions.Held, "抓起" + m.Label);
                for (int k = 0; k < 40; k++) qa.Step(Dt, new ExploreInput { Move = new Vector2(-1f, 0f) });
                Act(qa, 1, new LanternActionInput { Interact = true });
                Act(qa, 1, LanternActionInput.None);
                Assert.IsNull(qa.Actions.Held, "隨時可以放下" + m.Label);
                for (int k = 0; k < 60; k++) qa.Step(Dt, ExploreInput.None);
                Assert.AreEqual(LanternGraybox.V2Origin.y, m.Box.bounds.min.y, 0.05f, m.Label + "放在地上");
                Assert.Less(Vector3.Angle(m.transform.up, Vector3.up), 2f, m.Label + "沒有翻倒");
                Aim(qa, m.Box.bounds.center);
                Act(qa, 1, new LanternActionInput { Interact = true });
                Act(qa, 1, LanternActionInput.None);
                Assert.AreSame(m.Tether, qa.Actions.Held, "放下之後可以再抓起來");
                Act(qa, 1, new LanternActionInput { Interact = true });
                Act(qa, 1, LanternActionInput.None);
            }
        }

        [Test]
        public void LanternOnTheTable_YouCarryByHand_NoBeam_AndMustWalkClose()
        {
            var qa = NewQa();
            var w = qa.World;
            var v2 = qa.V2;
            TakeThread(qa);
            StandAt(qa, w.V2SewingMachine.GetComponent<Collider>().bounds.center, w.V2ShopRoom.center, 1.0f);
            Act(qa, 1, new LanternActionInput { Interact = true });
            Act(qa, 1, LanternActionInput.None);
            Assert.IsTrue(v2.LanternPlaced && qa.Actions.HandsOnly, "提燈放在桌上：只能用手");
            var box = w.V2MovableBox;
            // 站在 3 m 外：提燈不在手上，抓不到。
            qa.Player.Teleport(PV(1.35f, 2.4f), 0f, 0f); Settle(qa);
            Aim(qa, box.Box.bounds.center); Act(qa, 1, LanternActionInput.None);
            StringAssert.DoesNotContain("拿起紙箱", qa.PromptText, "太遠：要走過去用手拿");
            // 走到旁邊：用手拿起，沒有光束。
            qa.Player.Teleport(PV(1.35f, 3.7f), 0f, 0f); Settle(qa);
            Aim(qa, box.Box.bounds.center); Act(qa, 1, LanternActionInput.None);
            StringAssert.Contains("用手拿起紙箱", qa.PromptText);
            Act(qa, 1, new LanternActionInput { Interact = true });
            for (int k = 0; k < 20; k++) qa.Step(Dt, ExploreInput.None);
            Assert.AreSame(box.Tether, qa.Actions.Held, "用手抱著");
            Assert.IsFalse(qa.Actions.BeamVisible, "沒有從桌上的提燈拉出來的光束");
            var d = box.Box.bounds.center - qa.Cam.transform.position; d.y = 0f;
            Assert.Less(d.magnitude, 1.0f, "抱在身前");
            Act(qa, 1, new LanternActionInput { Interact = true });
            Act(qa, 1, LanternActionInput.None);
            Assert.IsNull(qa.Actions.Held, "放下");
        }

        [Test]
        public void Stool_CannotBeStoodOn_OnlySatOnToSew()
        {
            var qa = NewQa();
            var w = qa.World;
            var stool = w.V2StoolMovable;
            Assert.IsFalse(stool.Climbable, "凳子不能爬上去站");
            var seat = stool.transform.position;
            qa.Player.Teleport(new Vector3(seat.x, LanternGraybox.V2Origin.y + 0.05f, seat.z + 0.75f), 180f, 0f);
            Settle(qa);
            for (int k = 0; k < 60; k++) qa.Step(Dt, new ExploreInput { Move = new Vector2(0f, 1f) });
            Assert.AreEqual(0, qa.Carry.Climbs, "頂著凳子走也不會爬上去");
            Assert.Less(qa.Player.transform.position.y, LanternGraybox.V2Origin.y + 0.1f, "還在地上");
        }

        [Test]
        public void Lantern_PullsBackAndDims_WhenYourFaceIsNearAWall()
        {
            var qa = NewQa();
            qa.Rig.SetOn(true);
            qa.Player.Teleport(PV(3.0f, 4.75f), 0f, 0f);   // 臉貼著店後牆
            for (int k = 0; k < 30; k++) qa.Step(Dt, ExploreInput.None);
            Assert.Less(qa.Rig.NearWall, 0.7f, "前面就是牆");
            var eye = qa.Cam.transform.position;
            var toLamp = qa.Rig.Light.transform.position - eye;
            Assert.IsFalse(Physics.Raycast(eye, toLamp.normalized, toLamp.magnitude, ~(1 << LanternRig.ViewLayer), QueryTriggerInteraction.Ignore), "提燈的光沒有穿進牆裡");
            qa.Player.Teleport(PV(5.07f, 6.0f), 0f, 0f);   // 走廊，往前空曠
            for (int k = 0; k < 30; k++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(1f, qa.Rig.NearWall, 1e-3f, "前面空曠：正常亮度");
        }
        [Test]
        public void Body_ChangesWithMemoryAge_AndDoesNotGrowIntoFurniture()
        {
            var qa = NewQa();
            var p = qa.Player;
            qa.GoTo("V2Street"); Settle(qa);
            Assert.AreEqual(1.8f, p.Body.height, 0.01f, "街上：成人身體");
            Assert.AreEqual(0.35f, p.Body.radius, 0.01f);
            qa.GoTo("V2Hall"); Settle(qa);
            Assert.AreEqual(0.85f / 0.9f, p.Body.height, 0.01f, "幼年：身體真的變小（設計總綱 §11）");
            Assert.AreEqual(0.24f, p.Body.radius, 0.01f);
            Assert.Less(p.Body.stepOffset, 0.25f, "幼年跨不上紙箱（要爬）");
            float childReach = p.transform.position.y + p.EyeHeightValue + qa.ReachAboveEye - LanternGraybox.V2Origin.y;
            Assert.Less(childReach, qa.World.V2PowerLever.ReachY - LanternGraybox.V2Origin.y, "幼年站在地上手搆不到電箱拉桿（" + childReach.ToString("0.00") + " m）");
            // 頭上有東西：不會長大卡進去，移開之後才長。
            var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lid.transform.position = p.transform.position + Vector3.up * 1.15f;
            lid.transform.localScale = new Vector3(1.5f, 0.1f, 1.5f);
            Physics.SyncTransforms();
            qa.ViewOverride = qa.TeenEyeHeight;
            for (int k = 0; k < 60; k++) qa.Step(Dt, ExploreInput.None);
            Assert.Less(p.Body.height, 1.1f, "上面擋住：先不長大（" + p.Body.height.ToString("0.00") + "）");
            Object.DestroyImmediate(lid);
            Physics.SyncTransforms();
            for (int k = 0; k < 60; k++) qa.Step(Dt, ExploreInput.None);
            Assert.AreEqual(1.12f / 0.9f, p.Body.height, 0.01f, "移開之後長成青春期的身體");
            Assert.AreEqual(qa.TeenEyeHeight, p.EyeHeightValue, 0.01f);
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

        // ---------------- 記憶空間 A：老街市場 ----------------

        [Test]
        public void Market_YardDoorLeadsIn_RampIsMemory_PlateOpensShutter_KeepsakeEndsIt()
        {
            var qa = NewQa();
            var w = qa.World;
            Assert.IsNotNull(w.MarketRoot, "市場存在");
            Assert.IsNotNull(w.YardDoor, "側院有後門");
            w.YardDoor.Use();
            Act(qa, 2, LanternActionInput.None);
            Assert.IsTrue(w.InMarket(qa.Player.transform.position), "推開後門就到了老街");

            // 記憶中的遮雨棚：沒有光不在；定住後光拿開還在。
            var ramp = w.Memories.First(m => m != null && m.name == "MKAwningRamp");
            qa.Rig.SetOn(false);
            Act(qa, 20, LanternActionInput.None);
            Assert.IsFalse(ramp.Present, "沒有光：遮雨棚不在");
            Assert.IsTrue(ramp.TryHold(25f));
            Act(qa, 3, LanternActionInput.None);
            Assert.IsTrue(ramp.Present, "定住：遮雨棚在");

            // 走上遮雨棚斜坡，到騎樓。
            var o = LanternGraybox.MarketOrigin;
            qa.Player.Teleport(o + new Vector3(5.2f, 0.05f, 12.6f), 90f, 0f);
            Act(qa, 2, LanternActionInput.None);
            for (int i = 0; i < 150 && !qa.Market.Goals[1].Done; i++) qa.Step(Dt, new ExploreInput { Move = new Vector2(0f, 1f) });
            Assert.IsTrue(qa.Market.Goals[1].Done, "走上遮雨棚到騎樓（" + qa.Player.transform.position + "）");
            StringAssert.Contains("遮雨棚", qa.Market.Goals[1].Method);

            // 壓板上定住記憶中的米袋：鐵捲門捲上去。
            Assert.IsTrue(w.MarketSack.TryHold(25f));
            Act(qa, 75, LanternActionInput.None);
            Assert.IsTrue(qa.Market.ShutterOpen, "鐵捲門打開（" + qa.Market.PlateBy + "）");

            // 拿到捲尺：最後一個目標完成，信物到手。
            w.MarketKeepsake.Use();
            Assert.IsTrue(qa.Market.HasKeepsake && qa.Market.Goals[3].Done);
            Assert.IsFalse(w.MarketKeepsake.gameObject.activeSelf, "捲尺被拿走了");
            w.MarketExit.Use();
            Act(qa, 2, LanternActionInput.None);
            Assert.IsTrue(w.InV2House(qa.Player.transform.position), "從市場的門回到房子");
        }

        // ---------------- 家具配置 QA：動線淨寬 ----------------

        [Test]
        public void HouseV2_RoutesKeepClearWidth_MainAndSecondary()
        {
            var qa = NewQa();
            Physics.SyncTransforms();
            var res = LanternClearance.Measure(qa.World.V2Root, qa.World.V2ClearanceRoute());
            var report = LanternClearance.Report(res);
            Debug.Log(report);
            var bad = res.Where(r => !r.Ok).ToList();
            Assert.IsEmpty(bad, "動線淨寬不足：\n" + report);
        }

        [Test]
        public void HouseV2_DiningSample_PlayerCanWalkAroundTableAndToKitchen()
        {
            var qa = NewQa();
            var o = LanternGraybox.V2Origin; float S = LanternGraybox.V2Scale;
            Vector3 HP(float x, float z) => o + new Vector3(x * S, 0.05f, z * S);
            Assert.IsNotNull(qa.World.V2Root.Find("DN_Table"), "餐廳樣板的餐桌在場景裡");
            qa.Player.Teleport(HP(4.1f, 6.45f), 0f, 0f);
            qa.Step(Dt, ExploreInput.None);
            // 門口 → 起居區 → 橫樑開口 → 壁爐前走道 → 廚房門 → 壁龕櫃前 → 北側椅背後 → 碗櫃前 → 東側開口 → 電視前 → 回到門口。
            foreach (var (name, x, z) in new[] { ("起居區中間", 3.02f, 6.28f), ("橫樑開口西側", 1.33f, 7.51f), ("壁爐前走道", 1.10f, 8.59f), ("往廚房的門", 1.52f, 9.66f),
                ("壁龕櫃前", 0.87f, 9.47f), ("北側椅背後", 1.52f, 9.62f), ("北側椅背後（東）", 3.56f, 9.62f), ("碗櫃前", 3.71f, 8.12f),
                ("橫樑開口東側", 3.63f, 6.82f), ("電視前", 1.94f, 5.89f), ("門口", 4.1f, 6.45f) })
                Assert.IsTrue(WalkTo(qa, HP(x, z), 600), "走得到「" + name + "」（卡在 " + qa.Player.transform.position + "）");
        }

        // ---------------- 推門（拉鋸） ----------------

        private static LanternStruggle AimAtDiningDoor(LanternPrototypeQA qa)
        {
            var w = qa.World;
            var st = w.V2DiningStruggle;
            qa.GoTo("V2Hall");
            var c = st.GetComponent<Collider>().bounds.center;
            qa.Player.Teleport(c + new Vector3(0.75f, -c.y + 0.05f, 0f), 0f, 0f);   // 走廊中間（再往東是樓梯的牆）
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

        // ---------------- 正式預設：餐廳 B（空間層次）、廚房 B（改造）——使用者確認後同步，A／B 對照入口已移除 ----------------

        [Test]
        public void DiningLayout_FormalIsB_RoutesStayOpen_AndAStillRestores()
        {
            var qa = NewQa();
            var w = qa.World;
            Assert.IsTrue(w.DiningLayoutIsB, "正式遊戲預設是餐廳 B");
            Transform Find(string n) => w.V2Root.GetComponentsInChildren<Transform>(true).First(t => t.name == n);
            var books = Find("DN_Bookcase"); var portiere = Find("DN_LayoutB_Portiere");
            var focus = w.V2Root.GetComponentsInChildren<Light>(true).Where(l => l.name.StartsWith("DN_Focus_")).ToList();
            Assert.IsTrue(portiere.gameObject.activeSelf, "B：有門簾");
            Assert.IsTrue(focus.All(l => l.enabled), "B：光照引導打開");

            // B 的動線：門口繞一圈回到門口，每一點都走得到；淨寬全部合格。
            var o = LanternGraybox.V2Origin; float S = LanternGraybox.V2Scale;
            Vector3 HP(float x, float z) => o + new Vector3(x * S, 0.05f, z * S);
            var st = w.V2DiningStruggle;
            if (st != null && st.Door != null && !st.Door.IsOpen) st.Door.Toggle();
            qa.Player.Teleport(HP(4.1f, 6.45f), -90f, 0f);
            qa.Step(Dt, ExploreInput.None);
            foreach (var (name, x, z) in new[] { ("起居區中間", 3.02f, 6.28f), ("橫樑開口西側", 1.33f, 7.51f), ("壁爐前走道", 1.10f, 8.59f), ("往廚房的門", 1.52f, 9.66f),
                ("北側椅背後（東）", 3.56f, 9.62f), ("碗櫃前", 3.71f, 8.12f), ("橫樑開口東側", 3.63f, 6.82f), ("書櫃旁", 2.56f, 6.47f), ("電視前", 2.29f, 5.78f), ("書櫃旁（回程）", 2.56f, 6.47f), ("門口", 4.1f, 6.45f) })
                Assert.IsTrue(WalkTo(qa, HP(x, z), 600), "B：走得到「" + name + "」（卡在 " + qa.Player.transform.position + "）");
            Physics.SyncTransforms();
            var res = LanternClearance.Measure(w.V2Root, w.V2ClearanceRoute());
            Assert.IsEmpty(res.Where(r => !r.Ok).ToList(), "B：動線淨寬不足：\n" + LanternClearance.Report(res));

            // 程式裡仍保留 A（之後要對照時可以切回）
            var booksB = books.position;
            w.SetDiningLayout(false);
            Assert.Greater(Vector3.Distance(booksB, books.position), 0.5f, "切回 A：書櫃回原位");
            Assert.IsFalse(portiere.gameObject.activeSelf, "切回 A：門簾收起");
        }

        [Test]
        public void KitchenLayout_FormalIsB_KeepsPuzzlePieces_GroupsForDualState_AndRoutesStayOpen()
        {
            var qa = NewQa();
            var w = qa.World;
            Assert.IsTrue(w.KitchenLayoutIsB, "正式遊戲預設是廚房 B");
            Transform Find(string n) => w.V2Root.GetComponentsInChildren<Transform>(true).First(t => t.name == n);
            var fridge = Find("V2FridgeModel"); var tall = Find("V2TallCabinet"); var tin = Find("V2CookieTin"); var sink = Find("V2SinkUnit");
            var counterA = Find("V2Counter2"); var structB = Find("KT_B_Structure"); var lifeB = Find("KT_B_Life");
            Assert.IsFalse(counterA.gameObject.activeSelf, "B：灰盒流理台換掉");
            Assert.IsTrue(structB.gameObject.activeSelf && lifeB.gameObject.activeSelf, "B：斜天花板、爐灶凹槽、生活細節都在");
            var sinkB = sink.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
            var kRoom = w.V2KitchenRoom;
            Assert.Less(sinkB.min.x - kRoom.min.x, 0.05f, "B：水槽在西牆（切菜區旁），背貼牆");
            var prep = Find("KT_CounterN_Top").GetComponent<Renderer>().bounds;
            Assert.Less(Mathf.Abs(prep.min.z - sinkB.max.z), 0.1f, "B：水槽緊接著備料台面");
            Assert.IsFalse(Find("V2KitchenStool").gameObject.activeSelf, "B：窗邊的凳子拿掉");
            Assert.IsFalse(w.V2Root.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("KT_Airer") || t.name.StartsWith("KT_Table")), "B：沒有晾衣架、沒有窗邊小桌");
            var micro = Find("V2Microwave").GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
            var backTop = Find("KT_CounterBack_Top").GetComponent<Renderer>().bounds;
            Assert.AreEqual(backTop.max.y, micro.min.y, 0.02f, "B：微波爐放在檯面上（不穿模、不懸空）");
            var ceil = Find("KT_SlopedCeiling").GetComponent<Renderer>().bounds;
            Assert.AreEqual(2.85f, ceil.center.y, 0.06f, "天花板平均高度約 2.85 m");
            // 雙狀態預留：生活細節可以單獨關掉，K1／K2 永遠在。
            w.SetKitchenGroupVisible(LanternGraybox.KitchenGroup.Life, false);
            Assert.IsFalse(lifeB.gameObject.activeSelf, "生活細節那組可以關掉（之後的褪色態）");
            Assert.IsTrue(fridge.gameObject.activeSelf && tall.gameObject.activeSelf && tin.gameObject.activeSelf, "K1／K2 永遠在");
            w.SetKitchenGroupVisible(LanternGraybox.KitchenGroup.Life, true);

            var o = LanternGraybox.V2Origin; float S = LanternGraybox.V2Scale;
            Vector3 HP(float x, float z) => o + new Vector3(x * S, 0.05f, z * S);
            qa.Player.Teleport(HP(1.635f, 10.558f), 0f, 0f);
            qa.Step(Dt, ExploreInput.None);
            foreach (var (name, x, z) in new[] { ("爐灶前", 1.019f, 11.827f), ("流理台北段前", 1.096f, 13.327f), ("水槽前", 1.865f, 13.942f), ("高櫃前（K2）", 2.596f, 13.942f),
                ("後門前", 3.404f, 13.788f), ("廚房中央", 1.865f, 13.173f), ("廚房中段", 1.865f, 11.635f), ("冰箱前（K1）", 2.865f, 11.019f), ("廚房門內", 1.635f, 10.558f) })
                Assert.IsTrue(WalkTo(qa, HP(x, z), 600), "B：走得到「" + name + "」（卡在 " + qa.Player.transform.position + "）");
            Physics.SyncTransforms();
            var res = LanternClearance.Measure(w.V2Root, w.V2ClearanceRoute());
            Assert.IsEmpty(res.Where(r => !r.Ok).ToList(), "B：動線淨寬不足：\n" + LanternClearance.Report(res));
        }

        // ---------------- 正式移動手感（試玩後採用的 A：步行 1.5／快走 2.4）——對照入口已移除 ----------------

        [Test]
        public void Movement_FormalValues_StartQuickly_StopWithoutSliding_TurnWithoutSkid()
        {
            var qa = NewQa();
            var p = qa.Player;
            Assert.AreEqual(1.5f, p.WalkSpeedValue, 1e-4f, "正式步行 1.5 m/s");
            Assert.AreEqual(2.4f, p.RunSpeedValue, 1e-4f, "正式快走 2.4 m/s");
            Assert.AreEqual(1.5f / 0.45f, p.Acceleration, 1e-3f, "加速：約 0.45 秒到速");
            Assert.AreEqual(8f, p.Deceleration, 1e-3f, "減速另外設定");
            Assert.IsTrue(p.SmoothSpeedOnly, "轉頭不側滑");
            Assert.IsTrue(qa.Rig.GaitBob, "步伐式晃動");
            Assert.AreEqual(0.7f, qa.StepLength, 1e-4f, "腳步聲每 0.7 m");
            var o = LanternGraybox.V2Origin; float S = LanternGraybox.V2Scale;
            p.Teleport(o + new Vector3(5.07f * S, 0.05f, 5.4f * S), 0f, 0f);
            for (int k = 0; k < 3; k++) qa.Step(Dt, ExploreInput.None);
            float t = 0f, reach = -1f;
            for (int k = 0; k < 30; k++)
            {
                qa.Step(Dt, new ExploreInput { Move = new Vector2(0f, 1f) });
                t += Dt;
                if (reach < 0f && p.LastHorizontalSpeed >= 1.5f * 0.95f) reach = t;
            }
            Assert.AreEqual(1.5f, p.LastHorizontalSpeed, 0.06f, "穩定步行速度");
            Assert.IsTrue(reach > 0.2f && reach < 0.6f, "起步不是瞬間、也不拖泥帶水（" + reach + " 秒）");
            var a = p.transform.position;
            for (int k = 0; k < 30 && (k == 0 || p.LastHorizontalSpeed > 0.02f); k++) qa.Step(Dt, ExploreInput.None);
            var slide = p.transform.position - a; slide.y = 0f;
            Assert.Less(slide.magnitude, 0.3f, "放開方向鍵後很快停住（滑行 " + slide.magnitude.ToString("0.00") + " m）");
            float yaw0 = p.Yaw;
            qa.Step(Dt, new ExploreInput { Look = new Vector2(5f, 0f) });
            Assert.AreEqual(Mathf.Repeat(yaw0 + 5f * FirstPersonController.LookSensitivity * p.LookScale, 360f), p.Yaw, 0.01f, "轉向即時");
            // 走路時轉 90°：轉完之後就沿著鏡頭方向走（不側滑）。
            p.Teleport(o + new Vector3(0.125f + 4.6f, 0.05f, 6.56f + 1.2f), -90f, 0f);
            for (int k = 0; k < 20; k++) qa.Step(Dt, new ExploreInput { Move = new Vector2(0f, 1f) });
            for (int k = 0; k < 6; k++) qa.Step(Dt, new ExploreInput { Move = new Vector2(0f, 1f), Look = new Vector2(15f / (FirstPersonController.LookSensitivity * p.LookScale), 0f) });
            var b = p.transform.position;
            for (int k = 0; k < 10; k++) qa.Step(Dt, new ExploreInput { Move = new Vector2(0f, 1f) });
            var moved = p.transform.position - b; moved.y = 0f;
            var fwd = Quaternion.Euler(0f, p.Yaw, 0f) * Vector3.forward;
            Assert.Less(Mathf.Abs(Vector3.Dot(moved, Quaternion.Euler(0f, 90f, 0f) * fwd)), 0.03f, "轉頭後沒有側滑");
        }

        [Test]
        public void Movement_Formal_WalksThroughDoorways_AndUpTheStairs()
        {
            var qa = NewQa();
            var w = qa.World;
            var st = w.V2DiningStruggle;
            if (st != null && st.Door != null && !st.Door.IsOpen) st.Door.Toggle();
            foreach (var d in w.V2Doors) if (d != null && d.Label == "廚房的門" && !d.IsOpen) d.Toggle();
            var o = LanternGraybox.V2Origin; float S = LanternGraybox.V2Scale;
            Vector3 HP(float x, float z) => o + new Vector3(x * S, 0.05f, z * S);
            qa.Player.Teleport(HP(5.07f, 5.5f), 0f, 0f);
            qa.Step(Dt, ExploreInput.None);
            foreach (var (x, z) in new[] { (5.07f, 6.45f), (4.1f, 6.45f), (2.33f, 7.14f), (1.33f, 7.51f), (1.1f, 8.59f), (1.6f, 9.66f), (1.6f, 10.6f), (1.6f, 9.66f), (1.33f, 7.51f), (4.1f, 6.45f), (5.07f, 6.45f) })
                Assert.IsTrue(WalkTo(qa, HP(x, z), 600), "走得到 (" + x + ", " + z + ")（卡在 " + qa.Player.transform.position + "）");
            qa.Player.Teleport(HP(6.02f, 5.3f), 0f, 0f);
            qa.Step(Dt, ExploreInput.None);
            WalkTo(qa, HP(6.02f, 9.45f), 900);
            Assert.Greater(qa.Player.transform.position.y, 2.8f, "走得上樓梯（高度 " + qa.Player.transform.position.y.ToString("0.00") + "）");
        }

        // ---------------- 服飾店：A｜生活機能優先＝正式（2026-11-16 採用） ----------------

        [Test]
        public void ShopA_IsFormal_S1S2S4_StillWork_AndTheShopRepairs()
        {
            var qa = NewQa();
            var w = qa.World;
            var v2 = qa.V2;
            Assert.AreEqual(1, w.ShopLayout, "正式遊戲是 A");
            Assert.IsTrue(w.V2Root.GetComponentsInChildren<Transform>(true).First(t => t.name == "ShopA_CutTable").gameObject.activeInHierarchy, "A 的裁剪桌在");
            var center = w.V2ShopRoom.center;
            qa.Rig.SetOn(true);
            PowerS1(qa);   // 幼年：搬紙箱墊腳
            StandAt(qa, w.V2CoatTag.position, center, 1.4f);
            qa.FocusHeld = true;
            Act(qa, 75, LanternActionInput.None);
            qa.FocusHeld = false;
            Assert.IsTrue(v2.TagRead, "S2 聚光看清吊牌（" + v2.TagFocus.ToString("F2") + "）");
            TakeThread(qa);
            Sew(qa);
            Assert.IsTrue(v2.ShopRepaired, "店面的事都做完了");            var echo = v2.ShopEcho;
            Aim(qa, echo.transform.position);
            Act(qa, 1, LanternActionInput.None);
            Assert.IsTrue(echo.Playing, "影子殘影的牆沒有被家具擋住");
        }

        [Test]
        public void ShopA_RoutesWalkable_ClearanceOk_AndOriginalLayoutStillRestores()
        {
            var qa = NewQa();
            var w = qa.World;
            Transform Find(string n) => w.V2Root.GetComponentsInChildren<Transform>(true).First(t => t.name == n);
            var counter = Find("V2Counter");
            var counterA = counter.position;
            var o = LanternGraybox.V2Origin; float S = LanternGraybox.V2Scale;
            Vector3 HP(float x, float z) => o + new Vector3(x * S, 0.05f, z * S);
            var route = new[] { ("店門內側", 5.1f, 0.6f), ("櫃台前（接待）", 4.8f, 1.9f), ("櫃台後端", 4.8f, 3.6f), ("店主側往工作區", 3.3f, 3.75f), ("裁剪桌北側", 2.3f, 3.75f),
                ("外套前", 1.0f, 3.5f), ("左牆衣架旁", 1.15f, 2.3f), ("往窗邊", 2.1f, 1.6f), ("縫紉機旁（窗下）", 2.1f, 1.1f), ("回到外套前", 1.0f, 3.5f), ("電箱前", 1.85f, 4.25f), ("往住宅的門前", 5.07f, 4.3f) };
            qa.Player.Teleport(HP(route[0].Item2, route[0].Item3), 0f, 0f);
            qa.Step(Dt, ExploreInput.None);
            foreach (var (name, x, z) in route)
                Assert.IsTrue(WalkTo(qa, HP(x, z), 700), "A：走得到「" + name + "」（卡在 " + qa.Player.transform.position + "）");
            Physics.SyncTransforms();
            var res = LanternClearance.Measure(w.V2Root, w.V2ClearanceRoute());
            Assert.IsEmpty(res.Where(r => !r.Ok).ToList(), "動線淨寬不足：\n" + LanternClearance.Report(res));

            w.SetShopLayout(0);
            Assert.Greater(Vector3.Distance(counterA, counter.position), 0.5f, "原本的擺法：櫃台在別的位置");
            Assert.IsTrue(w.V2Root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("ShopA_")).All(t => !t.gameObject.activeInHierarchy), "A 專屬的物件收起來");
            w.SetShopLayout(1);
            Assert.AreEqual(0f, Vector3.Distance(counterA, counter.position), 1e-4f, "再套回 A：完全一樣");
        }

        // ---------------- 記憶年齡與廚房↔側院通路（設計總綱 §10） ----------------

        private static Vector3 PV(float x, float z) => LanternGraybox.V2Origin + new Vector3(x * LanternGraybox.V2Scale, 0.05f, z * LanternGraybox.V2Scale);

        private static void OpenAllV2Doors(LanternPrototypeQA qa)
        {
            var w = qa.World;
            var st = w.V2DiningStruggle;
            if (st != null && st.Door != null && !st.Door.IsOpen) st.Door.Toggle();
            foreach (var d in w.V2Doors) if (d != null && !d.IsOpen) d.Toggle();
            Physics.SyncTransforms();
        }

        private static void Walk(LanternPrototypeQA qa, string what, params (float x, float z)[] pts)
        {
            foreach (var (x, z) in pts)
                Assert.IsTrue(WalkTo(qa, PV(x, z), 700), what + "：走得到 (" + x + ", " + z + ")（卡在 " + qa.Player.transform.position + "）");
        }

        /// <summary>走到目標，回傳過程中每格眼高的最大變化。</summary>
        private static float WalkWatchingEye(LanternPrototypeQA qa, float x, float z, string what)
        {
            var p = qa.Player;
            var target = PV(x, z);
            float last = p.EyeHeightValue, maxStep = 0f;
            for (int i = 0; i < 700; i++)
            {
                var d = target - p.transform.position; d.y = 0f;
                if (d.magnitude < 0.35f) return maxStep;
                float want = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                float look = Mathf.Clamp(Mathf.DeltaAngle(p.Yaw, want), -20f, 20f) / (FirstPersonController.LookSensitivity * p.LookScale);
                qa.Step(Dt, new ExploreInput { Move = new Vector2(0f, 1f), Look = new Vector2(look, 0f) });
                maxStep = Mathf.Max(maxStep, Mathf.Abs(p.EyeHeightValue - last));
                last = p.EyeHeightValue;
            }
            Assert.Fail(what + "：走不到（卡在 " + p.transform.position + "）");
            return maxStep;
        }

        private static void Settle(LanternPrototypeQA qa) { for (int k = 0; k < 45; k++) qa.Step(Dt, ExploreInput.None); }

        private static int ActivePassageTrims(LanternGraybox w)
        {
            var a = PV(LanternGraybox.V2ExtWidth, LanternGraybox.KitchenYardZ0); var b = PV(LanternGraybox.V2ExtWidth, LanternGraybox.KitchenYardZ1);
            return w.V2Root.GetComponentsInChildren<Transform>(false).Count(t => t.name == "DoorTrim" && Mathf.Abs(t.position.x - a.x) < 0.3f && t.position.z > a.z - 0.3f && t.position.z < b.z + 0.3f);
        }

        /// <summary>開電箱：搆得到就直接拉（點一下 E）；幼年搆不到就用牽引把紙箱搬過去、爬上去再拉。</summary>
        private static void PowerS1(LanternPrototypeQA qa)
        {
            var w = qa.World;
            var lever = w.V2PowerLever;
            qa.Player.Teleport(PV(1.85f, 4.2f), 0f, 0f);
            Settle(qa);
            if (!qa.Actions.CanReach(lever))
            {
                TetherBoxToBreaker(qa);
                int f = 0;
                while (qa.Carry.Climbs == 0 && f++ < 150) qa.Step(Dt, new ExploreInput { Move = new Vector2(0f, 1f) });
                Assert.Greater(qa.Carry.Climbs, 0, "爬上紙箱");
                Settle(qa);
            }
            Aim(qa, lever.GetComponent<Collider>().bounds.center);
            Act(qa, 1, LanternActionInput.None);
            Act(qa, 1, new LanternActionInput { Interact = true });
            Act(qa, 50, LanternActionInput.None);
            Assert.IsTrue(qa.V2.Powered, "S1 有電了");
        }
        private static void TakeThread(LanternPrototypeQA qa)
        {
            var w = qa.World;
            StandAt(qa, w.V2ThreadSpool.position, w.Marks["V2Yard"].position, 0.9f);
            Act(qa, 1, new LanternActionInput { Interact = true });
            Act(qa, 1, LanternActionInput.None);
            Assert.IsTrue(qa.V2.HasThread, "拿到紅線");
        }

        /// <summary>縫紉：放燈 → 坐上（爬上）凳子 → 穿針（在亮的範圍按 E）→ 站起來。</summary>
        private static void Sew(LanternPrototypeQA qa)
        {
            var w = qa.World; var v2 = qa.V2;
            StandAt(qa, w.V2SewingMachine.GetComponent<Collider>().bounds.center, w.V2ShopRoom.center, 1.0f);
            for (int i = 0; i < 3 && !v2.Seated; i++)
            {
                Aim(qa, w.V2SewingMachine.GetComponent<Collider>().bounds.center);
                Act(qa, 1, LanternActionInput.None);
                Act(qa, 1, new LanternActionInput { Interact = true });
            }
            Assert.IsTrue(v2.Seated, "坐上凳子（" + qa.PromptText + "）");
            Assert.IsFalse(v2.Tap.Active, "還沒坐好，不會開始判定");
            for (int k = 0; k < 60 && !v2.Tap.Active; k++) qa.Step(Dt, ExploreInput.None);
            Assert.IsTrue(v2.Tap.Active, "坐好之後才開始穿針");
            Act(qa, 1, LanternActionInput.None);
            int frames = 0; bool pressed = false;
            while (v2.Tap.Active && frames++ < 1200)
            {
                bool press = !pressed && v2.Tap.InZone;
                qa.Step(Dt, ExploreInput.None, new LanternActionInput { Interact = press });
                pressed = press;
            }
            Assert.IsTrue(v2.Sewn, "S4 穿好針");
            Act(qa, 2, LanternActionInput.None);
            Assert.IsFalse(v2.Seated, "站起來");
            for (int k = 0; k < 40 && qa.SeatAnimProgress < 1f; k++) qa.Step(Dt, ExploreInput.None);   // 等站起來的動作播完
        }
        // 從主屋（店面／走廊）走進餐廳、再到廚房門內。
        private static readonly (float, float)[] HallToKitchen = { (5.07f, 6.45f), (4.1f, 6.45f), (2.33f, 7.14f), (1.33f, 7.51f), (1.1f, 8.59f), (1.6f, 9.66f), (1.6f, 10.6f) };
        // 廚房門內 → 通路前（廚房側）。
        private static readonly (float, float)[] KitchenToPassage = { (1.865f, 11.635f), (1.865f, 13.173f), (3.404f, 13.788f) };
        // 走廊 → 普通後門 → 側院。
        private static readonly (float, float)[] HallToYard = { (5.1f, 9.4f), (5.1f, 10.6f), (5.25f, 11.2f) };
        // 側院 → 普通後門 → 走廊 → 店面。
        private static readonly (float, float)[] YardToShop = { (5.25f, 11.2f), (5.1f, 10.6f), (5.1f, 9.4f), (5.07f, 5.5f), (5.07f, 4.3f), (4.9f, 2.6f) };

        [Test]
        public void KitchenYardPassage_StartsAsASolidWall_FromBothSides_LanternCannotRevealIt()
        {
            var qa = NewQa();
            var w = qa.World;
            Assert.AreEqual(LanternPrototypeQA.MemoryAge.Child, qa.Age);
            Assert.IsFalse(w.KitchenYardOpen, "一開始通路不存在");
            Assert.IsTrue(w.KitchenYardSeal.gameObject.activeInHierarchy, "補上的牆在");
            Assert.AreEqual(0, ActivePassageTrims(w), "沒有門框（看起來是完整的牆）");
            // 兩側都擋住：從廚房、從側院往門洞打射線，地面到門頂都打在補牆上。
            var seal = w.KitchenYardSeal.GetComponentsInChildren<Collider>();
            float zc = (LanternGraybox.KitchenYardZ0 + LanternGraybox.KitchenYardZ1) / 2f;
            foreach (float zz in new[] { LanternGraybox.KitchenYardZ0 + 0.08f, zc, LanternGraybox.KitchenYardZ1 - 0.08f })
                foreach (float y in new[] { 0.3f, 1.0f, 1.9f, 2.5f })
                {
                    var k = PV(3.4f, zz) + Vector3.up * y; var yd = PV(4.6f, zz) + Vector3.up * y;
                    Assert.IsTrue(Physics.Raycast(k, Vector3.right, out var h1, 2f) && seal.Contains(h1.collider), "廚房側被擋住（z " + zz + "、高 " + y + "）");
                    Assert.IsTrue(Physics.Raycast(yd, Vector3.left, out var h2, 2f) && seal.Contains(h2.collider), "側院側被擋住（z " + zz + "、高 " + y + "）");
                }
            // 補牆和同一面牆用同一種材質（兩個世界都存在），提燈照了也不會顯出門洞。
            Assert.IsTrue(Physics.Raycast(PV(3.4f, 10.8f) + Vector3.up * 1.2f, Vector3.right, out var wall, 2f), "廚房東牆");
            var sealR = w.KitchenYardSeal.GetComponentsInChildren<Renderer>().First(r => r.name == "Wall");
            Assert.AreSame(wall.collider.GetComponent<Renderer>().sharedMaterial, sealR.sharedMaterial, "補牆和廚房東牆是同一種牆面");
            // 幼年從側院走向那面牆：過不去。
            OpenAllV2Doors(qa);
            qa.Player.Teleport(PV(4.8f, 13.8f), -90f, 0f);
            qa.Step(Dt, ExploreInput.None);
            qa.Rig.SetOn(true);
            Aim(qa, PV(4.0f, 13.8f) + Vector3.up * 1.2f);
            Act(qa, 60, LanternActionInput.None);
            Assert.IsTrue(w.KitchenYardSeal.gameObject.activeInHierarchy && !w.KitchenYardOpen, "提燈照著也還是牆");
            Assert.IsFalse(WalkTo(qa, PV(3.4f, 13.8f), 200), "幼年不能從側院穿牆進廚房");
            Assert.Greater((qa.Player.transform.position.x - LanternGraybox.V2Origin.x) / LanternGraybox.V2Scale, LanternGraybox.V2ExtWidth, "還在側院");
            Assert.AreEqual(LanternPrototypeQA.MemoryAge.Child, qa.Age, "還是幼年");
        }

        [Test]
        public void RouteA_ChildTakesThreadViaHallDoor_Sews_ThenDiningTeen_KitchenOpensPassage_CrossingMakesAdult()
        {
            var qa = NewQa();
            var w = qa.World;
            OpenAllV2Doors(qa);
            PowerS1(qa);
            // 幼年：走廊 → 普通後門 → 側院取線（不會變成人）。
            qa.Player.Teleport(PV(5.07f, 5.5f), 0f, 0f);
            qa.Step(Dt, ExploreInput.None);
            Walk(qa, "幼年：走廊→側院（普通後門）", HallToYard);
            Settle(qa);
            Assert.AreEqual(LanternPrototypeQA.MemoryAge.Child, qa.Age, "從走廊的門進側院：還是幼年");
            Assert.AreEqual(qa.ChildEyeHeight, qa.Player.EyeHeightValue, 1e-4f, "側院：幼年眼高");
            float baseR = LanternRig.Radii[qa.Rig.RadiusIndex];
            Assert.AreEqual(baseR * qa.ChildLanternScale, qa.Rig.TargetRadius, 1e-3f, "幼年：提燈光圈比較小");
            Assert.IsFalse(w.KitchenYardOpen, "通路還沒出現");
            TakeThread(qa);
            Walk(qa, "幼年：側院→店面", YardToShop);
            Sew(qa);
            Assert.AreEqual(LanternPrototypeQA.MemoryAge.Child, qa.Age);
            // 進餐廳：青春期。回到走廊、側院（普通後門）都不倒退、也不提前成人。
            qa.Player.Teleport(PV(5.07f, 4.3f), 0f, 0f);   // 從縫紉機旁回到店面往走廊的門前（店內動線另有測試）
            qa.Step(Dt, ExploreInput.None);
            Walk(qa, "店面→走廊", (5.07f, 5.5f));
            Walk(qa, "走廊→餐廳", HallToKitchen[0], HallToKitchen[1], HallToKitchen[2]);
            Settle(qa);
            Assert.AreEqual(LanternPrototypeQA.MemoryAge.Teen, qa.Age, "進餐廳：青春期");
            Assert.AreEqual(qa.TeenEyeHeight, qa.Player.EyeHeightValue, 1e-4f, "餐廳：青春期眼高");
            Assert.AreEqual(baseR * qa.TeenLanternScale, qa.Rig.TargetRadius, 1e-3f, "青春期：提燈光圈中等");
            Walk(qa, "餐廳→走廊", HallToKitchen[1], HallToKitchen[0], (5.07f, 7.5f));
            Walk(qa, "走廊→側院", HallToYard);
            Settle(qa);
            Assert.AreEqual(qa.TeenEyeHeight, qa.Player.EyeHeightValue, 1e-4f, "從走廊回側院：維持青春期（不倒退、不提前成人）");
            Assert.IsFalse(w.KitchenYardOpen, "還沒進過廚房：通路還沒出現");
            Walk(qa, "側院→走廊", (5.1f, 10.6f), (5.1f, 9.4f), (5.07f, 6.45f));
            // 進廚房：通路恢復，廚房維持青春期。
            Walk(qa, "走廊→餐廳→廚房", HallToKitchen);
            Assert.IsTrue(w.KitchenYardOpen, "進廚房後通路恢復");
            // 不是瞬間出現：補牆先變成只存在於異域的牆，從門洞中央慢慢顯現（和提燈照出來的效果一樣），顯現完才拿掉碰撞。
            Assert.IsTrue(w.KitchenYardRevealing, "正在慢慢顯現");
            var sealWall = w.KitchenYardSeal.GetComponentsInChildren<Renderer>().First(r => r.name == "Wall");
            Assert.AreEqual(LanternReveal.Mode.AnomalyOnly, LanternReveal.GetMode(sealWall.sharedMaterial), "顯現中：補牆只存在於異域（提燈照到也會提早看見）");
            float zc = (LanternGraybox.KitchenYardZ0 + LanternGraybox.KitchenYardZ1) / 2f;
            var top = PV(LanternGraybox.V2ExtWidth, zc) + Vector3.up * 2.4f;
            bool sawPartial = false;
            float revealT = 0f;
            while (w.KitchenYardRevealing && revealT < 6f)
            {
                qa.Step(Dt, ExploreInput.None);
                revealT += Dt;
                if (w.KitchenYardRevealing)
                {
                    Assert.IsTrue(Physics.Raycast(PV(3.4f, zc) + Vector3.up * 1f, Vector3.right, out var hb, 2f) && hb.collider.transform.IsChildOf(w.KitchenYardSeal), "顯現完之前還走不過去");
                    if (LanternReveal.IsReal(PV(LanternGraybox.V2ExtWidth, zc) + Vector3.up * 1.1f) && !LanternReveal.IsReal(top)) sawPartial = true;
                }
            }
            Assert.IsFalse(w.KitchenYardRevealing, "顯現完了（" + revealT.ToString("0.0") + " 秒，" + w.KitchenYardRevealDebug + "）");
            Assert.IsTrue(revealT > 1.2f && revealT < 4.5f, "顯現大約 2～3 秒（實際 " + revealT.ToString("0.0") + " 秒）");
            Assert.IsTrue(sawPartial, "從中間往外顯現（中間先出現、上緣後出現）");
            Assert.IsFalse(w.KitchenYardSeal.gameObject.activeInHierarchy, "補牆收起來");
            Assert.AreEqual(0, LanternReveal.ActiveRooms, "顯現完：門洞回到褪色的樣子（不留永久的真實）");
            Assert.AreEqual(3, ActivePassageTrims(w), "門框出現");
            Assert.AreEqual(qa.TeenEyeHeight, qa.Player.EyeHeightValue, 1e-4f, "廚房：維持青春期（不再直接成人）");
            Walk(qa, "廚房→通路前", KitchenToPassage);
            // 第一次從廚房穿過通路進側院：平順地恢復成人。
            float maxStep = WalkWatchingEye(qa, 4.8f, 13.6f, "廚房→側院（新通路）");
            Assert.AreEqual(LanternPrototypeQA.MemoryAge.Adult, qa.Age, "從廚房進側院：成人");
            Assert.LessOrEqual(maxStep, 1.3f * Dt + 1e-4f, "眼高是平順過渡（每格最多 " + maxStep.ToString("0.000") + " m）");
            Settle(qa);
            Assert.AreEqual(LanternPrototypeQA.AdultEyeHeight, qa.Player.EyeHeightValue, 1e-4f, "成人眼高");
            Assert.AreEqual(baseR, qa.Rig.TargetRadius, 1e-3f, "成人：提燈光圈恢復原本大小");
            // 雙向：再走回廚房；成人之後到哪裡都不倒退。
            Walk(qa, "側院→廚房（雙向）", (4.6f, 13.8f), (3.404f, 13.788f));
            Settle(qa);
            Assert.AreEqual(LanternPrototypeQA.AdultEyeHeight, qa.Player.EyeHeightValue, 1e-4f, "回到廚房：成人");
            Walk(qa, "廚房→餐廳→走廊", (1.865f, 13.173f), (1.865f, 11.635f), (1.6f, 10.6f), (1.6f, 9.66f), (1.1f, 8.59f), (1.33f, 7.51f), (2.33f, 7.14f), (4.1f, 6.45f), (5.07f, 6.45f));
            Settle(qa);
            Assert.AreEqual(LanternPrototypeQA.AdultEyeHeight, qa.Player.EyeHeightValue, 1e-4f, "走廊：成人");
            Walk(qa, "走廊→店面", (5.07f, 5.5f), (5.07f, 4.3f), (4.9f, 2.6f));
            Settle(qa);
            Assert.AreEqual(LanternPrototypeQA.AdultEyeHeight, qa.Player.EyeHeightValue, 1e-4f, "改衣店：成人");
            Assert.IsTrue(w.KitchenYardOpen, "通路一直開著");
        }

        [Test]
        public void RouteB_DiningFirst_KitchenPassageToYard_TakeThread_BackToShopAndSew()
        {
            var qa = NewQa();
            var w = qa.World;
            var v2 = qa.V2;
            float body = qa.Player.Body.height;
            OpenAllV2Doors(qa);
            PowerS1(qa);
            Assert.IsFalse(v2.HasThread);
            qa.Player.Teleport(PV(5.07f, 5.5f), 0f, 0f);
            qa.Step(Dt, ExploreInput.None);
            Settle(qa);
            Assert.AreEqual(qa.ChildEyeHeight, qa.Player.EyeHeightValue, 1e-4f, "走廊：幼年");
            Walk(qa, "走廊→餐廳→廚房", HallToKitchen);
            Settle(qa);
            Assert.AreEqual(LanternPrototypeQA.MemoryAge.Teen, qa.Age);
            Assert.IsTrue(w.KitchenYardOpen, "還沒拿線也能開通路（不互相鎖死）");
            Walk(qa, "廚房→通路前", KitchenToPassage);
            Walk(qa, "廚房→側院（新通路）", (4.8f, 13.6f));
            Settle(qa);
            Assert.AreEqual(LanternPrototypeQA.MemoryAge.Adult, qa.Age);
            Assert.AreEqual(LanternPrototypeQA.AdultEyeHeight, qa.Player.EyeHeightValue, 1e-4f, "側院：成人");
            TakeThread(qa);
            Walk(qa, "側院→普通後門→店面", YardToShop);
            Settle(qa);
            Assert.AreEqual(LanternPrototypeQA.AdultEyeHeight, qa.Player.EyeHeightValue, 1e-4f, "回到改衣店：成人（不倒退）");
            Sew(qa);
            Assert.AreEqual(body, qa.Player.Body.height, 1e-4f, "碰撞體沒有變");
            Assert.AreEqual(LanternPrototypeQA.WalkSpeed, qa.Player.WalkSpeedValue, 1e-3f, "移動速度不變");
        }

        [Test]
        public void MemoryEyeHeight_StreetIsAdult_TestModeOverride_AndRebuildResetsToChild()
        {
            var qa = NewQa();
            var w = qa.World;
            Assert.AreEqual(0.85f, qa.ChildEyeHeight, 1e-4f, "幼年眼高 0.85 m");
            Assert.AreEqual(1.12f, qa.TeenEyeHeight, 1e-4f, "青春期眼高 1.12 m");
            qa.GoTo("V2Street"); Settle(qa);
            Assert.AreEqual(LanternPrototypeQA.AdultEyeHeight, qa.Player.EyeHeightValue, 1e-4f, "門外的街道：成人");
            qa.GoTo("V2Shop"); Settle(qa);
            Assert.AreEqual(qa.ChildEyeHeight, qa.Player.EyeHeightValue, 1e-4f, "店面：幼年");
            // 走進記憶門的過渡是平順的（測試模式強制視角也走同一套過渡）。
            qa.ViewOverride = qa.TeenEyeHeight;
            qa.Step(Dt, ExploreInput.None);
            Assert.Greater(qa.Player.EyeHeightValue, qa.ChildEyeHeight + 1e-3f);
            Assert.Less(qa.Player.EyeHeightValue, qa.TeenEyeHeight - 1e-3f, "還在過渡中");
            qa.ViewOverride = null;
            qa.CycleViewOverride(); Assert.AreEqual(LanternPrototypeQA.AdultEyeHeight, qa.ViewOverride.Value, 1e-4f);
            qa.CycleViewOverride(); Assert.AreEqual(qa.ChildEyeHeight, qa.ViewOverride.Value, 1e-4f);
            qa.CycleViewOverride(); Assert.IsNull(qa.ViewOverride);
            // 重新開始：房子重建、回到幼年、通路重新封住，正式布局也還在。
            qa.GoTo("V2Kitchen"); Settle(qa);
            Assert.IsTrue(w.KitchenYardOpen);
            qa.StartV2();
            Settle(qa);
            Assert.AreEqual(LanternPrototypeQA.MemoryAge.Child, qa.Age, "重新開始：幼年");
            Assert.IsFalse(qa.World.KitchenYardOpen, "重新開始：通路封住");
            Assert.AreEqual(1, qa.World.ShopLayout, "重建後仍是服飾店 A");
            Assert.IsTrue(qa.World.DiningLayoutIsB && qa.World.KitchenLayoutIsB, "重建後仍是餐廳 B、廚房 B");
            qa.MemoryHeights = false;
            qa.GoTo("V2Shop"); Settle(qa);
            Assert.AreEqual(LanternPrototypeQA.AdultEyeHeight, qa.Player.EyeHeightValue, 1e-4f, "關掉記憶眼高＝成人");
        }
    }
}