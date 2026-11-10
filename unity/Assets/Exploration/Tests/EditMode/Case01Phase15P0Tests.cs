using System.Collections.Generic;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// Phase 1.5 P0（在 Phase 1.7 開工前修）：游標鎖定與實際狀態每影格對齊、只有真的鎖定才用滑鼠轉視角；
    /// 路牌／門牌／郵遞名冊／計程車牌的文字落在牌面內、路牌背面不是反字；第三扇門的虛空背板擋住所有斜角視線。
    /// 自動化測不到 Windows 游標碰螢幕邊緣的實際手感，仍需真人在 Game View／Standalone 驗收。
    /// </summary>
    public sealed class Case01Phase15P0Tests
    {
        private readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r);
            roots.Clear();
            Case01RunState.Reset();
        }

        [Test]
        public void Cursor_LockFollowsWhatTheGameWants_AndNeverFightsLostFocus()
        {
            Assert.AreEqual(CursorLockMode.Locked, Case01Cursor.Desired(true, true, CursorLockMode.None), "想鎖但實際沒鎖（例如屋內 OnDestroy 晚一影格放開）→ 重新鎖定");
            Assert.IsNull(Case01Cursor.Desired(true, true, CursorLockMode.Locked), "已鎖定：不動");
            Assert.IsNull(Case01Cursor.Desired(true, false, CursorLockMode.None), "失焦（Alt-Tab）時不搶");
            Assert.AreEqual(CursorLockMode.None, Case01Cursor.Desired(false, true, CursorLockMode.Locked), "Esc／面板／戰鬥：放開");
            Assert.IsNull(Case01Cursor.Desired(false, false, CursorLockMode.None));
            Assert.IsFalse(Case01Cursor.Looking(false), "沒有要鎖定時，滑鼠位移不轉視角");
            Assert.IsTrue(Case01Cursor.Looking(true), "EditMode／batchmode 測試照舊轉視角");
        }

        private static Bounds PlateBounds(Transform group, string name)
        {
            foreach (var r in group.GetComponentsInChildren<Renderer>(true)) if (r.name == name) return r.bounds;
            Assert.Fail("找不到 " + name);
            return new Bounds();
        }

        private static void AssertInside(Text t, Bounds plate, string what, bool front)
        {
            Assert.Greater(t.preferredWidth, 5f, what + "：文字有實際尺寸");
            var corners = new Vector3[4];
            ((RectTransform)t.canvas.transform).GetWorldCorners(corners);
            foreach (var c in corners)
            {
                Assert.That(c.x, Is.InRange(plate.min.x - 0.001f, plate.max.x + 0.001f), what + "：文字左右在牌面內");
                Assert.That(c.y, Is.InRange(plate.min.y - 0.001f, plate.max.y + 0.001f), what + "：文字上下在牌面內");
                if (front) Assert.Less(c.z, plate.min.z, what + "：在牌面前方（不 Z-fighting）");
                else Assert.Greater(c.z, plate.max.z, what + "：在牌面後方");
            }
        }

        [Test]
        public void StreetSign_Plates_Listing_Taxi_TextStaysInsideTheBoards()
        {
            var go = new GameObject("Phase15Street");
            roots.Add(go);
            var d = go.AddComponent<Case01DemoSlice>();
            d.Build(false, Case01DemoArea.Street, Case01Floorplan.Original);
            var L = d.AreaLayout;
            var root = go.transform;
            Transform sign = null, listing = null, taxi = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "StreetSign") sign = t;
                if (t.name == "PostalListing") listing = t;
                if (t.name == "TaxiStop") taxi = t;
            }
            Assert.AreEqual(2, L.SignLabels.Count, "路牌前後兩面都有字");
            var plate = PlateBounds(sign, "Plate");
            AssertInside(L.SignLabels[0], plate, "路牌正面", true);
            AssertInside(L.SignLabels[1], plate, "路牌背面", false);
            AssertInside(L.ListingLabel, PlateBounds(listing, "List"), "郵遞名冊", true);
            AssertInside(L.TaxiLabel, PlateBounds(taxi, "Sign"), "計程車牌", true);
            foreach (var id in new[] { Case01DemoIds.Plate16, Case01DemoIds.Plate17, Case01DemoIds.Plate18 })
            {
                var label = L.Interactables[id].GetComponentInChildren<Text>();
                Transform plateT = null;
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == "Plate" + label.text) plateT = t;
                AssertInside(label, plateT.GetComponent<Renderer>().bounds, "門牌 " + label.text, true);
            }
        }

        [Test]
        public void ThirdDoorVoid_CatchesEveryRayThroughTheOpening_AtObliqueAngles()
        {
            var go = new GameObject("Phase15Portal");
            roots.Add(go);
            var qa = go.AddComponent<Case01HouseQA>();
            qa.Build(false, null, 1, Case01Checkpoint.S40, null, Case01Floorplan.V05A);
            qa.Tick(1f / 30f, Case01Input.None);
            foreach (var door in new[] { Case01Door.ThirdDoor, Case01Door.StudyDoor })
            {
                var p = qa.Layout.Portal(door);
                var back = p.Backdrop.transform;
                float halfW = back.lossyScale.x * 0.5f, halfH = back.lossyScale.y * 0.5f;
                float membrane = Vector3.Dot(p.Membrane.transform.position - p.PlanePoint, p.Normal);
                float behind = Vector3.Dot(back.position - p.PlanePoint, p.Normal);
                Assert.Greater(behind, membrane, door + "：背板在膜後面");
                Assert.GreaterOrEqual(behind, Case01HouseBuilder.WallThickness * 0.5f, door + "：背板在牆的另一面（視線不會從牆厚裡漏出去）");
                int rays = 0;
                // 從來源側不同距離、不同角度（正面到 80°）看向門洞內任一點。
                foreach (float dist in new[] { 0.25f, 0.6f, 1.5f, 3f })
                foreach (float ang in new[] { 0f, 30f, 50f, 65f, 80f })
                foreach (float side in new[] { -1f, 1f })
                foreach (float eyeY in new[] { 0.6f, 1.65f, 2.0f })
                {
                    var dir = Quaternion.AngleAxis(side * ang, Vector3.up) * p.Normal;
                    var eye = p.PlanePoint - dir * dist + Vector3.up * eyeY;
                    for (float u = -0.5f; u <= 0.5f; u += 0.25f)
                    for (float v = 0.1f; v <= 2.1f; v += 0.5f)
                    {
                        // 門洞在牆的另一面（出口）的那一點：視線一定經過它才看得到門後。
                        var exit = p.PlanePoint + p.Right * (u * Case01HouseBuilder.DoorWidth) + Vector3.up * v + p.Normal * (Case01HouseBuilder.WallThickness * 0.5f);
                        var ray = (exit - eye).normalized;
                        float denom = Vector3.Dot(ray, p.Normal);
                        if (denom <= 0.01f) continue;
                        float t = Vector3.Dot(back.position - eye, p.Normal) / denom;
                        var hit = eye + ray * t - back.position;
                        Assert.LessOrEqual(Mathf.Abs(Vector3.Dot(hit, p.Right)), halfW, door + " 視線從背板左右漏出（距離 " + dist + "、角度 " + ang + "）");
                        Assert.LessOrEqual(Mathf.Abs(hit.y), halfH, door + " 視線從背板上下漏出");
                        rays++;
                    }
                }
                Assert.Greater(rays, 400);
            }
            Assert.IsTrue(qa.Layout.Portal(Case01Door.ThirdDoor).Backdrop.activeSelf, "S40：第三扇門開著，背板在");
        }
    }
}
