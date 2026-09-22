using NUnit.Framework;
using System.Collections.Generic;
using CQ.Core.Battle;
using CQ.Core.Combat;

public class DamageTests
{
    private static Unit U(string id, Team team, int hp) => new Unit(id, team) { MaxHp = hp, Hp = hp };

    private static CombatContext Ctx(int targetHp)
    {
        var rng = new Rng(1);
        var units = new List<Unit> { U("src", Team.Player, 100), U("dst", Team.Enemy, targetHp) };
        return new CombatContext(rng, units);
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