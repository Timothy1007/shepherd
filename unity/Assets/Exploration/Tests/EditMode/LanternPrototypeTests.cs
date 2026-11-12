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
            if (root != null) Object.DestroyImmediate(root);
            root = null;
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
            Assert.AreEqual(LanternRig.Radii[1], LanternReveal.Radius, 1e-3f, "預設光圈 3 m");
            var ahead = rig.RevealCenter;
            Assert.IsTrue(LanternReveal.IsReal(ahead), "提燈前方顯示真實");
            Assert.IsFalse(LanternReveal.IsReal(ahead + qa.Cam.transform.forward * 6f), "光圈外仍是異域");
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
            Assert.IsTrue(vp.z > 0f && vp.x > 0.5f && vp.x < 1f && vp.y > 0f && vp.y < 0.6f, "提燈在畫面右下（" + vp + "）");
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
