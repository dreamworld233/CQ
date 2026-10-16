using System;
using System.Collections.Generic;
using System.Linq;
using CQ.Core.Cards;
using CQ.Core.Combat;
using CQ.Core.Config;
using CQ.Core.Status;

namespace CQ.Core.Battle
{
    /// <summary>战斗流程阶段（UI 呈现用，内部靠 TurnManager.BattlePhase 跑细节）。</summary>
    public enum BattleFlow
    {
        Setup,
        Input,
        Resolving,
        Finished
    }

    /// <summary>
    /// 完整战斗编排器（T7 串联合一）。把所有 resolver 接成一个确定性回合闭环：
    /// StartBattle → (打牌/终结技 输入Phase) → EndRound(速度混排+逐个结算+死亡+胜负+回合末衰减) → 下回合 或 结束。
    /// 卡牌在输入Phase即时生效（打断/虚弱/降速/提速/护盾/攻增），只影响本回合。
    /// 玩家正常行动自动普攻（最高有效嘲讽敌人），敌人按意图循环结算、被打断跳过并对可打断蓄力一并取消。
    /// </summary>
    public sealed class BattleEngine
    {
        public const string CardSourceId = "player";

        public Rng Rng { get; }
        public CombatContext Ctx { get; }
        public IReadOnlyList<Unit> Players { get; }
        public IReadOnlyList<Unit> Enemies { get; }
        public Deck Deck { get; }
        public BattleFlow Flow { get; private set; } = BattleFlow.Setup;

        public int Round => _turns.Round;
        public BattlePhase Phase => _turns.Phase;
        public bool IsFinished => _turns.IsFinished;
        public Team? Winner => _turns.Winner;
        public IReadOnlyList<Unit> TurnOrder => _lastOrder;
        public IReadOnlyList<string> Hand => Deck.Hand;

        /// <summary>最近一次结算产生的动作事件（表现层逐条播报）。每回合开局清空，输入阶段打牌会追加。</summary>
        public IReadOnlyList<BattleEvent> RoundEvents => _events.AsReadOnly();
        public IReadOnlyList<ActionResult> ActionResults => Ctx.ActionResults;

        /// <summary>本回合当前出手顺序（按需速度混排，输入阶段即最新；行动条读此）。</summary>
        public IReadOnlyList<Unit> CurrentOrder
        {
            get
            {
                var alive = Ctx.Units.Where(u => !u.IsDead).ToList();
                return SpeedSorter.Sort(alive, u => StatusResolver.EffectiveSpeedOfUnit(u, Ctx));
            }
        }

        private readonly Dictionary<string, CharacterConfig> _chars;
        private readonly Dictionary<string, EnemyConfig> _enemyCfgs;
        private readonly Dictionary<string, CardConfig> _cards;
        private readonly Dictionary<string, IntentModel> _intents;
        private readonly EnemyActionResolver _enemyResolver = new EnemyActionResolver();
        private readonly TurnManager _turns;
        private readonly List<BattleEvent> _events = new List<BattleEvent>();
        private IReadOnlyList<Unit> _lastOrder = new List<Unit>();
        private bool _started;

        public BattleEngine(
            Rng rng,
            IReadOnlyList<Unit> players,
            IReadOnlyList<Unit> enemies,
            IReadOnlyDictionary<string, CharacterConfig> chars,
            IReadOnlyDictionary<string, EnemyConfig> enemyCfgs,
            IEnumerable<CardConfig> cards,
            FormationConfig formationConfig = null)
        {
            Rng = rng;
            Players = players;
            Enemies = enemies;
            _chars = new Dictionary<string, CharacterConfig>(chars);
            _enemyCfgs = new Dictionary<string, EnemyConfig>(enemyCfgs);

            var cardList = cards.Where(c => c != null).ToList();
            _cards = new Dictionary<string, CardConfig>();
            foreach (var c in cardList) _cards[c.id] = c;

            var all = new List<Unit>();
            all.AddRange(players);
            all.AddRange(enemies);
            Ctx = new CombatContext(rng, all, formationConfig);

            foreach (var player in players)
                if (_chars.TryGetValue(player.Id, out var character))
                {
                    player.MaxUlt = character.maxUlt;
                    player.UltChargeTrigger = character.ultChargeTrigger;
                    player.UltChargeAmount = character.ultChargeAmount;
                }

            _intents = new Dictionary<string, IntentModel>();
            foreach (var e in enemies)
                if (_enemyCfgs.TryGetValue(e.Id, out var cfg))
                    _intents[e.Id] = new IntentModel(cfg);

            Deck = new Deck(rng, cardList.Select(c => (c.id, c.count)));
            _turns = new TurnManager(rng, players, enemies, u => StatusResolver.EffectiveSpeedOfUnit(u, Ctx));
        }

        /// <summary>从配置构建整场战斗（含单位实例化、敌人唯一 id 后缀、意图模型）。</summary>
        public static BattleEngine Create(
            long seed,
            IEnumerable<CharacterConfig> characters,
            IReadOnlyList<string> waveEnemyIds,
            IReadOnlyDictionary<string, EnemyConfig> enemyCatalog,
            IEnumerable<CardConfig> cards,
            IReadOnlyList<string> enemySlots = null,
            FormationConfig formationConfig = null)
        {
            if (waveEnemyIds == null) throw new ArgumentNullException(nameof(waveEnemyIds));
            if (waveEnemyIds.Count > Formation.Capacity) throw new ArgumentException("敌人超过五格容量", nameof(waveEnemyIds));
            if (enemySlots != null && enemySlots.Count != waveEnemyIds.Count)
                throw new ArgumentException("enemySlots 必须与敌人列表等长", nameof(enemySlots));
            var rng = new Rng(seed);

            var players = new List<Unit>();
            var charById = new Dictionary<string, CharacterConfig>();
            foreach (var c in characters)
            {
                if (c == null) continue;
                players.Add(BuildPlayer(c));
                charById[c.id] = c;
            }

            var enemies = new List<Unit>();
            var enemyByUnit = new Dictionary<string, EnemyConfig>();
            for (int i = 0; i < waveEnemyIds.Count; i++)
            {
                if (!enemyCatalog.TryGetValue(waveEnemyIds[i], out var cfg))
                    throw new ArgumentException("敌人配置不存在: " + waveEnemyIds[i], nameof(waveEnemyIds));
                string unitId = cfg.id + "_" + i;
                enemies.Add(BuildEnemy(cfg, unitId, enemySlots == null ? Formation.SlotIdAt(i) : enemySlots[i]));
                enemyByUnit[unitId] = cfg;
            }

            return new BattleEngine(rng, players, enemies, charById, enemyByUnit, cards, formationConfig);
        }

        public static BattleEngine Create(
            long seed,
            IEnumerable<CharacterConfig> characters,
            WaveConfig wave,
            IReadOnlyDictionary<string, EnemyConfig> enemyCatalog,
            IEnumerable<CardConfig> cards,
            FormationConfig formationConfig = null)
        {
            if (wave == null) throw new ArgumentNullException(nameof(wave));
            var error = ConfigValidator.ValidateLevel(new LevelConfig { id = 1, waves = new List<WaveConfig> { wave } });
            if (error != null) throw new ArgumentException(error, nameof(wave));
            return Create(seed, characters, wave.enemies, enemyCatalog, cards, wave.slots, formationConfig);
        }

        private static Unit BuildPlayer(CharacterConfig c) => new Unit(c.id, Team.Player)
        {
            Name = c.name,
            Hp = c.maxHp,
            MaxHp = c.maxHp,
            BaseSpeed = c.baseSpeed,
            SlotId = c.defaultSlot,
            UltChargeTrigger = c.ultChargeTrigger,
            UltChargeAmount = c.ultChargeAmount,
            Ult = 0,
            MaxUlt = c.maxUlt
        };

        private static Unit BuildEnemy(EnemyConfig c, string unitId, string slotId) => new Unit(unitId, Team.Enemy)
        {
            Name = c.name,
            Hp = c.maxHp,
            MaxHp = c.maxHp,
            BaseSpeed = c.baseSpeed,
            SlotId = slotId,
            Ult = 0,
            MaxUlt = 0
        };

        /// <summary>开局：意图亮（首轮游标已是 0）+ 抽 1 张，开放输入。</summary>
        public void StartBattle()
        {
            if (_started) return;
            _started = true;
            _lastOrder = new List<Unit>();
            Deck.Draw(1);
            Flow = BattleFlow.Input;
        }

        private bool InputOpen => _started && !IsFinished && Flow == BattleFlow.Input;

        /// <summary>打出第 handIndex 张手牌，作用于 targetUnitId。效果即时生效。</summary>
        public bool PlayCard(int handIndex, string targetUnitId)
        {
            if (!InputOpen || Ctx.GetUnit(targetUnitId) == null) return false;
            string cardId = Deck.Play(handIndex);
            if (cardId == null || !_cards.TryGetValue(cardId, out var cfg)) return false;
            Ctx.BeginAction(BattleEventKind.Card, null, cardId, false);
            CardEffectResolver.Resolve(cfg.effect, Ctx, targetUnitId, CardSourceId);
            Record(Ctx.CompleteAction());
            CheckWinner();
            return true;
        }

        public bool UseUltimate(string charId, string targetUnitId = null)
        {
            if (!InputOpen) return false;
            var unit = Ctx.GetUnit(charId);
            var skill = UltimateOf(charId);
            if (unit == null || unit.Team != Team.Player || skill == null) return false;
            var result = SkillResolver.Resolve(skill, Ctx, unit, Ctx.GetUnit(targetUnitId), AlivePlayers(), AliveEnemies());
            if (result == null) return false;
            Record(result);
            CheckWinner();
            return true;
        }

        public SkillSpec UltimateOf(string charId) =>
            _chars.TryGetValue(charId, out var cfg) ? cfg.skills?.Find(s => s.type == "ult") : null;

        public bool CanUseUltimate(string charId) =>
            InputOpen && SkillResolver.CanUse(UltimateOf(charId), Ctx.GetUnit(charId));

        private void CheckWinner()
        {
            _turns.CheckWinner();
            if (_turns.IsFinished) Flow = BattleFlow.Finished;
        }

        /// <summary>结束输入，跑本回合闭环。返回本回合出手顺序（含排序时存活单位）。</summary>
        public IReadOnlyList<Unit> EndRound()
        {
            if (!InputOpen) return _lastOrder;
            Flow = BattleFlow.Resolving;

            _events.Clear();
            _lastOrder = _turns.RunRound(ActUnit);
            StatusResolver.RoundEndTick(Ctx);

            if (_turns.IsFinished)
            {
                Flow = BattleFlow.Finished;
            }
            else
            {
                Deck.Draw(1);
                Flow = BattleFlow.Input;
            }
            return _lastOrder;
        }

        private void ActUnit(Unit unit)
        {
            if (unit == null || unit.IsDead) return;

            int count = Ctx.ActionResults.Count;
            if (unit.Team == Team.Player) ActPlayer(unit);
            else ActEnemy(unit);
            for (int i = count; i < Ctx.ActionResults.Count; i++) Record(Ctx.ActionResults[i]);
        }

        private void Record(ActionResult result)
        {
            bool any = false;
            foreach (var change in result.UnitChanges)
            {
                if (change.HpDelta == 0) continue;
                any = true;
                _events.Add(new BattleEvent { Kind = result.Kind, ActorId = result.ActorId, ActionId = result.ActionId,
                    TargetId = change.UnitId, HpDelta = change.HpDelta, TargetDead = change.After.IsDead });
            }
            if (!any) _events.Add(new BattleEvent { Kind = result.Kind, ActorId = result.ActorId,
                ActionId = result.ActionId, TargetId = result.ActorId });
        }

        private void ActPlayer(Unit unit)
        {
            if (!_chars.TryGetValue(unit.Id, out var cfg)) return;
            var basic = cfg.skills?.Find(s => s.type == "basic");
            SkillResolver.Resolve(basic, Ctx, unit, null, AlivePlayers(), AliveEnemies());
        }

        private void ActEnemy(Unit unit)
        {
            if (!_intents.TryGetValue(unit.Id, out var model)) return;

            if (StatusResolver.ShouldSkipTurn(unit, Ctx))
            {
                // 打断：本回合不出手；只走蓄力释放路径（interruptible 蓄力被取消）。
                _enemyResolver.ResolveSkipped(Ctx, unit.Id);
                return;
            }

            _enemyResolver.Resolve(Ctx, unit.Id, model);
        }

        private List<Unit> AlivePlayers() => Players.Where(p => !p.IsDead).ToList();
        private List<Unit> AliveEnemies() => Enemies.Where(e => !e.IsDead).ToList();

        public IReadOnlyList<SkillSpec> SkillsOf(string charId) =>
            _chars.TryGetValue(charId, out var c) ? c.skills : null;

        public IntentModel GetIntent(string enemyId) =>
            _intents.TryGetValue(enemyId, out var m) ? m : null;

        public IntentConfig CurrentIntentOf(string enemyId) => GetIntent(enemyId)?.CurrentIntent;

        public CardConfig GetCard(string cardId) =>
            _cards.TryGetValue(cardId, out var c) ? c : null;
    }
}