using System.Collections.Generic;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// Demo Phase 1 品質回歸 QA：F2 街上看 17 號、F3 進 17 號、F4 依序走過每個房間與異常門／夾層／形成演出、F6 H0→H3、F1 事務所；
    /// 正式 Demo 不處理這些鍵、不顯示 QA 字樣。
    /// </summary>
    public sealed class Case01Phase1QaTests
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

        private Case01DemoSlice NewDemo(bool qa)
        {
            var go = new GameObject("Case01Phase1Qa");
            roots.Add(go);
            var d = go.AddComponent<Case01DemoSlice>();
            d.QaMode = qa;
            d.Build(false, Case01DemoArea.Street, Case01Floorplan.V05A);
            return d;
        }

        private static void Settle(Case01DemoSlice d)
        {
            for (int i = 0; i < 120 && d.Transitioning; i++) d.Tick(Dt, Case01Input.None);
            d.Tick(Dt, Case01Input.None);
        }

        [Test]
        public void Phase1Qa_StartsFacing17_ThenWalksThroughEveryHouseView()
        {
            var d = NewDemo(true);
            Assert.AreEqual(Case01DemoArea.Street, d.Area);
            Assert.Less(Vector3.Distance(d.Player.transform.position, new Vector3(5.5f, 0.05f, -9.5f)), 0.2f, "從對街看 17 號");
            StringAssert.Contains("品質回歸 QA", d.Hud.Base.ViewMode.text);
            d.Tick(Dt, new Case01Input { Jump = 4 });
            StringAssert.Contains("街區", d.Hud.Base.ViewMode.text);
            d.Tick(Dt, new Case01Input { Jump = 3 });
            Settle(d);
            Assert.AreEqual(Case01DemoArea.House, d.Area, "F3 進 17 號");
            StringAssert.Contains("品質回歸 QA", d.House.Hud.Base.ViewMode.text, "屋內也顯示 QA 快捷鍵（只在 QA）");
            for (int i = 0; i < Case01DemoSlice.HouseViews.Length; i++)
            {
                d.Tick(Dt, new Case01Input { Jump = 4 });
                d.Tick(Dt, Case01Input.None);
                var v = Case01DemoSlice.HouseViews[i];
                Assert.AreEqual(i, d.QaHouseView);
                Assert.AreEqual(v.Room, d.House.CurrentRoom, v.Name + " 站在正確的房間");
                StringAssert.Contains(v.Name, d.House.Hud.Base.ViewMode.text);
                if (v.Kind == 1 && v.Room == Case01Room.Landing)
                {
                    var third = d.House.Layout.Portal(Case01Door.ThirdDoor);
                    Assert.IsTrue(third.Membrane.activeSelf && third.Backdrop.activeSelf, "第三扇門開啟、門後是虛空");
                }
                if (v.Kind == 2) Assert.AreEqual(Case01Mode.Manifesting, d.House.Mode, "形成演出");
            }
        }

        [Test]
        public void Phase1Qa_F6CyclesStages_F1Office_F2BackToThe17View()
        {
            var d = NewDemo(true);
            d.Tick(Dt, new Case01Input { Jump = 3 });
            Settle(d);
            d.Tick(Dt, new Case01Input { Jump = 4 });
            d.Tick(Dt, new Case01Input { Jump = 4 });
            Assert.AreEqual(Case01Room.Living, d.House.CurrentRoom);
            for (int h = 1; h <= 3; h++)
            {
                d.Tick(Dt, new Case01Input { Function = 6 });
                Assert.AreEqual(h, d.House.HouseStage, "F6 → H" + h);
                Assert.AreEqual(Case01Room.Living, d.House.CurrentRoom, "停在同一個視點");
            }
            d.Tick(Dt, new Case01Input { Jump = 1 });
            Settle(d);
            Assert.AreEqual(Case01DemoArea.Office, d.Area, "F1 事務所");
            Assert.IsNull(d.House, "離開時屋內場景移除");
            d.Tick(Dt, new Case01Input { Jump = 2 });
            Settle(d);
            Assert.AreEqual(Case01DemoArea.Street, d.Area);
            Assert.Less(Vector3.Distance(d.Player.transform.position, new Vector3(5.5f, 0.05f, -9.5f)), 0.2f, "F2 回到看 17 號的位置");
        }

        [Test]
        public void NormalDemo_IgnoresQaKeys_AndShowsNoQaText()
        {
            var d = NewDemo(false);
            Assert.AreEqual("", d.Hud.Base.ViewMode.text);
            d.Tick(Dt, new Case01Input { Jump = 1 });
            d.Tick(Dt, new Case01Input { Jump = 3 });
            Settle(d);
            Assert.AreEqual(Case01DemoArea.Street, d.Area, "正式 Demo 不處理 F 鍵");
        }
    }
}
