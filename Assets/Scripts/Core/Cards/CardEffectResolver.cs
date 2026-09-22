using CQ.Core.Combat;
using CQ.Core.Config;

namespace CQ.Core.Cards
{
    /// <summary>
    /// 卡牌效果解析。按 CardEffectSpec.type 把配置数值写入 CombatContext 状态。
    /// percent/amount 全部来自 effect 字段，代码零硬编码。
    /// </summary>
    public static class CardEffectResolver
    {
        public static void Resolve(CardEffectSpec effect, CombatContext ctx, string targetUnitId, string sourceUnitId)
        {
            if (effect == null || ctx == null || string.IsNullOrEmpty(targetUnitId)) return;

            switch (effect.type)
            {
                case "interrupt":
                    ctx.AddStatus(targetUnitId, new StatusEffect
                    {
                        Type = StatusType.Interrupt,
                        Duration = 1,
                        Turn = "round",
                        SourceUnitId = sourceUnitId
                    });
                    break;

                case "weaken":
                    ctx.AddStatus(targetUnitId, new StatusEffect
                    {
                        Type = StatusType.Weaken,
                        Percent = effect.percent,
                        Duration = 1,
                        Turn = "round",
                        SourceUnitId = sourceUnitId
                    });
                    break;

                case "slow":
                    ctx.AddStatus(targetUnitId, new StatusEffect
                    {
                        Type = StatusType.Slow,
                        Percent = effect.percent,
                        Duration = 1,
                        Turn = "round",
                        SourceUnitId = sourceUnitId
                    });
                    break;

                case "shield":
                    ctx.AddStatus(targetUnitId, new StatusEffect
                    {
                        Type = StatusType.Shield,
                        Amount = effect.amount,
                        Duration = 1,
                        Turn = "once",
                        SourceUnitId = sourceUnitId
                    });
                    break;

                case "attackUp":
                    ctx.AddStatus(targetUnitId, new StatusEffect
                    {
                        Type = StatusType.AttackUp,
                        Percent = effect.percent,
                        Duration = 1,
                        Turn = "round",
                        SourceUnitId = sourceUnitId
                    });
                    break;

                case "haste":
                    ctx.SetHaste(targetUnitId, effect.percent);
                    break;
            }
        }
    }
}