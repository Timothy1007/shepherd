using UnityEngine;

namespace Shepherd.Battle.UI.Arena
{
    /// <summary>掛在 3D 敵人根物件上，讓滑鼠射線能對應回 BattleState 的敵人編號。</summary>
    public sealed class ArenaEnemyMarker : MonoBehaviour
    {
        public int EnemyId;
    }
}
