using System;
using System.Collections.Generic;
using CQ.Core.Config;

namespace CQ.Core.Combat
{
    public sealed class Slot
    {
        public string Id { get; }
        public string Row => Formation.RowOf(Id);
        public Unit Occupant { get; internal set; }
        internal Slot(string id) { Id = id; }
    }

    public sealed class Formation
    {
        private static readonly string[] SlotIds = { "front1", "front2", "back1", "back2", "back3" };
        public const int Capacity = 5;
        private readonly List<Slot> _slots = new List<Slot>();
        public IReadOnlyList<Slot> Slots { get; }
        public Team Team { get; }
        public static bool IsValidSlot(string id) => Array.IndexOf(SlotIds, id) >= 0;
        public static string SlotIdAt(int index) => SlotIds[index];
        public static string RowOf(string id) => !IsValidSlot(id) ? null : id.StartsWith("front", StringComparison.Ordinal) ? "front" : "back";

        public Formation(Team team, FormationConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            Team = team;
            foreach (var id in SlotIds) _slots.Add(new Slot(id));
            Slots = _slots.AsReadOnly();
        }

        public Slot GetSlot(string id) => _slots.Find(slot => slot.Id == id);

        internal void Occupy(Unit unit)
        {
            if (unit.Team != Team) throw new ArgumentException("单位不属于该队", nameof(unit));
            var slot = GetSlot(unit.SlotId);
            if (slot == null) throw new ArgumentException("单位槽位非法: " + unit.Id + "/" + unit.SlotId);
            if (slot.Occupant != null) throw new ArgumentException("槽位重复: " + Team + "/" + unit.SlotId);
            slot.Occupant = unit;
        }

        public bool TryMove(Unit unit, string destination)
        {
            if (unit == null || unit.Team != Team) return false;
            var source = GetSlot(unit.SlotId);
            var target = GetSlot(destination);
            if (source == null || target == null || !ReferenceEquals(source.Occupant, unit)) return false;
            var other = target.Occupant;
            if (other != null && (other.Team != Team || other.SlotId != target.Id)) return false;
            source.Occupant = other;
            target.Occupant = unit;
            if (other != null) other.SlotId = source.Id;
            unit.SlotId = target.Id;
            return true;
        }
    }
}
