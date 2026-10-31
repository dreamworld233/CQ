using System;
using System.Collections.Generic;

namespace CQ.Core.Config
{
    [Serializable]
    public class SkillEffectSpec
    {
        public string type;
        public string target;
        public int count;
        public int multiplierPercent;
        public int hits;
        public int percent;
        public int duration;
        public int amount;
    }

    [Serializable]
    public class SkillSpec
    {
        public string id;
        public string name;
        public string type;
        public string trigger;
        public List<SkillEffectSpec> effects;
    }

    [Serializable]
    public class CharacterConfig
    {
        public string id;
        public string name;
        public int maxHp;
        public int baseAttack;
        public int baseDefense;
        public int baseSpeed;
        public string defaultSlot;
        public int initialEnergy;
        public int maxEnergy;
        public int actionEnergy;
        public int hitEnergy;
        public int specialInterval;
        public List<SkillSpec> skills;
    }
}
