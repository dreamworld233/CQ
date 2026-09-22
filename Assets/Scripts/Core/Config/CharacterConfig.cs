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
        public string type;       // basic | skill | ult
        public int energyDelta;   // 普攻回能量
        public int energyCost;    // 技能耗能量
        public int ultDelta;      // 攒大招条
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
        public int maxEnergy;
        public int maxUlt;
        public string row;        // front | back
        public int aggroWeight;
        public List<SkillSpec> skills;
    }
}