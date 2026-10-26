using CQ.Core;

namespace CQ.Core.Combat
{
    /// <summary>
    /// 确定性伤害管线：基础伤害 × 攻增(源) → 去尾 → × 虚弱(源) → 去尾 → × 易伤(目标) → 去尾 → 护盾 → 扣 HP。
    /// 攻增/虚弱读攻击方（源）状态，护盾读受击方（目标）状态；护盾挡到 0 后移除（once 语义）。
    /// </summary>
    public static class Damage
    {
        /// <summary>只读的源伤害修正；攻增、虚弱分别去尾，不处理护盾、目标或随机数。</summary>
        public static DamageResult CalculateSourceDamage(CombatContext ctx, string sourceUnitId, int amount)
        {
            var result = new DamageResult { RawDamage = amount };
            int damage = amount;
            var attackUp = ctx.FindStatus(sourceUnitId, StatusType.AttackUp);
            if (attackUp != null)
                damage = Mathd.FloorToInt(damage * (1.0 + attackUp.Percent / 100.0));
            result.AfterAttackUp = damage;
            var weaken = ctx.FindStatus(sourceUnitId, StatusType.Weaken);
            if (weaken != null)
                damage = Mathd.FloorToInt(damage * (1.0 - weaken.Percent / 100.0));
            result.AfterWeaken = damage;
            return result;
        }

        public static DamageResult Apply(CombatContext ctx, in DamageRequest req) => Apply(ctx, req, true);

        public static DamageResult ApplyCard(CombatContext ctx, in DamageRequest req) => Apply(ctx, req, false);

        private static DamageResult Apply(CombatContext ctx, in DamageRequest req, bool requireSource)
        {
            var result = new DamageResult { RawDamage = req.Amount };
            var source = ctx.GetUnit(req.SourceUnitId);
            var target = ctx.GetUnit(req.TargetUnitId);
            if ((requireSource && source == null) || target == null || target.IsDead || req.Amount <= 0) return result;

            result = CalculateSourceDamage(ctx, req.SourceUnitId, req.Amount);
            int dmg = result.AfterWeaken;
            var vulnerable = ctx.FindStatus(req.TargetUnitId, StatusType.Vulnerable);
            if (vulnerable != null)
                dmg = Mathd.FloorToInt(dmg * (1.0 + vulnerable.Percent / 100.0));
            result.AfterVulnerable = dmg;

            var shield = ctx.FindStatus(req.TargetUnitId, StatusType.Shield);
            int absorbed = 0;
            if (shield != null)
            {
                absorbed = dmg < shield.Amount ? dmg : shield.Amount;
                dmg -= absorbed;
                shield.Amount -= absorbed;
                if (shield.Amount <= 0)
                    ctx.RemoveStatus(req.TargetUnitId, StatusType.Shield);
            }
            result.ShieldAbsorbed = absorbed;
            result.FinalDamage = dmg;

            int hpBefore = target.Hp;
            target.TakeDamage(dmg);
            result.TargetDead = target.IsDead;
            ctx.RecordHit(req.SourceUnitId, target.Id, hpBefore - target.Hp, absorbed, target.IsDead);
            return result;
        }
    }

    public sealed class DamageResolver : IDamageResolver
    {
        public DamageResult ResolveDamage(in DamageRequest req, CombatContext ctx) => Damage.Apply(ctx, req);
    }
}