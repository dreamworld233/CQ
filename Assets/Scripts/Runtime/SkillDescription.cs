using System.Linq;
using CQ.Core.Config;

namespace CQ.Runtime
{
    /// <summary>从实际技能配置提取战斗内功能概述，不显示培养页级别的数值细节。</summary>
    public static class SkillDescription
    {
        public static string TypeLabel(string type)
        {
            switch (type)
            {
                case "special": return "技能";
                case "ult": return "终结技";
                case "passive": return "被动";
                default: return "普攻";
            }
        }

        public static string Summary(SkillSpec skill)
        {
            if (skill?.effects == null || skill.effects.Count == 0) return "暂无效果说明";
            string prefix = skill.type != "passive" ? "" : skill.trigger == "permanent" ? "常驻：" : "每回合：";
            return prefix + string.Join("；", skill.effects.Select(Effect).Distinct()) + "。";
        }

        private static string Effect(SkillEffectSpec effect)
        {
            if (effect == null) return "暂无效果说明";
            string target = Target(effect);
            switch (effect.type)
            {
                case "damage": return (effect.hits > 1 ? "连续攻击" : "攻击") + target;
                case "heal": return "为" + target + "恢复生命";
                case "attackUp": return "提高" + target + "的攻击";
                case "defenseUp": return "提高" + target + "的防御";
                case "energy": return "为" + target + "补充能量";
                case "stun": return "使" + target + "眩晕，无法行动";
                default: return "暂无效果说明";
            }
        }

        private static string Target(SkillEffectSpec effect)
        {
            string enemies = effect.count > 1 ? "多名敌人" : "敌人";
            string allies = effect.count > 1 ? "多名队友" : "队友";
            switch (effect.target)
            {
                case "self": return "自身";
                case "enemyFront": return "前排优先的" + enemies;
                case "enemyBack": return "后排优先的" + enemies;
                case "enemyRandom": return "随机" + enemies;
                case "enemyAll": return "全体敌人";
                case "allyRandom": return "随机" + allies;
                case "allyFront": return "前排优先的" + allies;
                case "allyLowestHp": return "生命比例最低的队友";
                case "allyLowestEnergy": return "能量最低的队友";
                case "allyAll": return "全体队友";
                default: return "目标";
            }
        }
    }
}
