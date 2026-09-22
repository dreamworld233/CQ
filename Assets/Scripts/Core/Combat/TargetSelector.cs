using System.Collections.Generic;

namespace CQ.Core.Combat
{
    /// <summary>
    /// 目标选择。按 AggroWeight 加权随机选一个存活单位，使用 ctx.Rng。
    /// aggroWeight &lt;= 0 视为 1。全部阵亡时返回 null。
    /// </summary>
    public static class TargetSelector
    {
        public static Unit PickTarget(CombatContext ctx, IReadOnlyList<Unit> candidates)
        {
            if (ctx == null || candidates == null) return null;

            var alive = new List<Unit>();
            int total = 0;
            foreach (var u in candidates)
            {
                if (u == null || u.IsDead) continue;
                alive.Add(u);
                total += u.AggroWeight <= 0 ? 1 : u.AggroWeight;
            }
            if (alive.Count == 0 || total <= 0) return null;

            double roll = ctx.Rng.NextDouble() * total;
            double acc = 0.0;
            foreach (var u in alive)
            {
                acc += u.AggroWeight <= 0 ? 1 : u.AggroWeight;
                if (roll < acc) return u;
            }
            return alive[alive.Count - 1];
        }
    }
}