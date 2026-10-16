using NUnit.Framework;
using System.Collections.Generic;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;

public class TargetSelectorTests
{
    private static Unit U(string id, string slot, int hp = 10) =>
        new Unit(id, Team.Player) { MaxHp = 10, Hp = hp, SlotId = slot };

    private static FormationConfig Rules() => new FormationConfig { frontBaseTaunt = 100, backBaseTaunt = 50 };

    [Test]
    public void Unique_Maximum_Is_Always_Picked_Not_Weighted()
    {
        for (long seed = 1; seed <= 100; seed++)
        {
            var front = U("front", "front1");
            var back = U("back", "back1");
            var units = new List<Unit> { back, front };
            var ctx = new CombatContext(new Rng(seed), units, Rules());
            Assert.AreSame(front, TargetSelector.PickTarget(ctx, units), "seed=" + seed);
        }
    }

    [Test]
    public void Slot_Modifier_Changes_Maximum_Immediately()
    {
        var front = U("front", "front1");
        var back = U("back", "back1");
        var units = new List<Unit> { front, back };
        var ctx = new CombatContext(new Rng(1), units, Rules());
        Assert.AreSame(front, TargetSelector.PickTarget(ctx, units));
        ctx.FormationOf(Team.Player).GetSlot("back1").AddModifier(60, 1);
        Assert.AreSame(back, TargetSelector.PickTarget(ctx, units));
        ctx.FormationOf(Team.Player).GetSlot("back1").Tick();
        Assert.AreSame(front, TargetSelector.PickTarget(ctx, units));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Ties_And_AllZero_Are_Seeded_And_Only_Select_Living_Maxima(bool allZero)
    {
        var seen = new HashSet<string>();
        for (long seed = 1; seed <= 64; seed++)
        {
            var a = U("a", "front1");
            var b = U("b", "front2");
            var dead = U("dead", "back2", 0);
            var lower = U("lower", "back1");
            var units = new List<Unit> { dead, a, lower, b };
            var config = allZero ? new FormationConfig() : Rules();
            var first = new CombatContext(new Rng(seed), units, config);
            var replay = new CombatContext(new Rng(seed), units, config);
            var candidates = new List<Unit> { dead, a, null, b };
            if (!allZero) candidates.Add(lower);
            for (int i = 0; i < 8; i++)
            {
                var picked = TargetSelector.PickTarget(first, candidates);
                Assert.IsNotNull(picked);
                Assert.That(picked.Id, Is.EqualTo("a").Or.EqualTo("b"));
                Assert.AreEqual(picked.Id, TargetSelector.PickTarget(replay, candidates).Id);
                seen.Add(picked.Id);
            }
        }
        CollectionAssert.AreEquivalent(new[] { "a", "b" }, seen, "Ties must use seeded RNG, not always the first slot.");
    }

    [Test]
    public void Dead_High_Taunt_Is_Excluded_And_Only_Living_Candidate_Is_Returned()
    {
        var dead = U("dead", "front1", 0);
        var alive = U("alive", "back1");
        var units = new List<Unit> { dead, alive };
        var ctx = new CombatContext(new Rng(1), units, Rules());
        ctx.FormationOf(Team.Player).GetSlot("front1").AddModifier(1000, 2);
        Assert.AreSame(alive, TargetSelector.PickTarget(ctx, units));
        alive.TakeDamage(alive.Hp);
        Assert.IsNull(TargetSelector.PickTarget(ctx, units));
    }

    [Test]
    public void Null_Empty_And_AllDead_Candidates_Return_Null()
    {
        var dead = U("dead", "front1", 0);
        var ctx = new CombatContext(new Rng(1), new[] { dead });
        Assert.IsNull(TargetSelector.PickTarget(null, new[] { dead }));
        Assert.IsNull(TargetSelector.PickTarget(ctx, null));
        Assert.IsNull(TargetSelector.PickTarget(ctx, new Unit[0]));
        Assert.IsNull(TargetSelector.PickTarget(ctx, new Unit[] { null, dead }));
    }
}
