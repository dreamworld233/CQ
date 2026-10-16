using System;
using System.Collections.Generic;
using CQ.Core.Config;

namespace CQ.Core.Combat
{
    /// <summary>格子拥有嘲讽及临时修正；占用者死亡后保留位置，但有效嘲讽为零。</summary>
    public sealed class Slot
    {
        private sealed class Modifier
        {
            public int Delta;
            public int Duration;
        }
        private readonly List<Modifier> _modifiers = new List<Modifier>();
        public string Id { get; }
        public string Row => Formation.RowOf(Id);
        public int BaseTaunt { get; }
        public Unit Occupant { get; internal set; }
        public int EffectiveTaunt
        {
            get
            {
                if (Occupant == null || Occupant.IsDead) return 0;
                long value = BaseTaunt;
                foreach (var modifier in _modifiers) value += modifier.Delta;
                return (int)Math.Max(0L, Math.Min(int.MaxValue, value));
            }
        }
        internal Slot(string id, int baseTaunt)
        {
            Id = id;
            BaseTaunt = baseTaunt;
        }
        public void AddModifier(int delta, int duration)
        {
            if (duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
            _modifiers.Add(new Modifier { Delta = delta, Duration = duration });
        }
        public void Tick()
        {
            for (int i = _modifiers.Count - 1; i >= 0; i--)
                if (--_modifiers[i].Duration <= 0) _modifiers.RemoveAt(i);
        }
    }

    /// <summary>每队固定前二后三；禁止隐式占位及死亡后补位。</summary>
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
            string error = ConfigValidator.ValidateFormation(config);
            if (error != null) throw new ArgumentException(error, nameof(config));
            Team = team;
            foreach (var id in SlotIds)
                _slots.Add(new Slot(id, RowOf(id) == "front" ? config.frontBaseTaunt : config.backBaseTaunt));
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
        public void Tick()
        {
            foreach (var slot in _slots) slot.Tick();
        }
    }
}
