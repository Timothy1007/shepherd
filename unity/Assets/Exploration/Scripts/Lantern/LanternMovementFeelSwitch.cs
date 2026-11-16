using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 第一人稱移動手感 QA（只存在於獨立場景 Assets/Exploration/Scenes/LanternMovementQA.unity）。
    /// F6：A → B → C 循環；0：舊版參數（對照用）；B：鏡頭晃動開／關（沿用提燈 QA 原本的按鍵）；按住 Alt 點按鈕；U 隱藏標籤。
    /// 2026-11-16 使用者試玩後採用 A 為正式參數（見 LanternPrototypeQA.WalkSpeed 等常數）；舊版＝1.9／3.0／加減速 7／時間式晃動。
    ///
    /// 漂浮感的原因（依原本的程式）：
    ///   1. 加速與減速是同一個值（7 m/s²）：放開方向鍵後還會滑 0.26～0.64 m 才停，像在冰上。
    ///   2. 整個速度向量用 MoveTowards 追目標：一邊走一邊轉頭時，速度方向落後鏡頭，有側滑感。
    ///   3. 鏡頭晃動的頻率跟時間走、不跟步伐走：1.9 m/s 時一次上下約 0.87 秒＝一步 1.65 m，步子大又慢，像飄著；
    ///      停下來時相位還在空轉；腳步聲每 0.8 m 一次，又跟晃動對不上。
    /// 這個 QA 的修正：加速約 0.45 秒到速、減速約 0.2 秒停住（慢速探索兩種都比較短）；只平滑速度大小、方向跟著鏡頭；
    /// 晃動改成「一步 0.7 m、上下約 ±0.8 cm、左右約 ±0.5 cm、沒有翻滾」，相位跟走過的距離走；腳步聲也改成每 0.7 m。
    /// </summary>
    public sealed class LanternMovementFeelSwitch : MonoBehaviour
    {
        public struct Preset
        {
            public string Name; public float Walk, Brisk, Accel, Decel; public bool Gait;
        }

        /// <summary>A／B／C（使用者給的起點值）與現行正式參數。加速＝步行速度／0.45 秒；減速＝max(步行／0.2 秒, 快走／0.3 秒)。</summary>
        public static readonly Preset[] Presets =
        {
            Make("A｜慢速探索", 1.5f, 2.4f),
            Make("B｜平衡探索", 1.9f, 3.0f),
            Make("C｜較快探索", 2.4f, 3.8f),
            new Preset { Name = "舊版參數（對照）", Walk = 1.9f, Brisk = 3.0f, Accel = 7f, Decel = 0f, Gait = false },
        };
        /// <summary>正式遊戲採用的預設（A）。</summary>
        public const int FormalIndex = 0;

        private static Preset Make(string n, float walk, float brisk) =>
            new Preset { Name = n, Walk = walk, Brisk = brisk, Accel = walk / 0.45f, Decel = Mathf.Max(walk / 0.2f, brisk / 0.3f), Gait = true };

        public LanternPrototypeQA Qa { get; private set; }
        public int Index { get; private set; } = FormalIndex;
        public Preset Current => Presets[Index];
        public bool LabelsVisible = true;
        private bool entered;
        private GUIStyle labelStyle, buttonStyle, hintStyle;

        // 量測：起步到 95% 速度的時間、放開後滑行的距離。
        public float LastStartTime { get; private set; } = -1f;
        public float LastStopDistance { get; private set; } = -1f;
        private bool wasMoving, measuringStart, measuringStop;
        private float startT, stopDist;
        private Vector3 lastPos;

        private void Start() => Bind(FindObjectOfType<LanternPrototypeQA>());

        public void Bind(LanternPrototypeQA qa)
        {
            Qa = qa;
            if (Qa == null) return;
            Qa.RenderStyleQA = true;     // F6 讓給這個 QA（平常是傳送到戰鬥場）
            Apply(Index);
        }

        private void OnDestroy()
        {
            if (Qa == null) return;
            Apply(FormalIndex);          // 還原成正式參數（A）
            Qa.RenderStyleQA = false; Qa.CursorUiOpen = false;
        }

        public void Apply(int i)
        {
            Index = Mathf.Clamp(i, 0, Presets.Length - 1);
            if (Qa == null || Qa.Player == null) return;
            var p = Current;
            Qa.WalkSpeedNow = p.Walk;
            Qa.BriskSpeedNow = p.Brisk;
            Qa.StepLength = p.Gait ? 0.7f : 0.8f;
            Qa.Player.Acceleration = p.Accel;
            Qa.Player.Deceleration = p.Decel;
            Qa.Player.SmoothSpeedOnly = p.Gait;
            if (Qa.Rig != null) Qa.Rig.GaitBob = p.Gait;
        }

        /// <summary>起點：走廊靠店面那端，面向北（往餐廳門、樓梯）。餐廳門與廚房門先打開，方便走固定路線。</summary>
        public void EnterAtStart()
        {
            if (Qa == null || Qa.World == null) return;
            var w = Qa.World;
            Qa.GoTo("V2Hall");
            var st = w.V2DiningStruggle;
            if (st != null && st.Door != null && !st.Door.IsOpen) st.Door.Toggle();
            foreach (var d in w.V2Doors) if (d != null && d.Label == "廚房的門" && !d.IsOpen) d.Toggle();
            Qa.Player.Teleport(LanternGraybox.V2Origin + new Vector3(5.07f * LanternGraybox.V2Scale, 0.05f, 5.5f * LanternGraybox.V2Scale), 0f, 0f);
            Qa.Rig.SetOn(false);
            lastPos = Qa.Player.transform.position;
        }

        private void Update()
        {
            if (!Application.isPlaying || Qa == null || !Qa.Ready) return;
            if (!entered) { entered = true; EnterAtStart(); }
            if (Input.GetKeyDown(KeyCode.F6)) Apply((Index >= 2 ? 0 : Index + 1));
            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) Apply(3);
            if (Input.GetKeyDown(KeyCode.U)) LabelsVisible = !LabelsVisible;
            Qa.CursorUiOpen = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            Measure(Time.deltaTime, Input.GetAxisRaw("Horizontal") != 0f || Input.GetAxisRaw("Vertical") != 0f);
        }

        private void LateUpdate()
        {
            if (Qa != null && Qa.Ready) Apply(Index);   // F1／R 重建後也維持目前預設
        }

        /// <summary>量起步時間與停止滑行距離（測試也會直接呼叫）。</summary>
        public void Measure(float dt, bool inputHeld)
        {
            if (Qa == null || Qa.Player == null || dt <= 0f) return;
            var pos = Qa.Player.transform.position;
            float moved = Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(lastPos.x, 0f, lastPos.z));
            lastPos = pos;
            float speed = Qa.Player.LastHorizontalSpeed;
            if (inputHeld && !wasMoving) { measuringStart = true; startT = 0f; measuringStop = false; }
            if (!inputHeld && wasMoving) { measuringStop = true; stopDist = 0f; measuringStart = false; }
            wasMoving = inputHeld;
            if (measuringStart)
            {
                startT += dt;
                if (speed >= Qa.Player.WalkSpeedValue * 0.95f) { LastStartTime = startT; measuringStart = false; }
                else if (startT > 3f) measuringStart = false;
            }
            if (measuringStop)
            {
                stopDist += moved;
                if (speed < 0.05f) { LastStopDistance = stopDist; measuringStop = false; }
            }
        }

        private void OnGUI()
        {
            if (Qa == null || !Qa.Ready) return;
            if (labelStyle == null)
            {
                var font = LanternFonts.UI;
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleRight, font = font };
                labelStyle.normal.textColor = new Color(1f, 0.97f, 0.9f);
                hintStyle = new GUIStyle(labelStyle) { fontSize = 13 };
                hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.78f);
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold, font = font };
            }
            float w = Screen.width;
            if (!LabelsVisible) { GUI.Label(new Rect(w - 260, 8, 250, 22), "U 顯示標籤", hintStyle); return; }
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(w - 480, 6, 474, 150), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var p = Current;
            GUI.Label(new Rect(w - 476, 10, 462, 24), "移動手感：" + p.Name, labelStyle);
            GUI.Label(new Rect(w - 476, 34, 462, 22), "步行 " + p.Walk.ToString("0.0") + " m/s・快走（Shift）" + p.Brisk.ToString("0.0") + " m/s・晃動 " + (Qa.Rig.HeadBob ? (p.Gait ? "步伐式" : "原本") : "關"), hintStyle);
            GUI.Label(new Rect(w - 476, 54, 462, 22), "目前速度 " + Qa.Player.LastHorizontalSpeed.ToString("0.00") + " m/s・起步 " + (LastStartTime < 0f ? "—" : LastStartTime.ToString("0.00") + " 秒") + "・停止滑行 " + (LastStopDistance < 0f ? "—" : LastStopDistance.ToString("0.00") + " m"), hintStyle);
            var prev = GUI.backgroundColor;
            string[] labels = { "A", "B", "C", "舊版" };
            for (int i = 0; i < 4; i++)
            {
                GUI.backgroundColor = i == Index ? new Color(1f, 0.75f, 0.4f) : prev;
                if (GUI.Button(new Rect(w - 330 + i * 62 - (i == 3 ? 0 : 0), 80, i == 3 ? 70 : 56, 30), labels[i], buttonStyle)) Apply(i);
            }
            GUI.backgroundColor = Qa.Rig.HeadBob ? new Color(0.7f, 0.85f, 1f) : prev;
            if (GUI.Button(new Rect(w - 476, 80, 130, 30), Qa.Rig.HeadBob ? "晃動：開" : "晃動：關", buttonStyle)) Qa.ToggleHeadBob();
            GUI.backgroundColor = prev;
            GUI.Label(new Rect(w - 476, 116, 462, 22), "F6 切換 A／B／C・0 舊版・B 晃動開關・按住 Alt 點按鈕・U 隱藏", hintStyle);
            GUI.Label(new Rect(w - 476, 134, 462, 20), "路線：走廊 → 餐廳門 → 餐桌旁 → 廚房門 → 廚房 → 回走廊 → 樓梯", hintStyle);
        }
    }
}
