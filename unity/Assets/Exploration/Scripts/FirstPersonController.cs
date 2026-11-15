using UnityEngine;

namespace Shepherd.Exploration
{
    /// <summary>
    /// 第一人稱角色：CharacterController 負責碰撞（不穿牆、可上小階梯），自行套用重力。
    /// 不做跳躍、攀爬與動畫。輸入由 <see cref="Step"/> 傳入，方便測試與暫停（戰鬥時不呼叫）。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        public const float Height = 1.8f;
        public const float Radius = 0.35f;
        public const float EyeHeight = 1.65f;
        public const float WalkSpeed = 3.5f;
        public const float RunSpeed = 6.5f;
        public const float Gravity = 20f;
        public const float LookSensitivity = 2.2f;
        public const float PitchLimit = 80f;

        private CharacterController cc;
        private float verticalSpeed;
        private Vector3 horizontalVelocity;

        // Phase 1.7：第一人稱體感 QA 可即時調整的值（預設＝上面的正式常數，正式 Demo 不改）。
        /// <summary>步行速度（m/s）。</summary>
        public float WalkSpeedValue = WalkSpeed;
        /// <summary>奔跑速度（m/s）。</summary>
        public float RunSpeedValue = RunSpeed;
        /// <summary>眼高（m）；碰撞身高與半徑不變（不縮小玩家）。</summary>
        public float EyeHeightValue = EyeHeight;
        /// <summary>滑鼠靈敏度倍率。</summary>
        public float LookScale = 1f;
        /// <summary>加減速（m/s²）；0＝關閉（立即到速，正式行為）。</summary>
        public float Acceleration;
        /// <summary>
        /// 減速（m/s²）：放開方向鍵、或往反方向走時使用。0＝和 Acceleration 相同（原本的行為）。
        /// 只在移動手感 QA 裡設定；其他場景維持 0。
        /// </summary>
        public float Deceleration;
        /// <summary>
        /// true＝只平滑「速度大小」，方向直接跟著輸入與鏡頭（轉頭時不會側滑）；往反方向時先減速到接近 0 再換方向。
        /// false＝原本的做法（整個速度向量用 MoveTowards 追，轉頭時速度方向會落後、有側滑感）。只在移動手感 QA 裡打開。
        /// </summary>
        public bool SmoothSpeedOnly;
        private float speedNow;
        private Vector3 dirNow = Vector3.forward;

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public bool Grounded { get; private set; }
        /// <summary>最近一次 Step 的水平速度（m/s）。</summary>
        public float LastHorizontalSpeed { get; private set; }

        public CharacterController Body
        {
            get
            {
                if (cc == null) cc = GetComponent<CharacterController>();
                return cc;
            }
        }

        public Vector3 EyePosition { get { return transform.position + Vector3.up * EyeHeightValue; } }

        public Quaternion ViewRotation { get { return Quaternion.Euler(Pitch, Yaw, 0f); } }

        public static FirstPersonController Create(Transform parent, Vector3 position, float yaw)
        {
            var go = new GameObject("FirstPersonPlayer");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<CharacterController>();
            c.height = Height;
            c.radius = Radius;
            c.center = new Vector3(0f, Height * 0.5f, 0f);
            c.stepOffset = 0.35f;
            c.slopeLimit = 50f;
            c.skinWidth = 0.04f;
            c.minMoveDistance = 0f;
            var fp = go.AddComponent<FirstPersonController>();
            fp.cc = c;
            fp.Teleport(position, yaw);
            return fp;
        }

        /// <summary>移動到指定位置與朝向（停用 CharacterController 期間設定位置，避免被碰撞拉回）。</summary>
        public void Teleport(Vector3 position, float yaw, float pitch = 0f)
        {
            bool was = Body.enabled;
            Body.enabled = false;
            transform.position = position;
            Body.enabled = was;
            Physics.SyncTransforms();
            Yaw = yaw;
            Pitch = pitch;
            verticalSpeed = 0f;
            horizontalVelocity = Vector3.zero;
            speedNow = 0f;
            transform.rotation = Quaternion.Euler(0f, Yaw, 0f);
        }

        /// <summary>依輸入推進一個影格：滑鼠轉視角（applyLook＝滑鼠已鎖定）、WASD 移動、Shift 奔跑、重力。</summary>
        public void Step(float dt, ExploreInput input, bool applyLook = true)
        {
            if (dt <= 0f) return;
            if (applyLook)
            {
                Yaw = Mathf.Repeat(Yaw + input.Look.x * LookSensitivity * LookScale, 360f);
                Pitch = Mathf.Clamp(Pitch - input.Look.y * LookSensitivity * LookScale, -PitchLimit, PitchLimit);
            }
            transform.rotation = Quaternion.Euler(0f, Yaw, 0f);

            var move = Vector2.ClampMagnitude(input.Move, 1f);
            var dir = transform.right * move.x + transform.forward * move.y;
            float speed = input.Run ? RunSpeedValue : WalkSpeedValue;
            var horizontal = dir * speed;
            if (SmoothSpeedOnly && Acceleration > 0f)
            {
                float target = dir.magnitude * speed;
                bool reversing = target > 0.01f && speedNow > 0.2f && Vector3.Dot(dir.normalized, dirNow) < -0.2f;
                if (reversing) target = 0f;                                  // 先煞住，再往新方向走
                float rate = target < speedNow ? (Deceleration > 0f ? Deceleration : Acceleration) : Acceleration;
                speedNow = Mathf.MoveTowards(speedNow, target, rate * dt);
                if (dir.sqrMagnitude > 1e-4f && !reversing) dirNow = dir.normalized;
                horizontal = dirNow * speedNow;
            }
            else if (Acceleration > 0f)
            {
                float rate = Acceleration;
                if (Deceleration > 0f && (horizontal.sqrMagnitude < horizontalVelocity.sqrMagnitude - 1e-4f || Vector3.Dot(horizontal, horizontalVelocity) < 0f)) rate = Deceleration;
                horizontal = Vector3.MoveTowards(horizontalVelocity, horizontal, rate * dt);
                speedNow = horizontal.magnitude;
                if (horizontal.sqrMagnitude > 1e-4f) dirNow = horizontal.normalized;
            }
            else { speedNow = horizontal.magnitude; if (horizontal.sqrMagnitude > 1e-4f) dirNow = horizontal.normalized; }
            horizontalVelocity = horizontal;

            if (Body.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed -= Gravity * dt;
            var before = transform.position;
            var flags = Body.Move((horizontal + Vector3.up * verticalSpeed) * dt);
            Grounded = (flags & CollisionFlags.Below) != 0;
            if (Grounded && verticalSpeed < 0f) verticalSpeed = -2f;
            var delta = transform.position - before;
            delta.y = 0f;
            LastHorizontalSpeed = delta.magnitude / dt;
        }
    }
}
