using System.Collections;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// 沉浸式住宅探索 QA 的 Play 模式（真正的 Update／LateUpdate）：從前門走進玄關、開手電筒、走進客廳、按 E 拿起合照（經過輸入過濾：不能走）、
    /// E 放回、恢復行走；只有一個玩家與聽者。
    /// </summary>
    public sealed class Case01ImmersiveHousePlayModeTests
    {
        private sealed class ScriptedInput : ICase01InputSource
        {
            public Case01Input? Once;
            public Vector2 Move, Look;

            public Case01Input Read()
            {
                var r = Once ?? Case01Input.From(new ExploreInput { Move = Move, Look = Look });
                Once = null;
                Look = Vector2.zero;
                return r;
            }
        }

        private static Vector2 LookDelta(FirstPersonController p, Vector3 target, bool level)
        {
            var d = target - p.EyePosition;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = level ? 0f : -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            return new Vector2(Mathf.DeltaAngle(p.Yaw, yaw) / (FirstPersonController.LookSensitivity * p.LookScale), -(pitch - p.Pitch) / (FirstPersonController.LookSensitivity * p.LookScale));
        }

        private bool arrived;

        private IEnumerator Walk(Case01ImmersiveHouseQA s, ScriptedInput input, float x, float z, float max = 12f)
        {
            arrived = false;
            var p = s.House.Player;
            float t = 0f;
            while (t < max)
            {
                var pos = p.transform.position;
                var to = new Vector3(x - pos.x, 0f, z - pos.z);
                if (to.magnitude < 0.22f)
                {
                    arrived = true;
                    break;
                }
                var local = Quaternion.Inverse(Quaternion.Euler(0f, p.Yaw, 0f)) * to.normalized;
                input.Move = new Vector2(local.x, local.z);
                input.Look = LookDelta(p, new Vector3(x, p.EyePosition.y, z), true) * 0.4f;
                t += Time.deltaTime;
                yield return null;
            }
            input.Move = Vector2.zero;
            yield return null;
        }

        private static IEnumerator Wait(float sec)
        {
            float t = 0f;
            while (t < sec)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ImmersiveHouse_RealPlayMode_WalkIn_Flashlight_PickUpPhoto_PutBack()
        {
            // 其他 Play 模式測試可能在同一個測試場景留下還沒銷毀的房屋（同樣的世界座標）：先清掉，避免撞到別人的牆。
            foreach (var stray in Object.FindObjectsOfType<Case01HouseQA>()) Object.Destroy(stray.transform.root.gameObject);
            yield return null;
            var go = new GameObject("Case01ImmersiveHouseQA");
            try
            {
                var s = go.AddComponent<Case01ImmersiveHouseQA>();
                var input = new ScriptedInput();
                s.UseInput(input);
                yield return null;
                yield return null;
                var p = s.House.Player;
                Assert.AreEqual(Case01Floorplan.R3, s.House.Layout.Floorplan);
                yield return Walk(s, input, 7.1f, 2.0f);
                Assert.IsTrue(arrived, "從前門走進玄關");
                s.ToggleFlashlight();
                yield return Wait(0.3f);
                Assert.IsTrue(s.Flashlight.Light.enabled, "手電筒在 Play 中亮起");
                yield return Walk(s, input, 7.6f, 3.6f);
                var door = s.Host.World.Door("door_living");
                if (!door.IsOpenEnough)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        input.Look = LookDelta(p, door.Hotspot.transform.position, false);
                        yield return null;
                    }
                    input.Once = Case01Input.From(new ExploreInput { Interact = true });
                    yield return Wait(1.3f);
                }
                Assert.IsTrue(door.IsOpenEnough, "E：推開客廳門（" + door.Angle + "°）");
                // 推開後讓它停在全開（往剛才打開的那一側），之後的路線才不會擦到半開的門板。
                door.SetAngle(door.SwingSide * door.OpenAngle, null);
                foreach (var pt in new[] { new Vector2(5.6f, 3.55f), new Vector2(4.95f, 3.55f), new Vector2(4.95f, 1.2f), new Vector2(1.6f, 1.2f), new Vector2(1.45f, 1.9f) })
                {
                    yield return Walk(s, input, pt.x, pt.y);
                    if (!arrived)
                    {
                        var at = p.transform.position + (new Vector3(pt.x, 0f, pt.y) - p.transform.position).normalized * 0.3f;
                        var hits = Physics.OverlapCapsule(at + Vector3.up * 0.4f, at + Vector3.up * 1.5f, 0.36f, ~0, QueryTriggerInteraction.Ignore);
                        var names = new System.Text.StringBuilder();
                        foreach (var h in hits) if (h != p.Body) names.Append(h.name).Append("@").Append(h.transform.position.ToString("F2")).Append("(root ").Append(h.transform.root.name).Append(") ");
                        Assert.Fail("走到 " + pt + "，停在 " + p.transform.position + "；前方碰撞：" + names);
                    }
                }
                var it = s.House.Layout.Interactables[Case01Ids.Photo];
                for (int i = 0; i < 6; i++)
                {
                    input.Look = LookDelta(p, it.transform.TransformPoint(it.GetComponent<BoxCollider>().center), false);
                    yield return null;
                }
                input.Once = Case01Input.From(new ExploreInput { Interact = true });
                yield return Wait(0.5f);
                Assert.IsTrue(s.Inspecting, "E：拿起合照");
                var pos = p.transform.position;
                input.Move = new Vector2(0f, 1f);
                yield return Wait(0.5f);
                input.Move = Vector2.zero;
                Assert.Less(Vector3.Distance(pos, p.transform.position), 0.01f, "檢視中不能走");
                input.Once = Case01Input.From(new ExploreInput { Interact = true });
                yield return Wait(0.6f);
                Assert.IsFalse(s.Inspecting, "E：放回");
                yield return Walk(s, input, 1.6f, 1.2f);
                Assert.IsTrue(arrived, "放回後恢復行走");
                Assert.AreEqual(1, go.GetComponentsInChildren<FirstPersonController>(true).Length);
                Assert.AreEqual(1, go.GetComponentsInChildren<AudioListener>(true).Length);
            }
            finally
            {
                Object.Destroy(go);
            }
            yield return null;
        }
    }
}
