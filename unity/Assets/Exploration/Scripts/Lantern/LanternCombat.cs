using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 失序體（戰鬥原型）：在異域裡追你。暗處只看得到模糊的黑影，進入提燈光裡才看清楚輪廓與裂紋。
    /// - 近身：前兆 0.75 秒（身體膨脹、核心發亮、尖嘯）→ 往前撲 3 m → 喘息 0.9 秒（破綻）。
    /// - 中距離（4.5–12 m）：前兆 0.9 秒 → 丟出一顆慢速光球（不追蹤，可以橫移躲開），冷卻 4.5 秒。
    /// - 被穩定能量或丟出的物件打中會暈眩；暈眩中受到的傷害加倍（破綻）。生命歸零後消散，那一區永久恢復現實。
    /// </summary>
    public sealed class LanternEnemy : MonoBehaviour
    {
        public enum State { Chase, WindUp, Lunge, Recover, OrbWindUp, Stunned, Dying, Dead }

        public const float MaxHp = 6f;
        public const float Speed = 2.0f;
        public const float LungeRange = 2.6f;
        public const float LungeDistance = 3.2f;
        public const float LungeDamage = 25f;
        public const float OrbDamage = 15f;
        public const float WindUpSeconds = 0.75f;
        public const float OrbWindUpSeconds = 0.9f;

        public State Current { get; private set; } = State.Chase;
        public float Hp { get; private set; } = MaxHp;
        public float StateTimer { get; private set; }
        public bool Alive => Current != State.Dying && Current != State.Dead;
        public bool Stunned => Current == State.Stunned;
        public int TimesHit { get; private set; }
        public Renderer[] Parts;
        public Material CoreMat;
        public Transform Body;
        public AudioSource Drone, Voice;
        public ParticleSystem Glitch;

        private Vector3 lungeDir, lungeStart;
        private bool lungeHit;
        private float orbCooldown = 0.8f;
        private float lastDamage;

        public void Init()
        {
            Hp = MaxHp;
            Current = State.Chase;
            StateTimer = 0f;
        }

        public void Hit(float strength, bool thrown)
        {
            if (!Alive) return;
            TimesHit++;
            float dmg = thrown ? 2f : 1f + 2f * Mathf.Clamp01(strength);
            if (Current == State.Stunned || Current == State.Recover) dmg *= 2f;
            lastDamage = dmg;
            Hp -= dmg;
            if (Glitch != null) Glitch.Emit(new ParticleSystem.EmitParams { position = transform.position + Vector3.up * 1.1f, applyShapeToPosition = true }, Mathf.RoundToInt(25 + 40 * strength));
            if (Voice != null && Application.isPlaying) Voice.PlayOneShot(LanternAudio.EnemyHit, 0.9f);
            if (Hp <= 0f)
            {
                Current = State.Dying;
                StateTimer = 1.2f;
                if (Voice != null && Application.isPlaying) Voice.PlayOneShot(LanternAudio.EnemyDeath, 1f);
                return;
            }
            Current = State.Stunned;
            StateTimer = thrown ? 2f : Mathf.Lerp(1.0f, 2.4f, Mathf.Clamp01(strength));
        }

        public float LastDamage => lastDamage;

        private bool Blocked(Vector3 dir, float dist)
        {
            return Physics.SphereCast(transform.position + Vector3.up * 1.0f, 0.4f, dir, out _, dist, ~(1 << LanternRig.ViewLayer), QueryTriggerInteraction.Ignore);
        }

        private void Move(Vector3 dir, float speed, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            dir.Normalize();
            var chosen = dir;
            if (Blocked(dir, 0.7f))
            {
                chosen = Vector3.zero;
                foreach (var a in new[] { 40f, -40f, 80f, -80f, 120f, -120f })
                {
                    var d = Quaternion.Euler(0f, a, 0f) * dir;
                    if (!Blocked(d, 0.7f)) { chosen = d; break; }
                }
            }
            transform.position += chosen * speed * dt;
            if (chosen.sqrMagnitude > 0f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(chosen), 1f - Mathf.Exp(-8f * dt));
        }

        /// <summary>推進一個影格。player＝玩家腳底位置；回傳這一格對玩家造成的近身傷害（光球由 LanternCombat 處理）。</summary>
        public float Step(float dt, float time, Vector3 player, bool playerAlive, List<LanternCombat.Orb> orbs, Transform orbParent, Material orbMat)
        {
            float damage = 0f;
            var to = player - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            StateTimer -= dt;
            orbCooldown -= dt;
            float pulse = 0f;
            switch (Current)
            {
                case State.Chase:
                    if (!playerAlive) break;
                    if (dist < LungeRange) { Current = State.WindUp; StateTimer = WindUpSeconds; Say(LanternAudio.EnemyWarn); break; }
                    if (dist > 4.5f && dist < 12f && orbCooldown <= 0f) { Current = State.OrbWindUp; StateTimer = OrbWindUpSeconds; Say(LanternAudio.EnemyOrb); break; }
                    Move(to, Speed, dt);
                    break;
                case State.WindUp:
                    pulse = 1f - Mathf.Clamp01(StateTimer / WindUpSeconds);
                    if (dist > 0.1f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 1f - Mathf.Exp(-6f * dt));
                    if (StateTimer <= 0f)
                    {
                        Current = State.Lunge;
                        StateTimer = 0.28f;
                        lungeDir = transform.forward;
                        lungeStart = transform.position;
                        lungeHit = false;
                        Say(LanternAudio.EnemyLunge);
                    }
                    break;
                case State.Lunge:
                    pulse = 1f;
                    if (!Blocked(lungeDir, 0.5f)) transform.position += lungeDir * (LungeDistance / 0.28f) * dt;
                    if (!lungeHit && playerAlive && dist < 1.1f) { lungeHit = true; damage = LungeDamage; }
                    if (StateTimer <= 0f) { Current = State.Recover; StateTimer = 0.9f; }
                    break;
                case State.Recover:
                    if (StateTimer <= 0f) Current = State.Chase;
                    break;
                case State.OrbWindUp:
                    pulse = 1f - Mathf.Clamp01(StateTimer / OrbWindUpSeconds);
                    if (dist > 0.1f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 1f - Mathf.Exp(-6f * dt));
                    if (StateTimer <= 0f)
                    {
                        var from = transform.position + Vector3.up * 1.3f + transform.forward * 0.4f;
                        var target = player + Vector3.up * 1.0f;
                        var go = ArenaArt.Prim(PrimitiveType.Sphere, orbParent, from, Vector3.one * 0.32f, orbMat, null, "DisorderOrb", false, false);
                        var l = go.gameObject.AddComponent<Light>();
                        l.type = LightType.Point;
                        l.color = new Color(0.7f, 0.3f, 1f);
                        l.range = 3f;
                        l.intensity = 1.6f;
                        orbs.Add(new LanternCombat.Orb { T = go, Vel = (target - from).normalized * 6.5f, Life = 3f });
                        orbCooldown = 4.5f;
                        Current = State.Chase;
                    }
                    break;
                case State.Stunned:
                    if (StateTimer <= 0f) Current = State.Chase;
                    break;
                case State.Dying:
                    float k = Mathf.Clamp01(StateTimer / 1.2f);
                    if (Body != null) Body.localScale = new Vector3(k, Mathf.Lerp(1.6f, 1f, k), k);
                    if (Glitch != null && Random.value < 0.6f) Glitch.Emit(new ParticleSystem.EmitParams { position = transform.position + Vector3.up * Random.Range(0.2f, 2f) + Random.insideUnitSphere * 0.3f, applyShapeToPosition = true }, 3);
                    if (StateTimer <= 0f)
                    {
                        Current = State.Dead;
                        LanternReveal.AddAnchor(transform.position + Vector3.up * 0.8f, 2.4f);
                        gameObject.SetActive(false);
                    }
                    break;
            }
            // 外觀：前兆時膨脹、核心變亮；暈眩時抖動。
            if (Body != null && Alive)
            {
                float s = 1f + pulse * 0.18f + Mathf.Sin(time * 2.1f) * 0.02f;
                Body.localScale = new Vector3(s, 1f + pulse * 0.08f, s);
                Body.localRotation = Current == State.Stunned ? Quaternion.Euler(Mathf.Sin(time * 70f) * 6f, 0f, Mathf.Cos(time * 61f) * 6f) : Quaternion.identity;
            }
            if (CoreMat != null && Alive)
            {
                // 平常核心只是暗暗的紫光（不會被光暈染成一整團）；攻擊前兆時快速變亮，當作警告。
                var baseCol = new Color(0.35f, 0.08f, 0.55f);
                float glow = Current == State.Stunned ? 0.3f : 1f + pulse * pulse * 9f;
                CoreMat.SetColor("_AnomalyEmission", baseCol * glow);
                CoreMat.SetColor("_EmissionColor", new Color(1.4f, 0.9f, 0.5f) * glow);
            }
            if (Drone != null) Drone.volume = Alive ? 0.55f : 0f;
            return damage;
        }

        private void Say(AudioClip c)
        {
            if (Voice != null && Application.isPlaying) Voice.PlayOneShot(c, 0.9f);
        }
    }

    /// <summary>戰鬥測試場的規則：玩家生命、受傷回饋、倒下與重生、失序體重生、光球。</summary>
    public sealed class LanternCombat
    {
        public sealed class Orb
        {
            public Transform T;
            public Vector3 Vel;
            public float Life;
        }

        public const float MaxHealth = 100f;
        public const float RegenDelay = 4f;
        public const float RegenPerSecond = 10f;
        public const float EnemyRespawnSeconds = 5f;

        public float Health { get; private set; } = MaxHealth;
        public bool PlayerDown { get; private set; }
        public float DownTimer { get; private set; }
        public int Deaths { get; private set; }
        public int Kills { get; private set; }
        public float HurtFlash { get; private set; }
        public readonly List<LanternEnemy> Enemies = new List<LanternEnemy>();
        public readonly List<Orb> Orbs = new List<Orb>();

        private readonly LanternGraybox world;
        private readonly FirstPersonController player;
        private readonly LanternRig rig;
        private readonly LanternAudio audio;
        private readonly ParticleSystem glitch;
        private readonly Material orbMat;
        private float sinceDamage = 99f;
        private readonly HashSet<LanternEnemy> counted = new HashSet<LanternEnemy>();
        private float respawnTimer = -1f;
        private int spawnIndex;
        private float time;

        public LanternCombat(LanternGraybox world, FirstPersonController player, LanternRig rig, LanternAudio audio, ParticleSystem glitch)
        {
            this.world = world;
            this.player = player;
            this.rig = rig;
            this.audio = audio;
            this.glitch = glitch;
            orbMat = ArenaArt.Mat(new Color(0.5f, 0.2f, 0.8f), 0.3f, new Color(2.2f, 0.8f, 3.4f));
            orbMat.name = "DisorderOrb";
            if (world.EnemySpawns.Count > 0) Spawn(world.EnemySpawns[0]);
        }

        public LanternEnemy Spawn(Vector3 at)
        {
            var e = world.BuildEnemy(at);
            e.Glitch = glitch;
            e.Init();
            Enemies.Add(e);
            return e;
        }

        public IEnumerable<Component> Living
        {
            get { foreach (var e in Enemies) if (e != null && e.Alive && e.gameObject.activeSelf) yield return e; }
        }

        public void Damage(float amount)
        {
            if (PlayerDown || amount <= 0f) return;
            Health = Mathf.Max(0f, Health - amount);
            sinceDamage = 0f;
            HurtFlash = Mathf.Min(1f, HurtFlash + amount / 40f + 0.25f);
            rig.Shake(0.06f + amount / 400f);
            audio?.Play(LanternAudio.Hurt, 1f);
            if (Health <= 0f)
            {
                PlayerDown = true;
                DownTimer = 2f;
                Deaths++;
            }
        }

        public void Step(float dt)
        {
            time += dt;
            sinceDamage += dt;
            HurtFlash = Mathf.MoveTowards(HurtFlash, 0f, dt * 1.4f);
            var feet = player.transform.position;

            if (PlayerDown)
            {
                DownTimer -= dt;
                if (DownTimer <= 0f) Respawn();
            }
            else if (sinceDamage > RegenDelay)
            {
                Health = Mathf.Min(MaxHealth, Health + RegenPerSecond * dt);
            }

            for (int i = Enemies.Count - 1; i >= 0; i--)
            {
                var e = Enemies[i];
                if (e == null) { Enemies.RemoveAt(i); continue; }
                if (e.gameObject.activeSelf) Damage(e.Step(dt, time, feet, !PlayerDown, Orbs, world.Root, orbMat));
                if (!e.Alive && counted.Add(e)) Kills++;
                if (e.Current == LanternEnemy.State.Dead && respawnTimer < 0f) respawnTimer = EnemyRespawnSeconds;
            }
            if (respawnTimer >= 0f)
            {
                respawnTimer -= dt;
                if (respawnTimer < 0f) Spawn(FarthestSpawn(feet));
            }

            var body = feet + Vector3.up * 1.0f;
            for (int i = Orbs.Count - 1; i >= 0; i--)
            {
                var o = Orbs[i];
                o.Life -= dt;
                var step = o.Vel * dt;
                bool gone = o.Life <= 0f;
                if (!gone && !PlayerDown && DistanceToSegment(body, o.T.position, o.T.position + step) < 0.55f)
                {
                    Damage(LanternEnemy.OrbDamage);
                    gone = true;
                }
                else if (!gone && Physics.Raycast(o.T.position, step.normalized, step.magnitude, ~(1 << LanternRig.ViewLayer), QueryTriggerInteraction.Ignore))
                {
                    gone = true;
                }
                if (gone)
                {
                    if (glitch != null) glitch.Emit(new ParticleSystem.EmitParams { position = o.T.position, applyShapeToPosition = true }, 15);
                    ArenaArt.Kill(o.T.gameObject);
                    Orbs.RemoveAt(i);
                    continue;
                }
                o.T.position += step;
            }
        }

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        private Vector3 FarthestSpawn(Vector3 from)
        {
            var best = world.EnemySpawns[0];
            float bd = -1f;
            foreach (var s in world.EnemySpawns)
            {
                float d = Vector3.Distance(s, from);
                if (d > bd) { bd = d; best = s; }
            }
            return best;
        }

        /// <summary>倒下後回到戰鬥場入口、生命回滿、清掉光球、重新放一隻失序體在遠處。</summary>
        public void Respawn()
        {
            PlayerDown = false;
            Health = MaxHealth;
            HurtFlash = 0f;
            foreach (var o in Orbs) if (o.T != null) ArenaArt.Kill(o.T.gameObject);
            Orbs.Clear();
            Transform start;
            if (world.Marks.TryGetValue("ArenaStart", out start))
            {
                var f = start.forward;
                player.Teleport(start.position, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, 0f);
            }
            foreach (var e in Enemies) if (e != null) ArenaArt.Kill(e.gameObject);
            Enemies.Clear();
            respawnTimer = -1f;
            Spawn(FarthestSpawn(player.transform.position));
        }

        public void Destroy()
        {
            foreach (var o in Orbs) if (o.T != null) ArenaArt.Kill(o.T.gameObject);
            Orbs.Clear();
            ArenaArt.Kill(orbMat);
        }
    }
}
