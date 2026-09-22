using System;
using System.Collections.Generic;
using CQ.Core.Battle;

namespace CQ.Core.Combat
{
    /// <summary>
    /// 所有 resolver 的唯一上下文。持有全场单位、Rng、每个单位的生效状态。
    /// 单位 id 统一 string（配置 id）。
    /// </summary>
    public sealed class CombatContext
    {
        public List<Unit> Units;
        public Rng Rng;
        public Dictionary<string, List<StatusEffect>> Statuses;
        public Dictionary<string, int> Haste;   // unitId -> 本回合提速%（瞬态，回合末清；降速走 StatusType.Slow）

        public CombatContext(Rng rng, IEnumerable<Unit> units)
        {
            Rng = rng;
            Units = new List<Unit>(units);
            Statuses = new Dictionary<string, List<StatusEffect>>();
            Haste = new Dictionary<string, int>();
        }

        public Unit GetUnit(string id)
        {
            foreach (var u in Units) if (u.Id == id) return u;
            return null;
        }

        public void AddStatus(string unitId, StatusEffect fx)
        {
            if (string.IsNullOrEmpty(unitId) || fx == null) return;
            if (!Statuses.TryGetValue(unitId, out var list))
            {
                list = new List<StatusEffect>();
                Statuses[unitId] = list;
            }
            list.Add(fx);
        }

        public IReadOnlyList<StatusEffect> GetStatuses(string unitId)
        {
            if (Statuses.TryGetValue(unitId, out var list)) return list;
            return Array.Empty<StatusEffect>();
        }

        public StatusEffect FindStatus(string unitId, StatusType type)
        {
            if (Statuses.TryGetValue(unitId, out var list))
                foreach (var fx in list) if (fx.Type == type) return fx;
            return null;
        }

        public bool HasStatus(string unitId, StatusType type) => FindStatus(unitId, type) != null;

        public void RemoveStatus(string unitId, StatusType type)
        {
            if (Statuses.TryGetValue(unitId, out var list))
                list.RemoveAll(fx => fx.Type == type);
        }

        public void SetHaste(string unitId, int percent)
        {
            if (string.IsNullOrEmpty(unitId)) return;
            Haste[unitId] = percent;
        }

        public int GetHaste(string unitId)
        {
            return Haste.TryGetValue(unitId, out var p) ? p : 0;
        }
    }
}