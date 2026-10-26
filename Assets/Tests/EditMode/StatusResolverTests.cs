using NUnit.Framework;
using System.Collections.Generic;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Status;

public class StatusResolverTests
{
    private static Unit U(string id, int speed) =>
        new Unit(id, Team.Player) { SlotId = "front1", MaxHp = 100, Hp = 100, BaseSpeed = speed };

    private static Unit E(string id) =>
        new Unit(id, Team.Enemy) { SlotId = "front1", MaxHp = 100, Hp = 100, BaseSpeed = 1 };

    private static CombatContext Ctx(params Unit[] units) => new CombatContext(new Rng(1), units);

    [TestCase(StatusType.Weaken)]
    [TestCase(StatusType.Vulnerable)]
    [TestCase(StatusType.AttackUp)]
    [TestCase(StatusType.Slow)]
    [TestCase(StatusType.Interrupt)]
    [TestCase(StatusType.Shield)]
    public void Reapplication_Only_Refreshes_To_Max_Duration_Without_Stacking(StatusType type)
    {
        var ctx = Ctx(U("u", 10));
        ctx.AddStatus("u", new StatusEffect { Type = type, Percent = 25, Amount = 20, Duration = 2, Turn = "round" });
        ctx.AddStatus("u", new StatusEffect { Type = type, Percent = 50, Amount = 40, Duration = 1, Turn = "round" });
        Assert.AreEqual(1, ctx.GetStatuses("u").Count);
        Assert.AreEqual(2, ctx.FindStatus("u", type).Duration);
        ctx.AddStatus("u", new StatusEffect { Type = type, Percent = 75, Amount = 60, Duration = 3, Turn = "round" });
        var status = ctx.FindStatus("u", type);
        Assert.AreEqual(1, ctx.GetStatuses("u").Count);
        Assert.AreEqual(3, status.Duration);
        Assert.AreEqual(25, status.Percent);
        Assert.AreEqual(20, status.Amount);
    }

    [Test]
    public void Vulnerable_And_Weaken_Coexist_And_Expire_At_Round_End()
    {
        var ctx = Ctx(E("e"));
        foreach (var type in new[] { StatusType.Weaken, StatusType.Vulnerable })
            ctx.AddStatus("e", new StatusEffect { Type = type, Percent = 25, Duration = 1, Turn = "round" });
        Assert.AreEqual(2, ctx.GetStatuses("e").Count);
        StatusResolver.RoundEndTick(ctx);
        Assert.IsEmpty(ctx.GetStatuses("e"));
    }

    [Test]
    public void Shield_Once_SurvivesRoundEnd_RemovedByDamage()
    {
        var a = E("e");
        var u = U("u", 10);
        var ctx = Ctx(a, u);
        ctx.AddStatus("u", new StatusEffect { Type = StatusType.Shield, Turn = "once", Amount = 20 });

        StatusResolver.RoundEndTick(ctx);
        Assert.IsTrue(ctx.HasStatus("u", StatusType.Shield), "once 护盾不随回合衰减");

        Damage.Apply(ctx, new DamageRequest { Amount = 20, SourceUnitId = "e", TargetUnitId = "u" });
        Assert.IsFalse(ctx.HasStatus("u", StatusType.Shield), "Damage.Apply 打空后移除");
    }

    [Test]
    public void Slow_HalvesEffectiveSpeed_Floor()
    {
        var u = U("u", 10);
        var ctx = Ctx(u);
        ctx.AddStatus("u", new StatusEffect { Type = StatusType.Slow, Turn = "round", Percent = 50, Duration = 1 });

        Assert.AreEqual(5, StatusResolver.EffectiveSpeedOfUnit(u, ctx));
    }

    [Test]
    public void Haste_BoostsSpeed_ThenClearedAtRoundEnd()
    {
        var u = U("u", 10);
        var ctx = Ctx(u);
        ctx.SetHaste("u", 50);

        Assert.AreEqual(15, StatusResolver.EffectiveSpeedOfUnit(u, ctx)); // 10 ×1.5 去尾

        StatusResolver.RoundEndTick(ctx);
        Assert.AreEqual(0, ctx.GetHaste("u"), "Haste 回合末清空");
    }

    [Test]
    public void Interrupt_SkipsTurn_ThenClearedAtRoundEnd()
    {
        var u = U("u", 10);
        var ctx = Ctx(u);
        ctx.AddStatus("u", new StatusEffect { Type = StatusType.Interrupt, Turn = "round", Duration = 1 });

        Assert.IsTrue(StatusResolver.ShouldSkipTurn(u, ctx));

        StatusResolver.RoundEndTick(ctx);
        Assert.IsFalse(StatusResolver.ShouldSkipTurn(u, ctx));
    }

    [Test]
    public void Duration_Round_Decrements_ThenRemoves()
    {
        var u = U("u", 10);
        var ctx = Ctx(u);
        ctx.AddStatus("u", new StatusEffect { Type = StatusType.Weaken, Turn = "round", Duration = 1, Percent = 25 });
        ctx.AddStatus("u", new StatusEffect { Type = StatusType.AttackUp, Turn = "round", Duration = 2, Percent = 25 });

        StatusResolver.RoundEndTick(ctx);

        Assert.IsFalse(ctx.HasStatus("u", StatusType.Weaken), "Duration=1 回合末移除");
        var attackUp = ctx.FindStatus("u", StatusType.AttackUp);
        Assert.IsNotNull(attackUp, "Duration=2 回合末仍在");
        Assert.AreEqual(1, attackUp.Duration, "Duration=2 减为 1");
    }
}