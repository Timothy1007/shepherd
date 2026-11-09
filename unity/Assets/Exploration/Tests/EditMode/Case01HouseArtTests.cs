using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// v0.7 Phase 1：方案 A 其他房間的美術換皮。模型確實載入（非 Primitive）、不新增碰撞、舊外觀隱藏而碰撞／熱區保留、
    /// 所有房間的調查物件在新美術下仍可調查、H1 的北側餐椅移位與第二副餐具消失照常、新舊切換乾淨；只在 Demo（方案 A）與客廳美術 QA 建立。
    /// </summary>
    public sealed class Case01HouseArtTests
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

        private Case01HouseQA DemoHouse()
        {
            var go = new GameObject("Case01HouseArtTest");
            roots.Add(go);
            var d = go.AddComponent<Case01DemoSlice>();
            d.Build(false, Case01DemoArea.House, Case01Floorplan.V05A);
            return d.House;
        }

        private static Vector2 LookDelta(FirstPersonController p, Vector3 target)
        {
            var dir = target - p.EyePosition;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;
            return new Vector2(Mathf.DeltaAngle(p.Yaw, yaw) / FirstPersonController.LookSensitivity, -(pitch - p.Pitch) / FirstPersonController.LookSensitivity);
        }

        private static void InteractFrom(Case01HouseQA qa, string id, Vector3 stand)
        {
            qa.Player.Teleport(stand, 0f);
            qa.Tick(Dt, Case01Input.None);
            var it = qa.Layout.Interactables[id];
            var target = it.transform.TransformPoint(it.GetComponent<BoxCollider>().center);
            for (int i = 0; i < 2; i++) qa.Tick(Dt, Case01Input.From(new ExploreInput { Look = LookDelta(qa.Player, target) }));
            Assert.AreEqual(id, qa.CurrentTarget != null ? qa.CurrentTarget.Id : null, "新美術下準星對得到 " + id + "（站在 " + stand + "）");
            qa.Tick(Dt, Case01Input.From(new ExploreInput { Interact = true }));
            if (qa.Hud.PanelVisible) qa.Tick(Dt, Case01Input.From(new ExploreInput { Interact = true }));
        }

        [Test]
        public void DemoHouseA_HasModelledFurniture_InEveryRoom_AndAddsNoColliders()
        {
            var qa = DemoHouse();
            var art = qa.Layout.HouseArt;
            Assert.IsNotNull(art, "Demo 方案 A 有 Phase 1 美術");
            Assert.IsNotNull(qa.Layout.LivingArt, "也有客廳 v0.6");
            Assert.IsFalse(qa.Layout.StartInLiving, "Demo 仍從玄關開始");
            Assert.AreEqual(Case01Room.Entrance, qa.CurrentRoom);
            foreach (var key in new[] { "FrontDoor", "HallBench", "CoatRack", "ShoeCabinet", "RangeCooker", "Sink", "WallCabinet", "Fridge", "DiningTable", "ChairNorth", "Sideboard",
                         "StudyDesk", "StudyChair", "Bed", "Wardrobe", "Nightstand", "StorageDoor" })
            {
                Assert.IsTrue(art.Pieces.ContainsKey(key) && art.Pieces[key] != null, key);
                var mfs = art.Pieces[key].GetComponentsInChildren<MeshFilter>();
                Assert.Greater(mfs.Sum(m => m.sharedMesh.vertexCount), 80, key + " 是建模網格");
                Assert.IsFalse(mfs.Any(m => m.sharedMesh.name == "Cube"), key + " 不是 Primitive");
            }
            Assert.AreEqual(0, art.Root.GetComponentsInChildren<Collider>(true).Length, "美術根物件沒有碰撞");
            foreach (var g in art.Kit.Added) Assert.AreEqual(0, g.GetComponentsInChildren<Collider>(true).Length, g.name + " 沒有新增碰撞");
            foreach (var r in art.Root.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    Assert.IsTrue(m != null && m.shader.isSupported, r.name + " 材質正常");
            // 舊外觀隱藏、碰撞保留。
            var oldCab = qa.Layout.Root.GetComponentsInChildren<Transform>(true).First(t => t.name == "ShoeCabinet");
            Assert.IsTrue(oldCab.GetComponentsInChildren<Renderer>(true).Where(r => !r.transform.IsChildOf(art.Pieces["ShoeCabinet"])).All(r => !r.enabled), "舊鞋櫃外觀隱藏");
            Assert.IsNotNull(oldCab.GetComponentInChildren<BoxCollider>(), "舊鞋櫃碰撞保留");
            var kitchenFloor = qa.Layout.Root.GetComponentsInChildren<Transform>(true).First(t => t.name == "KitchenFloor");
            Assert.AreEqual("P1_Tile", kitchenFloor.GetComponent<Renderer>().sharedMaterial.name, "廚房地磚不再是帶紫紋的中庭石材");
        }

        [Test]
        public void AllClues_StillInvestigable_WithTheNewArt()
        {
            var qa = DemoHouse();
            float y = Case01HouseBuilder.FloorHeight + 0.05f;
            InteractFrom(qa, Case01Ids.Table, new Vector3(2.7f, 0.05f, 6.0f));
            InteractFrom(qa, Case01Ids.ShoppingList, new Vector3(1.1f, 0.05f, 7.3f));
            InteractFrom(qa, Case01Ids.SundayNote, new Vector3(8.6f, 0.05f, 8.8f));
            InteractFrom(qa, Case01Ids.Tea, new Vector3(8.7f, 0.05f, 7.9f));
            var lidBefore = qa.Layout.Decor.TeaTinOpen;
            InteractFrom(qa, Case01Ids.TeaTin, new Vector3(8.7f, 0.05f, 7.5f));
            Assert.AreNotEqual(lidBefore, qa.Layout.Decor.TeaTinOpen, "茶葉盒照樣能打開");
            InteractFrom(qa, Case01Ids.Map, new Vector3(6.7f, y, 8.2f));
            InteractFrom(qa, Case01Ids.EllieNotes, new Vector3(7.6f, y, 8.2f));
            InteractFrom(qa, Case01Ids.OldRecord, new Vector3(8.7f, y, 7.5f));
            InteractFrom(qa, Case01Ids.Postcard, new Vector3(1.4f, y, 8.9f));
            InteractFrom(qa, Case01Ids.TravelRoute, new Vector3(4.3f, y, 8.3f));
            InteractFrom(qa, Case01Ids.JobLetter, new Vector3(3.6f, y, 8.4f));
            InteractFrom(qa, Case01Ids.StorageDoor, new Vector3(6.0f, y, 2.5f));
            Assert.IsTrue(qa.Session.Has(Case01Flags.TableTwo));
            Assert.IsNotNull(qa.Session.FindJournal("shopping_list"));
            Assert.IsNotNull(qa.Session.FindJournal("travel_route"));
        }

        [Test]
        public void H1_DiningChangesStillHappen_ThroughTheSkinnedObjects()
        {
            var qa = DemoHouse();
            var d = qa.Layout.Decor;
            var art = qa.Layout.HouseArt;
            var chair = art.Pieces["ChairNorth"];
            var before = chair.position;
            d.ApplyDining(true, false);
            Assert.Greater(Vector3.Distance(before, chair.position), 0.3f, "新美術的北側餐椅跟著被拉開");
            Assert.IsFalse(d.TraySecondCup.activeSelf, "托盤第二個杯子消失");
            d.ApplyDining(true, true);
            Assert.Less(Vector3.Distance(before, chair.position), 0.01f, "校正後椅子歸位（新美術跟著回來）");
        }

        [Test]
        public void ToggleNewOld_SwapsAppearanceOnly()
        {
            var go = new GameObject("LivingQa");
            roots.Add(go);
            var qa = go.AddComponent<Case01HouseQA>();
            qa.Build(false, null, 1, null, Case01QaMode.LivingArt);
            var art = qa.Layout.HouseArt;
            Assert.IsNotNull(art, "客廳美術 QA 也套用其他房間");
            Assert.IsTrue(art.Active);
            qa.Tick(Dt, new Case01Input { Function = 7 });
            Assert.IsFalse(art.Active, "F7 一起切回舊版");
            Assert.IsTrue(art.Kit.Added.All(g => !g.activeSelf), "新模型全部隱藏");
            var oldCab = qa.Layout.Root.GetComponentsInChildren<Transform>(true).First(t => t.name == "ShoeCabinet");
            Assert.IsTrue(oldCab.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled), "舊鞋櫃外觀回來");
            qa.Tick(Dt, new Case01Input { Function = 7 });
            Assert.IsTrue(art.Active);
        }

        [Test]
        public void OtherQas_AndOriginalLayoutDemo_DoNotGetTheArt()
        {
            var go = new GameObject("Std");
            roots.Add(go);
            var std = go.AddComponent<Case01HouseQA>();
            std.Build(false, null, 1, null, null, Case01Floorplan.V05A);
            Assert.IsNull(std.Layout.HouseArt, "住宅 A/B QA 維持原樣");
            var go2 = new GameObject("DemoOriginal");
            roots.Add(go2);
            var d = go2.AddComponent<Case01DemoSlice>();
            d.Build(false, Case01DemoArea.House, Case01Floorplan.Original);
            Assert.IsNull(d.House.Layout.HouseArt, "Demo 原版格局不套用（正式格局仍待使用者決定）");
        }
    }
}
