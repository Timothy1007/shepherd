using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// Phase 1.8：R2 沉浸版——v0.6／v0.7 美術就地套用且舊物件不再渲染；真門（兩面可用、往遠離玩家的一側開、擋人／擋視線、不夾人、
    /// 和書房門錯接分開）；逐房光影；可聽的 3D 聲音與門的阻隔；腳步材質；三種輕互動；H2 時同一扇門真的通往不同房間；
    /// 帶著門走完一樓到第三扇門的劇情路線；原 R2 測試屋不受污染。自動化測不到「臨場感」，要真人在 Play 裡驗收。
    /// </summary>
    public sealed class Case01ImmersionTests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
        }

        private Case01ImmersionQA NewQa(bool immersive = true)
        {
            var go = new GameObject("Case01ImmersionTest");
            roots.Add(go);
            var s = go.AddComponent<Case01ImmersionQA>();
            s.Setup(false, null, 1, immersive);
            return s;
        }

        // ---------------- 走路與互動 ----------------

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

        /// <summary>站到門的這一側、瞄準門把附近、按 E，等門開完。</summary>
        private static void OpenDoor(Case01ImmersionQA s, string id, int side)
        {
            var d = s.World.Door(id);
            // 門往自己這一側開的（書房門）要站遠一點，不在門板轉動的路徑上。
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

        // ---------------- 美術 ----------------

        [Test]
        public void Art_IsAppliedInPlace_OldBlocksNoLongerRender_RoomsLookDifferent()
        {
            var s = NewQa();
            var art = s.World.Art;
            Assert.IsNotNull(art, "R2 套用了 v0.6／v0.7 美術");
            foreach (var key in new[] { "FrontDoor", "HallBench", "Mantelpiece", "Sofa", "CoffeeTable", "Armchair", "DiningTable", "Sideboard", "RangeCooker", "Fridge", "StudyDesk", "Bed", "Wardrobe" })
                Assert.IsTrue(art.Pieces.ContainsKey(key) && art.Pieces[key] != null && art.Pieces[key].gameObject.activeInHierarchy, "模型 " + key);
            var root = s.House.Layout.Root;
            foreach (var old in new[] { "Sofa", "CoffeeTable", "Fridge", "Sideboard", "ShoeBench" })
            {
                var t = root.Find("GroundFloor").GetComponentsInChildren<Transform>(true).First(x => x.name == old);
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if (art.Kit.Added.Any(g => g != null && r.transform.IsChildOf(g.transform))) continue;
                    if (old == "Fridge" && (r.name == "FridgeNote" || r.name == "Magnet")) continue;
                    if (old == "Sideboard" && (r.transform.IsChildOf(t.Find("TeaTray")) || r.name == "ShoppingList" || r.name == "Vase")) continue;
                    Assert.IsFalse(r.enabled, old + " 的舊方塊 " + r.name + " 還在渲染（新舊重疊）");
                }
            }
            Assert.Greater(art.WallPieces, 40, "牆面逐段貼上");
            var colors = art.WallMaterial.Values.Select(m => m.color).Distinct().Count();
            Assert.GreaterOrEqual(colors, 6, "玄關、客廳、餐廳、廚房、平台、書房、臥室的牆面各有自己的處理");
        }

        [Test]
        public void OriginalR2_IsUntouched()
        {
            var s = NewQa(false);
            Assert.IsNull(s.World);
            Assert.IsFalse(s.House.Layout.Root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "ImmersionR2" || t.name.StartsWith("Door_")));
            var go = new GameObject("PlainR2");
            roots.Add(go);
            var plain = go.AddComponent<Case01HouseQA>();
            plain.Build(false, null, 1, Case01Checkpoint.S00, Case01QaMode.CoreV03, Case01Floorplan.R2);
            System.Func<Transform, List<string>> geo = r => r.GetComponentsInChildren<Collider>(true).Select(c => c.name + "@" + c.bounds.center.ToString("F2")).OrderBy(x => x).ToList();
            CollectionAssert.AreEqual(geo(plain.Layout.Root), geo(s.House.Layout.Root), "原 R2 測試屋＝Phase 1.7 的 R2");
            Assert.AreEqual(55f, s.FieldOfView, 1e-3f, "QA 預設垂直 FOV 55°");
            Assert.AreEqual(1.58f, s.House.Player.EyeHeightValue, 1e-3f, "QA 預設眼高 1.58 m");
        }

        // ---------------- 門 ----------------

        private static bool Blocked(Case01ImmersionQA s, Vector3 feet)
        {
            var cc = s.House.Player.Body;
            cc.enabled = false;
            bool hit = Physics.CheckCapsule(feet + Vector3.up * 0.4f, feet + Vector3.up * 1.45f, 0.3f, Case01SpatialMap.SightMask, QueryTriggerInteraction.Ignore);
            cc.enabled = true;
            return hit;
        }

        [Test]
        public void Doors_BlockWhenClosed_OpenAwayFromThePlayer_FromBothSides()
        {
            var s = NewQa();
            Assert.AreEqual(7, s.World.Doors.Count, "玄關內門、客廳、客廳↔餐廳、走廊↔餐廳、廚房、書房、臥室");
            foreach (var d in s.World.Doors)
            {
                d.SetAngle(0f, null);
                Physics.SyncTransforms();
                Assert.IsTrue(Blocked(s, d.Center), d.Id + "：關著的門擋人");
                Assert.IsTrue(Physics.Linecast(d.Center + Vector3.up * 1.5f - d.Normal, d.Center + Vector3.up * 1.5f + d.Normal, Case01SpatialMap.SightMask, QueryTriggerInteraction.Ignore), d.Id + "：關著的門擋視線");
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
                    Assert.AreEqual(d.OnlySide != 0 ? d.OnlySide : -side, d.SwingSide, d.Id + "：往遠離玩家的一側開");
                    Assert.IsFalse(Blocked(s, d.Center), d.Id + "：開著時門洞可以通過");
                    d.Toggle(stand, s.House.Player);
                    for (int i = 0; i < 40; i++) d.Tick(Dt, s.House.Player);
                    Assert.AreEqual(Case01DoorState.Closed, d.State, d.Id + " 關上");
                }
            }
            Assert.AreEqual(-1, s.World.Door("door_study").OnlySide, "書房門只往平台側開（不碰失序門的膜與背板）");
        }

        [Test]
        public void Door_NeverClosesOnThePlayer()
        {
            var s = NewQa();
            var d = s.World.Door("door_hall_dining");
            var stand = d.Center + d.Normal * 1.1f;
            s.House.Player.Teleport(stand, 0f);
            d.Toggle(stand, s.House.Player);
            for (int i = 0; i < 40; i++) d.Tick(Dt, s.House.Player);
            // 門往走廊側開；玩家站進門板關回去的路徑上（門軸與門洞中心之間、開門那一側）。
            var hinge = new Vector3(d.Hinge.position.x, 0.05f, d.Hinge.position.z);
            var toCenter = (new Vector3(d.Center.x, 0.05f, d.Center.z) - hinge).normalized;
            var inPath = hinge + toCenter * 0.45f + d.Normal * (d.SwingSide * 0.45f);
            s.House.Player.Teleport(inPath, 0f);
            Physics.SyncTransforms();
            d.Toggle(inPath, s.House.Player);
            for (int i = 0; i < 40; i++) d.Tick(Dt, s.House.Player);
            Assert.AreNotEqual(Case01DoorState.Closed, d.State, "玩家站在關門路徑上：門不關（不把人夾住）");
            Assert.Greater(d.LastBlocked, 0);
        }

        [Test]
        public void StudyDoor_PhysicalStateIsSeparateFromTheWrongConnection()
        {
            var s = NewQa();
            float y = Case01HouseBuilder.FloorHeight + 0.05f;
            s.House.QaView(Case01Flow.ForCheckpoint(Case01Checkpoint.S30, Case01CorrectionMode.RevisedV03), new Case01Pose(new Vector3(7.6f, y, Case01HouseBuilder.R17.StudyDoorZ), -90f), 0f);
            Run(s, 0.2f);
            Assert.IsTrue(s.House.Flow.PortalActive(Case01Door.StudyDoor), "S30：書房門錯接中");
            Assert.IsTrue(TryWalk(s, 5.6f, Case01HouseBuilder.R17.StudyDoorZ, 3f) == null, "門關著：走不過去（也不會觸發錯接）");
            Assert.AreEqual(Case01Room.Landing, s.House.CurrentRoom);
            OpenDoor(s, "door_study", -1);
            Assert.AreEqual(-1, s.World.Door("door_study").SwingSide, "往平台側開");
            Assert.IsTrue(TryWalk(s, 5.6f, Case01HouseBuilder.R17.StudyDoorZ, 6f) == false, "門開著：穿過門洞觸發錯接");
            for (float t = 0; t < 5f && s.House.Mode != Case01Mode.Exploring; t += Dt) Step(s, Case01Input.None);
            Assert.AreEqual(Case01Room.Dining, s.House.CurrentRoom, "書房門通往餐廳（路由由 Flow 決定，不是門）");
        }

        // ---------------- 光、聲音、腳步、輕互動 ----------------

        [Test]
        public void Lighting_IsPerRoom_AndOldLightingStaysAvailable()
        {
            var s = NewQa();
            Assert.GreaterOrEqual(s.World.AddedLights.Count, 10, "逐房的局部光");
            Assert.AreEqual(LightType.Spot, s.World.DiningSpot.type, "餐桌正上方的光圈");
            Assert.IsNotNull(s.World.FireLight, "壁爐");
            Assert.IsNotNull(s.World.StudyGlow, "書房門縫的光");
            var plain = NewQa(false);
            Assert.Less(s.House.Layout.RoomLightIntensity[Case01Room.Living], plain.House.Layout.RoomLightIntensity[Case01Room.Living] * 0.5f, "客廳吊燈調暗，主光改成壁爐與檯燈");
            s.Press(9);
            Assert.IsFalse(s.Options.NewLighting);
            Assert.AreEqual(0, s.World.AddedLights.Count, "F9：舊光影");
        }

        [Test]
        public void Audio_IsAudible_RoomBased_AndDoorsMuffleIt()
        {
            foreach (var n in Case01Sfx.All) Assert.Greater(Case01Sfx.Rms(Case01Sfx.Get(n)), 0.003f, n + " 不是無聲佔位");
            var s = NewQa();
            var w = s.World;
            Assert.AreEqual("living", w.Source("fire").Key);
            Assert.AreEqual("kitchen", w.Source("fridge").Key);
            bool muffled;
            var living = w.Door("door_living");
            living.SetAngle(0f, null);
            float closed = w.Propagation("hall", "living", out muffled);
            Assert.IsTrue(muffled, "門關著：悶住");
            living.SetAngle(90f, null);
            float open = w.Propagation("hall", "living", out muffled);
            Assert.IsFalse(muffled);
            Assert.Greater(open, closed * 2f, "開門後聽得比較清楚");
            Assert.Greater(w.Propagation("living", "living", out muffled), 0.99f);
            Assert.Less(w.Propagation("study", "kitchen", out muffled), 0.1f, "二樓書房幾乎聽不到廚房");
            s.Press(4);
            s.House.Tick(Dt, Case01Input.None);
            s.World.Tick(0f, s.House);
            Assert.IsTrue(w.Sources.All(x => x.A.volume == 0f), "F4：聲音關");
        }

        [Test]
        public void Footsteps_ChangeWithTheFloor()
        {
            var w = NewQa().World;
            float y2 = Case01HouseBuilder.FloorHeight + 0.05f;
            Assert.AreEqual("tile", w.Surface(new Vector3(6.9f, 0.05f, 0.9f)), "前廊磁磚");
            Assert.AreEqual("rug", w.Surface(new Vector3(7.7f, 0.05f, 5f)), "走廊長毯");
            Assert.AreEqual("wood", w.Surface(new Vector3(5.2f, 0.05f, 1.0f)), "客廳木地板");
            Assert.AreEqual("rug", w.Surface(new Vector3(2.6f, 0.05f, 2.9f)), "客廳地毯");
            Assert.AreEqual("tile", w.Surface(new Vector3(8f, 0.05f, 15f)), "廚房磁磚");
            Assert.AreEqual("stair", w.Surface(new Vector3(9.4f, 1.5f, 8.6f)), "樓梯");
            Assert.AreEqual("wood", w.Surface(new Vector3(5.5f, y2, 7f)), "書房木地板");
        }

        [Test]
        public void LightInteractions_LampBookTap_HaveVisibleAndAudibleFeedback()
        {
            var s = NewQa();
            var w = s.World;
            int sounds = w.OneShots;
            var lampHot = w.Root.GetComponentsInChildren<Case01Interactable>().First(i => i.Id == "fx_lamp");
            Assert.IsTrue(w.Interact(lampHot, s.House));
            Assert.IsFalse(w.LivingLamp.enabled, "檯燈關掉");
            var book = w.Root.GetComponentsInChildren<Case01Interactable>().First(i => i.Id == "fx_book");
            Assert.IsTrue(w.Interact(book, s.House));
            Assert.IsTrue(w.BookOpen, "雜誌翻開");
            Assert.Greater(w.Root.GetComponentsInChildren<Transform>().First(t => t.name == "CoverHinge").localEulerAngles.z, 150f, "封面真的翻過去");
            var tap = w.Root.GetComponentsInChildren<Case01Interactable>().First(i => i.Id == "fx_tap");
            Assert.IsTrue(w.Interact(tap, s.House));
            Assert.IsTrue(w.TapOn);
            Assert.IsTrue(w.Root.GetComponentsInChildren<Transform>(true).First(t => t.name == "WaterStream").gameObject.activeSelf, "水流");
            Assert.AreEqual(sounds + 3, w.OneShots, "每個輕互動都有聲音");
        }

        // ---------------- 空間異變 ----------------

        [Test]
        public void SpatialAnomaly_SameHallDoor_LeadsToTheKitchenAtH2_AndBackAtH3()
        {
            var s = NewQa();
            var w = s.World;
            // H0：走廊上的這扇門通往餐廳。
            s.House.Player.Teleport(new Vector3(7.6f, 0.05f, 8.0f), -90f);
            Run(s, 0.1f);
            OpenDoor(s, "door_hall_dining", -1);
            Path(s, 5.6f, 9.3f, 4.6f, 7.4f);
            Assert.AreEqual(Case01Room.Dining, s.House.CurrentRoom, "H0：門 → 餐廳");
            // H2：重建在二樓（看不到一樓）。
            float y2 = Case01HouseBuilder.FloorHeight + 0.05f;
            s.House.QaView(Case01Flow.ForHouseStage(2), new Case01Pose(new Vector3(8.4f, y2, 5.4f), 180f), 0f);
            Run(s, 0.1f);
            w = s.World;
            Assert.IsTrue(s.House.Flow.WrongConnectionActive);
            Assert.IsTrue(w.TopologyH2, "H2：房子的連接已改變");
            s.House.Player.Teleport(new Vector3(7.6f, 0.05f, 8.0f), -90f);
            Run(s, 0.1f);
            OpenDoor(s, "door_hall_dining", -1);
            Path(s, 5.8f, 9.3f, 5.8f, 11.4f);
            Assert.IsTrue(TryWalk(s, 4.0f, 10.0f, 3f) == null, "窄廊西側是牆：餐廳從這裡進不去");
            OpenDoor(s, "door_kitchen", -1);
            Path(s, 5.75f, 11.3f, 5.75f, 12.8f);
            Assert.AreEqual(Case01Room.Kitchen, s.House.CurrentRoom, "H2：同一扇門 → 窄廊 → 廚房");
            // 回程合法：餐廳仍可從客廳進去。
            Assert.IsFalse(Blocked(s, new Vector3(3.0f, 0.05f, 7.0f)), "餐廳本身還在，可以從客廳進去");
            // H3：核穩定後恢復原本的連接（但不回暖）。
            s.House.QaView(Case01Flow.ForHouseStage(3), new Case01Pose(new Vector3(8.4f, y2, 5.4f), 180f), 0f);
            Run(s, 0.1f);
            Assert.IsFalse(s.World.TopologyH2, "H3：連接恢復");
        }

        [Test]
        public void SpatialAnomaly_OnlySwapsOutOfSight()
        {
            var s = NewQa();
            s.Options.Anomaly = false;
            s.House.QaView(Case01Flow.ForHouseStage(2), new Case01Pose(new Vector3(7.6f, 0.05f, 8.0f), -90f), 0f);
            Run(s, 0.1f);
            Assert.IsFalse(s.World.TopologyH2, "異變原版：不變形");
            s.Options.Anomaly = true;
            Run(s, 0.2f);
            Assert.IsFalse(s.World.TopologyH2, "玩家就站在走廊：不在眼前換牆");
            s.House.Player.Teleport(new Vector3(8.4f, Case01HouseBuilder.FloorHeight + 0.05f, 5.4f), 180f);
            Run(s, 0.2f);
            Assert.IsTrue(s.World.TopologyH2, "上到二樓（看不到）才換");
        }

        // ---------------- 帶門的劇情路線 ----------------

        [Test]
        public void StoryRoute_WithRealDoors_FrontDoorToTheThirdDoor()
        {
            var s = NewQa();
            var qa = s.House;
            OpenDoor(s, "door_vestibule", -1);
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
            Assert.IsTrue(s.World.TopologyH2, "在二樓時，一樓的連接改變了");
            Path(s, 7.6f, Case01HouseBuilder.R17.StudyDoorZ);
            Assert.IsTrue(TryWalk(s, 5.6f, Case01HouseBuilder.R17.StudyDoorZ, 6f) == false, "書房門錯接");
            for (float t = 0; t < 5f && qa.Mode != Case01Mode.Exploring; t += Dt) Step(s, Case01Input.None);
            Assert.AreEqual(Case01Room.Dining, qa.CurrentRoom, "書房門 → 餐廳");
            // 餐廳往走廊的門現在是窄廊的牆：要從客廳繞回去。
            Path(s, 1.25f, 6.4f, 1.25f, 5.0f, 1.4f, 4.6f, 1.4f, 1.6f, 5.0f, 1.2f, 5.6f, 3.6f, 7.2f, 3.6f, 7.6f, 4.6f, 7.6f, 11.2f, 9.45f, 11.2f, 9.45f, 10.9f, 9.45f, 7.0f, 9.45f, 6.2f, 8.4f, 6.2f, 7.6f, 7.0f, 7.3f, 10.0f);
            Interact(s, Case01Ids.StudyMark);
            Step(s, new Case01Input { Choice = 1 });
            Assert.IsTrue(qa.Flow.StairRouteRestored, "書房門框覆寫");
            CloseText(s);
            Path(s, 7.4f, 4.7f, 7.4f, 3.7f, Case01HouseBuilder.R17.ThirdDoorX, 1.8f);
            Assert.IsTrue(TryWalk(s, Case01HouseBuilder.R17.ThirdDoorX, -0.6f) == false, "第三扇門");
            for (float t = 0; t < 5f && qa.Mode != Case01Mode.Exploring; t += Dt) Step(s, Case01Input.None);
            Assert.AreEqual(Case01Room.Mezzanine, qa.CurrentRoom, "進入夾層");
        }
    }
}
