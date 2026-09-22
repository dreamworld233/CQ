using NUnit.Framework;
using System.Collections.Generic;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;

public class EnemyActionResolverTests
{
    private static Unit P(string id, int hp) => new Unit(id, Team.Player) { MaxHp = hp, Hp = hp, AggroWeight = 1, Row = "front" };
    private static Unit E(string id, int hp) => new Unit(id, Team.Enemy) { MaxHp = hp, Hp = hp };

    private static CombatContext Ctx(Unit en, params Unit[] players)
    {
        var units = new List<Unit> { en };
        units.AddRange(players);
        return new CombatContext(new Rng(1), units);
    }

    private static IntentConfig Attack(int damage) =>
        new IntentConfig { id = "a", type = "attack", target = "single", damage = damage };

    private static IntentConfig AttackAll(int damage) =>
        new IntentConfig { id = "a", type = "attack", target = "all", damage = damage };

    private static IntentConfig Multi(int hits, int damage) =>
        new IntentConfig { id = "m", type = "multi", target = "single", hits = hits, damage = damage };

    private static IntentConfig Buff(int percent) =>
        new IntentConfig { id = "up", type = "attackUp", target = "single", buffPercent = percent };

    private static IntentConfig Charge(int nextDamage, bool interruptible) =>
        new IntentConfig { id = "c", type = "charge", target = "single", nextDamage = nextDamage, interruptible = interruptible };

    [Test]
    public void Attack_Deals_Exact_Damage()
    {
        var e = E("e", 100);
        var p = P("p", 100);
        var ctx = Ctx(e, p);
        var resolver = new EnemyActionResolver();

        resolver.Resolve(ctx, "e", Attack(30));

        Assert.AreEqual(70, p.Hp);
    }

    [Test]
    public void Multi_Hits_Three_Times_Same_Damage()
    {
        var e = E("e", 100);
        var p = P("p", 100);
        var ctx = Ctx(e, p);
        var resolver = new EnemyActionResolver();

        resolver.Resolve(ctx, "e", Multi(3, 10));

        Assert.AreEqual(70, p.Hp); // 100 - 10*3
    }

    [Test]
    public void AttackUp_Buffs_Self_Then_Damage_Scaled_By_Shared_Damage()
    {
        var e = E("e", 100);
        var p = P("p", 100);
        var ctx = Ctx(e, p);
        var resolver = new EnemyActionResolver();

        resolver.Resolve(ctx, "e", Buff(30));

        var fx = ctx.FindStatus("e", StatusType.AttackUp);
        Assert.IsNotNull(fx);
        Assert.AreEqual(30, fx.Percent);
        Assert.AreEqual(1, fx.Duration);
        Assert.AreEqual("round", fx.Turn);

        // 共享口径：40 × 1.3 = 52
        var r = Damage.Apply(ctx, new DamageRequest { Amount = 40, SourceUnitId = "e", TargetUnitId = "p" });
        Assert.AreEqual(52, r.AfterAttackUp);
        Assert.AreEqual(48, p.Hp);
    }

    [Test]
    public void Charge_No_Damage_First_Turn_Releases_Next_Damage_Second_Turn()
    {
        var e = E("e", 100);
        var p = P("p", 100);
        var ctx = Ctx(e, p);
        var resolver = new EnemyActionResolver();

        resolver.Resolve(ctx, "e", Charge(25, true));
        Assert.AreEqual(100, p.Hp, "第 1 回合不应造成伤害");

        resolver.Resolve(ctx, "e", Attack(10));
        Assert.AreEqual(65, p.Hp, "先释放 25 再执行 10 = 100-35");
    }

    [Test]
    public void Interrupted_Charge_Does_Not_Release()
    {
        var e = E("e", 100);
        var p = P("p", 100);
        var ctx = Ctx(e, p);
        var resolver = new EnemyActionResolver();

        resolver.Resolve(ctx, "e", Charge(25, true));
        ctx.AddStatus("e", new StatusEffect { Type = StatusType.Interrupt });

        resolver.Resolve(ctx, "e", Attack(10));
        Assert.AreEqual(90, p.Hp, "蓄力被 interrupt 取消，只受本轮 10 伤害");
    }

    [Test]
    public void Uninterruptible_Charge_Still_Releases()
    {
        var e = E("e", 100);
        var p = P("p", 100);
        var ctx = Ctx(e, p);
        var resolver = new EnemyActionResolver();

        resolver.Resolve(ctx, "e", Charge(25, false));
        ctx.AddStatus("e", new StatusEffect { Type = StatusType.Interrupt });

        resolver.Resolve(ctx, "e", Attack(10));
        Assert.AreEqual(65, p.Hp, "interruptible=false 不被 interrupt 取消，仍释放 25");
    }

    [Test]
    public void Debuff_Applies_Weaken_To_Locked_Player()
    {
        var e = E("e", 100);
        var p = P("p", 100);
        var ctx = Ctx(e, p);
        var resolver = new EnemyActionResolver();

        resolver.Resolve(ctx, "e", new IntentConfig
        {
            id = "d", type = "debuff", target = "single",
            debuffType = "weaken", debuffPercent = 25, debuffDuration = 2
        }, "p");

        var fx = ctx.FindStatus("p", StatusType.Weaken);
        Assert.IsNotNull(fx);
        Assert.AreEqual(25, fx.Percent);
        Assert.AreEqual(2, fx.Duration);
    }
}