using NUnit.Framework;
using System.Collections.Generic;
using CQ.Core.Battle;
using CQ.Core.Combat;

public class SpeedSorterTests
{
    private static Unit Player(int speed) => new Unit("p", Team.Player) { MaxHp = 10, Hp = 10, BaseSpeed = speed };
    private static Unit Enemy(int speed) => new Unit("e", Team.Enemy) { MaxHp = 10, Hp = 10, BaseSpeed = speed };

    [Test]
    public void SameSpeed_Player_First()
    {
        var e = Enemy(5);
        var p = Player(5);
        var order = SpeedSorter.Sort(new List<Unit> { e, p }, u => u.BaseSpeed);
        Assert.AreEqual(Team.Player, order[0].Team);
    }

    [Test]
    public void SameTeamSameSpeed_StableOrder()
    {
        var a = Player(5); a.Id = "a";
        var b = Player(5); b.Id = "b";
        var c = Player(5); c.Id = "c";
        var order = SpeedSorter.Sort(new List<Unit> { a, b, c }, u => u.BaseSpeed);
        Assert.AreEqual("a", order[0].Id);
        Assert.AreEqual("b", order[1].Id);
        Assert.AreEqual("c", order[2].Id);
    }

    [Test]
    public void FasterUnit_ActsFirst()
    {
        var slow = Player(4);
        var fast = Enemy(9);
        var order = SpeedSorter.Sort(new List<Unit> { slow, fast }, u => u.BaseSpeed);
        Assert.AreEqual(9, order[0].BaseSpeed);
    }

    [Test]
    public void EffectiveSpeed_HasteSlow_OnlyCurrentRound()
    {
        var u = Player(10);
        Assert.AreEqual(10, SpeedSorter.EffectiveSpeed(10, 0, 0));
        Assert.AreEqual(15, SpeedSorter.EffectiveSpeed(10, 50, 0));
        Assert.AreEqual(5, SpeedSorter.EffectiveSpeed(10, 0, 50));
        Assert.AreEqual(12, SpeedSorter.EffectiveSpeed(10, 25, 0)); // 12.5 去尾
        Assert.AreEqual(10, u.BaseSpeed); // 修正不落库，只本回合
    }
}