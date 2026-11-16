using System.Collections.Generic;
using UnityEngine;
using Mode = Shepherd.Exploration.Lantern.LanternReveal.Mode;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 測試：整間改衣店都換成卡通 shader（Shepherd/PainterlyReveal），模型和貼圖不重做（使用者 2026-11-17）。
    /// 用來看「只換 shader」能到什麼程度、哪些東西一定要重做。只在服飾店測試模式按 T 切換，正式畫面不受影響。
    /// 換的範圍：中心在店面裡、雙狀態（Dual／AlwaysReal）、不發光的不透明材質；只存在於真實／異域的東西（例如衣架後的照片）、
    /// 玻璃、燈泡、文字維持原樣，提燈揭露的規則照舊。
    /// </summary>
    public sealed partial class LanternGraybox
    {
        public bool ShopToon { get; private set; }
        private readonly List<(Renderer r, Material[] mats)> shopToonSaved = new List<(Renderer, Material[])>();
        private readonly Dictionary<Material, Material> shopToonCache = new Dictionary<Material, Material>();

        public int SetShopToon(bool on)
        {
            if (on == ShopToon) return shopToonSaved.Count;
            ShopToon = on;
            if (!on)
            {
                foreach (var (r, mats) in shopToonSaved) if (r != null) r.sharedMaterials = mats;
                shopToonSaved.Clear();
                return 0;
            }
            var toon = Shader.Find("Shepherd/PainterlyReveal");
            if (toon == null || V2Root == null || V2ShopRoom.size.sqrMagnitude <= 0f) { ShopToon = false; return 0; }
            var room = V2ShopRoom;
            room.Expand(new Vector3(0.6f, 0.6f, 0.6f));
            foreach (var r in V2Root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is ParticleSystemRenderer || r.gameObject.layer == LanternRig.ViewLayer) continue;
                if (!room.Contains(r.bounds.center)) continue;
                var src = r.sharedMaterials;
                Material[] dst = null;
                for (int i = 0; i < src.Length; i++)
                {
                    var t = ToonOf(src[i], toon);
                    if (t == null) continue;
                    if (dst == null) dst = (Material[])src.Clone();
                    dst[i] = t;
                }
                if (dst == null) continue;
                shopToonSaved.Add((r, src));
                r.sharedMaterials = dst;
            }
            return shopToonSaved.Count;
        }

        /// <summary>LanternReveal 材質 → 卡通版（同一張底圖、同一個顏色）；不適合換的回傳 null。</summary>
        private Material ToonOf(Material m, Shader toon)
        {
            if (m == null || m.shader == toon) return null;
            if (shopToonCache.TryGetValue(m, out var cached)) return cached;
            Material t = null;
            if (m.shader == LanternReveal.Shader && m.renderQueue < 2500)
            {
                var mode = LanternReveal.GetMode(m);
                bool glows = m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent > 0.15f;
                if ((mode == Mode.Dual || mode == Mode.AlwaysReal) && !glows)
                {
                    t = new Material(toon) { name = m.name + "_ToonTest" };
                    if (m.HasProperty("_BaseMap")) { t.SetTexture("_BaseMap", m.GetTexture("_BaseMap")); t.SetTextureScale("_BaseMap", m.GetTextureScale("_BaseMap")); t.SetTextureOffset("_BaseMap", m.GetTextureOffset("_BaseMap")); }
                    if (m.HasProperty("_BaseColor")) t.SetColor("_BaseColor", m.GetColor("_BaseColor"));
                    if (m.HasProperty("_WorldUV")) t.SetFloat("_WorldUV", m.GetFloat("_WorldUV"));
                    if (m.HasProperty("_Metallic") && m.GetFloat("_Metallic") > 0.5f) { t.SetFloat("_SpecStrength", 0.8f); t.SetFloat("_SpecSize", 0.3f); }
                    if (m.HasProperty("_Cull")) t.SetFloat("_Cull", m.GetFloat("_Cull"));
                    if (mode == Mode.AlwaysReal) t.SetFloat("_FadedSat", 1f);   // 現實世界的東西：不褪色
                }
            }
            shopToonCache[m] = t;
            return t;
        }
    }
}