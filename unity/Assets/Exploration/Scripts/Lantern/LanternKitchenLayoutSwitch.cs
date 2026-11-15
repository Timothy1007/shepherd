using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 廚房「空間改造 B 方案」QA。只存在於獨立場景 Assets/Exploration/Scenes/LanternKitchenLayoutQA.unity。
    /// F6：A（現況）⇄ B（改造）；L：B 的「生活細節」那組開／關（預覽之後褪色態比較空曠的樣子）；按住 Alt 點按鈕；U 隱藏標籤。
    /// 切換只移動／顯示物件：不重載場景、不動玩家位置、FOV、環境光、畫質與提燈。
    /// </summary>
    public sealed class LanternKitchenLayoutSwitch : MonoBehaviour
    {
        public LanternPrototypeQA Qa { get; private set; }
        public bool WantB { get; private set; }
        public bool WantLife { get; private set; } = true;
        public bool LabelsVisible = true;
        private bool entered;
        private GUIStyle labelStyle, buttonStyle, hintStyle;

        private void Start() => Bind(FindObjectOfType<LanternPrototypeQA>());

        public void Bind(LanternPrototypeQA qa)
        {
            Qa = qa;
            if (Qa == null) return;
            Qa.RenderStyleQA = true;   // F6 讓給這個 QA（平常是傳送到戰鬥場）
            ApplyNow();
        }

        private void OnDestroy()
        {
            if (Qa != null && Qa.World != null) { Qa.World.SetKitchenGroupVisible(LanternGraybox.KitchenGroup.Life, true); Qa.World.SetKitchenLayout(false); }
            if (Qa != null) { Qa.RenderStyleQA = false; Qa.CursorUiOpen = false; }
        }

        /// <summary>起點：廚房門內側（從餐廳進來的那扇門），面向北（後窗）。</summary>
        public static Vector3 EntrancePoint => LanternGraybox.V2Origin + new Vector3(0.125f + 2.0f, 0.05f, 13.125f + 0.55f);

        public void EnterAtDoor()
        {
            if (Qa == null || Qa.World == null) return;
            Qa.GoTo("V2Kitchen");
            Qa.Player.Teleport(EntrancePoint, 0f, 0f);
            Qa.Rig.SetOn(false);
        }

        public void SetB(bool b) { WantB = b; ApplyNow(); }
        public void SetLife(bool on) { WantLife = on; ApplyNow(); }

        private void ApplyNow()
        {
            if (Qa == null || Qa.World == null) return;
            var w = Qa.World;
            if (w.KitchenGroupVisible(LanternGraybox.KitchenGroup.Life) != WantLife) w.SetKitchenGroupVisible(LanternGraybox.KitchenGroup.Life, WantLife);
            if (w.KitchenLayoutIsB != WantB) w.SetKitchenLayout(WantB);
        }

        private void Update()
        {
            if (!Application.isPlaying || Qa == null || !Qa.Ready) return;
            if (!entered) { entered = true; EnterAtDoor(); }
            if (Input.GetKeyDown(KeyCode.F6)) SetB(!WantB);
            if (Input.GetKeyDown(KeyCode.L)) SetLife(!WantLife);
            if (Input.GetKeyDown(KeyCode.U)) LabelsVisible = !LabelsVisible;
            Qa.CursorUiOpen = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }

        private void LateUpdate()
        {
            if (Qa != null && Qa.Ready) ApplyNow();   // F1／R 重建房子後回到 A：每格確認狀態一致
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
            string name = WantB ? "B｜空間改造" + (WantLife ? "" : "（生活細節關閉）") : "A｜現況";
            GUI.Label(new Rect(w - 466, 10, 452, 26), "廚房：" + name, labelStyle);
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = !WantB ? new Color(1f, 0.75f, 0.4f) : prev;
            if (GUI.Button(new Rect(w - 300, 40, 56, 30), "A", buttonStyle)) SetB(false);
            GUI.backgroundColor = WantB ? new Color(1f, 0.75f, 0.4f) : prev;
            if (GUI.Button(new Rect(w - 238, 40, 56, 30), "B", buttonStyle)) SetB(true);
            GUI.backgroundColor = WantLife ? new Color(0.7f, 0.85f, 1f) : prev;
            if (GUI.Button(new Rect(w - 176, 40, 166, 30), WantLife ? "生活細節：開" : "生活細節：關", buttonStyle)) SetLife(!WantLife);
            GUI.backgroundColor = prev;
            GUI.Label(new Rect(w - 466, 76, 452, 22), "F6 A／B・L 生活細節（只在 B）・按住 Alt 點按鈕・U 隱藏", hintStyle);
        }
    }
}
