using System;
using System.Collections.Generic;

namespace CQ.Core.Config
{
    [Serializable]
    public class IntentConfig
    {
        public string id;         // 引用键
        public string type;       // attack | charge | attackUp | multi | debuff
        public string target;     // single | all
        public int damage;
        public int hits;          // multi 连击次数
        public int buffPercent;   // attackUp 或 attack/multi 伤害结束后的自身攻增
        public int buffDuration;  // 攻增持续回合（round，包含施加当轮）
        public int nextDamage;    // charge 下一回合伤害
        public bool interruptible;
        public string debuffType;   // debuff 状态 id（weaken/slow）
        public int debuffPercent;   // debuff 百分比
        public int debuffDuration;  // debuff 持续回合
    }

    [Serializable]
    public class EnemyConfig
    {
        public string id;
        public string name;
        public int maxHp;
        public int baseSpeed;
        public string[] intentSequence;
        public List<IntentConfig> intents;
    }
}