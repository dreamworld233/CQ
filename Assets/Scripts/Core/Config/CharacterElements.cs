using System;
using System.Collections.Generic;

namespace CQ.Core.Config
{
    /// <summary>五行分类与伤害相克关系；不绑定职业或技能类型。</summary>
    public static class CharacterElements
    {
        public static readonly IReadOnlyList<string> All = Array.AsReadOnly(new[] { "metal", "wood", "water", "fire", "earth" });
        public static bool IsValid(string value) => value == "metal" || value == "wood" || value == "water" || value == "fire" || value == "earth";
        public static int DamageMultiplierPercent(string source, string target)
        {
            if (!IsValid(source) || !IsValid(target)) return 100;
            if (Counters(source, target)) return 200;
            return Counters(target, source) ? 50 : 100;
        }

        private static bool Counters(string source, string target)
        {
            switch (source)
            {
                case "metal": return target == "wood";
                case "wood": return target == "earth";
                case "earth": return target == "water";
                case "water": return target == "fire";
                case "fire": return target == "metal";
                default: return false;
            }
        }

        public static string Label(string value)
        {
            switch (value)
            {
                case "metal": return "金";
                case "wood": return "木";
                case "water": return "水";
                case "fire": return "火";
                case "earth": return "土";
                default: return "未分类";
            }
        }
    }
}
