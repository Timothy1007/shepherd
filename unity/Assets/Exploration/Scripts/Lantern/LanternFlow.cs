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

        /// <summary>站在燈光裡（由流程每個影格設定）。暗處幾乎打不痛。</summary>
        public bool Lit;
        public const float DarkDamageScale = 0.1f;
        /// <summary>生命掉到這些比例時，它會把所有燈熄掉。</summary>
        public static readonly float[] BlackoutAt = { 2f / 3f, 1f / 3f };
        private int thresholdsPassed;
        /// <summary>被打中：(位置, 實際傷害, 是否在光裡)。</summary>
        public System.Action<Vector3, float, bool> OnDamaged;
        /// <summary>生命掉到門檻（第幾個）。</summary>
        public System.Action<int> OnThreshold;
        public float Flash { get; private set; }
        public Material ShellMat;
        public int DarkHits { get; private set; }

        public void Hit(float strength, bool thrown)
        {
            if (!Alive) return;
            float dmg = thrown ? 2.5f : 1f + 2f * Mathf.Clamp01(strength);
            if (Lit && (Current == State.Stunned || Current == State.Recover)) dmg *= 2f;
            if (!Lit) { dmg *= DarkDamageScale; DarkHits++; }
            Hp -= dmg;
            Flash = Lit ? 1f : 0.35f;
            OnDamaged?.Invoke(transform.position + Vector3.up * 2.6f, dmg, Lit);
            if (Glitch != null) Glitch.Emit(new ParticleSystem.EmitParams { position = transform.position + Vector3.up * 2f, applyShapeToPosition = true }, Lit ? 50 : 10);
            if (Voice != null && Application.isPlaying) Voice.PlayOneShot(LanternAudio.EnemyHit, Lit ? 1f : 0.4f);
            if (Hp <= 0f)
            {
                Current = State.Dying;
                StateTimer = 2.2f;
                if (SlamRing != null) SlamRing.gameObject.SetActive(false);
                if (Voice != null && Application.isPlaying) Voice.PlayOneShot(LanternAudio.EnemyDeath, 1f);
                return;
            }
            if (thresholdsPassed < BlackoutAt.Length && Hp <= MaxHp * BlackoutAt[thresholdsPassed])
            {
                // 生命掉到門檻：嘶吼、把燈全部熄掉。
                OnThreshold?.Invoke(thresholdsPassed);
                thresholdsPassed++;
                Current = State.Recover;
                StateTimer = 1.4f;
                if (SlamRing != null) SlamRing.gameObject.SetActive(false);
                Say(LanternAudio.EnemyWarn);
                return;
            }
            if (Lit && (thrown || strength >= 0.7f))
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
            // 被打中的閃白（在光裡＝亮白；暗處＝一下暗紫，表示沒什麼效果）。
            Flash = Mathf.MoveTowards(Flash, 0f, dt * 6f);
            if (ShellMat != null)
            {
                ShellMat.SetColor("_EmissionColor", new Color(0.12f, 0.08f, 0.05f) + new Color(3f, 2.6f, 2.2f) * Flash * (Lit ? 1f : 0.2f));
                ShellMat.SetColor("_AnomalyEmission", new Color(0.25f, 0.05f, 0.35f) * Flash);
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
    /// 1. 在住宅裡自由探索，修復線索（順序不限）。修好一個線索，那個房間就整間永久恢復真實（從線索處擴散開來）。
    ///    每修好一個，異域就反撲：燈先閃（預警），然後全部熄滅，失序體在玩家附近、看不到的地方出現；打倒這一波，燈才回來。
    /// 2. 最後一個線索完成時，場景崩壞，被拉進她的記憶深處（放大、歪斜的客廳），面對最後的反撲。
    ///    它在暗處幾乎打不痛：射擊牆上的電燈開關，把它引進光裡才會受傷；生命掉到 2/3、1/3 時它會把燈全部熄掉。
    ///    戰鬥中空中浮現母女之間的話；傷人的話會飛向玩家，射碎後浮現溫暖的另一面（文字皆為暫定，待正式劇本）。
    /// 3. 擊倒後：消散 → 燈回來 → 回到恢復真實的家（所有房間都是真的、有電、溫暖）→ 全家福恢復 → 幾行字 → 結案卡。
    /// </summary>
    public sealed class LanternFlow
    {
        public enum Phase { Explore, Collapsing, Boss, Ending, Ended }

        public const float CollapseSeconds = 2.6f;
        public const int MaxHouseEnemies = 2;
        /// <summary>結局演出的長度（秒）：淡出、回到家、幾行字、結案卡。</summary>
        public const float EndingSeconds = 15f;

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

        /// <summary>（暫定）結局的幾行字。</summary>
        public static readonly string[] EndingLines =
        {
            "（暫定）燈都亮了。房子終於安靜下來。",
            "（暫定）壁爐上的全家福裡，又多了一個人。",
            "（暫定）艾琳還是沒有回來。",
            "（暫定）但這一次，有人記得她曾經在這裡。",
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
        /// <summary>全畫面變黑的程度（崩壞、結局轉場）。</summary>
        public float Fade
        {
            get
            {
                if (Current == Phase.Collapsing) return 1f - Mathf.Clamp01(CollapseTimer / CollapseSeconds * 1.4f - 0.2f);
                if (Current == Phase.Ending) return EndingTime < 1.4f ? Mathf.Clamp01(EndingTime / 1.2f) : 1f - Mathf.Clamp01((EndingTime - 1.6f) / 1.4f);
                return 0f;
            }
        }
        public float CollapseFade => Fade;
        /// <summary>反撲中：燈全熄，直到這一波失序體被打倒。</summary>
        public bool WaveActive { get; private set; }
        public int WavesCleared { get; private set; }
        public float EndingTime { get; private set; }
        public int EndingLine { get; private set; } = -1;
        /// <summary>最後的反撲在暗處被打中的次數（第一次會提示要開燈）。</summary>
        public int DarkHintShown { get; private set; }
        public int Blackouts { get; private set; }
        public int DamageNumbers { get; private set; }
        public float BossLitShare { get; private set; }

        private readonly LanternGraybox world;
        private readonly FirstPersonController player;
        private readonly LanternRig rig;
        private readonly LanternCombat combat;
        private readonly LanternAudio audio;
        private readonly Camera cam;
        private readonly HashSet<LanternClue> handled = new HashSet<LanternClue>();
        /// <summary>修好之後先安靜一下、燈開始閃（預警），然後才反撲。</summary>
        public const float WarningSeconds = 1.6f;
        public float WarningTimer { get; private set; } = -1f;
        private int pendingBacklashes;
        private bool pendingCollapse;
        public bool PowerAnnounced { get; private set; }
        private float floatTimer;
        private float time;
        private int lineIndex;
        private float darkHintCooldown;
        private readonly Material phraseMat, warmMat, dmgMat, dullMat;
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
            dmgMat = new Material(font.material) { name = "DamageText" };
            dmgMat.color = new Color(1f, 0.95f, 0.75f);
            dullMat = new Material(font.material) { name = "DullText" };
            dullMat.color = new Color(0.6f, 0.62f, 0.7f);
        }

        public IEnumerable<Component> Targets
        {
            get
            {
                if (Boss != null && Boss.Alive) yield return Boss;
                foreach (var p in Phrases) if (p != null && !p.Shattered) yield return p;
                if (Current == Phase.Boss) foreach (var s in world.Switches) if (s != null && !s.On) yield return s;
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
            WavesCleared = 0;
            Blackouts = 0;
            WaveActive = false;
            handled.Clear();
            WarningTimer = -1f;
            pendingBacklashes = 0;
            pendingCollapse = false;
            PowerAnnounced = false;
            CollapseTimer = 0f;
            EndingTime = 0f;
            EndingLine = -1;
            ClearBossStuff();
            world.HouseLightScale = 1f;
            world.BossBlackout();
            LanternReveal.ClearRooms();
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
                    world.HouseLightScale = Mathf.PerlinNoise(time * 22f, 0.3f) > 0.5f ? 0.6f : 0f;
                    if (CollapseTimer <= 0f) EnterBoss();
                    break;
                case Phase.Boss: StepBoss(dt); break;
                case Phase.Ending: StepEnding(dt); break;
            }
            StepFloaters(dt);
        }

        private bool InHouse => world.HouseBounds.Contains(player.transform.position + Vector3.up);

        private int HouseEnemiesAlive
        {
            get
            {
                int n = 0;
                foreach (var e in combat.Living) if (world.HouseBounds.Contains(e.transform.position + Vector3.up)) n++;
                return n;
            }
        }

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
                // 修好這間房的線索：整間房從線索處開始，永久恢復成真實的家。
                if (c.HasRoom) LanternReveal.AddRoom(c.Room, c.transform.position);
                else if (c.Type == LanternClue.Kind.Focus) LanternReveal.AddAnchor(c.transform.position, 1.4f);
                audio?.Play(LanternAudio.RestoreDone, 0.8f);
                if (Repaired >= Total) pendingCollapse = true;
                else pendingBacklashes++;
                if (WarningTimer < 0f) WarningTimer = pendingCollapse ? 1.2f : WarningSeconds;
                else if (pendingCollapse) WarningTimer = Mathf.Min(WarningTimer, 1.2f);
                ShowToast("（" + c.Label + "恢復了——這個房間變回原本的樣子）", 2.5f);
            }
            // 電箱修好：整棟燈亮起（只是把廢墟照亮，不會讓東西變回真的）。
            if (world.FuseBox != null && world.FuseBox.Done && !world.HousePowered)
            {
                world.HousePowered = true;
                PowerAnnounced = true;
                ShowToast("（電來了——燈亮了。但照出來的，還是一棟廢墟）", 3.5f);
            }
            // 預警：燈快速閃爍、低鳴，然後才反撲／崩壞。
            if (WarningTimer >= 0f)
            {
                WarningTimer -= dt;
                world.HouseLightScale = Mathf.PerlinNoise(time * 18f, 0.7f) > 0.45f ? 1f : 0.1f;
                if (WarningTimer < 0f)
                {
                    if (pendingCollapse) { pendingCollapse = false; pendingBacklashes = 0; BeginCollapse(); return; }
                    while (pendingBacklashes > 0) { pendingBacklashes--; Backlash(); }
                }
                return;
            }
            // 反撲中：全黑，直到這一波被打倒。
            if (WaveActive)
            {
                world.HouseLightScale = Mathf.MoveTowards(world.HouseLightScale, 0f, dt * 6f);
                if (HouseEnemiesAlive == 0)
                {
                    WaveActive = false;
                    WavesCleared++;
                    ShowToast("（失序體散去了……燈又亮了起來）", 3f);
                    audio?.Play(LanternAudio.Placed, 0.7f);
                }
                return;
            }
            world.HouseLightScale = Mathf.MoveTowards(world.HouseLightScale, 1f, dt * 1.5f);
        }

        /// <summary>修好一個線索 → 異域反撲：燈全部熄滅，失序體在玩家附近、看不到的地方出現；打倒這一波，燈才回來。</summary>
        public void Backlash()
        {
            Backlashes++;
            ShowToast("（燈全熄了……有什麼東西從暗處醒來了）");
            audio?.Play(LanternAudio.EnemyWarn, 0.6f);
            rig.Shake(0.04f);
            world.HouseLightScale = 0f;
            int spawn = Mathf.Min(Repaired >= 3 ? 2 : 1, MaxHouseEnemies - HouseEnemiesAlive);
            var feet = player.transform.position;
            int made = 0;
            for (int i = 0; i < spawn; i++)
            {
                Vector3 at;
                if (combat.HiddenSpawnNear(feet, world.HouseInterior, out at, 5.5f + i * 2f)) { combat.Spawn(at); made++; }
            }
            if (made < spawn)
            {
                // 找不到合適的位置：退回固定的出生點（同樓層、離玩家 5 m 以上，越近越好）。
                var candidates = new List<Vector3>();
                foreach (var s in world.HouseSpawns) if (Mathf.Abs(s.y - feet.y) < 1.5f && Vector3.Distance(s, feet) > 5f) candidates.Add(s);
                candidates.Sort((a, b) => Vector3.Distance(a, feet).CompareTo(Vector3.Distance(b, feet)));
                for (int i = 0; made < spawn && i < candidates.Count; i++) { combat.Spawn(candidates[i]); made++; }
            }
            WaveActive = true;
        }

        /// <summary>QA 用：直接完成全部線索並進入崩壞（F10）。</summary>
        public void SkipToBoss()
        {
            foreach (var c in world.Clues) if (c != null) c.ForceComplete();
            foreach (var c in world.Clues) if (c != null && handled.Add(c) && c.HasRoom) LanternReveal.AddRoom(c.Room, c.transform.position, true);
            Repaired = Total;
            BeginCollapse();
        }

        public void BeginCollapse()
        {
            if (Current != Phase.Explore) return;
            Current = Phase.Collapsing;
            CollapseTimer = CollapseSeconds;
            WaveActive = false;
            ShowToast("（真相快要拼回來了——整棟房子開始崩塌）", 3f);
            audio?.Play(LanternAudio.EnemyDeath, 1f);
        }

        private void EnterBoss()
        {
            Current = Phase.Boss;
            combat.ClearEnemies();
            combat.AutoRespawn = false;
            combat.RespawnMark = "BossStart";
            world.HouseLightScale = 1f;
            LanternReveal.ClearAnchors();
            world.BossBlackout();
            Transform start;
            if (world.Marks.TryGetValue("BossStart", out start))
            {
                var f = start.forward;
                player.Teleport(start.position, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, 0f);
            }
            Boss = world.BuildBoss(world.BossSpawn, combat.Glitch);
            Boss.OnDamaged = OnBossDamaged;
            Boss.OnThreshold = OnBossThreshold;
            ShowToast("（這裡是她的記憶深處。它躲在黑暗裡打不痛——射擊牆上亮紅點的電燈開關，把它引進光裡）", 7f);
        }

        private void OnBossDamaged(Vector3 at, float dmg, bool lit)
        {
            DamageNumbers++;
            var side = cam.transform.right * Random.Range(-0.6f, 0.6f);
            if (lit) Floater("-" + Mathf.Max(1, Mathf.RoundToInt(dmg * 10f)), at + side, dmgMat, 1.1f, 1.1f);
            else
            {
                Floater("無效", at + side, dullMat, 0.9f, 1.0f);
                if (darkHintCooldown <= 0f)
                {
                    DarkHintShown++;
                    darkHintCooldown = 7f;
                    ShowToast(world.SwitchesOn == 0 ? "（在暗處打它沒有用……先射擊電燈開關，把燈打開）" : "（它不在光裡——退到亮的地方，把它引過來）", 3.5f);
                }
            }
        }

        private void OnBossThreshold(int i)
        {
            Blackouts++;
            world.BossBlackout();
            rig.Shake(0.08f);
            audio?.Play(LanternAudio.EnemyWarn, 1f);
            ShowToast("（它把燈全部熄掉了！再把燈打開）", 3.5f);
            for (int k = 0; k < 3; k++) Floater(Lines[(lineIndex + k) % Lines.GetLength(0), 0], Boss.transform.position + Vector3.up * (3f + k * 0.6f) + Random.insideUnitSphere * 1.5f, phraseMat, 0.9f, 3f);
        }

        private void StepBoss(float dt)
        {
            if (Boss == null) return;
            if (combat.PlayerDown) return;
            darkHintCooldown -= dt;
            Boss.Lit = world.BossLit(Boss.transform.position);
            BossLitShare = Mathf.Lerp(BossLitShare, Boss.Lit ? 1f : 0f, dt);
            if (Boss.gameObject.activeSelf)
                combat.Damage(Boss.Step(dt, time, player.transform.position, Volley));
            // 不讓它穿過牆：留在房間裡。
            var bp = Boss.transform.position;
            var bb = world.BossBounds;
            Boss.transform.position = new Vector3(Mathf.Clamp(bp.x, bb.min.x + 1.2f, bb.max.x - 1.2f), bp.y, Mathf.Clamp(bp.z, bb.min.z + 1.2f, bb.max.z - 2.2f));
            if (Boss.Current == LanternBoss.State.Dead && Current == Phase.Boss)
            {
                BeginEnding();
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
                var at = new Vector3(Random.Range(bb.min.x + 1.5f, bb.max.x - 1.5f), Random.Range(1.6f, 4.6f), Random.Range(bb.min.z + 1.5f, bb.max.z - 1.5f));
                // 只在離玩家 6 m 以外浮現，不會突然擋在眼前。
                var away = at - player.transform.position;
                away.y = 0f;
                if (away.magnitude < 6f) at += (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward) * (6f - away.magnitude);
                Floater(Lines[k, Random.value < 0.5f ? 0 : 1], at, Random.value < 0.5f ? phraseMat : warmMat, 0.6f, 5f);
            }
        }

        // ---------------- 結局 ----------------

        private void BeginEnding()
        {
            Current = Phase.Ending;
            EndingTime = 0f;
            EndingLine = -1;
            foreach (var p in Phrases) if (p != null) ArenaArt.Kill(p.gameObject);
            Phrases.Clear();
            ShowToast("", 0f);
            audio?.Play(LanternAudio.RestoreDone, 1f);
            // 消散的同時，所有燈回來。
            foreach (var s in world.Switches) if (s != null) s.Set(true);
        }

        private void StepEnding(float dt)
        {
            float before = EndingTime;
            EndingTime += dt;
            // 1.3 秒：畫面全黑時回到家——所有房間恢復真實、有電、溫暖。
            if (before < 1.3f && EndingTime >= 1.3f) ReturnHome();
            for (int i = 0; i < EndingLines.Length; i++)
            {
                float at = 3.2f + i * 2.8f;
                if (before < at && EndingTime >= at) { EndingLine = i; ShowToast(EndingLines[i], 2.7f); }
            }
            if (EndingTime >= EndingSeconds)
            {
                Current = Phase.Ended;
                ShowToast("", 0f);
            }
        }

        private void ReturnHome()
        {
            combat.ClearEnemies();
            ClearBossStuff();
            world.BossBlackout();
            LanternReveal.ClearAnchors();
            world.HousePowered = true;
            world.HouseLightScale = 1f;
            WaveActive = false;
            // 整棟房子從全家福開始，一路恢復成真實的家。
            var photo = world.Clues.Find(c => c != null && c.Id == "photo");
            var seed = photo != null ? photo.transform.position : world.HouseBounds.center;
            LanternReveal.ClearRooms();
            LanternReveal.AddRoom(world.WholeHouse, seed);
            Transform mark;
            if (world.Marks.TryGetValue("HouseEnding", out mark) || world.Marks.TryGetValue("HouseLiving", out mark))
            {
                var f = mark.forward;
                float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                float pitch = -Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
                player.Teleport(mark.position, yaw, pitch);
            }
            combat.RespawnMark = "HouseEntry";
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
                f.position += Vector3.up * (life.Total < 1.5f ? 0.9f : 0.15f) * dt;
                Billboard(f);
                var tm = f.GetComponentInChildren<TextMesh>();
                if (tm != null) { var c = tm.color; c.a = Mathf.Clamp01(life.Left / Mathf.Min(1.2f, life.Total * 0.5f)) * Mathf.Clamp01((life.Total - life.Left) / Mathf.Min(0.6f, life.Total * 0.2f)); tm.color = c; }
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
            ArenaArt.Kill(dmgMat);
            ArenaArt.Kill(dullMat);
        }
    }

    public sealed class FloaterLife : MonoBehaviour
    {
        public float Left, Total;
        public void Init(float seconds) { Left = Total = seconds; }
    }
}
