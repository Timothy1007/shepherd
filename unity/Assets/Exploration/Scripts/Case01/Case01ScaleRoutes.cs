using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// Phase 1.7：每個格局的「主路線」分段（實際能走的路點，避開家具）。測試用真正的移動走一遍，
    /// 量測工具用它算路長與各步速下的秒數。O＝v0.5 方案 A（Phase 1.6 使用者玩過的版本）。
    /// </summary>
    public static class Case01ScaleRoutes
    {
        public struct Leg
        {
            public string Name;
            /// <summary>x、z 交錯的路點（從上一段的終點出發）。</summary>
            public float[] XZ;

            public Leg(string name, params float[] xz)
            {
                Name = name;
                XZ = xz;
            }
        }

        public static Vector3 Start(Case01Floorplan p)
        {
            if (p == Case01Floorplan.R2) return Case01HouseBuilder.R17.SpawnR2;
            if (Case01HouseBuilder.IsR(p)) return Case01HouseBuilder.R17.Spawn;
            return Case01HouseBuilder.V05A.Spawn;
        }

        public static Leg[] For(Case01Floorplan p)
        {
            if (p == Case01Floorplan.R1)
                return new[]
                {
                    new Leg("玄關 → 客廳（壁爐前）", 6.9f, 1.8f, 5.6f, 1.8f, 5.0f, 1.2f, 1.6f, 1.2f),
                    new Leg("客廳 → 餐廳", 1.4f, 1.9f, 1.4f, 4.9f, 3.2f, 5.2f, 3.2f, 6.4f),
                    new Leg("餐廳 → 廚房", 4.9f, 7.3f, 4.9f, 11.4f, 4.6f, 12.8f),
                    new Leg("廚房 → 走廊 → 樓梯 → 二樓平台", 4.9f, 11.4f, 5.6f, 9.2f, 7.2f, 9.2f, 7.6f, 11.2f, 9.45f, 11.2f, 9.45f, 10.9f, 9.45f, 7.0f, 9.45f, 6.2f, 8.4f, 5.4f),
                    new Leg("平台 → 沿樓梯井走廊 → 書房", 7.6f, 7.0f, 7.6f, 11.15f, 5.8f, 11.15f, 5.3f, 10.0f, 4.0f, 9.6f),
                    new Leg("書房 → 臥室", 5.3f, 10.0f, 5.8f, 11.15f, 7.6f, 11.15f, 7.6f, 6.9f, 7.6f, 4.8f, 7.6f, 3.3f, 5.6f, 3.3f, 4.6f, 2.4f),
                    new Leg("臥室 → 第三扇門前", 5.6f, 3.3f, 7.0f, 3.3f, 8.0f, 1.6f, 8.6f, 0.9f),
                };
            if (p == Case01Floorplan.R2)
                return new[]
                {
                    new Leg("前廊 → 內玄關 → 客廳（壁爐前）", 7.1f, 2.2f, 6.9f, 3.6f, 5.6f, 3.6f, 5.0f, 1.2f, 1.6f, 1.2f),
                    new Leg("客廳 → 壁爐旁門 → 餐廳", 1.4f, 1.9f, 1.4f, 4.9f, 1.25f, 5.3f, 1.25f, 6.4f),
                    new Leg("餐廳 → 廚房", 5.3f, 7.3f, 5.5f, 11.4f, 5.7f, 12.8f),
                    new Leg("廚房 → 走廊 → 樓梯 → 二樓平台", 5.5f, 11.4f, 5.6f, 9.2f, 7.2f, 9.2f, 7.6f, 11.2f, 9.45f, 11.2f, 9.45f, 10.9f, 9.45f, 7.0f, 9.45f, 6.2f, 8.4f, 5.4f),
                    new Leg("平台 → 沿樓梯井走廊 → 書房", 7.6f, 7.0f, 7.6f, 11.15f, 5.8f, 11.15f, 5.3f, 10.0f, 4.0f, 9.6f),
                    new Leg("書房 → 拱門 → 臥室", 5.3f, 10.0f, 5.8f, 11.15f, 7.6f, 11.15f, 7.6f, 6.9f, 7.4f, 4.7f, 7.4f, 3.7f, 7.0f, 3.3f, 5.6f, 3.3f, 4.6f, 2.4f),
                    new Leg("臥室 → 第三扇門前", 5.6f, 3.3f, 7.0f, 3.3f, 8.0f, 1.6f, 8.6f, 0.9f),
                };
            return new[]
            {
                new Leg("玄關 → 客廳（壁爐前）", 6.2f, 1.4f, 4.7f, 1.4f, 1.6f, 1.5f),
                new Leg("客廳 → 餐廳", 4.7f, 1.5f, 4.7f, 4.7f, 3.4f, 5.05f, 3.4f, 6.0f),
                new Leg("餐廳 → 廚房", 3.4f, 6.4f, 4.6f, 7.6f, 6.2f, 7.6f),
                new Leg("廚房 → 樓梯 → 二樓平台", 8.0f, 6.0f, 9.35f, 5.4f, 9.35f, 4.75f, 9.35f, 1.3f, 9.4f, 0.5f, 8.2f, 0.5f, 6.95f, 1.6f, 6.95f, 3.6f),
                new Leg("平台 → 書房", 6.95f, 4.3f, 6.95f, 5.8f, 6.7f, 8.2f),
                new Leg("書房 → 臥室", 6.95f, 7.5f, 5.6f, 7.5f, 4.0f, 7.0f),
                new Leg("臥室 → 第三扇門前", 5.6f, 7.5f, 6.95f, 6.6f, 6.95f, 4.2f, 6.95f, 1.0f),
            };
        }

        /// <summary>一段的水平路長（從 from 出發）。樓梯段只算水平距離（爬升另計在坡度）。</summary>
        public static float Length(Vector3 from, Leg leg, out Vector3 end)
        {
            float d = 0f;
            var cur = new Vector2(from.x, from.z);
            for (int i = 0; i + 1 < leg.XZ.Length; i += 2)
            {
                var n = new Vector2(leg.XZ[i], leg.XZ[i + 1]);
                d += Vector2.Distance(cur, n);
                cur = n;
            }
            end = new Vector3(cur.x, 0f, cur.y);
            return d;
        }
    }
}
