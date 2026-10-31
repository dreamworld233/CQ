using System;
using System.Collections.Generic;
using CQ.Core.Battle;
using CQ.Core.Config;

namespace CQ.Core.Combat
{
    public sealed class CombatContext
    {
        public IReadOnlyList<Unit> Units { get; }
        public Rng Rng { get; }
        private readonly Dictionary<string, Unit> _units = new Dictionary<string, Unit>();
        private readonly Dictionary<string, ActionStatuses> _states = new Dictionary<string, ActionStatuses>();
        private readonly Dictionary<string, PersonalActionCycle> _cycles = new Dictionary<string, PersonalActionCycle>();
        private readonly Formation _players;
        private readonly Formation _enemies;
        private readonly List<ActionResult> _results = new List<ActionResult>();
        public IReadOnlyList<ActionResult> ActionResults { get; }
        private List<UnitSnapshot> _before;
        private List<AttackHit> _hits;
        private List<UltCharge> _charges;
        private List<HealingHit> _heals;
        private Unit _actor;
        private long _sequence;

        public CombatContext(Rng rng, IEnumerable<Unit> units,
            IReadOnlyDictionary<string, CharacterConfig> configs, FormationConfig formationConfig = null)
        {
            Rng = rng ?? throw new ArgumentNullException(nameof(rng));
            var all = new List<Unit>(units);
            Units = all.AsReadOnly();
            ActionResults = _results.AsReadOnly();
            var formation = formationConfig ?? new FormationConfig();
            _players = new Formation(Team.Player, formation);
            _enemies = new Formation(Team.Enemy, formation);
            foreach (var unit in all)
            {
                if (unit == null || string.IsNullOrEmpty(unit.Id) || _units.ContainsKey(unit.Id))
                    throw new ArgumentException("单位为空或id重复", nameof(units));
                _units.Add(unit.Id, unit);
                FormationOf(unit.Team).Occupy(unit);
                _states.Add(unit.Id, new ActionStatuses());
                var config = configs[unit.Id];
                var passive = config.skills.Find(skill => skill.type == "passive");
                if (passive.trigger == "permanent")
                    foreach (var effect in passive.effects)
                        _states[unit.Id].ApplyPermanent(
                            effect.type == "attackUp" ? StatusType.AttackUp : StatusType.DefenseUp, effect.percent);
                _cycles.Add(unit.Id, new PersonalActionCycle(unit, config.initialEnergy, config.maxEnergy,
                    config.actionEnergy, config.hitEnergy, config.specialInterval));
            }
        }

        public Unit GetUnit(string id) => id != null && _units.TryGetValue(id, out var unit) ? unit : null;
        public Formation FormationOf(Team team) => team == Team.Player ? _players : _enemies;
        public ActionStatuses StatesOf(string id) => _states[id];
        public PersonalActionCycle CycleOf(string id) => _cycles[id];
        public IReadOnlyList<StatusEffect> GetStatuses(string id) => StatesOf(id).Snapshot();

        public void BeginAction(Unit actor)
        {
            if (_before != null) throw new InvalidOperationException("动作边界不可嵌套");
            _before = new List<UnitSnapshot>();
            foreach (var unit in Units) _before.Add(new UnitSnapshot(unit, this));
            _hits = new List<AttackHit>();
            _charges = new List<UltCharge>();
            _heals = new List<HealingHit>();
            _actor = actor;
        }

        internal void RecordHit(Unit source, Unit target, int damage)
        {
            _hits?.Add(new AttackHit(source.Id, target.Id, damage, target.IsDead));
        }

        public void Heal(Unit source, Unit target, int amount)
        {
            int before = target.Hp;
            target.Heal(amount);
            if (target.Hp > before) _heals?.Add(new HealingHit(source.Id, target.Id, target.Hp - before));
        }

        public void GainEnergy(Unit unit, int amount, string trigger)
        {
            if (unit.IsDead) return;
            int before = unit.Ult;
            unit.GainUlt(amount);
            RecordCharge(unit, trigger, before);
        }

        private void RecordCharge(Unit unit, string trigger, int before)
        {
            if (unit.Ult > before) _charges?.Add(new UltCharge(unit.Id, trigger, before, unit.Ult));
        }

        public ActionResult CompleteAction(SkillSpec skill, BattleEventKind kind, string actionType)
        {
            if (_before == null) throw new InvalidOperationException("没有进行中的动作");
            if (kind != BattleEventKind.Passive)
            {
                int before = _actor.Ult;
                CycleOf(_actor.Id).Complete();
                RecordCharge(_actor, "action", before);
                var seen = new HashSet<string>();
                foreach (var hit in _hits)
                    if (seen.Add(hit.TargetUnitId))
                    {
                        var unit = GetUnit(hit.TargetUnitId);
                        int energy = unit.Ult;
                        CycleOf(unit.Id).ReceiveAttack();
                        RecordCharge(unit, "hit", energy);
                    }
                StatesOf(_actor.Id).CompleteAction();
            }
            var changes = new List<UnitChange>();
            foreach (var before in _before)
                changes.Add(new UnitChange(before, new UnitSnapshot(GetUnit(before.UnitId), this)));
            var result = new ActionResult(++_sequence, kind, _actor.Id, skill?.id ?? "stunned",
                skill?.name ?? "眩晕跳过", actionType, changes, _hits, _charges, _heals);
            _results.Add(result);
            _before = null; _hits = null; _charges = null; _heals = null; _actor = null;
            return result;
        }
    }
}
