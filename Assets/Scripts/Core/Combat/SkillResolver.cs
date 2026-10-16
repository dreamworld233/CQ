using System.Collections.Generic;
using CQ.Core.Battle;
using CQ.Core.Config;

namespace CQ.Core.Combat
{
    public static class SkillResolver
    {
        public static bool CanUse(SkillSpec skill, Unit caster) =>
            skill != null && caster != null && !caster.IsDead &&
            (skill.type == "basic" || (skill.type == "ult" && caster.MaxUlt > 0 && caster.Ult >= caster.MaxUlt));

        private static bool Contains(IReadOnlyList<Unit> units, Unit target)
        {
            if (target == null || target.IsDead || units == null) return false;
            foreach (var unit in units) if (ReferenceEquals(unit, target)) return true;
            return false;
        }

        public static ActionResult Resolve(SkillSpec skill, CombatContext ctx, Unit caster, Unit target,
            IReadOnlyList<Unit> allies, IReadOnlyList<Unit> enemies)
        {
            if (ctx == null || !CanUse(skill, caster) || !ReferenceEquals(ctx.GetUnit(caster.Id), caster)) return null;
            // 先验目标，避免非法请求扣费或改变后续随机序列。
            if (skill.damage != null && skill.damage.target == "snipe" &&
                (!Contains(enemies, target) || target.Team == caster.Team || !ReferenceEquals(ctx.GetUnit(target.Id), target))) return null;
            if (skill.heal != null && skill.heal.target == "single" &&
                (!Contains(allies, target) || target.Team != caster.Team || !ReferenceEquals(ctx.GetUnit(target.Id), target))) return null;
            Unit damageTarget = target;
            if (skill.damage != null && skill.damage.target == "single")
            {
                damageTarget = TargetSelector.PickTarget(ctx, enemies);
                if (damageTarget == null) return null;
            }
            ctx.BeginAction(BattleEventKind.Skill, caster, skill.id, skill.type == "basic");
            if (skill.type == "ult") caster.GainUlt(-caster.MaxUlt);
            if (skill.damage != null)
            {
                if (skill.damage.target == "all")
                {
                    foreach (var enemy in enemies) Hit(skill, ctx, caster, enemy);
                }
                else Hit(skill, ctx, caster, damageTarget);
            }
            if (skill.heal != null)
            {
                switch (skill.heal.target)
                {
                    case "self": Heal(caster, skill.heal.amount); break;
                    case "all": foreach (var ally in allies) Heal(ally, skill.heal.amount); break;
                    case "single": Heal(target, skill.heal.amount); break;
                }
            }
            return ctx.CompleteAction();
        }

        private static void Heal(Unit unit, int amount)
        {
            if (unit != null && !unit.IsDead) unit.Heal(amount);
        }

        private static void Hit(SkillSpec skill, CombatContext ctx, Unit caster, Unit target)
        {
            if (target == null || target.Team == caster.Team) return;
            Damage.Apply(ctx, new DamageRequest { Amount = skill.damage.amount, Kind = skill.damage.kind,
                SourceUnitId = caster.Id, TargetUnitId = target.Id });
        }
    }
}
