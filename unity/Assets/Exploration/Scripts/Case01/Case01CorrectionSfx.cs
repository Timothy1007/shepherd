using System.Collections.Generic;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// 現實校正 QA 專用的合成音效（只有這個 QA 用；不動共用的 Case01Sfx）：
    /// 校正命中（上升的和聲＋玻璃般的泛音）、落空（短促的嘶聲）、櫃子立起（木頭吱呀＋氣流）、撐不住的預警（低頻顫動的木頭聲）、
    /// 倒下（沉重的木頭撞擊＋罐子碰撞）、牽引拾取／放下／拒絕、牽引中的低鳴、異域的低沉嗡聲、局部穩定（下行解決的和弦）、遠處的敲擊。
    /// </summary>
    public static class Case01CorrectionSfx
    {
        public const int Rate = 22050;
        private static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        public static readonly string[] All = { "rc_correct", "rc_fizzle", "rc_rise", "rc_warn", "rc_thud", "rc_grab", "rc_place", "rc_deny", "rc_tether_loop", "rc_zone_loop", "rc_stable", "rc_knock" };

        public static AudioClip Get(string name)
        {
            AudioClip c;
            if (cache.TryGetValue(name, out c) && c != null) return c;
            c = Make(name);
            cache[name] = c;
            return c;
        }

        private static float Env(float t, float a, float d) { return t < a ? t / a : Mathf.Exp(-(t - a) / d); }

        private static AudioClip Clip(string name, float[] d)
        {
            var c = AudioClip.Create("Case01RcSfx_" + name, d.Length, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        private static AudioClip Make(string name)
        {
            var rng = new System.Random(name.GetHashCode());
            System.Func<float> N = () => (float)rng.NextDouble() * 2f - 1f;
            float[] d;
            int n;
            switch (name)
            {
                case "rc_correct":
                {
                    n = (int)(Rate * 0.9f);
                    d = new float[n];
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        float sweep = Mathf.Lerp(330f, 660f, Mathf.Clamp01(t / 0.25f));
                        float v = Mathf.Sin(2f * Mathf.PI * sweep * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * sweep * 1.5f * t) * 0.2f + Mathf.Sin(2f * Mathf.PI * 1980f * t) * 0.08f;
                        d[i] = v * Env(t, 0.02f, 0.35f) * 0.7f;
                    }
                    return Clip(name, d);
                }
                case "rc_fizzle":
                {
                    n = (int)(Rate * 0.25f);
                    d = new float[n];
                    float lp = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        lp += (N() - lp) * 0.3f;
                        d[i] = (lp * 0.8f + Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(500f, 200f, t / 0.25f) * t) * 0.2f) * Env(t, 0.005f, 0.07f) * 0.5f;
                    }
                    return Clip(name, d);
                }
                case "rc_rise":
                case "rc_warn":
                {
                    bool warn = name == "rc_warn";
                    float len = warn ? 1.2f : 0.6f;
                    n = (int)(Rate * len);
                    d = new float[n];
                    float ph = 0f, lp = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        float f = (warn ? 140f : 260f) + (warn ? 40f * Mathf.Sin(t * 31f) : 180f * t);
                        ph += f / Rate;
                        float stick = Mathf.Pow(Mathf.Abs(Mathf.Sin(ph * Mathf.PI * 2f)), warn ? 5f : 8f);
                        lp += (N() - lp) * 0.05f;
                        float trem = warn ? 0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * 9f) : 1f;
                        d[i] = (stick * 0.55f + lp * (warn ? 0.2f : 0.5f)) * Mathf.Sin(Mathf.PI * t / len) * trem * 0.55f;
                    }
                    return Clip(name, d);
                }
                case "rc_thud":
                {
                    n = (int)(Rate * 1.1f);
                    d = new float[n];
                    float lp = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        lp += (N() - lp) * 0.06f;
                        float body = (Mathf.Sin(2f * Mathf.PI * 62f * t) * 0.9f + lp * 1.8f) * Env(t, 0.003f, 0.16f);
                        float clink = 0f;
                        foreach (var at in new[] { 0.09f, 0.17f, 0.26f, 0.41f, 0.58f })
                            if (t >= at) clink += Mathf.Sin(2f * Mathf.PI * (2100f + at * 900f) * t) * Env(t - at, 0.001f, 0.04f) * 0.25f;
                        d[i] = Mathf.Clamp(body + clink, -1f, 1f) * 0.85f;
                    }
                    return Clip(name, d);
                }
                case "rc_grab":
                case "rc_place":
                case "rc_deny":
                {
                    float len = name == "rc_deny" ? 0.3f : 0.35f;
                    n = (int)(Rate * len);
                    d = new float[n];
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        float v;
                        if (name == "rc_grab") v = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(420f, 700f, t / len) * t) * Env(t, 0.01f, 0.1f) * 0.45f;
                        else if (name == "rc_place") v = (Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.7f + N() * 0.2f) * Env(t, 0.002f, 0.06f) * 0.7f + Mathf.Sin(2f * Mathf.PI * 520f * t) * Env(t, 0.01f, 0.12f) * 0.2f;
                        else v = (Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 190f * t) * 0.5f) * Env(t, 0.005f, 0.09f) * 0.45f;
                        d[i] = v;
                    }
                    return Clip(name, d);
                }
                case "rc_tether_loop":
                case "rc_zone_loop":
                {
                    bool zone = name == "rc_zone_loop";
                    n = Rate * (zone ? 4 : 2);
                    d = new float[n];
                    float lp = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        lp += (N() - lp) * 0.01f;
                        float f0 = zone ? 55f : 220f;
                        // 循環點對齊：頻率是 0.5 Hz 的整數倍。
                        float v = Mathf.Sin(2f * Mathf.PI * f0 * t) * 0.4f + Mathf.Sin(2f * Mathf.PI * f0 * 1.5f * t) * 0.15f;
                        float wob = zone ? 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.5f * t) : 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 6f * t);
                        d[i] = (v * wob + lp * (zone ? 1.2f : 0.3f)) * 0.35f;
                    }
                    return Clip(name, d);
                }
                case "rc_stable":
                {
                    n = (int)(Rate * 2.2f);
                    d = new float[n];
                    float[] fs = { 523.25f, 392f, 329.63f, 261.63f };
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        float v = 0f;
                        for (int k = 0; k < fs.Length; k++)
                        {
                            float at = k * 0.18f;
                            if (t >= at) v += Mathf.Sin(2f * Mathf.PI * fs[k] * t) * Env(t - at, 0.03f, 0.9f) * 0.22f;
                        }
                        d[i] = v;
                    }
                    return Clip(name, d);
                }
                case "rc_knock":
                {
                    n = (int)(Rate * 0.9f);
                    d = new float[n];
                    float lp = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        lp += (N() - lp) * 0.1f;
                        float v = 0f;
                        foreach (var at in new[] { 0f, 0.28f, 0.52f }) if (t >= at) v += (Mathf.Sin(2f * Mathf.PI * 95f * t) + lp) * Env(t - at, 0.002f, 0.05f);
                        d[i] = v * 0.5f;
                    }
                    return Clip(name, d);
                }
                default:
                    d = new float[Rate / 10];
                    return Clip(name, d);
            }
        }
    }
}
