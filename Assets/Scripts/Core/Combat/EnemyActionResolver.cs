using System.Collections.Generic;
using CQ.Core.Battle;
using CQ.Core.Config;

namespace CQ.Core.Combat
{
    /// <summary>实际攻击开始时选择最高嘲讽目标；连击不换目标，蓄力释放重新选择。</summary>
    public sealed class EnemyActionResolver
    {
        private sealed class PendingCharge
        {
            public int Damage;
            public bool Interruptible;
        }

        private readonly Dictionary<string, PendingCharge> _pending = new Dictionary<string, PendingCharge>();

        public IReadOnlyList<Unit> GetPlayerTargets(CombatContext ctx)
        {
            var list = new List<Unit>();
            if (ctx == null) return list;
            foreach (var u in ctx.Units)
                if (u.Team == Team.Player && !u.IsDead) list.Add(u);
            return list;
        }

        /// <summary>只读查询，不推进意图、不消费 pending/Rng、不选择或锁定目标。</summary>
        public EnemyIntentPreview Preview(CombatContext ctx, string enemyId, IntentConfig intent)
        {
            if (ctx == null || string.IsNullOrEmpty(enemyId)) return null;
            var actor = ctx.GetUnit(enemyId);
            if (actor == null || actor.Team != Team.Enemy) return null;
            bool interrupted = ctx.HasStatus(enemyId, StatusType.Interrupt);
            bool canAct = !actor.IsDead && GetPlayerTargets(ctx).Count > 0;
            EnemyIntentPreviewStep pending = null;
            if (_pending.TryGetValue(enemyId, out var charge))
                pending = new EnemyIntentPreviewStep(ctx, enemyId, new IntentConfig
                {
                    type = "chargeRelease", target = "single", damage = charge.Damage,
                    interruptible = charge.Interruptible
                }, !canAct || (interrupted && charge.Interruptible));
            var current = intent == null ? null : new EnemyIntentPreviewStep(ctx, enemyId, intent, !canAct || interrupted);
            return new EnemyIntentPreview(interrupted, canAct,
                ctx.FindStatus(enemyId, StatusType.AttackUp)?.Percent ?? 0,
                ctx.FindStatus(enemyId, StatusType.Weaken)?.Percent ?? 0, pending, current);
        }

        public void Resolve(CombatContext ctx, string enemyId, IntentConfig intent)
        {
            if (ctx == null || string.IsNullOrEmpty(enemyId) || intent == null) return;
            var actor = ctx.GetUnit(enemyId);
            if (actor == null || actor.IsDead || GetPlayerTargets(ctx).Count == 0) return;
            ctx.BeginAction(BattleEventKind.Intent, actor, intent.type, true);
            ReleasePending(ctx, enemyId);
            if (GetPlayerTargets(ctx).Count > 0) ExecuteIntent(ctx, enemyId, intent);
            ctx.CompleteAction();
        }

        public void Resolve(CombatContext ctx, string enemyId, IntentModel model)
        {
            if (ctx == null || string.IsNullOrEmpty(enemyId) || model == null) return;
            Resolve(ctx, enemyId, model.CurrentIntent);
            model.Advance();
        }

        /// <summary>
        /// 被打断跳过本回合时调用：只走蓄力释放路径，不执行本回合意图。
        /// interruptible 蓄力被 interrupt 取消；非 interruptible 蓄力仍照常释放。
        /// </summary>
        public void ResolveSkipped(CombatContext ctx, string enemyId)
        {
            if (ctx == null || string.IsNullOrEmpty(enemyId)) return;
            var actor = ctx.GetUnit(enemyId);
            if (actor == null || actor.IsDead || GetPlayerTargets(ctx).Count == 0) return;
            ctx.BeginAction(BattleEventKind.Intent, actor, "skipped", true);
            ReleasePending(ctx, enemyId);
            ctx.CompleteAction();
        }

        private void ReleasePending(CombatContext ctx, string enemyId)
        {
            if (!_pending.TryGetValue(enemyId, out var p)) return;
            _pending.Remove(enemyId);

            bool cancelled = p.Interruptible && ctx.HasStatus(enemyId, StatusType.Interrupt);
            if (cancelled) return;

            var target = TargetSelector.PickTarget(ctx, GetPlayerTargets(ctx));
            if (target == null) return;
            Damage.Apply(ctx, new DamageRequest { Amount = p.Damage, SourceUnitId = enemyId, TargetUnitId = target.Id });
        }

        private void ExecuteIntent(CombatContext ctx, string enemyId, IntentConfig intent)
        {
            switch (intent.type)
            {
                case "attack": Attack(ctx, enemyId, intent); break;
                case "multi": Multi(ctx, enemyId, intent); break;
                case "attackUp": AttackUp(ctx, enemyId, intent); break;
                case "charge": Charge(ctx, enemyId, intent); break;
                case "debuff": Debuff(ctx, enemyId, intent); break;
            }
        }

        private void Attack(CombatContext ctx, string enemyId, IntentConfig intent)
        {
            ResolveAttack(ctx, enemyId, intent, 1);
        }

        private void Multi(CombatContext ctx, string enemyId, IntentConfig intent)
        {
            ResolveAttack(ctx, enemyId, intent, intent.hits);
        }

        private void ResolveAttack(CombatContext ctx, string enemyId, IntentConfig intent, int hits)
        {
            // 只选一次；死亡不重选。全体也在第一段前快照，附加效果不再走选目标。
            var targets = new List<Unit>();
            if (intent.target == "all" && intent.type == "attack")
                targets.AddRange(GetPlayerTargets(ctx));
            else
            {
                var target = TargetSelector.PickTarget(ctx, GetPlayerTargets(ctx));
                if (target != null) targets.Add(target);
            }
            var hitTargets = new HashSet<Unit>();
            for (int i = 0; i < hits; i++)
                foreach (var target in targets)
                {
                    if (target.IsDead || intent.damage <= 0) continue;
                    Damage.Apply(ctx, new DamageRequest
                    {
                        Amount = intent.damage, SourceUnitId = enemyId, TargetUnitId = target.Id
                    });
                    // 全盾挡/虚弱减至零也是实际命中，但死亡者不接受后续 debuff。
                    hitTargets.Add(target);
                }

            // 前置伤害已结束战斗，不执行余下的自身 buff/目标 debuff。
            if (GetPlayerTargets(ctx).Count == 0) return;
            if (intent.buffPercent > 0) AttackUp(ctx, enemyId, intent);
            if (!string.IsNullOrEmpty(intent.debuffType))
                foreach (var target in targets)
                    if (hitTargets.Contains(target) && !target.IsDead)
                        ApplyDebuff(ctx, enemyId, target.Id, intent);
        }

        private void AttackUp(CombatContext ctx, string enemyId, IntentConfig intent)
        {
            ctx.AddStatus(enemyId, new StatusEffect
            {
                Type = StatusType.AttackUp,
                Percent = intent.buffPercent,
                Duration = intent.buffDuration > 0 ? intent.buffDuration : 1,
                Turn = "round",
                SourceUnitId = enemyId
            });
        }

        private void Charge(CombatContext ctx, string enemyId, IntentConfig intent)
        {
            _pending[enemyId] = new PendingCharge { Damage = intent.nextDamage, Interruptible = intent.interruptible };
        }

        private void Debuff(CombatContext ctx, string enemyId, IntentConfig intent)
        {
            var t = TargetSelector.PickTarget(ctx, GetPlayerTargets(ctx));
            if (t == null) return;
            ApplyDebuff(ctx, enemyId, t.Id, intent);
        }

        private static void ApplyDebuff(CombatContext ctx, string enemyId, string targetId, IntentConfig intent)
        {
            ctx.AddStatus(targetId, new StatusEffect
            {
                Type = StatusTypes.Parse(intent.debuffType),
                Percent = intent.debuffPercent,
                Duration = intent.debuffDuration > 0 ? intent.debuffDuration : 1,
                Turn = "round",
                SourceUnitId = enemyId
            });
        }

    }
}
