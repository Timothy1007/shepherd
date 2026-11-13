using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 一個影格的動作輸入（情境操作，不用切模式）：
    /// Primary＝左鍵（射擊；抓著東西時是丟出），Interact＝E（抓取／放下、按住復原、閱讀），Deploy＝Q（擬態錨定）。
    /// </summary>
    public struct LanternActionInput
    {
        public bool Primary;
        public bool Interact;
        public bool Deploy;

        public static LanternActionInput None => new LanternActionInput();
    }

    /// <summary>
    /// 右手穩定器與情境互動（灰盒原型）：
    /// - 左鍵射擊：按住蓄力、放開射出；每發消耗穩定器能量（輕射 10、滿蓄力 20），停手後慢慢回復。
    ///   子彈沿準心那條線飛（畫面上從裝置射出、很快匯到準心線），只有準心很接近敵人時才有輕微瞄準輔助。
    /// - E：對著可以搬的東西＝抓起（抓著時左鍵丟出、再按 E 放下）；按住 E 對著倒下的東西＝復原；對著筆記＝閱讀。
    /// - Q：擬態錨定——在準心處展開一個暫時的穩定場（場內是真實、擋光球、失序體變慢），兩次，會充能。
    /// </summary>
    public sealed class LanternActions
    {
        public const float ChargeSeconds = 1.1f;
        public const float Reach = 6.5f;
        public const float MaxEnergy = 100f;
        public const float EnergyRegen = 8f;
        public const float AssistAngle = 4f;

        private sealed class Shot
        {
            public Transform T;
            public Vector3 Origin, Dir, Offset;
            public float Dist, Speed, Charge, Life;
            public Transform Homing;
        }

        public sealed class Shield
        {
            public Vector3 Center;
            public float Radius, Life;
            public Transform Visual;
        }

        public const float ShieldRadius = 2.3f;
        public const float ShieldSeconds = 8f;
        public const int ShieldCharges = 2;
        public const float ShieldRecharge = 14f;

        public float Energy { get; private set; } = MaxEnergy;
        public float Charge { get; private set; }
        public float ThrowCharge { get; private set; }
        public LanternTetherable Held { get; private set; }
        public LanternRestorable Restoring { get; private set; }
        public Component Aimed { get; private set; }
        public int ShotsFired { get; private set; }
        public int Impacts { get; private set; }
        public int TargetHits { get; private set; }
        public int EnemyHits { get; private set; }
        public int Restored { get; private set; }
        public int Placed { get; private set; }
        public Vector3 LastImpact { get; private set; }
        public Vector3 HoldPoint { get; private set; }
        public string Prompt { get; private set; } = "";
        public LanternReadable LastRead { get; private set; }
        public int ShieldChargesLeft { get; private set; } = ShieldCharges;
        public float ShieldRechargeTimer { get; private set; }
        public readonly List<Shield> Shields = new List<Shield>();

        public System.Func<IEnumerable<Component>> Targets;
        public LanternAudio Audio;
        public ParticleSystem Particles => burst;
        /// <summary>閱讀時呼叫（舊介面，保留）。</summary>
        public System.Action<LanternReadable> OnRead;
        /// <summary>E 拿起可以近看的東西時呼叫（QA 進入近看畫面）。</summary>
        public System.Action<LanternInspectable> OnInspect;
        public int Toggled { get; private set; }
        /// <summary>近看中：不處理射擊與互動。</summary>
        public bool Suspended;

        private readonly Camera cam;
        private readonly LanternRig rig;
        private readonly LanternGraybox world;
        private readonly List<Shot> shots = new List<Shot>();
        private readonly LineRenderer beam;
        private readonly ParticleSystem burst;
        private readonly Material shotMat, shieldMat;
        private readonly Light holdLight;
        private bool primaryWas, interactWas, deployWas;
        private float holdDistance = 2f;
        private float time, sinceShot = 99f;
        private readonly int mask;

        public LanternActions(Camera cam, LanternRig rig, LanternGraybox world)
        {
            this.cam = cam;
            this.rig = rig;
            this.world = world;
            mask = ~(1 << LanternRig.ViewLayer);

            var bg = new GameObject("StabilizerBeam");
            bg.transform.SetParent(world.Root, false);
            beam = bg.AddComponent<LineRenderer>();
            beam.sharedMaterial = LanternReveal.Additive;
            beam.positionCount = 16;
            beam.widthMultiplier = 0.018f;
            beam.numCapVertices = 2;
            beam.enabled = false;
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var pg = new GameObject("GlitchBurst");
            pg.transform.SetParent(world.Root, false);
            burst = pg.AddComponent<ParticleSystem>();
            burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = burst.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.55f), new Color(0.6f, 0.95f, 1f));
            main.gravityModifier = -0.05f;
            main.maxParticles = 800;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = burst.emission;
            em.rateOverTime = 0f;
            var shape = burst.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            var pr = burst.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = LanternReveal.Additive;
            pr.renderMode = ParticleSystemRenderMode.Stretch;
            pr.lengthScale = 2.5f;

            shotMat = ArenaArt.Mat(new Color(1f, 0.9f, 0.7f), 0.2f, new Color(5f, 3.8f, 2.2f));
            shotMat.name = "StabilizerShot";
            shieldMat = new Material(LanternReveal.Additive) { name = "MimicAnchorDome", hideFlags = HideFlags.DontSave };
            shieldMat.SetColor("_BaseColor", new Color(0.55f, 0.85f, 1f, 0.12f));

            var hl = new GameObject("TetherGlow");
            hl.transform.SetParent(world.Root, false);
            holdLight = hl.AddComponent<Light>();
            holdLight.type = LightType.Point;
            holdLight.range = 1.6f;
            holdLight.intensity = 0f;
            holdLight.shadows = LightShadows.None;

            if (world.Target != null) world.Target.Glitch = burst;
        }

        public void Destroy()
        {
            foreach (var s in shots) if (s.T != null) ArenaArt.Kill(s.T.gameObject);
            shots.Clear();
            foreach (var s in Shields) if (s.Visual != null) ArenaArt.Kill(s.Visual.gameObject);
            Shields.Clear();
            ArenaArt.Kill(shotMat);
            ArenaArt.Kill(shieldMat);
        }

        /// <summary>打開的電燈開關數（射擊或按 E）。</summary>
        public int SwitchesHit { get; private set; }

        public void AddEnergy(float v) => Energy = Mathf.Min(MaxEnergy, Energy + v);
        public static float ShotCost(float charge) => 10f + 10f * Mathf.Clamp01(charge);

        // ---------------- 瞄準 ----------------

        private Ray AimRay => new Ray(cam.transform.position, cam.transform.forward);

        private bool LineOfSight(Vector3 to, Component target)
        {
            var from = cam.transform.position;
            var d = to - from;
            RaycastHit hit;
            if (!Physics.Raycast(from, d.normalized, out hit, d.magnitude, mask, QueryTriggerInteraction.Ignore)) return true;
            return hit.collider != null && target != null && hit.collider.GetComponentInParent(target.GetType()) == target;
        }

        /// <summary>準星直接對到的東西：只接受指定的元件種類（不會把牆的 Transform 當成目標）。</summary>
        private static readonly System.Type[] TargetTypes = { typeof(LanternTarget), typeof(LanternEnemy), typeof(LanternBoss), typeof(LanternPhrase), typeof(LanternSwitch) };
        private static readonly System.Type[] InteractTypes = { typeof(LanternTetherable), typeof(LanternRestorable), typeof(LanternToggle), typeof(LanternInspectable), typeof(LanternSwitch) };

        private static Component Pick(Collider col, System.Type[] types)
        {
            foreach (var t in types)
            {
                var c = col.GetComponentInParent(t);
                if (c != null) return c;
            }
            return null;
        }

        private Component FindAimedOf(IEnumerable<Component> candidates, System.Type[] types, float maxAngle, float range)
        {
            RaycastHit hit;
            float wallDist = range;
            if (Physics.SphereCast(AimRay, 0.1f, out hit, range, mask, QueryTriggerInteraction.Ignore))
            {
                var direct = Pick(hit.collider, types);
                if (direct != null && (!(direct is LanternRestorable) || !((LanternRestorable)direct).Done)) return direct;
                wallDist = hit.distance + 0.5f;
            }
            Component best = null;
            float bestAngle = maxAngle;
            foreach (var c in candidates)
            {
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                var center = Center(c);
                var d = center - cam.transform.position;
                if (d.magnitude > range || d.magnitude > wallDist + 1.5f) continue;
                float a = Vector3.Angle(cam.transform.forward, d);
                if (a < bestAngle && LineOfSight(center, c)) { bestAngle = a; best = c; }
            }
            return best;
        }

        private T FindAimed<T>(IEnumerable<T> candidates, float maxAngle, float range) where T : Component
        {
            RaycastHit hit;
            if (Physics.SphereCast(AimRay, 0.1f, out hit, range, mask, QueryTriggerInteraction.Ignore))
            {
                var direct = hit.collider.GetComponentInParent<T>();
                if (direct != null && typeof(T) != typeof(Component)) return direct;
            }
            T best = null;
            float bestAngle = maxAngle;
            foreach (var c in candidates)
            {
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                var center = Center(c);
                var d = center - cam.transform.position;
                if (d.magnitude > range) continue;
                float a = Vector3.Angle(cam.transform.forward, d);
                if (a < bestAngle && LineOfSight(center, c)) { bestAngle = a; best = c; }
            }
            return best;
        }

        private static Vector3 Center(Component c)
        {
            var col = c.GetComponentInChildren<Collider>();
            return col != null ? col.bounds.center : c.transform.position;
        }

        private IEnumerable<Component> AimTargets()
        {
            if (world.Target != null) yield return world.Target;
            if (Targets != null) foreach (var c in Targets()) if (c != null) yield return c;
        }

        private IEnumerable<Component> Interactables()
        {
            foreach (var t in world.Tetherables) if (t != null) yield return t;
            foreach (var r in world.Restorables) if (r != null && !r.Done) yield return r;
            foreach (var t in world.Toggles) if (t != null) yield return t;
            foreach (var i in world.Inspectables) if (i != null) yield return i;
            foreach (var s in world.Switches) if (s != null && !s.On) yield return s;
        }

        // ---------------- 每個影格 ----------------

        public void Step(float dt, LanternActionInput inp)
        {
            time += dt;
            sinceShot += dt;
            bool pDown = inp.Primary && !primaryWas, pUp = !inp.Primary && primaryWas;
            bool eDown = inp.Interact && !interactWas;
            bool qDown = inp.Deploy && !deployWas;
            primaryWas = inp.Primary;
            interactWas = inp.Interact;
            deployWas = inp.Deploy;
            Physics.SyncTransforms();
            if (sinceShot > 1.2f) Energy = Mathf.Min(MaxEnergy, Energy + EnergyRegen * dt);

            bool active = false;
            Aimed = null;
            if (Suspended) { Prompt = ""; Charge = 0f; beam.enabled = false; holdLight.intensity = 0f; Restoring = null; }
            else if (Held != null)
            {
                active = StepHeld(dt, inp, pUp, eDown);
            }
            else
            {
                // 射擊（不用切模式）。
                if (inp.Primary && Energy >= ShotCost(0f)) Charge = Mathf.Min(1f, Charge + dt / ChargeSeconds);
                if (pUp && Charge > 0f)
                {
                    float c = Charge;
                    while (c > 0f && ShotCost(c) > Energy) c -= 0.1f;
                    if (c > 0f || Energy >= ShotCost(0f)) Fire(Mathf.Max(0f, c));
                    Charge = 0f;
                }
                if (pDown && Energy < ShotCost(0f)) Audio?.Play(LanternAudio.Toggle, 0.5f);

                // E 情境互動。
                var aimed = FindAimedOf(Interactables(), InteractTypes, 7f, Reach);
                Aimed = aimed;
                active = StepInteract(dt, inp, eDown, aimed);
            }
            if (Restoring == null)
                foreach (var r in world.Restorables) if (r != null) r.Step(dt, false);

            if (qDown) DeployShield();
            StepShields(dt);
            StepShots(dt);
            StepThrown(dt);
            foreach (var s in world.Spots) if (s != null && s.Check()) { Placed++; Burst(s.transform.position + Vector3.up * 0.4f, 40); Audio?.Play(LanternAudio.Placed, 0.9f); }
            foreach (var b in world.Blockers) if (b != null) b.Refresh();
            // 抓著對的東西時，該放的位置會發光；生活互動的動畫；掉出世界的東西回到原位。
            foreach (var s in world.Spots) if (s != null) { s.Highlight = Held != null && s.Expected == Held; s.UpdateHighlight(time); }
            foreach (var tg in world.Toggles) if (tg != null) tg.Step(dt, time);
            foreach (var t in world.Tetherables)
                if (t != null && t.transform.position.y < -3f)
                {
                    if (Held == t) DropHeld(false);
                    t.Body.velocity = Vector3.zero;
                    t.Body.angularVelocity = Vector3.zero;
                    t.Body.position = t.HomePosition + Vector3.up * 0.05f;
                    t.Body.rotation = t.HomeRotation;
                    t.transform.SetPositionAndRotation(t.Body.position, t.Body.rotation);
                }
            if (world.Target != null) world.Target.Step(dt, time);
            rig.SetDevice(Held != null ? 1 : Restoring != null ? 2 : 0, Held != null ? ThrowCharge : Charge, active);
            if (Audio != null)
            {
                LanternAudio.Loop(Audio.Charge, Charge > 0.01f || ThrowCharge > 0.01f, 0.8f + Mathf.Max(Charge, ThrowCharge) * 1.1f, 0.35f, dt);
                LanternAudio.Loop(Audio.Tether, Held != null, 1f + Mathf.Sin(time * 3f) * 0.03f, 0.3f, dt);
                LanternAudio.Loop(Audio.Restore, Restoring != null, 0.9f + (Restoring != null ? Restoring.Progress * 0.5f : 0f), 0.35f, dt);
            }
            if (Held == null && Restoring == null && string.IsNullOrEmpty(Prompt))
                Prompt = Energy < ShotCost(0f) ? "穩定器能量不足…" : "";
        }

        private bool StepInteract(float dt, LanternActionInput inp, bool eDown, Component aimed)
        {
            Prompt = "";
            // 持續復原：按住 E 期間只要大致對著同一件東西就繼續。
            LanternRestorable target = null;
            if (Restoring != null && inp.Interact && !Restoring.Done)
            {
                var d = Center(Restoring) - cam.transform.position;
                if (Vector3.Angle(cam.transform.forward, d) < 22f && d.magnitude < Reach + 1f) target = Restoring;
            }
            if (target == null && inp.Interact) target = aimed as LanternRestorable;
            Restoring = target;
            bool casting = target != null;
            if (casting)
            {
                foreach (var r in world.Restorables)
                {
                    if (r == null) continue;
                    if (r.Step(dt, r == target))
                    {
                        Restored++;
                        Burst(Center(r), 80);
                        Audio?.Play(LanternAudio.RestoreDone, 0.9f);
                        Restoring = null;
                    }
                }
                if (Restoring != null)
                {
                    DrawBeam(Center(Restoring), new Color(0.6f, 1f, 0.7f, 0.85f));
                    holdLight.transform.position = Center(Restoring);
                    holdLight.color = new Color(0.7f, 1f, 0.8f);
                    holdLight.intensity = 0.7f;
                    Prompt = "復原中… " + Mathf.RoundToInt(Restoring.Progress * 100f) + "%";
                    if (Random.value < 0.5f) Burst(Center(Restoring) + Random.insideUnitSphere * 0.4f, 2);
                    return true;
                }
            }
            beam.enabled = false;
            holdLight.intensity = 0f;

            var tether = aimed as LanternTetherable;
            var rest = aimed as LanternRestorable;
            var toggle = aimed as LanternToggle;
            var insp = aimed as LanternInspectable;
            var sw = aimed as LanternSwitch;
            if (tether != null)
            {
                Prompt = "E 抓起" + tether.Label;
                if (eDown) Grab(tether);
            }
            else if (rest != null) Prompt = "按住 E " + (rest == world.FuseBox ? "推回" : "復原") + rest.Label;
            else if (toggle != null)
            {
                Prompt = toggle.Prompt;
                if (eDown) { toggle.Toggle(); Toggled++; Audio?.Play(LanternAudio.Toggle, 0.6f); }
            }
            else if (sw != null)
            {
                Prompt = "E 打開" + sw.Label + "（也可以從遠處射擊開關）";
                if (eDown && sw.Hit()) { SwitchesHit++; Audio?.Play(LanternAudio.Toggle, 0.8f); }
            }
            else if (insp != null)
            {
                Prompt = "E 拿起來看：" + insp.Title;
                if (eDown) { insp.Opened++; OnInspect?.Invoke(insp); Audio?.Play(LanternAudio.Grab, 0.5f); }
            }
            return false;
        }

        // ---------------- 射擊 ----------------

        public void Fire(float charge)
        {
            charge = Mathf.Clamp01(charge);
            Energy = Mathf.Max(0f, Energy - ShotCost(charge));
            sinceShot = 0f;
            var origin = cam.transform.position;
            var dir = cam.transform.forward;
            Transform homing = null;
            var t = FindAimedOf(AimTargets(), TargetTypes, AssistAngle, 25f);
            if (t != null) { dir = (Center(t) - origin).normalized; homing = t.transform; }
            var muzzle = rig.Muzzle != null ? rig.Muzzle.position : origin;
            var go = ArenaArt.Prim(PrimitiveType.Sphere, world.Root, muzzle, Vector3.one * (0.07f + 0.08f * charge), shotMat, null, "StabilizerShot", false, false);
            var l = go.gameObject.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.85f, 0.6f);
            l.range = 2f + 2f * charge;
            l.intensity = 1.5f;
            l.shadows = LightShadows.None;
            shots.Add(new Shot { T = go, Origin = origin, Dir = dir, Offset = muzzle - origin, Speed = 16f + 8f * charge, Charge = charge, Life = 2.5f, Homing = homing });
            ShotsFired++;
            rig.Recoil();
            Audio?.Play(LanternAudio.Shot, 0.8f);
        }

        private void StepShots(float dt)
        {
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var s = shots[i];
                s.Life -= dt;
                if (s.Homing != null && s.Homing.gameObject.activeInHierarchy)
                {
                    var p0 = s.Origin + s.Dir * s.Dist;
                    var hc = s.Homing.GetComponentInChildren<Collider>();
                    var want = ((hc != null ? hc.bounds.center : s.Homing.position + Vector3.up * 1.0f) - p0).normalized;
                    s.Dir = Vector3.RotateTowards(s.Dir, want, Mathf.Deg2Rad * 60f * dt, 0f);
                    s.Origin = p0 - s.Dir * s.Dist;
                }
                float step = s.Speed * dt;
                var from = s.Origin + s.Dir * s.Dist;
                RaycastHit hit;
                if (Physics.SphereCast(from, 0.05f, s.Dir, out hit, step, mask, QueryTriggerInteraction.Ignore))
                {
                    Impact(hit.point + hit.normal * 0.05f, hit.collider, s.Charge);
                    ArenaArt.Kill(s.T.gameObject);
                    shots.RemoveAt(i);
                    continue;
                }
                s.Dist += step;
                // 畫面上：從裝置出發，很快匯到準心那條線上。
                s.T.position = s.Origin + s.Dir * s.Dist + s.Offset * Mathf.Exp(-s.Dist * 4f);
                if (s.Life <= 0f) { ArenaArt.Kill(s.T.gameObject); shots.RemoveAt(i); }
            }
        }

        private void Impact(Vector3 p, Collider c, float charge)
        {
            Impacts++;
            LastImpact = p;
            LanternReveal.AddBubble(p, 0.7f + 0.7f * charge, 3f + 2f * charge);
            Burst(p, Mathf.RoundToInt(20 + 40 * charge));
            Audio?.Play(LanternAudio.Impact, 0.7f);
            if (c == null) return;
            var target = c.GetComponentInParent<LanternTarget>();
            if (target != null) { target.Hit(charge); TargetHits++; }
            var enemy = c.GetComponentInParent<LanternEnemy>();
            if (enemy != null && enemy.Alive) { enemy.Hit(charge, false); EnemyHits++; }
            var boss = c.GetComponentInParent<LanternBoss>();
            if (boss != null && boss.Alive) { boss.Hit(charge, false); EnemyHits++; }
            var phrase = c.GetComponentInParent<LanternPhrase>();
            if (phrase != null) phrase.Shatter();
            var sw = c.GetComponentInParent<LanternSwitch>();
            if (sw != null && sw.Hit()) { SwitchesHit++; Burst(p, 60); Audio?.Play(LanternAudio.Toggle, 0.9f); }
        }

        private void Burst(Vector3 p, int count)
        {
            var ep = new ParticleSystem.EmitParams { position = p, applyShapeToPosition = true };
            burst.Emit(ep, count);
        }

        // ---------------- 牽引 ----------------

        private bool StepHeld(float dt, LanternActionInput inp, bool pUp, bool eDown)
        {
            if (eDown) { DropHeld(false); return false; }
            if (inp.Primary) ThrowCharge = Mathf.Min(1f, ThrowCharge + dt / ChargeSeconds);
            if (pUp) { Throw(ThrowCharge); ThrowCharge = 0f; return false; }
            var rb = Held.Body;
            var target = cam.transform.position + cam.transform.forward * holdDistance + Vector3.down * 0.2f + Vector3.up * Mathf.Sin(time * 2.2f) * 0.03f;
            target.y = Mathf.Max(target.y, cam.transform.position.y - 0.55f);
            HoldPoint = target;
            var desired = Vector3.ClampMagnitude((target - rb.position) * 9f, 9f);
            rb.velocity = Vector3.Lerp(rb.velocity, desired, 1f - Mathf.Exp(-10f * dt));
            rb.angularVelocity *= Mathf.Exp(-4f * dt);
            var upright = Quaternion.Euler(0f, rb.rotation.eulerAngles.y, 0f);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, upright, 1f - Mathf.Exp(-3f * dt)));
            if (Vector3.Distance(rb.position, target) > 3.5f) { DropHeld(false); return false; }
            DrawBeam(Center(Held), new Color(0.5f, 0.85f, 1f, 0.85f));
            holdLight.transform.position = Center(Held);
            holdLight.color = new Color(0.6f, 0.9f, 1f);
            holdLight.intensity = 0.6f;
            Prompt = ThrowCharge > 0f ? "蓄力中… 放開左鍵丟出" : "左鍵 丟出（按住蓄力）｜E 放下";
            foreach (var s in world.Spots) if (s != null && !s.Solved && s.Expected == Held && !string.IsNullOrEmpty(s.Hint)) Prompt += "｜好像該放回" + s.Hint + "（會發光）";
            return true;
        }

        public void Grab(LanternTetherable t)
        {
            Audio?.Play(LanternAudio.Grab, 0.6f);
            Held = t;
            t.Held = true;
            t.Body.useGravity = false;
            Charge = 0f;
            holdDistance = Mathf.Clamp(Vector3.Distance(cam.transform.position, Center(t)), 1.5f, 2.6f);
        }

        public void DropHeld(bool keepVelocity)
        {
            if (Held == null) return;
            if (!keepVelocity) Audio?.Play(LanternAudio.Drop, 0.6f);
            Held.Held = false;
            Held.Body.useGravity = true;
            if (!keepVelocity) Held.Body.velocity *= 0.3f;
            Held = null;
            beam.enabled = false;
            holdLight.intensity = 0f;
        }

        private void Throw(float charge)
        {
            if (Held == null) return;
            var t = Held;
            var dir = cam.transform.forward;
            var target = FindAimedOf(AimTargets(), TargetTypes, 12f, 20f);
            if (target != null) dir = (Center(target) - Center(t)).normalized;
            DropHeld(true);
            t.Body.velocity = dir * (6f + 9f * charge) + Vector3.up * 0.8f;
            t.ThrownTimer = 1.6f;
            rig.Recoil();
            Audio?.Play(LanternAudio.Throw, 0.8f);
        }

        private void StepThrown(float dt)
        {
            foreach (var t in world.Tetherables)
            {
                if (t == null || t.ThrownTimer <= 0f) continue;
                t.ThrownTimer -= dt;
                if (t.Body.velocity.magnitude <= 2f) continue;
                var p = Center(t);
                foreach (var c in AimTargets())
                {
                    var col = c.GetComponent<Collider>();
                    if (col == null || Vector3.Distance(col.ClosestPoint(p), p) > 0.5f) continue;
                    var lt = c as LanternTarget;
                    var le = c as LanternEnemy;
                    var lb = c as LanternBoss;
                    if (lt != null) { lt.Hit(0.7f); TargetHits++; }
                    else if (le != null && le.Alive) { le.Hit(0.7f, true); EnemyHits++; }
                    else if (lb != null && lb.Alive) { lb.Hit(0.7f, true); EnemyHits++; }
                    else continue;
                    t.ThrownTimer = 0f;
                    LanternReveal.AddBubble(p, 1f, 3f);
                    Burst(p, 40);
                    Audio?.Play(LanternAudio.Impact, 0.8f);
                    break;
                }
            }
        }

        // ---------------- 擬態錨定 ----------------

        public bool DeployShield()
        {
            if (ShieldChargesLeft <= 0) { Audio?.Play(LanternAudio.Toggle, 0.5f); return false; }
            var at = cam.transform.position + cam.transform.forward * 3f;
            RaycastHit hit;
            if (Physics.Raycast(AimRay, out hit, 6f, mask, QueryTriggerInteraction.Ignore)) at = hit.point;
            if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out hit, 6f, mask, QueryTriggerInteraction.Ignore)) at = hit.point;
            ShieldChargesLeft--;
            if (ShieldRechargeTimer <= 0f) ShieldRechargeTimer = ShieldRecharge;
            var v = ArenaArt.Prim(PrimitiveType.Sphere, world.Root, at, Vector3.one * ShieldRadius * 2f, shieldMat, null, "MimicAnchor", false, false);
            Shields.Add(new Shield { Center = at, Radius = ShieldRadius, Life = ShieldSeconds, Visual = v });
            LanternReveal.AddBubble(at + Vector3.up * 0.8f, ShieldRadius + 0.3f, ShieldSeconds);
            Burst(at + Vector3.up * 0.5f, 60);
            Audio?.Play(LanternAudio.RestoreDone, 0.6f);
            return true;
        }

        /// <summary>這個點是否在某個擬態錨定的穩定場內。</summary>
        public bool InShield(Vector3 p)
        {
            foreach (var s in Shields) if (Vector3.Distance(new Vector3(p.x, s.Center.y, p.z), s.Center) < s.Radius && Mathf.Abs(p.y - s.Center.y) < 3f) return true;
            return false;
        }

        /// <summary>失序體貼著穩定場時會侵蝕它（縮短時間）。</summary>
        public void Erode(Vector3 p, float seconds)
        {
            foreach (var s in Shields)
                if (Vector3.Distance(new Vector3(p.x, s.Center.y, p.z), s.Center) < s.Radius + 0.6f) s.Life -= seconds;
        }

        private void StepShields(float dt)
        {
            if (ShieldChargesLeft < ShieldCharges)
            {
                ShieldRechargeTimer -= dt;
                if (ShieldRechargeTimer <= 0f)
                {
                    ShieldChargesLeft++;
                    ShieldRechargeTimer = ShieldChargesLeft < ShieldCharges ? ShieldRecharge : 0f;
                }
            }
            for (int i = Shields.Count - 1; i >= 0; i--)
            {
                var s = Shields[i];
                s.Life -= dt;
                if (s.Visual != null)
                {
                    float k = Mathf.Clamp01(s.Life / 0.6f) * Mathf.Clamp01((ShieldSeconds - s.Life) / 0.3f + 0.2f);
                    s.Visual.localScale = Vector3.one * s.Radius * 2f * Mathf.Max(0.05f, k) * (1f + Mathf.Sin(time * 3f) * 0.01f);
                }
                if (s.Life <= 0f)
                {
                    if (s.Visual != null) ArenaArt.Kill(s.Visual.gameObject);
                    Shields.RemoveAt(i);
                }
            }
        }

        // ---------------- 光線 ----------------

        private void DrawBeam(Vector3 to, Color c)
        {
            var from = rig.Muzzle != null ? rig.Muzzle.position : cam.transform.position;
            beam.enabled = true;
            beam.startColor = c;
            beam.endColor = new Color(c.r, c.g, c.b, c.a * 0.6f);
            var mid = (from + to) * 0.5f + Vector3.down * (0.12f + Mathf.Sin(time * 3f) * 0.03f);
            int n = beam.positionCount;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)(n - 1);
                var p = (1 - k) * (1 - k) * from + 2 * (1 - k) * k * mid + k * k * to;
                p += cam.transform.right * Mathf.Sin(k * 9f + time * 7f) * 0.012f * Mathf.Sin(k * Mathf.PI);
                beam.SetPosition(i, p);
            }
        }
    }
}
