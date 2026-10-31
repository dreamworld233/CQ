using System;
using System.Collections.Generic;
using CQ.Core.Combat;

namespace CQ.Core.Config
{
    public static class ConfigValidator
    {
        private const int MaxValue = 1000000;
        private static readonly string[] SkillTypes = { "basic", "special", "ult", "passive" };
        private static readonly string[] EffectTypes = { "damage", "heal", "attackUp", "defenseUp", "energy", "stun" };
        private static readonly string[] Targets = { "enemyFront", "enemyBack", "enemyRandom", "enemyAll",
            "allyRandom", "allyFront", "allyLowestHp", "allyLowestEnergy", "allyAll", "self" };

        private static bool In(string[] values, string value) => Array.IndexOf(values, value) >= 0;
        private static bool Positive(int value) => value > 0 && value <= MaxValue;
        private static bool Nonnegative(int value) => value >= 0 && value <= MaxValue;

        public static string ValidateCharacter(CharacterConfig character)
        {
            if (character == null) return "空对象";
            if (string.IsNullOrWhiteSpace(character.id)) return "缺 id";
            if (string.IsNullOrWhiteSpace(character.name)) return "缺 name";
            if (!Positive(character.maxHp)) return "maxHp 须为1~1000000";
            if (!Positive(character.baseAttack)) return "baseAttack 须为1~1000000";
            if (!Nonnegative(character.baseDefense)) return "baseDefense 须为0~1000000";
            if (!Positive(character.baseSpeed)) return "baseSpeed 须为1~1000000";
            if (!Formation.IsValidSlot(character.defaultSlot)) return "defaultSlot 非法";
            if (!Positive(character.maxEnergy)) return "maxEnergy 须为1~1000000";
            if (character.initialEnergy < 0 || character.initialEnergy > character.maxEnergy)
                return "initialEnergy 须为0~maxEnergy";
            if (!Nonnegative(character.actionEnergy) || !Nonnegative(character.hitEnergy))
                return "actionEnergy/hitEnergy 须为0~1000000";
            if (!Positive(character.specialInterval)) return "specialInterval 须为1~1000000";
            if (character.skills == null || character.skills.Count != SkillTypes.Length)
                return "skills 须恰有 basic/special/ult/passive 各一个";
            var ids = new HashSet<string>();
            var types = new HashSet<string>();
            foreach (var skill in character.skills)
            {
                if (skill == null) return "技能为空";
                if (string.IsNullOrWhiteSpace(skill.id) || !ids.Add(skill.id)) return "技能 id 空或重复";
                if (string.IsNullOrWhiteSpace(skill.name)) return "技能缺 name";
                if (!In(SkillTypes, skill.type) || !types.Add(skill.type)) return "技能 type 非法或重复";
                if (skill.type == "passive")
                {
                    if (skill.trigger != "roundStart" && skill.trigger != "permanent")
                        return "被动 trigger 须为 roundStart/permanent";
                }
                else if (!string.IsNullOrEmpty(skill.trigger)) return "主动技能不可配置 trigger";
                if (skill.effects == null || skill.effects.Count == 0) return "技能 effects 空";
                foreach (var effect in skill.effects)
                {
                    var error = ValidateEffect(effect, skill);
                    if (error != null) return skill.id + ": " + error;
                }
            }
            return null;
        }

        private static string ValidateEffect(SkillEffectSpec effect, SkillSpec skill)
        {
            string skillType = skill.type;
            bool permanent = skillType == "passive" && skill.trigger == "permanent";
            if (effect == null) return "效果为空";
            if (!In(EffectTypes, effect.type)) return "效果 type 非法";
            if (!In(Targets, effect.target)) return "效果 target 非法";
            if (effect.count <= 0 || effect.count > Formation.Capacity) return "效果 count 须为1~5";
            if ((effect.target == "self" || effect.target.EndsWith("All", StringComparison.Ordinal)) && effect.count != 1)
                return "self/All 目标 count 须为1";
            bool enemy = effect.target.StartsWith("enemy", StringComparison.Ordinal);
            bool hostile = effect.type == "damage" || effect.type == "stun";
            if (enemy != hostile) return "效果与目标阵营不匹配";
            if (skillType == "basic" && effect.type != "damage") return "basic 只支持伤害效果";
            if (skillType == "passive" && effect.type != "energy" && effect.type != "attackUp" && effect.type != "defenseUp")
                return "回合被动仅支持回能/攻增/防增";
            if (permanent && (effect.target != "self" ||
                (effect.type != "attackUp" && effect.type != "defenseUp")))
                return "常驻被动仅支持自身攻增/防增";
            if (effect.type == "damage")
            {
                if (effect.multiplierPercent <= 0 || effect.multiplierPercent > 10000 || effect.hits <= 0 || effect.hits > 100)
                    return "damage 需1~10000 multiplierPercent及1~100 hits";
                if (effect.percent != 0 || effect.duration != 0 || effect.amount != 0) return "damage 含无关字段";
            }
            else
            {
                if (effect.multiplierPercent != 0 || effect.hits != 0) return "非伤害效果不可配置倍率/段数";
                switch (effect.type)
                {
                    case "heal":
                        if (effect.percent <= 0 || effect.percent > 100 || effect.duration != 0 || effect.amount != 0)
                            return "heal 需1~100 percent且无duration/amount";
                        break;
                    case "attackUp":
                    case "defenseUp":
                        if (effect.percent <= 0 || effect.percent > 10000 ||
                            (permanent ? effect.duration != 0 : !Positive(effect.duration)) || effect.amount != 0)
                            return "增益需1~10000 percent，常驻无duration、临时需正数duration，且无amount";
                        break;
                    case "energy":
                        if (!Positive(effect.amount) || effect.percent != 0 || effect.duration != 0)
                            return "energy 需正数amount且无percent/duration";
                        break;
                    case "stun":
                        if (!Positive(effect.duration) || effect.percent != 0 || effect.amount != 0)
                            return "stun 需正数duration且无percent/amount";
                        break;
                }
            }
            return null;
        }

        public static string ValidateEnemy(EnemyConfig enemy) => ValidateCharacter(enemy);

        public static string ValidateFormation(FormationConfig config) => config == null ? "阵型配置为空" : null;

        public static string ValidateLevelReferences(LevelConfig level, IEnumerable<CharacterConfig> characters,
            IReadOnlyDictionary<string, EnemyConfig> enemies)
        {
            string error = ValidateLevel(level);
            if (error != null) return error;
            if (characters == null || enemies == null) return "角色或敌人目录为空";
            var catalog = new Dictionary<string, CharacterConfig>();
            foreach (var character in characters)
            {
                error = ValidateCharacter(character);
                if (error != null) return "角色配置: " + error;
                if (catalog.ContainsKey(character.id)) return "角色 id 重复: " + character.id;
                catalog.Add(character.id, character);
            }
            var roster = new List<CharacterConfig>();
            if (level.players == null) roster.AddRange(catalog.Values);
            else
                foreach (string id in level.players)
                {
                    if (!catalog.TryGetValue(id, out var character)) return "关卡角色不存在: " + id;
                    roster.Add(character);
                }
            if (roster.Count == 0 || roster.Count > Formation.Capacity) return "关卡角色数量须为1~5";
            var occupied = new HashSet<string>();
            foreach (var character in roster)
                if (!occupied.Add(character.defaultSlot)) return "角色初始槽位重复: " + character.defaultSlot;
            foreach (var pair in enemies)
            {
                error = ValidateEnemy(pair.Value);
                if (error != null) return "敌人配置: " + error;
                if (pair.Key != pair.Value.id) return "敌人目录键与id不符: " + pair.Key;
            }
            var runtimeIds = new HashSet<string>();
            foreach (var character in roster) runtimeIds.Add(character.id);
            var wave = level.waves[0];
            for (int i = 0; i < wave.enemies.Length; i++)
            {
                string id = wave.enemies[i];
                if (!enemies.ContainsKey(id)) return "关卡敌人不存在: " + id;
                string runtimeId = id + "_" + i;
                if (!runtimeIds.Add(runtimeId)) return "战斗单位运行时 id 冲突: " + runtimeId;
            }
            return null;
        }

        public static string ValidateLevel(LevelConfig level)
        {
            if (level == null) return "空对象";
            if (level.id <= 0) return "id 须 > 0";
            if (string.IsNullOrWhiteSpace(level.name)) return "关卡缺 name";
            if (level.waves == null || level.waves.Count != 1) return "首版关卡须恰有一波敌人";
            if (level.players != null)
            {
                if (level.players.Length == 0 || level.players.Length > Formation.Capacity) return "players 数量非法";
                var players = new HashSet<string>();
                foreach (string id in level.players)
                    if (string.IsNullOrWhiteSpace(id) || !players.Add(id)) return "players 含空值或重复角色";
            }
            if (level.tutorialHints != null)
                foreach (string hint in level.tutorialHints)
                    if (string.IsNullOrWhiteSpace(hint)) return "教学提示不可空";
            var wave = level.waves[0];
            if (wave == null || wave.enemies == null || wave.enemies.Length == 0 || wave.enemies.Length > Formation.Capacity)
                return "wave enemies 数量须为1~5";
            if (wave.slots == null || wave.slots.Length != wave.enemies.Length) return "wave slots 必须与 enemies 等长";
            if (wave.maxHp != null)
            {
                if (wave.maxHp.Length != wave.enemies.Length) return "wave maxHp 必须与 enemies 等长";
                foreach (int hp in wave.maxHp)
                    if (!Positive(hp)) return "wave maxHp 须为1~1000000";
            }
            var occupied = new HashSet<string>();
            for (int i = 0; i < wave.enemies.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(wave.enemies[i])) return "wave enemy id 空";
                if (!Formation.IsValidSlot(wave.slots[i])) return "wave slot 非法: " + wave.slots[i];
                if (!occupied.Add(wave.slots[i])) return "wave slot 重复: " + wave.slots[i];
            }
            return null;
        }
    }
}
