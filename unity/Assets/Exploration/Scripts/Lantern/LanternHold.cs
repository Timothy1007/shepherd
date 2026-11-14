using System.Collections.Generic;
using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 「定」（能力原型，Q）：對準一個東西按 Q，把它「現在的狀態」固定一段時間。
    /// - 記憶中的東西：沒有光也維持住（看得到、踩得到）。有偏差的記憶定不住（碎掉，次數照扣）。
    /// - 現在的東西（可以抓的箱子、木板）：固定在原地，不受重力、抓不動。
    /// 3 次，每次約 20 秒恢復；同時最多定住 2 個（第 3 個會讓最早的那個解開）。
    /// 起始數值見 docs/design/CORE_LOOP_AND_ABILITIES_V1.md 4.1。
    /// </summary>
    public sealed class LanternHold
    {
        public const int MaxCharges = 3;
        public const float RechargeSeconds = 20f;
        public const float HoldSeconds = 25f;
        public const int MaxActive = 2;
        public const float Range = 5f;

        public float Charges { get; private set; } = MaxCharges;
        public int Holds { get; private set; }
        public int Fails { get; private set; }

        private sealed class Entry
        {
            public LanternMemory Memory;
            public LanternTetherable Body;
            public float Left;
        }
        private readonly List<Entry> active = new List<Entry>();

        public int ActiveCount => active.Count;

        public bool IsFrozen(LanternTetherable t)
        {
            foreach (var e in active) if (e.Body == t) return true;
            return false;
        }

        /// <summary>對準的東西（記憶或可以抓的物件）；沒有回傳 null。</summary>
        public static Component Target(Camera cam)
        {
            RaycastHit hit;
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            if (!Physics.SphereCast(ray, 0.12f, out hit, Range, ~0, QueryTriggerInteraction.Ignore)) return null;
            var m = hit.collider.GetComponentInParent<LanternMemory>();
            if (m != null) return m;
            return hit.collider.GetComponentInParent<LanternTetherable>();
        }

        /// <summary>按 Q。回傳要顯示給玩家的一句話（空字串＝不用顯示）。</summary>
        public string Use(Camera cam, LanternActions actions)
        {
            if (Charges < 1f) return "（定的力量還沒恢復）";
            var target = Target(cam);
            if (target == null) return "（沒有可以定住的東西）";
            var mem = target as LanternMemory;
            if (mem != null)
            {
                if (!mem.Present) return "（它在光外，看不到了）";
                Charges -= 1f;
                if (!mem.TryHold(HoldSeconds)) { Fails++; return "（它在光裡晃了一下，碎掉了……這段記憶不可靠）"; }
                Push(new Entry { Memory = mem, Left = HoldSeconds });
                Holds++;
                return "";
            }
            var body = target as LanternTetherable;
            if (body != null)
            {
                if (actions != null && actions.Held == body) actions.DropHeld(false);
                Charges -= 1f;
                body.Body.velocity = Vector3.zero;
                body.Body.angularVelocity = Vector3.zero;
                body.Body.isKinematic = true;
                Push(new Entry { Body = body, Left = HoldSeconds });
                Holds++;
                return "";
            }
            return "";
        }

        private void Push(Entry e)
        {
            // 同一個東西再定一次＝重新計時。
            for (int i = active.Count - 1; i >= 0; i--)
                if ((e.Memory != null && active[i].Memory == e.Memory) || (e.Body != null && active[i].Body == e.Body)) active.RemoveAt(i);
            while (active.Count >= MaxActive) Release(active[0]);
            active.Add(e);
        }

        private void Release(Entry e)
        {
            if (e.Memory != null) e.Memory.Release();
            if (e.Body != null && e.Body.Body != null) e.Body.Body.isKinematic = false;
            active.Remove(e);
        }

        public void Step(float dt, LanternActions actions)
        {
            if (Charges < MaxCharges) Charges = Mathf.Min(MaxCharges, Charges + dt / RechargeSeconds);
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var e = active[i];
                e.Left -= dt;
                if (e.Memory == null && e.Body == null) { active.RemoveAt(i); continue; }
                // 被定住的東西抓不動。
                if (e.Body != null && actions != null && actions.Held == e.Body) actions.DropHeld(false);
                if (e.Left <= 0f) Release(e);
            }
        }

        public void Clear()
        {
            for (int i = active.Count - 1; i >= 0; i--) Release(active[i]);
            Charges = MaxCharges;
        }

        /// <summary>介面用：●＝可以用的次數、◐＝正在恢復。</summary>
        public string ChargeText()
        {
            int full = Mathf.FloorToInt(Charges);
            var s = "定 " + new string('●', full);
            if (full < MaxCharges) s += Charges - full > 0.01f ? "◐" : "○";
            if (full + 1 < MaxCharges) s += new string('○', MaxCharges - full - 1);
            return s + (active.Count > 0 ? "　定住中 " + active.Count + " / " + MaxActive : "");
        }
    }
}
