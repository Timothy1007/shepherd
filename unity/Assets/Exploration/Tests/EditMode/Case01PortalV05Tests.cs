using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Battle.UI;
using Shepherd.Exploration.Case01;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// v0.5 Phase 3：彩色訊號破損的失序門（RGB 膜＋流動／斷裂的邊界＋殘影），降低閃爍、校正後收斂、
    /// 獨立 Portal QA 可切 v0.4／v0.5；穿門判定、路由與互動不變，普通門不會變成彩色故障。
    /// </summary>
    public sealed class Case01PortalV05Tests
    {
        private const float Dt = 1f / 30f;
        private readonly List<GameObject> roots = new List<GameObject>();

        private Case01HouseQA NewQa(Case01Checkpoint? cp = null, Case01QaMode? mode = null)
        {
            var go = new GameObject("Case01PortalV05Test");
            roots.Add(go);
            var qa = go.AddComponent<Case01HouseQA>();
            qa.Build(false, null, 1, cp, mode);
            return qa;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
        }

        private static void Run(Case01HouseQA qa, float s)
        {
            for (float t = 0; t < s; t += Dt) qa.Tick(Dt, Case01Input.None);
        }

        [Test]
        public void WrongStudyDoor_IsColourGlitch_WithFlowingBrokenEdge()
        {
            var qa = NewQa(Case01Checkpoint.S30);
            Assert.AreEqual(Case01PortalStyle.V05, qa.PortalStyle, "預設 v0.5");
            var p = qa.Layout.Portal(Case01Door.StudyDoor);
            Run(qa, 0.2f);
            Assert.AreEqual(Case01PortalState.Wrong, qa.PortalState(Case01Door.StudyDoor));
            var mat = p.Membrane.GetComponent<Renderer>().sharedMaterial;
            Assert.AreEqual("Shepherd/Case01PortalGlitchV05", mat.shader.name, "彩色訊號破損 shader");
            Assert.AreEqual(1f, mat.GetFloat("_Sat"), 1e-3, "錯接：完整色彩");
            Assert.AreEqual(1f, mat.GetFloat("_Glitch"), 1e-3);
            Assert.IsTrue(p.Edge.Root.gameObject.activeInHierarchy, "邊界框出現");
            Assert.AreEqual(2 * Case01PortalEdge.JambSegments + Case01PortalEdge.HeadSegments, p.Edge.SegmentCount);
            int maxGaps = 0, maxColors = 0;
            float maxOffset = 0f;
            for (int i = 0; i < 90; i++)
            {
                qa.Tick(Dt, Case01Input.None);
                maxGaps = Mathf.Max(maxGaps, p.Edge.Gaps);
                maxColors = Mathf.Max(maxColors, p.Edge.ColorsInUse);
                maxOffset = Mathf.Max(maxOffset, p.Edge.MaxOffset);
            }
            Assert.Greater(maxGaps, 0, "邊界會短暫斷裂");
            Assert.GreaterOrEqual(maxColors, 3, "紅綠藍紫青等色偏");
            Assert.Greater(maxOffset, 0.02f, "邊界局部扭曲、流動（不是固定矩形）");
            Assert.AreEqual(0, p.Edge.Root.GetComponentsInChildren<Collider>(true).Length, "邊界沒有碰撞體");
            Assert.IsNull(p.Membrane.GetComponent<Collider>(), "膜不擋路");
        }

        [Test]
        public void GlitchShaders_CompileWithoutErrors()
        {
            foreach (var s in new[] { Case01Glitch.ShaderV05, Case01Glitch.Shader })
            {
                Assert.IsNotNull(s);
                Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(s), s.name + " 沒有編譯錯誤（否則會變成整片洋紅色）");
            }
        }

        [Test]
        public void Traversal_AndRouting_Unchanged()
        {
            var qa = NewQa(Case01Checkpoint.S30);
            var p = qa.Layout.Portal(Case01Door.StudyDoor);
            Assert.AreEqual(Case01HouseBuilder.DoorWidth * 0.5f, p.HalfWidth, 1e-4, "傳送平面寬度不變");
            qa.Player.Teleport(new Vector3(Case01HouseBuilder.StudyDoorX, Case01HouseBuilder.FloorHeight + 0.05f, 3.6f), 0f);
            for (int i = 0; i < 200 && qa.CurrentRoom != Case01Room.Dining; i++) qa.Tick(Dt, Case01Input.From(ExploreInput.Forward()));
            Assert.AreEqual(Case01Room.Dining, qa.CurrentRoom, "書房門仍錯接到餐廳");
        }

        [Test]
        public void ReduceFlicker_IsSlowerGreyer_AndHasNoGapsOrJumps()
        {
            var qa = NewQa(Case01Checkpoint.S30);
            qa.ReduceFlicker = true;
            var p = qa.Layout.Portal(Case01Door.StudyDoor);
            float maxOffset = 0f;
            for (int i = 0; i < 90; i++)
            {
                qa.Tick(Dt, Case01Input.None);
                Assert.AreEqual(0, p.Edge.Gaps, "降低閃爍：沒有缺口");
                maxOffset = Mathf.Max(maxOffset, p.Edge.MaxOffset);
            }
            Assert.LessOrEqual(maxOffset, 0.061f, "只有緩慢流動，沒有跳格");
            var mat = p.MembraneMatV05;
            Assert.LessOrEqual(mat.GetFloat("_Sat"), 0.35f + 1e-3, "色彩較低飽和");
            Assert.Less(mat.GetFloat("_Speed"), 0.5f, "動態較慢");
        }

        [Test]
        public void AfterCorrection_EdgeSettles_ToStableGreyFrame()
        {
            var qa = NewQa(null, Case01QaMode.PortalV05);
            qa.PreviewGlitch(4);
            Run(qa, 0.5f);
            var p = qa.Layout.Portal(Case01Door.MezzTriangle);
            Assert.AreEqual(Case01PortalState.Corrected, qa.PortalState(Case01Door.MezzTriangle));
            for (int i = 0; i < 30; i++)
            {
                qa.Tick(Dt, Case01Input.None);
                Assert.AreEqual(0, p.Edge.Gaps);
                Assert.AreEqual(0f, p.Edge.MaxOffset, 1e-5, "校正後邊界對齊、不動");
            }
            Assert.AreEqual(0f, p.MembraneMatV05.GetFloat("_Glitch"), 1e-3, "故障停止");
            Assert.AreEqual(0f, p.MembraneMatV05.GetFloat("_Sat"), 1e-3, "不再彩閃");
        }

        [Test]
        public void PortalQa_StartsAtTheDoor_AndTogglesV04V05_AndStates()
        {
            var qa = NewQa(null, Case01QaMode.PortalV05);
            Assert.AreEqual(0, qa.GlitchPreview, "直接站到錯接的書房門前");
            StringAssert.Contains("Portal v0.5 QA", qa.Hud.Base.ViewMode.text);
            var p = qa.Layout.Portal(Case01Door.StudyDoor);
            Run(qa, 0.2f);
            Assert.IsTrue(p.Edge.Root.gameObject.activeSelf);
            qa.Tick(Dt, new Case01Input { Function = 7 });
            Run(qa, 0.1f);
            Assert.AreEqual(Case01PortalStyle.V04, qa.PortalStyle);
            Assert.AreSame(p.MembraneMat, p.Membrane.GetComponent<Renderer>().sharedMaterial, "同一扇門換回 v0.4 膜");
            Assert.IsFalse(p.Edge.Root.gameObject.activeSelf, "v0.4 沒有彩色邊界");
            StringAssert.Contains("v0.4", qa.Hud.Base.ViewMode.text);
            qa.Tick(Dt, new Case01Input { Function = 7 });
            Run(qa, 0.1f);
            Assert.AreSame(p.MembraneMatV05, p.Membrane.GetComponent<Renderer>().sharedMaterial);
            qa.Tick(Dt, new Case01Input { Function = 6 });
            Assert.AreEqual(1, qa.GlitchPreview, "F6 切下一個狀態");
            Assert.AreEqual(Case01PortalStyle.V05, qa.PortalStyle, "切狀態不會改外觀版本");
        }

        [Test]
        public void DormantDoors_AndOrdinaryDoors_HaveNoColourGlitch()
        {
            var qa = NewQa();
            Run(qa, 0.2f);
            foreach (var p in qa.Layout.Portals)
                if (qa.PortalState(p.Door) == Case01PortalState.Dormant)
                    Assert.IsFalse(p.Edge.Root.gameObject.activeSelf, p.Door + "：未啟動的門沒有故障邊界");
            int edges = qa.Layout.Root.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("PortalEdgeV05_"));
            Assert.AreEqual(qa.Layout.Portals.Count, edges, "只有失序門有邊界框（普通門沒有）");
        }

        [Test]
        public void ThirdDoorFrameHint_StillAppearsBeforeThePortalOpens()
        {
            var qa = NewQa(Case01Checkpoint.S30);
            Run(qa, 0.2f);
            var third = qa.Layout.Portal(Case01Door.ThirdDoor);
            if (qa.Layout.ThirdDoorFrame.activeSelf && !third.Membrane.activeSelf)
                Assert.IsTrue(third.FrameRenderers.All(r => r.enabled), "門還沒打開時，浮現的門框照樣看得到");
        }
    }
}
