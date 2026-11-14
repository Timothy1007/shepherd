using System.Collections.Generic;
using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 記憶中的東西（能力原型，見 docs/design/CORE_LOOP_AND_ABILITIES_V1.md 第 4 節）：
    /// 「照」——只存在於記憶中的東西，被提燈照到時看得到、踩得到、會擋路；光移開約 0.4 秒後消失。
    /// 「定」——被定住的記憶在沒有光的時候也維持住（看得到、踩得到），邊緣的光隨剩餘時間變弱。
    /// 有偏差的記憶（Biased）：在光裡會晃動、時有時無，而且定不住。
    /// </summary>
    public sealed class LanternMemory : MonoBehaviour
    {
        public string Label = "記憶中的東西";
        public bool Biased;
        public Collider Solid;
        public Vector3[] Probes = { Vector3.zero };
        public float HoldTime { get; private set; }
        public float HoldTotal { get; private set; } = 1f;
        public bool Lit { get; private set; }
        public bool Held => HoldTime > 0f;
        /// <summary>現在踩得到、擋得住。</summary>
        public bool Present => Solid != null && Solid.enabled;
        public const float Grace = 0.4f;

        private readonly List<Material> mats = new List<Material>();
        private float grace, seed, mode = -1f;
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int EdgeId = Shader.PropertyToID("_EmissionColor");

        /// <summary>把底下所有網格換成自己的材質副本（才能單獨切換「定住時沒有光也顯示」）。</summary>
        public void Init(Collider solid)
        {
            Solid = solid;
            seed = (transform.position.x * 13.1f + transform.position.z * 7.7f) % 100f;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var arr = r.sharedMaterials;
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i] == null) continue;
                    arr[i] = new Material(arr[i]) { name = arr[i].name + "_Mem", hideFlags = HideFlags.DontSave };
                    mats.Add(arr[i]);
                }
                r.sharedMaterials = arr;
            }
            if (Solid != null) Solid.enabled = false;
            Apply(false);
        }

        public void Step(float dt, float time)
        {
            bool lit = false;
            foreach (var p in Probes) if (LanternReveal.IsReal(transform.TransformPoint(p))) { lit = true; break; }
            // 有偏差的記憶：照到了也時有時無（大約三成的時間會掉）。
            if (Biased && lit) lit = Mathf.PerlinNoise(time * 1.6f, seed) > 0.34f;
            Lit = lit;
            if (HoldTime > 0f) HoldTime = Mathf.Max(0f, HoldTime - dt);
            bool want = lit || HoldTime > 0f;
            grace = want ? Grace : Mathf.Max(0f, grace - dt);
            if (Solid != null) Solid.enabled = grace > 0f;
            Apply(HoldTime > 0f);
        }

        private void Apply(bool held)
        {
            float m = held ? (float)LanternReveal.Mode.AlwaysReal : (float)LanternReveal.Mode.RealOnly;
            if (m != mode) { foreach (var mat in mats) mat.SetFloat(ModeId, m); mode = m; }
            // 定住時邊緣有一圈淡淡的暖光，越接近結束越弱（倒數）。
            var glow = held ? new Color(0.9f, 0.7f, 0.35f) * (0.15f + 0.45f * (HoldTime / Mathf.Max(0.01f, HoldTotal))) : Color.black;
            foreach (var mat in mats) mat.SetColor(EdgeId, glow);
        }

        /// <summary>試著定住。有偏差的記憶定不住（回傳 false）。</summary>
        public bool TryHold(float seconds)
        {
            if (Biased) return false;
            HoldTime = HoldTotal = seconds;
            grace = Grace;
            if (Solid != null) Solid.enabled = true;
            Apply(true);
            return true;
        }

        public void Release() { HoldTime = 0f; Apply(false); }

        private void OnDestroy() { foreach (var m in mats) if (m != null) Object.DestroyImmediate(m); }
    }
}
