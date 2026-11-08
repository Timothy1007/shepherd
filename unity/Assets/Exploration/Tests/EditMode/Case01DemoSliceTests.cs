using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shepherd.Battle.UI;
using Shepherd.Battle.UI.Arena;
using Shepherd.Exploration.Case01;
using UnityEngine;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// v0.5 Phase 4：事務所 → 街區 → 十七號 → 街區 → 事務所 的最小閉環；跨區域保留委託、屋內進度（修核）、FP-001；
    /// 正式遊玩沒有 QA 快捷鍵；街區不開戰；房屋獨立 QA 不受影響。
    /// </summary>
    public sealed class Case01DemoSliceTests
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

        private Case01DemoSlice NewDemo(Case01DemoArea start = Case01DemoArea.Office, Case01Floorplan plan = Case01Floorplan.Original)
        {
            var go = new GameObject("Case01DemoSliceTest");
            roots.Add(go);
            var d = go.AddComponent<Case01DemoSlice>();
            d.Build(false, start, plan);
            return d;
        }

        private static void Settle(Case01DemoSlice d)
        {
            for (int i = 0; i < 120 && d.Transitioning; i++) d.Tick(Dt, Case01Input.None);
            Assert.IsFalse(d.Transitioning, "轉場結束");
        }

        private static Vector2 LookDelta(FirstPersonController p, Vector3 target)
        {
            var dir = target - p.EyePosition;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;
            return new Vector2(Mathf.DeltaAngle(p.Yaw, yaw) / FirstPersonController.LookSensitivity, -(pitch - p.Pitch) / FirstPersonController.LookSensitivity);
        }

        /// <summary>站到熱區前方、轉頭對準、按 E（真正的準星判定）。</summary>
        private static void LookAndInteract(Case01DemoSlice d, string id, Vector3 standAt)
        {
            d.Player.Teleport(standAt, 0f);
            var it = d.AreaLayout.Interactables[id];
            var target = it.transform.TransformPoint(it.GetComponent<BoxCollider>().center);
            for (int i = 0; i < 2; i++) d.Tick(Dt, Case01Input.From(new ExploreInput { Look = LookDelta(d.Player, target) }));
            Assert.IsNotNull(d.CurrentTarget, "準星對到 " + id);
            Assert.AreEqual(id, d.CurrentTarget.Id);
            d.Tick(Dt, Case01Input.From(new ExploreInput { Interact = true }));
        }

        private static void HouseFrontDoor(Case01HouseQA house)
        {
            house.Player.Teleport(house.Layout.Spawn.Position + new Vector3(0f, 0f, 0.3f), 180f, 8f);
            house.Tick(Dt, Case01Input.None);
            Assert.AreEqual(Case01Ids.FrontDoor, house.CurrentTarget != null ? house.CurrentTarget.Id : null, "對著前門");
            house.Tick(Dt, Case01Input.From(new ExploreInput { Interact = true }));
        }

        [Test]
        public void FullLoop_Office_Street_House_Street_Office_KeepsProgress()
        {
            var d = NewDemo();
            Assert.AreEqual(Case01DemoArea.Office, d.Area);
            StringAssert.Contains("委託", d.ObjectiveText());

            // 事務所：還沒接委託不能出門；看桌上的委託（真正的準星互動）。
            d.Interact(Case01DemoIds.OfficeDoor);
            Assert.IsFalse(d.Transitioning, "沒接委託不能出門");
            LookAndInteract(d, Case01DemoIds.OfficeCase, new Vector3(4.5f, 0.05f, 2.85f));
            Assert.IsTrue(Case01RunState.CaseAccepted, "接下委託");
            StringAssert.Contains("十七號門", d.Hud.Base.InspectText.text);
            StringAssert.DoesNotContain("死", d.Hud.Base.InspectText.text, "不寫死艾琳的下落");
            d.Interact(Case01DemoIds.OfficeDoor);
            Settle(d);
            Assert.AreEqual(Case01DemoArea.Street, d.Area);

            // 街區：先查門牌／名冊，才進得去。
            d.Interact(Case01DemoIds.Door17);
            Assert.IsFalse(d.Transitioning, "還沒確認門牌");
            LookAndInteract(d, Case01DemoIds.StreetListing, new Vector3(-3.4f, 0.05f, -3.6f));
            Assert.IsTrue(Case01RunState.StreetSurveyed);
            StringAssert.Contains("沒有 17", d.Hud.Base.InspectText.text);
            d.Hud.HidePanel();
            Assert.AreEqual(0, Object.FindObjectsOfType<CombatArenaView>().Length, "街區不開戰");
            LookAndInteract(d, Case01DemoIds.Door17, new Vector3(7.1f, 0.05f, -1.4f));
            Settle(d);
            Assert.AreEqual(Case01DemoArea.House, d.Area);
            var house = d.House;
            Assert.IsNotNull(house);
            Assert.IsTrue(house.IsDemo);
            Assert.AreSame(Case01RunState.HouseFlow, house.Flow, "屋內流程存在 RunState");
            Assert.AreEqual(Case01Room.Entrance, house.CurrentRoom, "從十七號的前門進到玄關");

            // 屋內：調查餐桌後先回街上，再進來進度還在。
            house.Flow.Interact(Case01Ids.Table);
            Assert.IsTrue(house.Flow.Session.Has(Case01Flags.TableTwo));
            HouseFrontDoor(house);
            Settle(d);
            Assert.AreEqual(Case01DemoArea.Street, d.Area, "前門回到街上");
            Assert.Less(Vector3.Distance(d.Player.transform.position, new Vector3(7.1f, 0.05f, -1.0f)), 0.3f, "站在十七號門外");
            d.Interact(Case01DemoIds.StreetBack);
            Assert.IsFalse(d.Transitioning, "十七號還沒查清楚，不急著回事務所");
            d.Interact(Case01DemoIds.Door17);
            Settle(d);
            var flow = d.House.Flow;
            Assert.AreSame(Case01RunState.HouseFlow, flow);
            Assert.IsTrue(flow.Session.Has(Case01Flags.TableTwo), "再進屋，餐桌的觀察還在");

            // 完成屋內（以已校正、已拿 FP-001 的狀態代表；完整屋內流程另有測試）。
            var resolved = Case01Flow.ForHouseStage(3);
            Case01RunState.HouseFlow = resolved;
            if (resolved.FamilyPhotoAvailable) resolved.Interact(Case01Ids.EvidencePhoto);
            HouseFrontDoor(d.House);
            Settle(d);
            Assert.AreEqual(Case01DemoArea.Street, d.Area);
            Assert.IsTrue(Case01RunState.HouseResolved, "修核狀態跨區域保留");
            bool fp = Case01RunState.FamilyPhotoCollected;
            d.Interact(Case01DemoIds.Door17);
            Assert.IsFalse(d.Transitioning, "結束後不再重進");
            d.Interact(Case01DemoIds.StreetBack);
            Settle(d);
            Assert.AreEqual(Case01DemoArea.Office, d.Area, "回到事務所");
            Assert.AreEqual(fp, Case01RunState.FamilyPhotoCollected, "FP-001 狀態跨區域保留");
            StringAssert.Contains("結案", d.ObjectiveText());
            d.Interact(Case01DemoIds.OfficeCase);
            Assert.IsTrue(Case01RunState.CaseClosed);
            Assert.IsTrue(d.Ended);
            StringAssert.Contains("仍未確認", d.Hud.EndText.text, "結案也不寫死艾琳的下落");
            Assert.GreaterOrEqual(d.Transitions, 6);
        }

        [Test]
        public void DemoHouse_IgnoresQaKeys_AndHidesQaOverlay()
        {
            var d = NewDemo(Case01DemoArea.House);
            var house = d.House;
            Assert.IsTrue(house.IsDemo);
            var layoutBefore = house.Layout;
            d.Tick(Dt, new Case01Input { Jump = 3 });
            d.Tick(Dt, new Case01Input { Function = 6 });
            d.Tick(Dt, new Case01Input { Function = 9 });
            d.Tick(Dt, new Case01Input { Function = 5 });
            Assert.AreEqual(0, house.Jumps, "F1～F4 不作用");
            Assert.AreSame(layoutBefore, house.Layout, "沒有被 QA 鍵重建");
            Assert.AreEqual(Case01CorrectionMode.RevisedV03, house.Flow.Correction, "F5 不切換");
            Assert.AreEqual("", house.Hud.Base.ViewMode.text, "沒有 QA 快捷鍵說明");
            Assert.IsFalse(house.Hud.QaText.transform.parent.gameObject.activeSelf, "沒有 QA 狀態列");
            StringAssert.DoesNotContain("QA", house.Hud.Base.Objective.text);
            StringAssert.DoesNotContain("QA", house.Layout.Interactables[Case01Ids.FrontDoor].Prompt);
        }

        [Test]
        public void Street_17MatchesTheHouse_AndHasSignsPlatesListing()
        {
            var d = NewDemo(Case01DemoArea.Street, Case01Floorplan.V05A);
            var L = d.AreaLayout;
            foreach (var id in new[] { Case01DemoIds.StreetSign, Case01DemoIds.StreetListing, Case01DemoIds.Plate16, Case01DemoIds.Plate17, Case01DemoIds.Plate18,
                         Case01DemoIds.Door17, Case01DemoIds.StreetBack })
                Assert.IsTrue(L.Interactables.ContainsKey(id), id);
            var door = L.Root.GetComponentsInChildren<Transform>().First(t => t.name == "FrontDoor17");
            Assert.AreEqual(7.1f, door.position.x, 1e-3, "與屋內前門同一個 x");
            var wins = L.Root.GetComponentsInChildren<Transform>().Where(t => t.name == "Window17").Select(t => t.position.x).OrderBy(x => x).ToArray();
            CollectionAssert.AreEqual(new[] { 1.6f, 3.8f }, wins, "前窗位置與方案 A 的客廳一致");
            Assert.Greater(L.Lights.Count, 3, "路燈有實際光源");
            // 街區邊界：走不出巷子、進不了別人家。
            Assert.IsTrue(Physics.Raycast(new Vector3(5f, 1f, -1f), Vector3.forward, 2f), "立面有碰撞");
            Assert.IsTrue(Physics.Raycast(new Vector3(0f, 1f, -6f), Vector3.left, 14f), "西側邊界");
        }

        [Test]
        public void HouseStandaloneQa_IsUnaffected()
        {
            var go = new GameObject("StandaloneHouse");
            roots.Add(go);
            var qa = go.AddComponent<Case01HouseQA>();
            qa.Build(false);
            Assert.IsFalse(qa.IsDemo);
            StringAssert.Contains("QA", qa.Hud.Base.ViewMode.text);
            qa.Tick(Dt, new Case01Input { Jump = 3 });
            Assert.AreEqual(1, qa.Jumps, "獨立 QA 的 F 鍵照常");
        }

        [Test]
        public void OfficeAndStreet_StandaloneEntries_Work()
        {
            var o = NewDemo(Case01DemoArea.Office);
            Assert.AreEqual(Case01DemoArea.Office, o.Area);
            foreach (var id in new[] { Case01DemoIds.OfficeCase, Case01DemoIds.OfficeBoard, Case01DemoIds.OfficeCabinet, Case01DemoIds.OfficeDoor })
                Assert.IsTrue(o.AreaLayout.Interactables.ContainsKey(id), id);
            o.Interact(Case01DemoIds.OfficeBoard);
            Assert.IsTrue(o.Hud.PanelVisible);
            var s = NewDemo(Case01DemoArea.Street);
            Assert.AreEqual(Case01DemoArea.Street, s.Area);
            Assert.IsTrue(Case01RunState.CaseAccepted, "直接從街區開始：視為已接委託");
            // 在街上走一段（地面與碰撞正常）。
            var start = s.Player.transform.position;
            for (int i = 0; i < 60; i++) s.Tick(Dt, Case01Input.From(ExploreInput.Forward()));
            Assert.Greater(Vector3.Distance(start, s.Player.transform.position), 2f, "可以走");
            Assert.Less(Mathf.Abs(s.Player.transform.position.y - start.y), 0.2f, "沒有掉下去");
        }
    }
}
