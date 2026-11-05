using System.Collections.Generic;
using Shepherd.Battle.Core;
using UnityEngine;

namespace Shepherd.Battle.UI.Arena
{
    /// <summary>視覺回饋的計數（測試用，確認每種回饋都由對應的 BattleEvent 觸發）。</summary>
    public sealed class ArenaFeedbackStats
    {
        public int Casts;
        public int EnemyHits;
        public int EnemyAttacks;
        public int PlayerHits;
        public int ShieldAbsorbs;
        public int ShieldGains;
        public int Heals;
        public int Resolves;
        public int Resonances;
    }

    /// <summary>
    /// 確認施放後的事件播放：依序讀取 CommitResult 中 RoundResolver 產生的 BattleEvent，
    /// 每個事件對應一段視覺回饋，數字一律取自事件本身；播放期間 HUD 顯示「目前播放到的狀態」，
    /// 播放結束後再以引擎快照覆蓋，保證畫面與 BattleState 一致。
    /// </summary>
    public sealed partial class CombatArenaView
    {
        private sealed class DisplayEnemy
        {
            public int Value;
            public int Links;
            public int Corrections;
            public int Bind;
            public bool Resolved;
        }

        private readonly Dictionary<int, DisplayEnemy> display = new Dictionary<int, DisplayEnemy>();
        private int displayAnchor;
        private int displayShield;
        private bool committing;
        private readonly Queue<BattleEvent> queue = new Queue<BattleEvent>();
        private bool playing;
        private float wait;
        private readonly List<BattleEvent> played = new List<BattleEvent>();

        public ArenaFeedbackStats Stats { get; private set; } = new ArenaFeedbackStats();
        public bool IsPlaying { get { return playing; } }
        public int DisplayAnchor { get { return displayAnchor; } }
        public int DisplayShield { get { return displayShield; } }
        /// <summary>最近一次確認時播放的事件（與 CommitResult 的事件相同）。</summary>
        public IReadOnlyList<BattleEvent> PlayedEvents { get { return played; } }
        /// <summary>最近一次確認的引擎結果。</summary>
        public CommitResult LastCommit { get; private set; }

        public int DisplayValue(int enemyId)
        {
            DisplayEnemy d;
            return display.TryGetValue(enemyId, out d) ? d.Value : -1;
        }

        public bool DisplayResolved(int enemyId)
        {
            DisplayEnemy d;
            return display.TryGetValue(enemyId, out d) && d.Resolved;
        }

        private void SyncDisplayFromSnapshot(BattleSnapshot snap)
        {
            displayAnchor = snap.Anchor;
            displayShield = snap.Shield;
            foreach (var e in snap.Enemies)
            {
                DisplayEnemy d;
                if (!display.TryGetValue(e.Id, out d)) display[e.Id] = d = new DisplayEnemy();
                d.Value = e.Value;
                d.Links = e.Links;
                d.Corrections = e.Corrections;
                d.Bind = e.Bind;
                d.Resolved = e.Resolved;
                var view = Enemy(e.Id);
                if (view != null && e.Resolved && !view.Dissolved)
                {
                    view.Dissolved = true;
                    view.Root.gameObject.SetActive(false);
                }
            }
        }

        private void OnCommitClicked()
        {
            if (IsPlaying)
            {
                FinishPlayback();
                return;
            }
            if (Session.IsOver) return;
            HideCardTooltip(-1);
            redrawMode = false;
            committing = true;
            CommitResult r;
            try
            {
                r = Session.Commit();
            }
            finally
            {
                committing = false;
            }
            if (!r.Accepted)
            {
                Refresh();
                return;
            }
            StartPlayback(r);
        }

        /// <summary>播放一次確認施放的結果（事件來自 RoundResolver，順序不變）。</summary>
        private void StartPlayback(CommitResult r)
        {
            LastCommit = r;
            Stats = new ArenaFeedbackStats();
            played.Clear();
            queue.Clear();
            var before = Session.LastRound;
            displayAnchor = before.AnchorBefore;
            displayShield = before.ShieldBefore;
            foreach (var e in before.Enemies)
            {
                DisplayEnemy d;
                if (!display.TryGetValue(e.Id, out d)) display[e.Id] = d = new DisplayEnemy();
                d.Value = e.ValueBefore;
                d.Links = e.LinksBefore;
                d.Corrections = e.CorrectionsBefore;
                d.Resolved = e.ResolvedBefore;
            }
            foreach (var e in r.Resolution.Events) queue.Enqueue(e);
            playing = true;
            wait = 0.15f;
            Refresh();
        }

        private void TickPlayback(float dt)
        {
            if (!playing) return;
            wait -= dt;
            while (playing && wait <= 0f)
            {
                if (queue.Count == 0)
                {
                    EndPlayback();
                    return;
                }
                wait += Play(queue.Dequeue());
                RefreshPlates(true);
                RefreshStatus(Session.Snapshot(), true);
            }
        }

        /// <summary>立即播完剩下的事件並結束所有效果（略過動畫、測試）。</summary>
        public void FinishPlayback()
        {
            if (!playing) return;
            while (queue.Count > 0) Play(queue.Dequeue());
            fx.FinishAll();
            EndPlayback();
        }

        private void EndPlayback()
        {
            playing = false;
            queue.Clear();
            if (cam != null) cam.transform.localPosition = cameraHome;
            var body = player != null ? player.Find("Body") : null;
            if (body != null) body.localPosition = Vector3.zero;
            Refresh();
            var snap = Session.Snapshot();
            if (snap.Outcome == BattleOutcome.Victory)
            {
                bannerTitle.text = "勝利";
                bannerTitle.color = new Color(0.6f, 0.95f, 0.65f);
                bannerSubtitle.text = "異域已平息：所有失序體都已解除";
            }
            else if (snap.Outcome == BattleOutcome.Defeat)
            {
                bannerTitle.text = "戰敗";
                bannerTitle.color = new Color(1f, 0.45f, 0.4f);
                bannerSubtitle.text = "錨定崩潰：錨定值歸零，現實被異域吞沒";
            }
            banner.gameObject.SetActive(snap.Outcome != BattleOutcome.Ongoing);
            banner.SetAsLastSibling();
        }

        // ---------------- 單一事件的視覺回饋 ----------------

        private static readonly Color DamageColor = new Color(1f, 0.93f, 0.8f);
        private static readonly Color PlayerHurtColor = new Color(1f, 0.35f, 0.3f);
        private static readonly Color ShieldColor = new Color(0.45f, 0.75f, 1f);
        private static readonly Color HealColor = new Color(0.5f, 1f, 0.6f);

        /// <summary>播放一個事件，回傳到下一個事件前要等待的秒數。</summary>
        private float Play(BattleEvent ev)
        {
            played.Add(ev);
            var target = ev.EnemyId != 0 ? Enemy(ev.EnemyId) : null;
            DisplayEnemy d = null;
            if (ev.EnemyId != 0) display.TryGetValue(ev.EnemyId, out d);
            string src = ev.Source ?? "";

            switch (ev.Kind)
            {
                case EventKind.Resonance:
                    Stats.Resonances++;
                    fx.Floater(PlayerChest + Vector3.up, "共鳴！主要數值 ×1.5", CombatLabUi.Highlight, 30);
                    fx.Glow(PlayerChest, CombatLabUi.Highlight, 3.2f, 0.7f);
                    return 0.55f;

                case EventKind.CardResolved:
                {
                    Stats.Casts++;
                    var color = ArenaArt.ElementColor((ElementKind)ev.Amount);
                    fx.Glow(LanternPoint, color, 1.1f, 0.4f);
                    if (target != null && !target.Dissolved)
                    {
                        fx.Projectile(LanternPoint, EnemyChest(target), color, 0.42f);
                        return 0.5f;
                    }
                    fx.Glow(PlayerChest, color, 2.4f, 0.6f);
                    return 0.45f;
                }

                case EventKind.CardFizzled:
                    fx.Floater(PlayerChest + Vector3.up, "目標已解除，效果失效", CombatLabUi.MutedText, 22);
                    return 0.4f;

                case EventKind.Pressure:
                case EventKind.Conduction:
                {
                    if (d != null) d.Value = Mathf.Max(0, d.Value - ev.Value);
                    if (target == null || target.Dissolved) return 0.1f;
                    Stats.EnemyHits++;
                    string prefix = ev.Kind == EventKind.Conduction ? "傳導 " : src == "passive:Response" ? "回應 "
                        : src == "passive:Divert" ? "回流 " : src == "phase:Correction" ? "校正 " : "";
                    fx.Floater(EnemyChest(target) + Vector3.up * 0.4f, prefix + "−" + ev.Value, DamageColor, ev.Value >= 6 ? 40 : 32);
                    fx.Tint(target.Renderers, Color.white, 0.25f);
                    fx.Shake(target.Body, 0.12f, 0.3f);
                    fx.Burst(EnemyChest(target), ArenaArt.Rift, 12, 2.2f);
                    return src == "phase:Correction" ? 0.45f : 0.4f;
                }

                case EventKind.ShieldChanged:
                    displayShield = ev.Value;
                    if (ev.Amount > 0)
                    {
                        Stats.ShieldGains++;
                        fx.Glow(PlayerChest, ShieldColor, 2.6f, 0.55f);
                        fx.Floater(PlayerChest + Vector3.up * 0.8f, "擬態 +" + ev.Amount, ShieldColor, 28);
                        FlashShield(0.22f);
                        return 0.35f;
                    }
                    if (src.StartsWith("enemy:"))
                    {
                        Stats.ShieldAbsorbs++;
                        FlashShield(0.38f);
                        fx.ScreenFlash(shieldFlash, new Color(0.25f, 0.55f, 1f, 0.28f), 0.45f);
                        fx.Burst(PlayerChest, ShieldColor, 22, 3f);
                        fx.Floater(PlayerChest + Vector3.up * 0.9f, "擬態擋下 " + (-ev.Amount), ShieldColor, 32);
                        return 0.45f;
                    }
                    fx.Floater(PlayerChest + Vector3.up * 0.8f, "擬態 " + ev.Amount + "（轉化）", ShieldColor, 24);
                    return 0.3f;

                case EventKind.AnchorChanged:
                    displayAnchor = ev.Value;
                    if (ev.Amount < 0)
                    {
                        Stats.PlayerHits++;
                        fx.ScreenFlash(hurtFlash, new Color(0.9f, 0.06f, 0.04f, 0.38f), 0.55f);
                        fx.Tint(playerRenderers, new Color(1f, 0.15f, 0.1f), 0.45f);
                        fx.Shake(player.Find("Body"), 0.14f, 0.4f);
                        fx.Shake(cam.transform, 0.18f, 0.35f);
                        fx.Floater(PlayerChest + Vector3.up * 0.6f, "錨定值 −" + (-ev.Amount), PlayerHurtColor, 40);
                        return 0.6f;
                    }
                    Stats.Heals++;
                    fx.Glow(PlayerChest, HealColor, 2f, 0.5f);
                    fx.Floater(PlayerChest + Vector3.up * 0.6f, "錨定值 +" + ev.Amount, HealColor, 28);
                    return 0.35f;

                case EventKind.LinkChanged:
                    if (d != null) d.Links = ev.Value;
                    return StatusFloater(target, src, (ev.Amount > 0 ? "連結 +" : "連結 ") + ev.Amount, ArenaArt.Rift);

                case EventKind.CorrectionChanged:
                    if (d != null) d.Corrections = ev.Value;
                    return StatusFloater(target, src, (ev.Amount > 0 ? "校正 +" : "校正 ") + ev.Amount, new Color(0.4f, 0.95f, 0.9f));

                case EventKind.BindChanged:
                    if (d != null) d.Bind = ev.Value;
                    return StatusFloater(target, src, ev.Amount > 0 ? "束縛" : "束縛解除", new Color(0.85f, 0.85f, 0.5f));

                case EventKind.EnemyResolved:
                    if (d != null)
                    {
                        d.Resolved = true;
                        d.Value = 0;
                    }
                    if (target == null || target.Dissolved) return 0.1f;
                    Stats.Resolves++;
                    Dissolve(target);
                    return 0.85f;

                case EventKind.EnemyActed:
                    if (target == null || target.Dissolved) return 0.1f;
                    if (ev.Amount <= 0)
                    {
                        fx.Floater(EnemyHead(target), "（本回合不攻擊）", CombatLabUi.MutedText, 22);
                        return 0.35f;
                    }
                    Stats.EnemyAttacks++;
                    fx.Floater(EnemyHead(target), "攻擊 " + ev.Amount, new Color(1f, 0.6f, 0.5f), 30);
                    fx.Lunge(target.Root, PlayerPosition - target.Root.position, 1.3f, 0.6f);
                    fx.Projectile(EnemyChest(target), PlayerChest, ArenaArt.Rift, 0.4f);
                    return 0.55f;

                case EventKind.Victory:
                case EventKind.Defeat:
                    return 0.4f;
            }
            return 0f;
        }

        private float StatusFloater(EnemyView target, string src, string text, Color color)
        {
            // 回合結束的連結衰減只更新數字，不另外顯示。
            if (target == null || target.Dissolved || src == "phase:RoundEnd") return 0.05f;
            fx.Floater(EnemyHead(target) + Vector3.down * 0.4f, text, color, 24, 0.9f);
            return 0.22f;
        }

        private void FlashShield(float alpha)
        {
            var c = shieldMat.color;
            c.a = Mathf.Max(c.a, alpha);
            shieldMat.color = c;
        }

        private void Dissolve(EnemyView e)
        {
            e.Dissolved = true;
            var body = e.Body;
            var start = body.localScale;
            var root = e.Root;
            fx.Burst(EnemyChest(e), ArenaArt.Rift, 60, 4.5f);
            fx.Glow(EnemyChest(e), ArenaArt.Rift, 3.5f, 0.9f);
            fx.Floater(EnemyHead(e), "解除", new Color(0.8f, 0.6f, 1f), 36);
            fx.Add(0.85f, k =>
            {
                if (body == null) return;
                body.localScale = new Vector3(start.x * (1f + k * 0.3f), start.y * (1f - k), start.z * (1f + k * 0.3f));
                body.localPosition = new Vector3(0f, -k * 0.3f, 0f);
            }, () => { if (root != null) root.gameObject.SetActive(false); });
        }
    }
}
