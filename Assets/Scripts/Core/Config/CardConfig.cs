using System;

namespace CQ.Core.Config
{
    [Serializable]
    public class CardEffectSpec
    {
        public string type;    // interrupt | weaken | slow | haste | shield | attackUp
        public int percent;    // weaken / slow / haste / attackUp
        public int amount;     // shield
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