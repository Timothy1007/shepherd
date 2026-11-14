using UnityEngine;
using UnityEngine.UI;
#if SHEPHERD_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 風格測試（參考《冤罪殺機》的手繪感半寫實；借表現手法，不借設計語言）：
    /// - shader 全域開關 _ShepStylize：貼圖細節變少、光分成柔和色階、暗部偏冷藍紫、亮部偏暖、朝上的面略亮、邊緣暖色勾邊。
    /// - 調色：暗部冷、亮部暖（琥珀色提燈 × 冷藍月光）、對比與飽和略高、Bloom 與暗角。
    /// - 淡淡的冷色霧：讓遠處退後、輪廓更清楚。
    /// 只屬於提燈 QA（自己的 Volume，優先權高於共用的後製），關閉時完全還原。
    /// </summary>
    public sealed class LanternStyle
    {
        public static readonly int StylizeId = Shader.PropertyToID("_ShepStylize");
        public bool On { get; private set; }
        /// <summary>全畫面油彩濾鏡（Kuwahara）。</summary>
        public Material PaintMaterial { get; private set; }
        /// <summary>
        /// 全畫面油彩濾鏡要不要用。美術方向 v1（2026-11-14）：關掉（會把畫面抹糊；看火人風格要乾淨的邊緣和平塗的色塊）。
        /// </summary>
        public bool UsePaint = false;
        public static readonly int FadedId = Shader.PropertyToID("_ShepFaded");
        /// <summary>白天的程度（0＝夜晚、1＝白天）。</summary>
        public float Day { get; private set; }
#if SHEPHERD_URP
        private ColorAdjustments ca;
        private Vignette vig;
        private Bloom bloom;
#endif

        /// <summary>
        /// 白天（美術方向 v1）：亮、乾淨、淡淡的暖色霧氣；異域改成「褪色」而不是黑。indoor＝在屋子裡（霧淡一點）。
        /// </summary>
        public void SetDay(float k, bool indoor)
        {
            Day = Mathf.Clamp01(k);
            Shader.SetGlobalFloat(FadedId, Day > 0.5f ? 1f : 0f);
#if SHEPHERD_URP
            if (ca != null)
            {
                ca.postExposure.Override(Mathf.Lerp(0.45f, 0.25f, Day));
                ca.contrast.Override(Mathf.Lerp(16f, 6f, Day));
                ca.saturation.Override(Mathf.Lerp(14f, 8f, Day));
                vig.intensity.Override(Mathf.Lerp(0.33f, 0.16f, Day));
                bloom.threshold.Override(Mathf.Lerp(0.85f, 1.05f, Day));
            }
#endif
            if (On)
            {
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = Color.Lerp(new Color(0.035f, 0.045f, 0.07f), indoor ? new Color(0.72f, 0.7f, 0.68f) : new Color(0.8f, 0.82f, 0.86f), Day);
                RenderSettings.fogDensity = Mathf.Lerp(0.045f, indoor ? 0.012f : 0.018f, Day);
            }
        }
        private readonly Camera cam;
        private RenderTexture screenRt;
        private GameObject paintCanvas;
        private RawImage paintImage;
        private readonly bool savedFog;
        private readonly Color savedFogColor;
        private readonly FogMode savedFogMode;
        private readonly float savedFogDensity;
#if SHEPHERD_URP
        private GameObject volumeGo;
        private VolumeProfile profile;
        private Volume volume;
#endif

        public LanternStyle(Camera cam = null)
        {
            this.cam = cam;
            var ps = Shader.Find("Hidden/Shepherd/LanternPaint");
            if (ps == null) { var loaded = Resources.Load<Shader>("LanternPaint"); ps = loaded; }
            if (ps != null)
            {
                PaintMaterial = new Material(ps) { name = "LanternPaint", hideFlags = HideFlags.DontSave };
                PaintMaterial.SetFloat("_Radius", 4f);
                PaintMaterial.SetFloat("_Ink", 0.45f);
                PaintMaterial.SetFloat("_Grain", 0.03f);
            }
            savedFog = RenderSettings.fog;
            savedFogColor = RenderSettings.fogColor;
            savedFogMode = RenderSettings.fogMode;
            savedFogDensity = RenderSettings.fogDensity;
#if SHEPHERD_URP
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.hideFlags = HideFlags.DontSave;
            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.92f, 0.95f, 1.08f, 0f));
            smh.midtones.Override(new Vector4(1.0f, 0.98f, 0.97f, 0f));
            smh.highlights.Override(new Vector4(1.12f, 1.02f, 0.88f, 0.02f));
            ca = profile.Add<ColorAdjustments>(true);
            ca.contrast.Override(16f);
            ca.saturation.Override(14f);
            ca.postExposure.Override(0.45f); // ACES 會壓暗一點，曝光補回來
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES); // 電影感的亮部（燈不會死白）
            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.22f);
            grain.response.Override(0.85f);
            bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.75f);
            bloom.threshold.Override(0.85f);
            bloom.scatter.Override(0.75f);
            bloom.tint.Override(new Color(1f, 0.85f, 0.7f));
            vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.33f);
            vig.smoothness.Override(0.5f);
            vig.color.Override(new Color(0.02f, 0.02f, 0.05f));
            volumeGo = new GameObject("LanternStyleVolume") { hideFlags = HideFlags.DontSave };
            volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 60f;
            volume.sharedProfile = profile;
            volume.weight = 0f;
#endif
        }

        public void Set(bool on)
        {
            On = on;
            Shader.SetGlobalFloat(StylizeId, on ? 1f : 0f);
#if SHEPHERD_URP
            if (volume != null) volume.weight = on ? 1f : 0f;
#endif
            UpdateScreenPaint();
            if (on)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = new Color(0.035f, 0.045f, 0.07f);
                RenderSettings.fogDensity = 0.045f;
            }
            else
            {
                RenderSettings.fog = savedFog;
                RenderSettings.fogColor = savedFogColor;
                RenderSettings.fogMode = savedFogMode;
                RenderSettings.fogDensity = savedFogDensity;
            }
        }

        /// <summary>截圖用：把 src 畫成油彩的樣子寫進 dst。</summary>
        public void Paint(RenderTexture src, RenderTexture dst)
        {
            if (PaintMaterial != null) Graphics.Blit(src, dst, PaintMaterial);
            else Graphics.Blit(src, dst);
        }

        /// <summary>
        /// 播放時：鏡頭先畫到一張貼圖，再由最底層的 UI 用油彩濾鏡鋪滿畫面（不用改共用的 URP 設定）。
        /// 編輯器模式（測試、截圖）不做這件事。
        /// </summary>
        public void Tick()
        {
            if (!Application.isPlaying || cam == null) return;
            if (On && PaintMaterial != null && (screenRt == null || screenRt.width != Screen.width || screenRt.height != Screen.height)) UpdateScreenPaint();
        }

        private void UpdateScreenPaint()
        {
            if (!Application.isPlaying || cam == null) return;
            bool use = On && PaintMaterial != null && UsePaint;
            if (!use)
            {
                if (cam.targetTexture == screenRt) cam.targetTexture = null;
                if (paintCanvas != null) paintCanvas.SetActive(false);
                return;
            }
            if (screenRt == null || screenRt.width != Screen.width || screenRt.height != Screen.height)
            {
                if (cam.targetTexture == screenRt) cam.targetTexture = null;
                if (screenRt != null) { screenRt.Release(); Object.Destroy(screenRt); }
                screenRt = new RenderTexture(Mathf.Max(64, Screen.width), Mathf.Max(64, Screen.height), 24, RenderTextureFormat.DefaultHDR) { name = "LanternPaintScreen", antiAliasing = 1 };
            }
            if (paintCanvas == null)
            {
                paintCanvas = new GameObject("LanternPaintCanvas");
                var canvas = paintCanvas.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = -100;
                paintImage = new GameObject("Paint").AddComponent<RawImage>();
                paintImage.transform.SetParent(paintCanvas.transform, false);
                paintImage.rectTransform.anchorMin = Vector2.zero;
                paintImage.rectTransform.anchorMax = Vector2.one;
                paintImage.rectTransform.offsetMin = paintImage.rectTransform.offsetMax = Vector2.zero;
                paintImage.raycastTarget = false;
                paintImage.material = PaintMaterial;
            }
            paintCanvas.SetActive(true);
            paintImage.texture = screenRt;
            cam.targetTexture = screenRt;
        }

        public void Destroy()
        {
            if (cam != null && cam.targetTexture == screenRt) cam.targetTexture = null;
            if (paintCanvas != null) Object.Destroy(paintCanvas);
            if (screenRt != null) { screenRt.Release(); Object.DestroyImmediate(screenRt); }
            if (PaintMaterial != null) Object.DestroyImmediate(PaintMaterial);
            Shader.SetGlobalFloat(StylizeId, 0f);
            RenderSettings.fog = savedFog;
            RenderSettings.fogColor = savedFogColor;
            RenderSettings.fogMode = savedFogMode;
            RenderSettings.fogDensity = savedFogDensity;
#if SHEPHERD_URP
            if (volumeGo != null) Object.DestroyImmediate(volumeGo);
            if (profile != null) Object.DestroyImmediate(profile);
#endif
        }
    }
}
