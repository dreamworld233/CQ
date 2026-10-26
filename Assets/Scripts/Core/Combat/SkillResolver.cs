using System.Collections.Generic;
using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Cards;
using CQ.Core.Config;

namespace CQ.Core.Combat
{
    public static class SkillResolver
    {
        public static bool CanUse(SkillSpec skill, Unit caster) =>
            skill != null && caster != null && !caster.IsDead &&
            (skill.type == "basic" || (skill.type == "ult" && caster.MaxUlt > 0 && caster.Ult >= caster.MaxUlt));

        public static bool IsValidTarget(SkillSpec skill, CombatContext ctx, Unit caster, Deck deck = null)
        {
            if (skill == null || ctx == null || caster == null || caster.IsDead ||
                !ReferenceEquals(ctx.GetUnit(caster.Id), caster)) return false;
            if (skill.type == "ult")
                return deck != null && ConfigValidator.ValidateInjection(skill) == null;
            return skill.type == "basic" && skill.heal == null && skill.damage != null &&
                (skill.injectCards == null || skill.injectCards.Count == 0) &&
                (skill.damage.target == "single" || skill.damage.target == "all") &&
                ctx.Units.Any(u => u.Team != caster.Team && !u.IsDead);
        }

        public static ActionResult Resolve(SkillSpec skill, CombatContext ctx, Unit caster, Deck deck = null) =>
            ResolveEffect(skill, ctx, caster, deck, false);

        internal static ActionResult ResolveReserved(SkillSpec skill, CombatContext ctx, Unit caster, Deck deck) =>
            ResolveEffect(skill, ctx, caster, deck, true);

        private static ActionResult ResolveEffect(SkillSpec skill, CombatContext ctx, Unit caster, Deck deck, bool reserved)
        {
            if (!IsValidTarget(skill, ctx, caster, deck) || (!reserved && !CanUse(skill, caster))) return null;
            if (skill.type == "ult")
            {
                ctx.BeginAction(BattleEventKind.Skill, caster, skill.id, false);
                if (!reserved) caster.GainUlt(-caster.MaxUlt);
                var injected = new List<string>();
                foreach (var entry in skill.injectCards)
                    for (int i = 0; i < entry.count; i++)
                    {
                        deck.InsertIntoDrawPile(entry.cardId);
                        injected.Add(entry.cardId);
                    }
                return ctx.CompleteAction(injectedCardIds: injected);
            }

            var enemies = ctx.Units.Where(u => u.Team != caster.Team && !u.IsDead).ToList();
            Unit target = skill.damage.target == "single" ? TargetSelector.PickTarget(ctx, enemies) : null;
            ctx.BeginAction(BattleEventKind.Skill, caster, skill.id, true);
            if (skill.damage.target == "all")
                foreach (var enemy in enemies) Hit(skill, ctx, caster, enemy);
            else Hit(skill, ctx, caster, target);
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
