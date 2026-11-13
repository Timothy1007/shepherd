using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>可以被穩定器牽引（浮起、搬動、蓄力丟出）的物件。</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class LanternTetherable : MonoBehaviour
    {
        public string Label = "物件";
        public Rigidbody Body { get; private set; }
        public bool Held { get; set; }
        /// <summary>被丟出後一小段時間內撞到練習靶會讓它暈眩。</summary>
        public float ThrownTimer { get; set; }

        /// <summary>原本的位置（掉出世界時回到這裡）。</summary>
        public Vector3 HomePosition;
        public Quaternion HomeRotation;

        public void Init(float mass)
        {
            HomePosition = transform.position;
            HomeRotation = transform.rotation;
            Body = GetComponent<Rigidbody>();
            Body.mass = mass;
            Body.drag = 0.2f;
            Body.angularDrag = 0.6f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        public void Awake()
        {
            if (Body == null) Body = GetComponent<Rigidbody>();
        }
    }

    /// <summary>
    /// 可以被復原的物件：異域裡倒下／錯位（BrokenPose），持續施放後像倒帶一樣回到原位（FixedPose）。
    /// 中途放開會慢慢倒回去；完成後永久恢復，附近變成穩定區。
    /// </summary>
    public sealed class LanternRestorable : MonoBehaviour
    {
        public string Label = "物件";
        public Vector3 BrokenPosition, FixedPosition;
        public Quaternion BrokenRotation, FixedRotation;
        public float Seconds = 1.6f;
        public float Progress { get; private set; }
        public bool Done { get; private set; }
        public float AnchorRadius = 1.5f;

        /// <summary>QA／測試用：直接完成。</summary>
        public void ForceDone()
        {
            Progress = 1f;
            Done = true;
            Apply();
        }

        public void Init(Vector3 brokenPos, Quaternion brokenRot, Vector3 fixedPos, Quaternion fixedRot)
        {
            BrokenPosition = brokenPos;
            BrokenRotation = brokenRot;
            FixedPosition = fixedPos;
            FixedRotation = fixedRot;
            Apply();
        }

        /// <summary>推進一個影格；casting＝正在對它施放。回傳是否剛好完成。</summary>
        public bool Step(float dt, bool casting)
        {
            if (Done) return false;
            Progress = Mathf.Clamp01(Progress + (casting ? dt / Seconds : -dt / (Seconds * 1.6f)));
            Apply();
            if (Progress >= 1f)
            {
                Done = true;
                LanternReveal.AddAnchor(FixedPosition + Vector3.up * 0.6f, AnchorRadius);
                return true;
            }
            return false;
        }

        private void Apply()
        {
            // 先慢後快、最後輕輕落定（倒帶感）。
            float k = Mathf.SmoothStep(0f, 1f, Progress);
            float lift = Mathf.Sin(Progress * Mathf.PI) * 0.12f;
            transform.position = Vector3.Lerp(BrokenPosition, FixedPosition, k) + Vector3.up * lift;
            transform.rotation = Quaternion.Slerp(BrokenRotation, FixedRotation, k);
        }
    }

    /// <summary>練習靶（簡化的失序體）：左右飄移；被穩定能量或丟出的物件打中會暈眩並出現干擾粒子。</summary>
    public sealed class LanternTarget : MonoBehaviour
    {
        public Vector3 A, B;
        public float Speed = 0.35f;
        public int Hits { get; private set; }
        public float StunTimer { get; private set; }
        public bool Stunned => StunTimer > 0f;
        public ParticleSystem Glitch;
        public Renderer[] Parts;
        private float phase;

        public void Hit(float strength)
        {
            Hits++;
            StunTimer = Mathf.Lerp(1.2f, 2.6f, Mathf.Clamp01(strength));
            if (Glitch != null) Glitch.Emit(Mathf.RoundToInt(Mathf.Lerp(18f, 50f, strength)));
        }

        public void Step(float dt, float time)
        {
            if (StunTimer > 0f)
            {
                StunTimer -= dt;
                // 暈眩：抖動、身上一塊塊閃過「正常」的樣子（由 shader 的撞擊泡泡負責），停止移動。
                transform.localRotation = Quaternion.Euler(Mathf.Sin(time * 70f) * 4f, phase * 40f, Mathf.Cos(time * 63f) * 4f);
                return;
            }
            phase += dt * Speed;
            float k = (Mathf.Sin(phase * Mathf.PI) + 1f) * 0.5f;
            transform.position = Vector3.Lerp(A, B, k) + Vector3.up * Mathf.Sin(time * 1.7f) * 0.06f;
            transform.localRotation = Quaternion.Euler(0f, phase * 40f, 0f);
        }
    }

    /// <summary>只存在於異域的牆：被照成真實（提燈靠近或穩定能量打中）時可以穿過，真實的門洞就在後面。</summary>
    public sealed class LanternAnomalyBlocker : MonoBehaviour
    {
        public Collider Solid;
        public Vector3 Probe;
        /// <summary>其他檢查點（例如門洞的另一側）：任何一點是真實，牆就打開——走到一半不會被關在中間。</summary>
        public Vector3[] ExtraProbes;

        public bool Open => Solid != null && !Solid.enabled;

        public void Refresh()
        {
            if (Solid == null) return;
            bool real = LanternReveal.IsReal(Probe);
            if (!real && ExtraProbes != null) foreach (var p in ExtraProbes) if (LanternReveal.IsReal(p)) { real = true; break; }
            Solid.enabled = !real;
        }
    }

    /// <summary>地上的凹痕：把對的物件放在上面就「放對了」。</summary>
    public sealed class LanternPlacementSpot : MonoBehaviour
    {
        public LanternTetherable Expected;
        public float Tolerance = 0.3f;
        public bool Solved { get; private set; }
        public Light Glow;
        /// <summary>玩家正抓著對的物件時，這裡會微微發光（提示該放哪）。</summary>
        public bool Highlight;
        public string Hint = "";

        public void UpdateHighlight(float time)
        {
            if (Glow == null || Solved) return;
            Glow.enabled = Highlight;
            if (Highlight) Glow.intensity = 0.5f + 0.35f * Mathf.Sin(time * 4f);
        }

        public bool Check()
        {
            if (Solved || Expected == null || Expected.Held) return false;
            var p = Expected.transform.position;
            var flat = new Vector2(p.x - transform.position.x, p.z - transform.position.z);
            bool upright = Vector3.Dot(Expected.transform.up, Vector3.up) > 0.85f;
            if (flat.magnitude < Tolerance && upright && Expected.Body.velocity.magnitude < 0.3f)
            {
                Solved = true;
                if (Glow != null) { Glow.enabled = true; Glow.intensity = 0.9f; }
                LanternReveal.AddAnchor(transform.position + Vector3.up * 0.6f, 1.3f);
                return true;
            }
            return false;
        }
    }

    /// <summary>不會召喚失序體的生活互動：開關抽屜、冰箱門、窗簾、電視、檯燈。E 切換。</summary>
    public sealed class LanternToggle : MonoBehaviour
    {
        public enum Kind { Slide, Rotate, Light }
        public Kind Type;
        public string Label = "東西";
        public string OpenVerb = "打開", CloseVerb = "關上";
        public Transform Moving;
        public Vector3 ClosedLocal, OpenLocal;
        public Quaternion ClosedRot = Quaternion.identity, OpenRot = Quaternion.identity;
        public Light[] Lights;
        public Renderer Screen;
        /// <summary>要有電才會亮（立燈）；Powered 由住宅每個影格更新。</summary>
        public bool RequiresPower;
        /// <summary>被推開一條縫的程度（推門拉鋸時使用，0～1 的開門比例）。</summary>
        public float Ajar;
        public bool Powered = true;
        public bool IsOpen { get; private set; }
        public int Toggles { get; private set; }
        private float k;

        public void Toggle()
        {
            IsOpen = !IsOpen;
            Toggles++;
            if (Lights != null) foreach (var l in Lights) if (l != null) l.enabled = IsOpen && (!RequiresPower || Powered);
        }

        public void Step(float dt, float time)
        {
            k = Mathf.MoveTowards(k, IsOpen ? 1f : 0f, dt * 3.5f);
            if (RequiresPower && Lights != null) foreach (var l in Lights) if (l != null) l.enabled = IsOpen && Powered;
            float e = Mathf.Max(Mathf.SmoothStep(0f, 1f, k), Ajar);
            if (Moving != null)
            {
                if (Type == Kind.Slide) Moving.localPosition = Vector3.Lerp(ClosedLocal, OpenLocal, e);
                else if (Type == Kind.Rotate) Moving.localRotation = Quaternion.Slerp(ClosedRot, OpenRot, e);
            }
            if (Screen != null && IsOpen && Lights != null)
                foreach (var l in Lights) if (l != null) l.intensity = 0.5f + Mathf.PerlinNoise(time * 9f, 0.3f) * 0.6f;
        }

        public string Prompt => RequiresPower && !Powered ? "E 按" + Label + "的開關（沒有電）" : "E " + (IsOpen ? CloseVerb : OpenVerb) + Label;
    }

    /// <summary>
    /// 可以拿到眼前細看的東西（共用近看系統）：文件可翻頁、照片可翻面、物件可旋轉。
    /// 關閉近看時才算「看完」（線索會在這時完成，異域反撲在放下之後才開始）。
    /// </summary>
    public sealed class LanternInspectable : MonoBehaviour
    {
        public enum Kind { Pages, Image, Object }
        public Kind Type;
        public string Title = "";
        [TextArea] public string[] Pages = new string[0];
        public Texture Front, Back;
        public string BackText = "";
        public bool Seen { get; set; }
        public int Opened { get; set; }
        public LanternReadable Completes;
        /// <summary>藏起來的記憶碎片（可選）：第一次看完時計入。</summary>
        public bool Secret;
    }

    /// <summary>
    /// 推門（拉鋸）：門的另一邊像有人抵著，門縫的光像呼吸一樣一鬆一緊。
    /// 在「鬆」的時候按住推，門會開一點；在「緊」的時候硬推會被推回來；狂點會讓門猛地關回去。
    /// 心門硬撬不開，要等對方鬆口的時刻。推開之後就是一扇普通的門（LanternToggle）。
    /// </summary>
    public sealed class LanternStruggle : MonoBehaviour
    {
        public string Label = "門";
        public LanternToggle Door;
        public Light GapLight;
        /// <summary>一次「呼吸」的秒數、其中「鬆」的比例。</summary>
        public float Period = 2.2f, LooseFraction = 0.38f;
        /// <summary>鬆的時候推的速度、緊的時候硬推被推回的速度、狂點的懲罰。</summary>
        public float Gain = 0.5f, PushBack = 0.34f, SlamPenalty = 0.3f;
        public float Progress { get; private set; }
        public bool Done { get; private set; }
        public bool Loose { get; private set; }
        /// <summary>呼吸週期中的位置（0～1）；小於 LooseFraction 的那段是「鬆」。畫面上的量表用它。</summary>
        public float Phase { get; private set; }
        public bool Holding { get; private set; }
        public int Slams { get; private set; }
        public float SlamFlash { get; private set; }
        private float clock;
        private readonly System.Collections.Generic.Queue<float> presses = new System.Collections.Generic.Queue<float>();

        /// <summary>每個影格：holding＝玩家正對著它按住 E；pressed＝這一格剛按下。回傳是否剛推開。</summary>
        public bool Step(float dt, bool holding, bool pressed)
        {
            clock += dt;
            Phase = Mathf.Repeat(clock / Period, 1f);
            Loose = Phase < LooseFraction;
            SlamFlash = Mathf.MoveTowards(SlamFlash, 0f, dt * 2f);
            Holding = holding && !Done;
            if (GapLight != null)
            {
                float breath = Loose ? 1f : 0.2f;
                GapLight.intensity = Mathf.Lerp(GapLight.intensity, Done ? 1.2f : breath * (0.5f + Progress * 1.5f), 1f - Mathf.Exp(-8f * dt));
            }
            if (Done) return false;
            if (pressed) presses.Enqueue(clock);
            while (presses.Count > 0 && clock - presses.Peek() > 1f) presses.Dequeue();
            if (presses.Count >= 4)
            {
                // 狂點：門被猛地推回來。
                Progress = Mathf.Max(0f, Progress - SlamPenalty);
                Slams++;
                SlamFlash = 1f;
                presses.Clear();
            }
            if (holding) Progress = Mathf.Clamp01(Progress + (Loose ? Gain : -PushBack) * dt);
            else Progress = Mathf.Max(0f, Progress - 0.04f * dt);
            if (Door != null) Door.Ajar = Progress * 0.25f + (holding && Loose ? 0.02f : 0f);
            if (Progress >= 1f)
            {
                Done = true;
                if (Door != null) { Door.Ajar = 0f; if (!Door.IsOpen) Door.Toggle(); }
                return true;
            }
            return false;
        }

        public string Prompt => SlamFlash > 0.3f ? "（門被猛地推了回來）" : Holding ? (Loose ? "推……" : "推不動——等指針回到亮的那段") : "指針在亮的那段時，按住 E 推" + Label;
    }

    /// <summary>可以「使用」的東西（謎題用）：對準時顯示提示，按 E 執行。例如縫紉機、撿起一捲紅線。</summary>
    public sealed class LanternUsable : MonoBehaviour
    {
        public string Label = "東西";
        public System.Func<string> PromptFn;
        public System.Action OnUse;
        public int Uses { get; private set; }
        public string Prompt => PromptFn != null ? PromptFn() : "E 使用" + Label;
        public void Use() { Uses++; OnUse?.Invoke(); }
    }
}