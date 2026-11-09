using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Battle.UI;
using Shepherd.Exploration.Case01;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// Demo Phase 1（v0.7）P0：街上看不到室內異常門、門後不透出不相干的空間、屋內不畫異常空間（異常空間不畫街景）、
    /// 方案 A 玄關不直通廚房、門洞淨空；不改路由、旗標或傳送判定。
    /// </summary>
    public sealed class Case01Phase1P0Tests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
            Case01RunState.Reset();
        }

        private Case01HouseQA NewQa(Case01Checkpoint? cp = null, Case01Floorplan plan = Case01Floorplan.V05A)
        {
            var go = new GameObject("Case01P0Test");
            roots.Add(go);
            var qa = go.AddComponent<Case01HouseQA>();
            qa.Build(false, null, 1, cp, null, plan);
            return qa;
        }

        [Test]
        public void Street17Facade_ShowsNoInteriorPortalHint()
        {
            var go = new GameObject("Street");
            roots.Add(go);
            var d = go.AddComponent<Case01DemoSlice>();
            d.Build(false, Case01DemoArea.Street, Case01Floorplan.V05A);
            var h17 = d.AreaLayout.Root.GetComponentsInChildren<Transform>().First(t => t.name == "House17");
            Assert.IsFalse(h17.GetComponentsInChildren<Transform>().Any(t => t.name == "Scar"), "二樓正面沒有門形痕跡");
            foreach (var r in h17.GetComponentsInChildren<Renderer>())
            {
                var m = r.sharedMaterial;
                if (!m.IsKeywordEnabled("_EMISSION")) continue;
                var e = m.GetColor("_EmissionColor");
                Assert.IsFalse(e.b > e.r + 0.02f && e.b > 0.05f, r.name + " 沒有紫／藍色的異常自發光");
            }
            Assert.AreEqual(0, Object.FindObjectsOfType<Case01HouseQA>().Length, "街區不會同時存在屋內場景");
        }

        [Test]
        public void PortalVoid_FollowsMembrane_IsOneSided_AndHasNoCollider()
        {
            var qa = NewQa(Case01Checkpoint.S40);
            qa.Tick(Dt, Case01Input.None);
            foreach (var p in qa.Layout.Portals)
            {
                Assert.IsNotNull(p.Backdrop, p.Door + " 有門後虛空背板");
                Assert.AreEqual(p.Membrane.activeSelf, p.Backdrop.activeSelf, p.Door + "：與膜同步");
                Assert.IsNull(p.Backdrop.GetComponent<Collider>(), p.Door + "：沒有碰撞（不擋穿門）");
                Assert.Greater(Vector3.Dot(p.Backdrop.transform.forward, p.Normal), 0.99f, p.Door + "：只從來源側看得到");
                float behind = Vector3.Dot(p.Backdrop.transform.position - p.PlanePoint, p.Normal);
                Assert.That(behind, Is.InRange(0.1f, 0.4f), p.Door + "：貼在門後，不伸出去");
            }
            var third = qa.Layout.Portal(Case01Door.ThirdDoor);
            Assert.IsTrue(third.Membrane.activeSelf, "S40：第三扇門開著");
            Assert.IsTrue(third.Backdrop.activeSelf, "門後是虛空，看不到屋外街景");
            var study = NewQa(Case01Checkpoint.S00).Layout.Portal(Case01Door.StudyDoor);
            Assert.IsFalse(study.Backdrop.activeSelf, "未錯接時書房門後沒有背板（看得到真正的書房）");
        }

        [Test]
        public void ThirdDoor_StillTraversable_ToMezzanine()
        {
            var qa = NewQa(Case01Checkpoint.S40);
            qa.Player.Teleport(new Vector3(Case01HouseBuilder.StudyDoorX, Case01HouseBuilder.FloorHeight + 0.05f, 1.6f), 180f);
            for (int i = 0; i < 120 && qa.CurrentRoom != Case01Room.Mezzanine; i++) qa.Tick(Dt, Case01Input.From(ExploreInput.Forward()));
            for (int i = 0; i < 60 && qa.Mode != Case01Mode.Exploring; i++) qa.Tick(Dt, Case01Input.None);
            Assert.AreEqual(Case01Room.Mezzanine, qa.CurrentRoom, "第三扇門仍然通往夾層");
        }

        [Test]
        public void AnomalySpaces_NotRenderedFromTheHouse_StreetNotRenderedInside()
        {
            var qa = NewQa();
            var L = qa.Layout;
            foreach (var r in L.Root.Find("Mezzanine").GetComponentsInChildren<Renderer>(true)) Assert.AreEqual(Case01HouseBuilder.AnomalyLayer, r.gameObject.layer, r.name);
            foreach (var r in L.Root.Find("Courtyard").GetComponentsInChildren<Renderer>(true)) Assert.AreEqual(Case01HouseBuilder.AnomalyLayer, r.gameObject.layer, r.name);
            foreach (var r in L.Exterior.Root.GetComponentsInChildren<Renderer>(true)) Assert.AreEqual(Case01HouseBuilder.OutdoorLayer, r.gameObject.layer, r.name);
            qa.Tick(Dt, Case01Input.None);
            int mask = qa.ExploreCamera.cullingMask;
            Assert.AreEqual(0, mask & (1 << Case01HouseBuilder.AnomalyLayer), "屋內：不畫夾層與中庭");
            Assert.AreNotEqual(0, mask & (1 << Case01HouseBuilder.OutdoorLayer), "屋內：窗外街景照常");
            Assert.AreNotEqual(0, mask & 1, "預設層照常");
            var mezz = NewQa(Case01Checkpoint.S40);
            mezz.Tick(Dt, Case01Input.None);
            Assert.AreEqual(Case01Room.Mezzanine, mezz.CurrentRoom);
            mask = mezz.ExploreCamera.cullingMask;
            Assert.AreEqual(0, mask & (1 << Case01HouseBuilder.OutdoorLayer), "夾層：不畫街景");
            Assert.AreNotEqual(0, mask & (1 << Case01HouseBuilder.AnomalyLayer), "夾層：自己照常");
            Assert.IsTrue(Physics.Raycast(Case01HouseBuilder.MezzCenter + new Vector3(0f, 1f, 0f), Vector3.down, 2f), "只改渲染，夾層地板的碰撞還在");
        }

        [Test]
        public void ManifestationAndEnemies_OnTheAnomalyLayer()
        {
            var qa = NewQa(Case01Checkpoint.S40);
            qa.Player.Teleport(Case01HouseBuilder.MezzCenter + new Vector3(0f, 0.05f, -0.4f), 0f, 22f);
            qa.Tick(Dt, Case01Input.None);
            qa.Tick(Dt, Case01Input.From(new ExploreInput { Interact = true }));
            for (int i = 0; i < 400 && qa.Mode != Case01Mode.Manifesting; i++) qa.Tick(Dt, Case01Input.None);
            Assert.AreEqual(Case01Mode.Manifesting, qa.Mode);
            foreach (var r in qa.Manifestation.Root.GetComponentsInChildren<Renderer>(true)) Assert.AreEqual(Case01HouseBuilder.AnomalyLayer, r.gameObject.layer);
            Assert.AreNotEqual(0, qa.ExploreCamera.cullingMask & (1 << Case01HouseBuilder.AnomalyLayer), "中庭裡看得到失序體");
        }

        [Test]
        public void LayoutA_EntranceDoesNotOpenIntoTheKitchen_DoorwaysClear()
        {
            var qa = NewQa();
            Assert.IsTrue(Physics.Linecast(new Vector3(7.85f, 1f, 4.0f), new Vector3(7.85f, 1f, 4.9f)), "玄關北牆是實牆");
            Assert.IsTrue(Physics.Linecast(new Vector3(6.6f, 1f, 4.0f), new Vector3(6.6f, 1f, 4.9f)));
            var doorways = new Dictionary<string, Vector3>
            {
                { "前門內側", new Vector3(7.1f, 0f, 0.6f) },
                { "玄關→客廳開口", new Vector3(Case01HouseBuilder.V05A.HallX0, 0f, Case01HouseBuilder.V05A.LivingDoorZ) },
                { "客廳→餐廳寬開口", new Vector3(Case01HouseBuilder.V05A.DiningGapX, 0f, Case01HouseBuilder.V05A.LivingZ1) },
                { "餐廳→廚房門", new Vector3(Case01HouseBuilder.V05A.HallX0, 0f, Case01HouseBuilder.V05A.DiningKitchenDoorZ) },
                { "樓梯起步", new Vector3(9.35f, 0f, 5.2f) },
                { "平台→書房門", new Vector3(Case01HouseBuilder.StudyDoorX, Case01HouseBuilder.FloorHeight, 5f) },
                { "書房→臥室門", new Vector3(5f, Case01HouseBuilder.FloorHeight, 7.5f) },
            };
            qa.Player.Body.enabled = false;   // 玩家自己的膠囊不算
            foreach (var kv in doorways)
            {
                var b = kv.Value;
                Assert.IsFalse(Physics.CheckCapsule(b + Vector3.up * 0.45f, b + Vector3.up * 1.45f, 0.3f, ~0, QueryTriggerInteraction.Ignore), kv.Key + " 沒有被家具或模型佔住");
            }
            StringAssert.Contains("經餐廳進廚房", qa.ObjectiveText().Length > 0 ? Case01HouseQAObjective(qa) : "");
        }

        /// <summary>取方案 A 的樓梯提示（目標文字第二步才會提到樓梯；直接讀第一步之後的提示）。</summary>
        private static string Case01HouseQAObjective(Case01HouseQA qa)
        {
            qa.Flow.Interact(Case01Ids.Table);
            return qa.ObjectiveText();
        }
    }
}
