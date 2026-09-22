using CQ.Core;

namespace CQ.Core.Combat
{
    /// <summary>
    /// 确定性伤害管线：基础伤害 × 攻增(源) → 去尾 → × 虚弱(源) → 去尾 → 护盾(目标) → 扣 HP。
    /// 攻增/虚弱读攻击方（源）状态，护盾读受击方（目标）状态；护盾挡到 0 后移除（once 语义）。
    /// </summary>
    public static class Damage
    {
        public static DamageResult Apply(CombatContext ctx, in DamageRequest req)
        {
            var result = new DamageResult { RawDamage = req.Amount };
            var source = ctx.GetUnit(req.SourceUnitId);
            var target = ctx.GetUnit(req.TargetUnitId);
            if (source == null || target == null || target.IsDead || req.Amount <= 0) return result;

            int dmg = req.Amount;

            var attackUp = ctx.FindStatus(req.SourceUnitId, StatusType.AttackUp);
            if (attackUp != null)
                dmg = Mathd.FloorToInt(dmg * (1.0 + attackUp.Percent / 100.0));
            result.AfterAttackUp = dmg;

            var weaken = ctx.FindStatus(req.SourceUnitId, StatusType.Weaken);
            if (weaken != null)
                dmg = Mathd.FloorToInt(dmg * (1.0 - weaken.Percent / 100.0));
            result.AfterWeaken = dmg;

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

            target.TakeDamage(dmg);
            result.TargetDead = target.IsDead;
            return result;
        }
    }

    public sealed class DamageResolver : IDamageResolver
    {
        public DamageResult ResolveDamage(in DamageRequest req, CombatContext ctx) => Damage.Apply(ctx, req);
    }
}