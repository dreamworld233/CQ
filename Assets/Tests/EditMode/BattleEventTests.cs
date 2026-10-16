using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;

/// <summary>结算事件日志（表现层逐条播报用）的确定性断言。</summary>
public class BattleEventTests
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

    private static CharacterConfig C(string id, int hp, int speed, int atk, bool aoe = false) => new CharacterConfig
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
                damage = new DamageSpec { kind = "physical", target = aoe ? "all" : "single", amount = atk } }
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

    private static BattleEngine Make(long seed, IEnumerable<CharacterConfig> chars, IEnumerable<EnemyConfig> enemies)
    {
        var characterList = chars.ToList();
        AssignSlots(characterList);
        var list = enemies.ToList();
        return BattleEngine.Create(seed, characterList, list.Select(e => e.id).ToList(), list.ToDictionary(e => e.id),
            new CardConfig[0], SlotIds.Take(list.Count).ToArray(), TestFormation());
    }

    [Test]
    public void RoundEvents_Match_Per_Unit_Hp_Change()
    {
        var eng = Make(7, new[] { C("sword", 120, 10, 12), C("mage", 90, 8, 14) },
            new[] { E("grunt", 60, 4, 10), E("claw", 80, 6, 9) });
        eng.StartBattle();

        var before = eng.Ctx.Units.ToDictionary(u => u.Id, u => u.Hp);
        BattleTestDriver.CompleteRound(eng);

        Assert.IsNotEmpty(eng.RoundEvents, "结算后应有事件");

        var sum = new Dictionary<string, int>();
        foreach (var ev in eng.RoundEvents)
        {
            Assert.IsNotNull(ev.ActionId, "事件必须带动作 id");
            Assert.IsNotEmpty(ev.ActionId);
            if (ev.TargetId == null) continue;
            sum.TryGetValue(ev.TargetId, out int acc);
            sum[ev.TargetId] = acc + ev.HpDelta;
        }

        foreach (var u in eng.Ctx.Units)
        {
            sum.TryGetValue(u.Id, out int logged);
            Assert.AreEqual(u.Hp - before[u.Id], logged, u.Id + " 的事件血量变化合计应等于实际变化");
        }
    }

    [Test]
    public void Aoe_Action_Emits_One_Event_Per_Target()
    {
        var eng = Make(3, new[] { C("mage", 90, 20, 11, aoe: true) },
            new[] { E("e0", 60, 1, 1), E("e1", 60, 1, 1), E("e2", 60, 1, 1) });
        eng.StartBattle();
        BattleTestDriver.CompleteRound(eng);

        var hits = eng.RoundEvents.Where(e => e.Kind == BattleEventKind.Skill && e.HpDelta < 0).ToList();
        Assert.AreEqual(3, hits.Count, "全体普攻应产生 3 条命中事件");
        Assert.AreEqual(3, hits.Select(h => h.TargetId).Distinct().Count(), "三条事件目标各不相同");
        Assert.IsTrue(hits.All(h => h.ActionId == "mage_basic"));
    }

    [Test]
    public void Enemy_Turn_Emits_Event_With_Intent_Type()
    {
        var eng = Make(5, new[] { C("sword", 120, 1, 1) }, new[] { E("grunt", 60, 20, 13) });
        eng.StartBattle();
        BattleTestDriver.CompleteRound(eng);

        var ev = eng.RoundEvents.FirstOrDefault(e => e.Kind == BattleEventKind.Intent);
        Assert.IsNotNull(ev, "敌方出手应有意图事件");
        Assert.AreEqual("attack", ev.ActionId);
        Assert.AreEqual("sword", ev.TargetId, "单体攻击事件应指向执行时选中的玩家");
        Assert.AreEqual(-13, ev.HpDelta);
    }

    [Test]
    public void Later_Enemy_Targets_Living_Player_After_Earlier_Enemy_Kill()
    {
        var eng = Make(5, new[] { C("fragile", 10, 1, 0), C("survivor", 100, 1, 0) },
            new[] { E("killer", 100, 20, 20), E("follower", 100, 10, 7) });
        eng.StartBattle();
        eng.Ctx.FormationOf(Team.Player).GetSlot("front2").AddModifier(-100, 1);
        BattleTestDriver.CompleteRound(eng);

        var hits = eng.RoundEvents.Where(e => e.Kind == BattleEventKind.Intent && e.HpDelta < 0).ToList();
        Assert.AreEqual(2, hits.Count);
        Assert.AreEqual("killer_0", hits[0].ActorId);
        Assert.AreEqual("fragile", hits[0].TargetId);
        Assert.AreEqual(-10, hits[0].HpDelta, "过量伤害只记录实际扣血");
        Assert.IsTrue(hits[0].TargetDead);
        Assert.AreEqual("follower_1", hits[1].ActorId);
        Assert.AreEqual("survivor", hits[1].TargetId, "执行时不得选择已死亡的旧候选");
        Assert.AreEqual(-7, hits[1].HpDelta);
        Assert.IsFalse(hits[1].TargetDead);
        Assert.AreEqual(93, eng.Ctx.GetUnit("survivor").Hp);
        Assert.AreSame(eng.Ctx.GetUnit("fragile"), eng.Ctx.FormationOf(Team.Player).GetSlot("front1").Occupant,
            "死亡后保留槽位，不补位");
        Assert.AreEqual(0, eng.Ctx.FormationOf(Team.Player).GetSlot("front1").EffectiveTaunt);
    }

    [Test]
    public void RoundEvents_Reset_To_Current_Round_Only()
    {
        var eng = Make(11, new[] { C("sword", 120, 10, 12) }, new[] { E("grunt", 200, 4, 10) });
        eng.StartBattle();

        BattleTestDriver.CompleteRound(eng);
        int firstRound = eng.RoundEvents.Count;
        Assert.Greater(firstRound, 0);

        BattleTestDriver.CompleteRound(eng);
        Assert.AreEqual(firstRound, eng.RoundEvents.Count, "事件列表应每回合清空，不跨回合堆积");
    }
}
