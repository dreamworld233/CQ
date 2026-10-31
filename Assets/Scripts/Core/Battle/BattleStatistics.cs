using System.Collections.Generic;
using System.Linq;
using CQ.Core.Combat;

namespace CQ.Core.Battle
{
    public sealed class UnitBattleStatistics
    {
        public string UnitId { get; }
        public string Name { get; }
        public Team Team { get; }
        public long DamageDealt { get; internal set; }
        public long DamageTaken { get; internal set; }
        public long HealingDone { get; internal set; }

        internal UnitBattleStatistics(Unit unit)
        { UnitId = unit.Id; Name = unit.Name; Team = unit.Team; }
    }

    public static class BattleStatistics
    {
        public static IReadOnlyList<UnitBattleStatistics> Summarize(BattleEngine engine)
        {
            var rows = engine.Ctx.Units.Select(unit => new UnitBattleStatistics(unit)).ToList();
            var byId = rows.ToDictionary(row => row.UnitId);
            foreach (var action in engine.ActionResults)
            {
                foreach (var hit in action.Hits)
                {
                    byId[hit.SourceUnitId].DamageDealt += hit.HpDamage;
                    byId[hit.TargetUnitId].DamageTaken += hit.HpDamage;
                }
                foreach (var heal in action.Heals)
                    byId[heal.SourceUnitId].HealingDone += heal.HpRestored;
            }
            return rows.AsReadOnly();
        }
    }
}
