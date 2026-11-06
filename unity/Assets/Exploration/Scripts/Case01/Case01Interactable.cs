using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>可用準星＋E 調查的物件。碰撞體為 Trigger（不擋路），由視線射線偵測。執行時建立，不存進場景。</summary>
    public sealed class Case01Interactable : MonoBehaviour
    {
        public string Id;
        public string Prompt;
    }
}
