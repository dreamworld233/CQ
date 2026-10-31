using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Combat;
using NUnit.Framework;

public class BattleEventTests
{
    [Test]
    public void RoundStartPassivesFollowTeamThenFormationOrder_NotRosterOrderOrSpeed()
    {
        var engine = AutoBattleTestData.Engine(heroes: new[]
        {
            AutoBattleTestData.Hero("back", "back3", speed: 100),
            AutoBattleTestData.Hero("front2", "front2", speed: 20),
            AutoBattleTestData.Hero("front1", "front1", speed: 1)
        }, enemies: new[]
        {
            AutoBattleTestData.Enemy("enemyBack", "back1", speed: 200),
            AutoBattleTestData.Enemy("enemyFront", "front1", speed: 200)
        });
        engine.StartBattle();
        var passives = Enumerable.Range(0, 5).Select(_ => BattleTestDriver.Step(engine)).ToArray();
        Assert.IsTrue(passives.All(r => r.Kind == BattleEventKind.Passive));
        CollectionAssert.AreEqual(new[] { "front1", "front2", "back", engine.Enemies[1].Id, engine.Enemies[0].Id },
            passives.Select(r => r.ActorId));
        Assert.IsTrue(engine.Ctx.Units.All(u => engine.Ctx.CycleOf(u.Id).Opportunities == 0),
            "被动不消耗个人行动机会");
    }

    [Test]
    public void MultipleEnergyPassivesObserveUpdatedEnergyFromPreviousPassive()
    {
        var front = AutoBattleTestData.Hero("front", "front1");
        var back = AutoBattleTestData.Hero("back", "back1");
        foreach (var config in new[] { front, back })
            AutoBattleTestData.SetSkill(config, AutoBattleTestData.Skill("passive",
                AutoBattleTestData.Effect("energy", "allyLowestEnergy", multiplier: 0, hits: 0, amount: 10)));
        var engine = AutoBattleTestData.Engine(heroes: new[] { back, front });
        engine.Ctx.GetUnit("front").Ult = 0;
        engine.Ctx.GetUnit("back").Ult = 5;
        engine.StartBattle();
        var first = BattleTestDriver.Step(engine);
        Assert.AreEqual("front", first.ActorId);
        Assert.AreEqual(10, engine.Ctx.GetUnit("front").Ult);
        Assert.AreEqual(5, engine.Ctx.GetUnit("back").Ult);
        var second = BattleTestDriver.Step(engine);
        Assert.AreEqual("back", second.ActorId);
        Assert.AreEqual(10, engine.Ctx.GetUnit("front").Ult);
        Assert.AreEqual(15, engine.Ctx.GetUnit("back").Ult);
        Assert.IsTrue(first.Charges.All(c => c.Amount == 10));
        Assert.IsTrue(second.Charges.All(c => c.Amount == 10));
    }

    [Test]
    public void StunDoesNotPreventRoundStartPassive_OrConsumeStatusBeforeOwnAction()
    {
        var hero = AutoBattleTestData.Hero();
        AutoBattleTestData.SetSkill(hero, AutoBattleTestData.Skill("passive",
            AutoBattleTestData.Effect("energy", "self", multiplier: 0, hits: 0, amount: 10)));
        var engine = AutoBattleTestData.Engine(heroes: new[] { hero });
        engine.Ctx.StatesOf("hero").Apply(StatusType.Stun, 0, 1);
        engine.StartBattle();
        var passive = BattleTestDriver.Step(engine);
        Assert.AreEqual(BattleEventKind.Passive, passive.Kind);
        Assert.AreEqual(30, engine.Players[0].Ult);
        Assert.AreEqual(1, engine.Ctx.GetStatuses("hero").Single(s => s.Type == StatusType.Stun).Duration);
        var skipped = BattleTestDriver.NextAction(engine, "hero");
        Assert.AreEqual(BattleEventKind.Skipped, skipped.Kind);
        Assert.AreEqual(30, engine.Players[0].Ult);
        Assert.IsFalse(engine.Ctx.StatesOf("hero").Has(StatusType.Stun));
    }

    [Test]
    public void ActionChangesMatchActualHpDeltas_AndIdentifySkillForBothTeams()
    {
        var engine = AutoBattleTestData.Engine();
        engine.StartBattle();
        for (int i = 0; i < 12; i++)
        {
            var before = engine.Ctx.Units.ToDictionary(u => u.Id, u => u.Hp);
            var result = BattleTestDriver.Step(engine);
            Assert.IsNotEmpty(result.ActorId);
            Assert.IsNotEmpty(result.ActionId);
            Assert.IsNotEmpty(result.ActionName);
            foreach (var change in result.UnitChanges)
                Assert.AreEqual(engine.Ctx.GetUnit(change.UnitId).Hp - before[change.UnitId], change.HpDelta);
            foreach (var hit in result.Hits)
                Assert.AreEqual(result.ActorId, hit.SourceUnitId);
        }
        Assert.IsTrue(engine.ActionResults.Any(r => r.ActorId == engine.Enemies[0].Id && r.Kind == BattleEventKind.Skill));
    }
}
