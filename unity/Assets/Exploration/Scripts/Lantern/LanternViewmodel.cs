using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 正式遊戲的第一人稱左手提燈（Blender 模型，美術風格 QA 定稿版 ffd5f7c 搬過來）。
    /// 由 LanternRig 建立與驅動：平常手垂在畫面外提著燈；開關燈（展示）或聚光時手抬起來。
    ///   - 手：掌心朝下握住提把（握拳的空間照提把粗細算過，0 穿模）；手和提燈一起畫成 0.8 倍。
    ///   - 手臂：上臂＋前臂兩節骨頭 IK，手肘往外下方彎；舉燈時前臂從畫面左下角往前伸。
    ///   - 低頭抬頭只跟 60%；提燈像鐘擺一樣甩、手有走路起伏、呼吸與轉頭的慣性。
    /// 模型：Resources/Models/Custom/LanternViewmodel.prefab（由 StylizedArtQAEditor 產生）。找不到就回傳 null，LanternRig 用原本的灰盒。
    /// </summary>
    public sealed class LanternViewmodel
    {
        public const string PrefabPath = "Models/Custom/LanternViewmodel";
        public const float ViewScale = 0.8f, ArmWidth = 0.85f, PitchFollow = 0.6f;
        public const float UpperArmLength = 0.30f, ForearmLength = 0.29f, ForearmMeshLength = 0.27f, UpperArmMeshLength = 0.30f;
        /// <summary>手腕在手模型裡的位置（原點＝握把中心）。</summary>
        public static readonly Vector3 Wrist = new Vector3(0f, 0.026f, 0.100f);
        // 鏡頭座標（x 右、y 上、z 前；手的鏡頭和主鏡頭同為 62°）
        public static readonly Vector3 HandLowered = new Vector3(-0.24f, -0.34f, 0.46f);   // 平常：手在畫面下方外面
        public static readonly Vector3 HandRaised = new Vector3(-0.33f, -0.014f, 0.60f);   // 展示：拳頭在畫面左下約四分之一、整盞燈看得到
        public static readonly Vector3 HandFocus = new Vector3(-0.27f, 0.03f, 0.62f);      // 聚光：再往中間、往上一點
        public static readonly Vector3 ShoulderLow = new Vector3(-0.2f, -0.26f, 0.04f), PoleLow = new Vector3(-0.6f, -0.6f, 0f);
        public static readonly Vector3 ShoulderRaised = new Vector3(-0.45f, -0.30f, 0.18f), PoleRaised = new Vector3(-0.8f, -0.75f, 0.05f);

        public Transform Root { get; private set; }
        public Transform Arm { get; private set; }
        public Transform Forearm { get; private set; }
        public Transform UpperArm { get; private set; }
        public Transform LanternPivot { get; private set; }
        /// <summary>玻璃中心（LanternRig.Glass 指到這裡）。</summary>
        public Transform Glass { get; private set; }
        public Renderer LanternRenderer { get; private set; }
        public readonly List<Renderer> Renderers = new List<Renderer>();
        public Vector3 Elbow { get; private set; }

        private float lastYaw, lastPitch, swayPhase;
        private Vector2 swing, swingVel, lag, lagVel;
        private bool primed;
        private MaterialPropertyBlock mpb;

        public static LanternViewmodel TryBuild(Transform parent, int layer)
        {
            var prefab = Resources.Load<GameObject>(PrefabPath);
            if (prefab == null) return null;
            var inst = Object.Instantiate(prefab, parent, false);
            inst.name = "LanternViewmodel";
            var vm = new LanternViewmodel { Root = inst.transform };
            vm.Arm = inst.transform.Find("ArmLeft");
            vm.Forearm = inst.transform.Find("ForearmLeft");
            vm.UpperArm = inst.transform.Find("UpperArmLeft");
            vm.LanternPivot = inst.transform.Find("LanternPivot");
            if (vm.Arm == null || vm.LanternPivot == null) { ArenaArt.Kill(inst); return null; }
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
                if (t.name == "Glass") vm.Glass = t;
            }
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                vm.Renderers.Add(r);
                if (r.transform.IsChildOf(vm.LanternPivot) && vm.LanternRenderer == null) vm.LanternRenderer = r;
            }
            if (vm.Glass == null) vm.Glass = vm.LanternPivot;
            return vm;
        }

        /// <summary>
        /// raise：0＝垂在畫面外、1＝舉起來（LanternRig.LanternShown）；focus：聚光程度；glow：玻璃與火焰的亮度（0～1）。
        /// </summary>
        public void Tick(Transform cam, FirstPersonController player, float dt, float time, float raise, float focus, float glow)
        {
            if (Root == null || cam == null || player == null) return;
            if (dt <= 0f) dt = 1f / 60f;
            if (!primed) { lastYaw = player.Yaw; lastPitch = player.Pitch; primed = true; }
            float r = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(raise));
            float fb = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(focus));
            float speed = player.LastHorizontalSpeed;
            swayPhase += speed * dt / 0.7f * Mathf.PI;
            float walk = Mathf.Clamp01(speed / 1.2f);
            var bob = new Vector3(Mathf.Sin(swayPhase) * 0.006f, -Mathf.Abs(Mathf.Sin(swayPhase)) * 0.012f, 0f) * walk;
            var breath = new Vector3(Mathf.Sin(time * 0.7f) * 0.0015f, Mathf.Sin(time * 1.15f) * 0.003f, 0f) * (1f - walk);
            float dYaw = Mathf.DeltaAngle(lastYaw, player.Yaw), dPitch = player.Pitch - lastPitch;
            lastYaw = player.Yaw; lastPitch = player.Pitch;
            // 手的慣性：轉視角時稍微落後再跟上
            var lagTarget = Vector2.ClampMagnitude(new Vector2(-dYaw * 0.0016f, dPitch * 0.0016f), 0.04f);
            lagVel += (lagTarget - lag) * 90f * dt - lagVel * 13f * dt;
            lag += lagVel * dt;
            // 提燈像鐘擺：依轉動速度甩、走路前後晃、站著也有一點餘擺
            float yawRate = dYaw / dt, pitchRate = dPitch / dt;
            var push = new Vector2(pitchRate * 0.08f + Mathf.Sin(swayPhase) * 7f * walk + Mathf.Sin(time * 0.9f) * 1.2f * (1f - walk),
                                   -yawRate * 0.1f + Mathf.Cos(swayPhase * 0.5f) * 3f * walk);
            swingVel += (Vector2.ClampMagnitude(push, 24f) - swing) * 55f * dt - swingVel * 4.5f * dt;
            swing = Vector2.ClampMagnitude(swing + swingVel * dt, 30f);

            // 低頭抬頭只跟 60%
            Root.localPosition = Vector3.zero;
            Root.localRotation = Quaternion.Euler(-player.Pitch * (1f - PitchFollow), 0f, 0f);
            var rootRot = Root.rotation;
            var arc = new Vector3(-0.02f, 0f, 0.05f) * Mathf.Sin(r * Mathf.PI);
            var raisedPos = Vector3.Lerp(HandRaised, HandFocus, fb);
            Arm.localPosition = Vector3.Lerp(HandLowered, raisedPos, r) + arc + bob + breath + new Vector3(lag.x, lag.y, 0f);
            var level = rootRot * Quaternion.Euler(Mathf.Lerp(0f, -10f, r), Mathf.Lerp(0f, 12f, r), swing.y * 0.3f + Mathf.Lerp(0f, 4f, r));
            var handLevel = rootRot * Quaternion.Euler(0f, Mathf.Lerp(0f, 12f, r), swing.y * 0.3f + Mathf.Lerp(0f, 4f, r));
            Arm.rotation = level * Quaternion.Euler(0f, 180f, 0f);
            Arm.localScale = Vector3.one * ViewScale;
            LanternPivot.position = Arm.position;
            LanternPivot.rotation = handLevel * Quaternion.AngleAxis(swing.x, Vector3.right) * Quaternion.Euler(0f, 180f, 0f);
            LanternPivot.localScale = Vector3.one * ViewScale;
            SolveArm(r);

            if (LanternRenderer != null)
            {
                if (mpb == null) mpb = new MaterialPropertyBlock();
                LanternRenderer.GetPropertyBlock(mpb);
                mpb.SetFloat("_Intensity", Mathf.Lerp(0.18f, 1.5f, Mathf.Clamp01(glow)));
                LanternRenderer.SetPropertyBlock(mpb);
            }
        }

        private void SolveArm(float r)
        {
            var S = Root.TransformPoint(Vector3.Lerp(ShoulderLow, ShoulderRaised, r));
            var W = Arm.TransformPoint(Wrist);
            var P = Root.TransformPoint(Vector3.Lerp(PoleLow, PoleRaised, r));
            var E = SolveElbow(S, W, P, UpperArmLength, ForearmLength);
            Elbow = E;
            if (Forearm != null)
            {
                var fe = E - W;
                Forearm.position = W;
                Forearm.rotation = Quaternion.LookRotation(fe.normalized, Root.up);
                Forearm.localScale = new Vector3(ArmWidth, ArmWidth, fe.magnitude / ForearmMeshLength);
            }
            if (UpperArm != null)
            {
                var es = S - E;
                UpperArm.position = E;
                UpperArm.rotation = Quaternion.LookRotation(es.normalized, Root.up);
                UpperArm.localScale = new Vector3(ArmWidth, ArmWidth, es.magnitude / UpperArmMeshLength);
            }
        }

        /// <summary>兩節骨頭 IK：肩膀 S、手腕 W、手肘往 P 那一側彎。</summary>
        public static Vector3 SolveElbow(Vector3 S, Vector3 W, Vector3 P, float a, float b)
        {
            var sw = W - S;
            float d = Mathf.Clamp(sw.magnitude, Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
            var dir = sw.normalized;
            float along = (a * a - b * b + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            var toPole = P - S;
            var perp = (toPole - dir * Vector3.Dot(toPole, dir)).normalized;
            return S + dir * along + perp * h;
        }
    }
}
