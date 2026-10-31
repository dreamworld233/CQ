using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;
using NUnit.Framework;

public class BattleStatisticsTests
{
    [Test]
    public void GroupMultiHitUsesActualDamage_ClampsOverkill_AndRetainsDeadUnits()
    {
        var hero = AutoBattleTestData.Hero(attack: 10);
        AutoBattleTestData.SetSkill(hero, AutoBattleTestData.Skill("basic",
            AutoBattleTestData.Effect(target: "enemyAll", hits: 3)));
        var engine = AutoBattleTestData.Engine(heroes: new[] { hero }, enemies: new[]
        {
            AutoBattleTestData.Enemy("a", "front1", hp: 25),
            AutoBattleTestData.Enemy("b", "front2", hp: 7)
        });
        engine.StartBattle();
        var result = BattleTestDriver.NextAction(engine, "hero");
        Assert.AreEqual(4, result.Hits.Count);
        Assert.AreEqual(32, result.Hits.Sum(hit => hit.HpDamage));
        var rows = BattleStatistics.Summarize(engine);
        Assert.AreEqual(3, rows.Count);
        Assert.AreEqual(32L, rows.Single(row => row.UnitId == "hero").DamageDealt);
        Assert.AreEqual(25L, rows.Single(row => row.UnitId == engine.Enemies[0].Id).DamageTaken);
        Assert.AreEqual(7L, rows.Single(row => row.UnitId == engine.Enemies[1].Id).DamageTaken);
        Assert.IsTrue(engine.Enemies.All(unit => unit.IsDead));
        Assert.IsTrue(rows.Where(row => row.Team == Team.Enemy).All(row => row.DamageDealt == 0));
    }

    [Test]
    public void RepeatedSelfHealRecordsEachAppliedAmount_NotOverhealOrFullHealthAttempts()
    {
        var engine = AutoBattleTestData.Engine(heroes: new[] { AutoBattleTestData.Hero(hp: 100) });
        var actor = engine.Players[0];
        actor.Hp = 70;
        var skill = AutoBattleTestData.Skill("basic", Heal("self", 25), Heal("self", 25));
        var result = Resolve(engine, actor, skill);
        CollectionAssert.AreEqual(new[] { 25, 5 }, result.Heals.Select(heal => heal.HpRestored));
        Assert.IsTrue(result.Heals.All(heal => heal.SourceUnitId == actor.Id && heal.TargetUnitId == actor.Id));
        Assert.AreEqual(30L, BattleStatistics.Summarize(engine).Single(row => row.UnitId == actor.Id).HealingDone);
        var fullHealth = Resolve(engine, actor, skill);
        Assert.IsEmpty(fullHealth.Heals);
        Assert.AreEqual(30L, BattleStatistics.Summarize(engine).Single(row => row.UnitId == actor.Id).HealingDone);
        Assert.AreEqual(2, result.Heals.Count, "后续动作不能改变已有治疗事实快照");
    }

    [Test]
    public void MixedHealingAndDamageOnSameTarget_AreNotLostToNetHpDelta()
    {
        var engine = AutoBattleTestData.Engine(heroes: new[] { AutoBattleTestData.Hero(attack: 25, hp: 100) });
        var actor = engine.Players[0];
        actor.Hp = 50;
        var result = Resolve(engine, actor, AutoBattleTestData.Skill("basic", Heal("self", 25),
            AutoBattleTestData.Effect(target: "self")));
        Assert.AreEqual(0, result.UnitChanges.Single(change => change.UnitId == actor.Id).HpDelta);
        Assert.AreEqual(25, result.Heals.Single().HpRestored);
        Assert.AreEqual(25, result.Hits.Single().HpDamage);
        var row = BattleStatistics.Summarize(engine).Single(item => item.UnitId == actor.Id);
        Assert.AreEqual(25L, row.HealingDone);
        Assert.AreEqual(25L, row.DamageDealt);
        Assert.AreEqual(25L, row.DamageTaken);
    }

    [Test]
    public void HealingAnotherAllyIsAttributedToCaster_NotRecipient()
    {
        var engine = AutoBattleTestData.Engine(heroes: new[]
        {
            AutoBattleTestData.Hero("healer", "front1", hp: 100),
            AutoBattleTestData.Hero("ally", "back1", hp: 200)
        });
        engine.Players[1].Hp = 20;
        var result = Resolve(engine, engine.Players[0], AutoBattleTestData.Skill("basic", Heal("allyLowestHp", 25)));
        Assert.AreEqual("healer", result.Heals.Single().SourceUnitId);
        Assert.AreEqual("ally", result.Heals.Single().TargetUnitId);
        Assert.AreEqual(50, result.Heals.Single().HpRestored);
        var rows = BattleStatistics.Summarize(engine);
        Assert.AreEqual(50L, rows.Single(row => row.UnitId == "healer").HealingDone);
        Assert.AreEqual(0L, rows.Single(row => row.UnitId == "ally").HealingDone);
    }

    [Test]
    public void EnemyDamageAndSelfHealingAreKeptSeparateFromPlayerContributions()
    {
        var engine = AutoBattleTestData.Engine(enemies: new[] { AutoBattleTestData.Enemy(attack: 10, hp: 100) });
        var enemy = engine.Enemies[0];
        enemy.Hp = 70;
        Resolve(engine, enemy, AutoBattleTestData.Skill("basic", AutoBattleTestData.Effect(target: "enemyAll"), Heal("self", 25)));
        var rows = BattleStatistics.Summarize(engine);
        var enemyRow = rows.Single(row => row.UnitId == enemy.Id);
        Assert.AreEqual(Team.Enemy, enemyRow.Team);
        Assert.AreEqual(enemy.Name, enemyRow.Name);
        Assert.AreEqual(10L, enemyRow.DamageDealt);
        Assert.AreEqual(25L, enemyRow.HealingDone);
        Assert.AreEqual(10L, rows.Single(row => row.Team == Team.Player).DamageTaken);
        Assert.AreEqual(0L, rows.Single(row => row.Team == Team.Player).HealingDone);
    }

    [Test]
    public void EnergyPassiveDoesNotCountAsHealingOrDamage_AndZeroActionRosterIsIncluded()
    {
        var hero = AutoBattleTestData.Hero();
        AutoBattleTestData.SetSkill(hero, AutoBattleTestData.Skill("passive",
            AutoBattleTestData.Effect("energy", "self", multiplier: 0, hits: 0, amount: 10)));
        var engine = AutoBattleTestData.Engine(heroes: new[] { hero });
        engine.StartBattle();
        var passive = BattleTestDriver.Step(engine);
        Assert.AreEqual(BattleEventKind.Passive, passive.Kind);
        Assert.IsNotEmpty(passive.Charges);
        Assert.IsEmpty(passive.Heals);
        var rows = BattleStatistics.Summarize(engine);
        Assert.AreEqual(2, rows.Count);
        Assert.IsTrue(rows.All(row => row.DamageDealt == 0 && row.DamageTaken == 0 && row.HealingDone == 0));
    }

    [Test]
    public void SummariesAreIndependentSnapshots_RepeatedReadsDoNotDoubleCount()
    {
        var engine = AutoBattleTestData.Engine();
        Resolve(engine, engine.Players[0], AutoBattleTestData.Skill("basic", AutoBattleTestData.Effect()));
        var first = BattleStatistics.Summarize(engine);
        var second = BattleStatistics.Summarize(engine);
        CollectionAssert.AreEqual(first.Select(row => row.DamageDealt), second.Select(row => row.DamageDealt));
        Assert.AreNotSame(first[0], second[0]);
        Resolve(engine, engine.Players[0], AutoBattleTestData.Skill("basic", AutoBattleTestData.Effect()));
        Assert.AreEqual(10L, first[0].DamageDealt);
        Assert.AreEqual(20L, BattleStatistics.Summarize(engine)[0].DamageDealt);
    }

    [Test]
    public void FreshBattleHasNoStatisticsFromPreviousBattle_EvenWithIdenticalUnitIds()
    {
        var previous = AutoBattleTestData.Engine();
        Resolve(previous, previous.Players[0], AutoBattleTestData.Skill("basic", AutoBattleTestData.Effect()));
        var fresh = AutoBattleTestData.Engine();
        var rows = BattleStatistics.Summarize(fresh);
        Assert.AreNotEqual(previous.BattleId, fresh.BattleId);
        Assert.IsTrue(rows.All(row => row.DamageDealt == 0 && row.DamageTaken == 0 && row.HealingDone == 0));
        Assert.AreEqual(10L, BattleStatistics.Summarize(previous)[0].DamageDealt);
    }

    private static SkillEffectSpec Heal(string target, int percent) =>
        AutoBattleTestData.Effect("heal", target, multiplier: 0, hits: 0, percent: percent);

    private static ActionResult Resolve(BattleEngine engine, Unit actor, SkillSpec skill)
    {
        engine.Ctx.BeginAction(actor);
        engine.Ctx.StatesOf(actor.Id).BeginAction();
        engine.Ctx.CycleOf(actor.Id).Begin(false);
        SkillResolver.Resolve(engine.Ctx, actor, skill);
        return engine.Ctx.CompleteAction(skill, BattleEventKind.Skill, skill.type);
    }
}
