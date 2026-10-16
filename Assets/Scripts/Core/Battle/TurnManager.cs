using System;
using System.Collections.Generic;
using System.Linq;
using CQ.Core.Combat;

namespace CQ.Core.Battle
{
    public enum BattlePhase
    {
        Idle,
        RoundStart,
        Sorting,
        Acting,
        DeathCheck,
        RoundEnd,
        Finished
    }

    public sealed class TurnManager
    {
        private readonly List<Unit> _players;
        private readonly List<Unit> _enemies;
        private readonly Rng _rng;
        private readonly Func<Unit, int> _speedOf;

        public int Round { get; private set; }
        public BattlePhase Phase { get; private set; } = BattlePhase.Idle;
        public bool IsFinished { get; private set; }
        public Team? Winner { get; private set; }

        public TurnManager(Rng rng, IEnumerable<Unit> players, IEnumerable<Unit> enemies, Func<Unit, int> speedOf = null)
        {
            _rng = rng;
            _players = new List<Unit>(players);
            _enemies = new List<Unit>(enemies);
            _speedOf = speedOf ?? (u => u.BaseSpeed);
        }

        public IReadOnlyList<Unit> Players => _players;
        public IReadOnlyList<Unit> Enemies => _enemies;

        public IEnumerable<Unit> AliveUnits => _players.Concat(_enemies).Where(u => !u.IsDead);
        public bool AllPlayersDead => !_players.Any(u => !u.IsDead);
        public bool AllEnemiesDead => !_enemies.Any(u => !u.IsDead);

        private ActionQueue _queue;

        public IReadOnlyList<Unit> BeginActions()
        {
            if (IsFinished || Phase == BattlePhase.Acting) return null;
            Phase = BattlePhase.RoundStart;
            Round++;
            Phase = BattlePhase.Sorting;
            var order = SpeedSorter.Sort(AliveUnits.ToList(), _speedOf);
            _queue = new ActionQueue(order);
            Phase = BattlePhase.Acting;
            return order;
        }

        public Unit NextNormal()
        {
            if (IsFinished || Phase != BattlePhase.Acting) return null;
            while (_queue.HasNext)
            {
                var unit = _queue.Next();
                if (unit != null && !unit.IsDead) return unit;
            }
            return null;
        }

        public void CompleteRound()
        {
            if (!IsFinished && Phase == BattlePhase.Acting) Phase = BattlePhase.RoundEnd;
        }

        public void CheckWinner() => ResolveWin();

        private void ResolveWin()
        {
            if (AllEnemiesDead)
            {
                IsFinished = true;
                Winner = Team.Player;
            }
            else if (AllPlayersDead)
            {
                IsFinished = true;
                Winner = Team.Enemy;
            }
            if (IsFinished)
            {
                _queue = null;
                Phase = BattlePhase.Finished;
            }
        }
    }
}