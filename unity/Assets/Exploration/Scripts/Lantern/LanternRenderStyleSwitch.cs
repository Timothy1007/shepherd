using System.Collections.Generic;
using UnityEngine;
#if SHEPHERD_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 第十七號門｜三種實機渲染即時切換 QA（docs/CH17_RENDER_STYLE_SWITCH_QA_TASK.md）。
    /// 只存在於獨立的 QA 場景（Assets/Exploration/Scenes/LanternRenderStyleQA.unity），不會出現在正式探索場景。
    ///
    /// 三種模式用同一份場景、同一批模型與位置，只改「怎麼畫」：
    ///   A｜Cinematic Semi-Realistic：自然 PBR（關掉手繪光照）、完整貼圖細節、餐廳補光、溫和的後製。
    ///   B｜Painterly Semi-Realistic：A 的光影＋克制的繪畫材質層（世界座標的顏料色差、短筆觸明度與粗糙度變化）、輕度柔和光階、柔光後製。
    ///   C｜Stylized Cinematic：手繪光階全開、暖色主燈加強、補光減弱讓暗部更深、起居區偏冷，後製加強冷暖分離、對比與暗角聚焦。
    /// 切換只改 shader 全域參數、燈光強度／顏色、一個高優先權的 Volume，不重載場景、不動玩家位置與提燈狀態；關閉時全部還原。
    /// </summary>
    public sealed class LanternRenderStyleSwitch : MonoBehaviour
    {
        public enum Style { A, B, C }

        public static readonly string[] Names =
        {
            "A｜Cinematic Semi-Realistic（自然寫實）",
            "B｜Painterly Semi-Realistic（繪畫質感）",
            "C｜Stylized Cinematic（風格化電影感）",
        };

        public static readonly int StylizeId = Shader.PropertyToID("_ShepStylize");
        public static readonly int PaintId = Shader.PropertyToID("_ShepPaint");

        public Style Current { get; private set; } = Style.A;
        public bool LabelsVisible = true;
        /// <summary>切換次數（測試用）。</summary>
        public int Switches { get; private set; }
        public LanternPrototypeQA Qa { get; private set; }

        private struct LightBase { public Light L; public float I; public Color C; }
        private readonly List<LightBase> lights = new List<LightBase>();
        private Transform lightsFrom;
        private float savedStylize, savedPaint;
        private bool applied;
        private GUIStyle labelStyle, buttonStyle, hintStyle;
#if SHEPHERD_URP
        private GameObject volumeGo;
        private readonly VolumeProfile[] profiles = new VolumeProfile[3];
        private Volume volume;
#endif

        private void Awake()
        {
            // 餐廳以最新完成的美術版本為基底：E1 材質＋E2 補光（必須在 LanternPrototypeQA.Start 建房子之前設定）。
            LanternGraybox.DiningArtExperiment = 3;
        }

        private void OnDestroy()
        {
            Restore();
            LanternGraybox.DiningArtExperiment = 0;
        }

        private void Start()
        {
            Bind(FindObjectOfType<LanternPrototypeQA>());
        }

        private bool entered;

        /// <summary>接上 QA（測試也會直接呼叫）。</summary>
        public void Bind(LanternPrototypeQA qa)
        {
            Qa = qa;
            if (Qa == null) return;
            Qa.RenderStyleQA = true;
            savedStylize = Shader.GetGlobalFloat(StylizeId);
            savedPaint = Shader.GetGlobalFloat(PaintId);
            BuildProfiles();
            Apply(Current);
        }

        /// <summary>把玩家放進餐廳（起居區靠門口，面向壁爐），提燈先放下。</summary>
        public void EnterDining()
        {
            if (Qa == null || Qa.World == null) return;
            Qa.GoTo("V2Dining");
            var o = LanternGraybox.V2Origin;
            var eye = o + new Vector3(0.125f + 4.6f, 0.05f, 6.56f + 1.6f);
            Qa.Player.Teleport(eye, -110f, 0f);
            Qa.Rig.SetOn(false);
        }

        public void Cycle() => Apply((Style)(((int)Current + 1) % 3));

        public void Apply(Style s)
        {
            Current = s;
            Switches++;
            applied = true;
            ApplyNow();
        }

        private void ApplyNow()
        {
            // 1. Shader 全域參數。
            float stylize = Current == Style.A ? 0f : Current == Style.B ? 0.45f : 1f;
            float paint = Current == Style.A ? 0f : Current == Style.B ? 1f : 0.45f;
            Shader.SetGlobalFloat(StylizeId, stylize);
            Shader.SetGlobalFloat(PaintId, paint);
            // 2. 燈光（只動餐廳的燈）。
            CollectLights();
            foreach (var b in lights)
            {
                if (b.L == null) continue;
                float k = 1f; Color c = b.C;
                string n = b.L.name;
                bool bounce = n.StartsWith("DN_E2_");
                if (Current == Style.B)
                {
                    if (bounce) k = 1.15f;
                    c = Color.Lerp(b.C, b.C * new Color(1.02f, 0.98f, 0.92f), 0.6f);
                }
                else if (Current == Style.C)
                {
                    if (bounce) k = 0.35f;
                    else if (n == "DN_PendantLight" || n == "DN_PendantDown") { k = 1.35f; c = new Color(1f, 0.7f, 0.42f); }
                    else if (n == "DN_SconceLight") { k = 1.3f; c = new Color(1f, 0.68f, 0.4f); }
                    else if (n == "DN_SittingLight") { k = 0.6f; c = new Color(0.72f, 0.8f, 1f); }
                }
                b.L.intensity = b.I * k;
                b.L.color = c;
            }
#if SHEPHERD_URP
            if (volume != null) volume.sharedProfile = profiles[(int)Current];
#endif
        }

        private void CollectLights()
        {
            var root = Qa != null && Qa.World != null ? Qa.World.V2Root : null;
            bool stale = root != lightsFrom;
            foreach (var b in lights) if (b.L == null) { stale = true; break; }
            if (!stale) return;
            // 先把舊的燈還原（如果還在），再重新收集。
            foreach (var b in lights) if (b.L != null) { b.L.intensity = b.I; b.L.color = b.C; }
            lights.Clear();
            lightsFrom = root;
            if (root == null) return;
            foreach (var l in root.GetComponentsInChildren<Light>(true))
                if (l.name.StartsWith("DN_")) lights.Add(new LightBase { L = l, I = l.intensity, C = l.color });
        }

        private void LateUpdate()
        {
            if (Qa == null || !Qa.Ready || !applied) return;
            ApplyFrame();
        }

        /// <summary>
        /// 每格套用一次（遊玩時由 LateUpdate 呼叫；編輯模式截圖由 QA 工具在每次 Step 後呼叫）。
        /// QA 本身每格會重設環境光、也可能被 F1／R 重建房子，所以每格重新套用目前模式（只是幾個全域參數＋燈光，很便宜）。
        /// </summary>
        public void ApplyFrame()
        {
            if (!applied) return;
            ApplyNow();
            if (Current == Style.C) RenderSettings.ambientLight *= new Color(0.78f, 0.84f, 1f) * 0.85f;
            else if (Current == Style.B) RenderSettings.ambientLight *= new Color(1.03f, 1f, 0.95f);
        }

        private void Update()
        {
            if (!Application.isPlaying || Qa == null || !Qa.Ready) return;
            // QA 和這個元件的 Start 順序不固定：等 QA 準備好之後才把玩家放進餐廳（只做一次）。
            if (!entered) { entered = true; EnterDining(); }
            if (Input.GetKeyDown(KeyCode.F6)) Cycle();
            if (Input.GetKeyDown(KeyCode.U)) LabelsVisible = !LabelsVisible;
            // 按住 Alt：放開滑鼠，可以直接點右上角的 A／B／C（放開 Alt 自動回到鏡頭控制）。
            Qa.CursorUiOpen = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }

        private void OnGUI()
        {
            if (Qa == null || !Qa.Ready) return;
            if (labelStyle == null)
            {
                var font = LanternFonts.UI;
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleRight, font = font };
                labelStyle.normal.textColor = new Color(1f, 0.97f, 0.9f);
                hintStyle = new GUIStyle(labelStyle) { fontSize = 13 };
                hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold, font = font };
            }
            float w = Screen.width;
            if (!LabelsVisible)
            {
                GUI.Label(new Rect(w - 260, 8, 250, 22), "U 顯示渲染模式標籤", hintStyle);
                return;
            }
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(w - 470, 6, 464, 96), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(w - 466, 10, 452, 26), "渲染模式：" + Names[(int)Current], labelStyle);
            for (int i = 0; i < 3; i++)
            {
                var r = new Rect(w - 170 + i * 54, 40, 48, 32);
                var prev = GUI.backgroundColor;
                if (i == (int)Current) GUI.backgroundColor = new Color(1f, 0.75f, 0.4f);
                if (GUI.Button(r, ((Style)i).ToString(), buttonStyle)) Apply((Style)i);
                GUI.backgroundColor = prev;
            }
            GUI.Label(new Rect(w - 466, 76, 452, 22), "F6 切換・按住 Alt 用滑鼠點 A／B／C・U 隱藏標籤・F 提燈", hintStyle);
        }

        private void BuildProfiles()
        {
#if SHEPHERD_URP
            if (volumeGo != null) return;
            for (int i = 0; i < 3; i++)
            {
                var p = ScriptableObject.CreateInstance<VolumeProfile>();
                p.hideFlags = HideFlags.DontSave;
                p.name = "RenderStyle_" + (Style)i;
                var tone = p.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
                var ca = p.Add<ColorAdjustments>(true);
                var smh = p.Add<ShadowsMidtonesHighlights>(true);
                var bloom = p.Add<Bloom>(true);
                var vig = p.Add<Vignette>(true);
                var grain = p.Add<FilmGrain>(true);
                var split = p.Add<SplitToning>(true);
                var lgg = p.Add<LiftGammaGain>(true);
                vig.smoothness.Override(0.5f);
                vig.color.Override(new Color(0.02f, 0.02f, 0.04f));
                grain.type.Override(FilmGrainLookup.Thin1);
                if (i == 0)
                {
                    // A：中性、可信。曝光補回 ACES、對比溫和、幾乎不加色偏、Bloom 只在燈泡上。
                    ca.postExposure.Override(0.35f); ca.contrast.Override(5f); ca.saturation.Override(2f);
                    smh.shadows.Override(new Vector4(1f, 1f, 1f, 0f)); smh.midtones.Override(new Vector4(1f, 1f, 1f, 0f)); smh.highlights.Override(new Vector4(1f, 1f, 1f, 0f));
                    bloom.intensity.Override(0.3f); bloom.threshold.Override(1.1f); bloom.scatter.Override(0.6f); bloom.tint.Override(Color.white);
                    vig.intensity.Override(0.12f);
                    grain.intensity.Override(0f);
                    split.balance.Override(0f); split.shadows.Override(Color.gray); split.highlights.Override(Color.gray);
                    lgg.lift.Override(new Vector4(1f, 1f, 1f, 0f));
                }
                else if (i == 1)
                {
                    // B：像顏料：暗部微微抬起（不死黑）、整體偏暖、柔和的大範圍光暈、很淡的紙感顆粒。
                    ca.postExposure.Override(0.38f); ca.contrast.Override(-4f); ca.saturation.Override(8f);
                    smh.shadows.Override(new Vector4(0.98f, 0.96f, 1.04f, 0.02f)); smh.midtones.Override(new Vector4(1.02f, 1f, 0.97f, 0f)); smh.highlights.Override(new Vector4(1.04f, 1.01f, 0.94f, 0f));
                    bloom.intensity.Override(0.55f); bloom.threshold.Override(0.95f); bloom.scatter.Override(0.85f); bloom.tint.Override(new Color(1f, 0.88f, 0.74f));
                    vig.intensity.Override(0.16f);
                    grain.intensity.Override(0.12f); grain.response.Override(0.9f);
                    split.balance.Override(0f); split.shadows.Override(new Color(0.52f, 0.5f, 0.58f)); split.highlights.Override(new Color(0.6f, 0.54f, 0.46f));
                    lgg.lift.Override(new Vector4(1f, 0.99f, 1.02f, 0.04f));
                }
                else
                {
                    // C：冷暖分離與聚焦：暗部偏藍、亮部偏橘、對比與飽和拉高、暗角把視線收到畫面中央的燈下。
                    ca.postExposure.Override(0.28f); ca.contrast.Override(20f); ca.saturation.Override(14f);
                    smh.shadows.Override(new Vector4(0.88f, 0.94f, 1.12f, -0.02f)); smh.midtones.Override(new Vector4(1f, 0.98f, 0.98f, 0f)); smh.highlights.Override(new Vector4(1.12f, 1.02f, 0.86f, 0.03f));
                    bloom.intensity.Override(0.85f); bloom.threshold.Override(0.85f); bloom.scatter.Override(0.75f); bloom.tint.Override(new Color(1f, 0.82f, 0.6f));
                    vig.intensity.Override(0.34f);
                    grain.intensity.Override(0.18f); grain.response.Override(0.85f);
                    split.balance.Override(-15f); split.shadows.Override(new Color(0.38f, 0.46f, 0.7f)); split.highlights.Override(new Color(0.72f, 0.56f, 0.4f));
                    lgg.lift.Override(new Vector4(0.98f, 0.99f, 1.04f, -0.01f));
                }
                profiles[i] = p;
            }
            volumeGo = new GameObject("RenderStyleQAVolume") { hideFlags = HideFlags.DontSave };
            volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 70f;   // 高於提燈 QA 自己的 Volume（60），只在這個 QA 場景存在
            volume.weight = 1f;
#endif
        }

        /// <summary>還原所有被這個 QA 改過的東西（燈、全域參數、Volume）。</summary>
        public void Restore()
        {
            foreach (var b in lights) if (b.L != null) { b.L.intensity = b.I; b.L.color = b.C; }
            lights.Clear();
            lightsFrom = null;
            if (applied)
            {
                Shader.SetGlobalFloat(StylizeId, savedStylize);
                Shader.SetGlobalFloat(PaintId, savedPaint);
            }
            applied = false;
            if (Qa != null) { Qa.RenderStyleQA = false; Qa.CursorUiOpen = false; }
#if SHEPHERD_URP
            if (volumeGo != null) { if (Application.isPlaying) Destroy(volumeGo); else DestroyImmediate(volumeGo); volumeGo = null; volume = null; }
            for (int i = 0; i < 3; i++) if (profiles[i] != null) { if (Application.isPlaying) Destroy(profiles[i]); else DestroyImmediate(profiles[i]); profiles[i] = null; }
#endif
        }

        /// <summary>每種模式實際改了什麼（顯示在報告與測試裡）。</summary>
        public static string Describe(Style s)
        {
            switch (s)
            {
                case Style.A: return "Shader：_ShepStylize 0（自然 PBR、完整貼圖細節）、_ShepPaint 0｜燈：餐廳補光 1×｜後製：ACES、對比 +5、Bloom 0.3、暗角 0.12、無顆粒";
                case Style.B: return "Shader：_ShepStylize 0.45（柔化光階、貼圖細節收斂成色塊）、_ShepPaint 1（顏料色差＋筆觸明度／粗糙度）｜燈：補光 1.15×、略暖｜後製：暗部抬起、對比 -4、柔光 Bloom 0.55、紙感顆粒 0.12";
                default: return "Shader：_ShepStylize 1（手繪光階全開）、_ShepPaint 0.45｜燈：吊燈／壁燈 1.3×偏橘、補光 0.35×、起居燈偏冷 0.6×、環境光偏冷｜後製：冷暖分離、對比 +20、飽和 +14、Bloom 0.85、暗角 0.34";
            }
        }
    }
}
