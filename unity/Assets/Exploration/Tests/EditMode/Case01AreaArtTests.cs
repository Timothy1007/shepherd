using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// v0.7 Phase 1：事務所與街區的美術換皮。模型確實載入、不新增碰撞、熱區照舊、碰撞處看得到牆（沒有隱形牆）、
    /// 十七號二樓正面沒有任何門／窗形細節（不在屋外先透露異常門）、新舊切換還原材質與位置、品質回歸 QA 的 F7、屋內窗外同一套外景。
    /// </summary>
    public sealed class Case01AreaArtTests
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

        private Case01DemoSlice Demo(Case01DemoArea area, Case01Floorplan plan = Case01Floorplan.V05A)
        {
            var go = new GameObject("Case01AreaArtTest");
            roots.Add(go);
            var d = go.AddComponent<Case01DemoSlice>();
            d.Build(false, area, plan);
            return d;
        }

        private static void AssertModelled(Case01AreaArt art, string key)
        {
            Assert.IsTrue(art.Pieces.ContainsKey(key) && art.Pieces[key] != null, key);
            var mfs = art.Pieces[key].GetComponentsInChildren<MeshFilter>(true);
            Assert.Greater(mfs.Sum(m => m.sharedMesh.vertexCount), 60, key + " 是建模網格");
            Assert.IsFalse(mfs.Any(m => m.sharedMesh.name == "Cube"), key + " 不是 Primitive");
        }

        private static void AssertNoAddedColliders(Case01AreaArt art)
        {
            Assert.AreEqual(0, art.Root.GetComponentsInChildren<Collider>(true).Length, "美術根物件沒有碰撞");
            foreach (var g in art.Kit.Added) Assert.AreEqual(0, g.GetComponentsInChildren<Collider>(true).Length, g.name + " 沒有新增碰撞");
        }

        [Test]
        public void Office_HasModelledFurniture_KeepsHotspots_AndAddsNoColliders()
        {
            var d = Demo(Case01DemoArea.Office);
            var art = d.AreaLayout.Art;
            Assert.IsNotNull(art, "事務所有 Phase 1 美術");
            Assert.IsTrue(art.Active);
            foreach (var key in new[] { "Desk", "DeskLamp", "DeskChair", "ClientChair0", "ClientChair1", "FilingCabinet0", "FilingCabinet2", "OfficeDoor" }) AssertModelled(art, key);
            AssertNoAddedColliders(art);
            foreach (var id in new[] { Case01DemoIds.OfficeCase, Case01DemoIds.OfficeBoard, Case01DemoIds.OfficeCabinet, Case01DemoIds.OfficeDoor })
            {
                var c = d.AreaLayout.Interactables[id].GetComponent<BoxCollider>();
                Assert.IsTrue(c.enabled && c.isTrigger, id + " 熱區照舊");
            }
            // 委託資料夾仍看得到（換皮保留桌上的物件）。
            var folder = d.AreaLayout.Root.GetComponentsInChildren<Transform>(true).First(t => t.name == "CaseFolder");
            Assert.IsTrue(folder.GetComponent<Renderer>().enabled, "桌上的委託資料夾仍可見");
            var deskBlock = d.AreaLayout.Root.GetComponentsInChildren<Transform>(true).First(t => t.name == "DeskBlock");
            Assert.IsNotNull(deskBlock.GetComponent<BoxCollider>(), "書桌碰撞保留");
        }

        [Test]
        public void Street_HasFacadeModules_IronLamps_AndNoPortalHint()
        {
            var d = Demo(Case01DemoArea.Street);
            var art = d.AreaLayout.Art;
            Assert.IsNotNull(art, "街區有 Phase 1 美術");
            foreach (var key in new[] { "Front17", "TerraceFront", "LampPost", "Railing0" }) AssertModelled(art, key);
            var roof = art.Pieces["Roof17"].GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(2, roof.subMeshCount, "雙坡屋頂：石板瓦坡面＋磚造山牆");
            Assert.Greater(roof.bounds.size.y, 2f, "屋頂有坡度（不是平板）");
            Assert.AreEqual("Front17A", art.Pieces["Front17"].name, "方案 A 的十七號立面（兩扇一樓窗）");
            AssertNoAddedColliders(art);
            // 十七號二樓正面（y 3.3～5.9）沒有任何新的門／窗形細節；也沒有任何紫色材質。
            var h17 = d.AreaLayout.Root.Find("House17");
            foreach (var mf in h17.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = mf.GetComponent<Renderer>();
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || mf.name == "Body" || mf.name == "RoofArt") continue;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var w = mf.transform.TransformPoint(v);
                    if (w.z > 0.02f) continue;
                    Assert.IsFalse(w.y > 3.3f && w.y < 5.9f, mf.name + " 在十七號二樓正面有細節 " + w);
                }
            }
            foreach (var r in d.AreaLayout.Root.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    Assert.IsNotNull(m, r.name + " 材質正常");
                    var c = m.HasProperty("_Color") ? m.color : Color.white;
                    Assert.IsFalse(c.b > 0.45f && c.r > 0.35f && c.g < 0.3f, r.name + " 沒有紫色（異常）材質");
                }
            // 路面不再是帶紫紋的中庭石材。
            var pave = d.AreaLayout.Exterior.Root.Find("Pavement").GetComponent<Renderer>();
            StringAssert.StartsWith("P1_Pavement", pave.sharedMaterial.name);
        }

        [Test]
        public void Street_EveryWallCollider_HasAVisibleWall()
        {
            var d = Demo(Case01DemoArea.Street);
            var blocks = d.AreaLayout.Root.GetComponentsInChildren<BoxCollider>(true).Where(b => b.name == "FrontWallBlock").ToList();
            Assert.AreEqual(4, blocks.Count);
            var visible = d.AreaLayout.Root.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.enabled && (r.name == "FrontWall" || r.name == "NeighbourWall")).Select(r => r.bounds).ToList();
            foreach (var b in blocks)
            {
                var c = b.transform.TransformPoint(b.center);
                Assert.IsTrue(visible.Any(v => v.Contains(new Vector3(c.x, 0.3f, c.z))), "矮牆碰撞 " + c + " 有看得到的牆");
            }
        }

        [Test]
        public void ToggleOld_RestoresMaterialsAndWindowPositions()
        {
            var d = Demo(Case01DemoArea.Street);
            var art = d.AreaLayout.Art;
            var win = d.AreaLayout.Root.Find("House17").Cast<Transform>().First(t => t.name == "Window17");
            var pave = d.AreaLayout.Exterior.Root.Find("Pavement").GetComponent<Renderer>();
            var newPos = win.localPosition;
            art.SetActive(false);
            Assert.AreNotEqual(newPos, win.localPosition, "舊版的窗回到原位");
            Assert.IsFalse(pave.sharedMaterial.name.StartsWith("P1_"), "舊版材質");
            Assert.IsTrue(art.Kit.Added.All(g => !g.activeSelf), "新模型全部隱藏");
            art.SetActive(true);
            Assert.AreEqual(newPos, win.localPosition);
            StringAssert.StartsWith("P1_Pavement", pave.sharedMaterial.name);
        }

        [Test]
        public void QaF7_TogglesArt_AcrossAreas()
        {
            var go = new GameObject("QaDemo");
            roots.Add(go);
            var d = go.AddComponent<Case01DemoSlice>();
            d.QaMode = true;
            d.Build(false, Case01DemoArea.Street, Case01Floorplan.V05A);
            Assert.IsTrue(d.ArtOn);
            d.Tick(Dt, new Case01Input { Function = 7 });
            Assert.IsFalse(d.ArtOn, "F7 切到舊版");
            Assert.IsFalse(d.AreaLayout.Art.Active);
            StringAssert.Contains("舊版", d.QaBanner());
            d.Tick(Dt, new Case01Input { Jump = 1 });
            for (int i = 0; i < 90 && d.Transitioning; i++) d.Tick(Dt, Case01Input.None);
            Assert.AreEqual(Case01DemoArea.Office, d.Area);
            Assert.IsFalse(d.AreaLayout.Art.Active, "換區域後維持舊版");
            d.Tick(Dt, new Case01Input { Jump = 3 });
            for (int i = 0; i < 90 && d.Transitioning; i++) d.Tick(Dt, Case01Input.None);
            Assert.IsFalse(d.House.Layout.HouseArt.Active, "屋內也是舊版");
            d.Tick(Dt, new Case01Input { Function = 7 });
            Assert.IsTrue(d.House.Layout.HouseArt.Active && d.House.Layout.LivingArt.Active, "F7 切回新美術");
        }

        [Test]
        public void HouseWindows_SeeTheSameDressedStreet()
        {
            var d = Demo(Case01DemoArea.House);
            var art = d.House.Layout.HouseArt;
            Assert.IsTrue(art.Pieces.ContainsKey("TerraceFront") && art.Pieces["TerraceFront"] != null, "窗外對面的聯排住宅有立面模組");
            Assert.IsTrue(art.Pieces.ContainsKey("LampPost") && art.Pieces["LampPost"] != null, "窗外路燈是鑄鐵燈柱");
            Assert.AreEqual(Case01HouseBuilder.OutdoorLayer, art.Pieces["LampPost"].gameObject.layer, "外景模型在室外圖層（依房間裁切）");
        }

        [Test]
        public void OriginalFloorplanDemo_StreetUsesTheOriginalFront17()
        {
            var d = Demo(Case01DemoArea.Street, Case01Floorplan.Original);
            Assert.AreEqual("Front17O", d.AreaLayout.Art.Pieces["Front17"].name, "原版格局：一扇一樓前窗");
        }
    }
}
