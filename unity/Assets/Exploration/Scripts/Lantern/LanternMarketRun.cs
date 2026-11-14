using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 記憶空間 A（老街市場）的目標與紀錄：越過倒下的攤子、上到騎樓、過天橋、找到家裡的攤位（拿到捲尺）。
    /// 記憶物件與「定」的更新由 LanternSandboxRun 統一負責（全世界只更新一次）；這裡只管市場自己的東西。
    /// </summary>
    public sealed class LanternMarketRun
    {
        public readonly LanternSandboxRun.Goal[] Goals =
        {
            new LanternSandboxRun.Goal { Name = "越過倒下的攤子" },
            new LanternSandboxRun.Goal { Name = "上到騎樓" },
            new LanternSandboxRun.Goal { Name = "過天橋" },
            new LanternSandboxRun.Goal { Name = "找到家裡的攤位" },
        };
        public bool HasKeepsake { get; private set; }
        public bool ShutterOpen { get; private set; }
        public string PlateBy { get; private set; } = "";
        public float ShutterAmount { get; private set; }
        public int Visits { get; private set; }

        private readonly LanternGraybox world;
        private readonly FirstPersonController player;
        private readonly LanternSandboxRun rules;
        private readonly LanternFlow flow;
        private readonly LanternAudio audio;
        private readonly System.Action<string> goTo;
        private string barrierSupport = "", climbSupport = "", bridgeSupport = "", shutterBy = "";

        public LanternMarketRun(LanternGraybox world, FirstPersonController player, LanternSandboxRun rules, LanternFlow flow, LanternAudio audio, System.Action<string> goTo)
        {
            this.world = world; this.player = player; this.rules = rules; this.flow = flow; this.audio = audio; this.goTo = goTo;
            Bind();
        }

        /// <summary>重建市場或房子之後，重新接上門和信物。</summary>
        public void Bind()
        {
            if (world.YardDoor != null) world.YardDoor.OnUse = Enter;
            if (world.MarketExit != null) world.MarketExit.OnUse = () => { goTo("V2Yard"); flow?.ShowToast("（又回到了側院。外面的陽光照常）", 3f); };
            if (world.MarketKeepsake != null)
            {
                world.MarketKeepsake.gameObject.SetActive(!HasKeepsake);
                world.MarketKeepsake.OnUse = () =>
                {
                    HasKeepsake = true;
                    world.MarketKeepsake.gameObject.SetActive(false);
                    Complete(3, "拿到捲尺（信物）");
                    flow?.ShowToast("（一卷舊捲尺。刻度磨得幾乎看不見——她小時候就用這個？）", 4.5f);
                };
            }
        }

        public void Reset()
        {
            foreach (var g in Goals) { g.Done = false; g.Method = ""; }
            HasKeepsake = false;
            barrierSupport = climbSupport = bridgeSupport = shutterBy = PlateBy = "";
            ShutterOpen = false; ShutterAmount = 0f;
            Bind();
        }

        /// <summary>從側院的後門走進市場的記憶。</summary>
        public void Enter()
        {
            Visits++;
            goTo("Market");
            flow?.ShowToast(Visits == 1 ? "（後門外面不是巷子……是一條老街。招牌上的字，好像在哪裡看過）" : "（又回到了那條老街）", 4.5f);
        }

        private void Complete(int i, string method)
        {
            var g = Goals[i];
            if (g.Done) return;
            g.Done = true;
            g.Method = string.IsNullOrEmpty(method) ? "（不明）" : method;
            flow?.ShowToast("（" + g.Name + "——用了：" + g.Method + "）", 3.5f);
            audio?.Play(LanternAudio.Placed, 0.8f);
        }

        public void Step(float dt)
        {
            if (world.MarketRoot == null) return;
            var feet = player.transform.position;
            bool inside = world.InMarket(feet);

            // 鐵捲門：壓板上有東西就捲上去。
            PlateBy = rules.PlateCheckAt(world.MarketPlate);
            bool pressed = PlateBy != "";
            ShutterAmount = Mathf.MoveTowards(ShutterAmount, pressed ? 1f : 0f, dt * 1.2f);
            if (world.MarketShutter != null) world.MarketShutter.position = world.MarketShutterClosed + Vector3.up * 2.45f * ShutterAmount;
            ShutterOpen = ShutterAmount > 0.85f;
            if (pressed) shutterBy = PlateBy;
            if (!inside) return;

            var s = rules.Support(feet);
            bool special = s != "" && s != "地板";
            float z = feet.z;

            // 一、越過倒下的攤子：到了北邊（z > 攤子 + 0.6）就算；記下翻過去時踩的東西。
            if (!Goals[0].Done)
            {
                if (z < world.MarketBarrierZ - 0.2f && special) barrierSupport = s;
                if (z > world.MarketBarrierZ + 0.6f && feet.y < 0.6f) Complete(0, barrierSupport != "" ? barrierSupport : "（直接翻過去）");
            }

            // 二、上到騎樓：站上騎樓時，記下最後踩的（遮雨棚斜坡、或鐵捲門後的樓梯）。
            if (!Goals[1].Done)
            {
                if (world.MarketBalcony.Contains(feet + Vector3.up * 0.1f)) Complete(1, climbSupport != "" ? climbSupport : "鐵捲門後的樓梯（壓板上放了" + shutterBy + "）");
                else if (special) climbSupport = s;
                else if (s == "地板" && feet.y < 0.4f) climbSupport = "";
            }

            // 三、過天橋：在斷掉的那一段上方時踩著什麼；到了天台才算。
            if (!Goals[2].Done)
            {
                bool overGap = feet.x > world.MarketBridgeGapX0 && feet.x < world.MarketBridgeGapX1 && feet.y > 1.5f;
                if (overGap && special) bridgeSupport = s;
                if (feet.y < 1.2f) bridgeSupport = "";
                if (world.MarketTerrace.Contains(feet + Vector3.up * 0.1f) && bridgeSupport != "") Complete(2, bridgeSupport);
            }
        }
    }
}
