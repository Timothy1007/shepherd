using System.Collections.Generic;
using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 能力小原型的規則與紀錄：記憶物件每格更新、「定」的計時、壓板與門、三個目標的完成判定，
    /// 以及**每個目標是用什麼方法解的**（試玩後看有沒有出現我們沒設計的解法）。
    /// </summary>
    public sealed class LanternSandboxRun
    {
        public sealed class Goal
        {
            public string Name;
            public bool Done;
            public string Method = "";
        }

        public readonly Goal[] Goals =
        {
            new Goal { Name = "到高處" },
            new Goal { Name = "過缺口" },
            new Goal { Name = "打開封住的門" },
        };
        public readonly List<string> Log = new List<string>();
        public bool DoorOpen { get; private set; }
        public string PlateBy { get; private set; } = "";
        public float DoorOpenAmount { get; private set; }

        private readonly LanternGraybox world;
        private readonly FirstPersonController player;
        private readonly LanternHold hold;
        private readonly LanternActions actions;
        private readonly LanternFlow flow;
        private readonly LanternAudio audio;
        private string climbSupport = "", gapSupport = "", doorBy = "";
        private float time;

        public LanternSandboxRun(LanternGraybox world, FirstPersonController player, LanternHold hold, LanternActions actions, LanternFlow flow, LanternAudio audio)
        {
            this.world = world; this.player = player; this.hold = hold; this.actions = actions; this.flow = flow; this.audio = audio;
        }

        public void Reset()
        {
            foreach (var g in Goals) { g.Done = false; g.Method = ""; }
            climbSupport = gapSupport = doorBy = PlateBy = "";
            DoorOpen = false;
            DoorOpenAmount = 0f;
        }

        /// <summary>腳下踩的是什麼（判斷用了哪種方法）。</summary>
        public string Support(Vector3 feet)
        {
            RaycastHit hit;
            if (!Physics.SphereCast(feet + Vector3.up * 0.4f, 0.2f, Vector3.down, out hit, 0.7f, ~0, QueryTriggerInteraction.Ignore)) return "";
            return Describe(hit.collider);
        }

        private string Describe(Collider c)
        {
            var m = c.GetComponentInParent<LanternMemory>();
            if (m != null) return m.Label + (m.Held ? "（定住）" : "（光裡）");
            var t = c.GetComponentInParent<LanternTetherable>();
            if (t != null) return t.Label + (hold.IsFrozen(t) ? "（定住）" : "");
            return "地板";
        }

        /// <summary>壓板上有什麼（記憶要在場才算；可以抓的東西放上去就算）。</summary>
        public string PlateCheck() => PlateCheckAt(world.SandboxPlate);

        public string PlateCheckAt(Vector3 plate)
        {
            var hits = Physics.OverlapBox(plate + Vector3.up * 0.3f, new Vector3(0.45f, 0.28f, 0.45f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            foreach (var c in hits)
            {
                var m = c.GetComponentInParent<LanternMemory>();
                if (m != null && m.Present) return Describe(c);
                if (c.GetComponentInParent<LanternTetherable>() != null) return Describe(c);
            }
            return "";
        }

        private void Complete(int i, string method)
        {
            var g = Goals[i];
            if (g.Done) return;
            g.Done = true;
            g.Method = string.IsNullOrEmpty(method) ? "（不明）" : method;
            Log.Add(g.Name + "：" + g.Method);
            flow?.ShowToast("（" + g.Name + "——用了：" + g.Method + "）", 3.5f);
            audio?.Play(LanternAudio.Placed, 0.8f);
        }

        public void Step(float dt)
        {
            time += dt;
            foreach (var m in world.Memories) if (m != null) m.Step(dt, time);
            hold.Step(dt, actions);
            if (world.SandboxRoot == null) return;
            var feet = player.transform.position;

            // 一、到高處：記下站上高台前最後踩的東西。
            if (!Goals[0].Done)
            {
                if (world.SandboxPlatform.Contains(feet + Vector3.up * 0.1f)) Complete(0, climbSupport);
                else
                {
                    var s = Support(feet);
                    if (s != "" && s != "地板") climbSupport = s;
                    else if (s == "地板" && feet.y < world.SandboxPlatform.min.y - 0.6f) climbSupport = "";
                }
            }

            // 二、過缺口：在缺口上方時踩著什麼；走到另一側才算（掉進洞裡就重新記）。
            if (!Goals[1].Done)
            {
                bool overGap = feet.x > world.SandboxGapX0 && feet.x < world.SandboxGapX1;
                if (feet.y < -0.3f) gapSupport = "";
                else if (overGap) { var s = Support(feet); if (s != "" && s != "地板") gapSupport = s; }
                if (feet.x > world.SandboxGapX1 + 0.3f && feet.y > -0.2f && gapSupport != "") Complete(1, gapSupport);
            }

            // 三、門：壓板上有東西就打開（慢慢升起），沒有就慢慢關上。
            PlateBy = PlateCheck();
            bool pressed = PlateBy != "";
            DoorOpenAmount = Mathf.MoveTowards(DoorOpenAmount, pressed ? 1f : 0f, dt * 1.4f);
            if (world.SandboxDoor != null) world.SandboxDoor.position = world.SandboxDoorClosed + Vector3.up * 2.35f * DoorOpenAmount;
            DoorOpen = DoorOpenAmount > 0.85f;
            if (pressed) doorBy = PlateBy;
            if (!Goals[2].Done && world.SandboxDoor != null && feet.x > world.SandboxDoorClosed.x + 0.4f) Complete(2, "壓板上放了" + doorBy);
        }
    }
}
