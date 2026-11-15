using System;
using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 提燈原型的滑鼠鎖定狀態（唯一管理 Cursor.lockState 的地方）。
    /// 探索時鎖定並隱藏游標，視角只吃相對滑鼠位移（Mouse X／Mouse Y）；
    /// Esc＝放開滑鼠（點一下畫面繼續）；失去焦點時放開，回到視窗後自動重新鎖定。
    /// 編輯器在按 Esc、切換視窗時會自己把游標解鎖，所以每個影格都核對實際狀態，不一致就重新套用，
    /// 避免「程式以為還鎖著、實際上游標已經在螢幕上亂跑，碰到邊緣就轉不動」。
    /// 讀寫真正的 Cursor 透過 <see cref="ReadLock"/>／<see cref="Apply"/>，測試可以換成假的。
    /// </summary>
    public sealed class LanternCursorLock
    {
        /// <summary>讀取實際的鎖定狀態（預設＝Cursor.lockState）。</summary>
        public Func<CursorLockMode> ReadLock = () => Cursor.lockState;
        /// <summary>套用鎖定（true＝鎖定並隱藏；false＝放開並顯示）。</summary>
        public Action<bool> Apply = locked =>
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        };

        /// <summary>視窗有焦點（OnApplicationFocus）。</summary>
        public bool Focused { get; private set; } = true;
        /// <summary>玩家按了 Esc 放開滑鼠；點一下畫面才回到探索。</summary>
        public bool Paused { get; private set; }
        /// <summary>有需要游標指點的介面開著（例如之後的選單）。由使用端每個影格設定。</summary>
        public bool UiNeedsCursor;

        /// <summary>現在應該鎖定游標。</summary>
        public bool WantLocked => Focused && !Paused && !UiNeedsCursor;
        /// <summary>這個影格要不要把滑鼠位移拿來轉視角（剛重新鎖定的那一格不吃，避免游標跳回中央造成的大位移）。</summary>
        public bool LookActive => WantLocked && settleFrames == 0;
        /// <summary>左鍵可以當射擊（剛用來恢復鎖定的那一下，要放開後才算）。</summary>
        public bool PrimaryAllowed => WantLocked && !swallowHold;
        /// <summary>重新套用鎖定的次數（測試用）。</summary>
        public int Reapplied { get; private set; }

        private bool applied, everApplied, swallowHold;
        private int settleFrames, desyncStreak;

        public void OnFocus(bool focused)
        {
            if (Focused == focused) return;
            Focused = focused;
            // 點回視窗的那一下不要變成射擊。
            if (focused) swallowHold = true;
            Sync(false);
        }

        /// <summary>手動放開／恢復（例如 Esc 以外的暫停）。</summary>
        public void SetPaused(bool paused)
        {
            if (Paused == paused) return;
            Paused = paused;
            if (!paused) swallowHold = true;
            Sync(false);
        }

        /// <summary>每個影格呼叫一次。回傳 true＝這次的左鍵用來恢復鎖定（不要當成射擊）。</summary>
        public bool Tick(bool escDown, bool clickDown, bool clickHeld)
        {
            if (settleFrames > 0) settleFrames--;
            if (!clickHeld) swallowHold = false;
            bool consumed = false;
            if (escDown && WantLocked) { Paused = true; }
            else if (clickDown && Paused && Focused && !UiNeedsCursor)
            {
                Paused = false;
                consumed = true;
                swallowHold = clickHeld;
            }
            Sync(true);
            return consumed;
        }

        private void Sync(bool checkActual)
        {
            bool want = WantLocked;
            bool desync = checkActual && want && applied && ReadLock() != CursorLockMode.Locked;
            if (want != applied || !everApplied || desync)
            {
                Apply(want);
                if (want && (!applied || desync))
                {
                    // 一直對不上（平台不回報鎖定，例如 batchmode）時，不要每格都把視角卡掉。
                    // 在 Tick 外（焦點事件）鎖定時，下一次 Tick 會先扣一格，所以多留一格。
                    if (!desync || desyncStreak < 2) settleFrames = checkActual ? 1 : 2;
                    if (desync) Reapplied++;
                }
                applied = want;
                everApplied = true;
            }
            desyncStreak = desync ? desyncStreak + 1 : 0;
        }
    }

    /// <summary>滑鼠靈敏度設定（存在 PlayerPrefs，預設 1＝目前已試玩的手感）。</summary>
    public static class LanternLookSettings
    {
        public const string Key = "Shepherd.Lantern.LookScale";
        public const float Default = 1f, Min = 0.3f, Max = 3f, StepSize = 0.1f;

        public static float Clamp(float v) => Mathf.Clamp(Mathf.Round(v * 10f) / 10f, Min, Max);
        public static float Load() => Clamp(PlayerPrefs.GetFloat(Key, Default));
        public static void Save(float v) { PlayerPrefs.SetFloat(Key, Clamp(v)); PlayerPrefs.Save(); }
    }
}
