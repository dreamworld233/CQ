using System.Linq;
using CQ.Core.Combat;
using NUnit.Framework;

public class TargetSelectorTests
{
    [TestCase("enemyFront", "front1", "back1")]
    [TestCase("enemyBack", "back1", "front1")]
    public void PreferredRowIsStrict_AndFallsBackOnlyWhenPreferredRowHasNoLivingTargets(
        string selector, string preferred, string fallback)
    {
        var engine = AutoBattleTestData.Engine(enemies: new[]
        {
            AutoBattleTestData.Enemy("preferred", preferred), AutoBattleTestData.Enemy("fallback", fallback)
        });
        var actor = engine.Players[0];
        for (int i = 0; i < 20; i++)
            Assert.AreSame(engine.Enemies[0], TargetSelector.Select(engine.Ctx, actor, selector, 1).Single());
        engine.Enemies[0].TakeDamage(engine.Enemies[0].Hp);
        Assert.AreSame(engine.Enemies[1], TargetSelector.Select(engine.Ctx, actor, selector, 1).Single());
        engine.Enemies[1].TakeDamage(engine.Enemies[1].Hp);
        Assert.IsEmpty(TargetSelector.Select(engine.Ctx, actor, selector, 1));
    }

    [Test]
    public void RandomMultipleTargetsAreUnique_AndUseAllAvailableWhenCountIsLarger()
    {
        var engine = AutoBattleTestData.Engine(enemies: new[]
        {
            AutoBattleTestData.Enemy("a", "front1"), AutoBattleTestData.Enemy("b", "front2"),
            AutoBattleTestData.Enemy("c", "back1")
        });
        var actor = engine.Players[0];
        for (int i = 0; i < 20; i++)
        {
            var two = TargetSelector.Select(engine.Ctx, actor, "enemyRandom", 2);
            Assert.AreEqual(2, two.Count);
            Assert.AreEqual(2, two.Select(u => u.Id).Distinct().Count());
        }
        engine.Enemies[1].TakeDamage(engine.Enemies[1].Hp);
        var all = TargetSelector.Select(engine.Ctx, actor, "enemyRandom", 5);
        CollectionAssert.AreEquivalent(new[] { engine.Enemies[0].Id, engine.Enemies[2].Id }, all.Select(u => u.Id));
    }

    [Test]
    public void LowestHealthUsesPercentageNotAbsoluteHp_AndCanSelectSelf()
    {
        var engine = AutoBattleTestData.Engine(heroes: new[]
        {
            AutoBattleTestData.Hero("large", "front1", hp: 1000),
            AutoBattleTestData.Hero("small", "back1", hp: 100)
        });
        var large = engine.Players[0];
        var small = engine.Players[1];
        large.Hp = 300;
        small.Hp = 50;
        Assert.AreSame(large, TargetSelector.Select(engine.Ctx, large, "allyLowestHp", 1).Single());
        large.Hp = large.MaxHp;
        small.Hp = small.MaxHp;
        Assert.AreEqual(1, TargetSelector.Select(engine.Ctx, large, "allyLowestHp", 1).Count,
            "全员满血仍需选择治疗目标");
    }

    [Test]
    public void AllySelectorsIncludeSelf_AndExcludeDeadUnits()
    {
        var engine = AutoBattleTestData.Engine();
        var actor = engine.Players[0];
        foreach (var selector in new[] { "self", "allyRandom", "allyFront", "allyLowestHp", "allyLowestEnergy", "allyAll" })
            Assert.AreSame(actor, TargetSelector.Select(engine.Ctx, actor, selector, 1).Single(), selector);
        actor.TakeDamage(actor.Hp);
        Assert.IsEmpty(TargetSelector.Select(engine.Ctx, engine.Enemies[0], "enemyAll", 5));
    }

    [Test]
    public void TiedCandidatesUseSeededRandomness_NotAlwaysFirstSlot()
    {
        var engine = AutoBattleTestData.Engine(enemies: new[]
        {
            AutoBattleTestData.Enemy("a", "front1"), AutoBattleTestData.Enemy("b", "front2")
        });
        var choices = Enumerable.Range(0, 50)
            .Select(_ => TargetSelector.Select(engine.Ctx, engine.Players[0], "enemyFront", 1).Single().Id).Distinct().ToArray();
        Assert.AreEqual(2, choices.Length);
    }
}
