using NUnit.Framework;
using System.Collections.Generic;
using CQ.Core.Battle;
using CQ.Core.Cards;
using CQ.Core.Combat;
using CQ.Core.Config;

// 卡牌效果解析测试。6 个独特效果各触发一次。
// 说明：8 张卡 = 6 独特效果 × count 合计 8。count 只是卡组数据，不参与 Resolve。
public class CardEffectResolverTests
{
    private static CombatContext Ctx()
    {
        var rng = new Rng(1);
        var units = new List<Unit>
        {
            new Unit("source", Team.Player),
            new Unit("target", Team.Enemy)
        };
        return new CombatContext(rng, units);
    }

    private static CardEffectSpec Fx(string type, int percent, int amount) =>
        new CardEffectSpec { type = type, percent = percent, amount = amount };

    [Test]
    public void Interrupt_Adds_InterruptStatus()
    {
        var ctx = Ctx();
        CardEffectResolver.Resolve(Fx("interrupt", 0, 0), ctx, "target", "source");

        var fx = ctx.FindStatus("target", StatusType.Interrupt);
        Assert.IsNotNull(fx);
        Assert.AreEqual(StatusType.Interrupt, fx.Type);
        Assert.AreEqual(1, fx.Duration);
        Assert.AreEqual("round", fx.Turn);
    }

    [Test]
    public void Weaken_Sets_Percent_From_Config()
    {
        var ctx = Ctx();
        CardEffectResolver.Resolve(Fx("weaken", 25, 0), ctx, "target", "source");

        Assert.IsTrue(ctx.HasStatus("target", StatusType.Weaken));
        Assert.AreEqual(25, ctx.FindStatus("target", StatusType.Weaken).Percent);
    }

    [Test]
    public void Slow_Sets_Percent_From_Config()
    {
        var ctx = Ctx();
        CardEffectResolver.Resolve(Fx("slow", 30, 0), ctx, "target", "source");

        Assert.IsTrue(ctx.HasStatus("target", StatusType.Slow));
        Assert.AreEqual(30, ctx.FindStatus("target", StatusType.Slow).Percent);
    }

    [Test]
    public void Shield_Sets_Amount_From_Config_And_Turn_Once()
    {
        var ctx = Ctx();
        CardEffectResolver.Resolve(Fx("shield", 0, 40), ctx, "target", "source");

        var fx = ctx.FindStatus("target", StatusType.Shield);
        Assert.IsNotNull(fx);
        Assert.AreEqual(40, fx.Amount);
        Assert.AreEqual("once", fx.Turn);
    }

    [Test]
    public void AttackUp_Sets_Percent_From_Config()
    {
        var ctx = Ctx();
        CardEffectResolver.Resolve(Fx("attackUp", 20, 0), ctx, "target", "source");

        Assert.IsTrue(ctx.HasStatus("target", StatusType.AttackUp));
        Assert.AreEqual(20, ctx.FindStatus("target", StatusType.AttackUp).Percent);
    }

    [Test]
    public void Haste_Sets_Haste_From_Config()
    {
        var ctx = Ctx();
        CardEffectResolver.Resolve(Fx("haste", 15, 0), ctx, "target", "source");

        Assert.AreEqual(15, ctx.GetHaste("target"));
    }
}