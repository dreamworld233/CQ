using System;
using System.Collections.Generic;
using System.Linq;

namespace CQ.Core.Combat
{
    public static class TargetSelector
    {
        public static IReadOnlyList<Unit> Select(CombatContext ctx, Unit actor, string target, int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (target == "self") return actor.IsDead ? Array.Empty<Unit>() : new[] { actor };
            bool enemy = target.StartsWith("enemy", StringComparison.Ordinal);
            var candidates = ctx.Units.Where(u => !u.IsDead && (enemy ? u.Team != actor.Team : u.Team == actor.Team)).ToList();
            switch (target)
            {
                case "enemyFront":
                case "allyFront":
                    PreferRow(candidates, "front");
                    break;
                case "enemyBack":
                    PreferRow(candidates, "back");
                    break;
                case "allyLowestHp":
                    if (candidates.Count > 0)
                    {
                        var lowest = candidates[0];
                        foreach (var unit in candidates)
                            if ((long)unit.Hp * lowest.MaxHp < (long)lowest.Hp * unit.MaxHp) lowest = unit;
                        candidates.RemoveAll(u => (long)u.Hp * lowest.MaxHp != (long)lowest.Hp * u.MaxHp);
                    }
                    break;
                case "allyLowestEnergy":
                    if (candidates.Count > 0)
                    {
                        int minimum = candidates.Min(u => u.Ult);
                        candidates.RemoveAll(u => u.Ult != minimum);
                    }
                    break;
                case "enemyAll":
                case "allyAll":
                    return candidates.AsReadOnly();
                case "enemyRandom":
                case "allyRandom":
                    break;
                default:
                    throw new ArgumentException("未知目标规则: " + target, nameof(target));
            }
            if (count >= candidates.Count) return candidates.AsReadOnly();
            var selected = new List<Unit>();
            while (selected.Count < count)
            {
                int index = ctx.Rng.NextInt(candidates.Count);
                selected.Add(candidates[index]);
                candidates.RemoveAt(index);
            }
            return selected.AsReadOnly();
        }

        private static void PreferRow(List<Unit> candidates, string row)
        {
            if (candidates.Exists(u => u.Row == row)) candidates.RemoveAll(u => u.Row != row);
        }
    }
}
