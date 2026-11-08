using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>室內 QA 一個影格的輸入：沿用探索輸入（WASD／滑鼠／Shift／E／Esc），再加上選項、日誌與 QA 跳轉。</summary>
    public struct Case01Input
    {
        public ExploreInput Base;
        /// <summary>按下的選項（1～3），沒有為 0。</summary>
        public int Choice;
        /// <summary>J：開關日誌。</summary>
        public bool Journal;
        /// <summary>F1～F4：跳到 S00／S30／S40／S50 檢查點，沒有為 0。</summary>
        public int Jump;
        /// <summary>滑鼠滾輪（證物檢視時縮放）。</summary>
        public float Zoom;
        /// <summary>I：再次檢視已收藏的證物。</summary>
        public bool InspectEvidence;
        /// <summary>F5～F10 的 QA 功能鍵（5＝切換核校正 v0.2／v0.3，6＝H0～H3，7＝形成演出／自語預覽，8＝降低閃爍，9＝失序門預覽，10＝記憶殘響預覽），沒有為 0。</summary>
        public int Function;
        /// <summary>Tab：略過目前的自語字幕。</summary>
        public bool SkipLine;

        public static Case01Input None { get { return new Case01Input(); } }

        public static Case01Input From(ExploreInput b) { return new Case01Input { Base = b }; }
    }

    public interface ICase01InputSource
    {
        Case01Input Read();
    }

    /// <summary>使用專案現有的 Input Manager（舊輸入系統）。</summary>
    public sealed class LegacyCase01Input : ICase01InputSource
    {
        private readonly LegacyExploreInput explore = new LegacyExploreInput();

        public Case01Input Read()
        {
            var r = new Case01Input { Base = explore.Read() };
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) r.Choice = 1;
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) r.Choice = 2;
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) r.Choice = 3;
            r.Journal = Input.GetKeyDown(KeyCode.J);
            r.Zoom = Input.mouseScrollDelta.y;
            r.InspectEvidence = Input.GetKeyDown(KeyCode.I);
            r.SkipLine = Input.GetKeyDown(KeyCode.Tab);
            if (Input.GetKeyDown(KeyCode.F1)) r.Jump = 1;
            else if (Input.GetKeyDown(KeyCode.F2)) r.Jump = 2;
            else if (Input.GetKeyDown(KeyCode.F3)) r.Jump = 3;
            else if (Input.GetKeyDown(KeyCode.F4)) r.Jump = 4;
            if (Input.GetKeyDown(KeyCode.F5)) r.Function = 5;
            else if (Input.GetKeyDown(KeyCode.F6)) r.Function = 6;
            else if (Input.GetKeyDown(KeyCode.F7)) r.Function = 7;
            else if (Input.GetKeyDown(KeyCode.F8)) r.Function = 8;
            else if (Input.GetKeyDown(KeyCode.F9)) r.Function = 9;
            else if (Input.GetKeyDown(KeyCode.F10)) r.Function = 10;
            return r;
        }
    }
}
