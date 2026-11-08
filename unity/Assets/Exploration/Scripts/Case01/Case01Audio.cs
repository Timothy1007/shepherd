using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// Case01 的聲音：全部在程式中合成（可重現、無第三方素材），分成環境／效果／音樂三組音量。
    /// 所有 AudioSource 都掛在 QA 物件底下：離開 Play、銷毀場景或視窗失去焦點時停止／暫停，不會在背景偷偷發聲。
    /// 靜音（音量 0）不影響任何流程計時。
    /// </summary>
    public sealed class Case01Audio
    {
        public const int Rate = 22050;

        public AudioSource Ambient { get; private set; }
        public AudioSource Effects { get; private set; }
        public AudioSource Music { get; private set; }

        public AudioClip AmbientLoop { get; private set; }
        public AudioClip Rustle { get; private set; }
        public AudioClip Whoosh { get; private set; }
        public AudioClip Assemble { get; private set; }
        public AudioClip Lock { get; private set; }
        public AudioClip Sting { get; private set; }
        public AudioClip Settle { get; private set; }
        /// <summary>v0.4 失序門的輕微電流雜音（循環）。</summary>
        public AudioClip Static { get; private set; }
        /// <summary>v0.4 記憶殘響的占位低語（程式產生，不是角色配音）。</summary>
        public AudioClip Murmur { get; private set; }

        /// <summary>v0.4 失序門雜音的獨立音量。</summary>
        public float PortalVolume = 0.35f;
        public float AmbientVolume = 0.5f;
        public float EffectsVolume = 0.8f;
        public float MusicVolume = 0.7f;
        public float ScareIntensity = 1f;

        private readonly List<AudioSource> sources = new List<AudioSource>();
        private readonly List<AudioClip> clips = new List<AudioClip>();
        private bool paused;

        /// <summary>最近一次播放的效果音名稱（測試用：靜音時也會記錄）。</summary>
        public readonly List<string> Played = new List<string>();

        public Case01Audio(Transform parent)
        {
            Ambient = Source(parent, "Case01Ambient", true);
            Effects = Source(parent, "Case01Effects", false);
            Music = Source(parent, "Case01Music", false);
            AmbientLoop = Make("Case01AmbientLoop", 6f, AmbientSample);
            Rustle = Make("Case01Rustle", 1.2f, RustleSample);
            Whoosh = Make("Case01Whoosh", 1.5f, WhooshSample);
            Assemble = Make("Case01Assemble", 1.7f, AssembleSample);
            Lock = Make("Case01Lock", 1.1f, LockSample);
            Sting = Make("Case01Sting", 3.8f, StingSample);
            Settle = Make("Case01Settle", 1.4f, SettleSample);
            Static = Make("Case01PortalStatic", 2f, Case01Glitch.StaticSample);
            Murmur = Make("Case01EchoMurmur", 2.2f, MurmurSample);
            Ambient.clip = AmbientLoop;
        }

        /// <summary>場景中的 3D 循環音源（失序門、記憶殘響）：登記後同樣受失焦暫停與 StopAll 管理。</summary>
        public AudioSource AddSpatialLoop(Transform at, AudioClip clip, float maxDistance)
        {
            var s = at.gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = true;
            s.clip = clip;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 0.8f;
            s.maxDistance = maxDistance;
            s.volume = 0f;
            sources.Add(s);
            return s;
        }

        public bool Paused { get { return paused; } }

        /// <summary>依目標音量開關循環音源（0＝停止）。</summary>
        public void DriveLoop(AudioSource s, float volume)
        {
            if (s == null) return;
            s.volume = volume;
            if (!s.isActiveAndEnabled) return;
            if (volume > 0.001f && !s.isPlaying && !paused) s.Play();
            else if (volume <= 0.001f && s.isPlaying) s.Stop();
        }

        private AudioSource Source(Transform parent, string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            sources.Add(s);
            return s;
        }

        private AudioClip Make(string name, float seconds, System.Func<float, float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate, seconds), -1f, 1f);
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(data, 0);
            clips.Add(c);
            return c;
        }

        // ---------------- 播放 ----------------

        public float EffectGain { get { return EffectsVolume * Mathf.Lerp(0.35f, 1f, ScareIntensity); } }

        public void PlayEffect(AudioClip clip, float gain = 1f)
        {
            Played.Add(clip.name);
            if (paused || !Effects.isActiveAndEnabled) return;
            Effects.PlayOneShot(clip, EffectGain * gain);
        }

        public void PlayMusic(AudioClip clip)
        {
            Played.Add(clip.name);
            if (paused || !Music.isActiveAndEnabled) return;
            Music.Stop();
            Music.clip = clip;
            Music.volume = MusicVolume;
            Music.Play();
        }

        /// <summary>夾層／中庭的低頻環境聲；其他地方安靜。</summary>
        public void SetAmbient(bool on)
        {
            Ambient.volume = AmbientVolume * 0.6f;
            if (!Ambient.isActiveAndEnabled) return;
            if (on && !Ambient.isPlaying && !paused) Ambient.Play();
            else if (!on && Ambient.isPlaying) Ambient.Stop();
        }

        public void Pause(bool pause)
        {
            paused = pause;
            foreach (var s in sources)
            {
                if (s == null) continue;
                if (pause) s.Pause();
                else s.UnPause();
            }
        }

        public void StopAll()
        {
            foreach (var s in sources) if (s != null) s.Stop();
            sources.RemoveAll(s => s == null);
        }

        public void Dispose()
        {
            StopAll();
            foreach (var c in clips) if (c != null) ArenaArt.Kill(c);
            clips.Clear();
        }

        // ---------------- 合成 ----------------

        private static float Noise(float t, int seed)
        {
            unchecked
            {
                int i = (int)(t * Rate) * 1103515245 + seed * 12345;
                i ^= i >> 13;
                return ((i & 0xffff) / 32768f) - 1f;
            }
        }

        private static float Env(float t, float len, float attack, float release)
        {
            float a = Mathf.Clamp01(t / attack);
            float r = Mathf.Clamp01((len - t) / release);
            return a * r;
        }

        private static float AmbientSample(float t, float len)
        {
            // 低頻嗡鳴＋緩慢起伏的雜訊（頭尾平滑，可循環）。
            float drone = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * 82.5f * t) * 0.12f;
            float swell = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * t / len);
            float hiss = Noise(t, 3) * 0.04f;
            return (drone * swell + hiss) * 0.5f;
        }

        private static float RustleSample(float t, float len)
        {
            // 紙頁翻動：短促的雜訊顆粒。
            float grain = Mathf.Abs(Mathf.Sin(t * 47f)) * Mathf.Abs(Mathf.Sin(t * 13f));
            return Noise(t, 7) * grain * Env(t, len, 0.15f, 0.4f) * 0.5f;
        }

        private static float WhooshSample(float t, float len)
        {
            float k = t / len;
            float band = Noise(t, 11) * (0.3f + 0.7f * k);
            float tone = Mathf.Sin(2f * Mathf.PI * (120f + 220f * k) * t) * 0.15f;
            return (band * 0.35f + tone) * Env(t, len, 0.5f, 0.3f);
        }

        private static float AssembleSample(float t, float len)
        {
            float crackle = Noise(t, 13) * (Noise(t * 0.02f, 17) > 0.6f ? 0.8f : 0.1f);
            float rise = Mathf.Sin(2f * Mathf.PI * (80f + 60f * t) * t) * 0.25f;
            return (crackle * 0.3f + rise) * Env(t, len, 0.3f, 0.25f);
        }

        private static float LockSample(float t, float len)
        {
            // 低頻結構扣合聲：短促的打擊＋衰減的低音。
            float hit = Mathf.Sin(2f * Mathf.PI * 46f * t) * Mathf.Exp(-t * 5f);
            float click = Noise(t, 19) * Mathf.Exp(-t * 60f) * 0.6f;
            return (hit * 0.9f + click) * 0.8f;
        }

        private static float StingSample(float t, float len)
        {
            // 短音樂轉折：A 小調和弦慢慢膨脹，最後落在不協和的低音上。
            float[] f = { 110f, 130.81f, 164.81f, 246.94f };
            float s = 0f;
            for (int i = 0; i < f.Length; i++)
            {
                float detune = 1f + 0.003f * i;
                s += (Mathf.Sin(2f * Mathf.PI * f[i] * detune * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * f[i] * 2f * t)) * 0.18f;
            }
            if (t > len * 0.6f) s += Mathf.Sin(2f * Mathf.PI * 103.83f * t) * 0.25f * Mathf.Clamp01((t - len * 0.6f) * 4f);
            return s * Env(t, len, 1.8f, 0.8f);
        }

        private static float MurmurSample(float t, float len)
        {
            // 占位低語：像隔著牆聽到的說話起伏（兩個共振峰＋音節包絡），聽不出字，只表示「有人在說話」。
            float syll = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 4.3f * t + Mathf.Sin(t * 7f));
            float pitch = 180f + 25f * Mathf.Sin(2f * Mathf.PI * 0.8f * t);
            float voice = Mathf.Sin(2f * Mathf.PI * pitch * t) * 0.4f + Mathf.Sin(2f * Mathf.PI * pitch * 2.6f * t) * 0.15f;
            float breath = Noise(t, 23) * 0.06f;
            return (voice * syll + breath) * Env(t, len, 0.25f, 0.6f) * 0.35f;
        }

        private static float SettleSample(float t, float len)
        {
            // 校正提交時的低頻接合聲（v0.3 核校正使用）。
            float hum = Mathf.Sin(2f * Mathf.PI * 65f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 97.5f * t) * 0.25f;
            return hum * Env(t, len, 0.05f, 1.0f) * 0.7f;
        }
    }
}
