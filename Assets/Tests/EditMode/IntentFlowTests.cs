using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;

public class IntentFlowTests
{
    private static CharacterConfig Sword() => new CharacterConfig
    {
        id = "sword",
        name = "剑士",
        role = "dps",
        maxHp = 100,
        baseSpeed = 10,
        ultChargeTrigger = "attack", ultChargeAmount = 1,
        maxUlt = 3,
        defaultSlot = "front1",
        skills = new List<SkillSpec>
        {
            new SkillSpec { id = "sword_basic", name = "普攻", type = "basic",
                damage = new DamageSpec { kind = "physical", target = "single", amount = 12 } }
        }
    };

    private static EnemyConfig Lord() => new EnemyConfig
    {
        id = "lord",
        name = "领主",
        maxHp = 300,
        baseSpeed = 5,
        intentSequence = new[] { "charge", "attack" },
        intents = new List<IntentConfig>
        {
            new IntentConfig { id = "charge", type = "charge", nextDamage = 120, interruptible = true },
            new IntentConfig { id = "attack", type = "attack", target = "single", damage = 40 }
        }
    };

    private static EnemyConfig Bag() => new EnemyConfig
    {
        id = "bag",
        name = "沙包",
        maxHp = 99999,
        baseSpeed = 1,
        intentSequence = new[] { "attack" },
        intents = new List<IntentConfig>
        {
            new IntentConfig { id = "attack", type = "attack", target = "single", damage = 0 }
        }
    };

    private static CardConfig Card(string id, string type, int percent = 0, int amount = 0, int count = 1) =>
        new CardConfig { id = id, name = id, effect = new CardEffectSpec { type = type, percent = percent, amount = amount }, count = count };

    private static BattleEngine Make(long seed, IEnumerable<CharacterConfig> chars, IEnumerable<EnemyConfig> enemies, params CardConfig[] cards)
    {
        var enemyList = enemies.ToList();
        var ids = enemyList.Select(e => e.id).ToList();
        var catalog = enemyList.ToDictionary(e => e.id);
        return BattleEngine.Create(seed, chars, ids, catalog, cards,
            formationConfig: new FormationConfig { frontBaseTaunt = 100, backBaseTaunt = 50 });
    }

    private static CharacterConfig Fighter(string id, string slot)
    {
        var config = Sword();
        config.id = id;
        config.defaultSlot = slot;
        return config;
    }

    private static EnemyConfig Attacker(string type = "attack", string target = "single")
    {
        var config = Bag();
        config.intentSequence = new[] { "hit" };
        config.intents = new List<IntentConfig>
        {
            new IntentConfig { id = "hit", type = type, target = target, damage = 25, hits = 3 }
        };
        return config;
    }

    [TestCase("attack", 25)]
    [TestCase("multi", 75)]
    public void Revealed_Intent_Targets_Execution_Maximum_And_Records_Actual_Target(string type, int damage)
    {
        var eng = Make(7, new[] { Fighter("front", "front1"), Fighter("back", "back1") }, new[] { Attacker(type) });
        eng.StartBattle();
        string enemyId = eng.Enemies[0].Id;
        Assert.AreEqual("hit", eng.GetIntent(enemyId).CurrentIntentId);
        eng.Ctx.FormationOf(Team.Player).GetSlot("back1").AddModifier(60, 1);

        BattleTestDriver.CompleteRound(eng);

        Assert.AreEqual(100, eng.Ctx.GetUnit("front").Hp);
        Assert.AreEqual(100 - damage, eng.Ctx.GetUnit("back").Hp);
        var events = eng.RoundEvents.Where(e => e.Kind == BattleEventKind.Intent && e.ActorId == enemyId).ToList();
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(type, events[0].ActionId);
        Assert.AreEqual("back", events[0].TargetId);
        Assert.AreEqual(-damage, events[0].HpDelta);
        Assert.IsFalse(events[0].TargetDead);
    }

    [Test]
    public void Dead_Revealed_Maximum_Is_Not_Targeted_And_Lethal_Event_Is_Accurate()
    {
        var enemy = Attacker();
        enemy.intents[0].damage = 150;
        var eng = Make(7, new[] { Fighter("front", "front1"), Fighter("back", "back1") }, new[] { enemy });
        eng.StartBattle();
        eng.Ctx.GetUnit("front").TakeDamage(100);
        BattleTestDriver.CompleteRound(eng);
        var hit = eng.RoundEvents.Single(e => e.Kind == BattleEventKind.Intent && e.HpDelta < 0);
        Assert.AreEqual(eng.Enemies[0].Id, hit.ActorId);
        Assert.AreEqual("attack", hit.ActionId);
        Assert.AreEqual("back", hit.TargetId);
        Assert.AreEqual(-100, hit.HpDelta, "Record actual HP loss, not overkill damage.");
        Assert.IsTrue(hit.TargetDead);
        Assert.AreEqual(0, eng.Ctx.GetUnit("front").Hp);
    }

    [Test]
    public void Aoe_Events_Record_Each_Living_Target_Regardless_Of_Taunt()
    {
        var eng = Make(7, new[] { Fighter("front", "front1"), Fighter("back", "back1"), Fighter("dead", "back2") },
            new[] { Attacker(target: "all") });
        eng.StartBattle();
        eng.Ctx.GetUnit("dead").TakeDamage(100);
        eng.Ctx.FormationOf(Team.Player).GetSlot("back1").AddModifier(1000, 1);
        BattleTestDriver.CompleteRound(eng);
        var events = eng.RoundEvents.Where(e => e.Kind == BattleEventKind.Intent).ToList();
        Assert.AreEqual(2, events.Count);
        CollectionAssert.AreEquivalent(new[] { "front", "back" }, events.Select(e => e.TargetId));
        foreach (var hit in events)
        {
            Assert.AreEqual(eng.Enemies[0].Id, hit.ActorId);
            Assert.AreEqual("attack", hit.ActionId);
            Assert.AreEqual(-25, hit.HpDelta);
            Assert.IsFalse(hit.TargetDead);
        }
    }

    // T9.1 意图完整结算：AOE（attack target=all）打全体存活玩家
    [Test]
    public void Aoe_Intent_Hits_All_Living_Players()
    {
        var e = new Unit("e", Team.Enemy) { MaxHp = 100, Hp = 100, SlotId = "front1" };
        var p1 = new Unit("p1", Team.Player) { MaxHp = 100, Hp = 100, SlotId = "front1" };
        var p2 = new Unit("p2", Team.Player) { MaxHp = 100, Hp = 100, SlotId = "front2" };
        var p3 = new Unit("p3", Team.Player) { MaxHp = 100, Hp = 100, SlotId = "back1" };
        var ctx = new CombatContext(new Rng(1), new List<Unit> { e, p1, p2, p3 });

        var aoe = new IntentConfig { id = "aoe", type = "attack", target = "all", damage = 25 };
        new EnemyActionResolver().Resolve(ctx, "e", aoe);

        Assert.AreEqual(75, p1.Hp);
        Assert.AreEqual(75, p2.Hp);
        Assert.AreEqual(75, p3.Hp);
    }

    // T9.2 打断边界：打断后本回合不出手，下回合重新亮同一意图（不前进）
    [Test]
    public void Interrupt_ReReveals_Same_Intent_Next_Round()
    {
        var eng = Make(1, new[] { Sword() }, new[] { Lord() }, Card("interrupt", "interrupt", count: 2));
        eng.StartBattle();
        string lordId = eng.Enemies[0].Id;

        Assert.AreEqual("charge", eng.GetIntent(lordId).CurrentIntentId, "第 1 回合亮蓄力");
        BattleTestDriver.CompleteRound(eng); // 领主蓄力，意图前进

        Assert.AreEqual("attack", eng.GetIntent(lordId).CurrentIntentId, "第 2 回合亮普攻");
        Assert.IsTrue(eng.PlayCard(0, lordId)); // 打断领主
        BattleTestDriver.CompleteRound(eng); // 领主本回合不出手，意图不前移

        Assert.AreEqual("attack", eng.GetIntent(lordId).CurrentIntentId, "下回合重新亮同一意图");
    }

    // T9.3 存牌爆发点：跨回合存牌，同一回合多出，多效果同时成立
    [Test]
    public void Stored_Cards_Can_Be_Bursted_In_One_Round()
    {
        var eng = Make(1, new[] { Sword() }, new[] { Bag() },
            Card("weaken", "weaken", percent: 25), Card("shield", "shield", amount: 20));
        eng.StartBattle();
        BattleTestDriver.CompleteRound(eng); // 第 1 回合结束，已抽 2 张（weaken + shield）

        Assert.AreEqual(2, eng.Hand.Count, "两回合各抽 1，攒成 2 张手牌");

        string enemyId = eng.Enemies[0].Id;
        bool playedWeak = false, playedShield = false;
        for (int i = 0; i < eng.Hand.Count;)
        {
            string id = eng.Hand[i];
            if (id == "weaken" && !playedWeak) { Assert.IsTrue(eng.PlayCard(i, enemyId)); playedWeak = true; continue; }
            if (id == "shield" && !playedShield) { Assert.IsTrue(eng.PlayCard(i, "sword")); playedShield = true; continue; }
            i++;
        }

        Assert.IsTrue(playedWeak && playedShield, "两卡应都打出");
        Assert.IsTrue(eng.Ctx.HasStatus(enemyId, StatusType.Weaken), "敌身上虚弱成立");
        Assert.IsTrue(eng.Ctx.HasStatus("sword", StatusType.Shield), "我方护盾成立");
    }
}