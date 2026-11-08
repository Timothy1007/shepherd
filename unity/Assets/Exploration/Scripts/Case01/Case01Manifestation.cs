using System;
using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// 兩個失序體「由環境碎片凝聚成形」的演出（約 4.8 秒，可按 E 跳過）。
    /// 四拍：①地上的書、照片、紙張輕震翻動 → ②碎片漂向看不見的重心並環繞 → ③碎片聚成人形輪廓、陰影凝實（現有失序體模型由小變大）
    /// → ④低頻扣合聲＋短音樂轉折，完成後碎片留在身上成為外殼，接回既有的遭遇判定。
    /// 碎片是投影到中庭的幻影，不動夾層裡的原物件；不追玩家、不改敵人數量或戰鬥。
    /// 正式模型替換掛點：<see cref="Figures"/>（由 Case01HouseQA.BuildEncounter 提供）與 <see cref="Shells"/>。
    /// </summary>
    public sealed class Case01Manifestation
    {
        public const float TrembleEnd = 1.0f;
        public const float DriftEnd = 2.4f;
        public const float AssembleEnd = 4.0f;
        public const float Duration = 4.8f;
        public const int FragmentsPerFigure = Case01EnemyShell.PiecesPerFigure;

        private sealed class Fragment
        {
            public Transform T;
            public int Owner;
            public Vector3 Start;
            public Quaternion StartRot;
            public Vector3 Target;
            public Quaternion TargetRot;
            public float Phase;
            public float Orbit;
        }

        private readonly List<Fragment> fragments = new List<Fragment>();
        private readonly List<Transform> figures = new List<Transform>();
        private readonly List<Vector3> centers = new List<Vector3>();
        private readonly List<Vector3> figureScale = new List<Vector3>();
        private float t;
        private int lastPhase = -1;

        public Transform Root { get; private set; }
        public IList<Transform> Figures { get { return figures; } }
        /// <summary>每個失序體的碎片外殼根物件。</summary>
        public readonly List<Transform> Shells = new List<Transform>();
        public float Time { get { return t; } }
        public bool Done { get { return t >= Duration; } }
        public bool Skipped { get; private set; }
        public int FragmentCount { get { return fragments.Count; } }
        /// <summary>進入第 n 拍時觸發（0～3）；用來播聲音，不影響計時。</summary>
        public event Action<int> PhaseStarted;

        public int Phase
        {
            get
            {
                if (t < TrembleEnd) return 0;
                if (t < DriftEnd) return 1;
                if (t < AssembleEnd) return 2;
                return 3;
            }
        }

        public Case01Manifestation(Transform parent, Case01Palette p, IList<Transform> enemyFigures, IList<float> heights, int seed = 17)
        {
            Root = ArenaArt.Group("Manifestation", parent, Vector3.zero);
            var rng = new System.Random(seed);
            Func<float, float, float> R = (a, b) => a + (float)rng.NextDouble() * (b - a);
            // 紙、舊紙、書、相框木；其中一種帶微弱裂隙光，讓碎片在暗處也看得出軌跡（與戰鬥外殼同一組材質）。
            var mats = Case01EnemyShell.Materials(p);
            // v0.5：人形先隱藏，再擺碎片（建立的同一影格就不可見，不會閃出舊的黑色人形）。
            foreach (var fig in enemyFigures)
                if (fig != null) fig.gameObject.SetActive(false);
            for (int i = 0; i < enemyFigures.Count; i++)
            {
                var fig = enemyFigures[i];
                figures.Add(fig);
                figureScale.Add(fig.localScale);
                var c = fig.position;
                centers.Add(c);
                var shell = ArenaArt.Group("Shell_" + i, Root, Vector3.zero);
                Shells.Add(shell);
                // 終點：與戰鬥外殼相同的人形輪廓配置（頭、軀幹、雙臂、雙腿；以失序體的朝向為準）。
                var layout = Case01EnemyShell.Layout(i, heights[i]);
                for (int k = 0; k < layout.Count; k++)
                {
                    var piece = layout[k];
                    int kind = k % 3;
                    var tr = ArenaArt.Prim(PrimitiveType.Cube, shell, Vector3.zero, piece.Scale, mats[piece.Mat], null, "Fragment", false, false);
                    // 起點：散落在地上（以敵人位置為中心的環狀區域）。
                    float a = R(0f, Mathf.PI * 2f), r = R(1.2f, 3.4f);
                    var start = c + new Vector3(Mathf.Cos(a) * r, 0.02f + kind * 0.02f, Mathf.Sin(a) * r);
                    fragments.Add(new Fragment
                    {
                        T = tr, Owner = i, Start = start, StartRot = Quaternion.Euler(0f, R(0f, 360f), 0f),
                        Target = c + fig.rotation * piece.LocalPos, TargetRot = fig.rotation * piece.LocalRot,
                        Phase = R(0f, 6.28f), Orbit = R(0.8f, 1.4f),
                    });
                }
            }
            // 建立時只擺好起始位置，不觸發聲音（第一次 Tick 才進入第一拍）。
            UpdateTransforms();
        }

        /// <summary>v0.5 可見狀態：還沒開始＝只有地上的碎片；播放中＝凝聚；結束＝完成外觀。</summary>
        public Case01EnemyVisual Visual
        {
            get
            {
                if (Done) return Case01EnemyVisual.Manifested;
                return t <= 0f ? Case01EnemyVisual.DebrisOnly : Case01EnemyVisual.Assembling;
            }
        }

        /// <summary>人形開始凝實的時間：碎片已大致聚攏成輪廓之後（之前人形完全不可見）。</summary>
        public const float BodyAppearStart = 3.1f;

        /// <summary>第 i 個個體的碎片離輪廓終點的平均距離（測試用：人形可見時外殼必須已經聚攏）。</summary>
        public float MeanShellGap(int owner)
        {
            float sum = 0f;
            int n = 0;
            foreach (var f in fragments)
            {
                if (f.Owner != owner) continue;
                sum += Vector3.Distance(f.T.position, f.Target);
                n++;
            }
            return n > 0 ? sum / n : 0f;
        }

        public void Tick(float dt)
        {
            if (Done) return;
            t = Mathf.Min(Duration, t + dt);
            Apply();
        }

        /// <summary>跳過：直接到完成狀態（之後的流程與正常播完相同）。</summary>
        public void Skip()
        {
            if (Done) return;
            Skipped = true;
            t = Duration;
            Apply();
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private void Apply()
        {
            int ph = Phase;
            if (ph != lastPhase)
            {
                // 跳過時直接觸發最後一拍（只播扣合聲），不補播前面的聲音。
                if (Skipped) lastPhase = 2;
                for (int p = lastPhase + 1; p <= ph; p++)
                    if (PhaseStarted != null) PhaseStarted(p);
                lastPhase = ph;
            }
            UpdateTransforms();
        }

        private void UpdateTransforms()
        {
            foreach (var f in fragments)
            {
                var c = centers[f.Owner];
                Vector3 pos;
                Quaternion rot;
                if (t < TrembleEnd)
                {
                    float amp = 0.02f + 0.03f * (t / TrembleEnd);
                    pos = f.Start + new Vector3(0f, Mathf.Abs(Mathf.Sin(t * 22f + f.Phase)) * amp, 0f);
                    rot = f.StartRot * Quaternion.Euler(Mathf.Sin(t * 18f + f.Phase) * 12f * (t / TrembleEnd), 0f, 0f);
                }
                else if (t < DriftEnd)
                {
                    float k = Smooth((t - TrembleEnd) / (DriftEnd - TrembleEnd));
                    pos = Vector3.Lerp(f.Start, OrbitPoint(f, c, t), k);
                    rot = Quaternion.Slerp(f.StartRot, Quaternion.Euler(t * 90f + f.Phase * 30f, t * 60f, 0f), k);
                }
                else if (t < AssembleEnd)
                {
                    float k = Smooth((t - DriftEnd) / (AssembleEnd - DriftEnd));
                    pos = Vector3.Lerp(OrbitPoint(f, c, t), f.Target, k);
                    rot = Quaternion.Slerp(Quaternion.Euler(t * 90f + f.Phase * 30f, t * 60f, 0f), f.TargetRot, k);
                }
                else
                {
                    // 扣合後：貼在輪廓上，極輕微地呼吸。
                    float breathe = Done ? 0f : Mathf.Sin((t - AssembleEnd) * 12f + f.Phase) * 0.01f;
                    pos = f.Target + (f.Target - (c + Vector3.up * f.Target.y)) * breathe;
                    rot = f.TargetRot;
                }
                f.T.position = pos;
                f.T.rotation = rot;
            }
            // 陰影凝實：碎片大致聚攏之後，人形才在外殼內由小變大（v0.5：之前完全不可見）。
            float grow = Smooth((t - BodyAppearStart) / (AssembleEnd - BodyAppearStart));
            for (int i = 0; i < figures.Count; i++)
            {
                if (figures[i] == null) continue;
                figures[i].localScale = figureScale[i] * Mathf.Max(0.001f, grow);
                figures[i].gameObject.SetActive(grow > 0.01f);
            }
        }

        private static Vector3 OrbitPoint(Fragment f, Vector3 c, float t)
        {
            float k = Mathf.Clamp01((t - TrembleEnd) / (AssembleEnd - TrembleEnd));
            float radius = Mathf.Lerp(1.6f, 0.6f, k) * f.Orbit;
            float a = f.Phase + t * (1.6f + f.Orbit);
            return c + new Vector3(Mathf.Cos(a) * radius, 0.4f + k * 1.0f + Mathf.Sin(a * 2f) * 0.15f, Mathf.Sin(a) * radius);
        }

        /// <summary>重試戰鬥等情況：直接以完成狀態建立（不播聲音）。</summary>
        public void CompleteSilently()
        {
            var handlers = PhaseStarted;
            PhaseStarted = null;
            t = Duration;
            lastPhase = 3;
            Apply();
            PhaseStarted = handlers;
        }

        public void SetVisible(bool visible)
        {
            if (Root != null) Root.gameObject.SetActive(visible);
        }
    }
}
