using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Config;

namespace CQ.Core.Combat
{
    public static class SkillResolver
    {
        public static bool CanUse(SkillSpec skill, Unit caster) =>
            skill != null && caster != null && !caster.IsDead &&
            (skill.type == "basic" || (skill.type == "ult" && caster.MaxUlt > 0 && caster.Ult >= caster.MaxUlt));

        public static bool IsValidTarget(SkillSpec skill, CombatContext ctx, Unit caster)
        {
            if (skill == null || ctx == null || caster == null || caster.IsDead ||
                !ReferenceEquals(ctx.GetUnit(caster.Id), caster)) return false;
            if ((skill.damage == null) == (skill.heal == null)) return false;
            if (skill.damage != null)
                return (skill.damage.target == "single" || skill.damage.target == "all") &&
                    ctx.Units.Any(u => u.Team != caster.Team && !u.IsDead);
            return skill.heal.target == "single" || skill.heal.target == "self" || skill.heal.target == "all";
        }

        public static ActionResult Resolve(SkillSpec skill, CombatContext ctx, Unit caster) =>
            ResolveEffect(skill, ctx, caster, false);

        internal static ActionResult ResolveReserved(SkillSpec skill, CombatContext ctx, Unit caster) =>
            ResolveEffect(skill, ctx, caster, true);

        private static ActionResult ResolveEffect(SkillSpec skill, CombatContext ctx, Unit caster, bool reserved)
        {
            if (!IsValidTarget(skill, ctx, caster) || (!reserved && !CanUse(skill, caster))) return null;
            var enemies = ctx.Units.Where(u => u.Team != caster.Team && !u.IsDead).ToList();
            Unit damageTarget = skill.damage?.target == "single" ? TargetSelector.PickTarget(ctx, enemies) : null;
            // Evaluate healing at execution, in registration order; never consume combat Rng.
            Unit healTarget = null;
            if (skill.heal?.target == "single")
                foreach (var ally in ctx.Units)
                    if (ally.Team == caster.Team && !ally.IsDead && ally.MaxHp > 0 &&
                        (healTarget == null || (long)ally.Hp * healTarget.MaxHp < (long)healTarget.Hp * ally.MaxHp))
                        healTarget = ally;
            ctx.BeginAction(BattleEventKind.Skill, caster, skill.id, skill.type == "basic");
            if (skill.type == "ult" && !reserved) caster.GainUlt(-caster.MaxUlt);
            if (skill.damage != null)
            {
                if (skill.damage.target == "all")
                    foreach (var enemy in enemies) Hit(skill, ctx, caster, enemy);
                else Hit(skill, ctx, caster, damageTarget);
            }
            if (skill.heal != null)
            {
                switch (skill.heal.target)
                {
                    case "self": caster.Heal(skill.heal.amount); break;
                    case "all":
                        foreach (var ally in ctx.Units)
                            if (ally.Team == caster.Team && !ally.IsDead) ally.Heal(skill.heal.amount);
                        break;
                    case "single": healTarget?.Heal(skill.heal.amount); break;
                }
            }
            return ctx.CompleteAction();
        }

        private static void Hit(SkillSpec skill, CombatContext ctx, Unit caster, Unit target)
        {
            if (target == null || target.Team == caster.Team) return;
            Damage.Apply(ctx, new DamageRequest { Amount = skill.damage.amount, Kind = skill.damage.kind,
                SourceUnitId = caster.Id, TargetUnitId = target.Id });
        }
    }
}
