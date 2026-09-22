using CQ.Core.Config;

namespace CQ.Core.Cards
{
    /// <summary>
    /// CardConfig 只读包装。暴露 id/name/effect/count，不复制数据、不改配置。
    /// </summary>
    public sealed class CardDef
    {
        public string id => config.id;
        public string name => config.name;
        public CardEffectSpec effect => config.effect;
        public int count => config.count;

        private readonly CardConfig config;

        public CardDef(CardConfig config)
        {
            this.config = config;
        }
    }
}