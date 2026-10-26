using System;
using System.Collections.Generic;
using CQ.Core.Combat;

namespace CQ.Core.Config
{
    /// <summary>
    /// 纯 C# 配置校验，零引擎依赖。返回 null 表示通过，否则返回缺字段描述。
    /// </summary>
    public static class ConfigValidator
    {
        private static readonly string[] SkillTypes = { "basic", "ult" };
        private static readonly string[] IntentTypes = { "attack", "charge", "attackUp", "multi", "debuff" };
        private static readonly string[] CardTypes = { "damage", "heal", "remove", "vulnerable", "interrupt", "weaken", "slow", "haste", "shield", "attackUp", "tauntUp", "tauntDown" };
        private static readonly string[] StatusIds = { "vulnerable", "weaken", "shield", "attackUp", "slow", "interrupt" };
        private static readonly string[] Turns = { "round", "once" };

        private static bool In(string[] set, string value) => Array.IndexOf(set, value) >= 0;

        public static string ValidateCharacter(CharacterConfig c)
        {
            if (c == null) return "空对象";
            if (string.IsNullOrEmpty(c.id)) return "缺 id";
            if (string.IsNullOrEmpty(c.name)) return "缺 name";
            if (c.maxHp <= 0) return "maxHp 须 > 0";
            if (c.baseSpeed <= 0) return "baseSpeed 须 > 0";
            if (c.maxUlt <= 0) return "maxUlt 须 > 0";
            if (c.ultChargeTrigger != "attack" && c.ultChargeTrigger != "hit") return "ultChargeTrigger 须为 attack|hit";
            if (c.ultChargeAmount <= 0) return "ultChargeAmount 须 > 0";
            if (!Formation.IsValidSlot(c.defaultSlot)) return "defaultSlot 非法";
            if (c.skills == null || c.skills.Count != 2) return "skills 须恰有 basic/ult 各一个";
            int basics = 0, ults = 0;
            var ids = new HashSet<string>();
            foreach (var s in c.skills)
            {
                if (s == null) return "技能为空";
                if (string.IsNullOrEmpty(s.id) || !ids.Add(s.id)) return "技能 id 空或重复";
                if (string.IsNullOrEmpty(s.name)) return "技能缺 name";
                if (!In(SkillTypes, s.type)) return "技能 type 非法: " + s.type;
                if (s.type == "basic") basics++; else ults++;
                if (s.type == "ult")
                {
                    string error = ValidateInjection(s);
                    if (error != null) return error;
                    continue;
                }
                if (s.heal != null || (s.injectCards != null && s.injectCards.Count > 0))
                    return "basic 不可治疗或注牌";
                if (s.damage != null)
                {
                    if (s.damage.amount <= 0) return "damage amount 须 > 0";
                    if (s.damage.kind != "physical" && s.damage.kind != "magic") return "damage kind 非法";
                    if (s.damage.target != "single" && s.damage.target != "all") return "damage target 非法";
                }
                if (s.type == "basic" && s.damage == null) return "basic 须为攻击";
            }
            if (basics != 1 || ults != 1) return "须恰有 basic/ult 各一个";
            return null;
        }

        public static string ValidateInjection(SkillSpec skill)
        {
            if (skill == null || skill.type != "ult") return "注牌须为终结技";
            if (skill.damage != null || skill.heal != null) return "终结技不可直接伤害或治疗";
            if (skill.injectCards == null || skill.injectCards.Count == 0) return "终结技须配置 injectCards";
            foreach (var entry in skill.injectCards)
                if (entry == null || string.IsNullOrWhiteSpace(entry.cardId) || entry.count <= 0)
                    return "注牌须有 cardId 且 count > 0";
            return null;
        }

        public static string ValidateCardReferences(IEnumerable<CharacterConfig> characters,
            IReadOnlyDictionary<string, CardConfig> cards)
        {
            foreach (var character in characters)
                foreach (var skill in character.skills ?? new List<SkillSpec>())
                {
                    if (skill?.type != "ult") continue;
                    string error = ValidateInjection(skill);
                    if (error != null) return character.id + "/" + skill.id + ": " + error;
                    foreach (var entry in skill.injectCards)
                        if (!cards.TryGetValue(entry.cardId, out var card) || ValidateCard(card) != null)
                            return character.id + "/" + skill.id + " 引用了无效卡牌: " + entry.cardId;
                }
            return null;
        }

        public static string ValidateEnemy(EnemyConfig e)
        {
            if (e == null) return "空对象";
            if (string.IsNullOrEmpty(e.id)) return "缺 id";
            if (string.IsNullOrEmpty(e.name)) return "缺 name";
            if (e.maxHp <= 0) return "maxHp 须 > 0";
            if (e.baseSpeed <= 0) return "baseSpeed 须 > 0";
            if (e.intents == null || e.intents.Count == 0) return "intents 空";
            if (e.intentSequence == null || e.intentSequence.Length < 2 || e.intentSequence.Length > 4)
                return "intentSequence 长度须 2~4";
            var ids = new HashSet<string>();
            foreach (var it in e.intents)
            {
                if (it == null) return "意图为空";
                if (string.IsNullOrWhiteSpace(it.id) || !ids.Add(it.id)) return "意图 id 空或重复";
                if (!In(IntentTypes, it.type)) return "意图 type 非法: " + it.type;
                bool attack = it.type == "attack" || it.type == "multi";
                bool buff = it.buffPercent != 0 || it.buffDuration != 0;
                bool debuff = !string.IsNullOrEmpty(it.debuffType) || it.debuffPercent != 0 || it.debuffDuration != 0;
                if (attack)
                {
                    if (it.damage <= 0) return "攻击 damage 须 > 0";
                    if (it.target != "single" && it.target != "all") return "攻击 target 须为 single|all";
                    if (it.type == "multi" && (it.target != "single" || it.hits <= 0))
                        return "multi 须 single 且 hits > 0";
                    if (it.type == "attack" && it.hits != 0) return "attack 不可配置 hits";
                }
                else
                {
                    if (it.damage != 0 || it.hits != 0) return "非攻击意图不可配置 damage/hits";
                    if (it.type == "debuff")
                    {
                        if (it.target != "single") return "debuff target 须为 single";
                    }
                    else if (!string.IsNullOrEmpty(it.target) && it.target != "single")
                        return "charge/attackUp target 仅可省略或为 single";
                }
                if (it.type == "charge")
                {
                    if (it.nextDamage <= 0) return "charge nextDamage 须 > 0";
                    if (!it.interruptible) return "charge 必须 interruptible=true";
                }
                else if (it.nextDamage != 0 || it.interruptible) return "非 charge 意图不可配置蓄力字段";
                if (buff || it.type == "attackUp")
                {
                    if (!attack && it.type != "attackUp") return "此意图不可配置 buff";
                    if (it.buffPercent <= 0 || it.buffDuration <= 0) return "buff 需正数 buffPercent/buffDuration";
                }
                if (debuff || it.type == "debuff")
                {
                    if (!attack && it.type != "debuff") return "此意图不可配置 debuff";
                    if (it.debuffType != "weaken" && it.debuffType != "slow") return "debuffType 须为 weaken|slow";
                    if (it.debuffPercent <= 0 || it.debuffPercent > 100 || it.debuffDuration <= 0)
                        return "debuff 需 1~100 debuffPercent 及正数 debuffDuration";
                }
            }
            foreach (var id in e.intentSequence)
                if (string.IsNullOrWhiteSpace(id) || FindIntent(e, id) == null)
                    return "intentSequence 引用未定义意图: " + id;
            return null;
        }

        private static IntentConfig FindIntent(EnemyConfig e, string id)
        {
            foreach (var it in e.intents) if (it.id == id) return it;
            return null;
        }

        public static string ValidateCard(CardConfig c)
        {
            if (c == null) return "空对象";
            if (string.IsNullOrEmpty(c.id)) return "缺 id";
            if (string.IsNullOrEmpty(c.name)) return "缺 name";
            if (c.effect == null) return "缺 effect";
            if (!In(CardTypes, c.effect.type)) return "卡效果 type 非法: " + c.effect.type;
            if (c.count < 0) return "count 不可负";
            if (!string.IsNullOrEmpty(c.effect.target) && c.effect.target != "single" && c.effect.target != "all")
                return "卡效果 target 须为 single|all";
            if (c.effect.target == "all" && c.effect.type != "damage" && c.effect.type != "weaken" && c.effect.type != "vulnerable")
                return "此卡效果不支持群体目标";
            if (c.effect.type == "damage")
            {
                if (c.effect.target != "single" && c.effect.target != "all") return "伤害卡须指定 target";
                if (c.effect.amount <= 0) return "伤害卡 amount 须 > 0";
            }
            else if (c.effect.type == "heal")
            {
                if (c.effect.target != "single" || c.effect.amount <= 0)
                    return "治疗卡须 single 且 amount > 0";
            }
            else if (c.effect.type == "remove")
            {
                if (!string.IsNullOrEmpty(c.effect.target) || c.effect.amount != 0 ||
                    c.effect.percent != 0 || c.effect.duration != 0)
                    return "移除卡不可配置目标或额外效果";
            }
            else if (c.effect.type == "tauntUp" || c.effect.type == "tauntDown")
            {
                if (c.effect.amount <= 0 || c.effect.duration <= 0) return "格子卡 amount/duration 须 > 0";
            }
            else if (c.effect.type == "shield")
            {
                if (c.effect.amount <= 0) return "shield amount 须 > 0";
            }
            else if (c.effect.type != "interrupt" && c.effect.percent <= 0)
                return "卡效果 percent 须 > 0";
            if (c.effect.type == "weaken" && c.effect.percent > 100) return "weaken percent 不可超过100";
            return null;
        }

        public static string ValidateStatus(StatusConfig s)
        {
            if (s == null) return "空对象";
            if (!In(StatusIds, s.id)) return "状态 id 非法: " + s.id;
            if (s.duration < 0) return "duration 不可负";
            if (!In(Turns, s.turn)) return "turn 须为 round|once";
            return null;
        }

        public static string ValidateFormation(FormationConfig config)
        {
            if (config == null) return "空对象";
            if (config.frontBaseTaunt < 0 || config.backBaseTaunt < 0) return "基础嘲讽不可负";
            return null;
        }

        public static string ValidateLevel(LevelConfig l)
        {
            if (l == null) return "空对象";
            if (l.id <= 0) return "id 须 > 0";
            if (l.waves == null || l.waves.Count == 0) return "waves 空";
            foreach (var w in l.waves)
            {
                if (w == null || w.enemies == null || w.enemies.Length == 0) return "wave 的 enemies 空";
                if (w.enemies.Length > Formation.Capacity) return "wave 超过五格容量";
                if (w.slots == null || w.slots.Length != w.enemies.Length) return "wave slots 必须与 enemies 等长";
                var occupied = new HashSet<string>();
                for (int i = 0; i < w.slots.Length; i++)
                {
                    if (string.IsNullOrEmpty(w.enemies[i])) return "wave enemy id 空";
                    if (!Formation.IsValidSlot(w.slots[i])) return "wave slot 非法: " + w.slots[i];
                    if (!occupied.Add(w.slots[i])) return "wave slot 重复: " + w.slots[i];
                }
            }
            return null;
        }
    }
}