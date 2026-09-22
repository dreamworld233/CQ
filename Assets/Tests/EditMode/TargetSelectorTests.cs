using NUnit.Framework;
using System.Collections.Generic;
using CQ.Core.Battle;
using CQ.Core.Combat;

public class TargetSelectorTests
{
    private static Unit U(string id, Team team, int aggro, int hp) =>
        new Unit(id, team) { MaxHp = hp, Hp = hp, AggroWeight = aggro, Row = team == Team.Player ? "front" : "back" };

    [Test]
    public void Front_HighWeight_Picked_More_Than_Back_Across_Seeds()
    {
        int frontPicks = 0;
        int backPicks = 0;

        for (long seed = 1; seed <= 200; seed++)
        {
            var front = U("front", Team.Player, 3, 10);
            var back = U("back", Team.Player, 1, 10);
            var ctx = new CombatContext(new Rng(seed), new List<Unit> { front, back });

            var picked = TargetSelector.PickTarget(ctx, new List<Unit> { front, back });
            if (picked.Id == "front") frontPicks++;
            else backPicks++;
        }

        Assert.Greater(frontPicks, backPicks,
            $"front 计数 {frontPicks} 应大于 back 计数 {backPicks}");
    }

    [Test]
    public void Never_Returns_Dead_Unit()
    {
        var dead = U("dead", Team.Player, 100, 0);   // 高权重但已阵亡
        var alive = U("alive", Team.Player, 1, 10);

        for (long seed = 1; seed <= 50; seed++)
        {
            var ctx = new CombatContext(new Rng(seed), new List<Unit> { dead, alive });
            var picked = TargetSelector.PickTarget(ctx, new List<Unit> { dead, alive });

            Assert.IsNotNull(picked);
            Assert.IsFalse(picked.IsDead);
            Assert.AreEqual("alive", picked.Id);
        }
    }

    [Test]
    public void NonPositive_Weight_Counts_As_One()
    {
        var a = U("a", Team.Player, 0, 10);
        var b = U("b", Team.Player, 0, 10);

        for (long seed = 1; seed <= 50; seed++)
        {
            var ctx = new CombatContext(new Rng(seed), new List<Unit> { a, b });
            var picked = TargetSelector.PickTarget(ctx, new List<Unit> { a, b });

            Assert.IsNotNull(picked, "权重 <= 0 视为 1，应始终可选");
        }
    }
}