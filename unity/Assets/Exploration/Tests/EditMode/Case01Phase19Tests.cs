using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// Phase 1.9 整體關卡與異域美術重構：R3（前門進來沒有第二道內門、廚房分出後廚）、貼合的門板（縫 2 cm、模型＝碰撞）、
    /// 夾層「被折疊的住宅記憶」與中庭「被拆解又重組的都市內庭」（佔位撤除、功能保留、廣場淨空可走位）、世界內圖文正式化與近距離巡覽、
    /// R3 帶門走到夾層的劇情路線、1.9↔1.8 對照。自動化只驗結構與可走性；美感與臨場感要真人在 Play 裡走過。
    /// </summary>
    public sealed class Case01Phase19Tests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
        }

        private Case01ImmersionQA NewQa19()
        {
            var go = new GameObject("Case01Phase19Test");
            roots.Add(go);
            var s = go.AddComponent<Case01ImmersionQA>();
            s.Setup(false, null, 1, true, Case01QaMode.CoreV03, true);
            s.Frame(Dt);
            return s;
        }

        private static void Step(Case01ImmersionQA s, Case01Input input)
        {
            s.House.Tick(Dt, input);
            s.Frame(Dt);
        }

        private static void Run(Case01ImmersionQA s, float sec)
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

        private static bool? TryWalk(Case01ImmersionQA s, float x, float z, float max = 15f)
        {
            var qa = s.House;
            var stuckAt = qa.Player.transform.position;
            float stuckTime = 0f;
            for (float t = 0; t < max; t += Dt)
            {
                if (qa.Mode != Case01Mode.Exploring) return false;
                var p = qa.Player.transform.position;
                if (new Vector2(x - p.x, z - p.z).magnitude < 0.25f) return true;
                Step(s, Case01Input.From(new ExploreInput { Move = new Vector2(0f, 1f), Look = LookDelta(qa.Player, new Vector3(x, qa.Player.EyePosition.y, z), true) }));
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

        private static void WalkTo(Case01ImmersionQA s, float x, float z)
        {
            Assert.IsTrue(TryWalk(s, x, z) == true, "走不到 (" + x + ", " + z + ")，停在 " + s.House.Player.transform.position + "，房間 " + s.House.CurrentRoom);
        }

        private static void Path(Case01ImmersionQA s, params float[] xz)
        {
            for (int i = 0; i + 1 < xz.Length; i += 2) WalkTo(s, xz[i], xz[i + 1]);
        }

        private static void Aim(Case01ImmersionQA s, Vector3 target)
        {
            for (int i = 0; i < 2; i++) Step(s, Case01Input.From(new ExploreInput { Look = LookDelta(s.House.Player, target, false) }));
        }

        private static void Interact(Case01ImmersionQA s, string id)
        {
            var qa = s.House;
            var it = qa.Layout.Interactables[id];
            Aim(s, it.transform.TransformPoint(it.GetComponent<BoxCollider>().center));
            Assert.IsNotNull(qa.CurrentTarget, "準星對到 " + id + "（" + qa.Player.transform.position + "）");
            Assert.AreEqual(id, qa.CurrentTarget.Id);
            if (qa.Hud.PanelVisible && qa.PanelSourceId == id) Step(s, Case01Input.From(new ExploreInput { Interact = true }));
            Step(s, Case01Input.From(new ExploreInput { Interact = true }));
        }

        private static void CloseText(Case01ImmersionQA s)
        {
            if (s.House.Hud.PanelVisible) Step(s, Case01Input.From(new ExploreInput { Interact = true }));
        }

        private static void OpenDoor(Case01ImmersionQA s, string id, int side)
        {
            var d = s.World.Door(id);
            var stand = d.Center + d.Normal * (side * (d.OnlySide == side ? 1.5f : 1.05f));
            WalkTo(s, stand.x, stand.z);
            if (d.IsOpenEnough) return;
            Aim(s, d.Hotspot.transform.position);
            Assert.IsNotNull(s.House.CurrentTarget, "瞄準門 " + id);
            Assert.AreEqual(id, s.House.CurrentTarget.Id);
            Step(s, Case01Input.From(new ExploreInput { Interact = true }));
            Run(s, 1.2f);
            Assert.IsTrue(d.IsOpenEnough, id + " 打開（" + d.State + "，" + d.Angle + "°）");
        }

        private static bool Blocked(Case01ImmersionQA s, Vector3 feet, float radius = 0.3f)
        {
            var cc = s.House.Player.Body;
            cc.enabled = false;
            bool hit = Physics.CheckCapsule(feet + Vector3.up * 0.4f, feet + Vector3.up * 1.45f, radius, Case01SpatialMap.SightMask, QueryTriggerInteraction.Ignore);
            cc.enabled = true;
            return hit;
        }

        // ---------------- 格局與門 ----------------

        [Test]
        public void R3_FrontDoorOpensStraightIntoTheHall_NoSecondDoor_KitchenHasAScullery()
        {
            var s = NewQa19();
            Assert.AreEqual(Case01Floorplan.R3, s.House.Layout.Floorplan);
            Assert.IsNull(s.World.Door("door_vestibule"), "前門進來不再有第二道內門");
            Assert.AreEqual(6, s.World.Doors.Count, "客廳、客廳↔餐廳、走廊↔餐廳、廚房、書房、臥室");
            Physics.SyncTransforms();
            for (float z = 0.8f; z <= 2.6f; z += 0.3f)
                Assert.IsFalse(Blocked(s, new Vector3(Case01HouseBuilder.R17.FrontDoorX, 0.05f, z)), "前廊到走廊之間沒有牆或門（z " + z + "）");
            // 後廚：x 7.0 的隔牆擋人，只在拱口（z 12.8～14.0）可以通過。
            Assert.IsTrue(Blocked(s, new Vector3(Case01HouseBuilder.R3SculleryX, 0.05f, 16.0f), 0.2f), "後廚隔牆");
            Assert.IsFalse(Blocked(s, new Vector3(Case01HouseBuilder.R3SculleryX, 0.05f, Case01HouseBuilder.R3SculleryArchZ), 0.2f), "後廚拱口");
            // 實際走：前門 → 走廊（不用開門）。
            Path(s, 7.1f, 2.2f, 7.6f, 4.6f);
            Assert.AreEqual(Case01Room.Entrance, s.House.CurrentRoom);
        }

        [Test]
        public void Doors_AreSnug_ModelMatchesCollider_OpenWithoutClipping_FromBothSides()
        {
            var s = NewQa19();
            foreach (var d in s.World.Doors)
            {
                var leaf = d.Leaf.transform.localScale;
                Assert.AreEqual(d.Width - 0.04f, leaf.x, 0.005f, d.Id + "：門板寬＝門洞寬 − 兩側各 2 cm");
                Assert.AreEqual(2.18f, leaf.y, 0.005f, d.Id + "：門板高 2.18 m（門洞 2.2 m）");
                // 嵌板門模型的外框與碰撞方塊一致（誤差 3 cm 內）：看得到的門＝擋人的門。
                d.SetAngle(0f, null);
                Physics.SyncTransforms();
                var col = d.Leaf.bounds;
                var rends = d.Leaf.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.transform != d.Leaf.transform).ToArray();
                Assert.Greater(rends.Length, 0, d.Id + "：門板換成嵌板門模型");
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                Assert.AreEqual(col.size.y, b.size.y, 0.03f, d.Id + "：模型高＝碰撞高");
                Assert.AreEqual(Mathf.Max(col.size.x, col.size.z), Mathf.Max(b.size.x, b.size.z), 0.03f, d.Id + "：模型寬＝碰撞寬");
                Assert.AreEqual(col.min.y, b.min.y, 0.03f, d.Id + "：模型底＝碰撞底（不浮空、不埋地）");
                Assert.LessOrEqual(d.OpenAngle, 88.01f, d.Id + "：開到 88° 停");
                Physics.SyncTransforms();
                Assert.IsTrue(Blocked(s, d.Center + Vector3.up * 0.05f), d.Id + "：關著擋人");
                foreach (var side in new[] { 1, -1 })
                {
                    if (d.OnlySide != 0 && side == 1) continue;
                    var stand = d.Center + d.Normal * (side * (d.OnlySide == side ? 1.5f : 1.1f)) + Vector3.up * 0.05f;
                    s.House.Player.Teleport(stand, 0f);
                    Physics.SyncTransforms();
                    d.Toggle(stand, s.House.Player);
                    for (int i = 0; i < 40; i++) d.Tick(Dt, s.House.Player);
                    Physics.SyncTransforms();
                    Assert.IsTrue(d.IsOpenEnough, d.Id + " 從 " + side + " 側打開（" + d.Angle + "°）");
                    Assert.IsTrue(d.Free(d.Angle, null), d.Id + "：開著時門板不和任何東西重疊");
                    Assert.IsFalse(Blocked(s, d.Center + Vector3.up * 0.05f, 0.28f), d.Id + "：開著時門洞可以通過");
                    d.Toggle(stand, s.House.Player);
                    for (int i = 0; i < 40; i++) d.Tick(Dt, s.House.Player);
                    Assert.AreEqual(Case01DoorState.Closed, d.State, d.Id + " 關上");
                }
            }
        }

        // ---------------- 夾層 ----------------

        [Test]
        public void Mezzanine_IsAFoldedHouseMemory_PlaceholdersGone_FunctionsKept()
        {
            var s = NewQa19();
            var L = s.House.Layout;
            var art = s.World.P19;
            Assert.IsNotNull(art);
            var mezz = L.Root.Find("Mezzanine");
            foreach (var r in mezz.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "BrokenLine"))
                Assert.IsFalse(r.enabled, "地上斷線的佔位不再渲染");
            var c = Case01HouseBuilder.MezzCenter;
            foreach (var ov in L.Overlaps)
            {
                Assert.IsTrue(ov.parent.name.StartsWith("MemoryDoorAnchor_"), "重疊的門框改成嵌在牆上的住宅房門");
                float toWall = 5.925f - Mathf.Max(Mathf.Abs(ov.position.x - c.x), Mathf.Abs(ov.position.z - c.z));
                Assert.Less(toWall, 0.2f, "記憶門貼著牆（不是散落在房間中央）");
                Assert.IsTrue(ov.GetComponentsInChildren<Transform>(true).Any(t => t.name.Contains("PanelDoor") || t.name.Contains("Door")), "門框裡有嵌板門");
            }
            Assert.GreaterOrEqual(art.MezzFeatures.Count, 8, "記憶門、折疊樓梯、錯置的窗");
            Assert.IsTrue(mezz.GetComponentsInChildren<Transform>(true).Any(t => t.name == "FoldedStair"));
            foreach (var kv in L.MezzLabels) Assert.IsFalse(kv.Value.enabled, "浮空的符號字改成黃銅門牌");
            Assert.AreEqual(3, art.Graphics.Count(g => g.Name.StartsWith("門牌")), "△、○、□17 三塊門牌");
            foreach (var id in new[] { Case01Ids.Core, Case01Ids.MezzTriangleMark, Case01Ids.MezzCircleMark, Case01Ids.Wall17 })
                Assert.IsTrue(L.Interactables.ContainsKey(id), "夾層功能熱區保留：" + id);
            foreach (var t in art.MezzRoot.GetComponentsInChildren<Transform>(true))
                Assert.AreEqual(Case01HouseBuilder.AnomalyLayer, t.gameObject.layer, "夾層新物件在異常圖層：" + t.name);
            // 從入口走到核前（折疊樓梯、門、窗不擋主要動線）。
            s.Press(15);
            Run(s, 0.1f);
            Assert.AreEqual(Case01Room.Mezzanine, s.House.CurrentRoom);
            Path(s, c.x, c.z - 2.2f, c.x - 2.0f, c.z + 0.4f, c.x + 2.0f, c.z + 0.4f);
            // 核校正後：歪斜的門在牆面內轉正。
            s.House.QaView(Case01Flow.ForHouseStage(3), new Case01Pose(c + new Vector3(0f, 0.05f, -2.2f), 0f), 0f);
            Run(s, 4f);
            foreach (var ov in s.House.Layout.Overlaps)
                Assert.Less(Quaternion.Angle(ov.localRotation, Quaternion.identity), 2f, "核穩定：記憶門轉正");
        }

        // ---------------- 中庭 ----------------

        [Test]
        public void Courtyard_HasStructure_PlazaIsClearForCombat_PlaceholdersGone_EncounterKept()
        {
            var s = NewQa19();
            var L = s.House.Layout;
            var art = s.World.P19;
            var court = L.Root.Find("Courtyard");
            foreach (Transform t in court)
                if (t.name == "FloatingRoomFrame" || t.name == "WallFragment" || t.name == "Crack")
                    Assert.IsFalse(t.gameObject.activeInHierarchy, "佔位撤除：" + t.name);
            Assert.GreaterOrEqual(art.RemovedPlaceholders, 20);
            Assert.IsNotNull(L.EncounterAnchor, "遭遇點保留");
            Assert.GreaterOrEqual(art.CourtCover.Count, 8, "拱廊磚柱與矮牆可當掩體");
            foreach (var cov in art.CourtCover) Assert.IsNotNull(cov.GetComponent<Collider>(), "掩體有碰撞：" + cov.name);
            foreach (var name in new[] { "ArcadePier", "ArcadeLintel", "Parapet", "PlazaSeam" })
                Assert.IsTrue(art.CourtRoot.GetComponentsInChildren<Transform>(true).Any(t => t.name == name), "中庭結構：" + name);
            Assert.GreaterOrEqual(art.CourtRoot.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("TerraceFront")), 6, "掛在牆上／側翻的街屋立面");
            // 所有新造型都在中庭的牆內（不穿出去、不浮在廣場上空擋視線）。
            s.Press(16);
            Run(s, 0.1f);
            art = s.World.P19;
            L = s.House.Layout;
            foreach (var r in art.CourtRoot.GetComponentsInChildren<Renderer>())
            {
                var b = r.bounds;
                Assert.IsTrue(b.min.x > 25.8f && b.max.x < 54.2f && b.min.z > 23.8f && b.max.z < 58.2f && b.max.y < 7.6f, "中庭造型在牆內：" + r.name + " " + b);
            }
            // 廣場淨空：遭遇點半徑 6 m 內沒有任何碰撞（第一人稱戰鬥的移動空間）。
            Physics.SyncTransforms();
            var a = L.EncounterAnchor.position;
            for (float x = -6f; x <= 6f; x += 1f)
            for (float z = -6f; z <= 6f; z += 1f)
            {
                if (x * x + z * z > 36f) continue;
                Assert.IsFalse(Blocked(s, new Vector3(a.x + x, 0.05f, a.z + z)), "廣場淨空 (" + (a.x + x) + ", " + (a.z + z) + ")");
            }
            Assert.AreEqual(Case01Room.Courtyard, s.House.CurrentRoom);
            // 從裂口走到廣場中央、再走到拱廊前。
            Path(s, 40f, 33f, a.x, a.z - 2f, 36f, 52.5f);
        }

        // ---------------- 圖文 ----------------

        [Test]
        public void Graphics_AreFormal_ReadableAtCloseRange_ScaledToTheirCarriers()
        {
            foreach (var n in new[] { "photo_family", "photo_family_faded", "photo_beach", "calendar", "map", "route", "chalk_triangle", "sym_triangle", "sym_circle", "sym_square17", "paper_ruled", "enamel", "paper" })
            {
                var t = Case01Graphics.Tex(n);
                Assert.IsNotNull(t, n);
                Assert.GreaterOrEqual(Mathf.Min(t.width, t.height), 128, n + "：解析度夠近看");
            }
            var s = NewQa19();
            var art = s.World.P19;
            Assert.GreaterOrEqual(art.Graphics.Count, 17, "相片、日曆、紙條、清單、地圖、手記、路線、明信片、信、標籤、粉筆記號、門牌、指示牌");
            foreach (var g in art.Graphics)
            {
                Assert.IsNotNull(g.T, g.Name);
                Assert.IsTrue(g.Size.x > 0.05f && g.Size.x < 0.9f && g.Size.y > 0.04f && g.Size.y < 0.9f, g.Name + "：尺寸合理（" + g.Size + "）");
                if (g.Glyph > 0f) Assert.GreaterOrEqual(g.Glyph, 0.012f, g.Name + "：字高 ≥ 1.2 cm（1 m 內讀得到）");
                foreach (var w in g.T.GetComponentsInChildren<Canvas>(true))
                {
                    var rt = (RectTransform)w.transform;
                    float ww = rt.rect.width * rt.lossyScale.x, hh = rt.rect.height * rt.lossyScale.y;
                    Assert.LessOrEqual(ww, g.T.lossyScale.x + 1e-3f, g.Name + "：字沒有超出載體（寬）");
                    Assert.LessOrEqual(hh, g.T.lossyScale.y + 1e-3f, g.Name + "：字沒有超出載體（高）");
                }
            }
            Assert.IsFalse(s.House.Layout.StudyMarkLabel.enabled, "書房門框的記號改成粉筆畫");
            Assert.IsFalse(s.House.Layout.FrontDoorLabel.enabled, "前門上的 QA 文字隱藏");
            // 核穩定後（H3）：合照裡的第二個人淡掉（正式相片跟著原本的事件換）。
            var photo = art.Graphics.First(g => g.Name.Contains("合照")).T.GetComponent<Renderer>();
            var before = photo.sharedMaterial.mainTexture;
            s.House.QaView(Case01Flow.ForHouseStage(3), s.House.Layout.Spawn, 0f);
            Run(s, 0.1f);
            var photo1 = s.World.P19.Graphics.First(g => g.Name.Contains("合照")).T.GetComponent<Renderer>();
            Assert.AreEqual("photo_family_faded", photo1.sharedMaterial.mainTexture.name.Replace("Case01Gfx_", ""), "H3：合照第二個人淡掉");
            Assert.AreNotEqual(before.name, photo1.sharedMaterial.mainTexture.name);
        }

        [Test]
        public void GraphicsTour_StandsAtCloseRange_ItemIsInViewAndUnoccluded()
        {
            var s = NewQa19();
            int n = s.World.P19.Graphics.Count;
            int good = 0;
            var fails = new List<string>();
            for (int i = 0; i < n; i++)
            {
                s.Press(17);
                s.Frame(Dt);
                var g = s.World.P19.Graphics[s.TourIndex];
                var eye = s.House.Player.EyePosition;
                float dist = Vector3.Distance(eye, g.T.position);
                var cam = s.House.ExploreCamera;
                cam.transform.position = eye;
                cam.transform.rotation = s.House.Player.ViewRotation;
                var vp = cam.WorldToViewportPoint(g.T.position);
                bool inView = vp.z > 0f && vp.x > 0.2f && vp.x < 0.8f && vp.y > 0.2f && vp.y < 0.8f;
                var to = g.T.position - eye;
                bool clear = !Physics.Raycast(eye, to.normalized, to.magnitude - 0.06f, Case01SpatialMap.SightMask, QueryTriggerInteraction.Ignore);
                if (dist >= 0.4f && dist <= 2.6f && inView && clear) good++;
                else fails.Add(g.Name + " d=" + dist.ToString("0.00") + " vp=" + vp + " clear=" + clear);
            }
            Assert.AreEqual(n, good, "巡覽每一件都在 0.4～2.6 m、畫面中央、沒有被擋住：" + string.Join("；", fails));
        }

        // ---------------- 路線與對照 ----------------

        [Test]
        public void StoryRoute_R3_FrontDoorToTheMezzanine()
        {
            var s = NewQa19();
            var qa = s.House;
            Path(s, 7.1f, 2.2f, 7.6f, 4.6f, 7.6f, 8.0f);
            OpenDoor(s, "door_hall_dining", -1);
            Path(s, 5.6f, 9.3f, 4.6f, 8.1f);
            Assert.AreEqual(Case01Room.Dining, qa.CurrentRoom);
            Interact(s, Case01Ids.Table);
            CloseText(s);
            Path(s, 5.6f, 9.3f, 7.2f, 9.3f, 7.6f, 4.6f, 7.6f, 3.6f);
            OpenDoor(s, "door_living", -1);
            Path(s, 5.6f, 3.6f, 5.0f, 1.2f, 1.6f, 1.2f);
            Interact(s, Case01Ids.Photo);
            CloseText(s);
            Path(s, 1.4f, 1.9f, 1.4f, 4.7f);
            OpenDoor(s, "door_living_dining", -1);
            Path(s, 1.25f, 6.4f);
            Run(s, 0.1f);
            Assert.IsTrue(qa.Session.Has(Case01Flags.TableRewritten), "餐桌二→一");
            Path(s, 5.3f, 7.3f, 5.6f, 9.3f, 7.2f, 9.3f, 7.6f, 11.2f, 9.45f, 11.2f, 9.45f, 10.9f, 9.45f, 7.0f, 9.45f, 6.2f, 8.4f, 6.2f, 7.6f, 7.0f, 7.6f, 10.4f);
            Assert.AreEqual(Case01Room.Landing, qa.CurrentRoom, "上到二樓");
            OpenDoor(s, "door_study", -1);
            Path(s, 5.8f, Case01HouseBuilder.R17.StudyDoorZ, 5.3f, 10.0f, 3.6f, 10.1f);
            Interact(s, Case01Ids.Map);
            Interact(s, Case01Ids.EllieNotes);
            WalkTo(s, 5.2f, 9.5f);
            Interact(s, Case01Ids.OldRecord);
            CloseText(s);
            Path(s, 5.3f, 10.0f, 5.8f, Case01HouseBuilder.R17.StudyDoorZ, 7.6f, Case01HouseBuilder.R17.StudyDoorZ, 7.7f, 8.0f);
            Run(s, 0.3f);
            Assert.IsTrue(qa.Flow.WrongConnectionActive, "離開書房：錯接");
            Path(s, 7.6f, Case01HouseBuilder.R17.StudyDoorZ);
            Assert.IsTrue(TryWalk(s, 5.6f, Case01HouseBuilder.R17.StudyDoorZ, 6f) == false, "書房門錯接");
            for (float t = 0; t < 5f && qa.Mode != Case01Mode.Exploring; t += Dt) Step(s, Case01Input.None);
            Assert.AreEqual(Case01Room.Dining, qa.CurrentRoom, "書房門 → 餐廳");
            Path(s, 1.25f, 6.4f, 1.25f, 5.0f, 1.4f, 4.6f, 1.4f, 1.6f, 5.0f, 1.2f, 5.6f, 3.6f, 7.2f, 3.6f, 7.6f, 4.6f, 7.6f, 11.2f, 9.45f, 11.2f, 9.45f, 10.9f, 9.45f, 7.0f, 9.45f, 6.2f, 8.4f, 6.2f, 7.6f, 7.0f, 7.3f, 10.0f);
            Interact(s, Case01Ids.StudyMark);
            Step(s, new Case01Input { Choice = 1 });
            Assert.IsTrue(qa.Flow.StairRouteRestored, "書房門框覆寫");
            CloseText(s);
            Run(s, 0.1f);
            Assert.IsTrue(s.World.P19.Graphics.Any(g => g.Name.Contains("粉筆")), "粉筆記號");
            Assert.IsTrue(s.House.Layout.Root.GetComponentsInChildren<Transform>(true).First(t => t.name == "ChalkWord").gameObject.activeSelf, "覆寫後粉筆多寫了「歸位」");
            Path(s, 7.4f, 4.7f, 7.4f, 3.7f, Case01HouseBuilder.R17.ThirdDoorX, 1.8f);
            Assert.IsTrue(TryWalk(s, Case01HouseBuilder.R17.ThirdDoorX, -0.6f) == false, "第三扇門");
            for (float t = 0; t < 5f && qa.Mode != Case01Mode.Exploring; t += Dt) Step(s, Case01Input.None);
            Assert.AreEqual(Case01Room.Mezzanine, qa.CurrentRoom, "進入夾層");
        }

        [Test]
        public void F2_ComparesPhase19WithPhase18_AndPhase18QaIsUnchanged()
        {
            var s = NewQa19();
            Assert.IsNotNull(s.World.P19);
            s.Press(2);
            s.Frame(Dt);
            Assert.AreEqual(Case01Floorplan.R2, s.House.Layout.Floorplan, "F2：Phase 1.8 R2");
            Assert.IsNull(s.World.P19, "1.8 沒有 1.9 的重構");
            Assert.AreEqual(7, s.World.Doors.Count, "1.8：有前廊內門");
            Assert.AreEqual(0.8f, s.World.Door("door_living").Leaf.transform.localScale.x, 0.005f, "1.8 的門板保持原樣（對照）");
            s.Press(2);
            s.Frame(Dt);
            Assert.AreEqual(Case01Floorplan.R3, s.House.Layout.Floorplan);
            Assert.IsNotNull(s.World.P19);
            // Phase 1.8 QA 本身（沒有 phase19）不受影響。
            var go = new GameObject("Phase18");
            roots.Add(go);
            var q18 = go.AddComponent<Case01ImmersionQA>();
            q18.Setup(false, null, 1, true);
            Assert.IsFalse(q18.Phase19);
            Assert.AreEqual(Case01Floorplan.R2, q18.House.Layout.Floorplan);
            Assert.IsNull(q18.World.P19);
            Assert.AreEqual(7, q18.World.Doors.Count);
        }
    }
}
