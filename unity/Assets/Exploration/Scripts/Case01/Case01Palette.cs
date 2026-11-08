using System;
using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// 一次房屋建置所用的材質：共用的材質資產（不可修改）＋本次建置擁有的衍生材質（著色變體、可變的核／裂隙材質）。
    /// 衍生材質集中登記，重建或離開時一次銷毀，不在每影格產生新材質。
    /// 找不到資產時退回純色 Standard 材質（測試環境或尚未產生素材時仍可運作）。
    /// </summary>
    public sealed class Case01Palette : IDisposable
    {
        private static Case01ArtSet cachedSet;
        private readonly Case01ArtSet set;
        private readonly List<Material> owned = new List<Material>();
        private readonly Dictionary<string, Material> tints = new Dictionary<string, Material>();
        private readonly Dictionary<string, Material> fallbacks = new Dictionary<string, Material>();

        public Case01Palette()
        {
            if (cachedSet == null) cachedSet = Resources.Load<Case01ArtSet>(Case01ArtSet.ResourcePath);
            set = cachedSet;
        }

        /// <summary>是否成功載入美術素材（否則使用純色退回材質）。</summary>
        public bool HasArt { get { return set != null; } }
        public Case01ArtSet Set { get { return set; } }
        /// <summary>本次建置擁有的材質數（測試用：確認重建時會釋放）。</summary>
        public int OwnedCount { get { return owned.Count; } }

        /// <summary>共用材質資產（不可在執行時修改）。</summary>
        public Material Get(string key)
        {
            var m = set != null ? set.Find(key) : null;
            if (m != null) return m;
            Material fb;
            if (!fallbacks.TryGetValue(key, out fb))
            {
                fb = Own(ArenaArt.Mat(FallbackColor(key), 0.2f));
                fallbacks[key] = fb;
            }
            return fb;
        }

        /// <summary>同一材質的著色變體（依 key＋顏色快取，同次建置只建一次）。</summary>
        public Material Tint(string key, Color color)
        {
            string id = key + "|" + ColorUtility.ToHtmlStringRGBA(color);
            Material m;
            if (tints.TryGetValue(id, out m)) return m;
            m = Own(new Material(Get(key)) { name = key + "_tint" });
            m.color = color;
            tints[id] = m;
            return m;
        }

        /// <summary>可在執行時修改的獨立副本（核、裂隙線等會隨狀態改變發光的材質）。</summary>
        public Material Instance(string key)
        {
            return Own(new Material(Get(key)) { name = key + "_instance" });
        }

        /// <summary>隨物件移動的道具（FP-001）：一般 UV 的 Standard 紙張材質。</summary>
        public Material PaperStandard(Color color)
        {
            var m = Own(ArenaArt.Mat(color, 0.1f));
            if (set != null && set.PaperTexture != null) m.mainTexture = set.PaperTexture;
            return m;
        }

        public bool OwnsMaterial(Material m)
        {
            return m != null && owned.Contains(m);
        }

        public Material Own(Material m)
        {
            owned.Add(m);
            return m;
        }

        public void Dispose()
        {
            foreach (var m in owned) if (m != null) ArenaArt.Kill(m);
            owned.Clear();
            tints.Clear();
            fallbacks.Clear();
        }

        private static Color FallbackColor(string key)
        {
            if (key.StartsWith("wood") || key.StartsWith("floor") || key.StartsWith("frame")) return new Color(0.4f, 0.28f, 0.18f);
            if (key.StartsWith("wall") || key == "ceiling" || key.StartsWith("trim")) return new Color(0.6f, 0.58f, 0.53f);
            if (key.StartsWith("mezz") || key.StartsWith("core")) return new Color(0.2f, 0.17f, 0.23f);
            if (key.StartsWith("court")) return new Color(0.28f, 0.26f, 0.27f);
            return new Color(0.6f, 0.6f, 0.6f);
        }
    }
}
