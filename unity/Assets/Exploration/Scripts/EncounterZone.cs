using UnityEngine;

namespace Shepherd.Exploration
{
    public enum EncounterCheck
    {
        /// <summary>距離太遠或已擊敗：沒有反應。</summary>
        None,
        /// <summary>接近中：顯示提示，尚未進入戰鬥。</summary>
        Near,
        /// <summary>進入遭遇區：開始戰鬥。</summary>
        Trigger,
    }

    /// <summary>
    /// 異域遭遇區：以敵群中心為圓心的水平距離判定（不需物理觸發器，便於測試）。
    /// 已擊敗的遭遇區不會再觸發。
    /// </summary>
    public sealed class EncounterZone
    {
        public const float DefaultTriggerRadius = 6.5f;
        public const float DefaultWarnRadius = 14f;
        /// <summary>高度差超過此值不觸發（例如之後加入樓層時）。</summary>
        public const float MaxHeightDifference = 2.5f;

        public string Id;
        public Vector3 Center;
        public float TriggerRadius = DefaultTriggerRadius;
        public float WarnRadius = DefaultWarnRadius;
        public bool Defeated { get; private set; }
        /// <summary>觸發過的次數（測試確認不會重複觸發）。</summary>
        public int TriggerCount { get; private set; }

        public float HorizontalDistance(Vector3 p)
        {
            var d = p - Center;
            d.y = 0f;
            return d.magnitude;
        }

        public EncounterCheck Check(Vector3 playerFeet)
        {
            if (Defeated) return EncounterCheck.None;
            if (Mathf.Abs(playerFeet.y - Center.y) > MaxHeightDifference) return EncounterCheck.None;
            float d = HorizontalDistance(playerFeet);
            if (d <= TriggerRadius) return EncounterCheck.Trigger;
            if (d <= WarnRadius) return EncounterCheck.Near;
            return EncounterCheck.None;
        }

        public void MarkTriggered() { TriggerCount++; }

        public void MarkDefeated() { Defeated = true; }
    }
}
