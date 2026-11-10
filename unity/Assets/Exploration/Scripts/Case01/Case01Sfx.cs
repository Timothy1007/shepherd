using System.Collections.Generic;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// Phase 1.8：程式合成的音效與環境聲（全部在本專案內即時生成，沒有外部素材、沒有授權問題）。
    /// 同一個名稱只生成一次（重建房屋不會重複生成或疊加）。音色是「可辨認的佔位級」：
    /// 門軸吱呀、門閂、木門撞擊、門鎖晃動、木地板／磁磚／地毯／樓梯腳步、壁爐劈啪、時鐘滴答、冰箱嗡聲、水龍頭水流、
    /// 屋外車流與風、隔壁模糊的收音機、書房低頻、翻頁、檯燈開關。
    /// </summary>
    public static class Case01Sfx
    {
        public const int Rate = 22050;
        private static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        public static readonly string[] All =
        {
            "door_creak", "door_creak_low", "door_close", "door_latch", "door_locked", "step_wood", "step_tile", "step_rug", "step_stair", "stair_creak",
            "fire_loop", "clock_loop", "fridge_loop", "water_loop", "street_loop", "wind_loop", "radio_loop", "hum_loop", "draft_loop", "page", "switch", "touch", "settle",
        };

        public static AudioClip Get(string name)
        {
            AudioClip c;
            if (cache.TryGetValue(name, out c) && c != null) return c;
            c = Make(name);
            cache[name] = c;
            return c;
        }

        /// <summary>片段的均方根（測試確認不是無聲佔位）。</summary>
        public static float Rms(AudioClip c)
        {
            var d = new float[c.samples * c.channels];
            c.GetData(d, 0);
            double s = 0;
            foreach (var v in d) s += v * v;
            return Mathf.Sqrt((float)(s / Mathf.Max(1, d.Length)));
        }

        private static AudioClip Clip(string name, float[] d)
        {
            var c = AudioClip.Create("Case01Sfx_" + name, d.Length, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        private static float Env(float t, float attack, float decay) { return t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay); }

        private static AudioClip Make(string name)
        {
            var rng = new System.Random(name.GetHashCode());
            System.Func<float> N = () => (float)rng.NextDouble() * 2f - 1f;
            float[] d;
            int n;
            switch (name)
            {
                case "door_creak":
                case "door_creak_low":
                {
                    float baseF = name == "door_creak" ? 520f : 300f;
                    n = (int)(Rate * 0.75f);
                    d = new float[n];
                    float ph = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        float f = baseF + 140f * Mathf.Sin(t * 7f) + 60f * Mathf.Sin(t * 23f);
                        ph += f / Rate;
                        // 門軸的「黏滑」：不規則的脈衝串。
                        float stick = Mathf.Pow(Mathf.Abs(Mathf.Sin(ph * Mathf.PI * 2f)), 9f);
                        d[i] = (stick * 0.6f + N() * 0.05f) * Mathf.Sin(Mathf.PI * t / 0.75f) * 0.5f;
                    }
                    return Clip(name, d);
                }
                case "door_close":
                {
                    n = (int)(Rate * 0.45f);
                    d = new float[n];
                    float lp = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        lp += (N() - lp) * 0.08f;
                        d[i] = (Mathf.Sin(2f * Mathf.PI * 85f * t) * 0.8f + lp * 1.6f) * Env(t, 0.004f, 0.09f) * 0.8f;
                    }
                    return Clip(name, d);
                }
                case "door_latch":
                case "switch":
                case "touch":
                {
                    float f = name == "door_latch" ? 1900f : name == "switch" ? 3200f : 1200f;
                    n = (int)(Rate * 0.12f);
                    d = new float[n];
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        d[i] = (Mathf.Sin(2f * Mathf.PI * f * t) * 0.6f + N() * 0.4f) * Env(t, 0.001f, name == "touch" ? 0.03f : 0.015f) * (name == "touch" ? 0.3f : 0.6f);
                    }
                    return Clip(name, d);
                }
                case "door_locked":
                {
                    n = (int)(Rate * 0.5f);
                    d = new float[n];
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        float hit = 0f;
                        foreach (var at in new[] { 0f, 0.11f, 0.2f, 0.32f }) if (t >= at) hit += Env(t - at, 0.001f, 0.025f);
                        d[i] = (Mathf.Sin(2f * Mathf.PI * 1500f * t) * 0.4f + N() * 0.5f) * Mathf.Min(1f, hit) * 0.55f;
                    }
                    return Clip(name, d);
                }
                case "step_wood":
                case "step_tile":
                case "step_rug":
                case "step_stair":
                {
                    n = (int)(Rate * 0.16f);
                    d = new float[n];
                    float lp = 0f;
                    float body = name == "step_tile" ? 210f : name == "step_rug" ? 70f : 120f;
                    float bright = name == "step_tile" ? 0.6f : name == "step_rug" ? 0.03f : 0.18f;
                    float decay = name == "step_tile" ? 0.02f : name == "step_rug" ? 0.035f : 0.045f;
                    float gain = name == "step_rug" ? 0.25f : 0.55f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        lp += (N() - lp) * (name == "step_rug" ? 0.05f : 0.25f);
                        d[i] = (Mathf.Sin(2f * Mathf.PI * body * t) * 0.7f + lp * 0.8f + N() * bright) * Env(t, 0.003f, decay) * gain;
                    }
                    return Clip(name, d);
                }
                case "stair_creak":
                {
                    n = (int)(Rate * 0.4f);
                    d = new float[n];
                    float ph = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        ph += (180f + 60f * Mathf.Sin(t * 11f)) / Rate;
                        d[i] = Mathf.Pow(Mathf.Abs(Mathf.Sin(ph * Mathf.PI * 2f)), 6f) * Mathf.Sin(Mathf.PI * t / 0.4f) * 0.35f;
                    }
                    return Clip(name, d);
                }
                case "fire_loop":
                {
                    n = Rate * 4;
                    d = new float[n];
                    float lp = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        lp += (N() - lp) * 0.02f;
                        d[i] = lp * 0.5f;
                    }
                    for (int k = 0; k < 70; k++)
                    {
                        int at = rng.Next(n - 400);
                        float g = 0.2f + (float)rng.NextDouble() * 0.5f;
                        for (int j = 0; j < 300; j++) d[at + j] += N() * g * Mathf.Exp(-j / 40f);
                    }
                    return Clip(name, d);
                }
                case "clock_loop":
                {
                    n = Rate * 2;
                    d = new float[n];
                    for (int tick = 0; tick < 2; tick++)
                    {
                        int at = tick * Rate;
                        float f = tick == 0 ? 2600f : 2100f;
                        for (int j = 0; j < 600; j++)
                        {
                            float t = j / (float)Rate;
                            d[at + j] = (Mathf.Sin(2f * Mathf.PI * f * t) * 0.5f + N() * 0.3f) * Env(t, 0.0005f, 0.006f) * 0.5f;
                        }
                    }
                    return Clip(name, d);
                }
                case "fridge_loop":
                case "hum_loop":
                {
                    n = Rate * 2;
                    d = new float[n];
                    float f = name == "fridge_loop" ? 100f : 48f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        d[i] = (Mathf.Sin(2f * Mathf.PI * f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.2f + Mathf.Sin(2f * Mathf.PI * (f * 3f + 1f) * t) * 0.08f) *
                               (name == "hum_loop" ? 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.5f * t) : 1f) * 0.25f;
                    }
                    return Clip(name, d);
                }
                case "water_loop":
                case "draft_loop":
                case "wind_loop":
                {
                    n = Rate * 3;
                    d = new float[n];
                    float lp = 0f, lp2 = 0f;
                    float a = name == "water_loop" ? 0.5f : name == "draft_loop" ? 0.04f : 0.015f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        lp += (N() - lp) * a;
                        lp2 += (lp - lp2) * a;
                        float sw = name == "water_loop" ? 1f : 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * t / 3f);
                        d[i] = (lp - lp2 * 0.5f) * sw * (name == "water_loop" ? 0.35f : 1.4f);
                    }
                    return Clip(name, d);
                }
                case "street_loop":
                {
                    n = Rate * 8;
                    d = new float[n];
                    float lp = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        lp += (N() - lp) * 0.01f;
                        // 遠處車流的低鳴＋一輛車經過（第 2～5 秒）。
                        float car = Mathf.Exp(-Mathf.Pow((t - 3.5f) / 1.2f, 2f));
                        d[i] = lp * (0.6f + 1.6f * car) + Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.05f * car;
                    }
                    return Clip(name, d);
                }
                case "radio_loop":
                {
                    n = Rate * 4;
                    d = new float[n];
                    float lp = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        // 隔著牆的人聲／音樂：只剩模糊的起伏（低通）。
                        float syll = Mathf.Abs(Mathf.Sin(t * 5.3f)) * (0.5f + 0.5f * Mathf.Sin(t * 1.3f));
                        lp += ((Mathf.Sin(2f * Mathf.PI * (180f + 40f * Mathf.Sin(t * 2f)) * t) + N() * 0.3f) * syll - lp) * 0.05f;
                        d[i] = lp * 0.6f;
                    }
                    return Clip(name, d);
                }
                case "page":
                {
                    n = (int)(Rate * 0.35f);
                    d = new float[n];
                    float lp = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        lp += (N() - lp) * 0.5f;
                        d[i] = lp * Mathf.Sin(Mathf.PI * t / 0.35f) * (0.6f + 0.4f * Mathf.Sin(t * 60f)) * 0.4f;
                    }
                    return Clip(name, d);
                }
                default: // settle：房子遠處的一聲低沉沉降（空間異變後）。
                {
                    n = Rate;
                    d = new float[n];
                    float lp = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        lp += (N() - lp) * 0.01f;
                        d[i] = (Mathf.Sin(2f * Mathf.PI * 42f * t) * 0.6f + lp * 2f) * Env(t, 0.02f, 0.35f) * 0.6f;
                    }
                    return Clip(name, d);
                }
            }
        }
    }
}
