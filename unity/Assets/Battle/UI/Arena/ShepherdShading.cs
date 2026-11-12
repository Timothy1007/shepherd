using UnityEngine;
using UnityEngine.Rendering;

namespace Shepherd.Battle.UI.Arena
{
    /// <summary>
    /// 材質相容層：同一套程式在內建管線（Standard）與 URP（Universal Render Pipeline/Lit）下都能建立正確的 PBR 材質。
    /// 兩條管線的屬性名稱、關鍵字與透明設定不同，所有程式建立的材質都透過這裡設定。
    /// </summary>
    public static class ShepherdShading
    {
        public const string UrpLitName = "Universal Render Pipeline/Lit";
        public const string StandardName = "Standard";

        public static bool Urp => GraphicsSettings.currentRenderPipeline != null;

        private static Shader standard, urpLit;

        public static Shader Lit
        {
            get
            {
                if (Urp)
                {
                    if (urpLit == null) urpLit = Shader.Find(UrpLitName);
                    if (urpLit != null) return urpLit;
                }
                if (standard == null) standard = Shader.Find(StandardName);
                return standard;
            }
        }

        public static bool IsUrpLit(Material m) => m != null && m.shader != null && m.shader.name == UrpLitName;

        public static string SmoothnessProperty(Material m) => IsUrpLit(m) ? "_Smoothness" : "_Glossiness";
        public static string MetallicMapKeyword(Material m) => IsUrpLit(m) ? "_METALLICSPECGLOSSMAP" : "_METALLICGLOSSMAP";

        public static Material NewLit(string name = null)
        {
            var m = new Material(Lit);
            if (name != null) m.name = name;
            return m;
        }

        public static void SetSmoothness(Material m, float v)
        {
            m.SetFloat(SmoothnessProperty(m), v);
            if (!IsUrpLit(m) && m.HasProperty("_GlossMapScale") && !m.IsKeywordEnabled("_METALLICGLOSSMAP")) m.SetFloat("_GlossMapScale", v);
        }

        public static float GetSmoothness(Material m) => m.GetFloat(SmoothnessProperty(m));

        public static void SetNormalMap(Material m, Texture t, float scale = 1f)
        {
            m.SetTexture("_BumpMap", t);
            m.SetFloat("_BumpScale", scale);
            if (t != null) m.EnableKeyword("_NORMALMAP");
            else m.DisableKeyword("_NORMALMAP");
        }

        /// <summary>金屬（R）＋光滑度（A）貼圖；光滑度倍率設為 1。</summary>
        public static void SetMetallicGlossMap(Material m, Texture t)
        {
            m.SetTexture("_MetallicGlossMap", t);
            if (t == null) { m.DisableKeyword(MetallicMapKeyword(m)); return; }
            m.EnableKeyword(MetallicMapKeyword(m));
            if (IsUrpLit(m))
            {
                m.SetFloat("_Smoothness", 1f);
                m.SetFloat("_SmoothnessTextureChannel", 0f);
            }
            else m.SetFloat("_GlossMapScale", 1f);
        }

        public static bool HasMetallicGlossMap(Material m) => m.GetTexture("_MetallicGlossMap") != null && m.IsKeywordEnabled(MetallicMapKeyword(m));

        public static void SetEmission(Material m, Color c, Texture map = null)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", c);
            if (map != null) m.SetTexture("_EmissionMap", map);
        }

        /// <summary>一般半透明（Standard 的 Fade）。</summary>
        public static void MakeFade(Material m) => MakeTransparent(m, false);

        /// <summary>保留反光的半透明（Standard 的 Transparent，預乘 alpha），用於玻璃。</summary>
        public static void MakeGlass(Material m) => MakeTransparent(m, true);

        private static void MakeTransparent(Material m, bool premultiply)
        {
            if (IsUrpLit(m))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", premultiply ? 1f : 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)(premultiply ? BlendMode.One : BlendMode.SrcAlpha));
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (premultiply) m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                else m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.SetShaderPassEnabled("DepthOnly", false);
                m.renderQueue = (int)RenderQueue.Transparent;
                return;
            }
            m.SetFloat("_Mode", premultiply ? 3f : 2f);
            m.SetInt("_SrcBlend", (int)(premultiply ? BlendMode.One : BlendMode.SrcAlpha));
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            if (premultiply) { m.DisableKeyword("_ALPHABLEND_ON"); m.EnableKeyword("_ALPHAPREMULTIPLY_ON"); }
            else { m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON"); }
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        /// <summary>鏤空（葉片、流蘇）。</summary>
        public static void MakeCutout(Material m, float cutoff = 0.5f)
        {
            m.SetFloat("_Cutoff", cutoff);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            if (IsUrpLit(m)) m.SetFloat("_AlphaClip", 1f);
            else m.SetFloat("_Mode", 1f);
        }

        public static bool IsTransparent(Material m) => m.renderQueue >= (int)RenderQueue.Transparent - 500 && m.GetFloat("_ZWrite") < 0.5f;

        /// <summary>把內建 Standard 材質就地轉成 URP Lit（保留顏色、貼圖、光滑度、金屬、法線、自發光、透明模式）。</summary>
        public static bool UpgradeFromStandard(Material m)
        {
            if (m == null || m.shader == null) return false;
            var name = m.shader.name;
            if (name != StandardName && name != "Standard (Specular setup)") return false;
            var lit = Shader.Find(UrpLitName);
            if (lit == null) return false;

            var color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            var main = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            var st = m.HasProperty("_MainTex") ? new Vector4(m.mainTextureScale.x, m.mainTextureScale.y, m.mainTextureOffset.x, m.mainTextureOffset.y) : new Vector4(1, 1, 0, 0);
            var bump = m.GetTexture("_BumpMap");
            float bumpScale = m.HasProperty("_BumpScale") ? m.GetFloat("_BumpScale") : 1f;
            var ms = m.HasProperty("_MetallicGlossMap") ? m.GetTexture("_MetallicGlossMap") : null;
            bool msOn = ms != null && m.IsKeywordEnabled("_METALLICGLOSSMAP");
            float gloss = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.5f;
            float glossScale = m.HasProperty("_GlossMapScale") ? m.GetFloat("_GlossMapScale") : 1f;
            float metal = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
            var occ = m.HasProperty("_OcclusionMap") ? m.GetTexture("_OcclusionMap") : null;
            float occStrength = m.HasProperty("_OcclusionStrength") ? m.GetFloat("_OcclusionStrength") : 1f;
            bool emissive = m.IsKeywordEnabled("_EMISSION");
            var emission = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            var emissionMap = m.HasProperty("_EmissionMap") ? m.GetTexture("_EmissionMap") : null;
            float mode = m.HasProperty("_Mode") ? m.GetFloat("_Mode") : 0f;
            float cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;
            var gi = m.globalIlluminationFlags;

            m.shader = lit;
            m.shaderKeywords = new string[0];
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", main);
            m.SetTextureScale("_BaseMap", new Vector2(st.x, st.y));
            m.SetTextureOffset("_BaseMap", new Vector2(st.z, st.w));
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Smoothness", gloss);
            if (bump != null) SetNormalMap(m, bump, bumpScale);
            if (msOn)
            {
                SetMetallicGlossMap(m, ms);
                m.SetFloat("_Smoothness", glossScale);
            }
            if (occ != null)
            {
                m.SetTexture("_OcclusionMap", occ);
                m.SetFloat("_OcclusionStrength", occStrength);
                m.EnableKeyword("_OCCLUSIONMAP");
            }
            if (emissive) SetEmission(m, emission, emissionMap);
            m.globalIlluminationFlags = gi;
            m.renderQueue = -1;
            if (mode >= 2.5f) MakeGlass(m);
            else if (mode >= 1.5f) MakeFade(m);
            else if (mode >= 0.5f) MakeCutout(m, cutoff);
            else
            {
                m.SetFloat("_Surface", 0f);
                m.SetOverrideTag("RenderType", "");
            }
            return true;
        }
    }
}
