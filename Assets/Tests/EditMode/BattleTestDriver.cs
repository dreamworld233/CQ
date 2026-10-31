using System;
using System.Collections.Generic;
using CQ.Core.Battle;
using CQ.Core.Combat;
using NUnit.Framework;

internal static class BattleTestDriver
{
    public static ActionResult Step(BattleEngine engine)
    {
        var result = engine.CurrentAction ?? engine.Advance();
        Assert.IsNotNull(result, "战斗应有下一动作");
        Assert.IsTrue(engine.Confirm(engine.BattleId, result.Sequence));
        return result;
    }

    public static ActionResult NextAction(BattleEngine engine, string actorId)
    {
        for (int i = 0; i < 200 && !engine.IsFinished; i++)
        {
            var result = Step(engine);
            if (result.ActorId == actorId && result.Kind != BattleEventKind.Passive) return result;
        }
        Assert.Fail("未找到指定单位的个人行动: " + actorId);
        return null;
    }

    public static void Drain(BattleEngine engine, int limit = 10000)
    {
        for (int i = 0; i < limit; i++)
        {
            var result = engine.CurrentAction ?? engine.Advance();
            if (result == null)
            {
                Assert.IsTrue(engine.IsFinished, "全自动战斗不可停在玩家输入阶段");
                return;
            }
            Assert.IsTrue(engine.Confirm(engine.BattleId, result.Sequence));
        }
        Assert.Fail("自动战斗未在动作预算内结束");
    }

    public static IReadOnlyList<Unit> CompleteRound(TurnManager turns, Action<Unit> resolve = null)
    {
        var order = turns.BeginActions();
        if (order == null) return Array.Empty<Unit>();
        for (int i = 0; i < 1000; i++)
        {
            var unit = turns.NextNormal();
            if (unit == null)
            {
                turns.CheckWinner();
                turns.CompleteRound();
                return order;
            }
            resolve?.Invoke(unit);
            turns.CheckWinner();
        }
        Assert.Fail("普通队列推进超过上限");
        return order;
    }
}
