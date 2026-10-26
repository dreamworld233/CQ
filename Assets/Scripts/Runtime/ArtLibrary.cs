using System.Collections.Generic;
using CQ.Core.Config;
using UnityEngine;

namespace CQ.Runtime
{
    /// <summary>
    /// 表现层素材库：按约定名从 `Resources/Arts/UI/` 加载单位示意图、敌人意图图标、选中圆环。
    /// 缺素材返回 null，调用方回退纯色/代码图形占位，保证不空引用。
    /// （表现层映射，非平衡数值，不进 JSON。）
    /// </summary>
    public static class ArtLibrary
    {
        private const string Root = "Arts/UI/";

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>单位 id → 示意图文件名。敌人实例 id 形如 `grunt_0`，取配置 id 部分。</summary>
        private static readonly Dictionary<string, string> UnitArt = new Dictionary<string, string>
        {
            { "sword", "战士示意图" },
            { "mage",  "法师示意图" },
            { "guard", "盾兵示意图" },
            { "grunt", "小怪示意图" },
            { "claw",  "精英怪示意图" },
            { "lord",  "精英怪示意图" },   // 领主示意图缺，暂复用精英怪
        };

        /// <summary>敌人意图 type → 图标文件名。</summary>
        private static readonly Dictionary<string, string> IntentArt = new Dictionary<string, string>
        {
            { "attack",   "攻击" },
            { "multi",    "攻击" },
            { "charge",   "攻击" },
            { "attackUp", "增益" },
            { "debuff",   "减益" },
            { "defend",   "防御" },
        };

        public static Sprite UnitSprite(string unitId)
        {
            string key = ConfigIdOf(unitId);
            if (string.IsNullOrEmpty(key)) return null;
            return UnitArt.TryGetValue(key, out var file) ? Load(file) : null;
        }

        public static Sprite IntentSprite(string intentType)
        {
            if (string.IsNullOrEmpty(intentType)) return null;
            return IntentArt.TryGetValue(intentType, out var file) ? Load(file) : null;
        }

        /// <summary>
        /// 选中/可选目标圆环（`按钮选中框.png`）。
        /// 注意：该素材实测是**空心圆环**（200×200，环宽约 11px、外径约 184px，近白色），
        /// 不是矩形面板底框，所以不做 9-slice，直接当圆环用。
        /// </summary>
        public static Sprite RingSprite() => Load("按钮选中框");

        /// <summary>
        /// 技能图标占位：普攻 → 攻击图标，注牌终结技 → 增益图标。
        /// </summary>
        public static Sprite SkillIcon(SkillSpec skill)
        {
            if (skill == null) return null;
            if (skill.damage != null) return Load("攻击");
            if (skill.injectCards != null && skill.injectCards.Count > 0) return Load("增益");
            return null;
        }

        public static Sprite Load(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            if (Cache.TryGetValue(fileName, out var cached)) return cached;
            var s = Resources.Load<Sprite>(Root + fileName);
            Cache[fileName] = s;   // 缓存 null，避免重复 IO
            return s;
        }

        /// <summary>`grunt_0` → `grunt`；玩家 `sword` 原样返回。</summary>
        private static string ConfigIdOf(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return null;
            int cut = unitId.LastIndexOf('_');
            if (cut <= 0 || cut >= unitId.Length - 1) return unitId;
            for (int i = cut + 1; i < unitId.Length; i++)
                if (!char.IsDigit(unitId[i])) return unitId;
            return unitId.Substring(0, cut);
        }
    }
}
