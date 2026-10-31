namespace CQ.Core.Combat
{
    public static class Damage
    {
        public static int Apply(CombatContext ctx, Unit source, Unit target, int multiplierPercent)
        {
            if (source.IsDead || target.IsDead) return 0;
            int amount = ActionStatuses.CalculateDamage(source, ctx.StatesOf(source.Id),
                target, ctx.StatesOf(target.Id), multiplierPercent);
            int before = target.Hp;
            target.TakeDamage(amount);
            int applied = before - target.Hp;
            ctx.RecordHit(source, target, applied);
            return applied;
        }
    }
}
