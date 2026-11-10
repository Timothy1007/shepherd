using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// Phase 1.5 P0：滑鼠視角／游標鎖定。
    /// 根因：探索時「想要鎖定」（cursorCaptured）與實際的 Cursor.lockState 會不同步——
    /// 例如離開十七號時屋內 QA 的 OnDestroy（Destroy 延到影格結束）在街區已經鎖定之後才把游標放開；
    /// 或 Alt-Tab／編輯器失焦後回來。這時滑鼠位移仍在轉視角，但游標是自由的，移到螢幕邊緣就轉不動。
    /// 做法：每個影格把實際狀態對齊「想要的狀態」（有焦點才重新鎖定），而且只有真的鎖定時才用滑鼠轉視角。
    /// </summary>
    public static class Case01Cursor
    {
        /// <summary>依「想不想鎖定」與視窗焦點，回傳應該設定的鎖定狀態（null＝不要動）。純函式，供測試。</summary>
        public static CursorLockMode? Desired(bool wanted, bool focused, CursorLockMode current)
        {
            if (wanted)
            {
                if (!focused) return null; // 失焦時不要搶；回到焦點的那一影格再鎖。
                return current == CursorLockMode.Locked ? (CursorLockMode?)null : CursorLockMode.Locked;
            }
            return current == CursorLockMode.None ? (CursorLockMode?)null : CursorLockMode.None;
        }

        /// <summary>每個影格呼叫（只在 Play 中）：把游標對齊想要的狀態。</summary>
        public static void Maintain(bool wanted)
        {
            if (!Application.isPlaying) return;
            var d = Desired(wanted, Application.isFocused, Cursor.lockState);
            if (!d.HasValue) return;
            Cursor.lockState = d.Value;
            Cursor.visible = d.Value != CursorLockMode.Locked;
        }

        /// <summary>
        /// 這個影格能不能用滑鼠位移轉視角：想要鎖定，而且（Play 中、有焦點時）游標真的被鎖住。
        /// 沒鎖住時不轉（游標是自由的，會碰到螢幕邊緣），畫面顯示「點擊畫面恢復視角」。EditMode 測試與 batchmode 照舊。
        /// </summary>
        public static bool Looking(bool wanted)
        {
            if (!wanted) return false;
            if (!Application.isPlaying || Application.isBatchMode || !Application.isFocused) return true;
            return Cursor.lockState == CursorLockMode.Locked;
        }
    }
}
