using UnityEngine;

namespace Shepherd.Exploration
{
    /// <summary>一個影格的探索輸入。由輸入來源產生，測試可直接建立。</summary>
    public struct ExploreInput
    {
        /// <summary>x＝左右（A/D），y＝前後（W/S），範圍 -1～1。</summary>
        public Vector2 Move;
        /// <summary>滑鼠位移（Input Manager 的 Mouse X／Mouse Y）。</summary>
        public Vector2 Look;
        public bool Run;
        public bool Interact;
        public bool ToggleView;
        public bool ReleaseCursor;
        public bool Click;

        public static ExploreInput None { get { return new ExploreInput(); } }

        public static ExploreInput Forward(bool run = false)
        {
            return new ExploreInput { Move = new Vector2(0f, 1f), Run = run };
        }
    }

    public interface IExploreInputSource
    {
        ExploreInput Read();
    }

    /// <summary>使用專案現有的 Input Manager（舊輸入系統，不需另裝套件）。</summary>
    public sealed class LegacyExploreInput : IExploreInputSource
    {
        public ExploreInput Read()
        {
            return new ExploreInput
            {
                Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
                Look = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")),
                Run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
                Interact = Input.GetKeyDown(KeyCode.E),
                ToggleView = Input.GetKeyDown(KeyCode.V),
                ReleaseCursor = Input.GetKeyDown(KeyCode.Escape),
                Click = Input.GetMouseButtonDown(0),
            };
        }
    }
}
