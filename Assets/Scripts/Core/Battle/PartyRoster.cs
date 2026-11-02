using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CQ.Core.Combat;
using CQ.Core.Config;
using CQ.Core.Persistence;

namespace CQ.Core.Battle
{
    /// <summary>可选伙伴与实际部署分开；新增可选伙伴不会自动进入已有阵容。</summary>
    public sealed class PartyRoster
    {
        private readonly Dictionary<string, string> _deployed = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _remembered = new Dictionary<string, string>();
        public IReadOnlyList<CharacterConfig> Available { get; }
        public IReadOnlyDictionary<string, string> Deployment { get; }
        public bool CanStart => _deployed.Count > 0;

        public PartyRoster(IEnumerable<CharacterConfig> characters, IEnumerable<string> availableIds, PartyRoster previous = null)
        {
            if (characters == null) throw new ArgumentNullException(nameof(characters));
            var catalog = characters.ToDictionary(character => character.id);
            var available = new List<CharacterConfig>();
            var seen = new HashSet<string>();
            foreach (var id in availableIds ?? catalog.Keys)
            {
                if (id == null || !seen.Add(id) || !catalog.TryGetValue(id, out var character))
                    throw new ArgumentException("可选伙伴id空、重复或不存在", nameof(availableIds));
                available.Add(character);
                _remembered[id] = previous != null && previous._remembered.TryGetValue(id, out var slot) ? slot : character.defaultSlot;
                if (previous != null && previous._deployed.TryGetValue(id, out var deployed)) _deployed[id] = deployed;
            }
            Available = available.AsReadOnly();
            Deployment = new ReadOnlyDictionary<string, string>(_deployed);
            if (previous == null)
                foreach (var character in available.Take(Formation.Capacity)) TryToggle(character.id, out _);
        }

        public PartnerSave[] CaptureSave() => Available.Select(character => new PartnerSave
        {
            id = character.id, slot = _remembered[character.id], deployed = _deployed.ContainsKey(character.id)
        }).ToArray();

        public void RestoreSave(IEnumerable<PartnerSave> records)
        {
            if (records == null) throw new ArgumentNullException(nameof(records));
            var saved = records.ToArray();
            if (saved.Any(p => p == null || !_remembered.ContainsKey(p.id) || !Formation.IsValidSlot(p.slot)) ||
                saved.Select(p => p.id).Distinct().Count() != saved.Length ||
                saved.Count(p => p.deployed) > Formation.Capacity ||
                saved.Where(p => p.deployed).Select(p => p.slot).Distinct().Count() != saved.Count(p => p.deployed))
                throw new ArgumentException("存档伙伴或站位与当前关卡不兼容", nameof(records));
            _deployed.Clear();
            foreach (var record in saved)
            {
                _remembered[record.id] = record.slot;
                if (record.deployed) _deployed[record.id] = record.slot;
            }
        }

        public bool IsDeployed(string id) => id != null && _deployed.ContainsKey(id);

        public bool TryToggle(string id, out string error)
        {
            error = null;
            if (id == null || !_remembered.ContainsKey(id)) { error = "伙伴尚不可用"; return false; }
            if (_deployed.TryGetValue(id, out var oldSlot))
            {
                _remembered[id] = oldSlot;
                _deployed.Remove(id);
                return true;
            }
            if (_deployed.Count >= Formation.Capacity) { error = "阵容已满，请先下阵一位伙伴"; return false; }
            string target = _remembered[id];
            if (!Formation.IsValidSlot(target) || _deployed.Values.Contains(target))
                target = Enumerable.Range(0, Formation.Capacity).Select(Formation.SlotIdAt).First(slot => !_deployed.Values.Contains(slot));
            _deployed[id] = _remembered[id] = target;
            return true;
        }

        public void CapturePositions(IEnumerable<Unit> units)
        {
            var updated = new Dictionary<string, string>(_deployed);
            foreach (var unit in units)
                if (updated.ContainsKey(unit.Id)) updated[unit.Id] = unit.SlotId;
            if (updated.Values.Any(slot => !Formation.IsValidSlot(slot)) || updated.Values.Distinct().Count() != updated.Count)
                throw new ArgumentException("部署位置非法或重复", nameof(units));
            foreach (var pair in updated) _deployed[pair.Key] = _remembered[pair.Key] = pair.Value;
        }
    }
}
