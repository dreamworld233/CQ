using System;
using System.Collections.Generic;

namespace CQ.Core.Combat
{
    public sealed class ActionStatuses
    {
        private sealed class Entry
        {
            public StatusType Type;
            public int Percent;
            public int Remaining;
            public long Revision;
            public bool Permanent;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private List<(Entry entry, long revision)> _atStart;

        public void Apply(StatusType type, int percent, int actions)
        {
            if (percent < 0 || actions <= 0) throw new ArgumentOutOfRangeException(nameof(actions));
            var current = _entries.Find(e => e.Type == type && e.Percent == percent && !e.Permanent);
            if (current == null)
                _entries.Add(new Entry { Type = type, Percent = percent, Remaining = actions });
            else if (actions >= current.Remaining)
            {
                current.Remaining = actions;
                current.Revision++;
            }
        }

        public void ApplyPermanent(StatusType type, int percent)
        {
            if ((type != StatusType.AttackUp && type != StatusType.DefenseUp) || percent <= 0)
                throw new ArgumentOutOfRangeException(nameof(percent));
            var current = _entries.Find(e => e.Type == type && e.Percent == percent && e.Permanent);
            if (current == null)
                _entries.Add(new Entry { Type = type, Percent = percent, Permanent = true });
        }

        public int StrongestPercent(StatusType type)
        {
            int percent = 0;
            foreach (var entry in _entries)
                if (entry.Type == type) percent = Math.Max(percent, entry.Percent);
            return percent;
        }

        public bool Has(StatusType type) => _entries.Exists(e => e.Type == type);

        public IReadOnlyList<StatusEffect> Snapshot()
        {
            var result = new List<StatusEffect>();
            foreach (var entry in _entries)
                result.Add(new StatusEffect
                {
                    Type = entry.Type, Percent = entry.Percent,
                    Duration = entry.Remaining, Turn = entry.Permanent ? "permanent" : "action"
                });
            return result.AsReadOnly();
        }

        public void BeginAction()
        {
            if (_atStart != null) throw new InvalidOperationException("状态行动边界不可嵌套");
            _atStart = new List<(Entry, long)>();
            foreach (var entry in _entries)
                if (!entry.Permanent) _atStart.Add((entry, entry.Revision));
        }

        public void CompleteAction()
        {
            if (_atStart == null) throw new InvalidOperationException("没有进行中的状态行动");
            // 行动内新施加或续长的状态，从下次行动开始消耗。
            foreach (var item in _atStart)
                if (item.entry.Revision == item.revision) item.entry.Remaining--;
            _entries.RemoveAll(e => !e.Permanent && e.Remaining <= 0);
            _atStart = null;
        }

        public decimal EffectiveAttack(Unit unit) => unit.BaseAttack * (1m + StrongestPercent(StatusType.AttackUp) / 100m);
        public decimal EffectiveDefense(Unit unit) => unit.BaseDefense * (1m + StrongestPercent(StatusType.DefenseUp) / 100m);

        public static int CalculateDamage(Unit attacker, ActionStatuses attackerStatuses,
            Unit defender, ActionStatuses defenderStatuses, int multiplierPercent)
        {
            if (multiplierPercent <= 0) throw new ArgumentOutOfRangeException(nameof(multiplierPercent));
            decimal attack = attackerStatuses.EffectiveAttack(attacker);
            decimal defense = defenderStatuses.EffectiveDefense(defender);
            return (int)Math.Min(int.MaxValue,
                Math.Max(1m, decimal.Floor(attack * multiplierPercent / 100m) - decimal.Floor(defense)));
        }
    }
}
