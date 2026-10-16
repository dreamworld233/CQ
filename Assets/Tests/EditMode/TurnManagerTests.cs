using NUnit.Framework;
using System.Collections.Generic;
using CQ.Core.Battle;
using CQ.Core.Combat;

public class TurnManagerTests
{
    private static Unit P(string id, int speed) => new Unit(id, Team.Player) { SlotId = "front1", MaxHp = 10, Hp = 10, BaseSpeed = speed };
    private static Unit E(string id, int speed) => new Unit(id, Team.Enemy) { SlotId = "front1", MaxHp = 10, Hp = 10, BaseSpeed = speed };

    [Test]
    public void BeginActions_Freezes_Queue_And_Rejects_Second_Start()
    {
        var p = P("p", 9); var e = E("e", 5);
        var tm = new TurnManager(new Rng(1), new[] { p }, new[] { e });
        Assert.IsNull(tm.NextNormal());
        var order = tm.BeginActions();
        Assert.IsNull(tm.BeginActions()); Assert.AreEqual(1, tm.Round);
        e.BaseSpeed = 99;
        Assert.AreSame(p, tm.NextNormal()); Assert.AreSame(e, tm.NextNormal());
        Assert.IsNull(tm.NextNormal()); Assert.IsNull(tm.NextNormal());
        Assert.AreSame(p, order[0]); Assert.AreEqual(BattlePhase.Acting, tm.Phase);
        tm.CompleteRound(); Assert.AreEqual(BattlePhase.RoundEnd, tm.Phase);
        Assert.IsNotNull(tm.BeginActions()); Assert.AreEqual(2, tm.Round);
        Assert.AreSame(e, tm.NextNormal());
    }

    [Test]
    public void EmptyRound_Advances_WithoutError()
    {
        var tm = new TurnManager(new Rng(1), new List<Unit> { P("p1", 5) }, new List<Unit> { E("e1", 3) });
        var order = BattleTestDriver.CompleteRound(tm);

        Assert.AreEqual(1, tm.Round);
        Assert.AreEqual(2, order.Count);
        Assert.IsFalse(tm.IsFinished);
    }

    [Test]
    public void KillingEnemy_Finishes_Victory()
    {
        var e = E("e1", 3);
        var tm = new TurnManager(new Rng(1), new List<Unit> { P("p1", 5) }, new List<Unit> { e });
        BattleTestDriver.CompleteRound(tm, unit => { if (unit.Team == Team.Player) e.TakeDamage(999); });

        Assert.IsTrue(tm.IsFinished);
        Assert.AreEqual(Team.Player, tm.Winner);
    }

    [Test]
    public void DeadUnit_SkipsItsOwnTurn()
    {
        var e = E("e1", 5);
        var tm = new TurnManager(new Rng(1), new List<Unit> { P("p1", 9) }, new List<Unit> { e });
        int enemyActions = 0;
        BattleTestDriver.CompleteRound(tm, unit =>
        {
            if (unit.Team == Team.Player) e.TakeDamage(999);
            if (unit.Team == Team.Enemy) enemyActions++;
        });

        Assert.AreEqual(0, enemyActions, "被提前打死的单位不应再行动");
    }

    [Test]
    public void InjectedSpeed_ChangesOrder()
    {
        var p = P("p1", 5);
        var e = E("e1", 5);
        var tm = new TurnManager(new Rng(1), new List<Unit> { p }, new List<Unit> { e },
            u => u.Team == Team.Enemy ? SpeedSorter.EffectiveSpeed(u.BaseSpeed, 100, 0) : u.BaseSpeed);

        var order = BattleTestDriver.CompleteRound(tm);
        Assert.AreEqual(Team.Enemy, order[0].Team);
    }
}