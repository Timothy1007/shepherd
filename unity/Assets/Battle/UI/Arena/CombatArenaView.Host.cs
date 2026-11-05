using System;
using Shepherd.Battle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Battle.UI.Arena
{
    /// <summary>
    /// 把 3D 戰鬥嵌入其他場景（例如探索街區）時的設定。
    /// 預設值等同 CombatLab3D 單獨場景的行為；嵌入時由外部場景提供地形、燈光、霧與主攝影機。
    /// 戰鬥規則與結算仍完全由 <see cref="CombatLabSession"/>（BattleController／RoundResolver）負責。
    /// </summary>
    public sealed class ArenaHostOptions
    {
        /// <summary>是否建立 Combat Lab 自己的廣場、建築與月光。嵌入時關閉，沿用外部場景作為戰場背景。</summary>
        public bool BuildEnvironment = true;

        /// <summary>Play 時是否由戰鬥套用霧與環境光（嵌入時由外部場景管理）。</summary>
        public bool ManageAtmosphere = true;

        /// <summary>戰鬥攝影機一開始是否啟用；嵌入時由外部場景在鏡頭過場結束後啟用。</summary>
        public bool CameraStartsEnabled = true;

        /// <summary>結果橫幅按鈕：勝利時的文字與動作（null＝沿用「再戰一次」重置戰鬥）。</summary>
        public string VictoryLabel;
        public Action OnVictory;

        /// <summary>結果橫幅按鈕：戰敗時的文字與動作（null＝沿用「再戰一次」重置戰鬥）。</summary>
        public string DefeatLabel;
        public Action OnDefeat;

        /// <summary>CombatLab3D 單獨場景使用的預設值。</summary>
        public static ArenaHostOptions Standalone() { return new ArenaHostOptions(); }

        /// <summary>嵌入其他場景使用的預設值。</summary>
        public static ArenaHostOptions Embedded()
        {
            return new ArenaHostOptions { BuildEnvironment = false, ManageAtmosphere = false, CameraStartsEnabled = false };
        }
    }

    public sealed partial class CombatArenaView
    {
        public const float DefaultFieldOfView = 45f;

        private ArenaHostOptions host = ArenaHostOptions.Standalone();
        private Button bannerButton;
        private bool endRaised;

        /// <summary>戰鬥結束（勝利或戰敗的事件播放完畢）時觸發一次。</summary>
        public event Action<BattleOutcome> BattleEnded;

        /// <summary>false 時不處理滑鼠點選與 Space 略過（鏡頭過場期間使用）。</summary>
        public bool InteractionEnabled { get; set; } = true;

        public ArenaHostOptions Host { get { return host; } }

        public Button BannerButton { get { return bannerButton; } }

        public string BannerButtonText { get { return bannerButton.GetComponentInChildren<Text>().text; } }

        /// <summary>戰鬥 HUD 是否顯示（過場時隱藏）。</summary>
        public bool HudVisible
        {
            get { return canvas != null && canvas.enabled; }
            set { if (canvas != null) canvas.enabled = value; }
        }

        /// <summary>第 index 個（共 count 個）敵人相對於戰場原點的位置；探索場景用它把敵人擺在與戰鬥相同的位置。</summary>
        public static Vector3 EnemyLocalHome(int index, int count)
        {
            float x = (index - (count - 1) * 0.5f) * 3.6f + 0.6f;
            float z = 2.4f + (index % 2) * 0.7f;
            return new Vector3(x, 0f, z);
        }

        /// <summary>敵人模型高度（依失序值，與戰鬥中相同）。</summary>
        public static float EnemyHeightFor(int value)
        {
            return Mathf.Clamp(0.85f + value / 120f, 0.85f, 1.35f);
        }

        /// <summary>敵人面向玩家的朝向（相對於戰場原點）。</summary>
        public static Quaternion EnemyLocalRotation(Vector3 localHome)
        {
            var d = PlayerPosition - localHome;
            d.y = 0f;
            return Quaternion.LookRotation(d, Vector3.up);
        }

        /// <summary>戰鬥中玩家的朝向（相對於戰場原點）。</summary>
        public static readonly Quaternion PlayerLocalRotation = Quaternion.LookRotation(new Vector3(0.6f, 0f, 5.6f));

        /// <summary>敵群中心（相對於戰場原點）。</summary>
        public static Vector3 EnemyGroupCenter(int count)
        {
            var c = Vector3.zero;
            for (int i = 0; i < count; i++) c += EnemyLocalHome(i, count);
            return count > 0 ? c / count : new Vector3(0.6f, 0f, 2.6f);
        }

        /// <summary>
        /// 調整戰鬥攝影機（相對於戰場原點）。震動等效果以這個位置為基準。
        /// 供探索場景比較「第三人稱戰術鏡頭」與「第一人稱」兩種戰鬥視角。
        /// </summary>
        public void SetCameraPose(Vector3 localPosition, Vector3 localLookAt, float fieldOfView)
        {
            if (cam == null) return;
            cam.transform.localPosition = localPosition;
            cam.transform.LookAt(world.TransformPoint(localLookAt));
            cam.fieldOfView = fieldOfView;
            cameraHome = localPosition;
        }

        /// <summary>顯示或隱藏玩家模型（第一人稱戰鬥時隱藏，避免擋住鏡頭）。提燈光源保留。</summary>
        public bool PlayerAvatarVisible
        {
            get { return playerRenderers != null && playerRenderers.Length > 0 && playerRenderers[0].enabled; }
            set
            {
                if (playerRenderers == null) return;
                foreach (var r in playerRenderers) if (r != null) r.enabled = value;
            }
        }

        private void OnBannerButton()
        {
            var outcome = Session.Snapshot().Outcome;
            if (outcome == BattleOutcome.Victory && host.OnVictory != null) host.OnVictory();
            else if (outcome == BattleOutcome.Defeat && host.OnDefeat != null) host.OnDefeat();
            else OnReset();
        }

        private void RefreshBannerButton(BattleOutcome outcome)
        {
            if (bannerButton == null) return;
            string label = "再戰一次";
            if (outcome == BattleOutcome.Victory && host.OnVictory != null) label = host.VictoryLabel ?? label;
            else if (outcome == BattleOutcome.Defeat && host.OnDefeat != null) label = host.DefeatLabel ?? label;
            bannerButton.GetComponentInChildren<Text>().text = label;
        }

        private void RaiseBattleEnded(BattleOutcome outcome)
        {
            if (outcome == BattleOutcome.Ongoing || endRaised) return;
            endRaised = true;
            if (BattleEnded != null) BattleEnded(outcome);
        }
    }
}
