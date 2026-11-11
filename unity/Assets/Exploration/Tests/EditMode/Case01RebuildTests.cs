using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// 全屋重製 v2（新客廳）：新資產載入且不引用舊建築；PBR 材質接上法線與金屬光滑度；碰撞（牆、家具）真的擋人；
    /// 三段步速、FOV、眼高、曝光、環境燈、吊燈、手電筒都真的改變鏡頭或燈；合照可拿起、翻面、放回。
    /// 自動化測試不代表美術品質驗收。
    /// </summary>
    public sealed class Case01RebuildTests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
            var stray = GameObject.Find(Case01RebuildRoom.RootName);
            if (stray != null) Object.DestroyImmediate(stray);
        }

        private Case01RebuildQA NewQa()
        {
            var go = new GameObject("Case01RebuildTest");
            roots.Add(go);
            var s = go.AddComponent<Case01RebuildQA>();
            s.Setup(false);
            roots.Add(s.Room.Root.gameObject);
            s.Step(Dt, ExploreInput.None);
            return s;
        }

        private static Vector2 LookDelta(FirstPersonController p, Vector3 target)
        {
            var d = target - p.EyePosition;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            return new Vector2(Mathf.DeltaAngle(p.Yaw, yaw) / (FirstPersonController.LookSensitivity * p.LookScale), -(pitch - p.Pitch) / (FirstPersonController.LookSensitivity * p.LookScale));
        }

        [Test]
        public void NewLivingRoom_IsNewArchitecture_WithPbrMaterials_AndNoOldHouse()
        {
            Assert.IsTrue(Case01RebuildRoom.Available, "新客廳模型存在");
            var s = NewQa();
            var room = s.Room;
            Assert.GreaterOrEqual(room.ArchitectureColliders, 6, "牆、地板、天花、線腳、門框、窗");
            Assert.GreaterOrEqual(room.FurnitureColliders, 8, "沙發、扶手椅、茶几、壁爐架、書櫃、暖氣片、電視櫃、門");
            foreach (var n in new[] { "A_Walls", "A_Floor", "A_Mouldings", "A_Window", "F_Mantel", "F_FireInsert", "F_Sofa", "F_Armchair", "F_AlcoveCupboard", "F_Radiator", "D_Books", "D_CurtainL", "D_OverMirror", "D_PhotoPrint", "D_Rug" })
                Assert.IsNotNull(room.Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n), "新模型：" + n);
            var floor = room.Root.GetComponentsInChildren<MeshFilter>(true).First(m => m.name == "A_Floor").sharedMesh;
            Assert.Greater(floor.vertexCount, 2000, "地板是一塊一塊建出來的板（不是一張平面）");
            var books = room.Root.GetComponentsInChildren<MeshFilter>(true).First(m => m.name == "D_Books").sharedMesh;
            Assert.Greater(books.vertexCount, 1500, "書一本一本建");
            foreach (var n in new[] { "RB_Floor", "RB_Wallpaper", "RB_Velvet", "RB_Tweed", "RB_Mantel", "RB_Rug", "RB_Tile" })
            {
                var m = room.Get(n);
                Assert.IsNotNull(m.mainTexture, n + " albedo");
                Assert.IsTrue(m.IsKeywordEnabled("_NORMALMAP") && m.GetTexture("_BumpMap") != null, n + " 法線貼圖");
                Assert.IsTrue(m.IsKeywordEnabled("_METALLICGLOSSMAP") && m.GetTexture("_MetallicGlossMap") != null, n + " 金屬光滑度貼圖");
            }
            Assert.IsNull(Object.FindObjectOfType<Case01HouseQA>(), "不建立舊住宅（R3／Phase 1.9）");
            Assert.IsFalse(room.Root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "GroundFloor" || t.name.StartsWith("Case01Phase")), "不含舊建築物件");
            foreach (var mf in room.Root.GetComponentsInChildren<MeshFilter>(true))
                Assert.IsFalse(mf.sharedMesh != null && (mf.sharedMesh.name == "Cube" || mf.sharedMesh.name == "Sphere" || mf.sharedMesh.name == "Cylinder"), mf.name + " 不是 Primitive");
            Assert.IsTrue(s.Post.Available, "畫面後製 shader 可用");
            Assert.IsFalse(s.DebugVisible, "除錯資訊預設隱藏");
        }

        [Test]
        public void ViewAndLightingPresets_ChangeWhatTheCameraSees()
        {
            var s = NewQa();
            Assert.AreEqual(55f, s.Cam.fieldOfView, 1e-3f);
            s.CycleFov();
            s.Step(Dt, ExploreInput.None);
            Assert.AreEqual(62f, s.Cam.fieldOfView, 1e-3f, "F5：FOV");
            float eye0 = s.Cam.transform.position.y;
            s.SetEye(2);
            s.Step(Dt, ExploreInput.None);
            Assert.AreEqual(0.14f, s.Cam.transform.position.y - eye0, 0.02f, "F7：眼高 1.58 → 1.72");
            s.SetExposure(2);
            s.Step(Dt, ExploreInput.None);
            Assert.AreEqual(Case01RebuildQA.Exposures[2], s.Post.Exposure, 1e-3f, "G：曝光");
            Assert.IsTrue(s.Room.Lamps.All(l => l.enabled), "環境燈預設開");
            s.SetLamps(false);
            Assert.IsTrue(s.Room.Lamps.All(l => !l.enabled), "L：環境燈關");
            Assert.IsFalse(s.Room.Pendant.enabled);
            s.TogglePendant();
            Assert.IsTrue(s.Room.Pendant.enabled, "K：吊燈");
            Assert.IsFalse(s.Flashlight.Light.enabled);
            s.ToggleFlashlight();
            for (int i = 0; i < 10; i++) s.Step(Dt, ExploreInput.None);
            Assert.IsTrue(s.Flashlight.Light.enabled, "F：手電筒");
            Assert.Greater(Vector3.Dot(s.Flashlight.Light.transform.forward, s.Cam.transform.forward), 0.99f);
            foreach (var l in s.Room.Lamps.Where(l => l.name != "Fire")) Assert.AreEqual(LightShadows.Soft, l.shadows, l.name + " 有柔和陰影");
            Assert.IsNotNull(s.Room.Probe, "反射探針");
        }

        [Test]
        public void Walking_ThreeSpeeds_AndCollisionKeepsThePlayerInsideAndOutOfFurniture()
        {
            var s = NewQa();
            var floor = s.Room.Root.GetComponentsInChildren<Renderer>(true).First(r => r.name == "A_Floor").bounds;
            foreach (var p in new[] { 0, 1, 2 })
            {
                s.SetSpeed(p);
                s.ResetScene();
                // 窗邊 → 左側凹室：一段沒有家具的直線。
                var a = s.Room.Marks["E_CamWindow"].position;
                var b = s.Room.Marks["E_TgtDark"].position;
                var d = b - a;
                s.Player.Teleport(new Vector3(a.x, 0.05f, a.z), Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);
                for (int i = 0; i < 25; i++) s.Step(Dt, new ExploreInput { Move = new Vector2(0f, 1f) });
                Assert.AreEqual(Case01RebuildQA.WalkSpeeds[p], s.Player.LastHorizontalSpeed, 0.12f, "步速 " + Case01RebuildQA.WalkSpeeds[p]);
            }
            // 往四個方向一直走：被牆擋住，不會離開房間。
            foreach (var yaw in new[] { 0f, 90f, 180f, 270f })
            {
                s.ResetScene();
                var p = s.Player;
                p.Teleport(p.transform.position, yaw, 0f);
                for (int i = 0; i < 150; i++) s.Step(Dt, new ExploreInput { Move = new Vector2(0f, 1f) });
                var pos = p.transform.position;
                Assert.IsTrue(pos.x > floor.min.x - 0.05f && pos.x < floor.max.x + 1.3f && pos.z > floor.min.z - 0.05f && pos.z < floor.max.z + 0.05f, "朝 " + yaw + "° 走：還在房子裡（" + pos + "）");
                Assert.Greater(pos.y, -0.2f, "沒有掉出地板");
            }
            // 沙發擋人：從沙發一側走向它的中心，停在外面。
            var sofa = s.Room.Root.GetComponentsInChildren<Collider>(true).First(c => c.name == "F_Sofa").bounds;
            s.ResetScene();
            var start = new Vector3(sofa.center.x, 0.05f, sofa.max.z + 0.7f);
            s.Player.Teleport(start, 180f, 0f);
            for (int i = 0; i < 90; i++) s.Step(Dt, new ExploreInput { Move = new Vector2(0f, 1f), Look = LookDelta(s.Player, new Vector3(sofa.center.x, 1.5f, sofa.center.z)) * 0.3f });
            Assert.IsFalse(sofa.Contains(s.Player.transform.position + Vector3.up * 0.5f), "沒有走進沙發裡");
        }

        [Test]
        public void FamilyPhoto_PickUp_Flip_PutBack()
        {
            var s = NewQa();
            Assert.IsNotNull(s.Photo, "壁爐架上的合照");
            Assert.IsNotNull(s.Room.PhotoTexture);
            Assert.GreaterOrEqual(s.Room.PhotoTexture.width, 2048, "高解析");
            var p = s.Room.PhotoPrint.bounds.center;
            var dir = (s.Player.transform.position - p);
            dir.y = 0f;
            var feet = p + dir.normalized * 1.0f;
            s.Player.Teleport(new Vector3(feet.x, 0.05f, feet.z), 0f, 0f);
            for (int i = 0; i < 3; i++) s.Step(Dt, new ExploreInput { Look = LookDelta(s.Player, p) });
            Assert.IsTrue(s.AimingAtPhoto, "準星對到合照");
            Assert.AreEqual("E 拿起合照", s.PromptText);
            s.Step(Dt, new ExploreInput { Interact = true });
            for (int i = 0; i < 12; i++) s.Step(Dt, ExploreInput.None);
            Assert.IsTrue(s.Inspecting, "拿起近看");
            Assert.IsFalse(s.Photo.WorldCarrier.enabled);
            var pos = s.Player.transform.position;
            for (int i = 0; i < 10; i++) s.Step(Dt, new ExploreInput { Move = new Vector2(0f, 1f) });
            Assert.Less(Vector3.Distance(pos, s.Player.transform.position), 0.01f, "檢視中不能走");
            s.Inspector.Flip();
            for (int i = 0; i < 20; i++) s.Step(Dt, ExploreInput.None);
            Assert.IsTrue(s.Inspector.Flipped);
            s.Step(Dt, new ExploreInput { Interact = true });
            for (int i = 0; i < 20; i++) s.Step(Dt, ExploreInput.None);
            Assert.IsFalse(s.Inspecting, "放回");
            Assert.IsTrue(s.Photo.WorldCarrier.enabled);
        }
    }
}
