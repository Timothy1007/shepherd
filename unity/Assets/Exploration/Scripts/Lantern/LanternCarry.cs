using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 搬東西與爬上去（改衣店的紙箱、凳子；使用者 2026-11-16）：
    ///   ・對著箱子按 E＝抓住；走路時箱子保持在身前，跟著移動（推、拉都可以）；再按 E＝放下。搬的時候走慢一點。
    ///     箱子不會穿過牆或家具；卡住拉不動就自動放下。不用物理亂推，好控制，之後手機也只要一個鍵。
    ///   ・沒有抓著的時候，頂著箱子往前走約 0.35 秒＝爬上去（不另外加按鍵）。爬得上去的高度跟身體大小走（設計總綱 §11）。
    ///   ・站在箱子上，眼睛和手都跟著變高（例如幼年墊著紙箱才搆得到電箱的拉桿）。
    /// </summary>
    public sealed class LanternCarry
    {
        public LanternMovable Grabbed { get; private set; }
        public int Climbs { get; private set; }
        public float ClimbHold { get; private set; }
        /// <summary>搬東西時的步行速度倍率。</summary>
        public const float CarrySpeedScale = 0.6f;
        /// <summary>頂著往前走多久會爬上去（秒）。</summary>
        public const float ClimbDelay = 0.35f;
        public string Prompt { get; private set; } = "";

        private readonly FirstPersonController player;
        private readonly LanternGraybox world;
        private readonly LanternFlow flow;
        private readonly LanternAudio audio;
        private Vector3 offsetLocal;

        public LanternCarry(FirstPersonController player, LanternGraybox world, LanternFlow flow, LanternAudio audio)
        {
            this.player = player; this.world = world; this.flow = flow; this.audio = audio;
        }

        /// <summary>爬得上去的最高高度（腳底以上）：身高的六成（幼年約 0.57 m、成人約 1.08 m）。</summary>
        public float MaxClimb => player.Body.height * 0.6f;

        private Quaternion YawRot => Quaternion.Euler(0f, player.Yaw, 0f);
        private float Floor => player.transform.position.y;

        /// <summary>按 E 對著可搬的東西：抓住／放下。</summary>
        public void Toggle(LanternMovable m)
        {
            if (m == null) return;
            if (Grabbed == m) { Release(); return; }
            if (Grabbed != null) Release();
            if (!player.Grounded && player.transform.position.y > LanternGraybox.V2Origin.y + 0.15f) { flow?.ShowToast("（站在上面搬不動，先下來）", 2f); return; }
            var feet = player.transform.position;
            // 地面：從腳底往下量（腳底和地面之間有碰撞的皮層厚度）。
            float ground = Physics.Raycast(feet + Vector3.up * 0.2f, Vector3.down, out var gh, 1f, ~0, QueryTriggerInteraction.Ignore) ? gh.point.y : feet.y;
            float pivotToBottom = m.BottomOffset;
            var pos = m.transform.position;
            // 放在高處（例如疊在紙箱堆上）：先搬到地上——原地放得下就原地，否則放在身前。
            if (m.BottomY > ground + 0.05f)
            {
                var down = new Vector3(pos.x, ground + pivotToBottom, pos.z);
                var front = feet + YawRot * Vector3.forward * (player.Body.radius + HalfDepth(m) + 0.15f);
                front.y = ground + pivotToBottom;
                if (Free(m, down, m.transform.rotation)) pos = down;
                else if (Free(m, front, m.transform.rotation)) pos = front;
                else { flow?.ShowToast("（這裡放不下）", 2f); return; }
                m.transform.position = pos;
            }
            var rel = pos - feet; rel.y = 0f;
            float min = player.Body.radius + HalfDepth(m) + 0.08f;
            if (rel.magnitude < min) rel = (rel.sqrMagnitude > 1e-4f ? rel.normalized : YawRot * Vector3.forward) * min;
            offsetLocal = Quaternion.Inverse(YawRot) * rel;
            Grabbed = m;
            m.Grabbed = true;
            Physics.IgnoreCollision(player.Body, m.Box, true);
            Physics.SyncTransforms();
            audio?.Play(LanternAudio.Grab, 0.5f);
        }

        public void Release()
        {
            if (Grabbed == null) return;
            Physics.IgnoreCollision(player.Body, Grabbed.Box, false);
            Grabbed.Grabbed = false;
            Grabbed = null;
            audio?.Play(LanternAudio.Drop, 0.4f);
        }

        /// <summary>每格（玩家移動之後）：被抓著的東西跟上；沒抓著時判斷要不要爬上去。</summary>
        public void Step(float dt, Vector2 move)
        {
            Prompt = "";
            if (Grabbed != null)
            {
                var m = Grabbed;
                if (m == null || !m.gameObject.activeInHierarchy) { Grabbed = null; return; }
                var desired = player.transform.position + YawRot * offsetLocal;
                desired.y = m.transform.position.y;
                var cur = m.transform.position;
                var delta = desired - cur;
                if (delta.sqrMagnitude > 1e-6f)
                {
                    if (Free(m, cur + delta, m.transform.rotation)) m.transform.position = cur + delta;
                    else if (Free(m, cur + new Vector3(delta.x, 0f, 0f), m.transform.rotation)) m.transform.position = cur + new Vector3(delta.x, 0f, 0f);
                    else if (Free(m, cur + new Vector3(0f, 0f, delta.z), m.transform.rotation)) m.transform.position = cur + new Vector3(0f, 0f, delta.z);
                }
                Physics.SyncTransforms();
                var left = desired - m.transform.position; left.y = 0f;
                var toPlayer = m.transform.position - player.transform.position; toPlayer.y = 0f;
                if (left.magnitude > 0.6f || toPlayer.magnitude < player.Body.radius + HalfDepth(m) * 0.5f)
                {
                    Release();
                    flow?.ShowToast("（卡住了，先放下來）", 2f);
                    return;
                }
                Prompt = "E 放下" + m.Label;
                ClimbHold = 0f;
                return;
            }
            // 爬上去：頂著可搬的東西往前走。
            var target = InFront();
            if (target == null || move.y < 0.5f) { ClimbHold = 0f; return; }
            float h = target.TopY - Floor;
            if (h < 0.12f) { ClimbHold = 0f; return; }
            if (h > MaxClimb) { ClimbHold = 0f; Prompt = "（太高了，爬不上去）"; return; }
            ClimbHold += dt;
            Prompt = "爬上" + target.Label + "……";
            if (ClimbHold >= ClimbDelay) TryClimb(target);
        }

        /// <summary>身體正前方、貼著的可搬物件。</summary>
        public LanternMovable InFront()
        {
            var feet = player.transform.position;
            var fwd = YawRot * Vector3.forward;
            LanternMovable best = null; float bestD = 9f;
            foreach (var m in world.Movables)
            {
                if (m == null || m.Box == null || !m.Box.enabled || !m.gameObject.activeInHierarchy || m.Grabbed) continue;
                var c = m.Box.ClosestPoint(feet + Vector3.up * 0.1f);
                var d = c - feet; d.y = 0f;
                if (d.magnitude > player.Body.radius + 0.15f) continue;
                if (d.sqrMagnitude > 1e-4f && Vector3.Angle(fwd, d) > 55f) continue;
                if (d.magnitude < bestD) { bestD = d.magnitude; best = m; }
            }
            return best;
        }

        private void TryClimb(LanternMovable m)
        {
            var b = m.Box.bounds;
            // 站的位置：先試頂面中央，再往玩家這一側挪（箱子靠牆時，中央可能貼到牆）。
            var toPlayer = player.transform.position - b.center; toPlayer.y = 0f;
            toPlayer = toPlayer.sqrMagnitude > 1e-4f ? toPlayer.normalized : -(YawRot * Vector3.forward);
            float r = player.Body.radius - 0.02f;
            Vector3? spot = null;
            foreach (float s in new[] { 0f, 0.06f, 0.12f, 0.18f, 0.24f })
            {
                var p = new Vector3(b.center.x, b.max.y + 0.02f, b.center.z) + toPlayer * s;
                if (p.x < b.min.x + 0.03f || p.x > b.max.x - 0.03f || p.z < b.min.z + 0.03f || p.z > b.max.z - 0.03f) break;
                var bottom = p + Vector3.up * (r + 0.06f);
                var top = p + Vector3.up * (player.Body.height - r);
                bool blocked = false;
                foreach (var hit in Physics.OverlapCapsule(bottom, top, r, ~0, QueryTriggerInteraction.Ignore))
                    if (hit != player.Body && hit != m.Box) { blocked = true; break; }
                if (!blocked) { spot = p; break; }
            }
            if (spot == null) { ClimbHold = 0f; Prompt = "（上面太擠了）"; return; }
            player.Teleport(spot.Value, player.Yaw, player.Pitch);
            Climbs++;
            ClimbHold = 0f;
            audio?.Play(LanternAudio.Placed, 0.35f);
        }

        private static float HalfDepth(LanternMovable m)
        {
            var e = m.Box.bounds.extents;
            return Mathf.Max(e.x, e.z);
        }

        /// <summary>把 m 放在 pos（根物件位置）會不會撞到別的東西（地板、自己、玩家、觸發器不算）。</summary>
        private bool Free(LanternMovable m, Vector3 pos, Quaternion rot)
        {
            var t = m.transform;
            var ls = t.lossyScale;
            var half = Vector3.Scale(m.Box.size, ls) * 0.5f - new Vector3(0.01f, 0.03f, 0.01f);
            var center = pos + rot * Vector3.Scale(m.Box.center, ls) + Vector3.up * 0.03f;
            foreach (var c in Physics.OverlapBox(center, half, rot, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c == m.Box || c == player.Body || c.transform.IsChildOf(t)) continue;
                return false;
            }
            return true;
        }
    }
}
