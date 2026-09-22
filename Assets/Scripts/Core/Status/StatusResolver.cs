using CQ.Core.Battle;
using CQ.Core.Combat;

namespace CQ.Core.Status
{
    /// <summary>
    /// 状态系统结算时序（T6）。纯函数作用于 CombatContext，零 UnityEngine，零硬编码数值。
    /// 承上：虚弱/护盾/攻增结算已由 Damage.Apply 处理，本类不重复实现。
    /// 启下：EffectiveSpeedOfUnit 作为 speedOf 注入 TurnManager，只影响本回合速度。
    /// </summary>
    public static class StatusResolver
    {
        /// <summary>
        /// 有效速度 = SpeedSorter.EffectiveSpeed(baseSpeed, hastePercent, slowPercent)。
        /// slowPercent 读 u 身上的 StatusType.Slow.Percent（无则 0），hastePercent 读 ctx.GetHaste(u.Id)。
        /// </summary>
        public static int EffectiveSpeedOfUnit(Unit u, CombatContext ctx)
        {
            if (u == null || ctx == null) return u != null ? u.BaseSpeed : 0;

            int slowPercent = 0;
            var slow = ctx.FindStatus(u.Id, StatusType.Slow);
            if (slow != null) slowPercent = slow.Percent;

            int hastePercent = ctx.GetHaste(u.Id);

            return SpeedSorter.EffectiveSpeed(u.BaseSpeed, hastePercent, slowPercent);
        }

        /// <summary>身上有 Interrupt 则跳过本回合行动。</summary>
        public static bool ShouldSkipTurn(Unit u, CombatContext ctx)
        {
            return u != null && ctx != null && ctx.HasStatus(u.Id, StatusType.Interrupt);
        }

        /// <summary>
        /// 回合末结算：
        /// 1) turn == "round" 的状态 Duration 减 1，Duration &lt;= 0 移除；
        /// 2) Interrupt 一律清空；
        /// 3) Haste 瞬态清空（只本回合）；
        /// 4) turn != "round"（如 Shield 的 "once"）不随回合衰减，由 Damage.Apply 消费移除。
        /// </summary>
        public static void RoundEndTick(CombatContext ctx)
        {
            if (ctx == null) return;

            foreach (var unit in ctx.Units)
            {
                if (!ctx.Statuses.TryGetValue(unit.Id, out var list)) continue;

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var fx = list[i];
                    if (fx == null) continue;

                    if (fx.Type == StatusType.Interrupt)
                    {
                        list.RemoveAt(i);
                        continue;
                    }

                    if (fx.Turn == "round")
                    {
                        fx.Duration -= 1;
                        if (fx.Duration <= 0) list.RemoveAt(i);
                    }
                }
            }

            ctx.Haste.Clear();
        }
    }
}