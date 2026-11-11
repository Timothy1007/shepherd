using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// 手持手電筒：聚光燈掛在第一人稱鏡頭上（光束方向＝視線），帶光斑貼圖（中心亮、外圈柔邊、反光碗的淡環），
    /// 線性衰減到約 9 m；柔和陰影（只有這一盞投影，偏移調過避免閃爍）。
    /// 近距離照紙張或牆面時自動收光（射線量到表面距離，1.1 m 內逐漸降到 35%），不會把相片、文件照到過曝。
    /// 第一人稱手上有手電筒模型，開燈時鏡片微亮；開關有聲音。強度三段（QA 比較）。
    /// </summary>
    public sealed class Case01Flashlight
    {
        public static readonly float[] Intensities = { 1.3f, 2.1f, 3.0f };
        public static readonly string[] IntensityNames = { "弱", "中", "強" };

        public bool On { get; private set; }
        public int Level { get; private set; } = 1;
        public Light Light { get; private set; }
        public Transform Model { get; private set; }
        public float NearFactor { get; private set; } = 1f;
        public float LastHitDistance { get; private set; } = -1f;

        private readonly Material lens;
        private float shown;
        private AudioSource audio;

        public Case01Flashlight(Camera cam, Case01ArtKit kit, Case01Palette p, Material lensMat)
        {
            var go = new GameObject("FlashlightBeam");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0.16f, -0.16f, 0.25f);
            go.transform.localRotation = Quaternion.identity;
            Light = go.AddComponent<Light>();
            Light.type = LightType.Spot;
            Light.spotAngle = 54f;
            Light.innerSpotAngle = 20f;
            Light.range = 9f;
            Light.color = new Color(1f, 0.93f, 0.82f);
            Light.shadows = LightShadows.Soft;
            Light.shadowStrength = 0.85f;
            Light.shadowBias = 0.04f;
            Light.shadowNormalBias = 0.35f;
            Light.shadowNearPlane = 0.15f;
            Light.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Medium;
            Light.cookie = Resources.Load<Texture2D>(Case01ImmersiveHouse.Folder + "IH_FlashlightCookie");
            Light.cullingMask = ~(1 << Case01Inspector.Layer);
            Light.enabled = false;
            lens = lensMat;
            var prefab = Resources.Load<GameObject>(Case01ImmersiveHouse.Folder + "Flashlight");
            if (prefab != null)
            {
                var m = Object.Instantiate(prefab).transform;
                m.name = "FlashlightModel";
                m.SetParent(cam.transform, false);
                m.localPosition = new Vector3(0.2f, -0.2f, 0.34f);
                m.localRotation = Quaternion.Euler(4f, 174f, 0f);
                if (kit != null) kit.Remap(m.gameObject);
                Case01ImmersiveHouse.RemapIh(m.gameObject, p, lensMat);
                foreach (var r in m.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                foreach (var t in m.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = cam.gameObject.layer;
                Model = m;
                Model.gameObject.SetActive(false);
            }
            audio = cam.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
        }

        public void Toggle(bool sound = true)
        {
            On = !On;
            if (sound && audio != null && audio.isActiveAndEnabled)
            {
                var c = Case01Sfx.Get("switch");
                if (c != null) audio.PlayOneShot(c, 0.6f);
            }
        }

        public void SetOn(bool on)
        {
            if (On != on) Toggle(false);
        }

        public void CycleLevel()
        {
            Level = (Level + 1) % Intensities.Length;
        }

        public void SetLevel(int l) { Level = Mathf.Clamp(l, 0, Intensities.Length - 1); }

        /// <summary>每影格：拿出／收起手電筒（0.2 秒）、近距離收光、鏡片亮度。hidden：檢視物件中（暫時收起）。</summary>
        public void Tick(float dt, bool hidden)
        {
            bool want = On && !hidden;
            shown = Mathf.MoveTowards(shown, want ? 1f : 0f, dt / 0.2f);
            if (Model != null)
            {
                Model.gameObject.SetActive(shown > 0.01f);
                Model.localPosition = new Vector3(0.2f, Mathf.Lerp(-0.42f, -0.2f, shown), 0.34f);
            }
            Light.enabled = shown > 0.6f;
            if (!Light.enabled) return;
            RaycastHit h;
            float target = 1f;
            LastHitDistance = -1f;
            if (Physics.Raycast(Light.transform.position, Light.transform.forward, out h, 3f, Case01SpatialMap.SightMask, QueryTriggerInteraction.Ignore))
            {
                LastHitDistance = h.distance;
                target = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01((h.distance - 0.25f) / 0.85f));
            }
            NearFactor = Mathf.MoveTowards(NearFactor, target, dt * 2.5f);
            if (dt <= 0f) NearFactor = target;
            Light.intensity = Intensities[Level] * NearFactor;
            if (lens != null) ArenaArt.SetEmission(lens, new Color(1f, 0.9f, 0.7f) * (0.6f + Level * 0.3f));
        }

        public void Dispose()
        {
            if (Light != null) ArenaArt.Kill(Light.gameObject);
            if (Model != null) ArenaArt.Kill(Model.gameObject);
            if (audio != null) ArenaArt.Kill(audio);
        }
    }
}
