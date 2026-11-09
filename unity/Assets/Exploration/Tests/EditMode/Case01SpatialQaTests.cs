using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// Phase 1.6 空間結構灰盒 QA：A 與 v0.5 A 完全相同；B／C 的開口、門框深度、前廊、短走廊實際存在且可通行；
    /// 三個方案都能用真正的移動與碰撞走完一樓→二樓→第三扇門的劇情流程；所有調查點走得到；切換方案不留殘物；
    /// 固定視點在每個方案都合法；FOV／灰盒／盲測只改 QA；其他 QA 不受影響。
    /// </summary>
    public sealed class Case01SpatialQaTests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
        }

        private Case01SpatialQA NewQa(Case01SpatialVariant v = Case01SpatialVariant.A)
        {
            var go = new GameObject("Case01SpatialTest");
            roots.Add(go);
            var s = go.AddComponent<Case01SpatialQA>();
            s.Setup(false, null, 1);
            if (v != Case01SpatialVariant.A) s.SetVariant(v);
            return s;
        }

        // ---------------- 腳本化輸入（與住宅 v0.5 測試相同的走法） ----------------

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
            return new Vector2(Mathf.DeltaAngle(p.Yaw, yaw) / FirstPersonController.LookSensitivity, -(pitch - p.Pitch) / FirstPersonController.LookSensitivity);
        }

        private static bool? TryWalk(Case01HouseQA qa, float x, float z, float max = 12f)
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
                if ((np - stuckAt).magnitude > 0.05f)
                {
                    stuckAt = np;
                    stuckTime = 0f;
                }
                else if ((stuckTime += Dt) > 1.5f) return null;
            }
            return null;
        }

        private static void WalkTo(Case01HouseQA qa, float x, float z)
        {
            var r = TryWalk(qa, x, z);
            Assert.IsTrue(r == true, "走不到 (" + x + ", " + z + ")，停在 " + qa.Player.transform.position + "，房間 " + qa.CurrentRoom);
        }

        private static void Path(Case01HouseQA qa, params float[] xz)
        {
            for (int i = 0; i + 1 < xz.Length; i += 2) WalkTo(qa, xz[i], xz[i + 1]);
        }

        private static void WalkThrough(Case01HouseQA qa, float x, float z, Case01Room expect)
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

        private static void CloseText(Case01HouseQA qa)
        {
            if (qa.Hud.PanelVisible) Tick(qa, Case01Input.From(new ExploreInput { Interact = true }));
        }

        // ---------------- 三個方案的路線 ----------------

        private sealed class Routes
        {
            public float[] ToDining, DiningBack, ToPhoto, PhotoBack, DiningToStairTop;
        }

        private static readonly float[] KitchenToLanding = { 6.2f, 7.6f, 8.0f, 6.0f, 9.35f, 5.4f, 9.35f, 4.75f, 9.35f, 1.3f, 9.4f, 0.5f, 8.2f, 0.5f, 6.95f, 1.6f, 6.95f, 3.6f };

        private static Routes RoutesFor(Case01SpatialVariant v)
        {
            switch (v)
            {
                case Case01SpatialVariant.B:
                    // 玄關 → 1.0 m 客廳門 → 沿南側窗下往西 → 壁爐與茶几之間往北 → 壁爐旁的 1.2 m 門洞 → 餐廳。
                    return new Routes
                    {
                        ToDining = new[] { 6.2f, 1.4f, 4.7f, 1.4f, 1.35f, 1.35f, 1.35f, 4.9f, 1.85f, 5.1f, 1.85f, 6.0f },
                        DiningBack = new[] { 1.85f, 6.0f, 1.85f, 5.1f, 1.35f, 4.9f, 1.35f, 1.35f, 4.7f, 1.4f, 6.2f, 1.4f, 6.8f, 2.2f },
                        ToPhoto = new[] { 6.2f, 1.4f, 4.7f, 1.4f, 1.6f, 1.5f },
                        PhotoBack = new[] { 4.7f, 1.4f, 6.2f, 1.4f, 6.8f, 2.2f },
                        DiningToStairTop = new[] { 3.4f, 6.3f, 4.6f, 7.6f },
                    };
                case Case01SpatialVariant.C:
                    // 前廊 → 1.0 m 內門 → 內玄關 → 客廳門（z2.6）→ 沙發東側 → 層架旁 1.2 m 通道 → 餐廳。
                    return new Routes
                    {
                        ToDining = new[] { 7.1f, 2.2f, 6.2f, 2.6f, 4.6f, 2.6f, 4.6f, 4.75f, 3.3f, 5.0f, 3.3f, 6.0f },
                        DiningBack = new[] { 3.3f, 6.0f, 3.3f, 5.0f, 4.6f, 4.75f, 4.6f, 2.6f, 6.2f, 2.6f, 6.8f, 3.2f },
                        ToPhoto = new[] { 7.1f, 2.2f, 6.2f, 2.6f, 4.6f, 2.6f, 4.6f, 1.5f, 1.6f, 1.5f },
                        PhotoBack = new[] { 4.6f, 1.5f, 4.6f, 2.6f, 6.2f, 2.6f, 6.8f, 3.2f },
                        DiningToStairTop = new[] { 3.4f, 6.4f, 4.6f, 7.6f },
                    };
                default:
                    return new Routes
                    {
                        ToDining = new[] { 6.2f, 1.4f, 4.7f, 1.4f, 4.7f, 4.7f, 3.4f, 5.05f, 3.4f, 6.0f },
                        DiningBack = new[] { 3.4f, 6.0f, 3.4f, 5.05f, 4.7f, 4.7f, 4.7f, 1.4f, 6.2f, 1.4f, 6.8f, 2.2f },
                        ToPhoto = new[] { 6.2f, 1.4f, 4.7f, 1.4f, 1.6f, 1.5f },
                        PhotoBack = new[] { 4.7f, 1.4f, 6.2f, 1.4f, 6.8f, 2.2f },
                        DiningToStairTop = new[] { 3.4f, 6.4f, 4.6f, 7.6f },
                    };
            }
        }

        // ---------------- A 不改幾何 ----------------

        private static List<string> Geometry(Transform root)
        {
            return root.GetComponentsInChildren<Collider>(true)
                .Select(c => c.name + "@" + c.bounds.center.x.ToString("0.00") + "," + c.bounds.center.y.ToString("0.00") + "," + c.bounds.center.z.ToString("0.00") + (c.isTrigger ? "T" : ""))
                .OrderBy(s => s).ToList();
        }

        [Test]
        public void VariantA_IsTheV05ALayoutUnchanged()
        {
            var go = new GameObject("PlainV05A");
            roots.Add(go);
            var plain = go.AddComponent<Case01HouseQA>();
            plain.Build(false, null, 1, Case01Checkpoint.S00, null, Case01Floorplan.V05A);
            var s = NewQa();
            Assert.AreEqual(Case01SpatialVariant.A, s.Variant);
            CollectionAssert.AreEqual(Geometry(plain.Layout.Root), Geometry(s.House.Layout.Root), "方案 A＝最新 v0.5 A，沒有任何碰撞體不同");
            Assert.IsFalse(s.House.Layout.Root.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("Lining_") || t.name == "Larder" || t.name == "HalfScreenShelf"));
            Assert.AreEqual(plain.Layout.Rooms.Count, s.House.Layout.Rooms.Count);
        }

        // ---------------- B／C 的開口確實存在 ----------------

        private static bool Clear(Case01SpatialQA s, float x, float y, float z) { return s.Free(new Vector3(x, y, z)); }

        [Test]
        public void VariantB_OffsetOpenings_LiningDepth_FridgeByTheDoor_StairNib()
        {
            var s = NewQa(Case01SpatialVariant.B);
            var root = s.House.Layout.Root;
            Assert.IsTrue(Clear(s, 1.85f, 0.05f, 5.4f), "壁爐旁的 1.2 m 門洞可通行");
            Assert.IsFalse(Clear(s, 3.3f, 0.05f, 5.4f), "原本置中寬開口的位置已是牆");
            Assert.IsTrue(Clear(s, 5.4f, 0.05f, V05ALivingDoorZ), "玄關→客廳門（同位置、1.0 m）");
            Assert.IsFalse(Clear(s, 5.4f, 0.05f, 0.75f), "門縮窄：原本 1.4 m 開口的南緣已是牆");
            Assert.GreaterOrEqual(root.GetComponentsInChildren<Transform>().Count(t => t.name == "Lining_LivingDoor"), 2, "客廳門有門框深度");
            Assert.GreaterOrEqual(root.GetComponentsInChildren<Transform>().Count(t => t.name == "Lining_StudyDoor"), 2, "書房門有門框深度");
            var fridge = root.GetComponentsInChildren<Transform>().First(t => t.name == "Fridge");
            Assert.Less(Vector3.Distance(fridge.position, Case01HouseBuilder.Spatial16.FridgeByDoor), 0.01f);
            var note = s.House.Layout.Interactables[Case01Ids.SundayNote].transform.position;
            Assert.Less(new Vector2(note.x - fridge.position.x, note.z - fridge.position.z).magnitude, 0.8f, "冰箱紙條熱區跟著冰箱");
            Assert.Greater(note.x, fridge.position.x, "紙條在冰箱正面（朝東、背對廚房門）");
            Assert.IsFalse(Clear(s, 8.65f, 0.05f, 4.85f), "樓梯起步旁的牆垛");
            Assert.IsTrue(Clear(s, 9.35f, 0.05f, 5.2f), "樓梯起步空間仍淨空");
        }

        private const float V05ALivingDoorZ = Case01HouseBuilder.V05A.LivingDoorZ;

        [Test]
        public void VariantC_Vestibule_HalfScreen_Corridor_Bulkhead()
        {
            var s = NewQa(Case01SpatialVariant.C);
            var L = s.House.Layout;
            float y2 = Case01HouseBuilder.FloorHeight + 0.05f;
            Assert.IsTrue(Clear(s, 7.1f, 0.05f, 1.6f), "前廊內門");
            Assert.IsFalse(Clear(s, 6.0f, 0.05f, 1.6f), "前廊隔牆");
            Assert.IsTrue(Clear(s, 5.4f, 0.05f, 2.6f), "客廳門移到內玄關");
            Assert.IsFalse(Clear(s, 5.4f, 0.05f, V05ALivingDoorZ), "原客廳門位置在前廊裡，已封牆");
            Assert.IsFalse(Clear(s, 2.1f, 0.05f, 5.4f), "寬開口西半邊是開放層架（擋人）");
            Assert.IsTrue(Clear(s, 3.3f, 0.05f, 5.4f), "東半邊 1.2 m 通行");
            Assert.IsFalse(Physics.Linecast(new Vector3(2.1f, 1.65f, 4.6f), new Vector3(2.1f, 1.65f, 6.4f), Case01SpatialMap.SightMask, QueryTriggerInteraction.Ignore), "層架只擋到 1 m：眼高的視線可以從層板間穿過（半遮）");
            Assert.IsTrue(Clear(s, 4.15f, y2, 4.4f), "二樓短走廊");
            Assert.AreEqual(Case01Room.Landing, L.RoomAt(new Vector3(4.15f, y2, 4.4f)), "短走廊算樓梯平台（不是儲藏室）");
            Assert.IsTrue(Clear(s, Case01HouseBuilder.Spatial16.BedroomDoorX, y2, 5.0f), "走廊盡頭的臥室門");
            Assert.IsTrue(Clear(s, 9.35f, 0.05f, 5.2f), "樓梯起步空間淨空（低樑在 2.15 m 以上）");
            var beam = L.Root.GetComponentsInChildren<Transform>().First(t => t.name == "Bulkhead");
            Assert.Greater(beam.GetComponent<Collider>().bounds.min.y, FirstPersonController.Height + 0.3f);
            var cal = L.Interactables[Case01Ids.Calendar].transform.position;
            Assert.AreEqual(3.75f, cal.z, 0.01f, "日曆跟著移到客廳門北側");
        }

        // ---------------- 每個方案的劇情流程（真正的移動與碰撞） ----------------

        [TestCase(Case01SpatialVariant.A)]
        [TestCase(Case01SpatialVariant.B)]
        [TestCase(Case01SpatialVariant.C)]
        public void Playthrough_GroundFloor_Stairs_Study_WrongConnection_ThirdDoor(Case01SpatialVariant v)
        {
            var s = NewQa(v);
            var qa = s.House;
            var R = RoutesFor(v);
            Path(qa, R.ToDining);
            Assert.AreEqual(Case01Room.Dining, qa.CurrentRoom, "經客廳走進餐廳");
            Interact(qa, Case01Ids.Table);
            CloseText(qa);
            Path(qa, R.DiningBack);
            Assert.AreEqual(Case01Room.Entrance, qa.CurrentRoom);
            Path(qa, R.ToPhoto);
            Assert.AreEqual(Case01Room.Living, qa.CurrentRoom);
            Interact(qa, Case01Ids.Photo);
            CloseText(qa);
            Path(qa, R.PhotoBack);
            Run(qa, 0.1f);
            Path(qa, R.ToDining);
            Assert.IsTrue(qa.Session.Has(Case01Flags.TableRewritten), "餐桌二→一");
            Path(qa, R.DiningToStairTop);
            Path(qa, KitchenToLanding);
            Assert.AreEqual(Case01Room.Landing, qa.CurrentRoom, "經廚房上到二樓平台");

            Path(qa, 6.95f, 4.3f, 6.95f, 5.8f);
            Assert.AreEqual(Case01Room.Study, qa.CurrentRoom);
            WalkTo(qa, 6.7f, 8.2f);
            Interact(qa, Case01Ids.Map);
            Interact(qa, Case01Ids.EllieNotes);
            Path(qa, 7.5f, 7.7f, 8.7f, 7.5f);
            Interact(qa, Case01Ids.OldRecord);
            CloseText(qa);
            Path(qa, 7.5f, 7.0f, 6.95f, 6.0f, 6.95f, 4.2f);
            Assert.AreEqual(Case01Room.Landing, qa.CurrentRoom);
            WalkTo(qa, 6.95f, 4.3f);
            WalkThrough(qa, 6.95f, 5.8f, Case01Room.Dining);
            Assert.IsTrue(qa.Session.Has(Case01Flags.RouteMismatchSeen), "書房門錯接到餐廳");
            Path(qa, R.DiningToStairTop);
            Path(qa, KitchenToLanding);
            WalkTo(qa, 6.3f, 3.9f);
            Interact(qa, Case01Ids.StudyMark);
            Tick(qa, new Case01Input { Choice = 1 });
            Assert.IsTrue(qa.Flow.StairRouteRestored, "書房門框覆寫");
            CloseText(qa);
            WalkTo(qa, 6.95f, 2.0f);
            WalkThrough(qa, 6.95f, -0.6f, Case01Room.Mezzanine);
        }

        [Test]
        public void VariantC_BedroomByTheShortCorridor()
        {
            var s = NewQa(Case01SpatialVariant.C);
            var qa = s.House;
            qa.Player.Teleport(new Vector3(6.95f, Case01HouseBuilder.FloorHeight + 0.05f, 3.6f), -90f);
            Run(qa, 0.1f);
            Path(qa, 5.6f, 4.4f, 4.15f, 4.4f);
            Assert.AreEqual(Case01Room.Landing, qa.CurrentRoom, "走廊仍是平台");
            Path(qa, 4.15f, 5.8f);
            Assert.AreEqual(Case01Room.Bedroom, qa.CurrentRoom, "從平台經短走廊進臥室");
            Path(qa, 3.6f, 8.6f);
            Interact(qa, Case01Ids.JobLetter);
        }

        // ---------------- 調查點都走得到 ----------------

        private static HashSet<Vector2Int> Reach(Case01SpatialQA s, Vector3 start)
        {
            const float cell = 0.2f;
            float y = start.y;
            var seen = new HashSet<Vector2Int>();
            var q = new Queue<Vector2Int>();
            var c0 = new Vector2Int(Mathf.RoundToInt(start.x / cell), Mathf.RoundToInt(start.z / cell));
            System.Func<Vector2Int, bool> ok = c =>
            {
                var p = new Vector3(c.x * cell, y, c.y * cell);
                if (p.x < 0f || p.x > 10f || p.z < -0.2f || p.z > 10f) return false;
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

        [TestCase(Case01SpatialVariant.A)]
        [TestCase(Case01SpatialVariant.B)]
        [TestCase(Case01SpatialVariant.C)]
        public void EveryHouseHotspot_IsReachable_AndEveryRoomIsWalkable(Case01SpatialVariant v)
        {
            var s = NewQa(v);
            var L = s.House.Layout;
            s.House.Player.Body.enabled = false;
            var ground = Reach(s, new Vector3(7.1f, 0.05f, 1.0f));
            var upper = Reach(s, new Vector3(9.4f, Case01HouseBuilder.FloorHeight + 0.05f, 0.6f));
            s.House.Player.Body.enabled = true;
            System.Func<HashSet<Vector2Int>, float, IEnumerable<Vector3>> pts = (set, y) => set.Select(c => new Vector3(c.x * 0.2f, y, c.y * 0.2f));
            foreach (var room in new[] { Case01Room.Entrance, Case01Room.Living, Case01Room.Dining, Case01Room.Kitchen })
                Assert.IsTrue(pts(ground, 0.05f).Any(p => L.RoomAt(p) == room), v + "：一樓走得到 " + room);
            foreach (var room in new[] { Case01Room.Landing, Case01Room.Study, Case01Room.Bedroom })
                Assert.IsTrue(pts(upper, Case01HouseBuilder.FloorHeight + 0.05f).Any(p => L.RoomAt(p) == room), v + "：二樓走得到 " + room);
            var missing = new List<string>();
            foreach (var kv in L.Interactables)
            {
                var it = kv.Value;
                if (it == null || !it.gameObject.activeInHierarchy) continue;
                var bc = it.GetComponent<BoxCollider>();
                var c = it.transform.TransformPoint(bc.center);
                if (c.x > 15f || c.z > 15f) continue; // 夾層與異常中庭不受方案影響
                bool upstairs = c.y > 2.6f;
                var cells = pts(upstairs ? upper : ground, upstairs ? Case01HouseBuilder.FloorHeight + 0.05f : 0.05f);
                bool reach = cells.Any(p =>
                {
                    var eye = p + Vector3.up * FirstPersonController.EyeHeight;
                    var flat = new Vector2(p.x - c.x, p.z - c.z).magnitude;
                    return flat < 2.0f && Vector3.Distance(eye, c) < Case01HouseQA.InteractDistance - 0.1f &&
                           !Physics.Linecast(eye, c, Case01SpatialMap.SightMask, QueryTriggerInteraction.Ignore);
                });
                if (!reach) missing.Add(kv.Key);
            }
            CollectionAssert.IsEmpty(missing, v + "：走不到（或看不到）的調查點");
        }

        // ---------------- 切換與 QA 功能 ----------------

        private static List<string> Names(Transform root)
        {
            return root.GetComponentsInChildren<Transform>(true).Select(t => t.name + "@" + t.position.x.ToString("0.0") + "," + t.position.y.ToString("0.0") + "," + t.position.z.ToString("0.0")).OrderBy(n => n).ToList();
        }

        [Test]
        public void SwitchingVariants_LeavesNoDoubleWalls_Colliders_Lights_OrPlayers()
        {
            var s = NewQa();
            var objects = Names(s.House.Layout.Root);
            int colliders = s.GetComponentsInChildren<Collider>(true).Length;
            int lights = s.GetComponentsInChildren<Light>(true).Length;
            var start = s.House.Player.transform.position;
            for (int i = 0; i < 6; i++) s.Press(9);
            Assert.AreEqual(Case01SpatialVariant.A, s.Variant);
            var now = Names(s.House.Layout.Root);
            var diff = objects.Except(now).Concat(now.Except(objects)).Take(12).ToArray();
            Assert.AreEqual(objects.Count, now.Count, "A→B→C→A→B→C→A 後房屋物件相同：" + string.Join("、", diff));
            Assert.AreEqual(colliders, s.GetComponentsInChildren<Collider>(true).Length);
            Assert.AreEqual(lights, s.GetComponentsInChildren<Light>(true).Length);
            Assert.AreEqual(1, s.GetComponentsInChildren<FirstPersonController>(true).Length);
            Assert.AreEqual(1, s.House.transform.Cast<Transform>().Count(t => t.name == "Case01Content"), "只有一份房屋內容");
            Assert.Less(Vector3.Distance(start, s.House.Player.transform.position), 0.05f, "合法位置：切換後同座標");
            s.Press(9);
            Assert.AreEqual(Case01SpatialVariant.B, s.Variant);
            Assert.Greater(s.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Lining_")), 0);
            s.Press(9);
            s.Press(9);
            Assert.AreEqual(0, s.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Lining_") || t.name == "Bulkhead"), "回到 A：B／C 的物件都清掉");
        }

        [Test]
        public void SwitchIntoAWall_MovesToNearestSafeSpot_AndSaysSo()
        {
            var s = NewQa();
            // A 的客廳門口（z1.4）在方案 C 是前廊與客廳之間的牆。
            s.House.Player.Teleport(new Vector3(5.4f, 0.05f, 1.2f), -90f);
            s.SetVariant(Case01SpatialVariant.C);
            Assert.IsTrue(s.PositionRemapped, "不是嚴格同座標：要標示");
            Assert.IsTrue(s.Free(s.House.Player.transform.position), "移到可以站的位置");
            Assert.Less(Vector3.Distance(s.House.Player.transform.position, new Vector3(5.4f, 0.05f, 1.2f)), 1.7f);
        }

        [TestCase(Case01SpatialVariant.A)]
        [TestCase(Case01SpatialVariant.B)]
        [TestCase(Case01SpatialVariant.C)]
        public void FixedViewpoints_AreLegalInEveryVariant(Case01SpatialVariant v)
        {
            var s = NewQa(v);
            for (int i = 0; i < Case01SpatialQA.Views.Length; i++)
            {
                s.GoView(i);
                Assert.IsFalse(s.PositionRemapped, v + " 視點 " + (i + 1) + "（" + Case01SpatialQA.Views[i].Name + "）在牆或家具裡");
                Assert.AreEqual(Case01SpatialQA.Views[i].OnlyC && v != Case01SpatialVariant.C, s.ViewSubstituted);
                Assert.AreNotEqual(Case01Room.None, s.House.Layout.RoomAt(s.House.Player.transform.position));
            }
        }

        [Test]
        public void Fov_Graybox_Blind_Map_AreQaOnly()
        {
            var s = NewQa();
            var cam = s.House.ExploreCamera;
            s.Press(5);
            s.House.Tick(Dt, Case01Input.None);
            s.Frame(Dt);
            Assert.AreEqual(60f, cam.fieldOfView, 0.01f, "F5：FOV 60");
            s.Press(5);
            s.Frame(Dt);
            Assert.AreEqual(70f, cam.fieldOfView, 0.01f);
            Assert.AreEqual(1.65f, FirstPersonController.EyeHeight, 1e-4f, "眼高不變");

            var wall = s.House.Layout.Root.Find("GroundFloor").GetComponentsInChildren<MeshRenderer>().First(r => r.name == "Wall");
            var grayMat = wall.sharedMaterial;
            s.Press(7);
            Assert.IsFalse(s.Graybox);
            Assert.AreNotEqual(grayMat, wall.sharedMaterial, "F7：換回原材質");
            Assert.AreEqual(s.House.Layout.Palette.Get("wall_plaster"), wall.sharedMaterial);
            s.Press(7);
            Assert.AreEqual(grayMat, wall.sharedMaterial, "再按回灰盒");

            s.Press(10);
            Assert.IsTrue(s.Blind);
            foreach (var bad in new[] { "A｜", "B｜", "C｜", "原樣", "錯位", "過渡" }) StringAssert.DoesNotContain(bad, s.VariantLabel());
            s.Press(10);

            var plan = s.PlanTexture(0);
            Assert.IsNotNull(plan);
            var px = plan.GetPixels32();
            Assert.Greater(px.Count(p => p.r == Case01SpatialMap.WallColor.r && p.g == Case01SpatialMap.WallColor.g && p.b == Case01SpatialMap.WallColor.b), 500, "平面圖畫出牆");
            Assert.Greater(px.Count(p => p.r == Case01SpatialMap.HotspotColor.r && p.g == Case01SpatialMap.HotspotColor.g), 20, "平面圖標出調查點");
            int mode = s.MapMode;
            s.Press(4);
            Assert.AreEqual((mode + 1) % 3, s.MapMode);
        }

        [Test]
        public void Metrics_FromTheFrontDoor_NoRoomBeyondTheHallIsVisible()
        {
            foreach (var v in new[] { Case01SpatialVariant.A, Case01SpatialVariant.B, Case01SpatialVariant.C })
            {
                var s = NewQa(v);
                s.GoView(0);
                s.UpdateMetrics(16f / 9f);
                Vector2Int d;
                Assert.IsTrue(!s.LastStats.TryGetValue(Case01Room.Dining, out d) || d.x == 0, v + "：在前門看不到餐廳");
                Assert.IsTrue(!s.LastStats.TryGetValue(Case01Room.Kitchen, out d) || d.x == 0, v + "：在前門看不到廚房");
                Vector2Int e;
                Assert.IsTrue(s.LastStats.TryGetValue(Case01Room.Entrance, out e) && e.x > 0, v + "：看得到玄關地面");
                Object.DestroyImmediate(s.gameObject);
            }
        }

        [Test]
        public void OtherQaAndDemo_DoNotGetTheVariants()
        {
            var go = new GameObject("OtherQa");
            roots.Add(go);
            var qa = go.AddComponent<Case01HouseQA>();
            qa.Build(false, null, 1, Case01Checkpoint.S00, null, Case01Floorplan.V05A);
            Assert.IsNull(qa.LayoutPostBuild);
            Assert.IsFalse(qa.Layout.Root.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("Lining_") || t.name == "Bulkhead" || t.name == "HalfScreenShelf"));
            Assert.Throws<System.ArgumentException>(() =>
            {
                var g2 = new GameObject("OriginalPlan");
                roots.Add(g2);
                var q2 = g2.AddComponent<Case01HouseQA>();
                q2.Build(false, null, 1, Case01Checkpoint.S00);
                Case01HouseBuilder.ApplySpatialVariant(q2.Layout, Case01SpatialVariant.B);
            }, "方案只能套在 V05A 上");
        }
    }
}
