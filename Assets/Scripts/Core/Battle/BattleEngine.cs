using System;
using System.Collections.Generic;
using System.Linq;
using CQ.Core.Combat;
using CQ.Core.Config;

namespace CQ.Core.Battle
{
    public enum BattleFlow
    {
        Setup,
        Resolving,
        Finished
    }

    public sealed class BattleEngine
    {
        public CombatContext Ctx { get; }
        public Rng Rng => Ctx.Rng;
        public IReadOnlyList<Unit> Players { get; }
        public IReadOnlyList<Unit> Enemies { get; }
        public BattleFlow Flow { get; private set; } = BattleFlow.Setup;
        public int Round => _turns.Round;
        public int CurrentRound => Math.Max(1, Round);
        public bool IsFinished => _turns.IsFinished;
        public Team? Winner => _turns.Winner;
        public Guid BattleId { get; } = Guid.NewGuid();
        public ActionResult CurrentAction { get; private set; }
        public bool IsActionOccupied => CurrentAction != null;
        public IReadOnlyList<ActionResult> ActionResults => Ctx.ActionResults;
        public IReadOnlyList<Unit> TurnOrder => _lastOrder;
        public IReadOnlyList<Unit> CurrentOrder => Flow == BattleFlow.Setup
            ? SpeedSorter.Sort(Ctx.Units.Where(u => !u.IsDead).ToList(), u => u.BaseSpeed).AsReadOnly()
            : _lastOrder;

        private readonly Dictionary<string, CharacterConfig> _configs;
        private readonly TurnManager _turns;
        private IReadOnlyList<Unit> _lastOrder = Array.Empty<Unit>();
        private readonly List<Unit> _passives = new List<Unit>();
        private int _passiveCursor;

        private BattleEngine(long seed, List<Unit> players, List<Unit> enemies,
            Dictionary<string, CharacterConfig> configs, FormationConfig formation)
        {
            Players = players.AsReadOnly();
            Enemies = enemies.AsReadOnly();
            _configs = configs;
            Ctx = new CombatContext(new Rng(seed), players.Concat(enemies), configs, formation);
            _turns = new TurnManager(Ctx.Rng, players, enemies, u => u.BaseSpeed);
        }

        public static BattleEngine Create(long seed, IEnumerable<CharacterConfig> characters, WaveConfig wave,
            IReadOnlyDictionary<string, EnemyConfig> enemyCatalog, FormationConfig formationConfig = null) =>
            Create(seed, characters, new LevelConfig
            {
                id = 1, name = "战斗", waves = new List<WaveConfig> { wave }
            }, enemyCatalog, formationConfig);

        public static BattleEngine Create(long seed, IEnumerable<CharacterConfig> characters, LevelConfig level,
            IReadOnlyDictionary<string, EnemyConfig> enemyCatalog, FormationConfig formationConfig = null)
        {
            if (characters == null) throw new ArgumentNullException(nameof(characters));
            if (enemyCatalog == null) throw new ArgumentNullException(nameof(enemyCatalog));
            var allCharacters = characters.ToList();
            foreach (var config in allCharacters)
            {
                string error = ConfigValidator.ValidateCharacter(config);
                if (error != null) throw new ArgumentException(error, nameof(characters));
            }
            string referenceError = ConfigValidator.ValidateLevelReferences(level, allCharacters, enemyCatalog);
            if (referenceError != null) throw new ArgumentException(referenceError, nameof(level));
            var roster = level.players == null ? allCharacters :
                level.players.Select(id => allCharacters.First(c => c.id == id)).ToList();
            if (roster.Count == 0 || roster.Count > Formation.Capacity)
                throw new ArgumentException("我方数量须为1至5", nameof(characters));
            var players = new List<Unit>();
            var enemies = new List<Unit>();
            var configs = new Dictionary<string, CharacterConfig>();
            foreach (var config in roster)
            {
                players.Add(BuildUnit(config, config.id, Team.Player, config.defaultSlot));
                configs.Add(config.id, config);
            }
            var wave = level.waves[0];
            for (int i = 0; i < wave.enemies.Length; i++)
            {
                var config = enemyCatalog[wave.enemies[i]];
                string error = ConfigValidator.ValidateEnemy(config);
                if (error != null) throw new ArgumentException(error, nameof(enemyCatalog));
                string id = config.id + "_" + i;
                var unit = BuildUnit(config, id, Team.Enemy,
                    wave.slots == null ? Formation.SlotIdAt(i) : wave.slots[i]);
                if (wave.maxHp != null) unit.Hp = unit.MaxHp = wave.maxHp[i];
                enemies.Add(unit);
                configs.Add(id, config);
            }
            return new BattleEngine(seed, players, enemies, configs, formationConfig);
        }

        private static Unit BuildUnit(CharacterConfig config, string id, Team team, string slot) => new Unit(id, team)
        {
            Name = config.name, Hp = config.maxHp, MaxHp = config.maxHp,
            BaseAttack = config.baseAttack, BaseDefense = config.baseDefense,
            BaseSpeed = config.baseSpeed, SlotId = slot
        };

        public IReadOnlyList<SkillSpec> SkillsOf(string id) =>
            id != null && _configs.TryGetValue(id, out var config) ? config.skills.AsReadOnly() : null;

        public bool TryMovePlayer(string actorId, string slotId)
        {
            if (Flow != BattleFlow.Setup) return false;
            var unit = Ctx.GetUnit(actorId);
            return unit != null && !unit.IsDead && unit.Team == Team.Player &&
                Ctx.FormationOf(Team.Player).TryMove(unit, slotId);
        }

        public bool StartBattle()
        {
            if (Flow != BattleFlow.Setup) return false;
            CheckWinner();
            if (IsFinished) return false;
            Flow = BattleFlow.Resolving;
            BeginRound();
            return true;
        }

        private void BeginRound()
        {
            _lastOrder = _turns.BeginActions();
            _passives.Clear();
            foreach (Team team in new[] { Team.Player, Team.Enemy })
                foreach (var slot in Ctx.FormationOf(team).Slots)
                    if (slot.Occupant != null && !slot.Occupant.IsDead &&
                        _configs[slot.Occupant.Id].skills.Find(s => s.type == "passive").trigger == "roundStart")
                        _passives.Add(slot.Occupant);
            _passiveCursor = 0;
        }

        public ActionResult Advance()
        {
            if (Flow != BattleFlow.Resolving || IsActionOccupied) return null;
            CheckWinner();
            if (IsFinished) return null;
            while (true)
            {
                while (_passiveCursor < _passives.Count)
                {
                    var unit = _passives[_passiveCursor++];
                    if (unit.IsDead) continue;
                    var passive = _configs[unit.Id].skills.Find(s => s.type == "passive");
                    Ctx.BeginAction(unit);
                    SkillResolver.Resolve(Ctx, unit, passive);
                    return Occupy(Ctx.CompleteAction(passive, BattleEventKind.Passive, "passive"));
                }
                var actor = _turns.NextNormal();
                if (actor != null)
                {
                    Ctx.BeginAction(actor);
                    var statuses = Ctx.StatesOf(actor.Id);
                    statuses.BeginAction();
                    var kind = Ctx.CycleOf(actor.Id).Begin(statuses.Has(StatusType.Stun));
                    string type = kind == PersonalActionKind.Ultimate ? "ult" :
                        kind == PersonalActionKind.Special ? "special" :
                        kind == PersonalActionKind.Basic ? "basic" : "skipped";
                    var skill = kind == PersonalActionKind.Skipped ? null :
                        _configs[actor.Id].skills.Find(s => s.type == type);
                    if (skill != null) SkillResolver.Resolve(Ctx, actor, skill);
                    return Occupy(Ctx.CompleteAction(skill,
                        kind == PersonalActionKind.Skipped ? BattleEventKind.Skipped : BattleEventKind.Skill, type));
                }
                _turns.CompleteRound();
                CheckWinner();
                if (IsFinished) return null;
                BeginRound();
            }
        }

        private ActionResult Occupy(ActionResult result)
        {
            CurrentAction = result;
            CheckWinner();
            return result;
        }

        private void CheckWinner()
        {
            _turns.CheckWinner();
            if (IsFinished) Flow = BattleFlow.Finished;
        }

        public bool Confirm(Guid battleId, long sequence)
        {
            if (battleId != BattleId || CurrentAction == null || CurrentAction.Sequence != sequence) return false;
            CurrentAction = null;
            return true;
        }
    }
}
