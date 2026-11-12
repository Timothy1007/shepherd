using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// v0.4 記憶殘響的場景演出：半透明的簡單剪影（膠囊＋頭，不是正式人物模型）＋字幕＋程式產生的占位低語。
    /// 不鎖鏡頭、不擋路（無碰撞體）；剪影只在自己的字幕播放時淡入，字幕結束淡出後移除。
    /// 玩家走離（離開該房間或超過距離）、按 Tab、進入戰鬥或跳轉時立即結束，剩下的句子一併取消（台詞已記入日誌，不會錯過線索）。
    /// </summary>
    public sealed class Case01EchoPlayer
    {
        public const float WalkAwayDistance = 7f;
        public const float FadeIn = 0.5f;
        public const float FadeOut = 0.8f;
        public const float MaxAlpha = 0.3f;

        private sealed class Figure
        {
            public Transform Root;
            public Material Mat;
            public float Phase;
        }

        public struct Spot
        {
            public Case01Room Room;
            public Vector3 Anchor;
            public Vector3[] Figures;
            public float[] Yaws;
        }

        private readonly Case01Hud hud;
        private readonly Case01Audio sfx;
        private Transform root;
        private Case01Palette palette;
        private readonly List<Figure> figures = new List<Figure>();
        private AudioSource voice;
        private string active;
        private Spot spot;
        private float alpha;
        private string lastLine;
        private bool started;

        public float EchoVolume = 0.5f;
        public bool ReduceMotion;

        public Case01EchoPlayer(Case01Hud hud, Case01Audio sfx)
        {
            this.hud = hud;
            this.sfx = sfx;
        }

        public string Active { get { return active; } }
        public int FigureCount { get { return figures.Count; } }
        public float Alpha { get { return alpha; } }
        public bool VoicePlaying { get { return voice != null && voice.isPlaying; } }
        /// <summary>本次 QA 實際開始播放過的殘響（測試用）。</summary>
        public readonly List<string> Played = new List<string>();

        /// <summary>殘響的位置：剪影站在玩家進門後看得到的地方（不硬轉鏡頭）。</summary>
        public static Spot SpotOf(string id)
        {
            float y = Case01HouseBuilder.FloorHeight;
            switch (id)
            {
                case Case01Echoes.Entrance:
                    return new Spot
                    {
                        Room = Case01Room.Entrance, Anchor = new Vector3(6.9f, 0f, 3.9f),
                        Figures = new[] { new Vector3(6.1f, 0f, 3.9f), new Vector3(7.7f, 0f, 4.2f) }, Yaws = new[] { 75f, -100f },
                    };
                case Case01Echoes.Dining:
                    return new Spot
                    {
                        Room = Case01Room.Dining, Anchor = new Vector3(7.6f, 0f, 7.8f),
                        Figures = new[] { new Vector3(7.1f, 0f, 8.65f), new Vector3(8.75f, 0f, 8.7f) }, Yaws = new[] { 160f, -120f },
                    };
                case Case01Echoes.Study:
                    return new Spot
                    {
                        Room = Case01Room.Study, Anchor = new Vector3(7.5f, y, 8.4f),
                        Figures = new[] { new Vector3(7.2f, y, 8.85f) }, Yaws = new[] { 10f },
                    };
                case Case01Echoes.Landing:
                    // 只有遠處的聲音（沒有剪影）。
                    return new Spot { Room = Case01Room.Landing, Anchor = new Vector3(9.3f, y, 0.4f), Figures = new Vector3[0], Yaws = new float[0] };
                default:
                    return new Spot { Figures = new Vector3[0], Yaws = new float[0] };
            }
        }

        /// <summary>v0.5：依格局覆寫的位置（方案 A／B 的玄關與餐廳家具位置不同）。</summary>
        public static Spot SpotOf(string id, Case01HouseLayout layout)
        {
            Spot s;
            if (layout != null && layout.EchoSpots.TryGetValue(id, out s)) return s;
            return SpotOf(id);
        }

        /// <summary>開始一段殘響：字幕排入隊列；剪影與聲音跟著字幕。</summary>
        public void Play(string id, Transform parent, Case01Palette p, Case01HouseLayout layout = null)
        {
            Stop();
            var lines = Case01Texts.EchoLines(id);
            if (lines.Length == 0) return;
            active = id;
            spot = SpotOf(id, layout);
            palette = p;
            started = false;
            lastLine = null;
            alpha = 0f;
            foreach (var l in lines) hud.EnqueueLine(l[0], l[1], id);
            root = ArenaArt.Group("MemoryEcho_" + id, parent, Vector3.zero);
            for (int i = 0; i < spot.Figures.Length; i++) figures.Add(BuildFigure(root, spot.Figures[i], spot.Yaws[i], i));
            var vg = new GameObject("EchoVoice");
            vg.transform.SetParent(root, false);
            vg.transform.position = spot.Anchor + Vector3.up * 1.5f;
            if (sfx != null)
            {
                voice = sfx.AddSpatialLoop(vg.transform, sfx.Murmur, 12f);
                voice.loop = false;
            }
            SetAlpha(0f);
        }

        private Figure BuildFigure(Transform g, Vector3 pos, float yaw, int i)
        {
            var f = new Figure { Phase = i * 1.7f };
            var color = i == 0 ? new Color(0.86f, 0.9f, 1f, 0f) : new Color(0.95f, 0.92f, 0.86f, 0f);
            f.Mat = palette.Own(ArenaArt.FadeMat(color, new Color(0.16f, 0.17f, 0.2f)));
            // 殘像：沒有高光與反射，看起來是一層淡淡的光影而不是實心人偶。
            ShepherdShading.SetSmoothness(f.Mat, 0f);
            f.Mat.SetFloat("_Metallic", 0f);
            f.Mat.SetFloat("_SpecularHighlights", 0f);
            f.Mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            f.Mat.SetFloat("_GlossyReflections", 0f);
            f.Mat.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            f.Root = ArenaArt.Group("EchoFigure_" + i, g, pos);
            f.Root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float h = i == 0 ? 1f : 0.94f;
            ArenaArt.Prim(PrimitiveType.Capsule, f.Root, new Vector3(0f, 0.8f * h, 0f), new Vector3(0.42f, 0.78f * h, 0.3f), f.Mat, null, "Body", false, false);
            ArenaArt.Prim(PrimitiveType.Sphere, f.Root, new Vector3(0f, 1.68f * h, 0f), new Vector3(0.22f, 0.25f, 0.22f), f.Mat, null, "Head", false, false);
            return f;
        }

        private void SetAlpha(float a)
        {
            alpha = a;
            foreach (var f in figures)
            {
                if (f.Mat == null) continue;
                // 最多三成透明度（殘像，不是實體人物）。
                var c = f.Mat.color;
                c.a = a * MaxAlpha;
                f.Mat.color = c;
            }
        }

        /// <summary>每影格：依字幕狀態淡入淡出；走開即結束。</summary>
        public void Tick(float dt, Vector3 player, Case01Room room, bool allowed, float clock)
        {
            if (active == null) return;
            if (!allowed)
            {
                Stop();
                return;
            }
            bool inRoom = room == spot.Room || room == Case01Room.None;
            float dist = Vector3.Distance(new Vector3(player.x, spot.Anchor.y, player.z), spot.Anchor);
            if (started && (!inRoom || dist > WalkAwayDistance))
            {
                // 正常移動就能略過：剩下的句子取消，剪影快速淡出。
                hud.DropGroup(active);
            }
            bool showing = hud.CurrentGroup == active;
            bool pending = hud.HasPendingGroup(active);
            if (showing)
            {
                started = true;
                if (hud.MonologueCurrent != lastLine)
                {
                    lastLine = hud.MonologueCurrent;
                    if (voice != null && voice.isActiveAndEnabled && !sfx.Paused)
                    {
                        voice.volume = EchoVolume * 0.6f;
                        voice.pitch = lastLine != null && lastLine.Length % 2 == 0 ? 1.05f : 0.92f;
                        voice.Stop();
                        voice.Play();
                    }
                }
            }
            float target = showing && hud.MonologueVisible ? 1f : 0f;
            float rate = target > alpha ? 1f / FadeIn : 1f / FadeOut;
            SetAlpha(Mathf.MoveTowards(alpha, target, dt * rate));
            // 輕微的殘像起伏（降低動態時不動）。
            if (!ReduceMotion)
                foreach (var f in figures)
                    if (f.Root != null) f.Root.localScale = Vector3.one * (1f + 0.015f * Mathf.Sin(clock * 2.2f + f.Phase));
            // 這段的字幕都播完（或被取消）且剪影已淡出：移除。
            if (!showing && !pending && (!started || alpha <= 0.001f)) Stop();
        }

        /// <summary>立即結束（戰鬥、跳轉、結束、略過）。</summary>
        public void Stop()
        {
            if (active != null)
            {
                if (started) Played.Add(active);
                hud.DropGroup(active);
            }
            if (voice != null) voice.Stop();
            voice = null;
            if (root != null) ArenaArt.Kill(root.gameObject);
            root = null;
            figures.Clear();
            active = null;
            alpha = 0f;
            started = false;
        }
    }
}
