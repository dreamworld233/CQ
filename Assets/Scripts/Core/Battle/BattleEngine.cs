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
                if (Flow == BattleFlow.Resolving || IsFinished) return _lastOrder;
                var alive = Ctx.Units.Where(u => !u.IsDead).ToList();
                return SpeedSorter.Sort(alive, u => StatusResolver.EffectiveSpeedOfUnit(u, Ctx));
            }
        }

        public Guid BattleId { get; } = Guid.NewGuid();
        public ActionResult CurrentAction { get; private set; }
        public bool IsActionOccupied => CurrentAction != null;
        public bool CanPlayCards => InputOpen;
        public int PendingUltimateCount => _pending.Count;

        private sealed class UltimateRequest
        {
            public Unit Caster;
            public SkillSpec Skill;
            public int Reserved;
        }

        private readonly List<UltimateRequest> _pending = new List<UltimateRequest>();
        private readonly HashSet<string> _requested = new HashSet<string>();
        private UltimateRequest _executing;
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

        public bool TryMovePlayer(string actorId, string slotId)
        {
            if (Flow != BattleFlow.Setup) return false;
            var unit = Ctx.GetUnit(actorId);
            return unit != null && !unit.IsDead && unit.Team == Team.Player &&
                Ctx.FormationOf(Team.Player).TryMove(unit, slotId);
        }

        /// <summary>开局：意图亮（首轮游标已是 0）+ 抽 2 张，开放输入。</summary>
        public void StartBattle()
        {
            if (_started) return;
            _started = true;
            _lastOrder = new List<Unit>();
            Deck.Draw(2);
            Flow = BattleFlow.Input;
        }

        private bool InputOpen => _started && !IsFinished && Flow == BattleFlow.Input &&
            !IsActionOccupied && _pending.Count == 0;

        /// <summary>打出第 handIndex 张手牌，作用于 targetUnitId。效果即时生效。</summary>
        public bool PlayCard(int handIndex, string targetUnitId)
        {
            if (!CanPlayCard(handIndex, targetUnitId)) return false;
            string cardId = Deck.Play(handIndex);
            var cfg = _cards[cardId];
            Ctx.BeginAction(BattleEventKind.Card, null, cardId, false);
            CardEffectResolver.Resolve(cfg.effect, Ctx, targetUnitId, CardSourceId);
            Record(Ctx.CompleteAction(targetUnitId: targetUnitId));
            CheckWinner();
            return true;
        }

        private CardConfig HandCard(int index) => index >= 0 && index < Hand.Count &&
            _cards.TryGetValue(Hand[index], out var card) ? card : null;

        public bool CanPlayCard(int index, string targetUnitId) => InputOpen &&
            CardEffectResolver.IsValidUnitTarget(HandCard(index)?.effect, Ctx.GetUnit(targetUnitId));

        public bool CanPlaySlotCard(int index, Team team, string slotId) => InputOpen &&
            (team == Team.Player || team == Team.Enemy) &&
            CardEffectResolver.IsSlotEffect(HandCard(index)?.effect) && Formation.IsValidSlot(slotId);

        public bool PlaySlotCard(int index, Team team, string slotId)
        {
            if (!CanPlaySlotCard(index, team, slotId)) return false;
            string cardId = Deck.Play(index);
            Ctx.BeginAction(BattleEventKind.Card, null, cardId, false);
            CardEffectResolver.ResolveSlot(_cards[cardId].effect, Ctx, team, slotId);
            Record(Ctx.CompleteAction(team, slotId));
            return true;
        }

        public bool RequestUltimate(string charId)
        {
            if (!CanRequestUltimate(charId)) return false;
            var unit = Ctx.GetUnit(charId);
            var skill = UltimateOf(charId);
            if (!SkillResolver.IsValidTarget(skill, Ctx, unit))
                return false;
            int reserved = unit.MaxUlt;
            unit.GainUlt(-reserved);
            _requested.Add(charId);
            _pending.Add(new UltimateRequest { Caster = unit, Skill = skill, Reserved = reserved });
            return true;
        }

        public SkillSpec UltimateOf(string charId) =>
            _chars.TryGetValue(charId, out var cfg) ? cfg.skills?.Find(s => s.type == "ult") : null;

        public bool CanRequestUltimate(string charId) =>
            _started && !IsFinished && (Flow == BattleFlow.Input || Flow == BattleFlow.Resolving) &&
            charId != null && !_requested.Contains(charId) && Ctx.GetUnit(charId)?.Team == Team.Player &&
            SkillResolver.CanUse(UltimateOf(charId), Ctx.GetUnit(charId));

        public bool HasUltimateRequest(string charId) => charId != null && _requested.Contains(charId);
        private void Refund(UltimateRequest request)
        {
            request.Caster.GainUlt(request.Reserved);
            request.Reserved = 0;
            _requested.Remove(request.Caster.Id);
        }

        private void CancelInvalidRequests()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var request = _pending[i];
                if (!IsFinished && SkillResolver.IsValidTarget(request.Skill, Ctx, request.Caster)) continue;
                Refund(request);
                _pending.RemoveAt(i);
            }
        }

        private void CheckWinner()
        {
            _turns.CheckWinner();
            if (_turns.IsFinished) Flow = BattleFlow.Finished;
            CancelInvalidRequests();
        }

        public bool BeginActions()
        {
            if (!InputOpen) return false;
            CheckWinner();
            if (IsFinished) return false;
            _lastOrder = _turns.BeginActions();
            _events.Clear();
            Flow = BattleFlow.Resolving;
            return true;
        }

        public ActionResult Advance()
        {
            if (!_started || IsActionOccupied || IsFinished) return null;
            CheckWinner();
            if (IsFinished) return null;
            while (_pending.Count > 0)
            {
                var request = _pending[0];
                _pending.RemoveAt(0);
                var result = SkillResolver.ResolveReserved(request.Skill, Ctx, request.Caster);
                if (result == null) { Refund(request); continue; }
                request.Reserved = 0;
                _executing = request;
                CurrentAction = result;
                Record(result);
                CheckWinner();
                return result;
            }
            if (Flow != BattleFlow.Resolving) return null;
            Unit unit;
            while ((unit = _turns.NextNormal()) != null)
            {
                int count = Ctx.ActionResults.Count;
                ActUnit(unit);
                if (Ctx.ActionResults.Count == count) continue;
                CurrentAction = Ctx.ActionResults[Ctx.ActionResults.Count - 1];
                CheckWinner();
                return CurrentAction;
            }
            CheckWinner();
            if (!IsFinished)
            {
                _turns.CompleteRound();
                StatusResolver.RoundEndTick(Ctx);
                Deck.Draw(1);
                Flow = BattleFlow.Input;
            }
            return null;
        }

        public bool Confirm(Guid battleId, long sequence)
        {
            if (battleId != BattleId || CurrentAction == null || CurrentAction.Sequence != sequence) return false;
            CurrentAction = null;
            if (_executing != null)
            {
                _requested.Remove(_executing.Caster.Id);
                _executing = null;
            }
            return true;
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
                ActionId = result.ActionId, TargetId = result.TargetUnitId ?? result.ActorId,
                TargetTeam = result.TargetTeam, TargetSlotId = result.TargetSlotId });
        }

        private void ActPlayer(Unit unit)
        {
            if (!_chars.TryGetValue(unit.Id, out var cfg)) return;
            var basic = cfg.skills?.Find(s => s.type == "basic");
            SkillResolver.Resolve(basic, Ctx, unit);
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

        public IReadOnlyList<SkillSpec> SkillsOf(string charId) =>
            _chars.TryGetValue(charId, out var c) ? c.skills : null;

        public IntentModel GetIntent(string enemyId) =>
            _intents.TryGetValue(enemyId, out var m) ? m : null;

        /// <summary>只读意图快照：本轮 pending 释放 + 当前意图；数字为当前源状态下盾前伤害，不锁目标。</summary>
        public EnemyIntentPreview GetIntentPreview(string enemyId) =>
            string.IsNullOrEmpty(enemyId) ? null : _enemyResolver.Preview(Ctx, enemyId, CurrentIntentOf(enemyId));

        public IntentConfig CurrentIntentOf(string enemyId) => GetIntent(enemyId)?.CurrentIntent;

        public CardConfig GetCard(string cardId) =>
            _cards.TryGetValue(cardId, out var c) ? c : null;
    }
}