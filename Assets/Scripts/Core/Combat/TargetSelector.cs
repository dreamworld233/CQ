using System.Collections.Generic;

namespace CQ.Core.Combat
{
    /// <summary>只在最高有效格子嘲讽的存活候选间随机；全零仍可选。</summary>
    public static class TargetSelector
    {
        public static Unit PickTarget(CombatContext ctx, IReadOnlyList<Unit> candidates)
        {
            if (ctx == null || candidates == null) return null;
            var highest = new List<Unit>();
            var seen = new HashSet<Unit>();
            int maximum = -1;
            foreach (var unit in candidates)
            {
                if (unit == null || unit.IsDead || !seen.Add(unit)) continue;
                var slot = ctx.FormationOf(unit.Team).GetSlot(unit.SlotId);
                if (slot == null || !ReferenceEquals(slot.Occupant, unit)) continue;
                int taunt = slot.EffectiveTaunt;
                if (taunt > maximum)
                {
                    maximum = taunt;
                    highest.Clear();
                }
                if (taunt == maximum) highest.Add(unit);
            }
            return highest.Count == 0 ? null : highest[ctx.Rng.NextInt(highest.Count)];
        }
    }
}
