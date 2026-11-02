using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Runtime;
using NUnit.Framework;

public class BalanceTests
{
    [TestCase(1, 7)]
    [TestCase(1, 17)]
    [TestCase(1, 37)]
    [TestCase(1, 73)]
    [TestCase(1, 101)]
    [TestCase(2, 7)]
    [TestCase(2, 17)]
    [TestCase(2, 37)]
    [TestCase(2, 73)]
    [TestCase(2, 101)]
    [TestCase(3, 7)]
    [TestCase(3, 17)]
    [TestCase(3, 37)]
    [TestCase(3, 73)]
    [TestCase(3, 101)]
    public void ConfiguredEncounterAutomaticallyTerminates_WithValidHpAndEnergy(int levelId, long seed)
    {
        var chars = ConfigLoader.LoadCharacters();
        var enemies = ConfigLoader.LoadEnemies();
        var levels = ConfigLoader.LoadLevels();
        var formation = ConfigLoader.LoadFormation();
        Assert.IsEmpty(chars.errors);
        Assert.IsEmpty(enemies.errors);
        Assert.IsEmpty(levels.errors);
        Assert.IsEmpty(formation.errors);
        var level = levels.items.Single(l => l.id == levelId);
        var engine = BattleEngine.Create(seed, chars.items, level,
            enemies.items.ToDictionary(e => e.id), formation.items.Single());
        Assert.IsTrue(engine.StartBattle());
        BattleTestDriver.Drain(engine);
        TestContext.WriteLine($"Level {levelId} seed {seed}: {engine.Winner}, rounds={engine.Round}; " +
            string.Join(", ", engine.Ctx.Units.Select(u => $"{u.Id}={u.Hp}/{u.MaxHp}")));
        Assert.IsTrue(engine.IsFinished);
        Assert.IsTrue(engine.Winner == Team.Player || engine.Winner == Team.Enemy);
        Assert.IsTrue(engine.Ctx.Units.All(u => u.Hp >= 0 && u.Hp <= u.MaxHp));
        Assert.IsTrue(engine.Ctx.Units.All(u => u.Ult >= 0 && u.Ult <= u.MaxUlt));
        Assert.IsFalse(engine.IsActionOccupied);
        Assert.IsTrue(engine.ActionResults.Any(r => r.Kind == BattleEventKind.Skill));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ThirdEncounter_AcrossSeedsAndFrontlineChoices_TerminatesWithinRoundLimit(bool guardOnlyFront)
    {
        var chars = ConfigLoader.LoadCharacters().items;
        var enemies = ConfigLoader.LoadEnemies().items.ToDictionary(e => e.id);
        var level = ConfigLoader.LoadLevels().items.Single(l => l.id == 3);
        int wins = 0, timeouts = 0, longest = 0;
        for (long seed = 1; seed <= 100; seed++)
        {
            var engine = BattleEngine.Create(seed, chars, level, enemies);
            if (guardOnlyFront)
                Assert.IsTrue(engine.TryMovePlayer("sword", "back2"));
            engine.StartBattle();
            BattleTestDriver.Drain(engine);
            longest = System.Math.Max(longest, engine.Round);
            if (engine.Winner == Team.Player) wins++;
            Assert.IsTrue(engine.IsFinished);
            Assert.IsFalse(engine.IsActionOccupied);
            Assert.LessOrEqual(engine.Round, TurnManager.RoundLimit, $"seed={seed}, guardOnlyFront={guardOnlyFront}");
            if (engine.RoundLimitReached)
            {
                timeouts++;
                Assert.AreEqual(TurnManager.RoundLimit, engine.Round);
                Assert.AreEqual(Team.Enemy, engine.Winner);
                Assert.IsTrue(engine.Players.Any(unit => !unit.IsDead));
                Assert.IsTrue(engine.Enemies.Any(unit => !unit.IsDead));
            }
            else Assert.AreEqual(Team.Player, engine.Winner, $"非超时失败 seed={seed}");
        }
        Assert.AreEqual(100, wins + timeouts, "原样本胜局允许因新上限转为超时，不应出现未解释的全灭");
        if (guardOnlyFront) Assert.AreEqual(100, wins, "保留原本无长局阵型的胜率基线");
        TestContext.WriteLine($"guardOnlyFront={guardOnlyFront}: wins={wins}/100, timeouts={timeouts}/100, longest={longest}");
    }

    [Test]
    public void BothSidesReceiveNormalActionOpportunities_AndUseSpecialAndUltimate()
    {
        var engine = AutoBattleTestData.Engine();
        engine.StartBattle();
        for (int i = 0; i < 40; i++) BattleTestDriver.Step(engine);
        foreach (var unit in engine.Ctx.Units)
        {
            var types = engine.ActionResults.Where(r => r.ActorId == unit.Id).Select(r => r.ActionType).ToArray();
            CollectionAssert.IsSubsetOf(new[] { "basic", "special", "ult" }, types);
            Assert.GreaterOrEqual(engine.Ctx.CycleOf(unit.Id).Opportunities, 3);
        }
    }
}
