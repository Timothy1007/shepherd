using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 家具配置 QA：量動線上的實際淨寬（docs/level/CASE01_LIVED_IN_HOME_PROP_AND_SPATIAL_PLAN_V1.md 第 B 節）。
    /// 每個檢查點往左右兩側一步步往外走，碰到碰撞體或家具的外框（Renderer bounds，連沒有碰撞體的小物也算）就停；
    /// 在三個高度（腳 0.2 m、腰 0.9 m、肩 1.5 m）量，取最窄的那個。門洞本身的寬度是建築，另外列。
    /// </summary>
    public static class LanternClearance
    {
        public struct Point
        {
            public string Name;
            public Vector3 At;       // 世界座標（地面）
            public Vector3 Along;    // 動線方向
            public float Need;       // 要求的淨寬（主通道 1.1、次要 0.85）
            public bool Doorway;     // 門洞（只記錄，不算不合格）
        }

        public struct Result
        {
            public Point P;
            public float Width;
            public string BlockedBy;
            public bool Ok => P.Doorway || Width + 0.005f >= P.Need;
        }

        private static readonly float[] Heights = { 0.2f, 0.9f, 1.5f };
        private const float Step = 0.02f, Max = 1.6f;

        public static List<Result> Measure(Transform scope, IList<Point> points, Transform ignore = null)
        {
            var rends = new List<Renderer>();
            foreach (var r in scope.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                var b = r.bounds;
                if (b.size.y < 0.03f) continue;                       // 地毯、腳踏墊：走得過去
                if (ignore != null && r.transform.IsChildOf(ignore)) continue;
                rends.Add(r);
            }
            var results = new List<Result>();
            foreach (var p in points)
            {
                var n = Vector3.Cross(Vector3.up, p.Along.normalized).normalized;
                float best = float.MaxValue; string by = "";
                foreach (var h in Heights)
                {
                    string l, rr;
                    float a = March(p.At + Vector3.up * h, n, rends, out l);
                    float c = March(p.At + Vector3.up * h, -n, rends, out rr);
                    if (a + c < best) { best = a + c; by = (a < c ? l : rr); }
                }
                results.Add(new Result { P = p, Width = best, BlockedBy = by });
            }
            return results;
        }

        private static float March(Vector3 from, Vector3 dir, List<Renderer> rends, out string by)
        {
            by = "";
            for (float d = 0f; d <= Max; d += Step)
            {
                var q = from + dir * d;
                var cols = Physics.OverlapSphere(q, 0.005f, ~0, QueryTriggerInteraction.Ignore);
                foreach (var c in cols)
                {
                    if (c is CharacterController) continue;
                    by = c.name; return d;
                }
                foreach (var r in rends) if (r.bounds.Contains(q)) { by = r.name; return d; }
            }
            by = "（超過 1.6 m）";
            return Max;
        }

        public static string Report(List<Result> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("| 檢查點 | 要求 | 實際淨寬 | 最窄處擋到的 | 結果 |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var r in results)
                sb.Append("| ").Append(r.P.Name).Append(" | ").Append(r.P.Doorway ? "（門洞）" : r.P.Need.ToString("0.00") + " m")
                  .Append(" | ").Append(r.Width.ToString("0.00")).Append(" m | ").Append(r.BlockedBy)
                  .Append(" | ").Append(r.P.Doorway ? "記錄" : r.Ok ? "OK" : "不足").AppendLine(" |");
            return sb.ToString();
        }
    }
}
