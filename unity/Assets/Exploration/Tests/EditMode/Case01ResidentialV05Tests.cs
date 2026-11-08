using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Battle.Core;
using Shepherd.Battle.UI;
using Shepherd.Exploration.Case01;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// v0.5 Phase 1：住宅生活邏輯。一樓方案 A／B（獨立 QA，不取代原版）、房間用途與主家具、合照移到客廳、
    /// 透明窗與窗外街景、書房層板書櫃、艾琳臥室、儲藏室劇內文案；用真正的移動與碰撞走完 A、B 兩種格局的完整流程。
    /// </summary>
    public sealed class Case01ResidentialV05Tests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();
        private float walked;

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
        }

        private Case01HouseQA NewQa(Case01Floorplan plan, CombatLabScenario scenario = null, Case01Checkpoint? cp = null, Case01QaMode? mode = null)
        {
            var go = new GameObject("Case01ResidentialTest");
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

        // ---------------- 腳本化輸入 ----------------

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

        private bool? TryWalk(Case01HouseQA qa, float x, float z, float max = 12f)
        {
            var stuckAt = qa.Player.transform.position;
            float stuckTime = 0f;
            for (float t = 0; t < max; t += Dt)
            {
                if (qa.Mode != Case01Mode.Exploring) return false;
                var p = qa.Player.transform.position;
                if (new Vector2(x - p.x, z - p.z).magnitude < 0.25f) return true;
                Tick(qa, Case01Input.From(new ExploreInput { Move = new Vector2(0f, 1f), Look = LookDelta(qa.Player, new Vector3(x, qa.Player.EyePosition.y, z), true) }));
                var np = qa.Player.transform.position;
                if (qa.Mode == Case01Mode.Exploring) walked += new Vector2(np.x - p.x, np.z - p.z).magnitude;
                if ((np - stuckAt).magnitude > 0.05f)
                {
                    stuckAt = np;
                    stuckTime = 0f;
                }
                else if ((stuckTime += Dt) > 1.5f) return null;
            }
            return null;
        }

        private void WalkTo(Case01HouseQA qa, float x, float z)
        {
            var r = TryWalk(qa, x, z);
            Assert.IsTrue(r == true, "走不到 (" + x + ", " + z + ")，停在 " + qa.Player.transform.position + "，房間 " + qa.CurrentRoom);
        }

        private void WalkThrough(Case01HouseQA qa, float x, float z, Case01Room expect)
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

        // ---------------- 兩種格局的路線 ----------------

        private sealed class Routes
        {
            public System.Action<Case01HouseQA> EntranceToDining, DiningToEntrance, EntranceToLanding, LandingToEntrance, ToPhoto, PhotoToEntrance, DiningToLanding;
        }

        private Routes A()
        {
            return new Routes
            {
                // 玄關 → 左手邊客廳（沙發東側走道）→ 2.4 m 寬開口 → 餐廳。
                EntranceToDining = qa =>
                {
                    WalkTo(qa, 6.2f, 1.4f);
                    WalkTo(qa, 4.7f, 1.4f);
                    WalkTo(qa, 4.7f, 4.7f);
                    WalkTo(qa, 3.4f, 5.05f);
                    WalkTo(qa, 3.4f, 6.0f);
                    Assert.AreEqual(Case01Room.Dining, qa.CurrentRoom, "經客廳走進餐廳");
                },
                DiningToEntrance = qa =>
                {
                    WalkTo(qa, 3.4f, 6.0f);
                    WalkTo(qa, 3.4f, 5.05f);
                    WalkTo(qa, 4.7f, 4.7f);
                    WalkTo(qa, 4.7f, 1.4f);
                    WalkTo(qa, 6.2f, 1.4f);
                    WalkTo(qa, 6.8f, 2.2f);
                    Assert.AreEqual(Case01Room.Entrance, qa.CurrentRoom);
                },
                // 玄關北牆右側的門 → 廚房 → 東南角起步，往南上樓到第三扇門旁的平台。
                EntranceToLanding = qa =>
                {
                    WalkTo(qa, 7.6f, 3.0f);
                    WalkTo(qa, 7.85f, 4.0f);
                    WalkTo(qa, 7.85f, 5.3f);
                    Assert.AreEqual(Case01Room.Kitchen, qa.CurrentRoom, "玄關可以直接進廚房");
                    WalkTo(qa, 9.35f, 5.4f);
                    WalkTo(qa, 9.35f, 4.75f);
                    WalkTo(qa, 9.35f, 1.3f);
                    Assert.Greater(qa.Player.transform.position.y, 2.5f, "從廚房側往南上行");
                    WalkTo(qa, 9.4f, 0.5f);
                    WalkTo(qa, 8.2f, 0.5f);
                    WalkTo(qa, 6.95f, 1.6f);
                    WalkTo(qa, 6.95f, 3.6f);
                    Assert.AreEqual(Case01Room.Landing, qa.CurrentRoom, "到二樓平台");
                },
                LandingToEntrance = qa =>
                {
                    WalkTo(qa, 8.2f, 0.5f);
                    WalkTo(qa, 9.4f, 0.5f);
                    WalkTo(qa, 9.35f, 5.2f);
                    Assert.Less(qa.Player.transform.position.y, 0.3f, "往北下到廚房");
                    WalkTo(qa, 7.85f, 5.3f);
                    WalkTo(qa, 7.85f, 3.9f);
                    WalkTo(qa, 7.3f, 2.6f);
                    Assert.AreEqual(Case01Room.Entrance, qa.CurrentRoom);
                },
                ToPhoto = qa =>
                {
                    WalkTo(qa, 6.2f, 1.4f);
                    WalkTo(qa, 4.7f, 1.4f);
                    WalkTo(qa, 1.6f, 1.5f);
                    Assert.AreEqual(Case01Room.Living, qa.CurrentRoom);
                },
                PhotoToEntrance = qa =>
                {
                    WalkTo(qa, 4.7f, 1.4f);
                    WalkTo(qa, 6.2f, 1.4f);
                    WalkTo(qa, 6.8f, 2.2f);
                },
                // 錯接到餐廳後：經廚房回樓梯（不必繞回玄關）。
                DiningToLanding = qa =>
                {
                    WalkTo(qa, 3.4f, 6.4f);
                    WalkTo(qa, 4.6f, 7.6f);
                    WalkTo(qa, 6.2f, 7.6f);
                    Assert.AreEqual(Case01Room.Kitchen, qa.CurrentRoom, "餐廳緊鄰廚房");
                    WalkTo(qa, 8.0f, 6.0f);
                    WalkTo(qa, 9.35f, 5.4f);
                    WalkTo(qa, 9.35f, 4.75f);
                    WalkTo(qa, 9.35f, 1.3f);
                    WalkTo(qa, 9.4f, 0.5f);
                    WalkTo(qa, 8.2f, 0.5f);
                    WalkTo(qa, 6.95f, 1.6f);
                    WalkTo(qa, 6.95f, 3.6f);
                    Assert.AreEqual(Case01Room.Landing, qa.CurrentRoom);
                },
            };
        }

        private Routes B()
        {
            Routes r = null;
            r = new Routes
            {
                EntranceToDining = qa =>
                {
                    WalkTo(qa, 6.5f, 4.2f);
                    WalkTo(qa, 6.5f, 5.9f);
                    Assert.AreEqual(Case01Room.Dining, qa.CurrentRoom);
                },
                DiningToEntrance = qa =>
                {
                    WalkTo(qa, 6.5f, 5.9f);
                    WalkTo(qa, 6.5f, 4.2f);
                    WalkTo(qa, 6.9f, 2.6f);
                    Assert.AreEqual(Case01Room.Entrance, qa.CurrentRoom);
                },
                // 原版方向：南端起步、往北上行；起步旁是矮鞋櫃，樓梯下是掃具櫃。
                EntranceToLanding = qa =>
                {
                    WalkTo(qa, 7.6f, 0.85f);
                    WalkTo(qa, 9.4f, 0.8f);
                    WalkTo(qa, 9.4f, 4.4f);
                    Assert.Greater(qa.Player.transform.position.y, 2.5f, "往北上行");
                    WalkTo(qa, 9.4f, 4.75f);
                    WalkTo(qa, 8.2f, 4.5f);
                    WalkTo(qa, 6.95f, 3.6f);
                    Assert.AreEqual(Case01Room.Landing, qa.CurrentRoom);
                },
                LandingToEntrance = qa =>
                {
                    WalkTo(qa, 8.2f, 4.5f);
                    WalkTo(qa, 9.4f, 4.75f);
                    WalkTo(qa, 9.4f, 0.75f);
                    Assert.Less(qa.Player.transform.position.y, 0.3f);
                    WalkTo(qa, 7.6f, 0.85f);
                    WalkTo(qa, 6.9f, 2.4f);
                    Assert.AreEqual(Case01Room.Entrance, qa.CurrentRoom);
                },
                ToPhoto = qa =>
                {
                    WalkTo(qa, 6.0f, 2.5f);
                    WalkTo(qa, 4.2f, 2.5f);
                    WalkTo(qa, 2.5f, 3.2f);
                    WalkTo(qa, 2.5f, 5.6f);
                    WalkTo(qa, 1.45f, 6.35f);
                    Assert.AreEqual(Case01Room.Living, qa.CurrentRoom);
                },
                PhotoToEntrance = qa =>
                {
                    WalkTo(qa, 2.5f, 5.6f);
                    WalkTo(qa, 2.5f, 3.2f);
                    WalkTo(qa, 4.2f, 2.5f);
                    WalkTo(qa, 6.4f, 2.5f);
                },
            };
            r.DiningToLanding = qa =>
            {
                r.DiningToEntrance(qa);
                r.EntranceToLanding(qa);
            };
            return r;
        }

        private void LandingToStudy(Case01HouseQA qa)
        {
            WalkTo(qa, 6.95f, 4.3f);
            WalkTo(qa, 6.95f, 5.8f);
            Assert.AreEqual(Case01Room.Study, qa.CurrentRoom);
        }

        private void StudyToLanding(Case01HouseQA qa)
        {
            WalkTo(qa, 6.95f, 6.0f);
            WalkTo(qa, 6.95f, 4.2f);
            Assert.AreEqual(Case01Room.Landing, qa.CurrentRoom);
        }

        private void LandingToMezzanine(Case01HouseQA qa)
        {
            WalkTo(qa, 6.95f, 2.0f);
            WalkThrough(qa, 6.95f, -0.6f, Case01Room.Mezzanine);
        }

        private void MezzTo17(Case01HouseQA qa)
        {
            WalkTo(qa, 38.5f, 0f);
            WalkTo(qa, 38.5f, 4.5f);
            WalkTo(qa, 40f, 4.6f);
            WalkThrough(qa, 40f, 6.7f, Case01Room.Entrance);
        }

        // ---------------- 幾何與房間用途 ----------------

        private static Transform Find(Case01HouseQA qa, string name)
        {
            return qa.Layout.Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        }

        [TestCase(Case01Floorplan.V05A)]
        [TestCase(Case01Floorplan.V05B)]
        public void RoomsHaveRecognisableFunction_AndEvidenceLivesInTheRightRoom(Case01Floorplan plan)
        {
            var qa = NewQa(plan);
            var L = qa.Layout;
            Assert.AreEqual(plan, L.Floorplan);
            var expect = new Dictionary<string, Case01Room>
            {
                { Case01Ids.FrontDoor, Case01Room.Entrance }, { Case01Ids.Table, Case01Room.Dining }, { Case01Ids.ShoppingList, Case01Room.Dining },
                { Case01Ids.Photo, Case01Room.Living }, { Case01Ids.Calendar, Case01Room.Living }, { Case01Ids.LivingBookNote, Case01Room.Living },
                { Case01Ids.FriendCard, Case01Room.Living }, { Case01Ids.SundayNote, Case01Room.Kitchen }, { Case01Ids.Tea, Case01Room.Kitchen },
                { Case01Ids.TeaTin, Case01Room.Kitchen },
            };
            foreach (var kv in expect)
            {
                Assert.IsTrue(L.Interactables.ContainsKey(kv.Key), kv.Key);
                var p = L.Interactables[kv.Key].transform.position;
                Assert.AreEqual(kv.Value, L.RoomAt(new Vector3(p.x, 0.05f, p.z)), kv.Key + " 在 " + kv.Value);
            }
            foreach (var name in new[] { "Sink", "Stove", "Fridge", "KitchenRun", "UpperCabinet", "Sofa", "Fireplace", "TvCorner", "LivingBookcase", "CoffeeTable", "Sideboard",
                         "ChairWest", "ChairEast", "ShoeBench", "CoatHooks", "ShoeCabinet", "HallLandscape", "UnderStairCupboard" })
                Assert.IsNotNull(Find(qa, name), "主家具：" + name);
            Assert.IsNull(Find(qa, "KitchenTable"), "廚房沒有孤立的單人圓桌");
            Assert.IsNull(Find(qa, "Stool"));
            // 合照是壁爐架上的生活照，不再正對餐桌。
            var photo = L.Interactables[Case01Ids.Photo].transform.position;
            Assert.Greater(Vector3.Distance(photo, L.TablePoint), 3f);
            StringAssert.Contains("客廳", L.PhotoPlace);
            Assert.AreEqual(7, L.Portals.Count, "通路不變");
            foreach (var room in new[] { Case01Room.Entrance, Case01Room.Dining, Case01Room.Study, Case01Room.Landing, Case01Room.Mezzanine, Case01Room.Courtyard })
                Assert.AreEqual(room, L.RoomAt(L.Arrivals[room].Position), "到達點：" + room);
            Assert.IsNotNull(L.Decor.TeaTinLid);
            Assert.AreEqual(2, L.Decor.LivingMissingBooks.Count, "H1 客廳缺書");
            Assert.IsNotNull(L.Decor.TraySecondCup);
            Assert.IsNotNull(L.Decor.DiningRunner);
        }

        [Test]
        public void LayoutA_StairFromKitchenSide_NoGapByTheFrontDoor()
        {
            var qa = NewQa(Case01Floorplan.V05A);
            var L = qa.Layout;
            Assert.AreEqual(Case01HouseBuilder.StairZ1, L.StairBottomZ, "北端（廚房側）起步");
            Assert.AreEqual(Case01HouseBuilder.StairZ0, L.StairTopZ, "往南上到第三扇門旁的平台");
            Assert.That(L.StairAngle, Is.InRange(36f, 42f), "比 v0.4 B（45°）緩");
            Assert.IsTrue(Physics.Linecast(new Vector3(8.2f, 1f, 1.5f), new Vector3(9.4f, 1f, 1.5f)), "玄關與樓梯之間是牆（沒有畸零縫）");
            Assert.IsFalse(Physics.CheckCapsule(new Vector3(9.35f, 0.45f, 5.2f), new Vector3(9.35f, 1.45f, 5.2f), 0.34f), "廚房側的起步空間淨空");
            Assert.IsFalse(Physics.Linecast(new Vector3(7.85f, 1f, 4.0f), new Vector3(7.85f, 1f, 4.9f)), "玄關→廚房的門");
            Assert.IsFalse(Physics.Linecast(new Vector3(4.9f, 1f, 7.6f), new Vector3(5.9f, 1f, 7.6f)), "餐廳→廚房的門");
            Assert.IsFalse(Physics.Linecast(new Vector3(2.7f, 1f, 4.9f), new Vector3(2.7f, 1f, 5.9f)), "客廳↔餐廳寬開口");
            StringAssert.Contains("客廳", qa.ObjectiveText());
        }

        [Test]
        public void LayoutB_OriginalStairDirection_WithRealStorage()
        {
            var qa = NewQa(Case01Floorplan.V05B);
            var L = qa.Layout;
            Assert.AreEqual(Case01HouseBuilder.StairZ0, L.StairBottomZ, "原版方向：南端起步");
            Assert.AreEqual(Case01HouseBuilder.StairZ1, L.StairTopZ);
            Assert.IsNotNull(Find(qa, "LowCupboard"), "樓梯北端的空檔是矮櫃");
            Assert.IsNotNull(Find(qa, "UnderStairPanel"), "樓梯下是掃具櫃（側板＋門）");
            Assert.IsTrue(Physics.CheckBox(new Vector3(9.4f, 0.45f, 4.8f), new Vector3(0.3f, 0.2f, 0.1f)), "沒有可走進去的窄縫");
        }

        [TestCase(Case01Floorplan.Original)]
        [TestCase(Case01Floorplan.V04)]
        [TestCase(Case01Floorplan.V05A)]
        [TestCase(Case01Floorplan.V05B)]
        public void Windows_AreTransparent_RealOpenings_WithAWorldOutside(Case01Floorplan plan)
        {
            var qa = NewQa(plan);
            var glass = qa.Layout.Root.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "Glass").ToList();
            Assert.GreaterOrEqual(glass.Count, 5);
            foreach (var g in glass)
            {
                var m = g.sharedMaterial;
                Assert.GreaterOrEqual(m.renderQueue, 3000, "透明排序");
                Assert.Less(m.color.a, 0.3f, "不是不透明藍板");
                Assert.AreEqual(0, m.GetInt("_ZWrite"), "不寫深度");
                Assert.Greater(m.GetFloat("_Glossiness"), 0.8f, "保留輕微反光");
                Assert.IsNull(g.GetComponent<Collider>());
                // 從室內往外看：窗洞後面沒有牆。
                var w = g.transform;
                var outward = w.forward;
                var c = w.position;
                if ((c + outward - new Vector3(5f, c.y, 5f)).sqrMagnitude < (c - new Vector3(5f, c.y, 5f)).sqrMagnitude) outward = -outward;
                Assert.IsFalse(Physics.Raycast(c - outward * 0.4f, outward, 6f), "窗洞是真的開口：" + c);
            }
            Assert.IsNotNull(qa.Layout.Exterior);
            Assert.Greater(qa.Layout.Exterior.LitWindows.Count, 3, "窗外有亮著燈的住宅");
            Assert.AreEqual(0, qa.Layout.Exterior.Root.GetComponentsInChildren<Collider>().Length, "外景沒有碰撞體");
            Assert.AreEqual(0, qa.Layout.Exterior.Root.GetComponentsInChildren<Light>().Length, "外面沒有會穿牆的點光源");
        }

        [Test]
        public void StudyBookcase_HasShelves_BooksSitInside()
        {
            var qa = NewQa(Case01Floorplan.Original);
            var bc = Find(qa, "StudyBookcase");
            Assert.IsNotNull(bc);
            Assert.AreEqual(4, bc.Cast<Transform>().Count(t => t.name == "Shelf"), "多層層板");
            Assert.AreEqual(2, bc.Cast<Transform>().Count(t => t.name == "Side"));
            Assert.IsNotNull(bc.Find("Back"));
            Assert.IsNotNull(bc.Find("Top"));
            var books = bc.Cast<Transform>().Where(t => t.name == "StudyBook" || t.name == "BookFlat").ToList();
            Assert.Greater(books.Count, 60);
            Assert.Greater(books.Count(t => t.name == "BookFlat"), 2, "少量橫放");
            float y = Case01HouseBuilder.FloorHeight;
            foreach (var b in books)
            {
                var p = b.position;
                Assert.That(p.x, Is.InRange(Case01HouseBuilder.BookcaseFrontX, Case01HouseBuilder.BookcaseFrontX + Case01HouseBuilder.BookcaseDepth), "書在櫃體之內（不是貼在表面）");
                Assert.That(p.z, Is.InRange(6.54f, 8.46f));
                Assert.That(p.y, Is.InRange(y + 0.1f, y + 2.17f));
            }
            Assert.IsTrue(qa.Layout.Interactables.ContainsKey(Case01Ids.OldRecord));
            Assert.IsNotNull(qa.Layout.Decor.StudyDuplicateBook);
        }

        [Test]
        public void EllieBedroom_HasPersonality_AndStorageTextIsInWorld()
        {
            var qa = NewQa(Case01Floorplan.V05A);
            foreach (var name in new[] { "Headboard", "Pillow", "Duvet", "WardrobeDoor", "WardrobeDoorAjar", "Suitcase", "Corkboard", "PhotoWall_0", "DeskChairSeat", "Brochure" })
                Assert.IsNotNull(Find(qa, name), "臥室：" + name);
            StringAssert.DoesNotContain("QA", Case01Texts.Storage, "儲藏室沒有劇外文案");
            StringAssert.DoesNotContain("閣樓", Case01Texts.Storage);
            StringAssert.Contains("鎖", Case01Texts.Storage);
        }

        [Test]
        public void PhotoPlace_FollowsTheLayout_InCorrectionHints()
        {
            var qa = NewQa(Case01Floorplan.V05A, null, Case01Checkpoint.S50, Case01QaMode.CoreV03);
            Assert.AreEqual("一樓客廳", qa.Flow.PhotoPlace);
            StringAssert.Contains("一樓客廳", Case01Texts.PhotoNeed(qa.Flow.PhotoPlace));
            var orig = NewQa(Case01Floorplan.Original, null, Case01Checkpoint.S50, Case01QaMode.CoreV03);
            Assert.AreEqual("一樓餐廳", orig.Flow.PhotoPlace, "原版仍在餐廳");
        }

        [Test]
        public void ResidentialQa_F9Cycles_AB_Original_V04_AndJumpsKeepLayout()
        {
            var qa = NewQa(Case01Floorplan.V05A);
            Assert.IsTrue(qa.ResidentialQa);
            StringAssert.Contains("住宅 v0.5", qa.Hud.Base.ViewMode.text);
            var order = new[] { Case01Floorplan.V05B, Case01Floorplan.Original, Case01Floorplan.V04, Case01Floorplan.V05A };
            foreach (var p in order)
            {
                qa.Tick(Dt, new Case01Input { Function = 9 });
                Assert.AreEqual(p, qa.Layout.Floorplan);
            }
            qa.Tick(Dt, new Case01Input { Jump = 2 });
            Assert.AreEqual(Case01Floorplan.V05A, qa.Layout.Floorplan, "F1～F4 保留格局");
            var def = NewQa(Case01Floorplan.Original);
            Assert.IsFalse(def.ResidentialQa, "正式 QA 不受影響");
        }

        [Test]
        public void LayoutA_WrongConnectionBackToStairs_IsShort()
        {
            var qa = NewQa(Case01Floorplan.V05A, null, Case01Checkpoint.S30);
            qa.Player.Teleport(new Vector3(Case01HouseBuilder.StudyDoorX, Case01HouseBuilder.FloorHeight + 0.05f, 4.2f), 0f);
            Run(qa, 0.1f);
            WalkThrough(qa, Case01HouseBuilder.StudyDoorX, 5.8f, Case01Room.Dining);
            Assert.AreEqual(Case01Room.Dining, qa.CurrentRoom, "錯接後知道自己在餐廳（站在餐桌前）");
            Assert.Less(Vector3.Distance(qa.Player.transform.position, qa.Layout.TablePoint), 2.6f);
            walked = 0f;
            A().DiningToLanding(qa);
            Assert.Less(walked, 22f, "回到樓梯平台不是冤枉路（v0.4 B 錯接後回二樓 26.5 m）：實際 " + walked.ToString("0.0") + " m");
        }

        // ---------------- 完整流程（A、B） ----------------

        [TestCase(Case01Floorplan.V05A)]
        [TestCase(Case01Floorplan.V05B)]
        public void FullPlaythrough_V03Correction_FP001_FrontDoor(Case01Floorplan plan)
        {
            var R = plan == Case01Floorplan.V05A ? A() : B();
            var qa = NewQa(plan, QuickVictory(), Case01Checkpoint.S00, Case01QaMode.CoreV03);

            // 餐具（兩副）→ 合照（客廳）→ 回來只剩一副。
            R.EntranceToDining(qa);
            Interact(qa, Case01Ids.Table);
            R.DiningToEntrance(qa);
            R.ToPhoto(qa);
            Interact(qa, Case01Ids.Photo);
            CloseText(qa);
            R.PhotoToEntrance(qa);
            Run(qa, 0.1f);
            R.EntranceToDining(qa);
            Assert.IsTrue(qa.Session.Has(Case01Flags.TableRewritten), "餐桌二→一");
            R.DiningToEntrance(qa);

            // 樓梯 → 書房線索 → 錯接到餐廳 → 回樓梯。
            R.EntranceToLanding(qa);
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
            R.DiningToLanding(qa);
            WalkTo(qa, 6.3f, 3.9f);
            Interact(qa, Case01Ids.StudyMark);
            Choose(qa, 1);
            Assert.IsTrue(qa.Flow.StairRouteRestored);
            CloseText(qa);
            LandingToMezzanine(qa);

            // 核 → 反制 → 失序體 → 戰鬥（既有規則）。
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

            // 核校正 v0.3。
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

            // 回屋 → FP-001 → 前門。
            R.EntranceToLanding(qa);
            LandingToMezzanine(qa);
            Assert.IsTrue(qa.Flow.FamilyPhotoAvailable);
            var ev = qa.Layout.Interactables[Case01Ids.EvidencePhoto].transform.position;
            WalkTo(qa, ev.x + 1.2f, ev.z);
            Interact(qa, Case01Ids.EvidencePhoto);
            Assert.AreEqual(Case01Mode.Inspect, qa.Mode, "FP-001 檢視");
            Tick(qa, Case01Input.From(new ExploreInput { Interact = true }));
            Assert.IsTrue(qa.Flow.FamilyPhotoCollected);
            MezzTo17(qa);
            Interact(qa, Case01Ids.FrontDoor);
            Choose(qa, 1);
            Assert.AreEqual(Case01Mode.Ended, qa.Mode, "前門出口");
            foreach (var flag in Case01Flags.All) Assert.IsTrue(qa.Session.Has(flag), flag);
        }
    }
}
