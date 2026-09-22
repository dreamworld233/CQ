using System.Collections.Generic;
using CQ.Core.Config;

namespace CQ.Core.Combat
{
    /// <summary>
    /// 角色技能解析器（T3）：basic / skill / ult 三循环 + 能量/大招条。
    /// 能量/大招条改动只走 GainEnergy / GainUlt；一切数值来自 SkillSpec 与 Unit.MaxEnergy / MaxUlt，零硬编码。
    /// </summary>
    public static class SkillResolver
    {
        public static void Resolve(
            SkillSpec skill,
            CombatContext ctx,
            Unit caster,
            Unit target,
            IReadOnlyList<Unit> allies,
            IReadOnlyList<Unit> enemies)
        {
            if (skill == null || caster == null) return;

            switch (skill.type)
            {
                case "basic":
                    ResolveBasic(skill, ctx, caster, target, enemies);
                    break;
                case "skill":
                    ResolveSkill(skill, ctx, caster, target, allies, enemies);
                    break;
                case "ult":
                    ResolveUlt(skill, ctx, caster, target, allies, enemies);
                    break;
            }
        }

        private static void ResolveBasic(
            SkillSpec skill, CombatContext ctx, Unit caster, Unit target, IReadOnlyList<Unit> enemies)
        {
            caster.GainEnergy(skill.energyDelta);
            caster.GainUlt(skill.ultDelta);
            ApplyDamage(skill, ctx, caster, target, enemies);
        }

        private static void ResolveSkill(
            SkillSpec skill, CombatContext ctx, Unit caster, Unit target,
            IReadOnlyList<Unit> allies, IReadOnlyList<Unit> enemies)
        {
            if (caster.Energy < skill.energyCost) return;
            caster.GainEnergy(-skill.energyCost);
            caster.GainUlt(skill.ultDelta);
            ApplyDamage(skill, ctx, caster, target, enemies);
            ApplyHeal(skill, caster, target, allies);
        }

        private static void ResolveUlt(
            SkillSpec skill, CombatContext ctx, Unit caster, Unit target,
            IReadOnlyList<Unit> allies, IReadOnlyList<Unit> enemies)
        {
            if (caster.Ult < caster.MaxUlt) return;
            caster.GainUlt(-caster.MaxUlt);
            ApplyDamage(skill, ctx, caster, target, enemies);
            ApplyHeal(skill, caster, target, allies);
        }

        private static void ApplyDamage(
            SkillSpec skill, CombatContext ctx, Unit caster, Unit target, IReadOnlyList<Unit> enemies)
        {
            if (skill.damage == null) return;

            if (skill.damage.target == "all")
            {
                foreach (var enemy in enemies)
                {
                    if (enemy == null) continue;
                    Damage.Apply(ctx, new DamageRequest
                    {
                        Amount = skill.damage.amount,
                        Kind = skill.damage.kind,
                        SourceUnitId = caster.Id,
                        TargetUnitId = enemy.Id
                    });
                }
            }
            else
            {
                if (target == null) return;
                Damage.Apply(ctx, new DamageRequest
                {
                    Amount = skill.damage.amount,
                    Kind = skill.damage.kind,
                    SourceUnitId = caster.Id,
                    TargetUnitId = target.Id
                });
            }
        }

        private static void ApplyHeal(
            SkillSpec skill, Unit caster, Unit target, IReadOnlyList<Unit> allies)
        {
            if (skill.heal == null) return;

            switch (skill.heal.target)
            {
                case "self":
                    caster.Heal(skill.heal.amount);
                    break;
                case "all":
                    foreach (var ally in allies)
                    {
                        if (ally == null) continue;
                        ally.Heal(skill.heal.amount);
                    }
                    break;
                default:
                    if (target != null) target.Heal(skill.heal.amount);
                    break;
            }
        }
    }
}