using System;
using System.Collections.Generic;
using CQ.Core.Battle;
using CQ.Core.Config;

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

        private readonly List<ActionResult> _results = new List<ActionResult>();
        public IReadOnlyList<ActionResult> ActionResults => _results.AsReadOnly();
        private List<UnitSnapshot> _before;
        private List<AttackHit> _hits;
        private Unit _actor;
        private string _actionId;
        private BattleEventKind _kind;
        private bool _attackCharge;
        private long _sequence;

        public void BeginAction(BattleEventKind kind, Unit actor, string actionId, bool attackCharge)
        {
            if (_before != null) throw new InvalidOperationException("动作边界不可嵌套");
            _before = new List<UnitSnapshot>();
            foreach (var unit in Units) _before.Add(new UnitSnapshot(unit, this));
            _hits = new List<AttackHit>();
            _actor = actor; _actionId = actionId; _kind = kind; _attackCharge = attackCharge;
        }

        internal void RecordHit(string source, string target, int hpDamage, int absorbed, bool dead)
        {
            if (_hits != null) _hits.Add(new AttackHit(source, target, hpDamage, absorbed, dead));
        }

        public ActionResult CompleteAction()
        {
            if (_before == null) throw new InvalidOperationException("没有进行中的动作");
            var charges = new List<UltCharge>();
            if (_attackCharge && _actor != null && _hits.Exists(h => h.SourceUnitId == _actor.Id))
                Charge(_actor, "attack", charges);
            var seen = new HashSet<string>();
            foreach (var hit in _hits)
                if (seen.Add(hit.TargetUnitId)) Charge(GetUnit(hit.TargetUnitId), "hit", charges);
            var changes = new List<UnitChange>();
            foreach (var before in _before)
                changes.Add(new UnitChange(before, new UnitSnapshot(GetUnit(before.UnitId), this)));
            var result = new ActionResult(++_sequence, _kind, _actor?.Id, _actionId, changes, _hits, charges);
            _before = null; _hits = null; _actor = null;
            _results.Add(result);
            return result;
        }

        private static void Charge(Unit unit, string trigger, List<UltCharge> charges)
        {
            if (unit == null || unit.IsDead || unit.UltChargeTrigger != trigger || unit.UltChargeAmount <= 0) return;
            int before = unit.Ult;
            unit.GainUlt(unit.UltChargeAmount);
            if (unit.Ult != before) charges.Add(new UltCharge(unit.Id, trigger, before, unit.Ult));
        }

        private readonly Formation _players;
        private readonly Formation _enemies;

        public Formation FormationOf(Team team) => team == Team.Player ? _players : _enemies;

        public int EffectiveTaunt(Unit unit)
        {
            if (unit == null || unit.IsDead) return 0;
            var slot = FormationOf(unit.Team).GetSlot(unit.SlotId);
            return slot != null && ReferenceEquals(slot.Occupant, unit) ? slot.EffectiveTaunt : 0;
        }

        public CombatContext(Rng rng, IEnumerable<Unit> units, FormationConfig formationConfig = null)
        {
            Rng = rng ?? throw new ArgumentNullException(nameof(rng));
            Units = new List<Unit>(units);
            var rules = formationConfig ?? new FormationConfig();
            _players = new Formation(Team.Player, rules);
            _enemies = new Formation(Team.Enemy, rules);
            var ids = new HashSet<string>();
            foreach (var unit in Units)
            {
                if (unit == null || string.IsNullOrEmpty(unit.Id) || !ids.Add(unit.Id))
                    throw new ArgumentException("单位为空或 id 重复", nameof(units));
                FormationOf(unit.Team).Occupy(unit);
            }
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