using System;

namespace CQ.Core.Config
{
    [Serializable]
    public class CardEffectSpec
    {
        public string type;
        public string target;
        public int percent;
        public int amount;
        public int duration;
    }

    [Serializable]
    public class CardConfig
    {
        public string id;
        public string name;
        public CardEffectSpec effect;
        public int count;      // 卡组张数
    }
}