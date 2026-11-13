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
        /// <summary>移動速度倍率（被聚光照到、在擬態錨定裡會變慢）。</summary>
        public float SpeedScale = 1f;
        /// <summary>不能進入的地方（修好的房間＝安全據點）。</summary>
        public System.Func<Vector3, bool> AvoidZone;
        /// <summary>被安全據點擋在門外的時間（測試用）。</summary>
        public float HeldOutSeconds { get; private set; }

        private Vector3 lungeDir, lungeStart;
        private bool lungeHit;
        private float orbCooldown = 0.8f;
        private float lastDamage;
        // 找路：沿 NavMesh 路徑點走（每 0.5 秒重算一次）。
        private readonly List<Vector3> path = new List<Vector3>();
        private int pathIndex;
        private float repath;
        private Vector3 stuckFrom;
        private float stuckTimer, noPathTimer;
        /// <summary>卡住了（幾秒內幾乎沒移動，或根本找不到路）：由 LanternCombat 換個地方重生。</summary>
        public bool Stuck { get; private set; }
        public int StuckCount { get; private set; }
        public const float StuckSeconds = 3.5f;
        /// <summary>目前是否正沿著 NavMesh 路徑移動（測試用）。</summary>
        public bool UsingPath { get; private set; }

        public void Init()
        {
            Hp = MaxHp;
            Current = State.Chase;
            StateTimer = 0f;
            ResetPath();
        }

        /// <summary>移到新位置（卡住時重生用）。</summary>
        public void Relocate(Vector3 p)
        {
            transform.position = p;
            ResetPath();
            if (Current != State.Stunned && Alive) Current = State.Chase;
        }

        private void ResetPath()
        {
            path.Clear();
            pathIndex = 0;
            repath = 0f;
            stuckFrom = transform.position;
            stuckTimer = 0f;
            noPathTimer = 0f;
            Stuck = false;
        }

        /// <summary>沿 NavMesh 走向玩家；沒有 NavMesh 或找不到路回傳 false（改用舊的直線＋閃避）。</summary>
        private bool NavMove(Vector3 player, float speed, float dt)
        {
            UsingPath = false;
            if (LanternNav.Count == 0) return false;
            repath -= dt;
            if (repath <= 0f)
            {
                repath = 0.5f;
                if (LanternNav.Path(transform.position, player, path)) { noPathTimer = 0f; pathIndex = 1; }
                else { path.Clear(); noPathTimer += 0.5f; }
            }
            if (path.Count == 0) return false;
            if (pathIndex >= path.Count) { repath = 0f; return true; }
            var target = path[pathIndex];
            var d = target - transform.position;
            var flat = new Vector3(d.x, 0f, d.z);
            if (flat.magnitude < 0.12f) { pathIndex++; return true; }
            var step = flat.normalized * speed * dt;
            if (step.sqrMagnitude > flat.sqrMagnitude) step = flat;
            var p = transform.position + step;
            if (AvoidZone != null && AvoidZone(p) && !AvoidZone(transform.position))
            {
                // 安全據點：停在門口，只能看著。
                HeldOutSeconds += dt;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 1f - Mathf.Exp(-8f * dt));
                stuckTimer = 0f;
                stuckFrom = transform.position;
                return true;
            }
            Vector3 on;
            if (LanternNav.Sample(p + Vector3.up * 0.3f, 0.9f, out on)) p.y = on.y;
            transform.position = p;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 1f - Mathf.Exp(-8f * dt));
            UsingPath = true;
            return true;
        }

        private void CheckStuck(float dt, float dist)
        {
            stuckTimer += dt;
            if (noPathTimer > 2.5f) { if (!Stuck) StuckCount++; Stuck = true; return; }
            if (stuckTimer < StuckSeconds) return;
            if (Vector3.Distance(transform.position, stuckFrom) < 0.6f && dist > LungeRange + 0.6f) { StuckCount++; Stuck = true; }
            stuckTimer = 0f;
            stuckFrom = transform.position;
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

        /// <summary>看得到玩家才丟光球（不會隔著牆亂丟）。</summary>
        private bool SeesPlayer(Vector3 player)
        {
            RaycastHit hit;
            var from = transform.position + Vector3.up * 1.3f;
            var to = player + Vector3.up * 1.2f;
            if (!Physics.Linecast(from, to, out hit, ~(1 << LanternRig.ViewLayer), QueryTriggerInteraction.Ignore)) return true;
            return hit.collider is CharacterController || hit.collider.attachedRigidbody != null;
        }

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
            // 不同樓層（上下差超過 1.5 m）不能隔著地板攻擊：當成很遠，繼續找路。
            float dy = Mathf.Abs(to.y);
            to.y = 0f;
            float dist = dy > 1.5f ? Mathf.Max(to.magnitude, 20f) : to.magnitude;
            StateTimer -= dt;
            orbCooldown -= dt;
            float pulse = 0f;
            switch (Current)
            {
                case State.Chase:
                    if (!playerAlive) break;
                    if (dist < LungeRange) { Current = State.WindUp; StateTimer = WindUpSeconds; Say(LanternAudio.EnemyWarn); break; }
                    if (dist > 4.5f && dist < 12f && orbCooldown <= 0f && SeesPlayer(player)) { Current = State.OrbWindUp; StateTimer = OrbWindUpSeconds; Say(LanternAudio.EnemyOrb); break; }
                    if (!NavMove(player, Speed * SpeedScale, dt)) Move(to, Speed * SpeedScale, dt);
                    CheckStuck(dt, dist);
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
                    var lp = transform.position + lungeDir * (LungeDistance / 0.28f) * dt;
                    if (!Blocked(lungeDir, 0.5f) && (AvoidZone == null || !AvoidZone(lp))) transform.position = lp;
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
        /// <summary>戰鬥場測試用：失序體死後自動在遠處重生。住宅與最終戰關閉。</summary>
        public bool AutoRespawn = true;
        /// <summary>倒下後回到哪個標記。</summary>
        public string RespawnMark = "ArenaStart";
        public System.Func<Vector3, bool> InShield;
        public System.Action<Vector3, float> Erode;
        public System.Action<float> OnEnergy;
        public System.Action<string> OnMemory;
        public int MemoriesFound { get; private set; }
        public int EnergyPickups { get; private set; }
        public readonly List<Transform> Drops = new List<Transform>();
        public ParticleSystem Glitch => glitch;
        private Material energyMat, memoryMat;
        private int memoryIndex;

        /// <summary>（暫定）失序體消散時掉出的記憶碎片。正式內容待劇本。</summary>
        public static readonly string[] Memories =
        {
            "（記憶碎片）「媽，我週日可能晚一點到。」",
            "（記憶碎片）檔案館的燈還亮著，她在比對兩份互相矛盾的地址。",
            "（記憶碎片）「這條街的門牌……為什麼會跳過一號？」",
            "（記憶碎片）餐桌上，兩杯茶都沒有喝完。",
        };

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
            energyMat = ArenaArt.Mat(new Color(1f, 0.85f, 0.55f), 0.3f, new Color(3f, 2.2f, 1.1f));
            energyMat.name = "EnergyDrop";
            memoryMat = ArenaArt.Mat(new Color(0.7f, 0.85f, 1f), 0.3f, new Color(1.2f, 1.8f, 2.6f));
            memoryMat.name = "MemoryDrop";
            if (world.EnemySpawns.Count > 0) Spawn(world.EnemySpawns[0]);
        }

        public LanternEnemy Spawn(Vector3 at)
        {
            var e = world.BuildEnemy(at);
            e.Glitch = glitch;
            e.AvoidZone = world.InSafeRoom;
            e.Init();
            Enemies.Add(e);
            return e;
        }

        public void ClearEnemies()
        {
            foreach (var e in Enemies) if (e != null) ArenaArt.Kill(e.gameObject);
            Enemies.Clear();
            foreach (var o in Orbs) if (o.T != null) ArenaArt.Kill(o.T.gameObject);
            Orbs.Clear();
            respawnTimer = -1f;
        }

        private void Drop(Vector3 at)
        {
            var e = ArenaArt.Prim(PrimitiveType.Sphere, world.Root, at + Vector3.up * 0.6f, Vector3.one * 0.18f, energyMat, null, "EnergyDrop", false, false);
            Drops.Add(e);
            if (memoryIndex < Memories.Length && (Kills % 2 == 1 || memoryIndex == 0))
            {
                var m = ArenaArt.Prim(PrimitiveType.Sphere, world.Root, at + Vector3.up * 0.9f + Vector3.right * 0.4f, Vector3.one * 0.14f, memoryMat, null, "MemoryDrop", false, false);
                Drops.Add(m);
            }
        }

        private void StepDrops(Vector3 feet)
        {
            for (int i = Drops.Count - 1; i >= 0; i--)
            {
                var d = Drops[i];
                if (d == null) { Drops.RemoveAt(i); continue; }
                d.position += Vector3.up * Mathf.Sin(time * 3f + i) * 0.002f;
                var flat = new Vector2(d.position.x - feet.x, d.position.z - feet.z);
                if (flat.magnitude < 1.3f && Mathf.Abs(d.position.y - feet.y - 0.8f) < 1.2f)
                {
                    if (d.name == "MemoryDrop")
                    {
                        MemoriesFound++;
                        OnMemory?.Invoke(Memories[Mathf.Min(memoryIndex++, Memories.Length - 1)]);
                        audio?.Play(LanternAudio.Placed, 0.8f);
                    }
                    else
                    {
                        EnergyPickups++;
                        OnEnergy?.Invoke(35f);
                        audio?.Play(LanternAudio.Grab, 0.6f);
                    }
                    ArenaArt.Kill(d.gameObject);
                    Drops.RemoveAt(i);
                }
            }
        }

        public IEnumerable<Component> Living
        {
            get { foreach (var e in Enemies) if (e != null && e.Alive && e.gameObject.activeSelf) yield return e; }
        }

        /// <summary>QA／測試用：玩家不會受傷。</summary>
        public bool Invulnerable;

        public void Damage(float amount)
        {
            if (PlayerDown || amount <= 0f || Invulnerable) return;
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
                // 被聚光照到或在擬態錨定裡：變慢；貼著穩定場會侵蝕它。
                bool exposed = rig.FocusBlend > 0.6f && LanternReveal.IsReal(e.transform.position + Vector3.up * 1.1f);
                bool shielded = InShield != null && InShield(e.transform.position);
                e.SpeedScale = shielded ? 0.3f : exposed ? 0.5f : 1f;
                if (shielded) Erode?.Invoke(e.transform.position, dt * 1.5f);
                float hit = e.gameObject.activeSelf ? e.Step(dt, time, feet, !PlayerDown, Orbs, world.Root, orbMat) : 0f;
                // 卡住了：換到玩家附近、但看不到的地方重新出現。
                // 房間變成安全據點時，裡面的失序體會被推到外面看不到的地方。
                if (e.Alive && e.gameObject.activeSelf && world.InSafeRoom(e.transform.position))
                {
                    Vector3 outside;
                    if (HiddenSpawnNear(feet, world.HouseInterior, out outside, 3f, 18f)) { e.Relocate(outside); Relocations++; }
                }
                if (e.Stuck && e.Alive && Region(e.transform.position) != Region(feet)) e.Relocate(e.transform.position);
                else if (e.Stuck && e.Alive)
                {
                    var area = world.HouseBounds.Contains(feet + Vector3.up) ? world.HouseInterior : world.ArenaBounds;
                    Vector3 to;
                    if (HiddenSpawnNear(feet, area, out to)) { e.Relocate(to); Relocations++; }
                    else e.Relocate(e.transform.position);
                }
                if (hit > 0f && InShield != null && InShield(feet)) { Erode?.Invoke(feet, 2.5f); hit *= 0.3f; }
                Damage(hit);
                if (!e.Alive && counted.Add(e)) { Kills++; Drop(e.transform.position); }
            }
            // 戰鬥場測試：場上沒有活著的失序體時，5 秒後在離玩家最遠的出生點補一隻。
            if (AutoRespawn && respawnTimer < 0f)
            {
                bool any = false;
                foreach (var _ in Living) { any = true; break; }
                if (!any) respawnTimer = EnemyRespawnSeconds;
            }
            if (respawnTimer >= 0f)
            {
                respawnTimer -= dt;
                if (respawnTimer < 0f && AutoRespawn) Spawn(FarthestSpawn(feet));
            }
            StepDrops(feet);

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
                else if (!gone && InShield != null && InShield(o.T.position))
                {
                    gone = true;
                    Erode?.Invoke(o.T.position, 1f);
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

        public int Relocations { get; private set; }

        /// <summary>1＝住宅、2＝戰鬥場、3＝最終戰房間、0＝其他（卡住的失序體只會在玩家所在的區域重新出現）。</summary>
        private int Region(Vector3 p)
        {
            p += Vector3.up;
            var hb = world.HouseBounds;
            hb.Expand(new Vector3(0f, 6f, 0f));
            if (hb.Contains(p)) return 1;
            if (world.ArenaBounds.Contains(p)) return 2;
            if (world.BossBounds.Contains(p)) return 3;
            return 0;
        }
        private readonly List<Vector3> pathScratch = new List<Vector3>();

        /// <summary>
        /// 在玩家附近、但現在看不到的地方找一個出生點：沿 NavMesh 走過來要 minPath～maxPath 公尺，
        /// 而且不在畫面裡（或被牆擋住）。找不到回傳 false。
        /// </summary>
        public bool HiddenSpawnNear(Vector3 feet, Bounds area, out Vector3 spawn, float minPath = 5.5f, float maxPath = 16f)
        {
            spawn = feet;
            if (LanternNav.Count == 0) return false;
            var cam = rig.Camera;
            float best = float.MaxValue;
            bool found = false;
            int nS = 0, nY = 0, nA = 0, nP = 0, nL = 0, nV = 0;
            for (int k = 0; k < 40; k++)
            {
                float a = k * 137.5f * Mathf.Deg2Rad;
                float r = 4.5f + (k % 5) * 1.6f;
                var p = feet + new Vector3(Mathf.Cos(a) * r, 0.4f, Mathf.Sin(a) * r);
                Vector3 on;
                if (!LanternNav.Sample(p, 1.5f, out on)) { nS++; continue; }
                if (Mathf.Abs(on.y - feet.y) > 1.6f) { nY++; continue; }
                if (!area.Contains(on + Vector3.up) || world.InSafeRoom(on)) { nA++; continue; }
                if (!LanternNav.Path(on, feet, pathScratch)) { nP++; continue; }
                float len = LanternNav.Length(pathScratch);
                if (len < minPath || len > maxPath) { nL++; continue; }
                if (cam != null && Visible(cam, on + Vector3.up * 1.1f)) { nV++; continue; }
                if (len < best) { best = len; spawn = on; found = true; }
            }
            LastSpawnDebug = "sample " + nS + " floor " + nY + " area " + nA + " path " + nP + " len " + nL + " visible " + nV + " found " + found;
            return found;
        }

        public string LastSpawnDebug { get; private set; } = "";

        private static bool Visible(Camera cam, Vector3 p)
        {
            var vp = cam.WorldToViewportPoint(p);
            if (vp.z <= 0f || vp.x < -0.15f || vp.x > 1.15f || vp.y < -0.15f || vp.y > 1.15f) return false;
            RaycastHit hit;
            if (!Physics.Linecast(cam.transform.position, p, out hit, ~(1 << LanternRig.ViewLayer), QueryTriggerInteraction.Ignore)) return true;
            return hit.collider.attachedRigidbody != null || Vector3.Distance(hit.point, p) < 0.5f;
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
            if (world.Marks.TryGetValue(RespawnMark, out start) || world.Marks.TryGetValue("ArenaStart", out start))
            {
                var f = start.forward;
                player.Teleport(start.position, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, 0f);
            }
            foreach (var e in Enemies) if (e != null) ArenaArt.Kill(e.gameObject);
            Enemies.Clear();
            respawnTimer = -1f;
            // 戰鬥場測試：重新放一隻；住宅裡倒下則清掉追兵（給玩家喘息）。
            if (AutoRespawn && RespawnMark == "ArenaStart") Spawn(FarthestSpawn(player.transform.position));
        }

        public void Destroy()
        {
            foreach (var o in Orbs) if (o.T != null) ArenaArt.Kill(o.T.gameObject);
            Orbs.Clear();
            foreach (var d in Drops) if (d != null) ArenaArt.Kill(d.gameObject);
            Drops.Clear();
            ArenaArt.Kill(orbMat);
            ArenaArt.Kill(energyMat);
            ArenaArt.Kill(memoryMat);
        }
    }
}
