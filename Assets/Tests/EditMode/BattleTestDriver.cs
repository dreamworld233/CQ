using System;
using System.Collections.Generic;
using CQ.Core.Battle;
using CQ.Core.Combat;
using NUnit.Framework;

internal static class BattleTestDriver
{
    public static void Drain(BattleEngine engine)
    {
        for (int i = 0; i < 1000; i++)
        {
            var result = engine.CurrentAction ?? engine.Advance();
            if (result == null) return;
            Assert.IsTrue(engine.Confirm(engine.BattleId, result.Sequence));
        }
        Assert.Fail("动作推进超过上限");
    }

    public static IReadOnlyList<Unit> CompleteRound(BattleEngine engine)
    {
        Drain(engine);
        if (engine.IsFinished) return engine.TurnOrder;
        Assert.IsTrue(engine.BeginActions());
        Drain(engine);
        Assert.IsTrue(engine.IsFinished || engine.Flow == BattleFlow.Input);
        return engine.TurnOrder;
    }

    public static bool RequestAndPlay(BattleEngine engine, string actor, string target = null)
    {
        if (!engine.RequestUltimate(actor, target)) return false;
        Drain(engine);
        return true;
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
