using System;
using System.Collections.Generic;

namespace CQ.Core.Config
{
    [Serializable]
    public class DamageSpec
    {
        public string kind;    // physical | magic
        public string target;  // single | all
        public int amount;
    }

    [Serializable]
    public class HealSpec
    {
        public string target;  // self | single | all
        public int amount;
    }

    [Serializable]
    public class CardInjectionSpec
    {
        public string cardId;
        public int count;
    }

    [Serializable]
    public class SkillSpec
    {
        public string id;
        public string name;
        public string type;       // basic | ult
        public DamageSpec damage; // 伤害招式
        public HealSpec heal;     // 仅用于拒绝旧直接治疗配置，不再执行
        public List<CardInjectionSpec> injectCards; // 终结技注入剩余抽牌堆
    }

    [Serializable]
    public class CharacterConfig
    {
        public string id;
        public string name;
        public string role;       // dps | tank | support
        public int maxHp;
        public int baseSpeed;
        public int maxUlt;
        public string ultChargeTrigger; // attack | hit
        public int ultChargeAmount;
        public string defaultSlot; // front1 | front2 | back1 | back2 | back3
        public List<SkillSpec> skills;
    }
}