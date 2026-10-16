using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;

public class BattleEngineTests
{
    private static readonly string[] SlotIds = { "front1", "front2", "back1", "back2", "back3" };

    private static FormationConfig TestFormation() =>
        new FormationConfig { frontBaseTaunt = 100, backBaseTaunt = 50 };

    private static void AssignSlots(IReadOnlyList<CharacterConfig> characters)
    {
        Assert.LessOrEqual(characters.Count, SlotIds.Length);
        var used = new HashSet<string>(characters.Where(c => c.defaultSlot != null).Select(c => c.defaultSlot));
        foreach (var c in characters)
            if (c.defaultSlot == null)
            {
                c.defaultSlot = SlotIds.First(slot => !used.Contains(slot));
                used.Add(c.defaultSlot);
            }
        Assert.AreEqual(characters.Count, characters.Select(c => c.defaultSlot).Distinct().Count());
        Assert.IsTrue(characters.All(c => SlotIds.Contains(c.defaultSlot)));
    }

    private static CharacterConfig C(string id, int hp, int speed, int atk) => new CharacterConfig
    {
        id = id,
        name = id,
        role = "dps",
        maxHp = hp,
        baseSpeed = speed,
        ultChargeTrigger = "attack", ultChargeAmount = 1,
        maxUlt = 3,
        skills = new List<SkillSpec>
        {
            new SkillSpec { id = id + "_basic", name = "普攻", type = "basic",
                damage = new DamageSpec { kind = "physical", target = "single", amount = atk } },
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
        var characterList = chars.ToList();
        AssignSlots(characterList);
        var enemyList = enemies.ToList();
        var ids = enemyList.Select(e => e.id).ToList();
        var catalog = enemyList.ToDictionary(e => e.id);
        return BattleEngine.Create(seed, characterList, ids, catalog, cards,
            SlotIds.Take(ids.Count).ToArray(), TestFormation());
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
    public void Ultimate_Gated_Immediate_And_Does_Not_Replace_Basic()
    {
        var eng = Make(1, new[] { C("sword", 100, 10, 12) }, new[] { E("grunt", 600, 4, 10) });
        eng.StartBattle();
        Assert.IsFalse(eng.UseUltimate("sword"));
        eng.Players[0].GainUlt(3);
        Assert.IsTrue(eng.UseUltimate("sword"));
        Assert.AreEqual(564, eng.Enemies[0].Hp, "终结技立即结算");
        Assert.AreEqual(BattleFlow.Input, eng.Flow);
        Assert.AreEqual(0, eng.Players[0].Ult);
        eng.EndRound();
        Assert.AreEqual(552, eng.Enemies[0].Hp, "正常普攻仍执行");
        Assert.AreEqual(1, eng.Players[0].Ult);
        Assert.AreEqual(3, eng.ActionResults.Count);
        var sequence = eng.ActionResults.Last().Sequence;
        eng.EndRound();
        Assert.Greater(eng.ActionResults.Last().Sequence, sequence);
        CollectionAssert.AreEqual(Enumerable.Range(1, eng.ActionResults.Count).Select(i => (long)i),
            eng.ActionResults.Select(r => r.Sequence));
    }

    [Test]
    public void Ultimate_Can_Be_Recharged_And_Used_Twice_In_Same_Round()
    {
        var config = C("guard", 100, 10, 5);
        config.maxUlt = 1; config.ultChargeTrigger = "hit";
        var eng = Make(1, new[] { config }, new[] { E("bag", 1000, 1, 1) });
        eng.StartBattle();
        eng.Players[0].GainUlt(1);
        Assert.IsTrue(eng.UseUltimate("guard"));
        new EnemyActionResolver().Resolve(eng.Ctx, eng.Enemies[0].Id,
            new IntentConfig { type = "attack", damage = 1 });
        Assert.AreEqual(1, eng.Players[0].Ult);
        Assert.IsTrue(eng.UseUltimate("guard"));
        Assert.AreEqual(0, eng.Round, "两次终结技都仍在同一策略窗口");
        Assert.AreEqual(0, eng.Players[0].Ult);
        Assert.AreEqual(2, eng.ActionResults.Count(r => r.ActionId == "guard_ult"));
    }

    [Test]
    public void Lethal_Ultimate_Finishes_Immediately_Without_Starting_Normal_Round()
    {
        var eng = Make(1, new[] { C("sword", 100, 10, 12) }, new[] { E("grunt", 20, 1, 10) });
        eng.StartBattle(); eng.Players[0].GainUlt(3);
        Assert.IsTrue(eng.UseUltimate("sword"));
        Assert.IsTrue(eng.IsFinished); Assert.AreEqual(Team.Player, eng.Winner);
        Assert.AreEqual(BattleFlow.Finished, eng.Flow);
        Assert.IsFalse(eng.UseUltimate("sword"));
        eng.EndRound();
        Assert.AreEqual(1, eng.ActionResults.Count); Assert.AreEqual(0, eng.Round);
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
            string.Join(",", e.Ctx.Units.Select(u => u.Id + "=" + u.Hp + ":" + u.Ult));

        Assert.AreEqual(a.Winner, b.Winner);
        Assert.AreEqual(StateOf(a), StateOf(b));
    }

    [Test]
    public void IntentTarget_Uses_ExecutionTime_Taunt_And_Emits_Target_Event()
    {
        var eng = Make(1, new[] { C("sword", 100, 10, 12), C("c1", 160, 6, 8) }, new[] { E("grunt", 60, 4, 10) });
        eng.StartBattle();

        Assert.IsNotNull(eng.CurrentIntentOf(eng.Enemies[0].Id), "输入阶段仍应展示意图");
        // 意图展示之后才改变槽位嘲讽；唯一非零候选必须在执行时被选中。
        var formation = eng.Ctx.FormationOf(Team.Player);
        formation.GetSlot("front1").AddModifier(-100, 1);
        eng.EndRound();

        var hit = eng.RoundEvents.Single(e => e.Kind == BattleEventKind.Intent && e.HpDelta < 0);
        Assert.AreEqual(eng.Enemies[0].Id, hit.ActorId);
        Assert.AreEqual("attack", hit.ActionId);
        Assert.AreEqual("c1", hit.TargetId);
        Assert.AreEqual(-10, hit.HpDelta);
        Assert.AreEqual(100, eng.Ctx.GetUnit("sword").Hp);
        Assert.AreEqual(150, eng.Ctx.GetUnit("c1").Hp);
    }

    [Test]
    public void Create_Uses_Character_DefaultSlots_And_Explicit_EnemySlots()
    {
        var front = C("front", 100, 10, 1);
        var back = C("back", 100, 9, 1);
        front.defaultSlot = "front2";
        back.defaultSlot = "back3";
        var enemy = E("grunt", 100, 1, 1);
        var eng = BattleEngine.Create(7, new[] { front, back }, new[] { "grunt", "grunt" },
            new Dictionary<string, EnemyConfig> { { "grunt", enemy } }, new CardConfig[0],
            new[] { "back2", "front1" }, TestFormation());

        Assert.AreEqual("front2", eng.Players[0].SlotId);
        Assert.AreEqual("back3", eng.Players[1].SlotId);
        Assert.AreEqual("front", eng.Players[0].Row);
        Assert.AreEqual("back", eng.Players[1].Row);
        Assert.AreEqual("back2", eng.Enemies[0].SlotId);
        Assert.AreEqual("front1", eng.Enemies[1].SlotId);
        Assert.AreNotEqual(eng.Enemies[0].Id, eng.Enemies[1].Id);
        Assert.AreSame(eng.Players[1], eng.Ctx.FormationOf(Team.Player).GetSlot("back3").Occupant);
        Assert.AreSame(eng.Enemies[0], eng.Ctx.FormationOf(Team.Enemy).GetSlot("back2").Occupant);
        Assert.AreEqual(100, eng.Ctx.FormationOf(Team.Player).GetSlot("front2").BaseTaunt);
        Assert.AreEqual(50, eng.Ctx.FormationOf(Team.Enemy).GetSlot("back2").BaseTaunt);
    }

    [Test]
    public void Create_Wave_Overload_Preserves_Explicit_Slots()
    {
        var character = C("hero", 100, 10, 1);
        character.defaultSlot = "back1";
        var enemy = E("grunt", 100, 1, 1);
        var wave = new WaveConfig { enemies = new[] { "grunt" }, slots = new[] { "back3" } };
        var eng = BattleEngine.Create(7, new[] { character }, wave,
            new Dictionary<string, EnemyConfig> { { "grunt", enemy } }, new CardConfig[0], TestFormation());

        Assert.AreEqual("back1", eng.Players[0].SlotId);
        Assert.AreEqual("back3", eng.Enemies[0].SlotId);
    }

    [Test]
    public void HealSingle_Heals_Selected_Ally_Rejects_Enemy()
    {
        var healer = new CharacterConfig
        {
            id = "healer", name = "治疗", role = "support", maxHp = 100, baseSpeed = 10,
            maxUlt = 3, ultChargeTrigger = "hit", ultChargeAmount = 1, defaultSlot = "back1",
            skills = new List<SkillSpec>
            {
                new SkillSpec { id = "healer_ult", name = "治疗", type = "ult",
                    heal = new HealSpec { target = "single", amount = 20 } }
            }
        };
        var buddy = new CharacterConfig
        {
            id = "buddy", name = "伙伴", role = "dps", maxHp = 80, baseSpeed = 8,
            maxUlt = 3, ultChargeTrigger = "hit", ultChargeAmount = 1, defaultSlot = "front1",
            skills = new List<SkillSpec>
            {
                new SkillSpec { id = "buddy_basic", name = "普攻", type = "basic",
                    damage = new DamageSpec { kind = "physical", target = "single", amount = 5 } }
            }
        };

        var eng = Make(1, new[] { healer, buddy }, new[] { E("foe", 999, 0, 0) });
        eng.StartBattle();
        eng.Ctx.GetUnit("buddy").TakeDamage(50);

        eng.Players[0].GainUlt(3);
        Assert.IsFalse(eng.UseUltimate("healer", "foe_0"), "治疗不可指向敌方");
        Assert.IsTrue(eng.UseUltimate("healer", "buddy"), "治疗应能选友方");

        eng.EndRound();

        Assert.AreEqual(50, eng.Ctx.GetUnit("buddy").Hp, "治疗回 30→50");
    }
}