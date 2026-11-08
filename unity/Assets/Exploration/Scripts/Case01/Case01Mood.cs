using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// v0.4 房屋 H0→H3 的光色與屋況（純計算，可單元測試）。覆蓋 v0.3「H3 回到基準暖光」：
    /// H0 暖木生活感；H1 餐具改寫後全屋開始變冷（餐廳／客廳先）；H2 樓梯錯接後普遍冷清、暖燈失效、窗邊冷光增強；
    /// H3 核已校正：空間穩定、不再閃，但光色維持冷白／中性偏冷，舊化與無人痕跡保留，不回到 H0。
    /// </summary>
    public static class Case01Mood
    {
        public static readonly Color Cool = new Color(0.74f, 0.81f, 1f);
        public static readonly Color ColdNeutral = new Color(0.8f, 0.85f, 0.95f);

        public static bool Ground(Case01Room r)
        {
            return r == Case01Room.Entrance || r == Case01Room.Kitchen || r == Case01Room.Living || r == Case01Room.Dining;
        }

        public static bool House(Case01Room r)
        {
            return Ground(r) || r == Case01Room.Landing || r == Case01Room.Study || r == Case01Room.Bedroom || r == Case01Room.Storage;
        }

        /// <summary>房間主燈在某階段的目標顏色與亮度（由基準值計算，不累積漂移）。</summary>
        public static void RoomLight(int stage, Case01Room room, Color baseColor, float baseIntensity, out Color color, out float intensity)
        {
            color = baseColor;
            intensity = baseIntensity;
            switch (stage)
            {
                case 1:
                    bool first = room == Case01Room.Dining || room == Case01Room.Living;
                    color = Color.Lerp(baseColor, Cool, first ? 0.35f : Ground(room) ? 0.18f : 0.08f);
                    intensity = baseIntensity * (first ? 0.84f : Ground(room) ? 0.93f : 0.97f);
                    break;
                case 2:
                    // 書房稍亮一點，線索仍看得清楚。
                    color = Color.Lerp(baseColor, Cool, room == Case01Room.Study ? 0.6f : 0.65f);
                    intensity = baseIntensity * (room == Case01Room.Study ? 0.78f : 0.7f);
                    break;
                case 3:
                    color = Color.Lerp(baseColor, ColdNeutral, 0.85f);
                    intensity = baseIntensity * 0.72f;
                    break;
            }
        }

        /// <summary>小燈（閱讀燈、檯燈、壁燈）：H1 失溫、H2 失效或不合常理地暗、H3 不再亮回來。</summary>
        public static void Lamp(int stage, int index, Color baseColor, float baseIntensity, out Color color, out float intensity)
        {
            color = baseColor;
            intensity = baseIntensity;
            switch (stage)
            {
                case 1:
                    color = Color.Lerp(baseColor, Cool, 0.2f);
                    intensity = baseIntensity * 0.8f;
                    break;
                case 2:
                    // 有的幾乎熄掉、有的只剩一半──不是整齊的「全部變暗」。
                    color = Color.Lerp(baseColor, Cool, 0.55f);
                    intensity = baseIntensity * (index % 3 == 0 ? 0.12f : index % 3 == 1 ? 0.45f : 0.3f);
                    break;
                case 3:
                    color = Color.Lerp(baseColor, ColdNeutral, 0.75f);
                    intensity = baseIntensity * 0.3f;
                    break;
            }
        }

        /// <summary>吊燈燈泡的自發光倍率與顏色（材質 emissive 也要跟著冷下來）。</summary>
        public static Color BulbEmission(int stage)
        {
            switch (stage)
            {
                case 1: return new Color(0.85f, 0.78f, 0.66f);
                case 2: return new Color(0.42f, 0.46f, 0.55f);
                case 3: return new Color(0.48f, 0.5f, 0.56f);
                default: return new Color(1f, 0.85f, 0.6f);
            }
        }

        /// <summary>吊燈燈罩的顏色與自發光強度（H0 用原本材質）。</summary>
        public static Color ShadeColor(int stage)
        {
            return stage == 1 ? new Color(0.86f, 0.78f, 0.66f) : new Color(0.62f, 0.64f, 0.68f);
        }

        public static float ShadeGlow(int stage)
        {
            return stage == 1 ? 0.35f : stage == 2 ? 0.08f : 0.12f;
        }

        /// <summary>三色環境光（上方／水平／地面）。</summary>
        public static void Ambient(int stage, out Color sky, out Color equator, out Color ground)
        {
            switch (stage)
            {
                case 1:
                    sky = new Color(0.26f, 0.26f, 0.3f);
                    equator = new Color(0.18f, 0.17f, 0.18f);
                    ground = new Color(0.085f, 0.08f, 0.08f);
                    break;
                case 2:
                    sky = new Color(0.22f, 0.24f, 0.3f);
                    equator = new Color(0.14f, 0.15f, 0.18f);
                    ground = new Color(0.065f, 0.07f, 0.08f);
                    break;
                case 3:
                    sky = new Color(0.24f, 0.25f, 0.3f);
                    equator = new Color(0.16f, 0.17f, 0.19f);
                    ground = new Color(0.075f, 0.075f, 0.085f);
                    break;
                default:
                    sky = new Color(0.27f, 0.26f, 0.29f);
                    equator = new Color(0.2f, 0.18f, 0.17f);
                    ground = new Color(0.09f, 0.08f, 0.075f);
                    break;
            }
        }

        /// <summary>窗邊冷光（方向光）：H2 起增強。</summary>
        public static void WindowLight(int stage, out Color color, out float intensity)
        {
            color = stage >= 2 ? new Color(0.6f, 0.68f, 0.9f) : new Color(0.62f, 0.66f, 0.82f);
            intensity = stage == 0 ? 0.16f : stage == 1 ? 0.18f : stage == 2 ? 0.3f : 0.26f;
        }

        /// <summary>舊化程度（0 無；1 局部灰塵；2 灰塵＋污痕＋剝落＋散落紙張；3 灰塵／污痕／剝落保留，紙張收走）。</summary>
        public static int AgingLevel(int stage, Case01Room room)
        {
            if (!House(room) || room == Case01Room.Storage) return 0;
            switch (stage)
            {
                case 1: return room == Case01Room.Dining || room == Case01Room.Living ? 1 : 0;
                case 2: return 2;
                case 3: return 3;
                default: return 0;
            }
        }

        /// <summary>核與夾層燈：異常時紫、反制時紅、校正後冷白（不再是暖光）。</summary>
        public static readonly Color CoreStable = new Color(0.8f, 0.87f, 1f);
        public static readonly Color MezzStable = new Color(0.74f, 0.79f, 0.9f);
    }
}
