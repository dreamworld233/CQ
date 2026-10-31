using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Combat;
using NUnit.Framework;

public class SkillResolverTests
{
    [Test]
    public void MultiHitChargesActorAndTargetOnce_PerCompleteAttack()
    {
        var hero = AutoBattleTestData.Hero(attack: 10);
        AutoBattleTestData.SetSkill(hero, AutoBattleTestData.Skill("basic", AutoBattleTestData.Effect(hits: 3)));
        var engine = AutoBattleTestData.Engine(heroes: new[] { hero });
        engine.StartBattle();
        var result = BattleTestDriver.NextAction(engine, "hero");
        Assert.AreEqual(3, result.Hits.Count);
        Assert.AreEqual(30, result.Hits.Sum(h => h.HpDamage));
        Assert.AreEqual(60, engine.Players[0].Ult);
        Assert.AreEqual(30, engine.Enemies[0].Ult);
        Assert.AreEqual(1, result.HitTargetIds.Count);
        Assert.AreEqual(1, result.Charges.Count(c => c.UnitId == "hero"));
        Assert.AreEqual(1, result.Charges.Count(c => c.UnitId == engine.Enemies[0].Id));
    }

    [Test]
    public void GroupAttackChargesEachLivingHitTargetOnce_AndDoesNotHitDeadTargets()
    {
        var hero = AutoBattleTestData.Hero();
        AutoBattleTestData.SetSkill(hero, AutoBattleTestData.Skill("basic",
            AutoBattleTestData.Effect(target: "enemyAll", count: 1, hits: 3)));
        var engine = AutoBattleTestData.Engine(heroes: new[] { hero }, enemies: new[]
        {
            AutoBattleTestData.Enemy("a", "front1"), AutoBattleTestData.Enemy("b", "front2"),
            AutoBattleTestData.Enemy("dead", "back1")
        });
        engine.Enemies[2].TakeDamage(engine.Enemies[2].Hp);
        engine.StartBattle();
        var result = BattleTestDriver.NextAction(engine, "hero");
        Assert.AreEqual(6, result.Hits.Count);
        Assert.AreEqual(2, result.HitTargetIds.Count);
        Assert.AreEqual(60, engine.Players[0].Ult);
        Assert.AreEqual(30, engine.Enemies[0].Ult);
        Assert.AreEqual(30, engine.Enemies[1].Ult);
        Assert.AreEqual(20, engine.Enemies[2].Ult);
        Assert.IsFalse(result.Hits.Any(h => h.TargetUnitId == engine.Enemies[2].Id));
    }

    [Test]
    public void MultiHitStopsOnTargetDeath_AndDoesNotRetargetRemainingHits()
    {
        var hero = AutoBattleTestData.Hero(attack: 10);
        AutoBattleTestData.SetSkill(hero, AutoBattleTestData.Skill("basic", AutoBattleTestData.Effect(hits: 3)));
        var engine = AutoBattleTestData.Engine(heroes: new[] { hero }, enemies: new[]
        {
            AutoBattleTestData.Enemy("fragile", "front1", hp: 5), AutoBattleTestData.Enemy("back", "back1")
        });
        engine.StartBattle();
        var result = BattleTestDriver.NextAction(engine, "hero");
        Assert.AreEqual(1, result.Hits.Count);
        Assert.AreEqual(5, result.Hits.Single().HpDamage);
        Assert.IsTrue(result.Hits.Single().TargetDead);
        Assert.AreEqual(engine.Enemies[1].MaxHp, engine.Enemies[1].Hp);
        Assert.AreEqual(20, engine.Enemies[0].Ult, "死亡单位不继续受击回能");
    }

    [Test]
    public void FullHealthHealStillExecutesSpecial_AndGrantsActionEnergy()
    {
        var hero = AutoBattleTestData.Hero();
        AutoBattleTestData.SetSkill(hero, AutoBattleTestData.Skill("special",
            AutoBattleTestData.Effect("heal", "allyLowestHp", multiplier: 0, hits: 0, percent: 25)));
        var enemy = AutoBattleTestData.Enemy(speed: 1);
        var engine = AutoBattleTestData.Engine(heroes: new[] { hero }, enemies: new[] { enemy });
        engine.StartBattle();
        BattleTestDriver.NextAction(engine, "hero");
        engine.Ctx.StatesOf(engine.Enemies[0].Id).Apply(StatusType.Stun, 0, 2);
        engine.Players[0].Heal(engine.Players[0].MaxHp);
        var result = BattleTestDriver.NextAction(engine, "hero");
        Assert.AreEqual("special", result.ActionType);
        Assert.IsEmpty(result.Hits);
        Assert.AreEqual(100, engine.Players[0].Ult);
        Assert.AreEqual(engine.Players[0].MaxHp, engine.Players[0].Hp);
        Assert.AreEqual(40, result.Charges.Single(c => c.UnitId == "hero").Amount);
    }

    [Test]
    public void HealUsesTargetsMaximumHp_CapsAtFull_AndIncludesSelf()
    {
        var hero = AutoBattleTestData.Hero(hp: 200);
        AutoBattleTestData.SetSkill(hero, AutoBattleTestData.Skill("special",
            AutoBattleTestData.Effect("heal", "allyLowestHp", multiplier: 0, hits: 0, percent: 25)));
        var engine = AutoBattleTestData.Engine(heroes: new[] { hero });
        engine.StartBattle();
        BattleTestDriver.NextAction(engine, "hero");
        engine.Players[0].Hp = 170;
        var result = BattleTestDriver.NextAction(engine, "hero");
        var change = result.UnitChanges.Single(c => c.UnitId == "hero");
        Assert.AreEqual(200, change.After.Hp);
        Assert.Greater(change.HpDelta, 0);
        Assert.LessOrEqual(change.HpDelta, 50);
    }

    [Test]
    public void ActiveEnergyEffect_RecordsSkillSourceRatherThanPassive()
    {
        var hero = AutoBattleTestData.Hero();
        AutoBattleTestData.SetSkill(hero, AutoBattleTestData.Skill("special",
            AutoBattleTestData.Effect("energy", "self", multiplier: 0, hits: 0, amount: 15)));
        var engine = AutoBattleTestData.Engine(heroes: new[] { hero });
        engine.StartBattle();
        BattleTestDriver.NextAction(engine, "hero");
        engine.Ctx.StatesOf(engine.Enemies[0].Id).Apply(StatusType.Stun, 0, 2);
        var result = BattleTestDriver.NextAction(engine, "hero");
        Assert.AreEqual("special", result.ActionType);
        Assert.AreEqual(15, result.Charges.Single(c => c.Trigger == "skill").Amount);
        Assert.IsFalse(result.Charges.Any(c => c.Trigger == "passive"));
        Assert.IsTrue(result.Charges.Any(c => c.Trigger == "action"));
        Assert.AreEqual(100, engine.Players[0].Ult);
    }

    [Test]
    public void BuffSpecialGrantsEnergyWithoutAnyAttackHit()
    {
        var hero = AutoBattleTestData.Hero();
        AutoBattleTestData.SetSkill(hero, AutoBattleTestData.Skill("special",
            AutoBattleTestData.Effect("attackUp", "allyAll", multiplier: 0, hits: 0, percent: 25, duration: 2)));
        var engine = AutoBattleTestData.Engine(heroes: new[] { hero });
        engine.StartBattle();
        BattleTestDriver.NextAction(engine, "hero");
        var result = BattleTestDriver.NextAction(engine, "hero");
        Assert.IsEmpty(result.Hits);
        Assert.AreEqual(25, engine.Ctx.StatesOf("hero").StrongestPercent(StatusType.AttackUp));
        Assert.AreEqual(2, engine.Ctx.GetStatuses("hero").Single(s => s.Type == StatusType.AttackUp).Duration);
        Assert.AreEqual(100, engine.Players[0].Ult);
    }
}
