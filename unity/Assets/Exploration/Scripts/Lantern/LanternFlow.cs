using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>飛向玩家的話語（最終戰）：傷人的話會傷人；被穩定能量打中會碎掉，浮現同一段回憶裡溫暖的另一面。</summary>
    public sealed class LanternPhrase : MonoBehaviour
    {
        public string Warm;
        public Vector3 Vel;
        public float Life = 5f;
        public bool Shattered { get; private set; }
        public System.Action<LanternPhrase> OnShatter;

        public void Shatter()
        {
            if (Shattered) return;
            Shattered = true;
            OnShatter?.Invoke(this);
        }
    }

    /// <summary>
    /// 最後的反撲（大型失序體，灰盒）：比一般失序體大、慢、耐打。
    /// - 話語齊射：前兆 0.8 秒後丟出 2–3 句「傷人的話」，慢慢飛向玩家（可以閃、也可以射碎）。
    /// - 震地：前兆 1.2 秒（地上出現光圈），範圍 4.5 m 內扣 30，退開就能躲。
    /// - 被蓄力射擊（≥0.7）或丟出的物件打中才會暈眩；暈眩中傷害加倍。
    /// </summary>
    public sealed class LanternBoss : MonoBehaviour
    {
        public enum State { Approach, VolleyWindUp, SlamWindUp, Recover, Stunned, Dying, Dead }
        public const float MaxHp = 24f;
        public const float SlamRadius = 4.5f;
        public const float SlamDamage = 30f;
        public const float PhraseDamage = 12f;

        public State Current { get; private set; } = State.Approach;
        public float Hp { get; private set; } = MaxHp;
        public float StateTimer { get; private set; }
        public bool Alive => Current != State.Dying && Current != State.Dead;
        public Transform Body;
        public Material CoreMat;
        public Transform SlamRing;
        public AudioSource Voice;
        public ParticleSystem Glitch;
        private float attackCooldown = 1.5f;
        private int nextAttack;

        public void Hit(float strength, bool thrown)
        {
            if (!Alive) return;
            float dmg = thrown ? 2.5f : 1f + 2f * Mathf.Clamp01(strength);
            if (Current == State.Stunned || Current == State.Recover) dmg *= 2f;
            Hp -= dmg;
            if (Glitch != null) Glitch.Emit(new ParticleSystem.EmitParams { position = transform.position + Vector3.up * 2f, applyShapeToPosition = true }, 40);
            if (Voice != null && Application.isPlaying) Voice.PlayOneShot(LanternAudio.EnemyHit, 1f);
            if (Hp <= 0f)
            {
                Current = State.Dying;
                StateTimer = 2.2f;
                if (SlamRing != null) SlamRing.gameObject.SetActive(false);
                if (Voice != null && Application.isPlaying) Voice.PlayOneShot(LanternAudio.EnemyDeath, 1f);
                return;
            }
            if (thrown || strength >= 0.7f)
            {
                Current = State.Stunned;
                StateTimer = thrown ? 2.2f : 1.6f;
                if (SlamRing != null) SlamRing.gameObject.SetActive(false);
            }
        }

        /// <summary>回傳這一格的範圍傷害；需要丟話語時呼叫 volley。</summary>
        public float Step(float dt, float time, Vector3 player, System.Action<int> volley)
        {
            float damage = 0f;
            var to = player - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            StateTimer -= dt;
            attackCooldown -= dt;
            float pulse = 0f;
            switch (Current)
            {
                case State.Approach:
                    if (dist > 3f) transform.position += to.normalized * 1.3f * dt;
                    if (dist > 0.1f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 1f - Mathf.Exp(-3f * dt));
                    if (attackCooldown <= 0f)
                    {
                        bool slam = dist < SlamRadius + 0.5f && nextAttack % 2 == 1 || dist < 3f;
                        nextAttack++;
                        if (slam) { Current = State.SlamWindUp; StateTimer = 1.2f; if (SlamRing != null) SlamRing.gameObject.SetActive(true); Say(LanternAudio.EnemyWarn); }
                        else { Current = State.VolleyWindUp; StateTimer = 0.8f; Say(LanternAudio.EnemyOrb); }
                    }
                    break;
                case State.VolleyWindUp:
                    pulse = 1f - Mathf.Clamp01(StateTimer / 0.8f);
                    if (StateTimer <= 0f) { volley?.Invoke(2 + (nextAttack % 2)); Current = State.Recover; StateTimer = 0.8f; attackCooldown = 3.2f; }
                    break;
                case State.SlamWindUp:
                    pulse = 1f - Mathf.Clamp01(StateTimer / 1.2f);
                    if (SlamRing != null)
                    {
                        SlamRing.position = new Vector3(transform.position.x, transform.position.y + 0.03f, transform.position.z);
                        float r = SlamRadius * 2f * Mathf.Lerp(0.2f, 1f, pulse);
                        SlamRing.localScale = new Vector3(r, 0.02f, r);
                    }
                    if (StateTimer <= 0f)
                    {
                        if (dist < SlamRadius) damage = SlamDamage;
                        if (Glitch != null) Glitch.Emit(new ParticleSystem.EmitParams { position = transform.position + Vector3.up * 0.3f, applyShapeToPosition = true }, 120);
                        if (SlamRing != null) SlamRing.gameObject.SetActive(false);
                        Say(LanternAudio.EnemyLunge);
                        Current = State.Recover;
                        StateTimer = 1.3f;
                        attackCooldown = 2.8f;
                    }
                    break;
                case State.Recover:
                case State.Stunned:
                    if (StateTimer <= 0f) Current = State.Approach;
                    break;
                case State.Dying:
                    float k = Mathf.Clamp01(StateTimer / 2.2f);
                    if (Body != null) Body.localScale = new Vector3(k, Mathf.Lerp(2f, 1f, k), k) * 1.8f;
                    if (Glitch != null && Random.value < 0.8f) Glitch.Emit(new ParticleSystem.EmitParams { position = transform.position + Vector3.up * Random.Range(0.3f, 3.5f) + Random.insideUnitSphere * 0.6f, applyShapeToPosition = true }, 5);
                    if (StateTimer <= 0f)
                    {
                        Current = State.Dead;
                        LanternReveal.AddAnchor(transform.position + Vector3.up, 6f);
                        gameObject.SetActive(false);
                    }
                    break;
            }
            if (Body != null && Alive)
            {
                float s = 1.8f * (1f + pulse * 0.12f + Mathf.Sin(time * 1.6f) * 0.02f);
                Body.localScale = new Vector3(s, 1.8f * (1f + pulse * 0.06f), s);
                Body.localRotation = Current == State.Stunned ? Quaternion.Euler(Mathf.Sin(time * 50f) * 4f, 0f, Mathf.Cos(time * 47f) * 4f) : Quaternion.identity;
            }
            if (CoreMat != null && Alive)
            {
                float glow = Current == State.Stunned ? 0.3f : 1f + pulse * pulse * 8f;
                CoreMat.SetColor("_AnomalyEmission", new Color(0.5f, 0.1f, 0.8f) * glow);
                CoreMat.SetColor("_EmissionColor", new Color(1.4f, 0.9f, 0.5f) * glow);
            }
            return damage;
        }

        private void Say(AudioClip c)
        {
            if (Voice != null && Application.isPlaying) Voice.PlayOneShot(c, 1f);
        }
    }

    /// <summary>
    /// 第十七號門的流程（原型）：
    /// 1. 在住宅裡自由探索，修復線索（順序不限）。每修好一個，異域就反撲：住宅燈光熄滅一陣子、失序體從遠處的房間出現。
    /// 2. 最後一個線索完成的瞬間，場景崩壞，被拉進異域深處（戰鬥場），面對最後的反撲（大型失序體）。
    ///    戰鬥中空中浮現母女之間的話；傷人的話會飛向玩家，射碎後浮現溫暖的另一面（文字皆為暫定，待正式劇本）。
    /// 3. 擊倒後結束。
    /// </summary>
    public sealed class LanternFlow
    {
        public enum Phase { Explore, Collapsing, Boss, Ended }

        public const float CollapseSeconds = 2.6f;
        public const float BacklashLightSeconds = 7f;
        public const int MaxHouseEnemies = 2;

        /// <summary>（暫定）傷人的話與同一段回憶裡溫暖的另一面。正式對白待劇本。</summary>
        public static readonly string[,] Lines =
        {
            { "我都是為你好。", "週日晚餐，還是多擺了一副碗筷。" },
            { "我不需要你管。", "冰箱上的便條：記得吃飯。" },
            { "你為什麼什麼都不跟我說？", "那封寫給妳的信，我一直沒寄。" },
            { "你從來沒問過我想要什麼。", "她記得妳小時候最喜歡的那首歌。" },
            { "這個家你還要不要回來？", "鑰匙還放在老地方。" },
            { "我已經長大了。", "門框上還留著每年量身高的刻痕。" },
        };

        public Phase Current { get; private set; } = Phase.Explore;
        public int Repaired { get; private set; }
        public int Total => world.Clues.Count;
        public int Backlashes { get; private set; }
        public LanternBoss Boss { get; private set; }
        public float CollapseTimer { get; private set; }
        public readonly List<LanternPhrase> Phrases = new List<LanternPhrase>();
        public readonly List<Transform> Floaters = new List<Transform>();
        public int WarmLinesShown { get; private set; }
        public string Toast { get; private set; } = "";
        public float ToastTimer { get; private set; }
        public float CollapseFade => Current == Phase.Collapsing ? 1f - Mathf.Clamp01(CollapseTimer / CollapseSeconds * 1.4f - 0.2f) : 0f;

        private readonly LanternGraybox world;
        private readonly FirstPersonController player;
        private readonly LanternRig rig;
        private readonly LanternCombat combat;
        private readonly LanternAudio audio;
        private readonly Camera cam;
        private float lightTimer;
        private readonly HashSet<LanternClue> handled = new HashSet<LanternClue>();
        private float floatTimer;
        private float time;
        private int lineIndex;
        private readonly Material phraseMat, warmMat;
        private Font font;

        public LanternFlow(LanternGraybox world, FirstPersonController player, LanternRig rig, LanternCombat combat, LanternAudio audio, Camera cam)
        {
            this.world = world;
            this.player = player;
            this.rig = rig;
            this.combat = combat;
            this.audio = audio;
            this.cam = cam;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            phraseMat = new Material(font.material) { name = "PhraseText" };
            phraseMat.color = new Color(1f, 0.55f, 0.5f);
            warmMat = new Material(font.material) { name = "WarmText" };
            warmMat.color = new Color(1f, 0.9f, 0.65f);
        }

        public IEnumerable<Component> Targets
        {
            get
            {
                if (Boss != null && Boss.Alive) yield return Boss;
                foreach (var p in Phrases) if (p != null && !p.Shattered) yield return p;
            }
        }

        public void ShowToast(string s, float seconds = 3.5f)
        {
            Toast = s;
            ToastTimer = seconds;
        }

        /// <summary>住宅重建或重新開始時呼叫。</summary>
        public void ResetState()
        {
            Current = Phase.Explore;
            Repaired = 0;
            Backlashes = 0;
            handled.Clear();
            CollapseTimer = 0f;
            ClearBossStuff();
            world.HouseLightScale = 1f;
            combat.AutoRespawn = true;
            combat.RespawnMark = "HouseEntry";
        }

        private void ClearBossStuff()
        {
            if (Boss != null) ArenaArt.Kill(Boss.gameObject);
            Boss = null;
            foreach (var p in Phrases) if (p != null) ArenaArt.Kill(p.gameObject);
            Phrases.Clear();
            foreach (var f in Floaters) if (f != null) ArenaArt.Kill(f.gameObject);
            Floaters.Clear();
        }

        public void Step(float dt)
        {
            time += dt;
            if (ToastTimer > 0f) ToastTimer -= dt;
            switch (Current)
            {
                case Phase.Explore: StepExplore(dt); break;
                case Phase.Collapsing:
                    CollapseTimer -= dt;
                    rig.Shake(0.05f);
                    if (CollapseTimer <= 0f) EnterBoss();
                    break;
                case Phase.Boss: StepBoss(dt); break;
            }
            StepFloaters(dt);
        }

        private bool InHouse => world.HouseBounds.Contains(player.transform.position + Vector3.up);

        private void StepExplore(float dt)
        {
            if (InHouse) { combat.RespawnMark = "HouseEntry"; combat.AutoRespawn = false; }
            else if (world.ArenaBounds.Contains(player.transform.position + Vector3.up)) { combat.RespawnMark = "ArenaStart"; combat.AutoRespawn = true; }

            // 聚光照著全家福＝固定真相。
            foreach (var c in world.Clues)
            {
                if (c == null || c.Done || c.Type != LanternClue.Kind.Focus) continue;
                var p = c.transform.position;
                var d = p - cam.transform.position;
                if (rig.FocusBlend > 0.7f && d.magnitude < 7f && Vector3.Angle(cam.transform.forward, d) < 14f && LanternReveal.IsReal(p))
                    c.AddFocus(dt / 1.5f);
            }
            for (int i = 0; i < world.Clues.Count; i++)
            {
                var c = world.Clues[i];
                if (c == null) continue;
                c.Refresh();
                if (!c.Done || !handled.Add(c)) continue;
                Repaired++;
                if (c.Type == LanternClue.Kind.Focus) LanternReveal.AddAnchor(c.transform.position, 1.4f);
                audio?.Play(LanternAudio.RestoreDone, 0.8f);
                if (Repaired >= Total) { BeginCollapse(); return; }
                Backlash();
            }
            if (lightTimer > 0f)
            {
                lightTimer -= dt;
                world.HouseLightScale = Mathf.MoveTowards(world.HouseLightScale, lightTimer > 0f ? 0.08f : 1f, dt * 2f);
            }
            else world.HouseLightScale = Mathf.MoveTowards(world.HouseLightScale, 1f, dt * 0.5f);
        }

        /// <summary>修好一個線索 → 異域反撲：住宅熄燈一陣子，失序體從離玩家最遠的同樓層房間出現。</summary>
        public void Backlash()
        {
            Backlashes++;
            lightTimer = BacklashLightSeconds;
            ShowToast("（房子在反抗……有什麼東西從暗處醒來了）");
            audio?.Play(LanternAudio.EnemyWarn, 0.6f);
            rig.Shake(0.04f);
            int alive = 0;
            foreach (var e in combat.Living)
                if (world.HouseBounds.Contains(e.transform.position + Vector3.up)) alive++;
            int spawn = Mathf.Min(Repaired >= 3 ? 2 : 1, MaxHouseEnemies - alive);
            var feet = player.transform.position;
            var candidates = new List<Vector3>();
            foreach (var s in world.HouseSpawns) if (Mathf.Abs(s.y - feet.y) < 1.5f && Vector3.Distance(s, feet) > 5f) candidates.Add(s);
            candidates.Sort((a, b) => Vector3.Distance(b, feet).CompareTo(Vector3.Distance(a, feet)));
            for (int i = 0; i < spawn && i < candidates.Count; i++) combat.Spawn(candidates[i]);
        }

        public void BeginCollapse()
        {
            if (Current != Phase.Explore) return;
            Current = Phase.Collapsing;
            CollapseTimer = CollapseSeconds;
            ShowToast("（真相快要拼回來了——整棟房子開始崩塌）", 3f);
            audio?.Play(LanternAudio.EnemyDeath, 1f);
        }

        private void EnterBoss()
        {
            Current = Phase.Boss;
            combat.ClearEnemies();
            combat.AutoRespawn = false;
            combat.RespawnMark = "ArenaStart";
            world.HouseLightScale = 1f;
            Transform start;
            if (world.Marks.TryGetValue("ArenaStart", out start))
            {
                var f = start.forward;
                player.Teleport(start.position, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, 0f);
            }
            Boss = world.BuildBoss(new Vector3(38f, 0f, 12f), combat.Glitch);
            ShowToast("（這裡是她的記憶深處）", 3f);
        }

        private void StepBoss(float dt)
        {
            if (Boss == null) return;
            if (combat.PlayerDown) return;
            if (Boss.gameObject.activeSelf)
                combat.Damage(Boss.Step(dt, time, player.transform.position, Volley));
            if (Boss.Current == LanternBoss.State.Dead && Current == Phase.Boss)
            {
                Current = Phase.Ended;
                foreach (var p in Phrases) if (p != null) ArenaArt.Kill(p.gameObject);
                Phrases.Clear();
                ShowToast("", 0f);
                audio?.Play(LanternAudio.RestoreDone, 1f);
                return;
            }
            // 話語飛行。
            var body = player.transform.position + Vector3.up * 1.2f;
            for (int i = Phrases.Count - 1; i >= 0; i--)
            {
                var p = Phrases[i];
                if (p == null) { Phrases.RemoveAt(i); continue; }
                if (p.Shattered) { Phrases.RemoveAt(i); continue; }
                p.Life -= dt;
                var want = (body - p.transform.position).normalized * p.Vel.magnitude;
                p.Vel = Vector3.RotateTowards(p.Vel, want, Mathf.Deg2Rad * 25f * dt, 0f);
                p.transform.position += p.Vel * dt;
                Billboard(p.transform);
                if (Vector3.Distance(p.transform.position, body) < 0.7f)
                {
                    combat.Damage(LanternBoss.PhraseDamage);
                    ArenaArt.Kill(p.gameObject);
                    Phrases.RemoveAt(i);
                }
                else if (p.Life <= 0f)
                {
                    ArenaArt.Kill(p.gameObject);
                    Phrases.RemoveAt(i);
                }
            }
            // 背景裡飄著的話（不會傷人）。
            floatTimer -= dt;
            if (floatTimer <= 0f)
            {
                floatTimer = 2.2f;
                int k = Random.Range(0, Lines.GetLength(0));
                var at = new Vector3(Random.Range(23f, 41f), Random.Range(1.2f, 3.4f), Random.Range(3f, 21f));
                // 只在離玩家 6 m 以外浮現，不會突然擋在眼前。
                var away = at - player.transform.position;
                away.y = 0f;
                if (away.magnitude < 6f) at += (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward) * (6f - away.magnitude);
                Floater(Lines[k, Random.value < 0.5f ? 0 : 1], at, Random.value < 0.5f ? phraseMat : warmMat, 0.6f, 5f);
            }
        }

        private void Volley(int count)
        {
            if (Boss == null) return;
            for (int i = 0; i < count; i++)
            {
                int k = lineIndex++ % Lines.GetLength(0);
                var from = Boss.transform.position + Vector3.up * (2.6f + i * 0.5f) + Boss.transform.right * (i - (count - 1) / 2f) * 1.4f;
                var go = new GameObject("Phrase");
                go.transform.SetParent(world.Root, false);
                go.transform.position = from;
                var tm = Text(go.transform, Lines[k, 0], phraseMat, 1f);
                var col = go.AddComponent<BoxCollider>();
                col.size = new Vector3(Mathf.Max(2f, Lines[k, 0].Length * 0.5f), 0.9f, 0.4f);
                var ph = go.AddComponent<LanternPhrase>();
                ph.Warm = Lines[k, 1];
                ph.Vel = (player.transform.position + Vector3.up * 1.2f - from).normalized * 4.2f;
                ph.Life = 6f;
                ph.OnShatter = OnPhraseShatter;
                Billboard(go.transform);
                Phrases.Add(ph);
            }
        }

        private void OnPhraseShatter(LanternPhrase p)
        {
            WarmLinesShown++;
            Floater(p.Warm, p.transform.position + Vector3.up * 0.2f, warmMat, 0.9f, 4.5f);
            combat.Glitch?.Emit(new ParticleSystem.EmitParams { position = p.transform.position, applyShapeToPosition = true }, 40);
            ArenaArt.Kill(p.gameObject);
        }

        private TextMesh Text(Transform parent, string s, Material m, float scale)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = s;
            tm.fontSize = 64;
            tm.characterSize = 0.085f * scale;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            return tm;
        }

        private void Floater(string s, Vector3 at, Material m, float scale, float life)
        {
            var go = new GameObject("FloatingLine");
            go.transform.SetParent(world.Root, false);
            go.transform.position = at;
            Text(go.transform, s, m, scale);
            Billboard(go.transform);
            go.AddComponent<FloaterLife>().Init(life);
            Floaters.Add(go.transform);
        }

        private void StepFloaters(float dt)
        {
            for (int i = Floaters.Count - 1; i >= 0; i--)
            {
                var f = Floaters[i];
                if (f == null) { Floaters.RemoveAt(i); continue; }
                var life = f.GetComponent<FloaterLife>();
                life.Left -= dt;
                f.position += Vector3.up * 0.15f * dt;
                Billboard(f);
                var tm = f.GetComponentInChildren<TextMesh>();
                if (tm != null) { var c = tm.color; c.a = Mathf.Clamp01(life.Left / 1.2f) * Mathf.Clamp01((life.Total - life.Left) / 0.6f); tm.color = c; }
                if (life.Left <= 0f) { ArenaArt.Kill(f.gameObject); Floaters.RemoveAt(i); }
            }
        }

        private void Billboard(Transform t)
        {
            var d = t.position - cam.transform.position;
            if (d.sqrMagnitude > 1e-4f) t.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }

        public void Destroy()
        {
            ClearBossStuff();
            ArenaArt.Kill(phraseMat);
            ArenaArt.Kill(warmMat);
        }
    }

    public sealed class FloaterLife : MonoBehaviour
    {
        public float Left, Total;
        public void Init(float seconds) { Left = Total = seconds; }
    }
}
