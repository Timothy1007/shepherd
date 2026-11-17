using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 爬上可搬的東西（改衣店的紙箱、凳子；使用者 2026-11-16）：
    ///   ・搬動改用和其他箱子一樣的牽引（LanternActions：E 抓起、E 放下、左鍵丟出），放在任何放得下的地方。
    ///   ・頂著箱子往前走約 0.35 秒＝爬上去（不另外加按鍵）。爬得上去的高度跟身體大小走（設計總綱 §11）。
    ///   ・站在箱子上，眼睛和手都跟著變高（例如幼年墊著紙箱才搆得到電箱的拉桿）。
    /// </summary>
    public sealed class LanternCarry
    {
        public int Climbs { get; private set; }
        public float ClimbHold { get; private set; }
        /// <summary>頂著往前走多久會爬上去（秒）。</summary>
        public const float ClimbDelay = 0.35f;
        public string Prompt { get; private set; } = "";
        /// <summary>不能爬（例如抱著外套）。</summary>
        public System.Func<bool> Blocked;

        private readonly FirstPersonController player;
        private readonly LanternGraybox world;
        private readonly LanternFlow flow;
        private readonly LanternAudio audio;

        public LanternCarry(FirstPersonController player, LanternGraybox world, LanternFlow flow, LanternAudio audio)
        {
            this.player = player; this.world = world; this.flow = flow; this.audio = audio;
        }

        /// <summary>爬得上去的最高高度（腳底以上）：身高的六成（幼年約 0.57 m、成人約 1.08 m）。</summary>
        public float MaxClimb => player.Body.height * 0.6f;

        private Quaternion YawRot => Quaternion.Euler(0f, player.Yaw, 0f);
        private float Floor => player.transform.position.y;

        /// <summary>每格（玩家移動之後）：頂著可搬的東西往前走就爬上去。</summary>
        public void Step(float dt, Vector2 move)
        {
            Prompt = "";
            if (Blocked != null && Blocked()) { ClimbHold = 0f; return; }
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
                if (m == null || !m.Climbable || (m.ClimbBlocked != null && m.ClimbBlocked()) || m.Box == null || !m.Box.enabled || !m.gameObject.activeInHierarchy || m.Grabbed) continue;
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
    }
}
