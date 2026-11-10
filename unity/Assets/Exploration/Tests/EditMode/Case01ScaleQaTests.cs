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
    /// Phase 1.7：R1（長形連棟 10 × 16 m）與 R2（R1＋空間節奏）是完整可走的新住宅：所有劇情物件 ID 都在、
    /// 主路線用真正的移動與碰撞走得完、完整流程（餐桌→合照→書房→錯接→第三扇門→核→戰鬥→核校正 v0.3→FP-001→前門）跑得完、
    /// 所有調查點走得到；QA 的步速／FOV／眼高／靈敏度只改 QA 的玩家；換格局不留殘物；O（v0.5 A）與 Phase 1.6 QA 不受影響。
    /// </summary>
    public sealed class Case01ScaleQaTests
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

        private Case01ScaleQA NewQa(Case01Floorplan plan, CombatLabScenario scenario = null)
        {
            var go = new GameObject("Case01ScaleTest");
            roots.Add(go);
            var s = go.AddComponent<Case01ScaleQA>();
            s.Setup(false, scenario, 1, plan);
            return s;
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

        private static Vector2 LookDelta(FirstPersonController p, Vector3 target, bool level)
        {
            var d = target - p.EyePosition;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = level ? 0f : -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            return new Vector2(Mathf.DeltaAngle(p.Yaw, yaw) / (FirstPersonController.LookSensitivity * p.LookScale), -(pitch - p.Pitch) / (FirstPersonController.LookSensitivity * p.LookScale));
        }

        private bool? TryWalk(Case01HouseQA qa, float x, float z, float max = 15f)
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

        private void Path(Case01HouseQA qa, float[] xz)
        {
            for (int i = 0; i + 1 < xz.Length; i += 2) WalkTo(qa, xz[i], xz[i + 1]);
        }

        private void Leg(Case01HouseQA qa, Case01Floorplan plan, int i) { Path(qa, Case01ScaleRoutes.For(plan)[i].XZ); }

        private void WalkThrough(Case01HouseQA qa, float x, float z, Case01Room expect)
        {
            Assert.IsTrue(TryWalk(qa, x, z) == false, "應在門洞觸發傳送（停在 " + qa.Player.transform.position + "）");
            RunUntil(qa, () => qa.Mode == Case01Mode.Exploring, "傳送結束", 5f);
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

        // ---------------- 建置與尺寸 ----------------

        [TestCase(Case01Floorplan.R1)]
        [TestCase(Case01Floorplan.R2)]
        public void NewHouse_KeepsEveryStoryObject_IsDeeper_AndMatchesTheStreetFrontage(Case01Floorplan plan)
        {
            var o = NewQa(Case01Floorplan.V05A).House.Layout;
            var L = NewQa(plan).House.Layout;
            foreach (var id in o.Interactables.Keys) Assert.IsTrue(L.Interactables.ContainsKey(id), plan + " 缺少劇情物件 " + id);
            Assert.AreEqual(10f, L.Footprint.width, 1e-4f, "面寬 10 m：與街區十七號立面相同，16／18 號接壤不變");
            Assert.AreEqual(18f, L.Footprint.height, 1e-4f, "縱深 18 m（原版 10 m）");
            Assert.AreEqual(7.1f, L.Interactables[Case01Ids.FrontDoor].transform.position.x, 0.01f, "前門與街區立面同一個位置");
            Assert.That(L.StairAngle, Is.InRange(30f, 42f), "樓梯坡度");
            foreach (var r in new[] { Case01Room.Entrance, Case01Room.Living, Case01Room.Dining, Case01Room.Kitchen, Case01Room.Landing, Case01Room.Study, Case01Room.Bedroom })
                Assert.IsTrue(L.Rooms.Any(v => v.Room == r), r.ToString());
            var third = L.Portal(Case01Door.ThirdDoor);
            Assert.AreEqual(Case01HouseBuilder.R17.ThirdDoorX, third.PlanePoint.x, 1e-3f);
            Assert.IsFalse(L.Root.Find("SecondFloor").GetComponentsInChildren<Transform>().Any(t => t.name == "Casing" && Mathf.Abs(t.position.z) < 0.2f && Mathf.Abs(t.position.x - Case01HouseBuilder.R17.ThirdDoorX) < 0.8f),
                "第三扇門平時是平牆，沒有門框（不提前暴雷）");
            Assert.GreaterOrEqual(L.QaObservations.Count, 3, "可選的環境觀察點（QA 專用、不進日誌）");
            if (plan == Case01Floorplan.R2)
                foreach (var n in new[] { "Lining_VestibuleDoor", "Lining_LivingDiningDoor", "Lining_LandingArch", "Bulkhead" })
                    Assert.IsTrue(L.Root.GetComponentsInChildren<Transform>().Any(t => t.name == n), "R2 節奏：" + n);
        }

        [Test]
        public void Exterior_IsDeepenedWithTheHouse_NothingOutsideStandsInsideTheRooms()
        {
            var L = NewQa(Case01Floorplan.R1).House.Layout;
            foreach (var r in L.Exterior.Root.GetComponentsInChildren<Renderer>(true))
            {
                var c = r.bounds.center;
                if (c.y < 0.2f) continue;
                bool inside = c.x > 0.1f && c.x < 9.9f && c.z > 0.1f && c.z < 17.9f;
                Assert.IsFalse(inside, "窗外的 " + r.name + " 跑進屋子裡（" + c + "）");
            }
        }

        // ---------------- 路線（真正的移動） ----------------

        [TestCase(Case01Floorplan.V05A)]
        [TestCase(Case01Floorplan.R1)]
        [TestCase(Case01Floorplan.R2)]
        public void MainRoute_IsWalkable_AndRLayoutsAreReallyLonger(Case01Floorplan plan)
        {
            var s = NewQa(plan);
            var qa = s.House;
            walked = 0f;
            var legs = Case01ScaleRoutes.For(plan);
            var expect = new[] { Case01Room.Living, Case01Room.Dining, Case01Room.Kitchen, Case01Room.Landing, Case01Room.Study, Case01Room.Bedroom, Case01Room.Landing };
            for (int i = 0; i < legs.Length; i++)
            {
                Path(qa, legs[i].XZ);
                Assert.AreEqual(expect[i], qa.CurrentRoom, plan + "：" + legs[i].Name);
            }
            if (plan == Case01Floorplan.V05A) return;
            float oLen = 0f, rLen = 0f;
            Vector3 end, from = Case01ScaleRoutes.Start(Case01Floorplan.V05A);
            foreach (var leg in Case01ScaleRoutes.For(Case01Floorplan.V05A)) { oLen += Case01ScaleRoutes.Length(from, leg, out end); from = end; }
            from = Case01ScaleRoutes.Start(plan);
            foreach (var leg in legs) { rLen += Case01ScaleRoutes.Length(from, leg, out end); from = end; }
            Assert.Greater(rLen, oLen * 1.3f, plan + " 主路線至少比 O 長 30%（O " + oLen.ToString("0.0") + " m、" + plan + " " + rLen.ToString("0.0") + " m）");
            Assert.Greater(walked, rLen * 0.9f, "實際走的距離與路線相符");
        }

        // ---------------- 完整流程 ----------------

        [TestCase(Case01Floorplan.R1)]
        [TestCase(Case01Floorplan.R2)]
        public void FullPlaythrough_NewHouse_ToBattle_CoreV03_FP001_FrontDoor(Case01Floorplan plan)
        {
            var s = NewQa(plan, QuickVictory());
            var qa = s.House;
            var legs = Case01ScaleRoutes.For(plan);
            float y = Case01HouseBuilder.FloorHeight;
            bool r2 = plan == Case01Floorplan.R2;

            // 一樓：走廊 → 餐廳看餐具 → 客廳合照 → 回餐廳（二→一）。
            Path(qa, r2 ? new[] { 7.1f, 2.2f, 7.6f, 4.6f, 7.2f, 9.2f, 5.6f, 9.2f, 4.6f, 8.1f } : new[] { 7.6f, 1.8f, 7.6f, 4.6f, 7.2f, 9.2f, 5.6f, 9.2f, 4.6f, 8.1f });
            Assert.AreEqual(Case01Room.Dining, qa.CurrentRoom, "從走廊進餐廳");
            Interact(qa, Case01Ids.Table);
            CloseText(qa);
            Path(qa, r2 ? new[] { 5.6f, 9.2f, 7.2f, 9.2f, 7.6f, 4.6f, 7.3f, 2.6f } : new[] { 5.6f, 9.2f, 7.2f, 9.2f, 7.6f, 4.6f, 7.6f, 1.2f });
            Leg(qa, plan, 0);
            Interact(qa, Case01Ids.Photo);
            CloseText(qa);
            Leg(qa, plan, 1);
            Run(qa, 0.1f);
            Assert.IsTrue(qa.Session.Has(Case01Flags.TableRewritten), "餐桌二→一");
            Leg(qa, plan, 2);
            Assert.AreEqual(Case01Room.Kitchen, qa.CurrentRoom);
            // 冰箱紙條：R1 在東北角；R2 在廚房門旁（進門後轉身才看得到）。
            if (r2) WalkTo(qa, 5.0f, 13.8f);
            else Path(qa, new[] { 8.3f, 13.4f, 8.3f, 17.0f });
            Interact(qa, Case01Ids.SundayNote);
            CloseText(qa);
            if (r2) WalkTo(qa, 5.7f, 12.8f);
            else Path(qa, new[] { 8.3f, 13.4f, 4.6f, 12.8f });
            Leg(qa, plan, 3);
            Assert.AreEqual(Case01Room.Landing, qa.CurrentRoom, "走廊盡頭轉身上樓");
            Assert.Greater(qa.Player.transform.position.y, 2.5f);

            // 二樓：書房線索 → 書房門錯接到餐廳 → 回平台覆寫記號 → 第三扇門。
            Leg(qa, plan, 4);
            Assert.AreEqual(Case01Room.Study, qa.CurrentRoom);
            WalkTo(qa, 3.6f, 10.1f);
            Interact(qa, Case01Ids.Map);
            Interact(qa, Case01Ids.EllieNotes);
            WalkTo(qa, 5.2f, 9.5f);
            Interact(qa, Case01Ids.OldRecord);
            CloseText(qa);
            Path(qa, new[] { 5.3f, 10.0f, 5.8f, 11.15f, 7.6f, 11.15f, 7.7f, 8.0f });
            Assert.AreEqual(Case01Room.Landing, qa.CurrentRoom);
            WalkTo(qa, 7.6f, 11.15f);
            WalkThrough(qa, 5.6f, 11.15f, Case01Room.Dining);
            Assert.IsTrue(qa.Session.Has(Case01Flags.RouteMismatchSeen), "書房門錯接到餐廳");
            Path(qa, new[] { 5.6f, 7.9f, 5.6f, 9.2f, 7.2f, 9.2f, 7.6f, 11.2f, 9.45f, 11.2f, 9.45f, 10.9f, 9.45f, 7.0f, 9.45f, 6.2f });
            Path(qa, new[] { 8.4f, 6.2f, 7.6f, 7.0f, 7.3f, 10.0f });
            Interact(qa, Case01Ids.StudyMark);
            Choose(qa, 1);
            Assert.IsTrue(qa.Flow.StairRouteRestored, "書房門框覆寫");
            CloseText(qa);
            if (r2) Path(qa, new[] { 7.4f, 4.7f, 7.4f, 3.7f });
            WalkTo(qa, Case01HouseBuilder.R17.ThirdDoorX, 1.8f);
            WalkThrough(qa, Case01HouseBuilder.R17.ThirdDoorX, -0.6f, Case01Room.Mezzanine);

            // 核 → 失序體 → 戰鬥（既有規則）。
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

            // 核校正 v0.3 → 經 17 號門寫回 → 回到新住宅的玄關。
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
            Path(qa, new[] { 38.5f, 0f, 38.5f, 4.5f, 40f, 4.6f });
            WalkThrough(qa, 40f, 6.7f, Case01Room.Entrance);
            Assert.IsTrue(qa.Flow.CoreStabilized, "穿門寫回");
            Assert.Less(qa.Player.transform.position.z, 3f, "回到新住宅的前門內側");
            Run(qa, 2f);

            // 回屋上樓 → 夾層 FP-001 → 前門。
            Path(qa, new[] { 7.6f, 2.4f, 7.6f, 4.6f, 7.6f, 11.2f, 9.45f, 11.2f, 9.45f, 10.9f, 9.45f, 7.0f, 9.45f, 6.2f });
            if (r2) Path(qa, new[] { 7.4f, 4.7f, 7.4f, 3.7f });
            WalkTo(qa, Case01HouseBuilder.R17.ThirdDoorX, 1.8f);
            WalkThrough(qa, Case01HouseBuilder.R17.ThirdDoorX, -0.6f, Case01Room.Mezzanine);
            Assert.IsTrue(qa.Flow.FamilyPhotoAvailable);
            var ev = qa.Layout.Interactables[Case01Ids.EvidencePhoto].transform.position;
            WalkTo(qa, ev.x + 1.2f, ev.z);
            Interact(qa, Case01Ids.EvidencePhoto);
            Assert.AreEqual(Case01Mode.Inspect, qa.Mode, "FP-001 檢視");
            Tick(qa, Case01Input.From(new ExploreInput { Interact = true }));
            Assert.IsTrue(qa.Flow.FamilyPhotoCollected);
            Path(qa, new[] { 38.5f, 0f, 38.5f, 4.5f, 40f, 4.6f });
            WalkThrough(qa, 40f, 6.7f, Case01Room.Entrance);
            Interact(qa, Case01Ids.FrontDoor);
            Choose(qa, 1);
            Assert.AreEqual(Case01Mode.Ended, qa.Mode, "前門出口");
            foreach (var flag in Case01Flags.All) Assert.IsTrue(qa.Session.Has(flag), flag);
        }

        // ---------------- 調查點都走得到 ----------------

        private static HashSet<Vector2Int> Reach(Case01ScaleQA s, Vector3 start, Rect fp)
        {
            const float cell = 0.2f;
            float y = start.y;
            var seen = new HashSet<Vector2Int>();
            var q = new Queue<Vector2Int>();
            var c0 = new Vector2Int(Mathf.RoundToInt(start.x / cell), Mathf.RoundToInt(start.z / cell));
            System.Func<Vector2Int, bool> ok = c =>
            {
                var p = new Vector3(c.x * cell, y, c.y * cell);
                if (p.x < fp.xMin || p.x > fp.xMax || p.z < fp.yMin - 0.2f || p.z > fp.yMax) return false;
                if (!Case01SpatialMap.HouseRoom(s.House.Layout.RoomAt(p))) return false;
                if (!Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, 0.6f, Case01SpatialMap.SightMask, QueryTriggerInteraction.Ignore)) return false;
                return s.Free(p);
            };
            Assert.IsTrue(ok(c0), "起點可站：" + start);
            seen.Add(c0);
            q.Enqueue(c0);
            var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var d in dirs)
                {
                    var n = c + d;
                    if (seen.Contains(n) || !ok(n)) continue;
                    seen.Add(n);
                    q.Enqueue(n);
                }
            }
            return seen;
        }

        [TestCase(Case01Floorplan.R1)]
        [TestCase(Case01Floorplan.R2)]
        public void EveryHotspot_AndObservationPoint_IsReachable(Case01Floorplan plan)
        {
            var s = NewQa(plan);
            var L = s.House.Layout;
            s.House.Player.Body.enabled = false;
            var ground = Reach(s, L.Spawn.Position, L.Footprint);
            var upper = Reach(s, new Vector3(8.4f, Case01HouseBuilder.FloorHeight + 0.05f, 5.4f), L.Footprint);
            s.House.Player.Body.enabled = true;
            System.Func<HashSet<Vector2Int>, float, IEnumerable<Vector3>> pts = (set, yy) => set.Select(c => new Vector3(c.x * 0.2f, yy, c.y * 0.2f));
            foreach (var room in new[] { Case01Room.Entrance, Case01Room.Living, Case01Room.Dining, Case01Room.Kitchen })
                Assert.IsTrue(pts(ground, 0.05f).Any(p => L.RoomAt(p) == room), plan + "：一樓走得到 " + room);
            foreach (var room in new[] { Case01Room.Landing, Case01Room.Study, Case01Room.Bedroom })
                Assert.IsTrue(pts(upper, Case01HouseBuilder.FloorHeight + 0.05f).Any(p => L.RoomAt(p) == room), plan + "：二樓走得到 " + room);
            var missing = new List<string>();
            foreach (var kv in L.Interactables)
            {
                var it = kv.Value;
                if (it == null || !it.gameObject.activeInHierarchy) continue;
                var c = it.transform.TransformPoint(it.GetComponent<BoxCollider>().center);
                if (c.x > 20f || c.z > 20f) continue;
                bool upstairs = c.y > 2.6f;
                var cells = pts(upstairs ? upper : ground, upstairs ? Case01HouseBuilder.FloorHeight + 0.05f : 0.05f);
                bool reach = cells.Any(p =>
                {
                    var eye = p + Vector3.up * FirstPersonController.EyeHeight;
                    return new Vector2(p.x - c.x, p.z - c.z).magnitude < 2.0f && Vector3.Distance(eye, c) < Case01HouseQA.InteractDistance - 0.1f &&
                           !Physics.Linecast(eye, c, Case01SpatialMap.SightMask, QueryTriggerInteraction.Ignore);
                });
                if (!reach) missing.Add(kv.Key);
            }
            CollectionAssert.IsEmpty(missing, plan + "：走不到（或看不到）的調查點");
        }

        // ---------------- QA 功能 ----------------

        [Test]
        public void SpeedFovEyeLook_OnlyChangeTheQaPlayer_AndFormalDefaultsStay()
        {
            var s = NewQa(Case01Floorplan.R1);
            var p = s.House.Player;
            Assert.AreEqual(2.2f, p.WalkSpeedValue, 1e-4f, "QA 預設候選步速 2.2 m/s");
            s.Press(6);
            Assert.AreEqual(2.6f, s.House.Player.WalkSpeedValue, 1e-4f);
            s.Press(6);
            Assert.AreEqual(FirstPersonController.WalkSpeed, s.House.Player.WalkSpeedValue, 1e-4f, "第四組＝正式值");
            s.Press(7);
            Assert.AreEqual(1.72f, s.House.Player.EyeHeightValue, 1e-4f);
            Assert.AreEqual(1.72f, s.House.Player.EyePosition.y - s.House.Player.transform.position.y, 1e-3f, "鏡頭跟著眼高");
            Assert.AreEqual(FirstPersonController.Height, s.House.Player.Body.height, 1e-4f, "碰撞身高不變（不縮小玩家）");
            s.Press(5);
            s.House.Tick(Dt, Case01Input.None);
            s.Frame(Dt);
            Assert.AreEqual(55f, s.House.ExploreCamera.fieldOfView, 0.01f, "F5 循環到 55°");
            s.Press(10);
            Assert.AreEqual(1.4f, s.House.Player.LookScale, 1e-4f);
            s.Press(12);
            Assert.Greater(s.House.Player.Acceleration, 0f);
            // 換格局後新玩家也套用同一組參數。
            s.Press(9);
            Assert.AreEqual(Case01Floorplan.R2, s.Plan);
            Assert.AreEqual(1.72f, s.House.Player.EyeHeightValue, 1e-4f);
            // 其他 QA 的玩家仍是正式值。
            var go = new GameObject("Formal");
            roots.Add(go);
            var other = go.AddComponent<Case01HouseQA>();
            other.Build(false, null, 1, Case01Checkpoint.S00, null, Case01Floorplan.V05A);
            Assert.AreEqual(FirstPersonController.WalkSpeed, other.Player.WalkSpeedValue);
            Assert.AreEqual(FirstPersonController.EyeHeight, other.Player.EyeHeightValue);
            Assert.AreEqual(1f, other.Player.LookScale);
            Assert.AreEqual(0f, other.Player.Acceleration);
        }

        [Test]
        public void SwitchingPlans_KeepsStoryProgress_LeavesNoLeftovers()
        {
            var s = NewQa(Case01Floorplan.R1);
            s.House.Flow.Interact(Case01Ids.Table);
            int colliders = s.House.GetComponentsInChildren<Collider>(true).Length;
            int lights = s.House.GetComponentsInChildren<Light>(true).Length;
            var flow = s.House.Flow;
            for (int i = 0; i < 3; i++) s.Press(9);
            Assert.AreEqual(Case01Floorplan.R1, s.Plan, "O→R1→R2→O→R1");
            Assert.AreSame(flow, s.House.Flow, "同一個劇情流程");
            Assert.IsTrue(s.House.Session.Has(Case01Flags.TableTwo));
            Assert.AreEqual(colliders, s.House.GetComponentsInChildren<Collider>(true).Length, "沒有殘留的碰撞體");
            Assert.AreEqual(lights, s.House.GetComponentsInChildren<Light>(true).Length);
            Assert.AreEqual(1, s.GetComponentsInChildren<FirstPersonController>(true).Length);
            Assert.AreEqual(Case01Room.Entrance, s.House.CurrentRoom, "從該格局的玄關開始");
        }

        [Test]
        public void ObservationPoint_ShowsANote_NotAJournalEntry()
        {
            var s = NewQa(Case01Floorplan.R1);
            var qa = s.House;
            int journal = qa.Session.Journal.Count;
            Path(qa, new[] { 8.8f, 3.2f });
            Interact(qa, "qa_obs_phone");
            Assert.IsTrue(qa.Hud.NoteText.transform.parent.gameObject.activeSelf, "顯示短觀察");
            StringAssert.Contains("電話", qa.Hud.NoteText.text);
            Assert.AreEqual(journal, qa.Session.Journal.Count, "不是正式線索，不進日誌");
        }

        [TestCase(Case01Floorplan.V05A)]
        [TestCase(Case01Floorplan.R1)]
        [TestCase(Case01Floorplan.R2)]
        public void FixedViewpoints_AreLegal(Case01Floorplan plan)
        {
            var s = NewQa(plan);
            var views = Case01ScaleQA.ViewsFor(plan);
            for (int i = 0; i < views.Length; i++)
            {
                s.GoView(i);
                Assert.IsFalse(s.PositionRemapped, plan + " 視點 " + (i + 1) + "（" + views[i].Name + "）在牆或家具裡");
                Assert.AreNotEqual(Case01Room.None, s.House.Layout.RoomAt(s.House.Player.transform.position), views[i].Name);
            }
        }

        [Test]
        public void OtherQaAndDemo_StayOnTheirLayouts()
        {
            var go = new GameObject("DemoCheck");
            roots.Add(go);
            var d = go.AddComponent<Case01DemoSlice>();
            d.Build(false, Case01DemoArea.Street, Case01Floorplan.Original);
            var go2 = new GameObject("Plain");
            roots.Add(go2);
            var qa = go2.AddComponent<Case01HouseQA>();
            qa.Build(false, null, 1, Case01Checkpoint.S00);
            Assert.AreEqual(Case01Floorplan.Original, qa.Floorplan, "一般 QA 預設仍是原版");
            Assert.AreEqual(10f, qa.Layout.Footprint.height, 1e-4f);
            Assert.AreEqual(0, qa.Layout.QaObservations.Count, "原版沒有 QA 觀察點");
        }
    }
}
