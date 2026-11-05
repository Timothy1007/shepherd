using System;
using System.Collections.Generic;

namespace Shepherd.Battle.Core
{
    public sealed class ResolveResult
    {
        /// <summary>結算後的新狀態（補牌之前）。</summary>
        public readonly BattleState NewState;
        public readonly IReadOnlyList<BattleEvent> Events;
        public readonly BattleOutcome Outcome;

        public ResolveResult(BattleState newState, IReadOnlyList<BattleEvent> events, BattleOutcome outcome)
        {
            NewState = newState;
            Events = events;
            Outcome = outcome;
        }
    }

    /// <summary>
    /// 唯一的回合結算核心（規格第三、四、六節）。
    /// 不使用亂數、不讀時鐘、不修改輸入狀態；預覽與正式結算都呼叫這個函式。
    /// </summary>
    public static class RoundResolver
    {
        public static ResolveResult Resolve(BattleState state, RoundPlan plan, RuleSet rules)
        {
            var validation = PlanValidator.Validate(state, plan, rules);
            if (!validation.IsValid) throw new InvalidOperationException("編排不合法：" + validation.Error);

            var ctx = new Context(state.Clone(), plan, rules);
            ctx.Run();
            return new ResolveResult(ctx.S, ctx.Events.AsReadOnly(), ctx.S.Outcome);
        }

        private sealed class Context
        {
            public readonly BattleState S;
            public readonly List<BattleEvent> Events = new List<BattleEvent>();
            private readonly RoundPlan plan;
            private readonly RuleSet r;
            private readonly SchoolKind school;
            private bool stabilized;
            private int forecastTotal;

            public Context(BattleState s, RoundPlan plan, RuleSet rules)
            {
                S = s;
                this.plan = plan;
                r = rules;
                school = plan.School;
            }

            private bool Ended { get { return S.Outcome != BattleOutcome.Ongoing; } }

            public void Run()
            {
                // 本回合預告攻擊總量：回合開始時所有存活敵人本回合攻擊值的總和，之後不因敵人解除而重算。
                forecastTotal = 0;
                for (int i = 0; i < S.Enemies.Count; i++)
                {
                    if (S.Enemies[i].Alive) forecastTotal += S.Enemies[i].AttackForRound(S.Round);
                }

                for (int i = 0; i < plan.Cards.Count; i++) S.Hand.Remove(plan.Cards[i].CardId);

                PhaseSequence();
                if (!Ended) PhaseCorrection();
                if (!Ended) PhaseEnemy();
                if (!Ended) PhaseRoundEnd();

                // 打出的牌進棄牌區；勝負於序列中途決定時，剩餘的牌直接棄置。
                for (int i = 0; i < plan.Cards.Count; i++) S.Discard.Add(plan.Cards[i].CardId);
            }

            // ---------------- A. 序列 ----------------

            private void PhaseSequence()
            {
                int n = plan.Cards.Count;
                bool triple = n == RuleSet.TripleCount;
                for (int i = 1; i < n && triple; i++)
                {
                    if (plan.Cards[i].Element != plan.Cards[0].Element) triple = false;
                }
                if (triple) Emit(EventKind.Resonance, "sequence", 0, (int)plan.Cards[0].Element, n);

                for (int i = 0; i < n; i++)
                {
                    var card = plan.Cards[i];
                    bool hasPrev = i > 0;
                    ElementKind prev = hasPrev ? plan.Cards[i - 1].Element : ElementKind.Ember;
                    string src = "card:" + i;

                    // 1. 主要數值；2. 三連；3. 反應（三連之後才加）。
                    int v = r.PrimaryValue(school, card.Element);
                    if (triple) v = (int)Math.Floor(v * r.TripleMultiplier);
                    if (hasPrev && prev == ElementKind.Ember) v += r.ResidueEmberBonus;

                    // 4. 檢查目標：已解除則敵方指向部分失效，自身部分照常結算。
                    bool needsTarget = r.RequiresTarget(school, card.Element);
                    EnemyState target = card.TargetEnemyId.HasValue ? S.FindEnemy(card.TargetEnemyId.Value) : null;
                    bool fizzled = needsTarget && (target == null || target.Resolved);
                    Emit(fizzled ? EventKind.CardFizzled : EventKind.CardResolved, src,
                        target == null ? 0 : target.Id, (int)card.Element, v);

                    // 5. 效果（固定內部順序）。
                    ApplyEffects(card, v, target, fizzled, src);

                    if (CheckVictory(src)) return;

                    // 6. 承接附帶：在這張牌的效果之後結算，不因敵方指向部分失效而取消。
                    if (hasPrev)
                    {
                        string rsrc = "residue:" + i;
                        if (prev == ElementKind.Base)
                        {
                            ChangeShield(r.ResidueBaseShield, rsrc);
                        }
                        else if (prev == ElementKind.Binder)
                        {
                            var t = LeastLinkedAlive();
                            if (t != null) AddLinks(t, r.ResidueBinderLink, rsrc);
                        }
                    }
                }
            }

            private void ApplyEffects(PlannedCard card, int v, EnemyState target, bool fizzled, string src)
            {
                var steps = r.Effects(school, card.Element);
                int converted = 0;
                int severBonus = 0;
                for (int k = 0; k < steps.Count; k++)
                {
                    var step = steps[k];
                    if (step.EnemyDirected && fizzled) continue;
                    int amount = step.Source == AmountSource.Primary ? v : step.Constant;

                    switch (step.Op)
                    {
                        case EffectOp.Convert:
                        {
                            int cap = r.StructureConvertBase + r.StructureConvertPerLink * target.Links;
                            converted = Math.Min(S.Shield, cap);
                            if (converted > 0) ChangeShield(-converted, src);
                            break;
                        }
                        case EffectOp.Sever:
                        {
                            int layers = Math.Min(target.Links, r.RelationSeverMaxLayers);
                            if (layers > 0) ChangeLinks(target, -layers, src);
                            severBonus = layers * r.RelationSeverBonusPerLayer;
                            break;
                        }
                        case EffectOp.Pressure:
                            ApplyPressure(target, amount + converted + severBonus, src);
                            break;
                        case EffectOp.Shield:
                            ChangeShield(amount, src);
                            break;
                        case EffectOp.ThreatShield:
                            if (forecastTotal > S.Shield) ChangeShield(amount, src);
                            break;
                        case EffectOp.Correction:
                            ChangeCorrections(target, amount, src);
                            break;
                        case EffectOp.Stabilize:
                            stabilized = true;
                            break;
                        case EffectOp.Heal:
                            Heal(amount, src);
                            break;
                        case EffectOp.Link:
                            AddLinks(target, amount, src);
                            break;
                        case EffectOp.Bind:
                            ChangeBind(target, amount, src);
                            break;
                        default:
                            throw new InvalidOperationException("未知的效果指令 " + step.Op);
                    }
                }
            }

            // ---------------- B. 壓制 ----------------

            private void ApplyPressure(EnemyState enemy, int nominal, string src)
            {
                if (enemy == null || enemy.Resolved || nominal <= 0) return;

                int actual = Math.Min(nominal, enemy.Value);
                enemy.Value -= actual;
                Emit(EventKind.Pressure, src, enemy.Id, nominal, actual);

                // 傳導：關係論回合、目標此時還有連結；以實際減少量計算；傳導不再傳導。
                if (school == SchoolKind.Relation && enemy.Links > 0)
                {
                    int amount = actual * r.ConductionPercent / 100;
                    if (amount > 0)
                    {
                        for (int i = 0; i < S.Enemies.Count; i++)
                        {
                            var other = S.Enemies[i];
                            if (other == enemy || other.Resolved || other.Links <= 0) continue;
                            int d = Math.Min(amount, other.Value);
                            other.Value -= d;
                            Emit(EventKind.Conduction, "passive:Conduction", other.Id, amount, d);
                        }
                    }
                }

                // 解除在傳導之後處理。
                ResolveDefeatedEnemies(src);
            }

            private void ResolveDefeatedEnemies(string src)
            {
                for (int i = 0; i < S.Enemies.Count; i++)
                {
                    var e = S.Enemies[i];
                    if (e.Resolved || e.Value > 0) continue;
                    e.Value = 0;
                    e.Resolved = true;
                    // 附著在敵人身上的狀態隨該敵人解除而消失。
                    e.Links = 0;
                    e.Corrections = 0;
                    e.Bind = 0;
                    Emit(EventKind.EnemyResolved, src, e.Id, 0, 0);
                }
            }

            private bool CheckVictory(string src)
            {
                if (!S.AllEnemiesResolved()) return false;
                S.Outcome = BattleOutcome.Victory;
                Emit(EventKind.Victory, src, 0, 0, 0);
                return true;
            }

            // ---------------- C. 持續校正 ----------------

            private void PhaseCorrection()
            {
                bool settled = false;
                for (int i = 0; i < S.Enemies.Count; i++)
                {
                    var e = S.Enemies[i];
                    if (e.Resolved || e.Corrections <= 0) continue;

                    settled = true;
                    ApplyPressure(e, e.Corrections, "phase:Correction");
                    if (CheckVictory("phase:Correction")) return;
                    if (e.Resolved) continue;

                    if (stabilized)
                    {
                        // 穩定：本回合所有校正不衰減。
                    }
                    else if (school == SchoolKind.Process && e.Links > 0)
                    {
                        // 延續：消耗 1 層連結取消該次衰減。
                        ChangeLinks(e, -1, "passive:Continuation");
                    }
                    else
                    {
                        ChangeCorrections(e, -1, "phase:Correction");
                    }
                }

                // 回饋：過程哲學回合中至少一次校正結算，錨定值 +1，每回合最多一次。
                if (school == SchoolKind.Process && settled) Heal(r.ProcessFeedbackHeal, "passive:Feedback");
            }

            // ---------------- D. 敵方回合 ----------------

            private void PhaseEnemy()
            {
                for (int i = 0; i < S.Enemies.Count; i++)
                {
                    var e = S.Enemies[i];
                    if (e.Resolved) continue;

                    string src = "enemy:" + e.Id;
                    int a = e.AttackForRound(S.Round);
                    Emit(EventKind.EnemyActed, src, e.Id, a, a);

                    // 關係論分流與回流。
                    if (school == SchoolKind.Relation && a > 0)
                    {
                        int k = Math.Min(e.Links,
                            Math.Min(r.DivertBaseMaxLayers + e.Bind, CeilDiv(a, r.DivertPointsPerLayer)));
                        if (k > 0)
                        {
                            ChangeLinks(e, -k, "passive:Divert");
                            int divert = Math.Min(a, r.DivertPointsPerLayer * k);
                            a -= divert;
                            int back = (int)Math.Floor(divert * r.DivertReturnRatio);
                            ApplyPressure(e, back, "passive:Divert");
                            if (CheckVictory("passive:Divert")) return;
                        }
                    }

                    if (a <= 0) continue;

                    int absorbed = Math.Min(S.Shield, a);
                    if (absorbed > 0) ChangeShield(-absorbed, src);
                    int loss = a - absorbed;
                    if (loss > 0) ChangeAnchor(-loss, src);

                    if (S.Anchor <= 0)
                    {
                        S.Outcome = BattleOutcome.Defeat;
                        Emit(EventKind.Defeat, src, e.Id, 0, S.Anchor);
                        return;
                    }

                    // 實在論回應：a > 0 且錨定值沒有損失時，對該敵人壓制 3。
                    if (school == SchoolKind.Structure && loss == 0)
                    {
                        ApplyPressure(e, r.StructureResponsePressure, "passive:Response");
                        if (CheckVictory("passive:Response")) return;
                    }
                }
            }

            // ---------------- E. 回合結束 ----------------

            private void PhaseRoundEnd()
            {
                for (int i = 0; i < S.Enemies.Count; i++)
                {
                    var e = S.Enemies[i];
                    if (e.Resolved) continue;
                    int decay = Math.Min(e.Links, r.LinkDecayPerRound);
                    if (decay > 0) ChangeLinks(e, -decay, "phase:RoundEnd");
                    if (e.Bind != 0) ChangeBind(e, -e.Bind, "phase:RoundEnd");
                }
                S.Round += 1;
            }

            // ---------------- 狀態變化（含邊界） ----------------

            private EnemyState LeastLinkedAlive()
            {
                EnemyState best = null;
                for (int i = 0; i < S.Enemies.Count; i++)
                {
                    var e = S.Enemies[i];
                    if (e.Resolved) continue;
                    if (best == null || e.Links < best.Links || (e.Links == best.Links && e.Value > best.Value))
                        best = e;
                }
                return best;
            }

            private void ChangeShield(int delta, string src)
            {
                int next = Math.Max(0, S.Shield + delta);
                if (next == S.Shield) return;
                Emit(EventKind.ShieldChanged, src, 0, next - S.Shield, next);
                S.Shield = next;
            }

            private void ChangeAnchor(int delta, string src)
            {
                int next = Math.Min(r.MaxAnchor, S.Anchor + delta);
                if (next == S.Anchor) return;
                Emit(EventKind.AnchorChanged, src, 0, next - S.Anchor, next);
                S.Anchor = next;
            }

            private void Heal(int amount, string src)
            {
                if (amount > 0) ChangeAnchor(amount, src);
            }

            private void AddLinks(EnemyState e, int amount, string src)
            {
                if (e == null || e.Resolved || amount <= 0) return;
                int next = Math.Min(r.LinkCap, e.Links + amount);
                if (next == e.Links) return;
                Emit(EventKind.LinkChanged, src, e.Id, next - e.Links, next);
                e.Links = next;
            }

            private void ChangeLinks(EnemyState e, int delta, string src)
            {
                int next = Math.Max(0, Math.Min(r.LinkCap, e.Links + delta));
                if (next == e.Links) return;
                Emit(EventKind.LinkChanged, src, e.Id, next - e.Links, next);
                e.Links = next;
            }

            private void ChangeCorrections(EnemyState e, int delta, string src)
            {
                if (e == null || e.Resolved) return;
                int next = Math.Max(0, e.Corrections + delta);
                if (next == e.Corrections) return;
                Emit(EventKind.CorrectionChanged, src, e.Id, next - e.Corrections, next);
                e.Corrections = next;
            }

            private void ChangeBind(EnemyState e, int delta, string src)
            {
                if (e == null || e.Resolved) return;
                int next = Math.Max(0, e.Bind + delta);
                if (next == e.Bind) return;
                Emit(EventKind.BindChanged, src, e.Id, next - e.Bind, next);
                e.Bind = next;
            }

            private void Emit(EventKind kind, string src, int enemyId, int amount, int value)
            {
                Events.Add(new BattleEvent(kind, src, enemyId, amount, value));
            }

            private static int CeilDiv(int a, int b)
            {
                return (a + b - 1) / b;
            }
        }
    }
}
