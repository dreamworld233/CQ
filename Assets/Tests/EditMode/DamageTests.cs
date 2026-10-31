using CQ.Core.Combat;
using NUnit.Framework;

public class DamageTests
{
    [TestCase(100, 50, 25, 0, 200, 200)]
    [TestCase(100, 80, 0, 25, 100, 1)]
    [TestCase(1, 100, 0, 0, 100, 1)]
    [TestCase(100, 100, 0, 0, 100, 1)]
    [TestCase(101, 51, 25, 25, 200, 189)]
    public void ActualDamageUsesEffectiveAttackDefense_AndMinimumOne(
        int attack, int defense, int attackUp, int defenseUp, int multiplier, int expected)
    {
        var engine = AutoBattleTestData.Engine(heroes: new[] { AutoBattleTestData.Hero(attack: attack) },
            enemies: new[] { AutoBattleTestData.Enemy(defense: defense) });
        var source = engine.Players[0];
        var target = engine.Enemies[0];
        if (attackUp > 0) engine.Ctx.StatesOf(source.Id).Apply(StatusType.AttackUp, attackUp, 2);
        if (defenseUp > 0) engine.Ctx.StatesOf(target.Id).Apply(StatusType.DefenseUp, defenseUp, 2);
        int before = target.Hp;
        Assert.AreEqual(expected, Damage.Apply(engine.Ctx, source, target, multiplier));
        Assert.AreEqual(before - expected, target.Hp);
        Assert.AreEqual(attack, source.BaseAttack);
        Assert.AreEqual(defense, target.BaseDefense);
    }

    [Test]
    public void OverkillRecordsOnlyActualHpLost_AndCannotDamageDeadTargetAgain()
    {
        var engine = AutoBattleTestData.Engine(heroes: new[] { AutoBattleTestData.Hero(attack: 100) },
            enemies: new[] { AutoBattleTestData.Enemy(hp: 5) });
        var source = engine.Players[0];
        var target = engine.Enemies[0];
        Assert.AreEqual(5, Damage.Apply(engine.Ctx, source, target, 400));
        Assert.AreEqual(0, target.Hp);
        Assert.AreEqual(0, Damage.Apply(engine.Ctx, source, target, 400));
    }
}
