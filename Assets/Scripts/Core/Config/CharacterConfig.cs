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
    public class SkillSpec
    {
        public string id;
        public string name;
        public string type;       // basic | ult
        public DamageSpec damage; // 伤害招式
        public HealSpec heal;     // 治疗招式
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