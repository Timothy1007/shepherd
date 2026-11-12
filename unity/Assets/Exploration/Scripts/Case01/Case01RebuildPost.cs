using Shepherd.Battle.UI.Arena;
using UnityEngine;
#if SHEPHERD_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// 全屋重製 QA 的畫面後製（曝光、電影式色調映射、分離色調、暗角、顆粒；URP 下另有 Bloom）。只掛在這個 QA 的鏡頭上。
    /// 內建管線：OnRenderImage + 自製 shader。URP：建立只屬於這個鏡頭的全域 Volume（離開 QA 時一起銷毀）。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class Case01RebuildPost : MonoBehaviour
    {
        public float Exposure = 1f;
        public bool Enabled = true;
        private Material mat;

        public Material Material
        {
            get
            {
                if (mat == null)
                {
                    var sh = Shader.Find("Hidden/Case01RebuildPost");
                    if (sh != null) mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
                }
                return mat;
            }
        }

        public bool Available
        {
            get
            {
#if SHEPHERD_URP
                if (ShepherdShading.Urp) return true;
#endif
                return Material != null;
            }
        }

#if SHEPHERD_URP
        private GameObject volumeGo;
        private VolumeProfile profile;
        private ColorAdjustments colorAdjust;
        private Bloom bloom;
        public Volume Volume { get; private set; }

        private void EnsureVolume()
        {
            if (Volume != null) return;
            var cam = GetComponent<Camera>();
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            colorAdjust = profile.Add<ColorAdjustments>(true);
            colorAdjust.contrast.Override(8f);
            colorAdjust.saturation.Override(4f);
            var split = profile.Add<SplitToning>(true);
            split.highlights.Override(new Color(1f, 0.86f, 0.68f));
            split.shadows.Override(new Color(0.55f, 0.66f, 0.85f));
            split.balance.Override(-10f);
            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.65f);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.32f);
            vig.smoothness.Override(0.45f);
            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.18f);

            volumeGo = new GameObject("Case01RebuildPostVolume") { hideFlags = HideFlags.DontSave };
            volumeGo.transform.SetParent(transform, false);
            Volume = volumeGo.AddComponent<Volume>();
            Volume.isGlobal = true;
            Volume.priority = 50f;
            Volume.sharedProfile = profile;
        }

        private void LateUpdate()
        {
            if (!ShepherdShading.Urp) return;
            EnsureVolume();
            Volume.weight = Enabled ? 1f : 0f;
            // 內建管線版本的曝光 1.0 ≈ URP 的 +0.7 EV（舊版在 shader 內乘 1.6）。
            colorAdjust.postExposure.Override(Mathf.Log(Mathf.Max(0.05f, Exposure) * 1.6f, 2f));
        }

        /// <summary>立即套用（測試與截圖不必等 LateUpdate）。</summary>
        public void Apply() { if (ShepherdShading.Urp) LateUpdate(); }
#else
        public void Apply() { }
#endif

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            var m = Material;
            if (!Enabled || m == null)
            {
                Graphics.Blit(src, dst);
                return;
            }
            m.SetFloat("_Exposure", Exposure);
            Graphics.Blit(src, dst, m);
        }

        private void OnDestroy()
        {
            if (mat != null) DestroyImmediate(mat);
#if SHEPHERD_URP
            if (volumeGo != null) DestroyImmediate(volumeGo);
            if (profile != null) DestroyImmediate(profile);
#endif
        }
    }
}
