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

    /// <summary>
    /// 回合状态机骨架。单回合：RoundStart → Sorting → Acting → DeathCheck → RoundEnd。
    /// 骨架阶段 actResolver 默认空跑（不产生伤害），T7 接入真实技能/卡牌结算；
    /// speedOf 注入本回合速度修正（T6 状态系统落地前提/降速前先传 BaseSpeed）。
    /// </summary>
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

        /// <summary>跑一个小回合，返回本回合行动顺序快照。已完成时返回空。</summary>
        public IReadOnlyList<Unit> RunRound(Action<Unit> actResolver = null)
        {
            if (IsFinished) return new List<Unit>();

            Phase = BattlePhase.RoundStart;
            Round++;

            Phase = BattlePhase.Sorting;
            var alive = AliveUnits.ToList();
            var order = SpeedSorter.Sort(alive, _speedOf);
            var queue = new ActionQueue(order);

            Phase = BattlePhase.Acting;
            while (queue.HasNext)
            {
                var unit = queue.Next();
                if (unit == null || unit.IsDead) continue;
                actResolver?.Invoke(unit);
            }

            Phase = BattlePhase.DeathCheck;
            ResolveWin();

            if (!IsFinished) Phase = BattlePhase.RoundEnd;
            return order;
        }

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
            if (IsFinished) Phase = BattlePhase.Finished;
        }
    }
}