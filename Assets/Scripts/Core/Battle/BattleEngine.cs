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

    /// <summary>单个玩家本回合的出招计划。</summary>
    public sealed class SkillPlan
    {
        public SkillSpec Skill;
        public string TargetId;
    }

    /// <summary>
    /// 完整战斗编排器（T7 串联合一）。把所有 resolver 接成一个确定性回合闭环：
    /// StartBattle → (打牌/选招 输入Phase) → EndRound(速度混排+逐个结算+死亡+胜负+回合末衰减) → 下回合 或 结束。
    /// 卡牌在输入Phase即时生效（打断/虚弱/降速/提速/护盾/攻增），只影响本回合。
    /// 玩家未选招时默认普攻（首个存活敌人），敌人按意图循环结算、被打断跳过并对可打断蓄力一并取消。
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
        private readonly Dictionary<string, SkillPlan> _plan = new Dictionary<string, SkillPlan>();
        private readonly EnemyActionResolver _enemyResolver = new EnemyActionResolver();
        private readonly TurnManager _turns;
        private readonly Dictionary<string, string> _lockedTargets = new Dictionary<string, string>();
        private IReadOnlyList<Unit> _lastOrder = new List<Unit>();
        private bool _started;

        public BattleEngine(
            Rng rng,
            IReadOnlyList<Unit> players,
            IReadOnlyList<Unit> enemies,
            IReadOnlyDictionary<string, CharacterConfig> chars,
            IReadOnlyDictionary<string, EnemyConfig> enemyCfgs,
            IEnumerable<CardConfig> cards)
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
            Ctx = new CombatContext(rng, all);

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
            IEnumerable<CardConfig> cards)
        {
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
                if (!enemyCatalog.TryGetValue(waveEnemyIds[i], out var cfg)) continue;
                string unitId = cfg.id + "_" + i;
                enemies.Add(BuildEnemy(cfg, unitId));
                enemyByUnit[unitId] = cfg;
            }

            return new BattleEngine(rng, players, enemies, charById, enemyByUnit, cards);
        }

        private static Unit BuildPlayer(CharacterConfig c) => new Unit(c.id, Team.Player)
        {
            Name = c.name,
            Hp = c.maxHp,
            MaxHp = c.maxHp,
            BaseSpeed = c.baseSpeed,
            Row = c.row,
            AggroWeight = c.aggroWeight,
            Energy = 0,
            MaxEnergy = c.maxEnergy,
            Ult = 0,
            MaxUlt = c.maxUlt
        };

        private static Unit BuildEnemy(EnemyConfig c, string unitId) => new Unit(unitId, Team.Enemy)
        {
            Name = c.name,
            Hp = c.maxHp,
            MaxHp = c.maxHp,
            BaseSpeed = c.baseSpeed,
            Row = c.row,
            AggroWeight = c.aggroWeight,
            Energy = 0,
            MaxEnergy = 0,
            Ult = 0,
            MaxUlt = 0
        };

        /// <summary>开局：意图亮（首轮游标已是 0）+ 抽 1 张，开放输入。</summary>
        public void StartBattle()
        {
            if (_started) return;
            _started = true;
            BeginRound();
            Flow = BattleFlow.Input;
        }

        private void BeginRound()
        {
            _plan.Clear();
            _lastOrder = new List<Unit>();
            LockIntentTargets();
            Deck.Draw(1);
        }

        /// <summary>亮意图时预锁单体攻击/连击/蓄力/减益的目标（同 seed 确定）。</summary>
        private void LockIntentTargets()
        {
            _lockedTargets.Clear();
            var players = AlivePlayers();
            foreach (var e in Enemies)
            {
                if (e.IsDead) continue;
                var intent = CurrentIntentOf(e.Id);
                if (intent == null || !NeedsTarget(intent)) continue;
                var t = TargetSelector.PickTarget(Ctx, players);
                if (t != null) _lockedTargets[e.Id] = t.Id;
            }
        }

        private static bool NeedsTarget(IntentConfig intent)
        {
            if (intent.type == "multi" || intent.type == "charge" || intent.type == "debuff") return true;
            return intent.type == "attack" && intent.target == "single";
        }

        private bool InputOpen => _started && !IsFinished && Flow == BattleFlow.Input;

        /// <summary>打出第 handIndex 张手牌，作用于 targetUnitId。效果即时生效。</summary>
        public bool PlayCard(int handIndex, string targetUnitId)
        {
            if (!InputOpen || Ctx.GetUnit(targetUnitId) == null) return false;
            string cardId = Deck.Play(handIndex);
            if (cardId == null || !_cards.TryGetValue(cardId, out var cfg)) return false;
            CardEffectResolver.Resolve(cfg.effect, Ctx, targetUnitId, CardSourceId);
            return true;
        }

        /// <summary>登记某玩家本回合出招。能量/大招不足或目标非法则不登记，返回 false。</summary>
        public bool ChooseSkill(string charId, string skillId, string targetUnitId)
        {
            if (!InputOpen) return false;
            var unit = Ctx.GetUnit(charId);
            if (unit == null || unit.IsDead || unit.Team != Team.Player) return false;
            if (!_chars.TryGetValue(charId, out var cfg)) return false;
            var skill = FindSkill(cfg, skillId);
            if (skill == null || !SkillResolver.CanUse(skill, unit)) return false;
            if (!TargetAllowed(skill, targetUnitId)) return false;
            _plan[charId] = new SkillPlan { Skill = skill, TargetId = targetUnitId };
            return true;
        }

        /// <summary>目标合法性：单目标治疗须友方，单目标伤害须敌方；全目标/自愈忽略选择。</summary>
        private bool TargetAllowed(SkillSpec skill, string targetUnitId)
        {
            bool needsAlly = skill.heal != null && skill.heal.target == "single";
            bool needsEnemy = skill.damage != null && skill.damage.target == "single";
            if (!needsAlly && !needsEnemy) return true;

            var target = Ctx.GetUnit(targetUnitId);
            if (target == null || target.IsDead) return false;
            return needsAlly ? target.Team == Team.Player : target.Team == Team.Enemy;
        }

        /// <summary>结束输入，跑本回合闭环。返回本回合出手顺序（含排序时存活单位）。</summary>
        public IReadOnlyList<Unit> EndRound()
        {
            if (!InputOpen) return _lastOrder;
            Flow = BattleFlow.Resolving;

            _lastOrder = _turns.RunRound(ActUnit);

            if (_turns.IsFinished)
            {
                Flow = BattleFlow.Finished;
            }
            else
            {
                StatusResolver.RoundEndTick(Ctx);
                BeginRound();
                Flow = BattleFlow.Input;
            }
            return _lastOrder;
        }

        private void ActUnit(Unit unit)
        {
            if (unit == null || unit.IsDead) return;
            if (unit.Team == Team.Player) ActPlayer(unit);
            else ActEnemy(unit);
        }

        private void ActPlayer(Unit unit)
        {
            if (!_plan.TryGetValue(unit.Id, out var plan) || plan == null || plan.Skill == null)
                plan = DefaultBasicPlan(unit);
            if (plan == null || plan.Skill == null) return;

            SkillResolver.Resolve(plan.Skill, Ctx, unit, Ctx.GetUnit(plan.TargetId), AlivePlayers(), AliveEnemies());
        }

        private SkillPlan DefaultBasicPlan(Unit unit)
        {
            if (!_chars.TryGetValue(unit.Id, out var cfg)) return null;
            foreach (var s in cfg.skills)
                if (s.type == "basic")
                    return new SkillPlan { Skill = s, TargetId = FirstAliveEnemy()?.Id };
            return null;
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

            _lockedTargets.TryGetValue(unit.Id, out var lockedTargetId);
            _enemyResolver.Resolve(Ctx, unit.Id, model, lockedTargetId);
        }

        private Unit FirstAliveEnemy() => Enemies.FirstOrDefault(e => !e.IsDead);
        private List<Unit> AlivePlayers() => Players.Where(p => !p.IsDead).ToList();
        private List<Unit> AliveEnemies() => Enemies.Where(e => !e.IsDead).ToList();

        private static SkillSpec FindSkill(CharacterConfig cfg, string skillId)
        {
            if (cfg == null || cfg.skills == null) return null;
            foreach (var s in cfg.skills)
                if (s.id == skillId) return s;
            return null;
        }

        public bool CanUseSkill(string charId, string skillId)
        {
            var unit = Ctx.GetUnit(charId);
            if (unit == null) return false;
            if (!_chars.TryGetValue(charId, out var cfg)) return false;
            return SkillResolver.CanUse(FindSkill(cfg, skillId), unit);
        }

        public IReadOnlyList<SkillSpec> SkillsOf(string charId) =>
            _chars.TryGetValue(charId, out var c) ? c.skills : null;

        public IntentModel GetIntent(string enemyId) =>
            _intents.TryGetValue(enemyId, out var m) ? m : null;

        public IntentConfig CurrentIntentOf(string enemyId) => GetIntent(enemyId)?.CurrentIntent;

        public string GetIntentTarget(string enemyId) =>
            _lockedTargets.TryGetValue(enemyId, out var t) ? t : null;

        public CardConfig GetCard(string cardId) =>
            _cards.TryGetValue(cardId, out var c) ? c : null;
    }
}