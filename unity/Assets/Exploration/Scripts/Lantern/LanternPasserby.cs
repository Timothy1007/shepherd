using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 窗外走過的人影：一片和窗戶一樣大的面，貼圖裡有一個人的剪影，貼圖位移讓人影從一邊滑到另一邊（只在窗戶範圍內，不會畫到牆上）。
    /// 每隔幾秒走過一個，方向、速度、大人或小孩隨機。外面的世界照常在走，只有這間房子停住了。
    /// </summary>
    public sealed class LanternPasserby : MonoBehaviour
    {
        public float Speed = 0.32f;   // 每秒走過窗寬的比例
        private Material mat;
        private float pos = 0.1f, dir = 1f, wait;
        private System.Random rng = new System.Random(11);
        private static Texture2D adult, child;

        public static LanternPasserby Create(Transform parent, Vector3 center, Quaternion facing, Vector2 size)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "WindowPasserby";
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.SetPositionAndRotation(center, facing);
            q.transform.localScale = new Vector3(size.x, size.y, 1f);
            var p = q.AddComponent<LanternPasserby>();
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            p.mat = new Material(sh != null ? sh : Shader.Find("Sprites/Default")) { name = "Passerby", hideFlags = HideFlags.DontSave };
            p.mat.SetFloat("_Surface", 1f);
            p.mat.SetFloat("_Blend", 0f);
            p.mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            p.mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            p.mat.SetFloat("_ZWrite", 0f);
            p.mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            p.mat.renderQueue = 3020;
            var c = new Color(0.25f, 0.22f, 0.2f, 0.42f);
            p.mat.SetColor("_BaseColor", c);
            p.mat.SetColor("_Color", c);
            p.Pick(false);
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = p.mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            p.Apply();
            return p;
        }

        /// <summary>一張很寬的貼圖，中間站著一個人（上半身比較清楚，窗戶只看得到腰部以上）。</summary>
        private static Texture2D Strip(bool isChild)
        {
            const int w = 512, h = 128;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = isChild ? "PasserChild" : "PasserAdult", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var src = LanternEcho.Silhouette(isChild, 0);
            var px = new Color[w * h];
            // 剪影放在中間；窗戶看到的是人的上半段（頭到腰），所以取剪影的上 55%。
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x - (w / 2f - 40f)) / 80f, v = y / (float)h;
                    float a = 0f;
                    if (u >= 0f && u <= 1f)
                    {
                        float sv = isChild ? 0.25f + v * 0.62f : 0.42f + v * 0.58f;
                        a = src.GetPixelBilinear(u, Mathf.Clamp01(sv)).a;
                    }
                    px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        private void Pick(bool randomize)
        {
            bool kid = randomize && rng.NextDouble() < 0.25;
            if (adult == null) adult = Strip(false);
            if (child == null) child = Strip(true);
            var tex = kid ? child : adult;
            mat.SetTexture("_BaseMap", tex);
            mat.SetTexture("_MainTex", tex);
            if (randomize) { dir = rng.NextDouble() < 0.5 ? 1f : -1f; Speed = 0.22f + (float)rng.NextDouble() * 0.18f; }
        }

        private void Apply()
        {
            // 貼圖位移：pos 從 -0.6 走到 0.6（人影從窗外一邊進來、另一邊出去）。
            var off = new Vector4(1f, 1f, -pos, 0f);
            mat.SetVector("_BaseMap_ST", off);
            mat.SetTextureOffset("_MainTex", new Vector2(-pos, 0f));
        }

        public void Step(float dt)
        {
            if (wait > 0f) { wait -= dt; if (wait <= 0f) { Pick(true); pos = -0.6f * dir; } return; }
            pos += dir * Speed * dt;
            if (Mathf.Abs(pos) > 0.6f) { wait = 3f + (float)rng.NextDouble() * 6f; pos = 0.9f; }
            Apply();
        }

        private void Update() { if (Application.isPlaying) Step(Time.deltaTime); }

        private void OnDestroy() { if (mat != null) Object.DestroyImmediate(mat); }
    }
}
