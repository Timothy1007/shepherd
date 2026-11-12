using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;
#if SHEPHERD_URP
using UnityEngine.Rendering.Universal;
#endif

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 第一人稱的手、提燈與身體（灰盒造型）：
    /// - 右手提著提燈，提燈會隨轉頭與走路自然擺動；燈裡的光同時是照明與「顯示真實」的光圈來源。
    /// - 手與提燈畫在獨立的疊加鏡頭（不會穿進牆裡）；身體畫在主鏡頭，低頭看得到。
    /// - 走路時鏡頭有輕微上下晃動（可關閉）。
    /// </summary>
    public sealed class LanternRig
    {
        public const int ViewLayer = 31;
        /// <summary>提燈光的基準半徑（m）。實際形狀是往前偏的蛋形：前方 ×2.1、側面 ×0.85、後方 ×0.7；聚光時前方 ×5.2、側面 ×0.3（越遠越寬）。</summary>
        public static readonly float[] Radii = { 1.3f, 1.6f, 2.0f };
        public static readonly Vector3 NormalShape = new Vector3(2.1f, 0.85f, 0.7f);
        public static readonly Vector3 FocusShape = new Vector3(5.2f, 0.3f, 0.3f);
        /// <summary>側面每公尺變寬的倍率：平常 0.05、聚光 0.13（5 m 外約 1.1 m 寬的錐形）。</summary>
        public const float NormalSpread = 0.05f, FocusSpread = 0.13f;
        /// <summary>聚光（舉高提燈）：0～1 平滑過渡。</summary>
        public float FocusBlend { get; private set; }
        public bool Focus;
        public Light Beam { get; private set; }
        private Transform lanternArm;

        public Camera Camera { get; private set; }
        public Camera ViewCamera { get; private set; }
        public Transform ViewRoot { get; private set; }
        public Transform Hand { get; private set; }
        public Transform LanternPivot { get; private set; }
        public Transform Glass { get; private set; }
        public Light Light { get; private set; }
        public Transform Body { get; private set; }
        public readonly List<Renderer> BodyRenderers = new List<Renderer>();
        public readonly List<Renderer> ViewRenderers = new List<Renderer>();
        public readonly List<Material> Materials = new List<Material>();

        public bool On { get; private set; } = true;
        public int RadiusIndex { get; private set; } = 1;
        public float CurrentRadius { get; private set; }
        public bool HeadBob = true;
        public float RevealForward = 0.2f;
        public Transform Muzzle { get; private set; }
        public Transform Device { get; private set; }
        private Material deviceGlow;
        private float recoil;
        public Vector3 RevealCenter { get; private set; }

        private FirstPersonController player;
        private Vector2 swing, swingVel;
        private float bobPhase, bobAmount;
        private float lastYaw, lastPitch;
        private Material flame, glassMat;

        public float TargetRadius => On ? Radii[RadiusIndex] : 0f;

        public static LanternRig Build(Camera cam, FirstPersonController player)
        {
            var r = new LanternRig { Camera = cam, player = player };
            r.BuildView();
            r.BuildBody();
            r.lastYaw = player.Yaw;
            r.lastPitch = player.Pitch;
            r.CurrentRadius = r.TargetRadius;
            return r;
        }

        private Material Lit(string name, Color c, float smooth, Color? emission = null, float metallic = 0f)
        {
            var m = ArenaArt.Mat(c, smooth, emission, metallic);
            m.name = name;
            Materials.Add(m);
            return m;
        }

        private Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3? euler, string name, List<Renderer> list, int layer)
        {
            var t = ArenaArt.Prim(type, parent, pos, scale, m, euler, name, false, false);
            t.gameObject.layer = layer;
            list.Add(t.GetComponent<Renderer>());
            return t;
        }

        private void BuildView()
        {
            var sleeve = Lit("LR_Sleeve", new Color(0.16f, 0.15f, 0.17f), 0.15f);
            var skin = Lit("LR_Skin", new Color(0.72f, 0.56f, 0.46f), 0.3f);
            var brass = Lit("LR_Brass", new Color(0.55f, 0.4f, 0.2f), 0.6f, null, 0.8f);
            var iron = Lit("LR_Iron", new Color(0.12f, 0.12f, 0.13f), 0.45f, null, 0.6f);
            glassMat = Lit("LR_Glass", new Color(0.95f, 0.75f, 0.45f), 0.9f, new Color(1.4f, 0.95f, 0.5f));
            flame = Lit("LR_Flame", new Color(1f, 0.85f, 0.6f), 0.2f, new Color(6f, 4.2f, 2.2f));

            ViewRoot = new GameObject("LanternViewModel").transform;
            ViewRoot.SetParent(Camera.transform, false);
            ViewRoot.gameObject.layer = ViewLayer;

            // 左前臂提燈：從畫面左下外側伸進來。
            var arm = new GameObject("LeftArm").transform;
            lanternArm = arm;
            arm.SetParent(ViewRoot, false);
            arm.localPosition = new Vector3(-0.25f, -0.1f, 0.6f);
            arm.localScale = Vector3.one * 0.85f;
            arm.localRotation = Quaternion.identity;
            Part(PrimitiveType.Capsule, arm, new Vector3(-0.07f, -0.09f, -0.2f), new Vector3(0.085f, 0.21f, 0.085f), sleeve, new Vector3(62f, 18f, 0f), "Forearm", ViewRenderers, ViewLayer);
            Hand = Part(PrimitiveType.Sphere, arm, new Vector3(0f, -0.01f, 0.02f), new Vector3(0.075f, 0.065f, 0.09f), skin, null, "Hand", ViewRenderers, ViewLayer);

            // 提燈：掛在手下方，以手為支點擺動。
            LanternPivot = new GameObject("LanternPivot").transform;
            LanternPivot.SetParent(arm, false);
            LanternPivot.localPosition = new Vector3(0f, -0.03f, 0.02f);
            var L = LanternPivot;
            Part(PrimitiveType.Cylinder, L, new Vector3(0f, -0.04f, 0f), new Vector3(0.008f, 0.04f, 0.008f), iron, null, "Handle", ViewRenderers, ViewLayer);
            Part(PrimitiveType.Cylinder, L, new Vector3(0f, -0.09f, 0f), new Vector3(0.11f, 0.012f, 0.11f), brass, null, "Cap", ViewRenderers, ViewLayer);
            Part(PrimitiveType.Sphere, L, new Vector3(0f, -0.085f, 0f), new Vector3(0.06f, 0.03f, 0.06f), brass, null, "Dome", ViewRenderers, ViewLayer);
            Glass = Part(PrimitiveType.Cylinder, L, new Vector3(0f, -0.17f, 0f), new Vector3(0.085f, 0.07f, 0.085f), glassMat, null, "Glass", ViewRenderers, ViewLayer);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                Part(PrimitiveType.Cylinder, L, new Vector3(Mathf.Cos(a) * 0.046f, -0.17f, Mathf.Sin(a) * 0.046f), new Vector3(0.008f, 0.075f, 0.008f), brass, null, "Bar", ViewRenderers, ViewLayer);
            }
            Part(PrimitiveType.Sphere, L, new Vector3(0f, -0.18f, 0f), new Vector3(0.03f, 0.045f, 0.03f), flame, null, "Light", ViewRenderers, ViewLayer);
            Part(PrimitiveType.Cylinder, L, new Vector3(0f, -0.245f, 0f), new Vector3(0.105f, 0.01f, 0.105f), brass, null, "Base", ViewRenderers, ViewLayer);

            var lg = new GameObject("LanternLight");
            lg.transform.SetParent(L, false);
            lg.transform.localPosition = new Vector3(0f, -0.17f, 0f);
            Light = lg.AddComponent<Light>();
            Light.type = LightType.Point;
            Light.color = new Color(1f, 0.86f, 0.66f);
            Light.intensity = 0.9f;
            Light.range = 4f;
            Light.shadows = LightShadows.Soft;
            Light.shadowNearPlane = 0.25f;

            // 往前照的光（平常較弱，聚光時變強、變窄、變遠）。
            var bg = new GameObject("LanternBeam");
            bg.transform.SetParent(Camera.transform, false);
            bg.transform.localPosition = new Vector3(-0.2f, -0.2f, 0.3f);
            Beam = bg.AddComponent<Light>();
            Beam.type = LightType.Spot;
            Beam.color = new Color(1f, 0.86f, 0.66f);
            Beam.shadows = LightShadows.Soft;
            Beam.shadowNearPlane = 0.2f;

            BuildDevice(sleeve, skin, brass, iron);

            // 手與提燈的補光（只照疊加層），讓手的輪廓在暗處也讀得出來。
            var fill = new GameObject("ViewModelFill").AddComponent<Light>();
            fill.transform.SetParent(Camera.transform, false);
            fill.transform.localPosition = new Vector3(-0.3f, 0.35f, 0.1f);
            fill.type = LightType.Point;
            fill.range = 2f;
            fill.intensity = 0.35f;
            fill.color = new Color(0.75f, 0.8f, 0.95f);
            fill.cullingMask = 1 << ViewLayer;
            fill.shadows = LightShadows.None;

            // 疊加鏡頭只畫手與提燈。
            Camera.cullingMask &= ~(1 << ViewLayer);
            var vc = new GameObject("ViewModelCamera");
            vc.transform.SetParent(Camera.transform, false);
            ViewCamera = vc.AddComponent<Camera>();
            ViewCamera.cullingMask = 1 << ViewLayer;
            ViewCamera.nearClipPlane = 0.01f;
            ViewCamera.farClipPlane = 3f;
            ViewCamera.fieldOfView = 58f;
            ViewCamera.clearFlags = CameraClearFlags.Depth;
            ViewCamera.depth = Camera.depth + 1;
#if SHEPHERD_URP
            if (ShepherdShading.Urp)
            {
                var data = ViewCamera.GetUniversalAdditionalCameraData();
                data.renderType = CameraRenderType.Overlay;
                data.renderShadows = false;
                var baseData = Camera.GetUniversalAdditionalCameraData();
                if (!baseData.cameraStack.Contains(ViewCamera)) baseData.cameraStack.Add(ViewCamera);
            }
#endif
        }

        /// <summary>右手：事務所科學家做的「穩定器」（灰盒造型）。射擊／牽引／復原共用一把，線圈顏色代表目前模式。</summary>
        private void BuildDevice(Material sleeve, Material skin, Material brass, Material iron)
        {
            deviceGlow = Lit("LR_DeviceGlow", new Color(1f, 0.95f, 0.85f), 0.4f, new Color(1.2f, 1f, 0.7f));
            var wood = Lit("LR_DeviceGrip", new Color(0.3f, 0.18f, 0.1f), 0.35f);
            var arm = new GameObject("RightArm").transform;
            arm.SetParent(ViewRoot, false);
            arm.localPosition = new Vector3(0.2f, -0.15f, 0.46f);
            arm.localScale = Vector3.one * 0.85f;
            Part(PrimitiveType.Capsule, arm, new Vector3(0.06f, -0.08f, -0.2f), new Vector3(0.085f, 0.21f, 0.085f), sleeve, new Vector3(68f, -14f, 0f), "ForearmR", ViewRenderers, ViewLayer);
            Part(PrimitiveType.Sphere, arm, new Vector3(0f, -0.02f, 0f), new Vector3(0.075f, 0.07f, 0.09f), skin, null, "HandR", ViewRenderers, ViewLayer);
            Device = new GameObject("Stabilizer").transform;
            Device.SetParent(arm, false);
            Device.localPosition = new Vector3(0f, 0.02f, 0.02f);
            Part(PrimitiveType.Cube, Device, new Vector3(0f, -0.04f, -0.01f), new Vector3(0.035f, 0.09f, 0.045f), wood, new Vector3(-12f, 0f, 0f), "Grip", ViewRenderers, ViewLayer);
            Part(PrimitiveType.Cube, Device, new Vector3(0f, 0.025f, 0.05f), new Vector3(0.05f, 0.05f, 0.17f), brass, null, "Body", ViewRenderers, ViewLayer);
            Part(PrimitiveType.Cylinder, Device, new Vector3(0f, 0.03f, 0.16f), new Vector3(0.026f, 0.05f, 0.026f), iron, new Vector3(90f, 0f, 0f), "Barrel", ViewRenderers, ViewLayer);
            for (int i = 0; i < 3; i++)
                Part(PrimitiveType.Cylinder, Device, new Vector3(0f, 0.03f, 0.1f + i * 0.035f), new Vector3(0.042f, 0.006f, 0.042f), deviceGlow, new Vector3(90f, 0f, 0f), "Coil", ViewRenderers, ViewLayer);
            Part(PrimitiveType.Sphere, Device, new Vector3(0f, 0.062f, 0.02f), new Vector3(0.03f, 0.03f, 0.03f), deviceGlow, null, "Core", ViewRenderers, ViewLayer);
            Muzzle = new GameObject("Muzzle").transform;
            Muzzle.SetParent(Device, false);
            Muzzle.localPosition = new Vector3(0f, 0.03f, 0.22f);
        }

        /// <summary>穩定器的模式顏色與蓄力亮度（0～1）；active＝正在牽引／復原。</summary>
        public void SetDevice(int mode, float charge, bool active)
        {
            if (deviceGlow == null) return;
            var c = mode == 0 ? new Color(1.3f, 1.05f, 0.7f) : mode == 1 ? new Color(0.6f, 0.95f, 1.4f) : new Color(0.7f, 1.4f, 0.8f);
            float k = 0.6f + charge * 2.4f + (active ? 1.2f : 0f);
            deviceGlow.SetColor("_EmissionColor", c * k);
        }

        public void Recoil() => recoil = 1f;

        private float shake;
        /// <summary>受傷時鏡頭震動（公尺）。</summary>
        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        private void BuildBody()
        {
            var coat = Lit("LR_Coat", new Color(0.13f, 0.12f, 0.14f), 0.15f);
            var trousers = Lit("LR_Trousers", new Color(0.09f, 0.09f, 0.1f), 0.15f);
            var shoe = Lit("LR_Shoe", new Color(0.05f, 0.04f, 0.04f), 0.5f);
            Body = new GameObject("FirstPersonBody").transform;
            Body.SetParent(player.transform, false);
            Part(PrimitiveType.Capsule, Body, new Vector3(0f, 1.08f, -0.1f), new Vector3(0.4f, 0.36f, 0.26f), coat, null, "Torso", BodyRenderers, 0);
            Part(PrimitiveType.Capsule, Body, new Vector3(-0.1f, 0.45f, -0.04f), new Vector3(0.15f, 0.42f, 0.15f), trousers, null, "LegL", BodyRenderers, 0);
            Part(PrimitiveType.Capsule, Body, new Vector3(0.1f, 0.45f, -0.04f), new Vector3(0.15f, 0.42f, 0.15f), trousers, null, "LegR", BodyRenderers, 0);
            Part(PrimitiveType.Cube, Body, new Vector3(-0.1f, 0.04f, 0.04f), new Vector3(0.1f, 0.08f, 0.26f), shoe, null, "ShoeL", BodyRenderers, 0);
            Part(PrimitiveType.Cube, Body, new Vector3(0.1f, 0.04f, 0.04f), new Vector3(0.1f, 0.08f, 0.26f), shoe, null, "ShoeR", BodyRenderers, 0);
        }

        public void Toggle() => On = !On;
        public void SetOn(bool on) => On = on;
        public void CycleRadius() => RadiusIndex = (RadiusIndex + 1) % Radii.Length;
        public void SetRadius(int i) => RadiusIndex = Mathf.Clamp(i, 0, Radii.Length - 1);

        /// <summary>每個影格：鏡頭跟隨眼睛、晃動、提燈擺動、更新光圈。</summary>
        public void Tick(float dt, float time)
        {
            if (dt <= 0f) dt = 1f / 60f;
            float speed = player.LastHorizontalSpeed;
            float moving = Mathf.Clamp01(speed / 1.5f);
            bobAmount = Mathf.MoveTowards(bobAmount, HeadBob ? moving : 0f, dt * 3f);
            bobPhase += dt * Mathf.Lerp(4f, 9f, Mathf.Clamp01(speed / 3f));
            var bob = new Vector3(Mathf.Cos(bobPhase * 0.5f) * 0.012f, Mathf.Abs(Mathf.Sin(bobPhase * 0.5f)) * 0.022f - 0.011f, 0f) * bobAmount;
            shake = Mathf.MoveTowards(shake, 0f, dt * 0.35f);
            var jolt = shake > 0f ? new Vector3(Mathf.Sin(time * 61f), Mathf.Sin(time * 47f + 1.3f), 0f) * shake : Vector3.zero;
            Camera.transform.position = player.EyePosition + player.ViewRotation * (bob + jolt);
            Camera.transform.rotation = player.ViewRotation;
            if (ViewCamera != null) ViewCamera.fieldOfView = 58f;

            // 提燈擺動：轉頭時往反方向甩、走路時前後晃；阻尼彈簧。
            float dYaw = Mathf.DeltaAngle(lastYaw, player.Yaw), dPitch = player.Pitch - lastPitch;
            lastYaw = player.Yaw;
            lastPitch = player.Pitch;
            var push = new Vector2(dPitch * 0.9f + Mathf.Sin(bobPhase) * 2.2f * moving, -dYaw * 1.2f);
            var target = Vector2.ClampMagnitude(push, 22f);
            swingVel += (target - swing) * 60f * dt - swingVel * 9f * dt;
            swing += swingVel * dt;
            swing = Vector2.ClampMagnitude(swing, 28f);
            LanternPivot.localRotation = Quaternion.Euler(swing.x, 0f, swing.y);
            ViewRoot.localPosition = new Vector3(0f, -bob.y * 0.6f, 0f);
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 6f);
            if (Device != null) Device.localRotation = Quaternion.Euler(-recoil * 14f, 0f, 0f);

            // 聚光：把燈舉高、往前；光變成細長光束。
            FocusBlend = Mathf.MoveTowards(FocusBlend, Focus && On ? 1f : 0f, dt * 4f);
            float fb = Mathf.SmoothStep(0f, 1f, FocusBlend);
            if (lanternArm != null) lanternArm.localPosition = Vector3.Lerp(new Vector3(-0.25f, -0.1f, 0.6f), new Vector3(-0.21f, -0.04f, 0.64f), fb);

            // 光：開關時光圈平滑展開／收合；燈光輕微呼吸（不是火焰的劇烈跳動）。
            CurrentRadius = Mathf.MoveTowards(CurrentRadius, TargetRadius, dt * 9f);
            float breathe = 1f + Mathf.Sin(time * 2.3f) * 0.03f + (Mathf.PerlinNoise(time * 1.7f, 0.3f) - 0.5f) * 0.04f;
            float k = CurrentRadius / Radii[RadiusIndex];
            Light.enabled = CurrentRadius > 0.01f;
            Light.intensity = 0.9f * breathe * Mathf.Clamp01(k) * Mathf.Lerp(1f, 0.35f, fb);
            Light.range = Mathf.Max(0.5f, CurrentRadius * 2f);
            Beam.enabled = Light.enabled;
            Beam.intensity = Mathf.Lerp(0.7f, 2.6f, fb) * breathe * Mathf.Clamp01(k);
            Beam.range = CurrentRadius * Mathf.Lerp(NormalShape.x, FocusShape.x, fb) * 1.25f;
            Beam.spotAngle = Mathf.Lerp(95f, 30f, fb);
            Beam.innerSpotAngle = Beam.spotAngle * 0.45f;
            flame.SetColor("_EmissionColor", new Color(6f, 4.2f, 2.2f) * Mathf.Lerp(0.05f, 1f, Mathf.Clamp01(k)) * breathe);
            glassMat.SetColor("_EmissionColor", new Color(1.4f, 0.95f, 0.5f) * Mathf.Lerp(0.05f, 1f, Mathf.Clamp01(k)));

            RevealCenter = Light.transform.position + Camera.transform.forward * RevealForward;
            LanternReveal.SetSphere(RevealCenter, CurrentRadius);
            LanternReveal.SetShape(Camera.transform.forward, Vector3.Lerp(NormalShape, FocusShape, fb), Mathf.Lerp(NormalSpread, FocusSpread, fb));
        }

        /// <summary>立即套用目前開關與光圈（測試、截圖用）。</summary>
        public void Snap()
        {
            CurrentRadius = TargetRadius;
            Tick(0f, 0f);
        }

        public void Destroy()
        {
            if (ViewRoot != null) ArenaArt.Kill(ViewRoot.gameObject);
            if (ViewCamera != null) ArenaArt.Kill(ViewCamera.gameObject);
            if (Body != null) ArenaArt.Kill(Body.gameObject);
            foreach (var m in Materials) ArenaArt.Kill(m);
            Materials.Clear();
            LanternReveal.Clear();
        }
    }
}
