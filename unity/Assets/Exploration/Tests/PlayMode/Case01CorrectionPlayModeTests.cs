using System.Collections;
using NUnit.Framework;
using Shepherd.Exploration.Case01;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shepherd.Exploration.Tests
{
    /// <summary>
    /// 現實校正 QA 的 Play 模式（Update／LateUpdate 驅動、真的移動與碰撞）：
    /// 通路堵塞 → 校正短暫通過 → 超時安全倒回 → 牽引／放置 → 再次校正局部穩定 → 可通行。
    /// 滑鼠鍵無法在測試裡模擬，左右鍵改呼叫同一個處理函式（Correct／Tether）；走路、轉頭、E 都走真正的輸入管線。
    /// </summary>
    public sealed class Case01CorrectionPlayModeTests
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

        private IEnumerator Walk(Case01CorrectionQA s, ScriptedInput input, float x, float z, Vector3? look = null, float max = 12f)
        {
            arrived = false;
            float t = 0f;
            var p = s.House.Player;
            while (t < max)
            {
                var pos = p.transform.position;
                var to = new Vector3(x - pos.x, 0f, z - pos.z);
                if (to.magnitude < 0.22f)
                {
                    arrived = true;
                    break;
                }
                var lookAt = look ?? new Vector3(x, p.EyePosition.y, z);
                var local = Quaternion.Inverse(Quaternion.Euler(0f, p.Yaw, 0f)) * to.normalized;
                input.Move = new Vector2(local.x, local.z) * Mathf.Clamp01(to.magnitude / 0.6f + 0.3f);
                input.Look = LookDelta(p, lookAt, true) * 0.5f;
                Assert.IsFalse(s.Slice.PlayerOverlapsSlice(), "玩家和櫃子／椅子重疊");
                t += Time.deltaTime;
                yield return null;
            }
            input.Move = Vector2.zero;
            yield return null;
        }

        private static IEnumerator Aim(ScriptedInput input, FirstPersonController p, Vector3 target)
        {
            for (int i = 0; i < 4; i++)
            {
                input.Look = LookDelta(p, target, false);
                yield return null;
            }
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
        public IEnumerator RealityCorrection_RealPlayMode_FullLoop()
        {
            var go = new GameObject("Case01RealityCorrectionQA");
            try
            {
                var s = go.AddComponent<Case01CorrectionQA>();
                var input = new ScriptedInput();
                s.Host.UseInput(input);
                yield return null;
                yield return null;
                var sl = s.Slice;
                var p = s.House.Player;
                Assert.IsNotNull(sl, "Play 模式建立了校正切片");
                Assert.IsNull(s.ReticleText, "起點（前門內）沒有校正準星");
                s.GoToKitchenDoor();
                yield return null;
                // 開廚房門。
                var door = s.Host.World.Door("door_kitchen");
                yield return Walk(s, input, 5.7f, 11.1f);
                if (!door.IsOpenEnough)
                {
                    yield return Aim(input, p, door.Hotspot.transform.position);
                    input.Once = Case01Input.From(new ExploreInput { Interact = true });
                    yield return Wait(1.3f);
                }
                Assert.IsTrue(door.IsOpenEnough, "廚房門打開");
                yield return Walk(s, input, 5.7f, 12.6f);
                yield return Walk(s, input, 6.05f, 13.4f);
                Assert.IsTrue(sl.PlayerInZone);
                Assert.IsNotNull(s.ReticleText, "失序區域出現準星");
                // 堵塞：走不過去。
                yield return Walk(s, input, 7.9f, 13.4f, null, 2.5f);
                Assert.IsFalse(arrived, "倒下的櫃子擋住拱口");
                // 校正 → 通過。
                yield return Aim(input, p, sl.CabinetCollider.bounds.center);
                Assert.AreEqual(Case01CorrectResult.Restored, s.Correct());
                yield return Wait(Case01CorrectionSlice.RiseTime + 0.1f);
                yield return Walk(s, input, 7.9f, 13.4f);
                Assert.IsTrue(arrived, "校正後走進後廚");
                // 超時：安全倒回（玩家在後廚，不在路徑上）。
                yield return Wait(s.Duration + Case01CorrectionSlice.FallTime + 0.3f);
                Assert.AreEqual(Case01CabinetState.Fallen, sl.State, "超時倒回");
                // 從後廚這側再校正，回到廚房。
                yield return Aim(input, p, sl.CabinetCollider.bounds.center);
                Assert.AreEqual(Case01CorrectResult.Restored, s.Correct());
                yield return Wait(Case01CorrectionSlice.RiseTime + 0.1f);
                yield return Walk(s, input, 6.05f, 13.4f);
                Assert.IsTrue(arrived);
                yield return Wait(s.Duration);
                // 牽引椅子到凹痕。
                yield return Walk(s, input, 5.75f, 13.15f);
                yield return Walk(s, input, 2.05f, 13.15f);
                yield return Walk(s, input, 1.0f, 14.6f);
                yield return Aim(input, p, sl.ChairCollider.bounds.center);
                Assert.AreEqual(Case01TetherResult.Grabbed, s.Tether());
                for (int i = 0; i < 50; i++)
                {
                    input.Look = LookDelta(p, new Vector3(9f, 1.2f, 14.6f), true) * 0.1f;
                    yield return null;
                }
                yield return Wait(0.6f);
                yield return Walk(s, input, 1.4f, 16.3f, new Vector3(9f, 1.2f, 16.3f));
                yield return Walk(s, input, 4.6f, 16.35f, new Vector3(9f, 1.2f, 16.35f));
                yield return Walk(s, input, 6.6f - sl.HoldDistance, 16.85f, new Vector3(9f, 1.2f, 16.85f));
                yield return Wait(1.0f);
                Assert.IsTrue(sl.Holding, "牽引沒有斷開");
                Assert.AreEqual(Case01TetherResult.PlacedOnMark, s.Tether(), "放回凹痕（椅子在 " + sl.Chair.position + "）");
                // 再次校正：局部穩定 → 可通行。
                yield return Walk(s, input, 6.05f, 13.4f);
                yield return Aim(input, p, sl.CabinetCollider.bounds.center);
                Assert.AreEqual(Case01CorrectResult.Stabilized, s.Correct());
                yield return Wait(Case01CorrectionSlice.RiseTime + s.Duration + 1f);
                Assert.AreEqual(Case01CabinetState.Stable, sl.State, "局部穩定，不再倒下");
                yield return Walk(s, input, 7.9f, 13.4f);
                Assert.IsTrue(arrived, "穩定後拱口保持暢通");
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
