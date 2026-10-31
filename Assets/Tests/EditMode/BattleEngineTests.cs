using System;
using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Combat;
using NUnit.Framework;

public class BattleEngineTests
{
    [Test]
    public void PermanentPassives_ApplyToBothTeamsWithoutActionsOrEnergy_AndResetForNewBattle()
    {
        var hero = AutoBattleTestData.Hero(attack: 100);
        var enemy = AutoBattleTestData.Enemy(defense: 20);
        hero.skills[3].trigger = enemy.skills[3].trigger = "permanent";
        hero.skills[3].effects[0] = AutoBattleTestData.Effect("attackUp", "self",
            multiplier: 0, hits: 0, percent: 10);
        enemy.skills[3].effects[0] = AutoBattleTestData.Effect("defenseUp", "self",
            multiplier: 0, hits: 0, percent: 10);
        var engine = AutoBattleTestData.Engine(heroes: new[] { hero }, enemies: new[] { enemy });
        Assert.AreEqual(110m, engine.Ctx.StatesOf(hero.id).EffectiveAttack(engine.Players[0]));
        Assert.AreEqual(22m, engine.Ctx.StatesOf(engine.Enemies[0].Id).EffectiveDefense(engine.Enemies[0]));
        Assert.IsEmpty(engine.ActionResults);
        Assert.AreEqual(20, engine.Players[0].Ult);
        engine.StartBattle();
        var first = BattleTestDriver.Step(engine);
        Assert.AreEqual(BattleEventKind.Skill, first.Kind);
        Assert.AreEqual(88, first.Hits.Single().HpDamage);
        for (int i = 0; i < 12; i++) BattleTestDriver.Step(engine);
        Assert.IsFalse(engine.ActionResults.Any(r => r.Kind == BattleEventKind.Passive));
        engine.Ctx.StatesOf(hero.id).Apply(StatusType.AttackUp, 50, 2);
        var restarted = AutoBattleTestData.Engine(heroes: new[] { hero }, enemies: new[] { enemy });
        Assert.AreEqual(10, restarted.Ctx.StatesOf(hero.id).StrongestPercent(StatusType.AttackUp));
        Assert.AreEqual(100, restarted.Players[0].BaseAttack);
    }

    [Test]
    public void BattleStartsOnce_AndDoesNotExposeManualCombatCommands()
    {
        var engine = AutoBattleTestData.Engine();
        Assert.AreEqual(BattleFlow.Setup, engine.Flow);
        Assert.IsNull(engine.Advance());
        Assert.IsTrue(engine.StartBattle());
        Assert.AreEqual(BattleFlow.Resolving, engine.Flow);
        Assert.IsFalse(engine.StartBattle());
        foreach (var removed in new[] { "RequestUltimate", "PlayCard", "BeginActions", "GetIntentPreview" })
            Assert.IsNull(typeof(BattleEngine).GetMethod(removed), removed);
    }

    [Test]
    public void FirstThreePersonalActions_ShowBasicSpecialUltimate()
    {
        var engine = AutoBattleTestData.Engine();
        engine.StartBattle();
        var kinds = Enumerable.Range(0, 3).Select(_ => BattleTestDriver.NextAction(engine, "hero").ActionType).ToArray();
        CollectionAssert.AreEqual(new[] { "basic", "special", "ult" }, kinds);
        Assert.AreEqual(3, engine.Ctx.CycleOf("hero").Opportunities);
    }

    [Test]
    public void BattleAutomaticallyAdvancesAcrossRounds_WithoutInputWindows()
    {
        var engine = AutoBattleTestData.Engine();
        engine.StartBattle();
        for (int i = 0; i < 4; i++) BattleTestDriver.NextAction(engine, "hero");
        Assert.GreaterOrEqual(engine.CurrentRound, 4);
        Assert.AreEqual(BattleFlow.Resolving, engine.Flow);
        Assert.IsFalse(engine.IsActionOccupied);
    }

    [Test]
    public void OccupiedAction_CannotResolveAgain_AndInvalidConfirmDoesNotReleaseIt()
    {
        var engine = AutoBattleTestData.Engine();
        engine.StartBattle();
        var result = engine.Advance();
        Assert.IsNotNull(result);
        int history = engine.ActionResults.Count;
        var hp = engine.Ctx.Units.Select(u => u.Hp).ToArray();
        Assert.IsNull(engine.Advance());
        Assert.AreEqual(history, engine.ActionResults.Count);
        CollectionAssert.AreEqual(hp, engine.Ctx.Units.Select(u => u.Hp));
        Assert.IsFalse(engine.Confirm(Guid.NewGuid(), result.Sequence));
        Assert.IsFalse(engine.Confirm(engine.BattleId, result.Sequence + 1));
        Assert.AreSame(result, engine.CurrentAction);
        Assert.IsTrue(engine.Confirm(engine.BattleId, result.Sequence));
        Assert.IsFalse(engine.Confirm(engine.BattleId, result.Sequence));
        Assert.IsFalse(engine.IsActionOccupied);
    }

    [Test]
    public void SequenceIsMonotonicAcrossRounds_AndSnapshotsDoNotFollowLiveChanges()
    {
        var engine = AutoBattleTestData.Engine();
        engine.StartBattle();
        var first = BattleTestDriver.Step(engine);
        int hp = first.UnitChanges.First(c => c.UnitId == "hero").After.Hp;
        var statuses = first.UnitChanges.First(c => c.UnitId == "hero").After.Statuses.ToArray();
        engine.Players[0].TakeDamage(5);
        engine.Ctx.StatesOf("hero").Apply(StatusType.AttackUp, 25, 2);
        for (int i = 0; i < 15; i++) BattleTestDriver.Step(engine);
        Assert.AreEqual(hp, first.UnitChanges.First(c => c.UnitId == "hero").After.Hp);
        Assert.AreEqual(statuses.Length, first.UnitChanges.First(c => c.UnitId == "hero").After.Statuses.Count);
        CollectionAssert.AreEqual(Enumerable.Range(1, engine.ActionResults.Count).Select(i => (long)i),
            engine.ActionResults.Select(r => r.Sequence));
    }

    [Test]
    public void LethalAction_FinishesImmediately_ButStillRequiresPresentationConfirm()
    {
        var engine = AutoBattleTestData.Engine(heroes: new[] { AutoBattleTestData.Hero(attack: 100) },
            enemies: new[] { AutoBattleTestData.Enemy(hp: 5) });
        engine.StartBattle();
        ActionResult lethal = null;
        for (int i = 0; i < 20; i++)
        {
            var result = engine.Advance();
            Assert.IsNotNull(result);
            if (engine.IsFinished) { lethal = result; break; }
            Assert.IsTrue(engine.Confirm(engine.BattleId, result.Sequence));
        }
        Assert.IsNotNull(lethal);
        Assert.AreEqual(Team.Player, engine.Winner);
        Assert.AreEqual(BattleFlow.Finished, engine.Flow);
        Assert.IsTrue(engine.IsActionOccupied);
        Assert.IsNull(engine.Advance());
        Assert.IsTrue(engine.Confirm(engine.BattleId, lethal.Sequence));
        Assert.IsFalse(engine.IsActionOccupied);
        Assert.IsNull(engine.Advance());
        Assert.IsFalse(engine.ActionResults.Any(r => r.ActorId == engine.Enemies[0].Id && r.Kind != BattleEventKind.Passive));
    }

    [Test]
    public void EnemyUsesSameAutomaticCycle_AndCanWin()
    {
        var engine = AutoBattleTestData.Engine(heroes: new[] { AutoBattleTestData.Hero(hp: 20, attack: 1, speed: 1) },
            enemies: new[] { AutoBattleTestData.Enemy(attack: 100, speed: 20) });
        engine.StartBattle();
        BattleTestDriver.Drain(engine);
        Assert.AreEqual(Team.Enemy, engine.Winner);
        Assert.AreEqual(1, engine.Ctx.CycleOf(engine.Enemies[0].Id).Opportunities);
    }

    [Test]
    public void FreshBattleResetsEnergyStatusCounters_AndRejectsOldConfirmation()
    {
        var previous = AutoBattleTestData.Engine();
        previous.StartBattle();
        var old = previous.Advance();
        previous.Ctx.StatesOf("hero").Apply(StatusType.Stun, 0, 2);
        previous.Players[0].GainUlt(100);
        var fresh = AutoBattleTestData.Engine();
        Assert.AreNotEqual(previous.BattleId, fresh.BattleId);
        Assert.AreEqual(20, fresh.Players[0].Ult);
        Assert.AreEqual(0, fresh.Ctx.CycleOf("hero").Opportunities);
        Assert.IsEmpty(fresh.Ctx.GetStatuses("hero"));
        fresh.StartBattle();
        var current = fresh.Advance();
        Assert.IsFalse(fresh.Confirm(previous.BattleId, old.Sequence));
        Assert.AreSame(current, fresh.CurrentAction);
        Assert.IsTrue(fresh.Confirm(fresh.BattleId, current.Sequence));
    }

    [Test]
    public void SetupMoveSwapsOnlyOwnUnits_AndIsRejectedAfterStart()
    {
        var engine = AutoBattleTestData.Engine(heroes: new[]
        {
            AutoBattleTestData.Hero("first", "front1"), AutoBattleTestData.Hero("second", "back1")
        });
        Assert.IsTrue(engine.TryMovePlayer("first", "back1"));
        Assert.AreEqual("back1", engine.Players[0].SlotId);
        Assert.AreEqual("front1", engine.Players[1].SlotId);
        Assert.IsFalse(engine.TryMovePlayer(engine.Enemies[0].Id, "back2"));
        Assert.IsFalse(engine.TryMovePlayer("first", "invalid"));
        engine.StartBattle();
        Assert.IsFalse(engine.TryMovePlayer("first", "front2"));
    }

    [Test]
    public void StunSkipsUltimate_ConsumesBuffDuration_AndRetainsEnergy()
    {
        var engine = AutoBattleTestData.Engine();
        engine.Ctx.StatesOf("hero").Apply(StatusType.Stun, 0, 1);
        engine.Ctx.StatesOf("hero").Apply(StatusType.AttackUp, 25, 1);
        engine.Players[0].GainUlt(100);
        engine.StartBattle();
        var skipped = BattleTestDriver.NextAction(engine, "hero");
        Assert.AreEqual(BattleEventKind.Skipped, skipped.Kind);
        Assert.IsEmpty(skipped.Hits);
        Assert.AreEqual(100, engine.Players[0].Ult);
        Assert.IsFalse(engine.Ctx.StatesOf("hero").Has(StatusType.Stun));
        Assert.IsFalse(engine.Ctx.StatesOf("hero").Has(StatusType.AttackUp));
        Assert.AreEqual("ult", BattleTestDriver.NextAction(engine, "hero").ActionType);
    }
}
