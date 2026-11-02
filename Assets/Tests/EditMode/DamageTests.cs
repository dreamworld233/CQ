using CQ.Core.Combat;
using CQ.Core.Config;
using System.Linq;
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
    public void FiveElementMatchupsCoverEveryPairWithoutDoubleMultiplication()
    {
        string[] elements = { "metal", "wood", "water", "fire", "earth" };
        int[,] expected = {
            { 100, 200, 100, 50, 100 }, { 50, 100, 100, 100, 200 },
            { 100, 100, 100, 200, 50 }, { 200, 100, 50, 100, 100 }, { 100, 50, 200, 100, 100 }
        };
        for (int source = 0; source < elements.Length; source++)
            for (int target = 0; target < elements.Length; target++)
                Assert.AreEqual(expected[source, target], CharacterElements.DamageMultiplierPercent(elements[source], elements[target]),
                    elements[source] + " -> " + elements[target]);
        foreach (string missing in new[] { null, "", "unknown" })
        {
            Assert.AreEqual(100, CharacterElements.DamageMultiplierPercent(missing, "metal"));
            Assert.AreEqual(100, CharacterElements.DamageMultiplierPercent("metal", missing));
        }
    }

    [TestCase("metal", "wood", 180)]
    [TestCase("wood", "metal", 45)]
    [TestCase("metal", "metal", 90)]
    [TestCase("metal", "water", 90)]
    [TestCase(null, "wood", 90)]
    public void ElementsTravelFromConfigToUnits_AndScaleDamageAfterDefense(string sourceElement, string targetElement, int expected)
    {
        var hero = AutoBattleTestData.Hero(attack: 101);
        var enemy = AutoBattleTestData.Enemy(defense: 11);
        hero.element = sourceElement;
        enemy.element = targetElement;
        var engine = AutoBattleTestData.Engine(heroes: new[] { hero }, enemies: new[] { enemy });
        Assert.AreEqual(sourceElement, engine.Players[0].Element);
        Assert.AreEqual(targetElement, engine.Enemies[0].Element);
        Assert.AreEqual(expected, Damage.Apply(engine.Ctx, engine.Players[0], engine.Enemies[0], 100));
    }

    [TestCase(16, 11, "wood", "metal", 2)]
    [TestCase(1, 100, "wood", "metal", 1)]
    [TestCase(1, 100, "metal", "wood", 2)]
    [TestCase(int.MaxValue, 0, "metal", "wood", int.MaxValue)]
    public void ElementDamageFloorsOnceAfterDefense_StaysPositiveAndDoesNotOverflow(int attack, int defense, string source, string target, int expected)
    {
        var attacker = new Unit("a", Team.Player) { BaseAttack = attack, Element = source };
        var defender = new Unit("b", Team.Enemy) { BaseDefense = defense, Element = target };
        Assert.AreEqual(expected, ActionStatuses.CalculateDamage(attacker, new ActionStatuses(), defender, new ActionStatuses(), 100));
    }

    [Test]
    public void ElementModifierFollowsEffectiveBuffedDamage_NotRawAttack()
    {
        var hero = AutoBattleTestData.Hero(attack: 101);
        hero.element = "metal";
        var enemy = AutoBattleTestData.Enemy(defense: 51);
        enemy.element = "wood";
        var engine = AutoBattleTestData.Engine(heroes: new[] { hero }, enemies: new[] { enemy });
        engine.Ctx.StatesOf("hero").Apply(StatusType.AttackUp, 25, 2);
        engine.Ctx.StatesOf("enemy_0").Apply(StatusType.DefenseUp, 25, 2);
        Assert.AreEqual(378, Damage.Apply(engine.Ctx, engine.Players[0], engine.Enemies[0], 200));
    }

    [Test]
    public void ElementalMultiHitStillChargesOnce_AndLethalDamageStopsRemainingSegments()
    {
        var hero = AutoBattleTestData.Hero(attack: 10);
        hero.element = "metal";
        AutoBattleTestData.SetSkill(hero, AutoBattleTestData.Skill("basic", AutoBattleTestData.Effect(hits: 3)));
        var enemy = AutoBattleTestData.Enemy(hp: 1000);
        enemy.element = "wood";
        var engine = AutoBattleTestData.Engine(heroes: new[] { hero }, enemies: new[] { enemy });
        engine.StartBattle();
        var result = BattleTestDriver.NextAction(engine, "hero");
        CollectionAssert.AreEqual(new[] { 20, 20, 20 }, result.Hits.Select(hit => hit.HpDamage));
        Assert.AreEqual(1, result.Charges.Count(charge => charge.Trigger == "hit"));
        Assert.AreEqual(1, result.Charges.Count(charge => charge.Trigger == "action"));
        enemy.maxHp = 25;
        engine = AutoBattleTestData.Engine(heroes: new[] { hero }, enemies: new[] { enemy });
        engine.StartBattle();
        result = BattleTestDriver.NextAction(engine, "hero");
        CollectionAssert.AreEqual(new[] { 20, 5 }, result.Hits.Select(hit => hit.HpDamage));
        Assert.IsTrue(result.Hits.Last().TargetDead);
        Assert.AreEqual(0, engine.Enemies[0].Hp);
    }

    [Test]
    public void ElementDoesNotScaleHealing()
    {
        var hero = AutoBattleTestData.Hero(hp: 100);
        hero.element = "metal";
        var enemy = AutoBattleTestData.Enemy();
        enemy.element = "wood";
        var engine = AutoBattleTestData.Engine(heroes: new[] { hero }, enemies: new[] { enemy });
        var unit = engine.Players[0];
        unit.Hp = 50;
        SkillResolver.Resolve(engine.Ctx, unit, AutoBattleTestData.Skill("special",
            AutoBattleTestData.Effect("heal", "self", multiplier: 0, hits: 0, percent: 25)));
        Assert.AreEqual(75, unit.Hp);
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
