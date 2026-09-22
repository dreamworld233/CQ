using System;
using System.Collections.Generic;

namespace CQ.Core.Config
{
    [Serializable]
    public class IntentConfig
    {
        public string id;         // 引用键
        public string type;       // attack | charge | attackUp | multi
        public string target;     // single | all
        public int damage;
        public int hits;          // multi 连击次数
        public int buffPercent;   // attackUp 攻增百分比
        public int nextDamage;    // charge 下一回合伤害
        public bool interruptible;
    }

    [Serializable]
    public class EnemyConfig
    {
        public string id;
        public string name;
        public int maxHp;
        public int baseSpeed;
        public string row;        // front | back
        public int aggroWeight;
        public string[] intentSequence;
        public List<IntentConfig> intents;
    }
}