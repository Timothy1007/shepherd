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

        public Vector3 EyePosition { get { return transform.position + Vector3.up * EyeHeight; } }

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
            transform.rotation = Quaternion.Euler(0f, Yaw, 0f);
        }

        /// <summary>依輸入推進一個影格：滑鼠轉視角（applyLook＝滑鼠已鎖定）、WASD 移動、Shift 奔跑、重力。</summary>
        public void Step(float dt, ExploreInput input, bool applyLook = true)
        {
            if (dt <= 0f) return;
            if (applyLook)
            {
                Yaw = Mathf.Repeat(Yaw + input.Look.x * LookSensitivity, 360f);
                Pitch = Mathf.Clamp(Pitch - input.Look.y * LookSensitivity, -PitchLimit, PitchLimit);
            }
            transform.rotation = Quaternion.Euler(0f, Yaw, 0f);

            var move = Vector2.ClampMagnitude(input.Move, 1f);
            var dir = transform.right * move.x + transform.forward * move.y;
            float speed = input.Run ? RunSpeed : WalkSpeed;
            var horizontal = dir * speed;

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
