using NUnit.Framework;
using System.Collections.Generic;
using CQ.Core.Battle;
using CQ.Core.Combat;

public class DamageTests
{
    private static Unit U(string id, Team team, int hp) => new Unit(id, team) { SlotId = "front1", MaxHp = hp, Hp = hp };

    private static CombatContext Ctx(int targetHp)
    {
        var rng = new Rng(1);
        var units = new List<Unit> { U("src", Team.Player, 100), U("dst", Team.Enemy, targetHp) };
        return new CombatContext(rng, units);
    }

    [Test]
    public void Vulnerable_Applies_After_Source_Modifiers_And_Before_Shield()
    {
        var ctx = Ctx(100);
        ctx.AddStatus("src", new StatusEffect { Type = StatusType.AttackUp, Percent = 30 });
        ctx.AddStatus("src", new StatusEffect { Type = StatusType.Weaken, Percent = 25 });
        ctx.AddStatus("dst", new StatusEffect { Type = StatusType.Vulnerable, Percent = 50 });
        ctx.AddStatus("dst", new StatusEffect { Type = StatusType.Shield, Amount = 20 });
        var result = Damage.Apply(ctx, new DamageRequest { Amount = 66, SourceUnitId = "src", TargetUnitId = "dst" });
        Assert.AreEqual(85, result.AfterAttackUp);
        Assert.AreEqual(63, result.AfterWeaken);
        Assert.AreEqual(94, result.AfterVulnerable);
        Assert.AreEqual(20, result.ShieldAbsorbed);
        Assert.AreEqual(74, result.FinalDamage);
        Assert.AreEqual(26, ctx.GetUnit("dst").Hp);
    }

    [Test]
    public void Card_Damage_Does_Not_Borrow_A_Characters_Source_Modifiers()
    {
        var ctx = Ctx(100);
        ctx.AddStatus("src", new StatusEffect { Type = StatusType.AttackUp, Percent = 100 });
        ctx.AddStatus("src", new StatusEffect { Type = StatusType.Weaken, Percent = 25 });
        ctx.AddStatus("dst", new StatusEffect { Type = StatusType.Vulnerable, Percent = 50 });
        var request = new DamageRequest { Amount = 20, SourceUnitId = BattleEngine.CardSourceId, TargetUnitId = "dst" };
        Assert.AreEqual(0, Damage.Apply(ctx, request).FinalDamage);
        var result = Damage.ApplyCard(ctx, request);
        Assert.AreEqual(20, result.AfterWeaken);
        Assert.AreEqual(30, result.AfterVulnerable);
        Assert.AreEqual(70, ctx.GetUnit("dst").Hp);
    }

    [Test]
    public void Weaken_Floors_66_075_49()
    {
        var ctx = Ctx(100);
        ctx.AddStatus("src", new StatusEffect { Type = StatusType.Weaken, Percent = 25 });
        var r = Damage.Apply(ctx, new DamageRequest { Amount = 66, SourceUnitId = "src", TargetUnitId = "dst" });

        Assert.AreEqual(49, r.AfterWeaken);
        Assert.AreEqual(49, r.FinalDamage);
    }

    [Test]
    public void Pipeline_Order_AttackUp_Weaken_Shield()
    {
        var ctx = Ctx(100);
        ctx.AddStatus("src", new StatusEffect { Type = StatusType.AttackUp, Percent = 30 });
        ctx.AddStatus("src", new StatusEffect { Type = StatusType.Weaken, Percent = 25 });
        ctx.AddStatus("dst", new StatusEffect { Type = StatusType.Shield, Amount = 20 });

        // 66 ×1.3 = 85.8 → 85 → ×0.75 = 63.75 → 63 → 护盾 -20 → 43
        var r = Damage.Apply(ctx, new DamageRequest { Amount = 66, SourceUnitId = "src", TargetUnitId = "dst" });

        Assert.AreEqual(85, r.AfterAttackUp);
        Assert.AreEqual(63, r.AfterWeaken);
        Assert.AreEqual(20, r.ShieldAbsorbed);
        Assert.AreEqual(43, r.FinalDamage);
        Assert.AreEqual(57, ctx.GetUnit("dst").Hp);
    }

    [Test]
    public void Shield_Absorbs_Once_Then_Removed()
    {
        var ctx = Ctx(100);
        ctx.AddStatus("dst", new StatusEffect { Type = StatusType.Shield, Amount = 20 });

        Damage.Apply(ctx, new DamageRequest { Amount = 10, SourceUnitId = "src", TargetUnitId = "dst" });
        Assert.IsTrue(ctx.HasStatus("dst", StatusType.Shield), "未破盾应保留");

        Damage.Apply(ctx, new DamageRequest { Amount = 15, SourceUnitId = "src", TargetUnitId = "dst" });
        Assert.IsFalse(ctx.HasStatus("dst", StatusType.Shield), "护盾挡到 0 应移除");
        Assert.AreEqual(95, ctx.GetUnit("dst").Hp); // 100 - 10(全吸) - 5(15-10) = 95
    }
}