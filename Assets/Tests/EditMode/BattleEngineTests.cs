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
        for (int i = 0; i < maxRounds && !eng.IsFinished; i++) BattleTestDriver.CompleteRound(eng);
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
        BattleTestDriver.CompleteRound(eng);
        Assert.AreEqual(288, eng.Ctx.GetUnit(lordId).Hp, "剑士 12 命中领主");

        // 回合 2：打打断卡到领主 → 领主跳过本轮，蓄力被取消，不释放
        Assert.IsTrue(eng.PlayCard(0, lordId));
        BattleTestDriver.CompleteRound(eng);

        Assert.AreEqual(276, eng.Ctx.GetUnit(lordId).Hp, "剑士再命中 12");
        Assert.AreEqual(100, eng.Ctx.GetUnit("sword").Hp, "领主蓄力被取消、本轮未出手，剑士无伤");
    }

    [Test]
    public void Ultimate_Gated_Confirmed_And_Does_Not_Replace_Basic()
    {
        var eng = Make(1, new[] { C("sword", 100, 10, 12) }, new[] { E("grunt", 600, 4, 10) });
        eng.StartBattle();
        Assert.IsFalse(BattleTestDriver.RequestAndPlay(eng, "sword"));
        eng.Players[0].GainUlt(3);
        Assert.IsTrue(BattleTestDriver.RequestAndPlay(eng, "sword"));
        Assert.AreEqual(564, eng.Enemies[0].Hp, "终结技经推进与确认完成结算");
        Assert.AreEqual(BattleFlow.Input, eng.Flow);
        Assert.AreEqual(0, eng.Players[0].Ult);
        BattleTestDriver.CompleteRound(eng);
        Assert.AreEqual(552, eng.Enemies[0].Hp, "正常普攻仍执行");
        Assert.AreEqual(1, eng.Players[0].Ult);
        Assert.AreEqual(3, eng.ActionResults.Count);
        var sequence = eng.ActionResults.Last().Sequence;
        BattleTestDriver.CompleteRound(eng);
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
        Assert.IsTrue(BattleTestDriver.RequestAndPlay(eng, "guard"));
        new EnemyActionResolver().Resolve(eng.Ctx, eng.Enemies[0].Id,
            new IntentConfig { type = "attack", damage = 1 });
        Assert.AreEqual(1, eng.Players[0].Ult);
        Assert.IsTrue(BattleTestDriver.RequestAndPlay(eng, "guard"));
        Assert.AreEqual(0, eng.Round, "两次终结技都仍在同一策略窗口");
        Assert.AreEqual(0, eng.Players[0].Ult);
        Assert.AreEqual(2, eng.ActionResults.Count(r => r.ActionId == "guard_ult"));
    }

    [Test]
    public void Lethal_Ultimate_Finishes_Immediately_Without_Starting_Normal_Round()
    {
        var eng = Make(1, new[] { C("sword", 100, 10, 12) }, new[] { E("grunt", 20, 1, 10) });
        eng.StartBattle(); eng.Players[0].GainUlt(3);
        Assert.IsTrue(BattleTestDriver.RequestAndPlay(eng, "sword"));
        Assert.IsTrue(eng.IsFinished); Assert.AreEqual(Team.Player, eng.Winner);
        Assert.AreEqual(BattleFlow.Finished, eng.Flow);
        Assert.IsFalse(BattleTestDriver.RequestAndPlay(eng, "sword"));
        BattleTestDriver.CompleteRound(eng);
        Assert.AreEqual(1, eng.ActionResults.Count); Assert.AreEqual(0, eng.Round);
    }

    [Test]
    public void Draw_OnePerRound_CapsAtDeckSize()
    {
        var bag = E("bag", 99999, 1, 0);
        var eng = Make(1, new[] { C("sword", 100, 10, 12) }, new[] { bag }, Interrupt(2));
        eng.StartBattle();

        Assert.AreEqual(1, eng.Hand.Count, "首回合抽 1");

        BattleTestDriver.CompleteRound(eng);
        BattleTestDriver.CompleteRound(eng);

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
        BattleTestDriver.CompleteRound(eng);

        var hit = eng.RoundEvents.Single(e => e.Kind == BattleEventKind.Intent && e.HpDelta < 0);
        Assert.AreEqual(eng.Enemies[0].Id, hit.ActorId);
        Assert.AreEqual("attack", hit.ActionId);
        Assert.AreEqual("c1", hit.TargetId);
        Assert.AreEqual(-10, hit.HpDelta);
        Assert.AreEqual(100, eng.Ctx.GetUnit("sword").Hp);
        Assert.AreEqual(150, eng.Ctx.GetUnit("c1").Hp);
    }

    [Test]
    public void Advance_Requires_Exact_Once_Per_Battle_Confirmation()
    {
        var a = Make(1, new[] { C("a", 100, 10, 1) }, new[] { E("foe", 1000, 1, 1) });
        var b = Make(1, new[] { C("a", 100, 10, 1) }, new[] { E("foe", 1000, 1, 1) });
        a.StartBattle(); b.StartBattle();
        Assert.IsTrue(a.BeginActions()); Assert.IsTrue(b.BeginActions());
        var result = a.Advance(); var other = b.Advance();
        Assert.IsNotNull(result); Assert.AreEqual(result.Sequence, other.Sequence);
        Assert.IsNull(a.Advance()); Assert.AreEqual(1, a.ActionResults.Count);
        Assert.IsFalse(a.BeginActions());
        Assert.IsFalse(a.Confirm(b.BattleId, result.Sequence));
        Assert.IsFalse(a.Confirm(a.BattleId, result.Sequence + 1));
        Assert.AreSame(result, a.CurrentAction);
        Assert.IsTrue(a.Confirm(a.BattleId, result.Sequence));
        Assert.IsFalse(a.Confirm(a.BattleId, result.Sequence));
        var next = a.Advance();
        Assert.AreEqual("foe_0", next.ActorId);
        Assert.IsFalse(a.Confirm(a.BattleId, result.Sequence));
        Assert.AreSame(next, a.CurrentAction);
    }

    [Test]
    public void Input_Ultimate_Stays_Input_And_Blocks_Cards_Until_Confirmed()
    {
        var eng = Make(1, new[] { C("a", 100, 10, 1) }, new[] { E("foe", 1000, 1, 1) }, Interrupt(3));
        eng.StartBattle(); eng.Players[0].GainUlt(3);
        Assert.IsTrue(eng.RequestUltimate("a"));
        Assert.AreEqual(1000, eng.Enemies[0].Hp);
        Assert.IsFalse(eng.BeginActions()); Assert.IsFalse(eng.PlayCard(0, "foe_0"));
        var result = eng.Advance();
        Assert.AreEqual("a_ult", result.ActionId);
        Assert.AreEqual(BattleFlow.Input, eng.Flow);
        Assert.IsFalse(eng.PlayCard(0, "foe_0"));
        Assert.IsTrue(eng.Confirm(eng.BattleId, result.Sequence));
        Assert.IsNull(eng.Advance()); Assert.IsNull(eng.Advance());
        Assert.AreEqual(0, eng.Round); Assert.AreEqual(1, eng.ActionResults.Count);
        Assert.AreEqual(1, eng.Hand.Count);
        Assert.IsTrue(eng.PlayCard(0, "foe_0"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Newly_Full_After_Basic_Inserts_Before_Next_Normal(bool requestAfterConfirm)
    {
        var config = C("a", 100, 10, 1); config.maxUlt = 1;
        var eng = Make(1, new[] { config }, new[] { E("foe", 1000, 1, 1) });
        eng.StartBattle(); Assert.IsTrue(eng.BeginActions());
        var basic = eng.Advance(); Assert.AreEqual("a_basic", basic.ActionId);
        Assert.AreEqual(1, eng.Players[0].Ult);
        if (requestAfterConfirm) Assert.IsTrue(eng.Confirm(eng.BattleId, basic.Sequence));
        Assert.IsTrue(eng.RequestUltimate("a"));
        if (!requestAfterConfirm)
        {
            Assert.IsNull(eng.Advance());
            Assert.IsTrue(eng.Confirm(eng.BattleId, basic.Sequence));
        }
        var ultimate = eng.Advance(); Assert.AreEqual("a_ult", ultimate.ActionId);
        Assert.IsTrue(eng.Confirm(eng.BattleId, ultimate.Sequence));
        Assert.AreEqual("foe_0", eng.Advance().ActorId);
    }

    [TestCase("before")]
    [TestCase("player")]
    [TestCase("enemy")]
    public void Ready_B_Inserts_At_Next_Complete_Action_Boundary(string boundary)
    {
        var eng = Make(1, new[] { C("a", 100, 10, 1), C("b", 100, 2, 1) },
            new[] { E("foe", 1000, boundary == "enemy" ? 20 : 5, 1) });
        eng.StartBattle(); eng.Ctx.GetUnit("b").GainUlt(3);
        Assert.IsTrue(eng.BeginActions());
        ActionResult current = boundary == "before" ? null : eng.Advance();
        if (current != null) Assert.AreEqual(boundary == "enemy" ? "foe_0" : "a", current.ActorId);
        Assert.IsTrue(eng.RequestUltimate("b"));
        if (current != null)
        {
            Assert.IsNull(eng.Advance());
            Assert.IsTrue(eng.Confirm(eng.BattleId, current.Sequence));
        }
        var ult = eng.Advance(); Assert.AreEqual("b_ult", ult.ActionId);
        Assert.IsTrue(eng.Confirm(eng.BattleId, ult.Sequence));
        BattleTestDriver.Drain(eng);
        Assert.AreEqual(1, eng.ActionResults.Count(r => r.ActionId == "a_basic"));
        Assert.AreEqual(1, eng.ActionResults.Count(r => r.ActionId == "b_basic"));
        Assert.AreEqual(1, eng.ActionResults.Count(r => r.ActorId == "foe_0"));
    }

    [Test]
    public void Fifo_Ignores_Speed_And_Normal_Order_Is_Frozen()
    {
        var eng = Make(3, new[] { C("fast", 100, 10, 1), C("slow", 100, 2, 1) },
            new[] { E("foe", 1000, 5, 1) });
        eng.StartBattle(); Assert.IsTrue(eng.BeginActions());
        var frozen = eng.TurnOrder.Select(u => u.Id).ToArray();
        eng.Ctx.SetHaste("slow", 1000);
        foreach (var player in eng.Players) player.GainUlt(3);
        Assert.IsTrue(eng.RequestUltimate("slow")); Assert.IsTrue(eng.RequestUltimate("fast"));
        CollectionAssert.AreEqual(frozen, eng.CurrentOrder.Select(u => u.Id));
        BattleTestDriver.Drain(eng);
        CollectionAssert.AreEqual(new[] { "slow_ult", "fast_ult", "fast_basic", "attack", "slow_basic" },
            eng.ActionResults.Select(r => r.ActionId));
        CollectionAssert.AreEqual(frozen, eng.TurnOrder.Select(u => u.Id));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Last_Normal_Accepts_Request_Before_Or_After_Confirm_And_Ticks_Once(bool afterConfirm)
    {
        var eng = Make(1, new[] { C("a", 100, 10, 1) }, new[] { E("foe", 1000, 1, 1) }, Interrupt(5));
        eng.StartBattle();
        eng.Ctx.AddStatus("a", new StatusEffect { Type = StatusType.AttackUp, Percent = 1, Duration = 2, Turn = "round" });
        eng.Ctx.FormationOf(Team.Player).GetSlot("front1").AddModifier(20, 2);
        Assert.IsTrue(eng.BeginActions());
        var first = eng.Advance(); Assert.IsTrue(eng.Confirm(eng.BattleId, first.Sequence));
        var last = eng.Advance(); Assert.AreEqual("foe_0", last.ActorId);
        eng.Players[0].GainUlt(3);
        if (afterConfirm) Assert.IsTrue(eng.Confirm(eng.BattleId, last.Sequence));
        Assert.IsTrue(eng.RequestUltimate("a"));
        Assert.AreEqual(1, eng.Hand.Count);
        if (!afterConfirm) Assert.IsTrue(eng.Confirm(eng.BattleId, last.Sequence));
        var ult = eng.Advance(); Assert.AreEqual("a_ult", ult.ActionId);
        Assert.AreEqual(2, eng.Ctx.FindStatus("a", StatusType.AttackUp).Duration);
        Assert.AreEqual(1, eng.Hand.Count);
        Assert.IsTrue(eng.Confirm(eng.BattleId, ult.Sequence));
        Assert.IsNull(eng.Advance()); Assert.IsNull(eng.Advance());
        Assert.AreEqual(BattleFlow.Input, eng.Flow); Assert.AreEqual(2, eng.Hand.Count);
        Assert.AreEqual(1, eng.Ctx.FindStatus("a", StatusType.AttackUp).Duration);
        Assert.AreEqual(120, eng.Ctx.EffectiveTaunt(eng.Players[0]));
        Assert.IsTrue(eng.BeginActions()); BattleTestDriver.Drain(eng);
        Assert.AreEqual(100, eng.Ctx.EffectiveTaunt(eng.Players[0]));
    }

    [TestCase(0)]
    [TestCase(2)]
    [TestCase(3)]
    public void Reservation_Does_Not_Recheck_Or_Spend_Recharge_And_Unlocks_Only_On_Confirm(int recharge)
    {
        var eng = Make(1, new[] { C("a", 100, 10, 1) }, new[] { E("foe", 1000, 1, 1) });
        eng.StartBattle(); var actor = eng.Players[0]; actor.GainUlt(3);
        Assert.IsTrue(eng.RequestUltimate("a")); Assert.AreEqual(0, actor.Ult);
        actor.GainUlt(recharge);
        Assert.IsFalse(eng.RequestUltimate("a"));
        Assert.IsTrue(eng.CanCancelUltimate("a"));
        var ult = eng.Advance(); Assert.IsNotNull(ult); Assert.AreEqual(recharge, actor.Ult);
        Assert.IsTrue(eng.HasUltimateRequest("a")); Assert.IsFalse(eng.CancelUltimate("a"));
        Assert.IsFalse(eng.RequestUltimate("a"));
        Assert.IsTrue(eng.Confirm(eng.BattleId, ult.Sequence));
        Assert.IsFalse(eng.HasUltimateRequest("a"));
        Assert.AreEqual(recharge == 3, eng.RequestUltimate("a"));
    }

    [Test]
    public void Waiting_Recharge_From_Actual_Hit_Survives_Reserved_Execution()
    {
        var config = C("a", 100, 10, 1); config.ultChargeTrigger = "hit";
        var eng = Make(1, new[] { config }, new[] { E("foe", 1000, 1, 1) });
        eng.StartBattle(); eng.Players[0].GainUlt(3);
        Assert.IsTrue(eng.RequestUltimate("a"));
        new EnemyActionResolver().Resolve(eng.Ctx, "foe_0", new IntentConfig { type = "attack", damage = 1 });
        Assert.AreEqual(1, eng.Players[0].Ult);
        var ult = eng.Advance(); Assert.AreEqual("a_ult", ult.ActionId);
        Assert.AreEqual(1, eng.Players[0].Ult);
    }

    [Test]
    public void Cancel_Refunds_Clamped_Once_And_Requeues_At_Tail()
    {
        var eng = Make(1, new[] { C("a", 100, 10, 1), C("b", 100, 2, 1) }, new[] { E("foe", 1000, 1, 1) });
        eng.StartBattle(); foreach (var actor in eng.Players) actor.GainUlt(3);
        Assert.IsTrue(eng.RequestUltimate("a")); Assert.IsTrue(eng.RequestUltimate("b"));
        eng.Players[0].GainUlt(2);
        Assert.IsTrue(eng.CancelUltimate("a")); Assert.AreEqual(3, eng.Players[0].Ult);
        eng.Players[0].GainUlt(-1);
        Assert.IsFalse(eng.CancelUltimate("a")); Assert.AreEqual(2, eng.Players[0].Ult);
        eng.Players[0].GainUlt(1); Assert.IsTrue(eng.RequestUltimate("a"));
        BattleTestDriver.Drain(eng);
        CollectionAssert.AreEqual(new[] { "b_ult", "a_ult" }, eng.ActionResults.Select(r => r.ActionId));
    }

    [TestCase("snipe")]
    [TestCase("heal")]
    public void Invalid_Explicit_Target_Is_Rejected_And_Dead_Target_Is_Not_Replaced(string mode)
    {
        var config = C("a", 100, 10, 1);
        var skill = config.skills[1];
        if (mode == "snipe") skill.damage.target = "snipe";
        else { skill.damage = null; skill.heal = new HealSpec { target = "single", amount = 10 }; }
        var eng = Make(1, new[] { config, C("b", 100, 2, 1) },
            new[] { E("foe", 1000, 1, 1), E("other", 1000, 1, 1) });
        eng.StartBattle(); eng.Players[0].GainUlt(3);
        string target = mode == "snipe" ? "foe_0" : "b";
        Assert.IsFalse(eng.RequestUltimate("a"));
        Assert.IsFalse(eng.RequestUltimate("a", mode == "snipe" ? "b" : "foe_0"));
        Assert.AreEqual(3, eng.Players[0].Ult); Assert.AreEqual(0, eng.PendingUltimateCount);
        Assert.IsTrue(eng.RequestUltimate("a", target));
        eng.Ctx.GetUnit(target).TakeDamage(9999);
        Assert.IsNull(eng.Advance()); Assert.IsFalse(eng.IsFinished);
        Assert.AreEqual(3, eng.Players[0].Ult); Assert.IsEmpty(eng.ActionResults);
        Assert.IsFalse(eng.HasUltimateRequest("a"));
        Assert.IsFalse(eng.CancelUltimate("a"));
        Assert.IsFalse(eng.RequestUltimate("a", target));
        Assert.AreEqual(3, eng.Players[0].Ult);
    }

    [Test]
    public void Ordinary_Single_Ultimate_Selects_Taunt_At_Execution_Not_Registration()
    {
        var config = C("a", 100, 10, 1); config.skills[1].damage.target = "single";
        var eng = Make(1, new[] { config }, new[] { E("foe", 1000, 1, 1), E("other", 1000, 1, 1) });
        eng.StartBattle(); eng.Players[0].GainUlt(3);
        Assert.IsTrue(eng.RequestUltimate("a", "foe_0"));
        eng.Ctx.FormationOf(Team.Enemy).GetSlot("front2").AddModifier(100, 1);
        var result = eng.Advance();
        Assert.AreEqual("other_1", result.Hits.Single().TargetUnitId);
    }

    [Test]
    public void Dead_Waiting_Caster_Refunds_Once_And_Surviving_Fifo_Continues()
    {
        var eng = Make(1, new[] { C("a", 100, 10, 1), C("b", 100, 2, 1) }, new[] { E("foe", 1000, 1, 1) });
        eng.StartBattle(); foreach (var actor in eng.Players) actor.GainUlt(3);
        Assert.IsTrue(eng.RequestUltimate("a")); Assert.IsTrue(eng.RequestUltimate("b"));
        eng.Players[0].TakeDamage(999);
        var result = eng.Advance(); Assert.AreEqual("b_ult", result.ActionId);
        Assert.AreEqual(3, eng.Players[0].Ult); Assert.IsFalse(eng.HasUltimateRequest("a"));
        eng.Players[0].GainUlt(-1); Assert.IsFalse(eng.CancelUltimate("a"));
        Assert.AreEqual(2, eng.Players[0].Ult);
        Assert.IsFalse(eng.RequestUltimate("a"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Winner_Clears_Waiting_And_Normal_But_Preserves_Current_For_Playback(bool playerWins)
    {
        var eng = Make(1, new[] { C("a", 100, 10, 1000), C("b", 100, 2, 1) },
            new[] { E("foe", playerWins ? 10 : 10000, playerWins ? 1 : 20, playerWins ? 1 : 1000) });
        eng.StartBattle(); Assert.IsTrue(eng.BeginActions());
        ActionResult current;
        if (playerWins)
        {
            foreach (var actor in eng.Players) actor.GainUlt(3);
            Assert.IsTrue(eng.RequestUltimate("a")); Assert.IsTrue(eng.RequestUltimate("b"));
            current = eng.Advance();
            Assert.IsTrue(eng.HasUltimateRequest("a"));
        }
        else
        {
            eng.Ctx.GetUnit("a").Hp = 0;
            current = eng.Advance();
        }
        Assert.IsTrue(eng.IsFinished); Assert.AreSame(current, eng.CurrentAction);
        Assert.AreEqual(playerWins ? Team.Player : Team.Enemy, eng.Winner);
        Assert.AreEqual(0, eng.PendingUltimateCount);
        if (playerWins) Assert.AreEqual(3, eng.Ctx.GetUnit("b").Ult);
        Assert.IsFalse(eng.RequestUltimate("b")); Assert.IsNull(eng.Advance());
        Assert.IsFalse(eng.Confirm(System.Guid.NewGuid(), current.Sequence));
        Assert.IsTrue(eng.Confirm(eng.BattleId, current.Sequence));
        Assert.IsFalse(eng.HasUltimateRequest("a"));
        Assert.IsNull(eng.Advance()); Assert.IsFalse(eng.BeginActions());
        Assert.AreEqual(1, eng.ActionResults.Count);
    }

    [Test]
    public void Dead_Normal_Queue_Entry_Is_Skipped_After_Inserted_Ultimate()
    {
        var config = C("a", 100, 10, 10); config.skills[1].damage.target = "snipe";
        var eng = Make(1, new[] { config }, new[] { E("foe", 20, 5, 1), E("other", 1000, 1, 1) });
        eng.StartBattle(); Assert.IsTrue(eng.BeginActions());
        eng.Players[0].GainUlt(3); Assert.IsTrue(eng.RequestUltimate("a", "foe_0"));
        BattleTestDriver.Drain(eng);
        Assert.IsFalse(eng.ActionResults.Any(r => r.ActorId == "foe_0"));
        Assert.AreEqual(1, eng.ActionResults.Count(r => r.ActionId == "a_basic"));
        Assert.AreEqual(1, eng.ActionResults.Count(r => r.ActorId == "other_1"));
    }

    [Test]
    public void Same_Seed_And_Boundary_Commands_Produce_Identical_Action_Trace()
    {
        BattleEngine Build()
        {
            var config = C("a", 100, 10, 1); config.maxUlt = 1;
            var engine = Make(81, new[] { config, C("b", 100, 2, 1) },
                new[] { E("foe", 1000, 5, 1), E("other", 1000, 5, 1) });
            engine.StartBattle(); return engine;
        }
        var a = Build(); var b = Build();
        for (int round = 0; round < 3; round++)
        {
            Assert.IsTrue(a.BeginActions()); Assert.IsTrue(b.BeginActions());
            for (int step = 0; step < 100; step++)
            {
                var left = a.Advance(); var right = b.Advance();
                if (left == null || right == null) { Assert.IsNull(left); Assert.IsNull(right); break; }
                Assert.AreEqual(left.Sequence, right.Sequence); Assert.AreEqual(left.ActionId, right.ActionId);
                Assert.AreEqual(left.ActorId, right.ActorId);
                CollectionAssert.AreEqual(left.Hits.Select(h => h.TargetUnitId + ":" + h.HpDamage),
                    right.Hits.Select(h => h.TargetUnitId + ":" + h.HpDamage));
                if (step == 0)
                {
                    Assert.AreEqual(a.RequestUltimate("a"), b.RequestUltimate("a"));
                    Assert.IsFalse(a.RequestUltimate("a")); Assert.IsFalse(b.RequestUltimate("a"));
                }
                if (step == 1)
                {
                    a.Players[1].GainUlt(3); b.Players[1].GainUlt(3);
                    Assert.IsTrue(a.RequestUltimate("b")); Assert.IsTrue(b.RequestUltimate("b"));
                    Assert.IsTrue(a.CancelUltimate("b")); Assert.IsTrue(b.CancelUltimate("b"));
                    Assert.IsTrue(a.RequestUltimate("b")); Assert.IsTrue(b.RequestUltimate("b"));
                }
                Assert.IsTrue(a.Confirm(a.BattleId, left.Sequence)); Assert.IsTrue(b.Confirm(b.BattleId, right.Sequence));
                Assert.Less(step, 99);
            }
            CollectionAssert.AreEqual(a.Ctx.Units.Select(u => u.Id + ":" + u.Hp + ":" + u.Ult),
                b.Ctx.Units.Select(u => u.Id + ":" + u.Hp + ":" + u.Ult));
        }
    }

    [Test]
    public void Executing_Recharged_Actor_Requeues_Only_After_Confirm_At_Fifo_Tail()
    {
        var eng = Make(1, new[] { C("a", 100, 10, 1), C("b", 100, 2, 1) }, new[] { E("foe", 1000, 1, 1) });
        eng.StartBattle(); Assert.IsTrue(eng.BeginActions());
        foreach (var actor in eng.Players) actor.GainUlt(3);
        Assert.IsTrue(eng.RequestUltimate("a"));
        var current = eng.Advance(); eng.Players[0].GainUlt(3);
        Assert.IsTrue(eng.RequestUltimate("b"));
        Assert.IsFalse(eng.RequestUltimate("a")); Assert.IsFalse(eng.CancelUltimate("a"));
        Assert.IsTrue(eng.Confirm(eng.BattleId, current.Sequence));
        Assert.IsTrue(eng.RequestUltimate("a"));
        BattleTestDriver.Drain(eng);
        CollectionAssert.AreEqual(new[] { "a_ult", "b_ult", "a_ult", "a_basic", "b_basic", "attack" },
            eng.ActionResults.Select(r => r.ActionId));
    }

    [Test]
    public void Enemy_Multi_Is_Whole_Action_Before_Queued_Ultimate()
    {
        var enemy = E("foe", 1000, 20, 1);
        enemy.intents[0].type = "multi"; enemy.intents[0].hits = 3;
        var eng = Make(1, new[] { C("a", 100, 10, 1) }, new[] { enemy });
        eng.StartBattle(); Assert.IsTrue(eng.BeginActions());
        var multi = eng.Advance();
        Assert.AreEqual(3, multi.Hits.Count); Assert.AreEqual(97, eng.Players[0].Hp);
        eng.Players[0].GainUlt(3); Assert.IsTrue(eng.RequestUltimate("a"));
        Assert.IsNull(eng.Advance()); Assert.AreEqual(1, eng.ActionResults.Count);
        Assert.IsTrue(eng.Confirm(eng.BattleId, multi.Sequence));
        var ult = eng.Advance(); Assert.AreEqual("a_ult", ult.ActionId);
        Assert.AreEqual(97, eng.Players[0].Hp);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Finished_Battle_Refunds_All_Waiting_Once_Without_RoundEnd(bool playerWins)
    {
        var eng = Make(1, new[] { C("a", 100, 10, 1), C("b", 100, 2, 1) },
            new[] { E("foe", 1000, 1, 1) }, Interrupt(4));
        eng.StartBattle(); Assert.IsTrue(eng.BeginActions());
        foreach (var actor in eng.Players)
        {
            actor.GainUlt(3); Assert.IsTrue(eng.RequestUltimate(actor.Id)); actor.GainUlt(2);
        }
        if (playerWins) eng.Enemies[0].TakeDamage(9999);
        else foreach (var actor in eng.Players) actor.TakeDamage(9999);
        Assert.IsNull(eng.Advance()); Assert.IsTrue(eng.IsFinished);
        Assert.AreEqual(playerWins ? Team.Player : Team.Enemy, eng.Winner);
        Assert.AreEqual(0, eng.PendingUltimateCount); Assert.AreEqual(1, eng.Hand.Count);
        foreach (var actor in eng.Players)
        {
            Assert.AreEqual(3, actor.Ult); Assert.IsFalse(eng.HasUltimateRequest(actor.Id));
            actor.GainUlt(-1); Assert.IsFalse(eng.CancelUltimate(actor.Id));
        }
        Assert.IsNull(eng.Advance()); Assert.IsNull(eng.Advance());
        Assert.IsTrue(eng.Players.All(actor => actor.Ult == 2));
        Assert.IsEmpty(eng.ActionResults);
    }

    [Test]
    public void Basic_Selects_Taunt_When_Its_Frozen_Queue_Entry_Starts()
    {
        var eng = Make(1, new[] { C("a", 100, 10, 1), C("b", 100, 2, 1) },
            new[] { E("foe", 1000, 1, 1), E("other", 1000, 1, 1) });
        eng.StartBattle(); Assert.IsTrue(eng.BeginActions());
        var first = eng.Advance();
        eng.Ctx.FormationOf(Team.Enemy).GetSlot("front2").AddModifier(100, 1);
        Assert.IsTrue(eng.Confirm(eng.BattleId, first.Sequence));
        var second = eng.Advance(); Assert.AreEqual("b_basic", second.ActionId);
        Assert.AreEqual("other_1", second.Hits.Single().TargetUnitId);
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
        Assert.IsFalse(BattleTestDriver.RequestAndPlay(eng, "healer", "foe_0"), "治疗不可指向敌方");
        Assert.IsTrue(BattleTestDriver.RequestAndPlay(eng, "healer", "buddy"), "治疗应能选友方");

        BattleTestDriver.CompleteRound(eng);

        Assert.AreEqual(50, eng.Ctx.GetUnit("buddy").Hp, "治疗回 30→50");
    }
}