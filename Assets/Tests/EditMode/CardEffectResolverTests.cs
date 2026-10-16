using NUnit.Framework;
using System.Collections.Generic;
using CQ.Core.Battle;
using CQ.Core.Cards;
using CQ.Core.Combat;
using CQ.Core.Config;

// 八种卡牌效果；count 仅参与组牌，单位/格子入口约束由引擎测试覆盖。
public class CardEffectResolverTests
{
    private static CombatContext Ctx()
    {
        var rng = new Rng(1);
        var units = new List<Unit>
        {
            new Unit("source", Team.Player) { SlotId = "front1", MaxHp = 100, Hp = 100 },
            new Unit("target", Team.Enemy) { SlotId = "front1", MaxHp = 100, Hp = 100 }
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
        CardEffectResolver.Resolve(Fx("shield", 0, 40), ctx, "source", "source");

        var fx = ctx.FindStatus("source", StatusType.Shield);
        Assert.IsNotNull(fx);
        Assert.AreEqual(40, fx.Amount);
        Assert.AreEqual("once", fx.Turn);
    }

    [Test]
    public void AttackUp_Sets_Percent_From_Config()
    {
        var ctx = Ctx();
        CardEffectResolver.Resolve(Fx("attackUp", 20, 0), ctx, "source", "source");

        Assert.IsTrue(ctx.HasStatus("source", StatusType.AttackUp));
        Assert.AreEqual(20, ctx.FindStatus("source", StatusType.AttackUp).Percent);
    }

    [Test]
    public void Haste_Sets_Haste_From_Config()
    {
        var ctx = Ctx();
        CardEffectResolver.Resolve(Fx("haste", 15, 0), ctx, "source", "source");

        Assert.AreEqual(15, ctx.GetHaste("source"));
    }

    [TestCase("tauntUp", 35)]
    [TestCase("tauntDown", -35)]
    public void Slot_Effect_Uses_Positive_Amount_And_Configured_Duration(string type, int delta)
    {
        var ctx = new CombatContext(new Rng(1), new[]
        {
            new Unit("p", Team.Player) { SlotId = "front1", MaxHp = 100, Hp = 100 },
            new Unit("e", Team.Enemy) { SlotId = "front1", MaxHp = 100, Hp = 100 }
        }, new FormationConfig { frontBaseTaunt = 100, backBaseTaunt = 50 });
        foreach (var team in new[] { Team.Player, Team.Enemy })
        {
            var effect = new CardEffectSpec { type = type, amount = 35, duration = 2 };
            Assert.IsTrue(CardEffectResolver.IsSlotEffect(effect));
            CardEffectResolver.ResolveSlot(effect, ctx, team, "front1");
            var slot = ctx.FormationOf(team).GetSlot("front1");
            Assert.AreEqual(100 + delta, slot.EffectiveTaunt);
            slot.Tick();
            Assert.AreEqual(100 + delta, slot.EffectiveTaunt);
            slot.Tick();
            Assert.AreEqual(100, slot.EffectiveTaunt);
        }
    }
}