using System;
using CQ.Core.Config;

namespace CQ.Core.Combat
{
    public static class SkillResolver
    {
        public static void Resolve(CombatContext ctx, Unit actor, SkillSpec skill)
        {
            foreach (var effect in skill.effects)
            {
                var targets = TargetSelector.Select(ctx, actor, effect.target, effect.count);
                foreach (var target in targets)
                {
                    switch (effect.type)
                    {
                        case "damage":
                            for (int hit = 0; hit < effect.hits && !target.IsDead; hit++)
                                Damage.Apply(ctx, actor, target, effect.multiplierPercent);
                            break;
                        case "heal":
                            ctx.Heal(actor, target, (int)Math.Min(target.MaxHp,
                                decimal.Floor((decimal)target.MaxHp * effect.percent / 100m)));
                            break;
                        case "energy":
                            ctx.GainEnergy(target, effect.amount, skill.type == "passive" ? "passive" : "skill");
                            break;
                        case "attackUp":
                            ctx.StatesOf(target.Id).Apply(StatusType.AttackUp, effect.percent, effect.duration);
                            break;
                        case "defenseUp":
                            ctx.StatesOf(target.Id).Apply(StatusType.DefenseUp, effect.percent, effect.duration);
                            break;
                        case "stun":
                            ctx.StatesOf(target.Id).Apply(StatusType.Stun, 0, effect.duration);
                            break;
                        default:
                            throw new InvalidOperationException("未知技能效果: " + effect.type);
                    }
                }
            }
        }
    }
}
