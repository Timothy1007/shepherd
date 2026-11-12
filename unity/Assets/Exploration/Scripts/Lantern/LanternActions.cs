using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    public enum StabilizerMode { Shot = 0, Tether = 1, Restore = 2 }

    /// <summary>一個影格的穩定器輸入。SelectMode：0＝不切換，1～3＝射擊／牽引／復原。</summary>
    public struct LanternActionInput
    {
        public bool Primary;
        public bool Secondary;
        public int SelectMode;
        public float Scroll;

        public static LanternActionInput None => new LanternActionInput();
    }

    /// <summary>
    /// 右手穩定器的三種用法（灰盒原型）：
    /// - 射擊：按住左鍵蓄力、放開射出穩定能量；有瞄準輔助與輕微追蹤。撞擊點短暫打開一小塊現實，打中練習靶會暈眩。
    /// - 牽引：左鍵抓住／放下；物件浮起、慢半拍地跟著、會晃、被牆擋；按住右鍵蓄力丟出（會追蹤練習靶）。
    /// - 復原：對著倒下／錯位的物件按住左鍵，物件像倒帶一樣回到原位；中途放開會倒回去；完成後附近永久變成穩定區。
    /// </summary>
    public sealed class LanternActions
    {
        public const float ChargeSeconds = 1.1f;
        public const float Reach = 8f;

        private sealed class Shot
        {
            public Transform T;
            public Vector3 Vel;
            public float Charge, Life;
            public Transform Homing;
        }

        public StabilizerMode Mode { get; private set; }
        public float Charge { get; private set; }
        public float ThrowCharge { get; private set; }
        public LanternTetherable Held { get; private set; }
        public LanternRestorable Restoring { get; private set; }
        public Component Aimed { get; private set; }
        public int ShotsFired { get; private set; }
        public int Impacts { get; private set; }
        public int TargetHits { get; private set; }
        public int Restored { get; private set; }
        public int Placed { get; private set; }
        public Vector3 LastImpact { get; private set; }
        /// <summary>牽引時物件要浮到的位置（手前方）。</summary>
        public Vector3 HoldPoint { get; private set; }
        public string Prompt { get; private set; } = "";

        private readonly Camera cam;
        private readonly LanternRig rig;
        private readonly LanternGraybox world;
        private readonly List<Shot> shots = new List<Shot>();
        private readonly LineRenderer beam;
        private readonly ParticleSystem burst;
        private readonly Material shotMat;
        private readonly Light holdLight;
        private bool primaryWas, secondaryWas;
        private float holdDistance = 2f;
        private float time;
        private int mask;
        /// <summary>射擊與丟擲的瞄準輔助目標（練習靶＋活著的失序體）。</summary>
        public System.Func<IEnumerable<Component>> Targets;
        public LanternAudio Audio;
        public int EnemyHits { get; private set; }
        public ParticleSystem Particles => burst;

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
            main.maxParticles = 600;
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

            var hl = new GameObject("TetherGlow");
            hl.transform.SetParent(world.Root, false);
            holdLight = hl.AddComponent<Light>();
            holdLight.type = LightType.Point;
            holdLight.range = 1.6f;
            holdLight.intensity = 0f;
            holdLight.shadows = LightShadows.None;

            if (world.Target != null)
            {
                world.Target.Glitch = burst;
            }
        }

        public void Destroy()
        {
            foreach (var s in shots) if (s.T != null) ArenaArt.Kill(s.T.gameObject);
            shots.Clear();
            ArenaArt.Kill(shotMat);
        }

        public void SetMode(StabilizerMode m)
        {
            if (m == Mode) return;
            DropHeld(false);
            Restoring = null;
            Charge = 0f;
            ThrowCharge = 0f;
            Mode = m;
        }

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

        /// <summary>準星正對或在小角度內（瞄準輔助）的物件。</summary>
        private T FindAimed<T>(IEnumerable<T> candidates, float maxAngle, float range) where T : Component
        {
            RaycastHit hit;
            if (Physics.SphereCast(AimRay, 0.12f, out hit, range, mask, QueryTriggerInteraction.Ignore))
            {
                var direct = hit.collider.GetComponentInParent<T>();
                if (direct != null) return direct;
            }
            T best = null;
            float bestAngle = maxAngle;
            foreach (var c in candidates)
            {
                if (c == null) continue;
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

        // ---------------- 每個影格 ----------------

        public void Step(float dt, LanternActionInput inp)
        {
            time += dt;
            if (inp.SelectMode >= 1 && inp.SelectMode <= 3) SetMode((StabilizerMode)(inp.SelectMode - 1));
            if (Mathf.Abs(inp.Scroll) > 0.01f) SetMode((StabilizerMode)(((int)Mode + (inp.Scroll > 0 ? 2 : 1)) % 3));

            bool pDown = inp.Primary && !primaryWas, pUp = !inp.Primary && primaryWas;
            bool sUp = !inp.Secondary && secondaryWas;
            primaryWas = inp.Primary;
            secondaryWas = inp.Secondary;
            Physics.SyncTransforms();

            bool active = false;
            switch (Mode)
            {
                case StabilizerMode.Shot:
                    Aimed = FindAimed(AimTargets(), 10f, 25f);
                    if (inp.Primary) Charge = Mathf.Min(1f, Charge + dt / ChargeSeconds);
                    if (pUp) { Fire(Charge); Charge = 0f; }
                    Prompt = inp.Primary ? "蓄力中… 放開射出穩定能量" : "按住左鍵蓄力，放開射出穩定能量";
                    beam.enabled = false;
                    break;
                case StabilizerMode.Tether:
                    active = StepTether(dt, inp, pDown, sUp);
                    break;
                case StabilizerMode.Restore:
                    active = StepRestore(dt, inp);
                    break;
            }
            if (Mode != StabilizerMode.Restore)
                foreach (var r in world.Restorables) if (r != null && r.Step(dt, false)) { }

            StepShots(dt);
            StepThrown(dt);
            foreach (var s in world.Spots) if (s != null && s.Check()) { Placed++; Burst(s.transform.position + Vector3.up * 0.4f, 40); Audio?.Play(LanternAudio.Placed, 0.9f); }
            foreach (var b in world.Blockers) if (b != null) b.Refresh();
            if (world.Target != null) world.Target.Step(dt, time);
            rig.SetDevice((int)Mode, Mode == StabilizerMode.Tether ? ThrowCharge : Charge, active);
            if (Audio != null)
            {
                LanternAudio.Loop(Audio.Charge, Mode == StabilizerMode.Shot && inp.Primary || ThrowCharge > 0.01f, 0.8f + Mathf.Max(Charge, ThrowCharge) * 1.1f, 0.35f, dt);
                LanternAudio.Loop(Audio.Tether, Held != null, 1f + Mathf.Sin(time * 3f) * 0.03f, 0.3f, dt);
                LanternAudio.Loop(Audio.Restore, Restoring != null, 0.9f + (Restoring != null ? Restoring.Progress * 0.5f : 0f), 0.35f, dt);
            }
        }

        // ---------------- 射擊 ----------------

        public void Fire(float charge)
        {
            var origin = rig.Muzzle != null ? rig.Muzzle.position : cam.transform.position;
            var aimPoint = cam.transform.position + cam.transform.forward * 30f;
            RaycastHit hit;
            if (Physics.Raycast(AimRay, out hit, 30f, mask, QueryTriggerInteraction.Ignore)) aimPoint = hit.point;
            Transform homing = null;
            var t = FindAimed(AimTargets(), 10f, 25f);
            if (t != null) { aimPoint = Center(t); homing = t.transform; }
            var go = ArenaArt.Prim(PrimitiveType.Sphere, world.Root, origin, Vector3.one * (0.07f + 0.08f * charge), shotMat, null, "StabilizerShot", false, false);
            var l = go.gameObject.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.85f, 0.6f);
            l.range = 2f + 2f * charge;
            l.intensity = 1.5f;
            l.shadows = LightShadows.None;
            float speed = 14f + 8f * charge;
            shots.Add(new Shot { T = go, Vel = (aimPoint - origin).normalized * speed, Charge = charge, Life = 2.5f, Homing = homing });
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
                if (s.Homing != null)
                {
                    var want = (s.Homing.position + Vector3.up * 0.9f - s.T.position).normalized * s.Vel.magnitude;
                    s.Vel = Vector3.RotateTowards(s.Vel, want, Mathf.Deg2Rad * 120f * dt, 0f);
                }
                var step = s.Vel * dt;
                RaycastHit hit;
                if (Physics.SphereCast(s.T.position, 0.06f, step.normalized, out hit, step.magnitude, mask, QueryTriggerInteraction.Ignore))
                {
                    Impact(hit.point + hit.normal * 0.05f, hit.collider, s.Charge);
                    ArenaArt.Kill(s.T.gameObject);
                    shots.RemoveAt(i);
                    continue;
                }
                s.T.position += step;
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
            var target = c != null ? c.GetComponentInParent<LanternTarget>() : null;
            if (target != null) { target.Hit(charge); TargetHits++; }
            var enemy = c != null ? c.GetComponentInParent<LanternEnemy>() : null;
            if (enemy != null && enemy.Alive) { enemy.Hit(charge, false); EnemyHits++; }
        }

        private void Burst(Vector3 p, int count)
        {
            var ep = new ParticleSystem.EmitParams { position = p, applyShapeToPosition = true };
            burst.Emit(ep, count);
        }

        // ---------------- 牽引 ----------------

        private bool StepTether(float dt, LanternActionInput inp, bool pDown, bool sUp)
        {
            if (Held == null)
            {
                var t = FindAimed(world.Tetherables, 9f, Reach);
                Aimed = t;
                Prompt = t != null ? "左鍵 牽引" + t.Label : "對準可以牽引的東西";
                beam.enabled = false;
                holdLight.intensity = 0f;
                if (pDown && t != null) Grab(t);
                return false;
            }
            if (pDown) { DropHeld(false); return false; }
            if (inp.Secondary) ThrowCharge = Mathf.Min(1f, ThrowCharge + dt / ChargeSeconds);
            if (sUp) { Throw(ThrowCharge); ThrowCharge = 0f; return false; }

            var rb = Held.Body;
            var target = cam.transform.position + cam.transform.forward * holdDistance + Vector3.down * 0.2f + Vector3.up * Mathf.Sin(time * 2.2f) * 0.03f;
            // 至少浮到胸口高度（低頭抓地上的東西時也會先把它提起來）。
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
            Prompt = ThrowCharge > 0f ? "蓄力中… 放開右鍵丟出" : "左鍵 放下｜按住右鍵蓄力丟出";
            return true;
        }

        private IEnumerable<Component> AimTargets()
        {
            if (world.Target != null) yield return world.Target;
            if (Targets != null) foreach (var c in Targets()) if (c != null) yield return c;
        }

        public void Grab(LanternTetherable t)
        {
            Audio?.Play(LanternAudio.Grab, 0.6f);
            Held = t;
            t.Held = true;
            t.Body.useGravity = false;
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
            var target = FindAimed(AimTargets(), 12f, 20f);
            if (target != null) dir = (Center(target) - Center(t)).normalized;
            DropHeld(true);
            Audio?.Play(LanternAudio.Throw, 0.8f);
            t.Body.velocity = dir * (6f + 9f * charge) + Vector3.up * 0.8f;
            t.ThrownTimer = 1.6f;
            rig.Recoil();
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
                    if (col == null || Vector3.Distance(col.ClosestPoint(p), p) > 0.45f) continue;
                    var lt = c as LanternTarget;
                    var le = c as LanternEnemy;
                    if (lt != null) { lt.Hit(0.7f); TargetHits++; }
                    else if (le != null && le.Alive) { le.Hit(0.7f, true); EnemyHits++; }
                    else continue;
                    t.ThrownTimer = 0f;
                    LanternReveal.AddBubble(p, 1f, 3f);
                    Burst(p, 40);
                    Audio?.Play(LanternAudio.Impact, 0.8f);
                    break;
                }
            }
        }

        // ---------------- 復原 ----------------

        private bool StepRestore(float dt, LanternActionInput inp)
        {
            LanternRestorable target = null;
            if (Restoring != null && inp.Primary && !Restoring.Done)
            {
                var d = Center(Restoring) - cam.transform.position;
                if (Vector3.Angle(cam.transform.forward, d) < 20f && d.magnitude < Reach + 1f) target = Restoring;
            }
            if (target == null)
            {
                var t = FindAimed(world.Restorables, 9f, Reach);
                if (t != null && t.Done) t = null;
                target = inp.Primary ? t : null;
                Aimed = t;
                Prompt = t != null ? "按住左鍵 復原" + t.Label : "對準倒下或錯位的東西";
            }
            Restoring = target;
            bool casting = target != null;
            foreach (var r in world.Restorables)
            {
                if (r == null) continue;
                if (r.Step(dt, casting && r == target))
                {
                    Restored++;
                    Audio?.Play(LanternAudio.RestoreDone, 0.9f);
                    Burst(Center(r), 80);
                    Restoring = null;
                    Prompt = r.Label + "復原了";
                }
            }
            if (casting && Restoring != null)
            {
                DrawBeam(Center(Restoring), new Color(0.6f, 1f, 0.7f, 0.85f));
                holdLight.transform.position = Center(Restoring);
                holdLight.color = new Color(0.7f, 1f, 0.8f);
                holdLight.intensity = 0.7f;
                Prompt = "復原中… " + Mathf.RoundToInt(Restoring.Progress * 100f) + "%";
                if (Random.value < 0.5f) Burst(Center(Restoring) + Random.insideUnitSphere * 0.4f, 2);
            }
            else
            {
                beam.enabled = false;
                holdLight.intensity = 0f;
            }
            return casting;
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
