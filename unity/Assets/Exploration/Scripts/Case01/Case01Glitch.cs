using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>失序門的路由狀態（只影響外觀；傳送仍完全由 Case01Flow／Portal.Crossed 決定）。</summary>
    public enum Case01PortalState
    {
        /// <summary>未啟動（膜不顯示）。</summary>
        Dormant,
        /// <summary>通往不可能空間、狀態未定。</summary>
        Unstable,
        /// <summary>記號與去向不符（錯接）。</summary>
        Wrong,
        /// <summary>已錨定的可信連接。</summary>
        Anchored,
        /// <summary>核已校正：故障停止，只剩門框可辨認路線。</summary>
        Corrected,
    }

    /// <summary>
    /// v0.4 失序門的「訊號故障」外觀：膜使用 <c>Shepherd/Case01PortalGlitch</c>（電視雪花、掃描線、間歇撕裂），
    /// 門框改成黑灰＋冷白的斷續發光與輕微橫向跳動；取代 v0.3 的整片紫色膜與紫色發光門框。
    /// 降低閃爍時：噪點更新變慢、撕裂減少、門框不跳動。
    /// </summary>
    public static class Case01Glitch
    {
        public const string ShaderResource = "Case01PortalGlitch";
        private static Shader shader;

        public static Shader Shader
        {
            get
            {
                if (shader == null) shader = Resources.Load<Shader>(ShaderResource);
                if (shader == null) shader = Shader.Find("Shepherd/Case01PortalGlitch");
                return shader;
            }
        }

        /// <summary>每扇門一個膜材質（本次建置擁有）。找不到 shader 時退回淡灰半透明（仍不是紫色）。</summary>
        public static Material NewMembrane(Case01Palette p)
        {
            var s = Shader;
            var m = s != null && s.isSupported ? new Material(s) : ArenaArt.FadeMat(new Color(0.55f, 0.57f, 0.62f, 0.5f), new Color(0.2f, 0.21f, 0.24f));
            m.name = "Case01PortalGlitch_instance";
            return p.Own(m);
        }

        /// <summary>門框材質：黑灰，冷白自發光由狀態控制。</summary>
        public static Material NewFrame(Case01Palette p)
        {
            var m = ArenaArt.Mat(new Color(0.16f, 0.16f, 0.18f), 0.25f, new Color(0.3f, 0.32f, 0.36f));
            m.name = "Case01GlitchFrame_instance";
            return p.Own(m);
        }

        public struct Look
        {
            public float Glitch;
            public float Speed;
            public float Alpha;
            public Color Tint;
            public Color Accent;
            /// <summary>門框跳動機率（每 0.1 秒）與幅度（公尺）。</summary>
            public float JitterChance;
            public float JitterAmount;
            public float FrameGlow;
            /// <summary>電流雜音音量倍率。</summary>
            public float Hum;
        }

        public static Look For(Case01PortalState state, bool reduceFlicker)
        {
            var l = new Look { Tint = new Color(0.78f, 0.8f, 0.86f), Accent = new Color(0.5f, 0.42f, 0.62f) };
            switch (state)
            {
                case Case01PortalState.Wrong:
                    l.Glitch = 1f; l.Speed = 1f; l.Alpha = 0.9f; l.JitterChance = 0.3f; l.JitterAmount = 0.035f; l.FrameGlow = 0.9f; l.Hum = 1f;
                    break;
                case Case01PortalState.Unstable:
                    l.Glitch = 0.6f; l.Speed = 0.8f; l.Alpha = 0.85f; l.JitterChance = 0.15f; l.JitterAmount = 0.02f; l.FrameGlow = 0.6f; l.Hum = 0.6f;
                    break;
                case Case01PortalState.Anchored:
                    l.Glitch = 0.18f; l.Speed = 0.4f; l.Alpha = 0.72f; l.FrameGlow = 0.4f; l.Hum = 0.15f;
                    l.Tint = new Color(0.86f, 0.9f, 0.96f);
                    break;
                case Case01PortalState.Corrected:
                    l.Glitch = 0f; l.Speed = 0.15f; l.Alpha = 0.5f; l.FrameGlow = 0.12f; l.Hum = 0f;
                    l.Tint = new Color(0.55f, 0.57f, 0.6f);
                    break;
                default:
                    l.Alpha = 0f;
                    break;
            }
            if (reduceFlicker)
            {
                l.Speed *= 0.25f;
                l.Glitch *= 0.6f;
                l.JitterChance = 0f;
                l.Hum *= 0.6f;
            }
            return l;
        }

        public static void Apply(Material m, Look l)
        {
            if (m == null) return;
            if (m.HasProperty("_Glitch"))
            {
                m.SetFloat("_Glitch", l.Glitch);
                m.SetFloat("_Speed", l.Speed);
                m.SetFloat("_Alpha", l.Alpha);
                m.SetColor("_Tint", l.Tint);
                m.SetColor("_Accent", l.Accent);
            }
            else
            {
                var c = l.Tint;
                c.a = l.Alpha * 0.6f;
                m.color = c;
            }
        }

        /// <summary>程序產生的低音量電流雜音（可循環）。</summary>
        public static float StaticSample(float t, float len)
        {
            unchecked
            {
                int i = (int)(t * Case01Audio.Rate) * 1103515245 + 977;
                i ^= i >> 13;
                float n = ((i & 0xffff) / 32768f) - 1f;
                float buzz = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * 240f * t) * 0.1f;
                float gate = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * t / len * 3f);
                return (n * 0.18f * gate + buzz * 0.35f) * 0.6f;
            }
        }
    }
}
