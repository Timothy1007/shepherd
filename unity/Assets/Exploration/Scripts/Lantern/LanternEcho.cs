using System.Collections.Generic;
using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 牆上的影子殘影（情境式演出，不切走鏡頭）：母女兩個人的影子在牆上重演一段過去，旁邊的字逐字出現（約每秒 25 字）。
    /// 灰盒版本：影子是程式畫的剪影，動作用幾個姿勢切換；之後換成正式的剪影美術。
    /// </summary>
    public sealed class LanternEcho : MonoBehaviour
    {
        public struct Beat
        {
            public float T;
            public float MotherX, GirlX;
            public int MotherPose, GirlPose;
            public string Line;
            /// <summary>誰在說（字幕前面的名字）。</summary>
            public string Who;
        }

        /// <summary>false（預設）＝字不打在牆上，改由畫面下方的字幕顯示（使用者回饋：牆上的字會糊）。</summary>
        public bool WorldText = false;
        public string CurrentWho { get; private set; } = "";

        public const float CharsPerSecond = 25f;
        public Beat[] Beats = new Beat[0];
        public float Duration = 20f;
        public bool Playing { get; private set; }
        public bool Finished { get; private set; }
        public float Time01 => Duration > 0f ? Mathf.Clamp01(time / Duration) : 0f;
        public int LinesShown { get; private set; }
        public string CurrentLine { get; private set; } = "";
        public string VisibleText => text != null ? text.text : "";

        private Transform mother, girl;
        private Renderer motherR, girlR;
        private TextMesh text;
        private Material motherMat, girlMat;
        private float time;
        private int beat = -1;
        private float typed;
        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        public static LanternEcho Create(Transform parent, Vector3 at, Quaternion facing, Beat[] beats, float duration)
        {
            var go = new GameObject("WallEcho");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, facing);
            var e = go.AddComponent<LanternEcho>();
            e.Beats = beats;
            e.Duration = duration;
            e.Build();
            return e;
        }

        private Material ShadowMaterial(string name)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = new Material(sh != null ? sh : Shader.Find("Sprites/Default")) { name = name, hideFlags = HideFlags.DontSave };
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3050;
            m.SetColor("_BaseColor", new Color(0.03f, 0.02f, 0.02f, 0f));
            m.SetColor("_Color", new Color(0.03f, 0.02f, 0.02f, 0f));
            return m;
        }

        private void Build()
        {
            motherMat = ShadowMaterial("EchoMother");
            girlMat = ShadowMaterial("EchoGirl");
            mother = Quad("Mother", motherMat, 0.9f, 1.85f, out motherR);
            girl = Quad("Girl", girlMat, 0.62f, 1.42f, out girlR);
            var tgo = new GameObject("EchoText");
            tgo.transform.SetParent(transform, false);
            tgo.transform.localPosition = new Vector3(0.15f, 0.98f, -0.02f); // 在兩個人的頭上方
            text = tgo.AddComponent<TextMesh>();
            text.font = LanternFonts.UI;
            text.fontSize = 64;
            text.characterSize = 0.03f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            var tm = new Material(text.font.material) { name = "EchoText", hideFlags = HideFlags.DontSave };
            tm.color = new Color(1f, 0.88f, 0.68f);
            tgo.GetComponent<MeshRenderer>().sharedMaterial = tm;
            text.text = "";
            // 可以再看一次：牆上一塊薄薄的感應區（E）。
            var hit = new GameObject("EchoReplay");
            hit.transform.SetParent(transform, false);
            hit.transform.localPosition = new Vector3(0.2f, 0f, -0.04f);
            var bc = hit.AddComponent<BoxCollider>();
            bc.size = new Vector3(2.0f, 2.2f, 0.04f);
            Replay = hit.AddComponent<LanternUsable>();
            Replay.Label = "牆上的影子";
            Replay.PromptFn = () => Playing ? "" : Armed ? "E 看牆上的影子" : "E 再看一次（牆上的影子）";
            Replay.OnUse = () => { if (!Playing) Play(); };
            // 照在牆上的暖光：影子要落在一片亮的牆上才看得出是影子。
            var lgo = new GameObject("EchoLight");
            lgo.transform.SetParent(transform, false);
            lgo.transform.localPosition = new Vector3(0f, -0.1f, -2.6f);
            lgo.transform.localRotation = Quaternion.identity;
            wallLight = lgo.AddComponent<Light>();
            wallLight.type = LightType.Spot;
            wallLight.color = new Color(1f, 0.78f, 0.52f);
            wallLight.range = 5f;
            wallLight.spotAngle = 62f;
            wallLight.innerSpotAngle = 30f;
            wallLight.intensity = 0f;
            wallLight.shadows = LightShadows.None;
            gameObject.SetActive(false);
        }

        public const float WallLightIntensity = 2.2f;
        public LanternUsable Replay { get; private set; }
        private Light wallLight;
        public Light WallLight => wallLight;

        private Transform Quad(string name, Material m, float w, float h, out Renderer r)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(transform, false);
            q.transform.localScale = new Vector3(w, h, 1f);
            q.transform.localRotation = Quaternion.identity;
            r = q.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return q.transform;
        }

        /// <summary>等著被看見：牆上先亮起一片暖光（還沒有影子），玩家看過來才開始演。</summary>
        public bool Armed { get; private set; }
        public int TimesPlayed { get; private set; }

        public void Arm()
        {
            gameObject.SetActive(true);
            Armed = true;
            Playing = false;
            Hide();
            if (wallLight != null) wallLight.intensity = WallLightIntensity * 0.7f;
        }

        public void Play()
        {
            gameObject.SetActive(true);
            Armed = false;
            Playing = true;
            Finished = false;
            TimesPlayed++;
            time = 0f;
            beat = -1;
            typed = 0f;
            LinesShown = 0;
            text.text = "";
        }

        /// <summary>演完（或中斷）：影子、字、光都收掉，物件留著（可以再看一次）。</summary>
        public void Stop()
        {
            Playing = false;
            Armed = false;
            Hide();
        }

        private void Hide()
        {
            var c = new Color(0.03f, 0.02f, 0.02f, 0f);
            motherMat.SetColor("_BaseColor", c); girlMat.SetColor("_BaseColor", c);
            motherMat.SetColor("_Color", c); girlMat.SetColor("_Color", c);
            text.text = "";
            if (wallLight != null) wallLight.intensity = 0f;
        }

        /// <summary>每個影格：推進時間軸、切換姿勢、逐字顯示。skip＝這一格玩家按了跳過（整句直接顯示）。</summary>
        public void Step(float dt, bool skip = false)
        {
            if (!Playing) return;
            time += dt;
            while (beat + 1 < Beats.Length && time >= Beats[beat + 1].T)
            {
                beat++;
                typed = 0f;
                CurrentLine = Beats[beat].Line ?? "";
                CurrentWho = Beats[beat].Who ?? "";
                if (CurrentLine.Length > 0) LinesShown++;
                motherR.sharedMaterial.SetTexture("_BaseMap", Silhouette(false, Beats[beat].MotherPose));
                girlR.sharedMaterial.SetTexture("_BaseMap", Silhouette(true, Beats[beat].GirlPose));
                motherR.sharedMaterial.SetTexture("_MainTex", Silhouette(false, Beats[beat].MotherPose));
                girlR.sharedMaterial.SetTexture("_MainTex", Silhouette(true, Beats[beat].GirlPose));
            }
            if (beat >= 0)
            {
                var b = Beats[beat];
                var next = beat + 1 < Beats.Length ? Beats[beat + 1] : b;
                float span = Mathf.Max(0.01f, next.T - b.T);
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time - b.T) / Mathf.Min(1.2f, span)));
                // 原點在牆上約 1.25 m 高；腳落在離地約 0.15 m（影子投在牆腳）。
                mother.localPosition = new Vector3(Mathf.Lerp(PrevX(true), b.MotherX, k), -0.18f, 0f);
                girl.localPosition = new Vector3(Mathf.Lerp(PrevX(false), b.GirlX, k), -0.39f, 0f);
                typed = skip ? CurrentLine.Length : typed + dt * CharsPerSecond;
                text.text = CurrentLine.Substring(0, Mathf.Min(CurrentLine.Length, Mathf.FloorToInt(typed)));
            }
            // 淡入淡出。
            float a = Mathf.Clamp01(time / 1.0f) * Mathf.Clamp01((Duration - time) / 1.2f) * 0.78f;
            if (wallLight != null) wallLight.intensity = WallLightIntensity * Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(time / 0.8f)) * Mathf.Clamp01((Duration - time) / 1.5f);
            var c = new Color(0.03f, 0.02f, 0.02f, a);
            motherMat.SetColor("_BaseColor", c); girlMat.SetColor("_BaseColor", c);
            motherMat.SetColor("_Color", c); girlMat.SetColor("_Color", c);
            text.GetComponent<MeshRenderer>().enabled = WorldText;
            var tc = text.GetComponent<MeshRenderer>().sharedMaterial;
            tc.color = new Color(1f, 0.88f, 0.68f, Mathf.Clamp01(time / 0.6f) * Mathf.Clamp01((Duration - time) / 1.2f));
            if (time >= Duration) { Finished = true; Stop(); }
        }

        private float PrevX(bool isMother)
        {
            if (beat <= 0) return isMother ? Beats[0].MotherX : Beats[0].GirlX;
            return isMother ? Beats[beat - 1].MotherX : Beats[beat - 1].GirlX;
        }

        // ---------------- 灰盒剪影（程式畫的，之後換正式美術） ----------------

        public static Texture2D Silhouette(bool child, int pose)
        {
            string key = (child ? "g" : "m") + pose;
            Texture2D t;
            if (textures.TryGetValue(key, out t) && t != null) return t;
            const int w = 128, h = 256;
            t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Echo_" + key, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            void Disc(float cx, float cy, float r)
            {
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    byte a = (byte)(Mathf.Clamp01(r + 1.5f - d) * 255f);
                    if (a > px[y * w + x].a) px[y * w + x] = new Color32(255, 255, 255, a);
                }
            }
            void Capsule(Vector2 a0, Vector2 a1, float r)
            {
                var ab = a1 - a0;
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(x, y);
                    float tt = Mathf.Clamp01(Vector2.Dot(p - a0, ab) / Mathf.Max(1e-3f, ab.sqrMagnitude));
                    float d = (p - (a0 + ab * tt)).magnitude;
                    byte al = (byte)(Mathf.Clamp01(r + 1.5f - d) * 255f);
                    if (al > px[y * w + x].a) px[y * w + x] = new Color32(255, 255, 255, al);
                }
            }
            // 身體：頭、軀幹、裙擺（大人）／短裙（小孩）、腿。
            float headY = child ? 214 : 222, headR = child ? 17 : 15;
            Disc(64, headY, headR);
            Capsule(new Vector2(64, headY - 26), new Vector2(64, 120), child ? 17 : 19);
            Capsule(new Vector2(56, 120), new Vector2(50, 30), 9);
            Capsule(new Vector2(72, 120), new Vector2(78, 30), 9);
            if (!child)
                for (int i = 0; i < 6; i++) Capsule(new Vector2(64, 150), new Vector2(42 + i * 9, 72), 7);
            // 手：姿勢 0＝垂下；1＝抬起（大人：手比在對方頭頂量身高；小孩：張開手臂笑）。
            if (pose == 0)
            {
                Capsule(new Vector2(46, headY - 34), new Vector2(40, 120), 6);
                Capsule(new Vector2(82, headY - 34), new Vector2(88, 120), 6);
            }
            else if (!child)
            {
                Capsule(new Vector2(46, headY - 34), new Vector2(40, 120), 6);
                Capsule(new Vector2(82, headY - 34), new Vector2(122, headY - 40), 6);
            }
            else
            {
                Capsule(new Vector2(46, headY - 30), new Vector2(10, headY - 4), 6);
                Capsule(new Vector2(82, headY - 30), new Vector2(118, headY - 4), 6);
            }
            t.SetPixels32(px);
            t.Apply(true);
            textures[key] = t;
            return t;
        }

        private void OnDestroy()
        {
            if (motherMat != null) Object.DestroyImmediate(motherMat);
            if (girlMat != null) Object.DestroyImmediate(girlMat);
        }
    }
}
