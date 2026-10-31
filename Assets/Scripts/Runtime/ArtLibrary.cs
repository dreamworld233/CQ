using System.Collections.Generic;
using CQ.Core.Config;
using UnityEngine;

namespace CQ.Runtime
{
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

        public static Sprite UnitSprite(string unitId)
        {
            string key = ConfigIdOf(unitId);
            if (string.IsNullOrEmpty(key)) return null;
            return UnitArt.TryGetValue(key, out var file) ? Load(file) : null;
        }

        /// <summary>
        /// 选中/可选目标圆环（`按钮选中框.png`）。
        /// 注意：该素材实测是**空心圆环**（200×200，环宽约 11px、外径约 184px，近白色），
        /// 不是矩形面板底框，所以不做 9-slice，直接当圆环用。
        /// </summary>
        public static Sprite RingSprite() => Load("按钮选中框");

        public static Sprite SkillIcon(SkillSpec skill)
        {
            if (skill?.effects == null) return null;
            foreach (var effect in skill.effects)
            {
                switch (effect.type)
                {
                    case "damage": return Load("攻击");
                    case "defenseUp": return Load("防御");
                    case "heal":
                    case "attackUp":
                    case "energy": return Load("增益");
                    case "stun": return Load("减益");
                }
            }
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
