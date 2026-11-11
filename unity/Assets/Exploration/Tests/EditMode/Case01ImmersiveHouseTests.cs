using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// 沉浸式住宅探索 QA：三段步速與平順加減速、鏡頭步態（可關、停下即靜止）、手電筒（跟視線、近距收光、檢視時收起）、
    /// 合照近看（拿起、不能走、放大、翻面、版本切換、放回後恢復）、環境亮度、場景重置、停用的 QA 鍵、
    /// 前門 → 客廳 → 合照 → 樓梯 → 二樓走廊 → 回一樓的實際行走（鏡頭不進牆）、生活陳設沒有缺件、其他 QA 不受影響。
    /// 自動化只驗證機制與碰撞；步行手感、明暗與美術品質要真人在 Play 裡驗收。
    /// </summary>
    public sealed class Case01ImmersiveHouseTests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
        }

        private Case01ImmersiveHouseQA NewQa()
        {
            var go = new GameObject("Case01ImmersiveHouseTest");
            roots.Add(go);
            var s = go.AddComponent<Case01ImmersiveHouseQA>();
            s.Setup(false, 1);
            Step(s, Case01Input.None);
            return s;
        }

        private static void Step(Case01ImmersiveHouseQA s, Case01Input input)
        {
            s.House.Tick(Dt, s.Filter(input));
            s.Host.Frame(Dt);
            s.Frame(Dt);
            s.ApplyCamera(Dt);
        }

        private static void Run(Case01ImmersiveHouseQA s, float sec)
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

        private static bool CameraClear(Case01ImmersiveHouseQA s)
        {
            var cc = s.House.Player.Body;
            cc.enabled = false;
            bool hit = Physics.CheckSphere(s.House.ExploreCamera.transform.position, 0.06f, Case01SpatialMap.SightMask, QueryTriggerInteraction.Ignore);
            cc.enabled = true;
            return !hit;
        }

        private static bool? TryWalk(Case01ImmersiveHouseQA s, float x, float z, float max = 25f)
        {
            var p = s.House.Player;
            var stuckAt = p.transform.position;
            float stuck = 0f;
            for (float t = 0; t < max; t += Dt)
            {
                var pos = p.transform.position;
                var to = new Vector3(x - pos.x, 0f, z - pos.z);
                if (to.magnitude < 0.22f) return true;
                Step(s, Case01Input.From(new ExploreInput { Move = new Vector2(0f, 1f), Look = LookDelta(p, new Vector3(x, p.EyePosition.y, z), true) }));
                Assert.IsTrue(CameraClear(s), "鏡頭進到牆或家具裡：" + s.House.ExploreCamera.transform.position);
                var np = p.transform.position;
                if ((np - stuckAt).magnitude > 0.05f)
                {
                    stuckAt = np;
                    stuck = 0f;
                }
                else if ((stuck += Dt) > 2f) return null;
            }
            return null;
        }

        private static void WalkTo(Case01ImmersiveHouseQA s, float x, float z)
        {
            Assert.IsTrue(TryWalk(s, x, z) == true, "走不到 (" + x + ", " + z + ")，停在 " + s.House.Player.transform.position + "（" + s.House.CurrentRoom + "）");
        }

        private static void Path(Case01ImmersiveHouseQA s, params float[] xz)
        {
            for (int i = 0; i + 1 < xz.Length; i += 2) WalkTo(s, xz[i], xz[i + 1]);
        }

        private static void Aim(Case01ImmersiveHouseQA s, Vector3 target)
        {
            for (int i = 0; i < 2; i++) Step(s, Case01Input.From(new ExploreInput { Look = LookDelta(s.House.Player, target, false) }));
        }

        private static void PickUpPhoto(Case01ImmersiveHouseQA s)
        {
            var it = s.House.Layout.Interactables[Case01Ids.Photo];
            Aim(s, it.transform.TransformPoint(it.GetComponent<BoxCollider>().center));
            Assert.IsNotNull(s.House.CurrentTarget, "準星對到合照");
            Assert.AreEqual(Case01Ids.Photo, s.House.CurrentTarget.Id);
            Step(s, Case01Input.From(new ExploreInput { Interact = true }));
        }

        // ---------------- 場景 ----------------

        [Test]
        public void Scene_IsTheR3House_DressedWithBlenderModels_NoMissingAssets()
        {
            Assert.IsTrue(Case01ImmersiveHouse.Available, "Case01Immersive 資產存在");
            var s = NewQa();
            Assert.AreEqual(Case01Floorplan.R3, s.House.Layout.Floorplan);
            var d = s.Dressing;
            Assert.IsNotNull(d);
            Assert.AreEqual(0, d.Missing, "所有陳設模型都載入");
            Assert.GreaterOrEqual(d.Props.Count, 60, "玄關、客廳、樓梯、二樓的陳設");
            Assert.AreEqual(3, d.Sconces.Count, "樓梯一盞、二樓走廊兩盞壁燈");
            Assert.GreaterOrEqual(d.Balusters.Count, 50, "樓梯與樓梯井的車削欄杆柱");
            foreach (var name in new[] { "ShoesPair", "BootsPair", "CoatHanging", "CoatHangingRed", "Umbrella", "KeyBowl", "LetterStack", "Mug", "Remote", "CushionTilted", "Newspaper", "WallSconce", "NewelPost", "Handrail", "StairRod" })
                Assert.IsTrue(d.Props.Any(p => p != null && p.name == name), "陳設：" + name);
            foreach (var p in d.Props)
            {
                if (p == null) continue;
                foreach (var mf in p.GetComponentsInChildren<MeshFilter>())
                    Assert.IsFalse(mf.sharedMesh == null || mf.sharedMesh.name == "Cube" || mf.sharedMesh.name == "Cylinder" || mf.sharedMesh.name == "Sphere", p.name + " 不是 Primitive 方塊");
                foreach (var r in p.GetComponentsInChildren<Renderer>())
                    foreach (var m in r.sharedMaterials)
                        Assert.IsTrue(m != null && (m.mainTexture != null || m.name == "LR_Plastic" || m.name == "LR_Screen" || m == d.LensMat || m.name.StartsWith("P1_Glass")), p.name + " 的材質 " + (m != null ? m.name : "null") + " 有貼圖");
                var b = p.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
                if (p.name != "Handrail") Assert.Less(Mathf.Max(b.size.x, b.size.z), 1.2f, p.name + " 尺寸合理（" + b.size + "）");
            }
            // 壁燈的燈罩朝向走廊（不是埋在牆裡）。
            foreach (var sc in d.Props.Where(p => p != null && p.name == "WallSconce"))
            {
                var b = sc.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
                Assert.IsTrue(s.House.Layout.RoomAt(new Vector3(b.center.x, sc.position.y - 1.6f, b.center.z)) != Case01Room.None, "壁燈在室內那一側");
            }
            Assert.IsFalse(s.DotVisible == false, "正常遊玩只有中央小點");
            Assert.IsFalse(s.DebugVisible, "除錯資訊預設隱藏");
            Assert.AreEqual("", s.House.Hud.Base.ViewMode.text, "沒有 QA 標題文字");
        }

        // ---------------- 移動 ----------------

        [Test]
        public void WalkSpeeds_ThreePresets_SmoothStartAndStop_MeasuredSpeedMatches()
        {
            var s = NewQa();
            Assert.AreEqual(1.5f, s.WalkSpeed, 1e-3f, "預設正常步行 1.5 m/s");
            s.Filter(new Case01Input { Function = 6 });
            Assert.AreEqual(1.8f, s.WalkSpeed, 1e-3f, "F6：較快步行");
            s.Filter(new Case01Input { Choice = 1 });
            Assert.AreEqual(1.2f, s.WalkSpeed, 1e-3f, "1：慢走");
            foreach (var preset in new[] { 0, 1, 2 })
            {
                s.SetSpeed(preset);
                s.House.Player.Teleport(new Vector3(7.6f, 0.05f, 4.2f), 0f, 0f);
                Run(s, 0.2f);
                Step(s, Case01Input.From(new ExploreInput { Move = new Vector2(0f, 1f) }));
                Step(s, Case01Input.From(new ExploreInput { Move = new Vector2(0f, 1f) }));
                float early = s.House.Player.LastHorizontalSpeed;
                Assert.Less(early, Case01ImmersiveHouseQA.WalkSpeeds[preset] * 0.6f, "起步有加速（不是瞬間全速）");
                var p0 = s.House.Player.transform.position;
                for (int i = 0; i < 30; i++) Step(s, Case01Input.From(new ExploreInput { Move = new Vector2(0f, 1f) }));
                Assert.AreEqual(Case01ImmersiveHouseQA.WalkSpeeds[preset], s.House.Player.LastHorizontalSpeed, 0.06f, "穩定步速＝" + Case01ImmersiveHouseQA.WalkSpeeds[preset]);
                Step(s, Case01Input.None);
                float stopping = s.House.Player.LastHorizontalSpeed;
                Assert.Greater(stopping, 0.2f, "放開按鍵不是急停");
                Run(s, 0.6f);
                Assert.Less(s.House.Player.LastHorizontalSpeed, 0.01f, "很快停穩");
            }
            // Shift：快步（不是跑）。
            s.SetSpeed(1);
            for (int i = 0; i < 30; i++) Step(s, Case01Input.From(new ExploreInput { Move = new Vector2(0f, -1f), Run = true }));
            Assert.AreEqual(1.5f * Case01ImmersiveHouseQA.BriskFactor, s.House.Player.LastHorizontalSpeed, 0.08f);
            Assert.AreEqual(55f, s.Host.FieldOfView, 1e-3f, "垂直 FOV 55°（F5 可換）");
            Assert.AreEqual(1.58f, s.House.Player.EyeHeightValue, 1e-3f, "眼高 1.58 m（F7 可換）");
        }

        [Test]
        public void HeadBob_IsSubtle_StopsCompletelyWhenStanding_AndCanBeTurnedOff()
        {
            var s = NewQa();
            s.House.Player.Teleport(new Vector3(7.6f, 0.05f, 4.2f), 0f, 0f);
            float maxOff = 0f;
            for (int i = 0; i < 40; i++)
            {
                Step(s, Case01Input.From(new ExploreInput { Move = new Vector2(0f, 1f) }));
                maxOff = Mathf.Max(maxOff, s.BobOffset.magnitude);
            }
            Assert.Greater(maxOff, 0.001f, "走路時有輕微步態");
            Assert.LessOrEqual(maxOff, 0.012f, "步態幅度很小（≤ 1.2 cm）");
            Run(s, 0.6f);
            Assert.Less(s.BobOffset.magnitude, 1e-4f, "停下後鏡頭靜止");
            s.ToggleHeadBob();
            for (int i = 0; i < 30; i++) Step(s, Case01Input.From(new ExploreInput { Move = new Vector2(0f, -1f) }));
            Assert.Less(s.BobOffset.magnitude, 1e-4f, "B：關閉鏡頭步態");
        }

        // ---------------- 照明 ----------------

        [Test]
        public void Flashlight_FollowsTheView_DimsUpClose_HidesDuringInspect_AndHasThreeLevels()
        {
            var s = NewQa();
            var f = s.Flashlight;
            Assert.IsNotNull(f);
            Assert.IsFalse(f.Light.enabled, "預設關");
            s.ToggleFlashlight();
            Run(s, 0.3f);
            Assert.IsTrue(f.Light.enabled, "F：開");
            Assert.AreEqual(LightType.Spot, f.Light.type);
            Assert.IsNotNull(f.Light.cookie, "有光斑貼圖");
            Assert.AreEqual(LightShadows.Soft, f.Light.shadows);
            Assert.Greater(Vector3.Dot(f.Light.transform.forward, s.House.ExploreCamera.transform.forward), 0.99f, "光束方向＝視線");
            Assert.IsTrue(f.Model != null && f.Model.gameObject.activeSelf, "手上拿著手電筒");
            Assert.AreEqual(0, f.Light.cullingMask & (1 << Case01Inspector.Layer), "不照到檢視台");
            // 遠處 vs 貼近牆面：近距離自動收光（不會把紙張照到過曝）。
            s.House.Player.Teleport(new Vector3(7.6f, 0.05f, 4.2f), 0f, 0f);
            Run(s, 0.6f);
            float far = f.Light.intensity;
            s.House.Player.Teleport(new Vector3(7.0f, 0.05f, 2.2f), -90f, 0f);
            Run(s, 0.8f);
            Assert.Greater(f.LastHitDistance, 0f);
            Assert.Less(f.LastHitDistance, 0.6f);
            Assert.Less(f.Light.intensity, far * 0.65f, "貼近牆：收光");
            int lv = f.Level;
            s.CycleFlashlightLevel();
            Assert.AreNotEqual(lv, f.Level, "X：強度三段");
            s.ToggleFlashlight();
            Run(s, 0.3f);
            Assert.IsFalse(f.Light.enabled);
            // 亮度預設。
            Assert.AreEqual(1, s.BrightnessPreset);
            s.CycleBrightness();
            Assert.AreEqual(2, s.BrightnessPreset, "L：環境亮度比較");
        }

        // ---------------- 合照 ----------------

        [Test]
        public void FamilyPhoto_PickUp_CannotWalk_Zoom_Flip_Variant_PutBack()
        {
            var s = NewQa();
            var d = s.Dressing;
            Assert.IsNotNull(d.Photo);
            Assert.GreaterOrEqual(d.PhotoNormal.width, 2048, "高解析照片");
            Assert.AreSame(d.PhotoNormal, d.PhotoMaterial.mainTexture, "壁爐架上的合照就是這張照片");
            s.House.Player.Teleport(new Vector3(1.45f, 0.05f, 1.9f), -60f, 0f);
            Run(s, 0.2f);
            s.ToggleFlashlight();
            PickUpPhoto(s);
            Run(s, 0.5f);
            Assert.IsTrue(s.Inspecting, "E：拿起近看");
            Assert.IsFalse(d.Photo.WorldCarrier.enabled, "世界中的照片被拿起");
            Assert.AreEqual(0.72f, s.Inspector.ScreenHeightFraction, 0.03f, "照片佔畫面高度約 72%（完整看清）");
            Assert.IsFalse(s.Flashlight.Light.enabled, "檢視時手電筒收起");
            Assert.IsFalse(s.DotVisible);
            var pos = s.House.Player.transform.position;
            for (int i = 0; i < 20; i++) Step(s, Case01Input.From(new ExploreInput { Move = new Vector2(0f, 1f), Look = new Vector2(3f, 0f) }));
            Assert.Less(Vector3.Distance(pos, s.House.Player.transform.position), 0.01f, "檢視中不能走");
            Assert.IsTrue(s.Inspecting, "移動滑鼠只是傾斜相片");
            // 放大、移動、翻面、版本。
            for (int i = 0; i < 10; i++) s.Inspector.Feed(Vector2.zero, 3f, false);
            Assert.AreEqual(Case01Inspector.MaxZoom, s.Inspector.Zoom, 1e-3f, "滾輪放大到 3.5×");
            Run(s, 0.05f);
            Assert.Greater(s.Inspector.ScreenHeightFraction, 2f, "放大後可以看細節");
            s.Inspector.Feed(new Vector2(-30f, 20f), 0f, true);
            Assert.Greater(s.Inspector.Pan.magnitude, 0.001f, "放大後拖曳移動");
            s.Inspector.Flip();
            Run(s, 0.6f);
            Assert.IsTrue(s.Inspector.Flipped, "空白鍵：翻到背面");
            Assert.IsTrue(s.Inspector.Sheet.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t => t.text.Contains("十七號")), "背面有手寫字");
            s.TogglePhotoVariant();
            Assert.AreSame(d.PhotoAnomaly, d.Photo.Front, "P：異常候選版本");
            Assert.AreSame(d.PhotoAnomaly, d.PhotoMaterial.mainTexture, "世界中的照片也一起換");
            s.TogglePhotoVariant();
            Assert.AreSame(d.PhotoNormal, d.PhotoMaterial.mainTexture);
            // E 放回。
            Step(s, Case01Input.From(new ExploreInput { Interact = true }));
            Run(s, 0.5f);
            Assert.IsFalse(s.Inspecting, "E：放回");
            Assert.IsTrue(d.Photo.WorldCarrier.enabled, "照片回到壁爐架");
            Assert.IsTrue(s.Flashlight.Light.enabled, "手電筒重新拿出");
            Assert.IsTrue(TryWalk(s, 1.6f, 1.2f, 4f) == true, "放回後可以正常走動");
        }

        // ---------------- 路線 ----------------

        [Test]
        public void WalkThrough_FrontDoor_Entrance_Living_Photo_Stairs_UpperHall_AndBackDown()
        {
            var s = NewQa();
            Assert.AreEqual(Case01Room.Entrance, s.House.CurrentRoom, "從前門內開始");
            Path(s, 7.1f, 2.0f, 7.6f, 3.6f);
            var door = s.Host.World.Door("door_living");
            if (!door.IsOpenEnough)
            {
                Aim(s, door.Hotspot.transform.position);
                Step(s, Case01Input.From(new ExploreInput { Interact = true }));
                Run(s, 1.2f);
            }
            Path(s, 5.6f, 3.55f, 4.95f, 3.55f, 4.95f, 1.2f, 1.6f, 1.2f, 1.45f, 1.9f);
            Assert.AreEqual(Case01Room.Living, s.House.CurrentRoom);
            PickUpPhoto(s);
            Run(s, 0.6f);
            Assert.IsTrue(s.Inspecting);
            Step(s, Case01Input.From(new ExploreInput { Interact = true }));
            Run(s, 0.5f);
            Path(s, 1.6f, 1.2f, 4.95f, 1.2f, 4.95f, 3.55f, 5.6f, 3.55f, 7.6f, 3.55f, 7.6f, 11.2f, 9.45f, 11.2f, 9.45f, 10.9f, 9.45f, 7.0f, 9.45f, 6.2f, 8.4f, 6.2f, 7.6f, 7.0f, 7.6f, 3.0f);
            Assert.AreEqual(Case01Room.Landing, s.House.CurrentRoom, "二樓走廊");
            Assert.Greater(s.House.Player.transform.position.y, 2.9f);
            Path(s, 7.6f, 7.0f, 8.4f, 6.2f, 9.45f, 6.2f, 9.45f, 10.9f, 9.45f, 11.3f, 7.6f, 11.2f, 7.6f, 3.0f);
            Assert.Less(s.House.Player.transform.position.y, 0.2f, "回到一樓");
            Assert.AreEqual(Case01Room.Entrance, s.House.CurrentRoom);
        }

        // ---------------- QA 鍵與保護 ----------------

        [Test]
        public void DisabledKeys_Reset_Debug_AndOtherQaUntouched()
        {
            var s = NewQa();
            s.Filter(new Case01Input { Jump = 2 });
            s.Filter(new Case01Input { Jump = 3 });
            Step(s, Case01Input.None);
            Assert.AreEqual(Case01Floorplan.R3, s.House.Layout.Floorplan, "F2（1.8 對照）停用");
            Assert.AreEqual(0, s.House.HouseStage, "F3（H 階段）停用：只看正常住宅");
            s.House.Player.Teleport(new Vector3(4f, 0.05f, 2f), 0f, 0f);
            s.ToggleFlashlight();
            s.TogglePhotoVariant();
            s.ResetScene();
            Assert.Less(Vector3.Distance(s.House.Player.transform.position, s.House.Layout.Spawn.Position), 0.05f, "R：回到前門內");
            Assert.IsFalse(s.Flashlight.On);
            Assert.AreEqual(0, s.PhotoVariant);
            s.ToggleDebug();
            Step(s, Case01Input.None);
            Assert.IsTrue(s.DebugVisible, "F12：除錯資訊");
            var go = new GameObject("Phase19");
            roots.Add(go);
            var q = go.AddComponent<Case01ImmersionQA>();
            q.Setup(false, null, 1, true, Case01QaMode.CoreV03, true);
            Assert.IsFalse(q.House.Layout.Root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "ImmersiveHouseQA"), "Phase 1.9 QA 不受影響");
        }
    }
}
