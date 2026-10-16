using NUnit.Framework;
using System.Collections.Generic;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;

public class EnemyActionResolverTests
{
    private static Unit P(string id, int hp, string slot = "front1") => new Unit(id, Team.Player) { MaxHp = hp, Hp = hp, SlotId = slot };
    private static Unit E(string id, int hp) => new Unit(id, Team.Enemy) { MaxHp = hp, Hp = hp, SlotId = "front1" };

    private static CombatContext Ctx(Unit en, params Unit[] players)
    {
        var units = new List<Unit> { en };
        units.AddRange(players);
        return new CombatContext(new Rng(1), units, new FormationConfig { frontBaseTaunt = 100, backBaseTaunt = 50 });
    }

    private static IntentConfig Attack(int damage) =>
        new IntentConfig { id = "a", type = "attack", target = "single", damage = damage };

    private static IntentConfig AttackAll(int damage) =>
        new IntentConfig { id = "a", type = "attack", target = "all", damage = damage };

    private static IntentConfig Multi(int hits, int damage) =>
        new IntentConfig { id = "m", type = "multi", target = "single", hits = hits, damage = damage };

    private static IntentConfig Buff(int percent) =>
        new IntentConfig { id = "up", type = "attackUp", target = "single", buffPercent = percent, buffDuration = 1 };

    private static IntentConfig Charge(int nextDamage, bool interruptible) =>
        new IntentConfig { id = "c", type = "charge", target = "single", nextDamage = nextDamage, interruptible = interruptible };

    private static IntentConfig Composite(string type = "multi", string debuff = "weaken") => new IntentConfig
    {
        id = "composite", type = type, target = "single", damage = 11, hits = type == "multi" ? 3 : 0,
        buffPercent = 50, buffDuration = 2, debuffType = debuff, debuffPercent = 25, debuffDuration = 2
    };

    [TestCase("attack", "weaken", 89, 1)]
    [TestCase("multi", "slow", 67, 3)]
    public void Composite_Applies_Effects_Once_After_All_Hits(string type, string debuff, int hp, int hits)
    {
        var p = P("p", 100);
        var ctx = Ctx(E("e", 100), p);
        new EnemyActionResolver().Resolve(ctx, "e", Composite(type, debuff));
        Assert.AreEqual(hp, p.Hp, "本次任何一段都不应受后置攻增影响");
        Assert.AreEqual(1, ctx.GetStatuses("e").Count);
        Assert.AreEqual(1, ctx.GetStatuses("p").Count);
        var fx = ctx.FindStatus("p", StatusTypes.Parse(debuff));
        Assert.AreEqual(25, fx.Percent);
        Assert.AreEqual(2, fx.Duration);
        Assert.AreEqual("e", fx.SourceUnitId);
        Assert.AreEqual(hits, ctx.ActionResults[0].Hits.Count);
        Assert.AreEqual(1, ctx.ActionResults.Count, "复合效果不拆动作边界");
    }

    [Test]
    public void Composite_Shielded_Hit_Still_Debuffs_Without_Reselecting()
    {
        var p = P("p", 100);
        var other = P("other", 100, "back1");
        var ctx = Ctx(E("e", 100), p, other);
        ctx.AddStatus("p", new StatusEffect { Type = StatusType.Shield, Amount = 100, Turn = "once" });
        new EnemyActionResolver().Resolve(ctx, "e", Composite());
        Assert.AreEqual(100, p.Hp);
        Assert.AreEqual(67, ctx.FindStatus("p", StatusType.Shield).Amount);
        Assert.IsNotNull(ctx.FindStatus("p", StatusType.Weaken));
        Assert.IsNull(ctx.FindStatus("other", StatusType.Weaken));
        Assert.AreEqual(3, ctx.ActionResults[0].Hits.Count);
    }

    [Test]
    public void Composite_Multi_Kill_Does_Not_Retarget_Or_Debuff_Dead_Target()
    {
        var p = P("p", 5);
        var other = P("other", 100, "back1");
        var ctx = Ctx(E("e", 100), p, other);
        new EnemyActionResolver().Resolve(ctx, "e", Composite());
        Assert.AreEqual(0, p.Hp);
        Assert.AreEqual(100, other.Hp);
        Assert.IsNull(ctx.FindStatus("p", StatusType.Weaken));
        Assert.IsNull(ctx.FindStatus("other", StatusType.Weaken));
        Assert.IsNotNull(ctx.FindStatus("e", StatusType.AttackUp));
        Assert.AreEqual(1, ctx.ActionResults[0].Hits.Count);
    }

    [Test]
    public void Composite_Aoe_Debuffs_Only_Living_Hit_Targets()
    {
        var p = P("p", 5);
        var other = P("other", 100, "back1");
        var dead = P("dead", 1, "back2");
        dead.TakeDamage(1);
        var ctx = Ctx(E("e", 100), p, other, dead);
        var intent = Composite("attack", "slow");
        intent.target = "all";
        new EnemyActionResolver().Resolve(ctx, "e", intent);
        Assert.AreEqual(89, other.Hp);
        Assert.IsNull(ctx.FindStatus("p", StatusType.Slow));
        Assert.IsNull(ctx.FindStatus("dead", StatusType.Slow));
        Assert.IsNotNull(ctx.FindStatus("other", StatusType.Slow));
        Assert.AreEqual(2, ctx.ActionResults[0].Hits.Count);
    }

    [Test]
    public void Composite_Lethal_Attack_Does_Not_Apply_Post_Effects()
    {
        var ctx = Ctx(E("e", 100), P("p", 5));
        new EnemyActionResolver().Resolve(ctx, "e", Composite());
        Assert.IsNull(ctx.FindStatus("e", StatusType.AttackUp));
        Assert.IsNull(ctx.FindStatus("p", StatusType.Weaken));
    }

    [Test]
    public void Pending_Lethal_Release_Does_Not_Execute_Current_Composite()
    {
        var ctx = Ctx(E("e", 100), P("p", 20));
        var resolver = new EnemyActionResolver();
        resolver.Resolve(ctx, "e", Charge(25, true));
        resolver.Resolve(ctx, "e", Composite());
        Assert.IsNull(ctx.FindStatus("e", StatusType.AttackUp));
        Assert.IsNull(ctx.FindStatus("p", StatusType.Weaken));
        Assert.AreEqual(1, ctx.ActionResults[1].Hits.Count);
        Assert.IsNull(resolver.Preview(ctx, "e", Attack(10)).PendingRelease);
    }

    [Test]
    public void Preview_Is_ReadOnly_And_Uses_Shared_Sequential_Flooring()
    {
        var ctx = Ctx(E("e", 100), P("p", 100), P("other", 100, "front2"));
        var resolver = new EnemyActionResolver();
        resolver.Resolve(ctx, "e", Charge(11, true));
        ctx.AddStatus("e", new StatusEffect { Type = StatusType.AttackUp, Percent = 25, Duration = 2 });
        ctx.AddStatus("e", new StatusEffect { Type = StatusType.Weaken, Percent = 25, Duration = 2 });
        ctx.AddStatus("p", new StatusEffect { Type = StatusType.Shield, Amount = 100 });
        var intent = Composite();
        for (int i = 0; i < 10; i++)
        {
            var preview = resolver.Preview(ctx, "e", intent);
            Assert.AreEqual("multi", preview.Type);
            Assert.AreEqual(9, preview.Current.DamagePerHit, "11 -> floor(13.75)=13 -> floor(9.75)=9");
            Assert.AreEqual(3, preview.Current.Hits);
            Assert.AreEqual(9, preview.PendingRelease.DamagePerHit);
            Assert.AreEqual(25, preview.SourceAttackUpPercent);
            Assert.AreEqual(25, preview.SourceWeakenPercent);
            StringAssert.Contains("9×3", preview.Text);
            StringAssert.Contains("本轮蓄力释放", preview.Text);
        }
        Assert.AreEqual(new Rng(1).NextULong(), ctx.Rng.NextULong(), "预览不能消费随机数");
        Assert.AreEqual(1, ctx.ActionResults.Count);
        Assert.AreEqual(100, ctx.GetUnit("p").Hp);
        Assert.AreEqual(100, ctx.FindStatus("p", StatusType.Shield).Amount);
        Assert.AreEqual(2, ctx.FindStatus("e", StatusType.AttackUp).Duration);
        var actual = Damage.Apply(ctx, new DamageRequest { SourceUnitId = "e", TargetUnitId = "p", Amount = 11 });
        Assert.AreEqual(13, actual.AfterAttackUp);
        Assert.AreEqual(9, actual.AfterWeaken);
        Assert.AreEqual(9, actual.ShieldAbsorbed);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Preview_Interrupt_Matches_Skipped_Release_And_Does_Not_Consume_It(bool interruptible)
    {
        var p = P("p", 100);
        var ctx = Ctx(E("e", 100), p);
        var resolver = new EnemyActionResolver();
        resolver.Resolve(ctx, "e", Charge(25, interruptible));
        ctx.AddStatus("e", new StatusEffect { Type = StatusType.Interrupt });
        var preview = resolver.Preview(ctx, "e", Composite());
        Assert.IsTrue(preview.Interrupted);
        Assert.IsTrue(preview.Current.Suppressed);
        Assert.AreEqual(interruptible, preview.PendingRelease.Suppressed);
        resolver.ResolveSkipped(ctx, "e");
        Assert.AreEqual(interruptible ? 100 : 75, p.Hp);
        Assert.IsNull(ctx.FindStatus("e", StatusType.AttackUp));
        Assert.IsNull(ctx.FindStatus("p", StatusType.Weaken));
        Assert.IsNull(resolver.Preview(ctx, "e", Composite()).PendingRelease);
    }

    [Test]
    public void Preview_Shows_Charge_Next_Round_And_Is_Detached_From_Config()
    {
        var ctx = Ctx(E("e", 100), P("p", 100));
        var intent = Charge(25, true);
        var resolver = new EnemyActionResolver();
        var preview = resolver.Preview(ctx, "e", intent);
        intent.nextDamage = 99;
        Assert.AreEqual(25, preview.Current.NextRoundDamage);
        Assert.AreEqual(0, preview.Current.DamagePerHit);
        Assert.AreEqual(0, preview.Current.Hits);
        Assert.IsNull(preview.PendingRelease);
        StringAssert.Contains("下轮 25", preview.Text);
        Assert.AreEqual(0, ctx.ActionResults.Count);
    }

    [Test]
    public void Preview_Does_Not_Lock_Target_Before_Execution()
    {
        var p = P("p", 100);
        var other = P("other", 100, "back1");
        var ctx = Ctx(E("e", 100), p, other);
        var resolver = new EnemyActionResolver();
        resolver.Preview(ctx, "e", Composite());
        ctx.FormationOf(Team.Player).GetSlot("back1").AddModifier(60, 1);
        resolver.Resolve(ctx, "e", Composite());
        Assert.AreEqual(100, p.Hp);
        Assert.AreEqual(67, other.Hp);
        Assert.IsNotNull(ctx.FindStatus("other", StatusType.Weaken));
    }

    [Test]
    public void Attack_Uses_Current_Maximum_After_Taunt_Changes()
    {
        var front = P("front", 100);
        var back = P("back", 100, "back1");
        var ctx = Ctx(E("e", 100), front, back);
        var resolver = new EnemyActionResolver();
        resolver.Resolve(ctx, "e", Attack(10));
        ctx.FormationOf(Team.Player).GetSlot("back1").AddModifier(60, 1);
        resolver.Resolve(ctx, "e", Attack(20));
        Assert.AreEqual(90, front.Hp);
        Assert.AreEqual(80, back.Hp);
    }

    [Test]
    public void Multi_Uses_Execution_Maximum_For_All_Hits()
    {
        var front = P("front", 100);
        var back = P("back", 100, "back1");
        var ctx = Ctx(E("e", 100), front, back);
        ctx.FormationOf(Team.Player).GetSlot("back1").AddModifier(60, 1);
        new EnemyActionResolver().Resolve(ctx, "e", Multi(3, 10));
        Assert.AreEqual(100, front.Hp);
        Assert.AreEqual(70, back.Hp);
    }

    [Test]
    public void Charge_Selects_Current_Target_On_Release_Not_On_Charge()
    {
        var front = P("front", 100);
        var back = P("back", 100, "back1");
        var ctx = Ctx(E("e", 100), front, back);
        var resolver = new EnemyActionResolver();
        resolver.Resolve(ctx, "e", Charge(25, true));
        Assert.AreEqual(100, front.Hp);
        Assert.AreEqual(100, back.Hp);
        ctx.FormationOf(Team.Player).GetSlot("back1").AddModifier(60, 2);
        resolver.Resolve(ctx, "e", Attack(10));
        Assert.AreEqual(100, front.Hp);
        Assert.AreEqual(65, back.Hp);
        resolver.Resolve(ctx, "e", Attack(10));
        Assert.AreEqual(55, back.Hp, "Pending charge must release only once.");
    }

    [Test]
    public void Charge_Release_Ignores_Dead_Former_Maximum()
    {
        var front = P("front", 100);
        var back = P("back", 100, "back1");
        var ctx = Ctx(E("e", 100), front, back);
        var resolver = new EnemyActionResolver();
        resolver.Resolve(ctx, "e", Charge(25, true));
        front.TakeDamage(100);
        resolver.Resolve(ctx, "e", Attack(10));
        Assert.AreEqual(0, front.Hp);
        Assert.AreEqual(65, back.Hp);
    }

    [Test]
    public void Aoe_Ignores_Taunt_And_Excludes_Dead_Players()
    {
        var front = P("front", 100);
        var back = P("back", 100, "back1");
        var dead = P("dead", 100, "back2");
        dead.TakeDamage(100);
        var enemy = E("e", 100);
        var ctx = Ctx(enemy, front, back, dead);
        new EnemyActionResolver().Resolve(ctx, "e", AttackAll(25));
        Assert.AreEqual(75, front.Hp);
        Assert.AreEqual(75, back.Hp);
        Assert.AreEqual(0, dead.Hp);
        Assert.AreEqual(100, enemy.Hp);
    }

    [Test]
    public void No_Living_Players_Is_A_Safe_NoOp_For_Targeted_Actions()
    {
        var dead = P("dead", 100);
        dead.TakeDamage(100);
        var ctx = Ctx(E("e", 100), dead);
        var resolver = new EnemyActionResolver();
        Assert.DoesNotThrow(() =>
        {
            resolver.Resolve(ctx, "e", Charge(25, true));
            resolver.Resolve(ctx, "e", Attack(10));
            resolver.Resolve(ctx, "e", Multi(3, 10));
            resolver.Resolve(ctx, "e", new IntentConfig { id = "d", type = "debuff", target = "single", debuffType = "weaken", debuffPercent = 25, debuffDuration = 1 });
        });
        Assert.AreEqual(0, dead.Hp);
        Assert.IsNull(ctx.FindStatus("dead", StatusType.Weaken));
    }

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
    public void Debuff_Applies_Weaken_To_Execution_Time_Maximum()
    {
        var e = E("e", 100);
        var front = P("front", 100);
        var p = P("p", 100, "back1");
        var ctx = Ctx(e, front, p);
        var resolver = new EnemyActionResolver();
        var intent = new IntentConfig
        {
            id = "d", type = "debuff", target = "single",
            debuffType = "weaken", debuffPercent = 25, debuffDuration = 2
        };
        ctx.FormationOf(Team.Player).GetSlot("back1").AddModifier(60, 2);

        resolver.Resolve(ctx, "e", intent);

        Assert.IsNull(ctx.FindStatus("front", StatusType.Weaken));
        var fx = ctx.FindStatus("p", StatusType.Weaken);
        Assert.IsNotNull(fx);
        Assert.AreEqual(25, fx.Percent);
        Assert.AreEqual(2, fx.Duration);
    }
}