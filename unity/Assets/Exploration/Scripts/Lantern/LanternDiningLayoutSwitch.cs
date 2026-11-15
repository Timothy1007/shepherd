using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 餐廳「空間層次與探索構圖」QA（docs/level/CASE01_DINING_SPATIAL_DEPTH_QA_PLAN.md）。
    /// 只存在於獨立場景 Assets/Exploration/Scenes/LanternDiningLayoutQA.unity。
    /// F6：A（現況）⇄ B（構圖改造）；G：B 的光照引導開／關；按住 Alt 可以用滑鼠點右上角按鈕；U 隱藏標籤。
    /// 切換只移動物件、開關 B 專屬物件與兩盞引導燈：不重載場景、不動玩家位置、FOV、環境光、畫質與提燈。
    /// </summary>
    public sealed class LanternDiningLayoutSwitch : MonoBehaviour
    {
        public LanternPrototypeQA Qa { get; private set; }
        public bool WantB { get; private set; }
        public bool WantFocus { get; private set; } = true;
        public bool LabelsVisible = true;
        private bool entered;
        private GUIStyle labelStyle, buttonStyle, hintStyle;

        private void Start() => Bind(FindObjectOfType<LanternPrototypeQA>());

        public void Bind(LanternPrototypeQA qa)
        {
            Qa = qa;
            if (Qa == null) return;
            Qa.RenderStyleQA = true;      // F6 讓給這個 QA（平常是傳送到戰鬥場）
            ApplyNow();
        }

        private void OnDestroy()
        {
            if (Qa != null && Qa.World != null) { Qa.World.SetDiningLayout(false); Qa.World.SetDiningFocusLights(true); }
            if (Qa != null) { Qa.RenderStyleQA = false; Qa.CursorUiOpen = false; }
        }

        /// <summary>入口：站在走廊門內側、面向西（和 A／B 截圖的「入口第一眼」同一個機位）。</summary>
        public void EnterAtDoor()
        {
            if (Qa == null || Qa.World == null) return;
            Qa.GoTo("V2Dining");
            var st = Qa.World.V2DiningStruggle;
            if (st != null && st.Door != null && !st.Door.IsOpen) st.Door.Toggle();
            Qa.Player.Teleport(EntrancePoint, -90f, 0f);
            Qa.Rig.SetOn(false);
        }

        public static Vector3 EntrancePoint => LanternGraybox.V2Origin + new Vector3(0.125f + 5.3f, 0.05f, 6.56f + 1.83f);

        public void SetB(bool b) { WantB = b; ApplyNow(); }
        public void SetFocus(bool on) { WantFocus = on; ApplyNow(); }

        private void ApplyNow()
        {
            if (Qa == null || Qa.World == null) return;
            if (Qa.World.DiningFocusLightsOn != WantFocus) Qa.World.SetDiningFocusLights(WantFocus);
            if (Qa.World.DiningLayoutIsB != WantB) Qa.World.SetDiningLayout(WantB);
        }

        private void Update()
        {
            if (!Application.isPlaying || Qa == null || !Qa.Ready) return;
            if (!entered) { entered = true; EnterAtDoor(); }
            if (Input.GetKeyDown(KeyCode.F6)) SetB(!WantB);
            if (Input.GetKeyDown(KeyCode.G)) SetFocus(!WantFocus);
            if (Input.GetKeyDown(KeyCode.U)) LabelsVisible = !LabelsVisible;
            Qa.CursorUiOpen = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }

        private void LateUpdate()
        {
            // F1／R 會重建房子（回到 A）：每格確認狀態一致。
            if (Qa != null && Qa.Ready) ApplyNow();
        }

        private void OnGUI()
        {
            if (Qa == null || !Qa.Ready) return;
            if (labelStyle == null)
            {
                var font = LanternFonts.UI;
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleRight, font = font };
                labelStyle.normal.textColor = new Color(1f, 0.97f, 0.9f);
                hintStyle = new GUIStyle(labelStyle) { fontSize = 13 };
                hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold, font = font };
            }
            float w = Screen.width;
            if (!LabelsVisible) { GUI.Label(new Rect(w - 260, 8, 250, 22), "U 顯示標籤", hintStyle); return; }
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(w - 470, 6, 464, 96), Texture2D.whiteTexture);
            GUI.color = Color.white;
            string name = WantB ? "B｜構圖改造" + (WantFocus ? "＋光照引導" : "（只改擺設）") : "A｜現況";
            GUI.Label(new Rect(w - 466, 10, 452, 26), "餐廳空間構圖：" + name, labelStyle);
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = !WantB ? new Color(1f, 0.75f, 0.4f) : prev;
            if (GUI.Button(new Rect(w - 300, 40, 56, 30), "A", buttonStyle)) SetB(false);
            GUI.backgroundColor = WantB ? new Color(1f, 0.75f, 0.4f) : prev;
            if (GUI.Button(new Rect(w - 238, 40, 56, 30), "B", buttonStyle)) SetB(true);
            GUI.backgroundColor = WantFocus ? new Color(0.7f, 0.85f, 1f) : prev;
            if (GUI.Button(new Rect(w - 176, 40, 166, 30), WantFocus ? "光照引導：開" : "光照引導：關", buttonStyle)) SetFocus(!WantFocus);
            GUI.backgroundColor = prev;
            GUI.Label(new Rect(w - 466, 76, 452, 22), "F6 A／B・G 光照引導（只在 B）・按住 Alt 點按鈕・U 隱藏", hintStyle);
        }
    }
}
