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
        maxEnergy = 3,
        maxUlt = 3,
        row = "front",
        aggroWeight = 2,
        skills = new List<SkillSpec>
        {
            new SkillSpec { id = "sword_basic", name = "普攻", type = "basic", energyDelta = 1, ultDelta = 1,
                damage = new DamageSpec { kind = "physical", target = "single", amount = 12 } }
        }
    };

    private static EnemyConfig Lord() => new EnemyConfig
    {
        id = "lord",
        name = "领主",
        maxHp = 300,
        baseSpeed = 5,
        row = "front",
        aggroWeight = 2,
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
        row = "front",
        aggroWeight = 2,
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
        return BattleEngine.Create(seed, chars, ids, catalog, cards);
    }

    // T9.1 意图完整结算：AOE（attack target=all）打全体存活玩家
    [Test]
    public void Aoe_Intent_Hits_All_Living_Players()
    {
        var e = new Unit("e", Team.Enemy) { MaxHp = 100, Hp = 100 };
        var p1 = new Unit("p1", Team.Player) { MaxHp = 100, Hp = 100, AggroWeight = 1 };
        var p2 = new Unit("p2", Team.Player) { MaxHp = 100, Hp = 100, AggroWeight = 1 };
        var p3 = new Unit("p3", Team.Player) { MaxHp = 100, Hp = 100, AggroWeight = 1 };
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
        eng.EndRound(); // 领主蓄力，意图前进

        Assert.AreEqual("attack", eng.GetIntent(lordId).CurrentIntentId, "第 2 回合亮普攻");
        Assert.IsTrue(eng.PlayCard(0, lordId)); // 打断领主
        eng.EndRound(); // 领主本回合不出手，意图不前移

        Assert.AreEqual("attack", eng.GetIntent(lordId).CurrentIntentId, "下回合重新亮同一意图");
    }

    // T9.3 存牌爆发点：跨回合存牌，同一回合多出，多效果同时成立
    [Test]
    public void Stored_Cards_Can_Be_Bursted_In_One_Round()
    {
        var eng = Make(1, new[] { Sword() }, new[] { Bag() },
            Card("weaken", "weaken", percent: 25), Card("shield", "shield", amount: 20));
        eng.StartBattle();
        eng.EndRound(); // 第 1 回合结束，已抽 2 张（weaken + shield）

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