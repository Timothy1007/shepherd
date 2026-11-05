using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Battle.UI.Arena
{
    /// <summary>
    /// 3D 戰鬥的短暫視覺效果（彈道、粒子、閃光、震動、浮動數字）。
    /// 效果只負責「怎麼演」，顯示的數字一律由呼叫端從 BattleEvent 帶入，不自行計算。
    /// 以 Tick(dt) 推進；FinishAll() 立即結束全部效果（略過動畫與自動化測試使用）。
    /// </summary>
    public sealed class ArenaFx
    {
        private sealed class Item
        {
            public float Time;
            public float Duration;
            public Action<float> Step;
            public Action Done;
        }

        private readonly List<Item> items = new List<Item>();
        private readonly Transform worldRoot;
        private readonly RectTransform floaterLayer;
        private readonly Func<Vector3, Vector2?> worldToHud;

        public ArenaFx(Transform worldRoot, RectTransform floaterLayer, Func<Vector3, Vector2?> worldToHud)
        {
            this.worldRoot = worldRoot;
            this.floaterLayer = floaterLayer;
            this.worldToHud = worldToHud;
        }

        public int ActiveCount { get { return items.Count; } }

        /// <summary>最近一次產生的浮動文字（測試用）。</summary>
        public readonly List<string> FloaterHistory = new List<string>();

        public void Add(float duration, Action<float> step, Action done = null)
        {
            var it = new Item { Duration = Mathf.Max(0.0001f, duration), Step = step, Done = done };
            items.Add(it);
            step(0f);
        }

        public void Tick(float dt)
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (i >= items.Count) continue;
                var it = items[i];
                it.Time += dt;
                float k = Mathf.Clamp01(it.Time / it.Duration);
                it.Step(k);
                if (k >= 1f)
                {
                    items.Remove(it);
                    if (it.Done != null) it.Done();
                }
            }
        }

        public void FinishAll()
        {
            // 完成時可能再產生新效果，最多處理幾輪。
            for (int guard = 0; guard < 8 && items.Count > 0; guard++)
            {
                var copy = items.ToArray();
                items.Clear();
                foreach (var it in copy)
                {
                    it.Step(1f);
                    if (it.Done != null) it.Done();
                }
            }
            items.Clear();
        }

        // ---------------- 效果 ----------------

        /// <summary>浮在世界座標上方、向上飄並淡出的數字或文字。</summary>
        public void Floater(Vector3 world, string text, Color color, int size = 30, float duration = 1.2f)
        {
            FloaterHistory.Add(text);
            var t = CombatLabUi.Label("Floater", floaterLayer, text, size, TextAnchor.MiddleCenter, color);
            t.fontStyle = FontStyle.Bold;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var shadow = t.gameObject.AddComponent<Outline>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(2f, -2f);
            t.rectTransform.sizeDelta = new Vector2(320f, 50f);
            float drift = (FloaterHistory.Count % 3 - 1) * 0.25f;
            Add(duration, k =>
            {
                if (t == null) return;
                var p = worldToHud(world + new Vector3(drift * k, 0.9f * k, 0f));
                if (p.HasValue) t.rectTransform.anchoredPosition = p.Value;
                float a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
                t.color = new Color(color.r, color.g, color.b, a);
                float s = k < 0.12f ? Mathf.Lerp(0.6f, 1.15f, k / 0.12f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((k - 0.12f) * 4f));
                t.rectTransform.localScale = Vector3.one * s;
            }, () => { if (t != null) ArenaArt.Kill(t.gameObject); });
        }

        /// <summary>從施法者飛向目標的光球（附拖尾），抵達時爆出粒子。</summary>
        public void Projectile(Vector3 from, Vector3 to, Color color, float duration)
        {
            var mat = ArenaArt.Mat(color, 0.9f, color * 3f);
            var ball = ArenaArt.Prim(PrimitiveType.Sphere, worldRoot, from, Vector3.one * 0.32f, mat, null, "Projectile", false, false);
            var trail = ball.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.startWidth = 0.22f;
            trail.endWidth = 0f;
            trail.material = ParticleMaterial(color);
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            var light = ArenaArt.PointLight(ball, Vector3.zero, color, 4f, 2.2f, false);
            Add(duration, k =>
            {
                if (ball == null) return;
                var p = Vector3.Lerp(from, to, k);
                p.y += Mathf.Sin(k * Mathf.PI) * 1.2f;
                ball.position = p;
                light.intensity = 2.2f;
            }, () =>
            {
                if (ball != null) ArenaArt.Kill(ball.gameObject);
                Burst(to, color, 26, 3.2f);
            });
        }

        /// <summary>原地發光（自身效果：擬態錨定、回復、校正等）。</summary>
        public void Glow(Vector3 world, Color color, float radius, float duration)
        {
            var mat = ArenaArt.FadeMat(new Color(color.r, color.g, color.b, 0.5f), color * 1.5f);
            var s = ArenaArt.Prim(PrimitiveType.Sphere, worldRoot, world, Vector3.one * 0.2f, mat, null, "Glow", false, false);
            var light = ArenaArt.PointLight(s, Vector3.zero, color, radius * 2.5f, 2.5f, false);
            Add(duration, k =>
            {
                if (s == null) return;
                s.localScale = Vector3.one * Mathf.Lerp(0.2f, radius, Mathf.Sqrt(k));
                var c = mat.color;
                c.a = 0.5f * (1f - k);
                mat.color = c;
                light.intensity = 2.5f * (1f - k);
            }, () => { if (s != null) ArenaArt.Kill(s.gameObject); });
        }

        /// <summary>一次性的粒子爆發。</summary>
        public void Burst(Vector3 world, Color color, int count, float speed)
        {
            var go = new GameObject("Burst");
            go.transform.SetParent(worldRoot, false);
            go.transform.position = world;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = 0.7f;
            main.startSpeed = speed;
            main.startSize = 0.22f;
            main.startColor = color;
            main.gravityModifier = 0.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ParticleMaterial(color);
            ps.Play();
            Add(1.2f, k => { }, () => { if (go != null) ArenaArt.Kill(go); });
        }

        /// <summary>以 MaterialPropertyBlock 把模型短暫染色（受擊閃白、受傷閃紅）。</summary>
        public void Tint(Renderer[] renderers, Color flash, float duration)
        {
            var originals = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                originals[i] = renderers[i] != null && renderers[i].sharedMaterial != null ? renderers[i].sharedMaterial.color : Color.white;
            var block = new MaterialPropertyBlock();
            Add(duration, k =>
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    renderers[i].GetPropertyBlock(block);
                    block.SetColor("_Color", Color.Lerp(flash, originals[i], k));
                    renderers[i].SetPropertyBlock(block);
                }
            }, () =>
            {
                foreach (var r in renderers) if (r != null) r.SetPropertyBlock(null);
            });
        }

        /// <summary>原地震動（受擊）。</summary>
        public void Shake(Transform t, float amplitude, float duration)
        {
            var basePos = t.localPosition;
            int seed = t.GetInstanceID();
            Add(duration, k =>
            {
                if (t == null) return;
                float a = amplitude * (1f - k);
                float x = Mathf.Sin((k * 40f) + seed) * a;
                float z = Mathf.Cos((k * 33f) + seed) * a * 0.6f;
                t.localPosition = basePos + new Vector3(x, 0f, z);
            }, () => { if (t != null) t.localPosition = basePos; });
        }

        /// <summary>朝某方向撲出再收回（敵人攻擊）。</summary>
        public void Lunge(Transform t, Vector3 worldDir, float distance, float duration)
        {
            var basePos = t.position;
            Add(duration, k =>
            {
                if (t == null) return;
                float f = k < 0.35f ? Mathf.SmoothStep(0f, 1f, k / 0.35f) : Mathf.SmoothStep(1f, 0f, (k - 0.35f) / 0.65f);
                t.position = basePos + worldDir.normalized * distance * f;
            }, () => { if (t != null) t.position = basePos; });
        }

        /// <summary>螢幕邊緣閃光（受傷紅、護盾藍）。</summary>
        public void ScreenFlash(Image img, Color color, float duration)
        {
            Add(duration, k =>
            {
                if (img == null) return;
                img.color = new Color(color.r, color.g, color.b, color.a * (1f - k));
            }, () => { if (img != null) img.color = new Color(color.r, color.g, color.b, 0f); });
        }

        private static readonly Dictionary<Color, Material> particleMats = new Dictionary<Color, Material>();

        public static Material ParticleMaterial(Color color)
        {
            Material m;
            if (particleMats.TryGetValue(color, out m) && m != null) return m;
            var shader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default");
            m = new Material(shader) { mainTexture = ArenaArt.SoftDot };
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(color.r, color.g, color.b, 0.5f));
            particleMats[color] = m;
            return m;
        }
    }
}
