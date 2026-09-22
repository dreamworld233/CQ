using System.Collections.Generic;
using CQ.Core.Config;

namespace CQ.Core.Combat
{
    /// <summary>
    /// 结算敌人一个 IntentConfig。敌人目标是玩家方存活单位。
    /// 单体攻击/连击/减益用「亮意图时预锁定的目标」(lockedTargetId)，已死则回退权重随机。
    /// charge 不立即伤害，存 pendingChargeDamage（含预锁目标）；该敌人下一次行动先释放 nextDamage
    /// 再执行本轮 intent。释放被 interrupt 取消当且仅当 蓄力 interruptible == true
    /// 且 ctx.HasStatus(敌人Id, Interrupt)。
    /// </summary>
    public sealed class EnemyActionResolver
    {
        private sealed class PendingCharge
        {
            public int Damage;
            public bool Interruptible;
            public string TargetId;
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

        public void Resolve(CombatContext ctx, string enemyId, IntentConfig intent)
            => Resolve(ctx, enemyId, intent, null);

        public void Resolve(CombatContext ctx, string enemyId, IntentConfig intent, string lockedTargetId)
        {
            if (ctx == null || string.IsNullOrEmpty(enemyId) || intent == null) return;
            ReleasePending(ctx, enemyId);
            ExecuteIntent(ctx, enemyId, intent, lockedTargetId);
        }

        public void Resolve(CombatContext ctx, string enemyId, IntentModel model, string lockedTargetId)
        {
            if (ctx == null || string.IsNullOrEmpty(enemyId) || model == null) return;
            Resolve(ctx, enemyId, model.CurrentIntent, lockedTargetId);
            model.Advance();
        }

        /// <summary>
        /// 被打断跳过本回合时调用：只走蓄力释放路径，不执行本回合意图。
        /// interruptible 蓄力被 interrupt 取消；非 interruptible 蓄力仍照常释放。
        /// </summary>
        public void ResolveSkipped(CombatContext ctx, string enemyId)
        {
            if (ctx == null || string.IsNullOrEmpty(enemyId)) return;
            ReleasePending(ctx, enemyId);
        }

        private void ReleasePending(CombatContext ctx, string enemyId)
        {
            if (!_pending.TryGetValue(enemyId, out var p)) return;
            _pending.Remove(enemyId);

            bool cancelled = p.Interruptible && ctx.HasStatus(enemyId, StatusType.Interrupt);
            if (cancelled) return;

            var target = ResolveTarget(ctx, p.TargetId, GetPlayerTargets(ctx));
            if (target == null) return;
            Damage.Apply(ctx, new DamageRequest { Amount = p.Damage, SourceUnitId = enemyId, TargetUnitId = target.Id });
        }

        private void ExecuteIntent(CombatContext ctx, string enemyId, IntentConfig intent, string lockedTargetId)
        {
            switch (intent.type)
            {
                case "attack": Attack(ctx, enemyId, intent, lockedTargetId); break;
                case "multi": Multi(ctx, enemyId, intent, lockedTargetId); break;
                case "attackUp": AttackUp(ctx, enemyId, intent); break;
                case "charge": Charge(ctx, enemyId, intent, lockedTargetId); break;
                case "debuff": Debuff(ctx, enemyId, intent, lockedTargetId); break;
            }
        }

        private void Attack(CombatContext ctx, string enemyId, IntentConfig intent, string lockedTargetId)
        {
            var players = GetPlayerTargets(ctx);
            if (intent.target == "all")
            {
                foreach (var p in players)
                    Damage.Apply(ctx, new DamageRequest { Amount = intent.damage, SourceUnitId = enemyId, TargetUnitId = p.Id });
            }
            else
            {
                var t = ResolveTarget(ctx, lockedTargetId, players);
                if (t != null)
                    Damage.Apply(ctx, new DamageRequest { Amount = intent.damage, SourceUnitId = enemyId, TargetUnitId = t.Id });
            }
        }

        private void Multi(CombatContext ctx, string enemyId, IntentConfig intent, string lockedTargetId)
        {
            var t = ResolveTarget(ctx, lockedTargetId, GetPlayerTargets(ctx));
            if (t == null) return;
            for (int i = 0; i < intent.hits; i++)
                Damage.Apply(ctx, new DamageRequest { Amount = intent.damage, SourceUnitId = enemyId, TargetUnitId = t.Id });
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

        private void Charge(CombatContext ctx, string enemyId, IntentConfig intent, string lockedTargetId)
        {
            _pending[enemyId] = new PendingCharge { Damage = intent.nextDamage, Interruptible = intent.interruptible, TargetId = lockedTargetId };
        }

        private void Debuff(CombatContext ctx, string enemyId, IntentConfig intent, string lockedTargetId)
        {
            var t = ResolveTarget(ctx, lockedTargetId, GetPlayerTargets(ctx));
            if (t == null) return;
            ctx.AddStatus(t.Id, new StatusEffect
            {
                Type = StatusTypes.Parse(intent.debuffType),
                Percent = intent.debuffPercent,
                Duration = intent.debuffDuration > 0 ? intent.debuffDuration : 1,
                Turn = "round",
                SourceUnitId = enemyId
            });
        }

        private static Unit ResolveTarget(CombatContext ctx, string lockedTargetId, IReadOnlyList<Unit> players)
        {
            if (!string.IsNullOrEmpty(lockedTargetId))
            {
                var u = ctx.GetUnit(lockedTargetId);
                if (u != null && !u.IsDead) return u;
            }
            return TargetSelector.PickTarget(ctx, players);
        }
    }
}