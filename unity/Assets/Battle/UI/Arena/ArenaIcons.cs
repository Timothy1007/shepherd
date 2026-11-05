using System;
using System.Collections.Generic;
using Shepherd.Battle.Core;
using UnityEngine;

namespace Shepherd.Battle.UI.Arena
{
    public enum ArenaIcon
    {
        Attack, Defense, Heal, Link, Sustain, Bind,
        SchoolStructure, SchoolProcess, SchoolRelation, Resonance,
    }

    /// <summary>
    /// 程式產生的簡單向量圖示（白色遮罩，使用時以 Image.color 上色）。佔位用，不使用外部美術。
    /// 形狀以 [-1,1] 座標的函式描述，4×4 超取樣做反鋸齒。
    /// </summary>
    public static class ArenaIcons
    {
        private const int Size = 64;
        private static readonly Dictionary<ArenaIcon, Sprite> cache = new Dictionary<ArenaIcon, Sprite>();

        public static Sprite Get(ArenaIcon icon)
        {
            Sprite s;
            if (cache.TryGetValue(icon, out s) && s != null) return s;
            s = Build(icon.ToString(), Shape(icon));
            cache[icon] = s;
            return s;
        }

        public static ArenaIcon For(CardRole role)
        {
            switch (role)
            {
                case CardRole.Attack: return ArenaIcon.Attack;
                case CardRole.Defense: return ArenaIcon.Defense;
                case CardRole.Heal: return ArenaIcon.Heal;
                case CardRole.Link: return ArenaIcon.Link;
                default: return ArenaIcon.Sustain;
            }
        }

        public static ArenaIcon For(SchoolKind school)
        {
            switch (school)
            {
                case SchoolKind.Structure: return ArenaIcon.SchoolStructure;
                case SchoolKind.Process: return ArenaIcon.SchoolProcess;
                default: return ArenaIcon.SchoolRelation;
            }
        }

        public static Color RoleColor(CardRole role)
        {
            switch (role)
            {
                case CardRole.Attack: return new Color(1f, 0.62f, 0.45f);
                case CardRole.Defense: return new Color(0.55f, 0.8f, 1f);
                case CardRole.Heal: return new Color(0.55f, 1f, 0.6f);
                case CardRole.Link: return new Color(0.82f, 0.62f, 1f);
                default: return new Color(0.45f, 0.95f, 0.88f);
            }
        }

        /// <summary>三個學派一致的識別色（按鈕、手牌頂條、編排區標題共用）。</summary>
        public static Color SchoolColor(SchoolKind s)
        {
            switch (s)
            {
                case SchoolKind.Structure: return new Color(0.86f, 0.62f, 0.3f);
                case SchoolKind.Process: return new Color(0.3f, 0.78f, 0.7f);
                default: return new Color(0.68f, 0.48f, 0.95f);
            }
        }

        private static Sprite Build(string name, Func<float, float, bool> inside)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "ArenaIcon_" + name, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int hit = 0;
                for (int sy = 0; sy < 4; sy++)
                for (int sx = 0; sx < 4; sx++)
                {
                    float u = ((x + (sx + 0.5f) / 4f) / Size) * 2f - 1f;
                    float v = ((y + (sy + 0.5f) / 4f) / Size) * 2f - 1f;
                    if (inside(u, v)) hit++;
                }
                pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(hit * 255 / 16));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = tex.name;
            return sprite;
        }

        private static bool Ring(float x, float y, float cx, float cy, float r, float t)
        {
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return Mathf.Abs(d - r) < t;
        }

        private static bool Disc(float x, float y, float cx, float cy, float r)
        {
            return (x - cx) * (x - cx) + (y - cy) * (y - cy) < r * r;
        }

        private static bool Segment(float x, float y, float ax, float ay, float bx, float by, float t)
        {
            float px = x - ax, py = y - ay, dx = bx - ax, dy = by - ay;
            float k = Mathf.Clamp01((px * dx + py * dy) / (dx * dx + dy * dy));
            float ex = px - dx * k, ey = py - dy * k;
            return ex * ex + ey * ey < t * t;
        }

        private static Func<float, float, bool> Shape(ArenaIcon icon)
        {
            switch (icon)
            {
                case ArenaIcon.Attack:
                    // 斜放的劍：刃、護手、握柄。
                    return (x, y) =>
                    {
                        float u = (x + y) * 0.7071f, v = (y - x) * 0.7071f;
                        bool blade = u > -0.35f && u < 0.92f && Mathf.Abs(v) < 0.15f * (1f - Mathf.Clamp01((u - 0.5f) / 0.42f));
                        bool guard = Mathf.Abs(u + 0.4f) < 0.08f && Mathf.Abs(v) < 0.4f;
                        bool hilt = Mathf.Abs(v) < 0.08f && u > -0.88f && u < -0.4f;
                        return blade || guard || hilt;
                    };
                case ArenaIcon.Defense:
                    // 盾形：上方方正、下方收尖。
                    return (x, y) => y < 0.8f && y > -0.92f && Mathf.Abs(x) < 0.74f
                        && (y > -0.15f || Mathf.Abs(x) < 0.74f * (y + 0.92f) / 0.77f);
                case ArenaIcon.Heal:
                    return (x, y) => (Mathf.Abs(x) < 0.22f && Mathf.Abs(y) < 0.78f) || (Mathf.Abs(y) < 0.22f && Mathf.Abs(x) < 0.78f);
                case ArenaIcon.Link:
                    return (x, y) => Ring(x, y, -0.3f, 0f, 0.42f, 0.13f) || Ring(x, y, 0.3f, 0f, 0.42f, 0.13f);
                case ArenaIcon.Sustain:
                    // 有缺口的循環箭頭。
                    return (x, y) =>
                    {
                        float a = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
                        bool ring = Ring(x, y, 0f, 0f, 0.6f, 0.15f) && !(a > 50f && a < 115f);
                        bool head = y > 0.22f && y < 0.95f && x > 0.12f && x < 0.75f && (y - 0.22f) < (0.75f - x) * 1.2f && x + y > 0.62f;
                        return ring || head;
                    };
                case ArenaIcon.Bind:
                    // 鎖頭：鎖身＋鎖環。
                    return (x, y) => (Mathf.Abs(x) < 0.58f && y > -0.85f && y < 0.12f)
                        || (Ring(x, y, 0f, 0.12f, 0.38f, 0.13f) && y > 0.1f);
                case ArenaIcon.SchoolStructure:
                    return (x, y) =>
                    {
                        float m = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                        return (m < 0.8f && m > 0.56f) || m < 0.22f;
                    };
                case ArenaIcon.SchoolProcess:
                    return (x, y) =>
                    {
                        float a = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
                        return (Ring(x, y, 0f, 0f, 0.7f, 0.12f) && !(a > -30f && a < 30f))
                            || (Ring(x, y, 0f, 0f, 0.36f, 0.12f) && !(a > 150f || a < -150f)) || Disc(x, y, 0f, 0f, 0.1f);
                    };
                case ArenaIcon.SchoolRelation:
                    return (x, y) => Disc(x, y, 0f, 0.6f, 0.22f) || Disc(x, y, -0.6f, -0.45f, 0.22f) || Disc(x, y, 0.6f, -0.45f, 0.22f)
                        || Segment(x, y, 0f, 0.6f, -0.6f, -0.45f, 0.07f) || Segment(x, y, 0f, 0.6f, 0.6f, -0.45f, 0.07f)
                        || Segment(x, y, -0.6f, -0.45f, 0.6f, -0.45f, 0.07f);
                default:
                    // 共鳴：八角星。
                    return (x, y) =>
                    {
                        float r = Mathf.Sqrt(x * x + y * y);
                        float a = Mathf.Atan2(y, x);
                        float edge = 0.38f + 0.52f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 4f)), 3f);
                        return r < edge;
                    };
            }
        }
    }
}
