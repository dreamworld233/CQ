using NUnit.Framework;
using System.Collections.Generic;
using CQ.Core.Battle;
using CQ.Core.Combat;

public class TurnManagerTests
{
    private static Unit P(string id, int speed) => new Unit(id, Team.Player) { MaxHp = 10, Hp = 10, BaseSpeed = speed };
    private static Unit E(string id, int speed) => new Unit(id, Team.Enemy) { MaxHp = 10, Hp = 10, BaseSpeed = speed };

    [Test]
    public void EmptyRound_Advances_WithoutError()
    {
        var tm = new TurnManager(new Rng(1), new List<Unit> { P("p1", 5) }, new List<Unit> { E("e1", 3) });
        var order = tm.RunRound();

        Assert.AreEqual(1, tm.Round);
        Assert.AreEqual(2, order.Count);
        Assert.IsFalse(tm.IsFinished);
    }

    [Test]
    public void KillingEnemy_Finishes_Victory()
    {
        var e = E("e1", 3);
        var tm = new TurnManager(new Rng(1), new List<Unit> { P("p1", 5) }, new List<Unit> { e });
        tm.RunRound(unit => { if (unit.Team == Team.Player) e.TakeDamage(999); });

        Assert.IsTrue(tm.IsFinished);
        Assert.AreEqual(Team.Player, tm.Winner);
    }

    [Test]
    public void DeadUnit_SkipsItsOwnTurn()
    {
        var e = E("e1", 5);
        var tm = new TurnManager(new Rng(1), new List<Unit> { P("p1", 9) }, new List<Unit> { e });
        int enemyActions = 0;
        tm.RunRound(unit =>
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

        var order = tm.RunRound();
        Assert.AreEqual(Team.Enemy, order[0].Team);
    }
}