using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// Case01 材質資產清單（由 <c>Shepherd/Case01/重建美術素材</c> 產生，放在 Case01Art/Resources，執行時以 Resources 載入）。
    /// </summary>
    [CreateAssetMenu(menuName = "Shepherd/Case01/Art Set", fileName = "Case01ArtSet")]
    public sealed class Case01ArtSet : ScriptableObject
    {
        public const string ResourcePath = "Case01ArtSet";

        [Serializable]
        public sealed class Entry
        {
            public string Key;
            public Material Material;
        }

        public List<Entry> Materials = new List<Entry>();

        /// <summary>FP-001 等需要隨物件移動的道具使用的紙張貼圖（一般 UV，不用世界座標投影）。</summary>
        public Texture2D PaperTexture;

        public Material Find(string key)
        {
            foreach (var e in Materials) if (e.Key == key) return e.Material;
            return null;
        }
    }
}
