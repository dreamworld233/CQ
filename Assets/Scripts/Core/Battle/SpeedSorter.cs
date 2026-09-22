using System;
using System.Collections.Generic;
using System.Linq;
using CQ.Core;
using CQ.Core.Combat;

namespace CQ.Core.Battle
{
    public struct SpeedModifiers
    {
        public int HastePercent;
        public int SlowPercent;
    }

    /// <summary>
    /// 速度混排。有效速度 = baseSpeed × (1 + (提速% − 降速%)/100)，去尾取整。
    /// 排序：有效速度降序；同速我方(Player)先；同方同速保持输入序（稳定）。
    /// 速度修正只由本轮注入的 modifiers 决定，不写入 Unit，故只影响本回合。
    /// </summary>
    public static class SpeedSorter
    {
        public static int EffectiveSpeed(int baseSpeed, int hastePercent, int slowPercent)
        {
            double factor = 1.0 + (hastePercent - slowPercent) / 100.0;
            return Mathd.FloorToInt(baseSpeed * factor);
        }

        public static int EffectiveSpeed(int baseSpeed, SpeedModifiers mods)
            => EffectiveSpeed(baseSpeed, mods.HastePercent, mods.SlowPercent);

        public static List<Unit> Sort(IReadOnlyList<Unit> units, Func<Unit, int> effectiveSpeedOf)
        {
            if (units == null || units.Count == 0) return new List<Unit>();

            return units
                .Select((u, index) => new { Unit = u, Index = index })
                .OrderByDescending(x => effectiveSpeedOf(x.Unit))
                .ThenBy(x => (int)x.Unit.Team)
                .ThenBy(x => x.Index)
                .Select(x => x.Unit)
                .ToList();
        }
    }
}