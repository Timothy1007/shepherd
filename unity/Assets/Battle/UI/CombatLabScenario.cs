using System.Collections.Generic;
using Shepherd.Battle.Core;

namespace Shepherd.Battle.UI
{
    /// <summary>
    /// Combat Lab 的固定測試戰鬥。兩個敵人讓目標選擇、目標失效、傳導與分流都能被觸發；
    /// 攻擊序列有高有低（含 0），方便觀察回應、穩定與敵方回合。數值只是測試用的情境設定，不是規格。
    /// </summary>
    public sealed class CombatLabScenario
    {
        public const int DefaultSeed = 20261105;

        public string Name;
        public List<EnemyState> Enemies = new List<EnemyState>();

        public static CombatLabScenario Default()
        {
            var s = new CombatLabScenario { Name = "Combat Lab 標準測試：兩個失序體" };
            s.Enemies.Add(new EnemyState
            {
                Id = 1,
                Name = "E1 失序體・甲",
                Value = 40,
                AttackPattern = new[] { 12, 8, 14, 6 },
            });
            s.Enemies.Add(new EnemyState
            {
                Id = 2,
                Name = "E2 失序體・乙",
                Value = 24,
                AttackPattern = new[] { 6, 0, 9, 5 },
            });
            return s;
        }
    }
}
