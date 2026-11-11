using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>全屋重製 QA 的畫面後製（曝光、電影式色調映射、分離色調、暗角、顆粒）。只掛在這個 QA 的鏡頭上。</summary>
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

        public bool Available { get { return Material != null; } }

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
        }
    }
}
