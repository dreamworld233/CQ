using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;

public class BattleEngineTests
{
    private static CharacterConfig C(string id, int hp, int speed, int atk) => new CharacterConfig
    {
        id = id,
        name = id,
        role = "dps",
        maxHp = hp,
        baseSpeed = speed,
        maxEnergy = 3,
        maxUlt = 3,
        row = "front",
        aggroWeight = 2,
        skills = new List<SkillSpec>
        {
            new SkillSpec { id = id + "_basic", name = "普攻", type = "basic", energyDelta = 1, ultDelta = 1,
                damage = new DamageSpec { kind = "physical", target = "single", amount = atk } },
            new SkillSpec { id = id + "_skill", name = "技能", type = "skill", energyCost = 2, ultDelta = 1,
                damage = new DamageSpec { kind = "physical", target = "single", amount = atk * 2 } },
            new SkillSpec { id = id + "_ult", name = "终结技", type = "ult",
                damage = new DamageSpec { kind = "physical", target = "all", amount = atk * 3 } }
        }
    };

    private static EnemyConfig E(string id, int hp, int speed, int atk) => new EnemyConfig
    {
        id = id,
        name = id,
        maxHp = hp,
        baseSpeed = speed,
        row = "front",
        aggroWeight = 2,
        intentSequence = new[] { "attack" },
        intents = new List<IntentConfig>
        {
            new IntentConfig { id = "attack", type = "attack", target = "single", damage = atk }
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

    private static CardConfig Interrupt(int count) => new CardConfig
    {
        id = "interrupt",
        name = "打断",
        effect = new CardEffectSpec { type = "interrupt" },
        count = count
    };

    private static BattleEngine Make(long seed, IEnumerable<CharacterConfig> chars, IEnumerable<EnemyConfig> enemies, params CardConfig[] cards)
    {
        var enemyList = enemies.ToList();
        var ids = enemyList.Select(e => e.id).ToList();
        var catalog = enemyList.ToDictionary(e => e.id);
        return BattleEngine.Create(seed, chars, ids, catalog, cards);
    }

    private static void RunToEnd(BattleEngine eng, int maxRounds = 200)
    {
        for (int i = 0; i < maxRounds && !eng.IsFinished; i++) eng.EndRound();
        Assert.IsTrue(eng.IsFinished, "未在 " + maxRounds + " 回合内结束");
    }

    [Test]
    public void AutoBasic_PlayerWins_AgainstOneGrunt()
    {
        var eng = Make(1, new[] { C("sword", 100, 10, 12) }, new[] { E("grunt", 60, 4, 10) });
        eng.StartBattle();

        RunToEnd(eng);

        Assert.AreEqual(Team.Player, eng.Winner);
    }

    [Test]
    public void Interrupt_SkipsTurn_And_Cancels_Interruptible_Charge()
    {
        var eng = Make(1, new[] { C("sword", 100, 10, 12) }, new[] { Lord() }, Interrupt(2));
        eng.StartBattle();
        string lordId = eng.Enemies[0].Id;

        // 回合 1：剑士普攻，领主蓄力（无伤害）
        eng.EndRound();
        Assert.AreEqual(288, eng.Ctx.GetUnit(lordId).Hp, "剑士 12 命中领主");

        // 回合 2：打打断卡到领主 → 领主跳过本轮，蓄力被取消，不释放
        Assert.IsTrue(eng.PlayCard(0, lordId));
        eng.EndRound();

        Assert.AreEqual(276, eng.Ctx.GetUnit(lordId).Hp, "剑士再命中 12");
        Assert.AreEqual(100, eng.Ctx.GetUnit("sword").Hp, "领主蓄力被取消、本轮未出手，剑士无伤");
    }

    [Test]
    public void Energy_And_Ult_Gate_Skill_Selection()
    {
        var eng = Make(1, new[] { C("sword", 100, 10, 12) }, new[] { E("grunt", 60, 4, 10) });
        eng.StartBattle();
        string target = eng.Enemies[0].Id;

        // 初始 0 能量 / 0 大招条
        Assert.IsTrue(eng.ChooseSkill("sword", "sword_basic", target));
        Assert.IsFalse(eng.ChooseSkill("sword", "sword_skill", target), "0 能量不可放技能");
        Assert.IsFalse(eng.ChooseSkill("sword", "sword_ult", target), "0 大招条不可放终结技");
    }

    [Test]
    public void Draw_OnePerRound_CapsAtDeckSize()
    {
        var bag = E("bag", 99999, 1, 0);
        var eng = Make(1, new[] { C("sword", 100, 10, 12) }, new[] { bag }, Interrupt(2));
        eng.StartBattle();

        Assert.AreEqual(1, eng.Hand.Count, "首回合抽 1");

        eng.EndRound();
        eng.EndRound();

        Assert.AreEqual(2, eng.Hand.Count, "抽牌堆空且弃牌空，手牌封顶在卡组总数");
    }

    [Test]
    public void EnemyWins_WhenAllPlayersDead()
    {
        var boss = E("boss", 9999, 99, 999);
        var eng = Make(1, new[] { C("sword", 100, 10, 12) }, new[] { boss });
        eng.StartBattle();

        RunToEnd(eng);

        Assert.AreEqual(Team.Enemy, eng.Winner);
    }

    [Test]
    public void SameSeed_SameBattle_State()
    {
        var chars = new[] { C("c0", 100, 10, 12), C("c1", 160, 6, 8), C("c2", 80, 8, 10) };
        var enemies = new[] { E("e0", 60, 5, 8), E("e1", 60, 5, 8), E("e2", 60, 5, 8) };

        var a = Make(42, chars, enemies);
        var b = Make(42, chars, enemies);
        a.StartBattle();
        b.StartBattle();

        RunToEnd(a);
        RunToEnd(b);

        string StateOf(BattleEngine e) =>
            string.Join(",", e.Ctx.Units.Select(u => u.Id + "=" + u.Hp + ":" + u.Energy + ":" + u.Ult));

        Assert.AreEqual(a.Winner, b.Winner);
        Assert.AreEqual(StateOf(a), StateOf(b));
    }
}