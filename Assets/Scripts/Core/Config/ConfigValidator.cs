using System;

namespace CQ.Core.Config
{
    /// <summary>
    /// 纯 C# 配置校验，零引擎依赖。返回 null 表示通过，否则返回缺字段描述。
    /// </summary>
    public static class ConfigValidator
    {
        private static readonly string[] Rows = { "front", "back" };
        private static readonly string[] SkillTypes = { "basic", "skill", "ult" };
        private static readonly string[] IntentTypes = { "attack", "charge", "attackUp", "multi" };
        private static readonly string[] CardTypes = { "interrupt", "weaken", "slow", "haste", "shield", "attackUp" };
        private static readonly string[] StatusIds = { "weaken", "shield", "attackUp", "slow", "interrupt" };
        private static readonly string[] Turns = { "round", "once" };

        private static bool In(string[] set, string value) => Array.IndexOf(set, value) >= 0;

        public static string ValidateCharacter(CharacterConfig c)
        {
            if (c == null) return "空对象";
            if (string.IsNullOrEmpty(c.id)) return "缺 id";
            if (string.IsNullOrEmpty(c.name)) return "缺 name";
            if (c.maxHp <= 0) return "maxHp 须 > 0";
            if (c.baseSpeed <= 0) return "baseSpeed 须 > 0";
            if (c.maxEnergy < 0 || c.maxUlt < 0) return "maxEnergy/maxUlt 不可负";
            if (!In(Rows, c.row)) return "row 须为 front|back";
            if (c.aggroWeight < 0) return "aggroWeight 不可负";
            if (c.skills == null || c.skills.Count == 0) return "skills 空";
            foreach (var s in c.skills)
            {
                if (string.IsNullOrEmpty(s.id)) return "技能缺 id";
                if (string.IsNullOrEmpty(s.name)) return "技能缺 name";
                if (!In(SkillTypes, s.type)) return "技能 type 非法: " + s.type;
                if (s.damage == null && s.heal == null) return "技能 " + s.id + " 缺 damage/heal";
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
            if (!In(Rows, e.row)) return "row 须为 front|back";
            if (e.aggroWeight < 0) return "aggroWeight 不可负";
            if (e.intents == null || e.intents.Count == 0) return "intents 空";
            if (e.intentSequence == null || e.intentSequence.Length < 2 || e.intentSequence.Length > 4)
                return "intentSequence 长度须 2~4";
            foreach (var id in e.intentSequence)
                if (FindIntent(e, id) == null) return "intentSequence 引用未定义意图: " + id;
            foreach (var it in e.intents)
            {
                if (string.IsNullOrEmpty(it.id)) return "意图缺 id";
                if (!In(IntentTypes, it.type)) return "意图 type 非法: " + it.type;
                if (it.type == "charge" && !it.interruptible) return "charge 必须 interruptible=true";
                if (it.type == "attackUp" && it.buffPercent <= 0) return "attackUp 需 buffPercent > 0";
                if (it.type == "attackUp" && it.buffDuration <= 0) return "attackUp 需 buffDuration > 0";
            }
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
            if (c.count <= 0) return "count 须 > 0";
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

        public static string ValidateLevel(LevelConfig l)
        {
            if (l == null) return "空对象";
            if (l.id <= 0) return "id 须 > 0";
            if (l.waves == null || l.waves.Count == 0) return "waves 空";
            foreach (var w in l.waves)
                if (w.enemies == null || w.enemies.Length == 0) return "wave 的 enemies 空";
            return null;
        }
    }
}