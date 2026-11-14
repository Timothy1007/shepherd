using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 提燈 QA 的程式合成音效（暫用，非正式音效）：蓄力、射擊、撞擊、牽引、復原、失序體、受傷、腳步、異域環境聲。
    /// 全部在執行時用正弦波／雜訊合成，不使用外部音檔。
    /// </summary>
    public sealed class LanternAudio
    {
        public const int Rate = 44100;
        private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        public AudioSource Sfx { get; private set; }
        public AudioSource Charge { get; private set; }
        public AudioSource Tether { get; private set; }
        public AudioSource Restore { get; private set; }
        public AudioSource Ambient { get; private set; }
        public int OneShotsPlayed { get; private set; }

        private static float Noise(int i) { uint x = (uint)i * 747796405u + 2891336453u; x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u; return ((x >> 22) ^ x) / (float)uint.MaxValue * 2f - 1f; }

        private static AudioClip Synth(string name, float seconds, Func<float, int, float> f)
        {
            AudioClip c;
            if (clips.TryGetValue(name, out c) && c != null) return c;
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds * Rate));
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate, i), -1f, 1f);
            c = AudioClip.Create("LA_" + name, n, 1, Rate, false);
            c.SetData(data, 0);
            c.hideFlags = HideFlags.DontSave;
            clips[name] = c;
            return c;
        }

        private static float Sin(float hz, float t) => Mathf.Sin(2f * Mathf.PI * hz * t);
        private static float Env(float t, float attack, float length) => Mathf.Clamp01(t / Mathf.Max(attack, 1e-4f)) * Mathf.Clamp01(1f - (t - attack) / Mathf.Max(length - attack, 1e-4f));

        // ---------------- 音色 ----------------

        public static AudioClip ChargeHum => Synth("ChargeHum", 1f, (t, i) => (Sin(220f, t) * 0.5f + Sin(331f, t) * 0.3f + Sin(441f, t) * 0.15f + Noise(i) * 0.04f) * 0.5f);
        public static AudioClip Shot => Synth("Shot", 0.3f, (t, i) => { float fr = Mathf.Lerp(1500f, 260f, t / 0.3f); return (Sin(fr, t) * 0.7f + Noise(i) * 0.3f * (1f - t / 0.3f)) * Env(t, 0.005f, 0.3f) * 0.8f; });
        public static AudioClip Impact => Synth("Impact", 0.55f, (t, i) => (Sin(90f, t) * Mathf.Exp(-t * 9f) * 0.8f + Noise(i) * Mathf.Exp(-t * 18f) * 0.5f + Sin(1240f, t) * Mathf.Exp(-t * 6f) * 0.18f) * 0.85f);
        public static AudioClip Grab => Synth("Grab", 0.22f, (t, i) => Sin(Mathf.Lerp(300f, 900f, t / 0.22f), t) * Env(t, 0.01f, 0.22f) * 0.5f);
        public static AudioClip TetherHum => Synth("TetherHum", 1f, (t, i) => (Sin(140f, t) * 0.6f + Sin(281f, t) * 0.25f) * (0.75f + 0.25f * Sin(6f, t)) * 0.45f);
        public static AudioClip Drop => Synth("Drop", 0.18f, (t, i) => (Sin(80f, t) * 0.7f + Noise(i) * 0.3f) * Mathf.Exp(-t * 22f) * 0.8f);
        public static AudioClip Throw => Synth("Throw", 0.35f, (t, i) => { float k = t / 0.35f; return Noise(i) * Mathf.Sin(Mathf.PI * k) * 0.45f + Sin(Mathf.Lerp(500f, 200f, k), t) * 0.15f * (1f - k); });
        public static AudioClip RestoreHum => Synth("RestoreHum", 1f, (t, i) => (Sin(523f, t) + Sin(659f, t) * 0.8f + Sin(784f, t) * 0.6f) * (0.6f + 0.4f * Sin(9f, t)) * 0.16f);
        public static AudioClip RestoreDone => Synth("RestoreDone", 1.3f, (t, i) => (Sin(523f, t) + Sin(784f, t) * 0.8f + Sin(1046f, t) * 0.6f) * Mathf.Exp(-t * 3f) * Env(t, 0.01f, 1.3f) * 0.35f);
        public static AudioClip Placed => Synth("Placed", 0.9f, (t, i) => (t < 0.18f ? Sin(659f, t) : Sin(988f, t)) * Mathf.Exp(-(t % 0.18f) * 4f - t * 2f) * 0.4f);
        public static AudioClip Toggle => Synth("Toggle", 0.08f, (t, i) => (Noise(i) * 0.5f + Sin(1800f, t) * 0.3f) * Mathf.Exp(-t * 60f));
        public static AudioClip Step => Synth("Step", 0.09f, (t, i) => (Noise(i) * 0.6f + Sin(70f, t) * 0.5f) * Mathf.Exp(-t * 45f) * 0.5f);
        public static AudioClip Hurt => Synth("Hurt", 0.4f, (t, i) => (Sin(60f, t) * 0.8f + Noise(i) * 0.5f) * Mathf.Exp(-t * 8f) * 0.9f);
        public static AudioClip AmbientDrone => Synth("Ambient", 6f, (t, i) => (Sin(41f, t) * 0.5f + Sin(61.3f, t) * 0.35f + Sin(123f, t) * 0.08f * (0.5f + 0.5f * Sin(0.21f, t)) + Noise(i) * 0.03f * (0.5f + 0.5f * Sin(0.13f, t))) * 0.5f);
        public static AudioClip EnemyDrone => Synth("EnemyDrone", 2f, (t, i) => (Sin(55f, t) * 0.5f + Sin(82.5f + 3f * Sin(0.7f, t), t) * 0.3f + Noise(i) * 0.15f * (0.5f + 0.5f * Sin(3f, t))) * 0.6f);
        public static AudioClip EnemyWarn => Synth("EnemyWarn", 0.8f, (t, i) => { float k = t / 0.8f; return (Sin(Mathf.Lerp(380f, 1500f, k * k), t) * 0.5f + Noise(i) * 0.35f * k) * Env(t, 0.05f, 0.8f) * 0.7f; });
        public static AudioClip EnemyLunge => Synth("EnemyLunge", 0.3f, (t, i) => Noise(i) * Mathf.Sin(Mathf.PI * t / 0.3f) * 0.6f);
        public static AudioClip EnemyOrb => Synth("EnemyOrb", 0.5f, (t, i) => Sin(300f + 120f * Sin(18f, t), t) * Env(t, 0.02f, 0.5f) * 0.5f);
        public static AudioClip EnemyHit => Synth("EnemyHit", 0.35f, (t, i) => { float fr = 400f + 900f * Mathf.Abs(Noise(i / 600)); return (Sin(fr, t) * 0.6f + Noise(i) * 0.3f) * Mathf.Exp(-t * 7f) * 0.8f; });
        public static AudioClip EnemyDeath => Synth("EnemyDeath", 1.4f, (t, i) => { float fr = Mathf.Lerp(700f, 60f, t / 1.4f) * (1f + 0.2f * Noise(i / 900)); return (Sin(fr, t) * 0.6f + Noise(i) * 0.25f) * Env(t, 0.02f, 1.4f) * 0.8f; });

        // ---------------- 環境聲（可無縫循環） ----------------

        /// <summary>產生可以無縫循環的長音：多算一段尾巴，交叉淡入到開頭。f 依序被呼叫（可以有狀態）。</summary>
        private static AudioClip LoopSynth(string name, float seconds, Func<float, int, float> f)
        {
            AudioClip c;
            if (clips.TryGetValue(name, out c) && c != null) return c;
            int n = Mathf.RoundToInt(seconds * Rate), m = Rate / 2;
            var raw = new float[n + m];
            for (int i = 0; i < n + m; i++) raw[i] = f(i / (float)Rate, i);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float v = raw[i];
                if (i < m) { float k = i / (float)m; v = raw[i] * k + raw[n + i] * (1f - k); }
                data[i] = Mathf.Clamp(v, -1f, 1f);
            }
            c = AudioClip.Create("LA_" + name, n, 1, Rate, false);
            c.SetData(data, 0);
            c.hideFlags = HideFlags.DontSave;
            clips[name] = c;
            return c;
        }

        /// <summary>室內的底噪：很低、很軟的空氣聲（房間不會「死寂」）。</summary>
        public static AudioClip RoomTone
        {
            get
            {
                float b = 0f, b2 = 0f;
                return LoopSynth("RoomTone", 8f, (t, i) => { b = b * 0.995f + Noise(i) * 0.02f; b2 = b2 * 0.9f + b * 0.1f; return b2 * 1.6f + Sin(50f, t) * 0.006f; });
            }
        }

        /// <summary>遠處的城市：低沉的車流聲，每隔幾秒有一台車經過（起伏）。</summary>
        public static AudioClip CityHum
        {
            get
            {
                float b = 0f, lp = 0f;
                return LoopSynth("CityHum", 16f, (t, i) =>
                {
                    b = b * 0.998f + Noise(i) * 0.03f;
                    lp = lp * 0.97f + b * 0.03f;
                    float d0 = (t - 2.5f) / 1.6f, d1 = (t - 8.0f) / 1.6f, d2 = (t - 12.6f) / 1.6f;
                    float car = Mathf.Exp(-d0 * d0) + Mathf.Exp(-d1 * d1) + Mathf.Exp(-d2 * d2);
                    float swish = Noise(i) * 0.05f * car;
                    return lp * (2.2f + 1.4f * car) + swish + Sin(38f, t) * 0.02f;
                });
            }
        }

        /// <summary>木地板的腳步：幾種輕重不同的悶響，偶爾帶一點木頭的吱聲。</summary>
        public static AudioClip WoodStep(int v)
        {
            v = Mathf.Abs(v) % 4;
            float body = 60f + v * 9f, creak = v == 3 ? 1f : 0f;
            return Synth("WoodStep" + v, 0.16f, (t, i) =>
                (Sin(body, t) * 0.55f * Mathf.Exp(-t * 30f)
                 + Noise(i + v * 977) * 0.35f * Mathf.Exp(-t * 70f)
                 + Sin(900f + 300f * Sin(13f, t), t) * 0.05f * creak * Env(t, 0.03f, 0.16f)) * 0.6f);
        }

        private int stepIndex;
        private static readonly int[] stepOrder = { 0, 1, 2, 1, 0, 2, 3, 1 };
        /// <summary>播一個腳步（輪流用不同的版本，聽起來不像機器）。</summary>
        public void PlayStep(float volume)
        {
            stepIndex++;
            Play(WoodStep(stepOrder[stepIndex % stepOrder.Length]), volume * (0.85f + 0.15f * ((stepIndex * 37) % 7) / 6f));
        }

        public AudioSource Room { get; private set; }
        public AudioSource City { get; private set; }

        /// <summary>依位置混音：街上＝城市聲大；室內＝房間底噪＋悶掉的城市聲＋異域的低鳴。</summary>
        public void Mix(bool onStreet, bool indoors, float dt)
        {
            float k = dt > 0f ? dt * 1.5f : 1f;
            if (City != null) City.volume = Mathf.MoveTowards(City.volume, onStreet ? 0.55f : indoors ? 0.14f : 0.3f, k);
            if (Room != null) Room.volume = Mathf.MoveTowards(Room.volume, indoors ? 0.45f : 0f, k);
            if (Ambient != null) Ambient.volume = Mathf.MoveTowards(Ambient.volume, onStreet ? 0.05f : 0.3f, k);
        }

        // ---------------- 播放 ----------------

        public static LanternAudio Build(Transform listenerHost)
        {
            var a = new LanternAudio();
            AudioSource Make(string n, bool loop, float vol)
            {
                var s = listenerHost.gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = loop;
                s.volume = vol;
                s.spatialBlend = 0f;
                return s;
            }
            a.Sfx = Make("Sfx", false, 1f);
            a.Charge = Make("Charge", true, 0f);
            a.Charge.clip = ChargeHum;
            a.Tether = Make("Tether", true, 0f);
            a.Tether.clip = TetherHum;
            a.Restore = Make("Restore", true, 0f);
            a.Restore.clip = RestoreHum;
            a.Ambient = Make("Ambient", true, 0.35f);
            a.Ambient.clip = AmbientDrone;
            a.Room = Make("Room", true, 0f);
            a.Room.clip = RoomTone;
            a.City = Make("City", true, 0.3f);
            a.City.clip = CityHum;
            if (Application.isPlaying)
            {
                a.Charge.Play();
                a.Tether.Play();
                a.Restore.Play();
                a.Ambient.Play();
                a.Room.Play();
                a.City.Play();
            }
            return a;
        }

        public void Play(AudioClip c, float volume = 1f)
        {
            OneShotsPlayed++;
            if (Sfx != null && Application.isPlaying) Sfx.PlayOneShot(c, volume);
        }

        /// <summary>持續音（蓄力、牽引、復原）：以音量淡入淡出，pitch 表示強度。</summary>
        public static void Loop(AudioSource s, bool on, float pitch, float volume, float dt)
        {
            if (s == null) return;
            s.volume = Mathf.MoveTowards(s.volume, on ? volume : 0f, dt * 4f);
            s.pitch = pitch;
        }

        public static AudioSource Spatial(GameObject host, AudioClip clip, bool loop, float volume)
        {
            var s = host.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = loop;
            s.volume = volume;
            s.spatialBlend = 1f;
            s.minDistance = 1.5f;
            s.maxDistance = 22f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.playOnAwake = false;
            if (loop && Application.isPlaying) s.Play();
            return s;
        }
    }
}
