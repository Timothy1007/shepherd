using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Battle.UI;
using Shepherd.Exploration.Case01;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// v0.6 客廳美術標準間：Blender 程序模型真的載入（不是 Primitive）、新舊切換只換顯示與碰撞、
    /// 合照／朋友卡片／夾便箋的書在兩版都能調查、H0→H2→H3 的客廳異變與燈光照常、走道不被新家具擋住、其他 QA 不受影響。
    /// </summary>
    public sealed class Case01LivingArtTests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();

        private Case01HouseQA NewQa(Case01QaMode mode = Case01QaMode.LivingArt)
        {
            var go = new GameObject("Case01LivingArtTest");
            roots.Add(go);
            var qa = go.AddComponent<Case01HouseQA>();
            qa.Build(false, null, 1, null, mode);
            return qa;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
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
            float stuck = 0f;
            for (float t = 0; t < max; t += Dt)
            {
                var p = qa.Player.transform.position;
                if (new Vector2(x - p.x, z - p.z).magnitude < 0.25f) return true;
                qa.Tick(Dt, Case01Input.From(new ExploreInput { Move = new Vector2(0f, 1f), Look = LookDelta(qa.Player, new Vector3(x, qa.Player.EyePosition.y, z), true) }));
                if ((qa.Player.transform.position - stuckAt).magnitude > 0.05f)
                {
                    stuckAt = qa.Player.transform.position;
                    stuck = 0f;
                }
                else if ((stuck += Dt) > 1.5f) return null;
            }
            return null;
        }

        private static void WalkTo(Case01HouseQA qa, float x, float z)
        {
            Assert.IsTrue(TryWalk(qa, x, z) == true, "走不到 (" + x + ", " + z + ")，停在 " + qa.Player.transform.position + "（新美術 " + qa.LivingArtOn + "）");
        }

        private static void Interact(Case01HouseQA qa, string id)
        {
            var it = qa.Layout.Interactables[id];
            var target = it.transform.TransformPoint(it.GetComponent<BoxCollider>().center);
            for (int i = 0; i < 2; i++) qa.Tick(Dt, Case01Input.From(new ExploreInput { Look = LookDelta(qa.Player, target, false) }));
            Assert.IsNotNull(qa.CurrentTarget, "準星對到 " + id + "（" + qa.Player.transform.position + "，新美術 " + qa.LivingArtOn + "）");
            Assert.AreEqual(id, qa.CurrentTarget.Id);
            qa.Tick(Dt, Case01Input.From(new ExploreInput { Interact = true }));
            if (qa.Hud.PanelVisible) qa.Tick(Dt, Case01Input.From(new ExploreInput { Interact = true }));
        }

        [Test]
        public void LivingArtQa_StartsInTheLivingRoom_WithRealModelledFurniture()
        {
            Assert.IsTrue(Case01LivingArt.AssetsAvailable, "美術資產在 Resources/Case01LivingRoom");
            var qa = NewQa();
            Assert.AreEqual(Case01Floorplan.V05A, qa.Layout.Floorplan);
            Assert.AreEqual(Case01Room.Living, qa.CurrentRoom, "直接站在客廳");
            var art = qa.Layout.LivingArt;
            Assert.IsNotNull(art);
            Assert.IsTrue(qa.LivingArtOn, "預設顯示新美術");
            StringAssert.Contains("客廳美術標準間", qa.Hud.Base.ViewMode.text);
            foreach (var name in new[] { "Sofa", "CoffeeTable", "Bookcase", "SashWindow", "Curtains", "TableLamp", "Rug", "Mantelpiece", "TvUnit", "PhotoFrame" })
            {
                Assert.IsTrue(art.Pieces.ContainsKey(name), name);
                var mfs = art.Pieces[name].GetComponentsInChildren<MeshFilter>().Where(m => m.name != "FriendCard" && m.name != "NoteTab").ToArray();
                Assert.Greater(mfs.Sum(m => m.sharedMesh.vertexCount), 100, name + " 是建模的網格，不是單一方塊");
                Assert.IsFalse(mfs.Any(m => m.sharedMesh.name == "Cube"), name + " 沒有用 Primitive");
            }
            Assert.Greater(art.Pieces["Sofa"].GetComponentsInChildren<MeshFilter>().Sum(m => m.sharedMesh.vertexCount), 2000, "沙發有扶手、座墊、靠墊的曲面");
            foreach (var r in art.Root.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    Assert.IsNotNull(m, r.name + " 沒有遺失材質");
                    Assert.IsTrue(m.shader.isSupported && m.shader.name != "Hidden/InternalErrorShader", r.name + " 不是洋紅色錯誤 shader");
                    if (m.name.StartsWith("LR_") && m.name != "LR_Plastic" && m.name != "LR_Screen" && m.name != "LR_Ember" && m.name != "LR_Flower" && m.name != "LR_Glass")
                        Assert.IsNotNull(m.mainTexture, m.name + " 有繪畫感貼圖");
                }
            Assert.Greater(art.Books.Count, 40, "書放在層板上");
            Assert.Greater(art.WallPieces, 6, "牆面依現有牆段貼上壁紙");
        }

        [Test]
        public void ToggleOldNew_SwapsOnlyVisualsAndColliders_HotspotsStay()
        {
            var qa = NewQa();
            var art = qa.Layout.LivingArt;
            var old = qa.Layout.LivingOldRoot;
            Assert.IsTrue(art.Root.gameObject.activeSelf);
            Assert.IsFalse(old.gameObject.activeSelf, "新美術時舊家具（含碰撞）關閉");
            int artColliders = art.Root.GetComponentsInChildren<Collider>().Count(c => !c.isTrigger && c.enabled);
            Assert.GreaterOrEqual(artColliders, 7, "新家具有自己的碰撞");
            qa.Tick(Dt, new Case01Input { Function = 7 });
            Assert.IsFalse(qa.LivingArtOn);
            Assert.IsFalse(art.Root.gameObject.activeSelf);
            Assert.IsTrue(old.gameObject.activeSelf);
            StringAssert.Contains("舊版", qa.Hud.Base.ViewMode.text);
            foreach (var w in qa.Layout.LivingWindows)
                Assert.IsTrue(w.GetComponentsInChildren<Renderer>().Where(r => r.name != "Glass").All(r => r.enabled), "舊版窗框回來");
            foreach (var id in new[] { Case01Ids.Photo, Case01Ids.FriendCard, Case01Ids.LivingBookNote, Case01Ids.Calendar })
                Assert.IsTrue(qa.Layout.Interactables[id].gameObject.activeInHierarchy, id + " 熱區在兩版都存在");
            qa.Tick(Dt, new Case01Input { Function = 7 });
            Assert.IsTrue(qa.LivingArtOn);
            foreach (var w in qa.Layout.LivingWindows)
            {
                var glass = w.GetComponentsInChildren<Renderer>().First(r => r.name == "Glass");
                Assert.IsTrue(glass.enabled, "新美術保留原本的透明玻璃");
                Assert.IsTrue(w.GetComponentsInChildren<Renderer>().Where(r => r.name != "Glass").All(r => !r.enabled), "舊窗框改由上下拉窗模型取代");
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void LivingClues_AreInvestigable_InBothVersions(bool art)
        {
            var qa = NewQa();
            if (!art) qa.Tick(Dt, new Case01Input { Function = 7 });
            Assert.AreEqual(art, qa.LivingArtOn);
            WalkTo(qa, 4.7f, 1.4f);
            WalkTo(qa, 1.6f, 1.5f);
            Interact(qa, Case01Ids.Photo);
            Assert.IsTrue(qa.Session.Has(Case01Flags.PhotoObserved), "合照");
            WalkTo(qa, 2.2f, 1.9f);
            Interact(qa, Case01Ids.FriendCard);
            Assert.IsNotNull(qa.Session.FindJournal("friend_card"), "朋友卡片記入日誌");
            WalkTo(qa, 1.45f, 2.0f);
            WalkTo(qa, 1.25f, 4.6f);
            Interact(qa, Case01Ids.LivingBookNote);
            Assert.IsTrue(qa.Hud.NoteVisible, "夾便箋的書");
            Assert.AreEqual(Case01Texts.LivingBookNote, qa.Hud.NoteText.text);
            // 回到走道 → 餐廳（沙發東側走道與寬開口沒有被新家具擋住）。
            WalkTo(qa, 1.6f, 1.5f);
            WalkTo(qa, 4.7f, 1.4f);
            WalkTo(qa, 4.7f, 4.7f);
            WalkTo(qa, 3.4f, 5.05f);
            WalkTo(qa, 3.4f, 6.0f);
            Assert.AreEqual(Case01Room.Dining, qa.CurrentRoom, "走得到餐廳");
        }

        [Test]
        public void Stages_H0H2H3_KeepLivingChanges_FireGoesCold_ViewStays()
        {
            var qa = NewQa();
            var d = qa.Layout.Decor;
            var art = qa.Layout.LivingArt;
            Assert.AreEqual(0, qa.HouseStage);
            qa.Tick(Dt, Case01Input.None);
            Assert.IsTrue(art.FireLight.enabled, "H0 爐火");
            Assert.IsTrue(d.LivingFramePhotoAlt.activeSelf, "H0 相框有照片（新美術版）");
            Assert.IsTrue(d.LivingMissingBooks.All(b => b.activeSelf));
            qa.Player.Teleport(new Vector3(3.0f, 0.05f, 1.6f), -60f, 6f);
            qa.Tick(Dt, new Case01Input { Function = 6 });
            Assert.AreEqual(2, qa.HouseStage, "F6 → H2");
            art = qa.Layout.LivingArt;
            d = qa.Layout.Decor;
            Assert.IsNotNull(art, "重建後仍是新美術");
            Assert.IsTrue(qa.LivingArtOn);
            Assert.Less(Vector3.Distance(qa.Player.transform.position, new Vector3(3.0f, 0.05f, 1.6f)), 0.05f, "同一個位置比較");
            Assert.AreEqual(-60f, Mathf.DeltaAngle(0f, qa.Player.Yaw), 0.5f, "同一個視角");
            qa.Tick(Dt, Case01Input.None);
            Assert.IsFalse(art.FireLight.enabled, "H2 爐火熄滅");
            Assert.IsTrue(d.LivingFrameBlankAlt.activeSelf, "H2 相框空白（新美術版也同步）");
            Assert.IsTrue(d.LivingMissingBooks.Where(b => art.Books.Contains(b)).All(b => !b.activeSelf), "H2 書櫃缺兩本（新美術版）");
            Assert.Greater(qa.Layout.Aging.VisibleCount(Case01Room.Living), 0, "舊化痕跡照常");
            qa.Tick(Dt, new Case01Input { Function = 6 });
            Assert.AreEqual(3, qa.HouseStage, "F6 → H3");
            qa.Tick(Dt, Case01Input.None);
            Assert.IsFalse(qa.Layout.LivingArt.FireLight.enabled, "H3 不回暖");
            Assert.IsTrue(qa.Layout.Decor.LivingFrameBlankAlt.activeSelf, "H3 殘留空白相框");
            qa.Tick(Dt, new Case01Input { Function = 6 });
            Assert.AreEqual(0, qa.HouseStage, "回到 H0");
            Assert.IsTrue(qa.Layout.LivingArt.LampLight != null && qa.Layout.LampLights.Contains(qa.Layout.LivingArt.LampLight), "桌燈交給 Case01Mood 依階段調整");
        }

        [Test]
        public void DoorwaysStayClear_AndF1DoesNotLeak()
        {
            var qa = NewQa();
            Assert.IsFalse(Physics.CheckCapsule(new Vector3(5.4f, 0.45f, 1.4f), new Vector3(5.4f, 1.45f, 1.4f), 0.3f, ~0, QueryTriggerInteraction.Ignore), "玄關→客廳開口");
            Assert.IsFalse(Physics.CheckCapsule(new Vector3(2.7f, 0.45f, 5.4f), new Vector3(2.7f, 1.45f, 5.4f), 0.3f, ~0, QueryTriggerInteraction.Ignore), "客廳→餐廳開口");
            Assert.IsFalse(Physics.CheckCapsule(new Vector3(4.7f, 0.45f, 3.0f), new Vector3(4.7f, 1.45f, 3.0f), 0.3f, ~0, QueryTriggerInteraction.Ignore), "沙發東側走道");
            qa.Tick(Dt, new Case01Input { Jump = 1 });
            Assert.AreEqual(Case01Room.Living, qa.CurrentRoom, "F1 回到客廳入口");
            Assert.AreEqual(1, Object.FindObjectsOfType<Transform>().Count(t => t.name == "LivingArtV06"), "重建不殘留");
        }

        [Test]
        public void OtherQas_AndDemo_DoNotBuildTheArtPass()
        {
            foreach (var mode in new[] { Case01QaMode.Standard, Case01QaMode.ArtPreview, Case01QaMode.NarrativeV04 })
            {
                var qa = NewQa(mode);
                Assert.IsNull(qa.Layout.LivingArt, mode + "：不套用新美術");
            }
            var go = new GameObject("ResQa");
            roots.Add(go);
            var res = go.AddComponent<Case01HouseQA>();
            res.Build(false, null, 1, null, null, Case01Floorplan.V05A);
            Assert.IsNull(res.Layout.LivingArt, "住宅 A/B QA 不套用");
            Assert.IsTrue(res.Layout.LivingOldRoot.gameObject.activeSelf, "原客廳照常");
        }
    }
}
