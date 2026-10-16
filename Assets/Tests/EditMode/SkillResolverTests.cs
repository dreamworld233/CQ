using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;
using CQ.Core.Status;
using CQ.Runtime;

public class SkillResolverTests
{
    private static Unit U(string id, Team team, string slot = "front1", string trigger = "attack", int hp = 100) =>
        new Unit(id, team) { SlotId = slot, Hp = hp, MaxHp = hp, MaxUlt = 3,
            UltChargeTrigger = trigger, UltChargeAmount = 1 };
    private static CombatContext Ctx(params Unit[] units) => new CombatContext(new Rng(1), units,
        new FormationConfig { frontBaseTaunt = 100, backBaseTaunt = 50 });
    private static SkillSpec Attack(string type = "basic", string target = "single", int amount = 12) =>
        new SkillSpec { id = type, type = type, damage = new DamageSpec { kind = "physical", target = target, amount = amount } };
    private static ActionResult Cast(CombatContext ctx, Unit actor, SkillSpec skill) =>
        SkillResolver.Resolve(skill, ctx, actor);

    [TestCase(12, "physical")]
    [TestCase(8, "physical")]
    [TestCase(10, "magic")]
    public void Basic_Deals_Configured_Damage_And_Charges_Once(int damage, string kind)
    {
        var actor = U("actor", Team.Player);
        var enemy = U("enemy", Team.Enemy);
        var ctx = Ctx(actor, enemy);
        var skill = Attack(amount: damage); skill.damage.kind = kind;
        var result = Cast(ctx, actor, skill);
        Assert.AreEqual(100 - damage, enemy.Hp);
        Assert.AreEqual(1, actor.Ult);
        Assert.AreEqual(1, result.Charges.Count);
        Assert.AreEqual(1, result.Hits.Count);
        Assert.AreEqual(1, result.UnitChanges.Single(c => c.UnitId == actor.Id).After.Ult);
    }

    [Test]
    public void Charge_Clamps_And_No_Hit_Does_Not_Charge()
    {
        var actor = U("actor", Team.Player); actor.Ult = 2; actor.UltChargeAmount = 9;
        var enemy = U("enemy", Team.Enemy);
        var ctx = Ctx(actor, enemy);
        var result = Cast(ctx, actor, Attack());
        Assert.AreEqual(3, actor.Ult);
        Assert.AreEqual(1, result.Charges.Single().Amount);
        actor.Ult = 0; enemy.Hp = 0;
        Assert.IsNull(Cast(ctx, actor, Attack()));
        Assert.AreEqual(0, actor.Ult);
        Assert.AreEqual(1, ctx.ActionResults.Count);
    }

    [Test]
    public void Shield_Fully_Absorbs_Still_Charges_And_Records_Fact()
    {
        var actor = U("actor", Team.Player);
        var enemy = U("enemy", Team.Enemy, trigger: "hit");
        var ctx = Ctx(actor, enemy);
        ctx.AddStatus(enemy.Id, new StatusEffect { Type = StatusType.Shield, Amount = 50 });
        var result = Cast(ctx, actor, Attack());
        Assert.AreEqual(100, enemy.Hp);
        Assert.AreEqual(1, enemy.Ult);
        Assert.AreEqual(1, actor.Ult);
        Assert.AreEqual(0, result.Hits.Single().HpDamage);
        Assert.AreEqual(12, result.Hits.Single().ShieldAbsorbed);
    }

    [Test]
    public void Aoe_Attacker_Once_Each_Surviving_Target_Once_Dead_Not_Charged()
    {
        var actor = U("actor", Team.Player);
        var a = U("a", Team.Enemy, trigger: "hit");
        var b = U("b", Team.Enemy, "front2", "hit");
        var dead = U("dead", Team.Enemy, "back1", "hit"); dead.Hp = 0;
        var killed = U("killed", Team.Enemy, "back2", "hit", 5);
        var result = Cast(Ctx(actor, a, b, dead, killed), actor, Attack(target: "all"));
        Assert.AreEqual(1, actor.Ult);
        Assert.AreEqual(1, a.Ult); Assert.AreEqual(1, b.Ult);
        Assert.AreEqual(0, dead.Ult); Assert.AreEqual(0, killed.Ult);
        Assert.AreEqual(3, result.Hits.Count);
        Assert.AreEqual(5, result.Hits.Single(h => h.TargetUnitId == killed.Id).HpDamage);
    }

    [TestCase(45)]
    [TestCase(38)]
    public void Ultimate_Consumes_Full_Charge_No_Self_Charge_But_Victims_Charge(int damage)
    {
        var actor = U("actor", Team.Player); actor.Ult = 3;
        var a = U("a", Team.Enemy, trigger: "hit");
        var b = U("b", Team.Enemy, "back1", "hit");
        var result = Cast(Ctx(actor, a, b), actor, Attack("ult", "all", damage));
        Assert.AreEqual(0, actor.Ult);
        Assert.AreEqual(100 - damage, a.Hp); Assert.AreEqual(100 - damage, b.Hp);
        Assert.AreEqual(1, a.Ult); Assert.AreEqual(1, b.Ult);
        Assert.IsTrue(result.Charges.All(c => c.Trigger == "hit"));
    }

    [Test]
    public void Ultimate_Not_Full_Does_Nothing()
    {
        var actor = U("actor", Team.Player); actor.Ult = 2;
        var enemy = U("enemy", Team.Enemy);
        var ctx = Ctx(actor, enemy);
        Assert.IsNull(Cast(ctx, actor, Attack("ult")));
        Assert.AreEqual(2, actor.Ult); Assert.AreEqual(100, enemy.Hp);
        Assert.IsEmpty(ctx.ActionResults);
    }

    [Test]
    public void Single_Uses_Taunt_And_Removed_Snipe_Is_Rejected()
    {
        var actor = U("actor", Team.Player);
        var front = U("front", Team.Enemy);
        var back = U("back", Team.Enemy, "back1");
        var ctx = Ctx(actor, front, back);
        Cast(ctx, actor, Attack());
        Assert.AreEqual(88, front.Hp); Assert.AreEqual(100, back.Hp);
        actor.Ult = 3;
        Assert.IsNull(Cast(ctx, actor, Attack("ult", "snipe")));
        Assert.AreEqual(3, actor.Ult); Assert.AreEqual(1, ctx.ActionResults.Count);
    }

    [TestCase("self")]
    [TestCase("single")]
    [TestCase("all")]
    public void Heal_Ultimate_Heals_Living_Allies_Clamps_And_Never_Revives(string mode)
    {
        var actor = U("actor", Team.Player); actor.Hp = 80; actor.Ult = 3;
        var ally = U("ally", Team.Player, "front2"); ally.Hp = 90;
        var dead = U("dead", Team.Player, "back1"); dead.Hp = 0;
        var enemy = U("enemy", Team.Enemy);
        var result = Cast(Ctx(actor, ally, dead, enemy), actor,
            new SkillSpec { id = "heal", type = "ult", heal = new HealSpec { target = mode, amount = 20 } });
        Assert.AreEqual(100, actor.Hp);
        Assert.AreEqual(mode == "all" ? 100 : 90, ally.Hp);
        Assert.AreEqual(0, dead.Hp); Assert.AreEqual(0, actor.Ult);
        Assert.IsEmpty(result.Hits); Assert.IsEmpty(result.Charges);
    }

    [Test]
    public void Multi_Hits_Deduplicate_Charge_And_Stop_Hitting_Dead_Target()
    {
        var actor = U("enemy", Team.Enemy);
        var target = U("target", Team.Player, trigger: "hit");
        var ctx = Ctx(actor, target);
        var resolver = new EnemyActionResolver();
        resolver.Resolve(ctx, actor.Id, new IntentConfig { type = "multi", hits = 3, damage = 10 });
        Assert.AreEqual(70, target.Hp); Assert.AreEqual(1, target.Ult); Assert.AreEqual(1, actor.Ult);
        Assert.AreEqual(3, ctx.ActionResults.Single().Hits.Count);
        target.Hp = 5;
        resolver.Resolve(ctx, actor.Id, new IntentConfig { type = "multi", hits = 3, damage = 10 });
        Assert.AreEqual(1, ctx.ActionResults.Last().Hits.Count);
        Assert.AreEqual(5, ctx.ActionResults.Last().Hits.Single().HpDamage);
        Assert.AreEqual(1, target.Ult, "死亡不再充能");
    }

    [Test]
    public void Pending_Release_And_Intent_Are_One_Action_One_Charge()
    {
        var actor = U("enemy", Team.Enemy);
        var target = U("target", Team.Player, trigger: "hit");
        var ctx = Ctx(actor, target);
        var resolver = new EnemyActionResolver();
        resolver.Resolve(ctx, actor.Id, new IntentConfig { type = "charge", nextDamage = 20 });
        Assert.AreEqual(0, actor.Ult); Assert.AreEqual(0, target.Ult);
        resolver.Resolve(ctx, actor.Id, new IntentConfig { type = "attack", damage = 10 });
        Assert.AreEqual(70, target.Hp); Assert.AreEqual(1, actor.Ult); Assert.AreEqual(1, target.Ult);
        Assert.AreEqual(2, ctx.ActionResults.Count);
        Assert.AreEqual(2, ctx.ActionResults.Last().Hits.Count);
        Assert.AreEqual(1, ctx.ActionResults.Last().HitTargetIds.Count);
    }

    [Test]
    public void Lethal_Pending_Release_Does_Not_Execute_Followup_Buff()
    {
        var actor = U("enemy", Team.Enemy);
        var target = U("target", Team.Player, trigger: "hit");
        var ctx = Ctx(actor, target);
        var resolver = new EnemyActionResolver();
        resolver.Resolve(ctx, actor.Id, new IntentConfig { type = "charge", nextDamage = 500 });
        resolver.Resolve(ctx, actor.Id, new IntentConfig { type = "attackUp", buffPercent = 50 });
        Assert.IsTrue(target.IsDead); Assert.IsFalse(ctx.HasStatus(actor.Id, StatusType.AttackUp));
        Assert.AreEqual(0, target.Ult);
    }

    [Test]
    public void Pure_Debuff_Charge_Preparation_And_RoundEnd_Do_Not_Charge()
    {
        var actor = U("enemy", Team.Enemy);
        var target = U("target", Team.Player, trigger: "hit");
        var ctx = Ctx(actor, target);
        var resolver = new EnemyActionResolver();
        resolver.Resolve(ctx, actor.Id, new IntentConfig { type = "debuff", debuffType = "weaken", debuffPercent = 25 });
        resolver.Resolve(ctx, actor.Id, new IntentConfig { type = "charge", nextDamage = 20 });
        StatusResolver.RoundEndTick(ctx);
        Assert.AreEqual(0, actor.Ult); Assert.AreEqual(0, target.Ult);
        Assert.IsTrue(ctx.ActionResults.All(r => r.Hits.Count == 0 && r.Charges.Count == 0));
    }

    [Test]
    public void Result_Is_Immutable_And_Snapshot_Does_Not_Follow_Later_Mutations()
    {
        var actor = U("actor", Team.Player);
        var target = U("target", Team.Enemy, trigger: "hit");
        var ctx = Ctx(actor, target);
        ctx.AddStatus(target.Id, new StatusEffect { Type = StatusType.Shield, Amount = 50, Duration = 2 });
        var result = Cast(ctx, actor, Attack());
        var after = result.UnitChanges.Single(c => c.UnitId == target.Id).After;
        target.Hp = 1; target.Ult = 3;
        ctx.FindStatus(target.Id, StatusType.Shield).Amount = 0;
        Assert.AreEqual(100, after.Hp); Assert.AreEqual(1, after.Ult);
        Assert.AreEqual(38, after.Statuses.Single().Amount);
        Assert.Throws<NotSupportedException>(() => ((IList<AttackHit>)result.Hits).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<UnitChange>)result.UnitChanges).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<UltCharge>)result.Charges).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<StatusSnapshot>)after.Statuses).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)result.HitTargetIds).Clear());
    }

    [Test]
    public void Hit_Config_Does_Not_Charge_From_Its_Own_Basic_Or_RoundEnd()
    {
        var guard = U("guard", Team.Player, trigger: "hit");
        var enemy = U("enemy", Team.Enemy);
        var ctx = Ctx(guard, enemy);
        Cast(ctx, guard, Attack());
        StatusResolver.RoundEndTick(ctx);
        Assert.AreEqual(0, guard.Ult);
    }

    [Test]
    public void Card_Action_Damage_Does_Not_Grant_Attack_Charge()
    {
        var actor = U("actor", Team.Player);
        var target = U("target", Team.Enemy, trigger: "hit");
        var ctx = Ctx(actor, target);
        ctx.BeginAction(BattleEventKind.Card, actor, "damageCard", false);
        Damage.Apply(ctx, new DamageRequest { SourceUnitId = actor.Id, TargetUnitId = target.Id, Amount = 5 });
        var result = ctx.CompleteAction();
        Assert.AreEqual(0, actor.Ult);
        Assert.AreEqual(1, target.Ult);
        Assert.AreEqual(1, result.Hits.Count);
    }

    [Test]
    public void Skipped_Noninterruptible_Pending_Release_Is_One_Action()
    {
        var actor = U("enemy", Team.Enemy);
        var target = U("target", Team.Player, trigger: "hit");
        var ctx = Ctx(actor, target);
        var resolver = new EnemyActionResolver();
        resolver.Resolve(ctx, actor.Id, new IntentConfig { type = "charge", nextDamage = 15, interruptible = false });
        ctx.AddStatus(actor.Id, new StatusEffect { Type = StatusType.Interrupt, Duration = 1 });
        resolver.ResolveSkipped(ctx, actor.Id);
        Assert.AreEqual(85, target.Hp);
        Assert.AreEqual(1, actor.Ult); Assert.AreEqual(1, target.Ult);
        Assert.AreEqual(2, ctx.ActionResults.Count);
        Assert.AreEqual(1, ctx.ActionResults.Last().Hits.Count);
    }

    [Test]
    public void Nested_Action_Is_Rejected_Not_Double_Charged()
    {
        var actor = U("actor", Team.Player); var enemy = U("enemy", Team.Enemy);
        var ctx = Ctx(actor, enemy);
        ctx.BeginAction(BattleEventKind.Card, null, "card", false);
        Assert.Throws<InvalidOperationException>(() => Cast(ctx, actor, Attack()));
        var result = ctx.CompleteAction();
        Assert.IsEmpty(result.Charges); Assert.AreEqual(1, ctx.ActionResults.Count);
    }

    [Test]
    public void Resources_Characters_All_Current_Skills_Resolve()
    {
        var configs = ConfigLoader.LoadCharacters();
        Assert.IsEmpty(configs.errors); Assert.AreEqual(3, configs.items.Count);
        foreach (var cfg in configs.items)
        {
            Assert.AreEqual(2, cfg.skills.Count);
            foreach (var skill in cfg.skills)
            {
                var unit = U(cfg.id, Team.Player, trigger: cfg.ultChargeTrigger, hp: cfg.maxHp);
                unit.MaxUlt = cfg.maxUlt; unit.UltChargeAmount = cfg.ultChargeAmount;
                if (skill.type == "ult") unit.Ult = cfg.maxUlt;
                var a = U("a", Team.Enemy); var b = U("b", Team.Enemy, "back1");
                Assert.IsNotNull(Cast(Ctx(unit, a, b), unit, skill), cfg.id + "/" + skill.id);
            }
        }
    }
    [Test]
    public void Heal_Uses_Percentage_Not_Absolute_Hp_And_Excludes_Enemy_And_Dead()
    {
        var actor = U("actor", Team.Player); actor.Ult = 3; actor.Hp = 90;
        var small = U("small", Team.Player, "front2", hp: 40); small.Hp = 20;
        var large = U("large", Team.Player, "back1", hp: 200); large.Hp = 40;
        var dead = U("dead", Team.Player, "back2"); dead.Hp = 0;
        var enemy = U("enemy", Team.Enemy); enemy.Hp = 1;
        Cast(Ctx(actor, small, large, dead, enemy), actor,
            new SkillSpec { id = "heal", type = "ult", heal = new HealSpec { target = "single", amount = 20 } });
        Assert.AreEqual(60, large.Hp); Assert.AreEqual(20, small.Hp);
        Assert.AreEqual(90, actor.Hp); Assert.AreEqual(0, dead.Hp); Assert.AreEqual(1, enemy.Hp);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Heal_Ties_Use_Registration_Order_Including_Self_And_Do_Not_Consume_Rng(bool selfFirst)
    {
        var actor = U("actor", Team.Player); actor.Ult = 3; actor.Hp = 50;
        var ally = U("ally", Team.Player, "back1", hp: 200); ally.Hp = 100;
        var enemy = U("enemy", Team.Enemy);
        var ctx = selfFirst ? Ctx(actor, ally, enemy) : Ctx(ally, actor, enemy);
        Cast(ctx, actor, new SkillSpec { id = "heal", type = "ult", heal = new HealSpec { target = "single", amount = 20 } });
        Assert.AreEqual(selfFirst ? 70 : 50, actor.Hp);
        Assert.AreEqual(selfFirst ? 100 : 120, ally.Hp);
        Assert.AreEqual(new Rng(1).NextInt(0, 100000), ctx.Rng.NextInt(0, 100000));
    }

    [Test]
    public void Heal_Large_Cross_Products_Do_Not_Overflow()
    {
        var actor = U("actor", Team.Player, hp: 2000000000); actor.Hp = 1500000000; actor.Ult = 3;
        var ally = U("ally", Team.Player, "back1", hp: 1800000000); ally.Hp = 1200000000;
        Cast(Ctx(actor, ally), actor,
            new SkillSpec { id = "heal", type = "ult", heal = new HealSpec { target = "single", amount = 20 } });
        Assert.AreEqual(1200000020, ally.Hp); Assert.AreEqual(1500000000, actor.Hp);
    }

}
