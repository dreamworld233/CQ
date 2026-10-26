using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;
using CQ.Core.Cards;

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
                injectCards = new List<CardInjectionSpec> { new CardInjectionSpec { cardId = "test_skill", count = 1 } } }
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

    private static CardConfig GeneratedCard() => new CardConfig
    {
        id = "test_skill", name = "技能牌", count = 0,
        effect = new CardEffectSpec { type = "damage", target = "single", amount = 40 }
    };

    private static BattleEngine Make(long seed, IEnumerable<CharacterConfig> chars, IEnumerable<EnemyConfig> enemies, params CardConfig[] cards)
    {
        var characterList = chars.ToList();
        AssignSlots(characterList);
        var enemyList = enemies.ToList();
        var ids = enemyList.Select(e => e.id).ToList();
        var catalog = enemyList.ToDictionary(e => e.id);
        var cardCatalog = cards.ToList();
        if (!cardCatalog.Any(c => c.id == "test_skill")) cardCatalog.Add(GeneratedCard());
        return BattleEngine.Create(seed, characterList, ids, catalog, cardCatalog,
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
        Assert.AreEqual(600, eng.Enemies[0].Hp, "终结技只注牌，不直接伤害");
        Assert.AreEqual(1, eng.Deck.DrawPileCount);
        Assert.AreEqual(BattleFlow.Input, eng.Flow);
        Assert.AreEqual(0, eng.Players[0].Ult);
        BattleTestDriver.CompleteRound(eng);
        Assert.AreEqual(588, eng.Enemies[0].Hp, "正常普攻仍执行");
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
    public void Generated_Attack_Finishes_Immediately_Without_Starting_Normal_Round()
    {
        var eng = Make(1, new[] { C("sword", 100, 10, 12) }, new[] { E("grunt", 20, 1, 10) });
        eng.StartBattle(); eng.Players[0].GainUlt(3);
        Assert.IsTrue(BattleTestDriver.RequestAndPlay(eng, "sword"));
        Assert.IsFalse(eng.IsFinished);
        Assert.IsEmpty(eng.Hand);
        eng.Deck.Draw(1);
        Assert.IsTrue(eng.PlayCard(0, eng.Enemies[0].Id));
        Assert.IsTrue(eng.IsFinished); Assert.AreEqual(Team.Player, eng.Winner);
        Assert.AreEqual(BattleFlow.Finished, eng.Flow);
        Assert.IsFalse(BattleTestDriver.RequestAndPlay(eng, "sword"));
        BattleTestDriver.CompleteRound(eng);
        Assert.AreEqual(2, eng.ActionResults.Count); Assert.AreEqual(0, eng.Round);
    }

    [Test]
    public void Opening_Draws_Two_Only_Once_And_Stops_At_Deck_Size()
    {
        var eng = Make(1, new[] { C("sword", 100, 10, 12) }, new[] { E("bag", 99999, 1, 0) }, Interrupt(2));
        eng.StartBattle();
        Assert.AreEqual(2, eng.Hand.Count);
        eng.StartBattle();
        Assert.AreEqual(2, eng.Hand.Count);
        BattleTestDriver.CompleteRound(eng);
        Assert.AreEqual(2, eng.Hand.Count);
        Assert.AreEqual(0, eng.Deck.DrawPileCount);
    }

    [Test]
    public void Full_Hand_Skips_Round_Draw_And_Playing_Does_Not_Refill()
    {
        var eng = Make(1, new[] { C("sword", 100, 10, 1) }, new[] { E("bag", 99999, 1, 0) }, Interrupt(5));
        eng.StartBattle();
        Assert.AreEqual(2, eng.Hand.Count);
        Assert.AreEqual(3, eng.Deck.DrawPileCount);
        BattleTestDriver.CompleteRound(eng);
        Assert.AreEqual(3, eng.Hand.Count);
        Assert.AreEqual(2, eng.Deck.DrawPileCount);
        Assert.IsFalse(eng.SkippedRoundDraw);
        BattleTestDriver.CompleteRound(eng);
        Assert.AreEqual(3, eng.Hand.Count);
        Assert.AreEqual(2, eng.Deck.DrawPileCount);
        Assert.IsTrue(eng.SkippedRoundDraw);
        Assert.IsTrue(eng.PlayCard(0, eng.Enemies[0].Id));
        Assert.AreEqual(2, eng.Hand.Count);
        Assert.AreEqual(2, eng.Deck.DrawPileCount);
        Assert.AreEqual(1, eng.Deck.DiscardPileCount);
        BattleTestDriver.CompleteRound(eng);
        Assert.AreEqual(3, eng.Hand.Count);
        Assert.AreEqual(1, eng.Deck.DrawPileCount);
    }

    [Test]
    public void Multiple_Cards_Can_Be_Played_Without_Immediate_Refill()
    {
        var eng = Make(1, new[] { C("sword", 100, 10, 1) }, new[] { E("bag", 99999, 1, 0) }, Interrupt(5));
        eng.StartBattle();
        Assert.IsTrue(eng.PlayCard(0, eng.Enemies[0].Id));
        Assert.IsTrue(eng.PlayCard(0, eng.Enemies[0].Id));
        Assert.IsEmpty(eng.Hand);
        Assert.AreEqual(3, eng.Deck.DrawPileCount);
        Assert.AreEqual(2, eng.Deck.DiscardPileCount);
        BattleTestDriver.CompleteRound(eng);
        Assert.AreEqual(1, eng.Hand.Count);
        Assert.AreEqual(2, eng.Deck.DrawPileCount);
    }

    [Test]
    public void Deck_Draw_Caps_At_Three_Without_Discarding_Overflow()
    {
        var deck = new Deck(new Rng(7), new[] { ("card", 5) });
        deck.Draw(100);
        Assert.AreEqual(Deck.MaxHandSize, deck.Hand.Count);
        Assert.AreEqual(2, deck.DrawPileCount);
        Assert.AreEqual(0, deck.DiscardPileCount);
    }

    [Test]
    public void Deck_Full_Hand_Does_Not_Reshuffle_Discard_Or_Consume_Randomness()
    {
        var rng = new Rng(7);
        var control = new Rng(7);
        var deck = new Deck(rng, new[] { ("card", 4) });
        var other = new Deck(control, new[] { ("card", 4) });
        foreach (var d in new[] { deck, other })
        {
            d.Draw(3);
            d.Play(0);
            d.Draw(1);
        }
        deck.Draw(10);
        Assert.AreEqual(3, deck.Hand.Count);
        Assert.AreEqual(0, deck.DrawPileCount);
        Assert.AreEqual(1, deck.DiscardPileCount);
        Assert.AreEqual(control.NextULong(), rng.NextULong());
    }

    [TestCase(-1)]
    [TestCase(2)]
    [TestCase(int.MaxValue)]
    public void Deck_Invalid_Play_And_Remove_Preserve_All_Piles_And_Randomness(int index)
    {
        var rng = new Rng(7);
        var control = new Rng(7);
        var deck = new Deck(rng, new[] { ("card", 4) });
        var other = new Deck(control, new[] { ("card", 4) });
        deck.Draw(2);
        other.Draw(2);
        var hand = deck.Hand.ToArray();
        Assert.IsNull(deck.Play(index));
        Assert.IsNull(deck.Remove(index));
        CollectionAssert.AreEqual(hand, deck.Hand);
        Assert.AreEqual(2, deck.DrawPileCount);
        Assert.AreEqual(0, deck.DiscardPileCount);
        Assert.AreEqual(control.NextULong(), rng.NextULong());
    }

    [Test]
    public void Deck_Remove_Consumes_Only_One_Copy_And_Never_Recycles_It()
    {
        var deck = new Deck(new Rng(7), new[] { ("slag", 2) });
        deck.Draw(2);
        Assert.AreEqual("slag", deck.Remove(0));
        CollectionAssert.AreEqual(new[] { "slag" }, deck.Hand);
        Assert.AreEqual(0, deck.DiscardPileCount);
        Assert.AreEqual("slag", deck.Play(0));
        for (int i = 0; i < 4; i++)
        {
            deck.Draw(3);
            CollectionAssert.AreEqual(new[] { "slag" }, deck.Hand);
            Assert.AreEqual(0, deck.DrawPileCount);
            Assert.AreEqual("slag", deck.Play(0));
            Assert.AreEqual(1, deck.DiscardPileCount);
        }
    }

    [Test]
    public void Deck_Injection_Does_Not_Draw_Or_Reshuffle_Discard_Even_When_Full()
    {
        var deck = new Deck(new Rng(7), new[] { ("card", 4) });
        deck.Draw(3);
        deck.Play(0);
        deck.Draw(1);
        var hand = deck.Hand.ToArray();
        deck.InsertIntoDrawPile("lightning");
        deck.InsertIntoDrawPile("slag");
        deck.Draw(1);
        CollectionAssert.AreEqual(hand, deck.Hand);
        Assert.AreEqual(2, deck.DrawPileCount);
        Assert.AreEqual(1, deck.DiscardPileCount);
        deck.Remove(0);
        deck.Draw(1);
        Assert.Contains(deck.Hand.Last(), new[] { "lightning", "slag" });
        Assert.AreEqual(1, deck.DiscardPileCount);
    }

    [Test]
    public void Deck_Injected_Skill_Joins_Later_Discard_Reshuffles()
    {
        var deck = new Deck(new Rng(7), new (string id, int count)[0]);
        deck.InsertIntoDrawPile("heal");
        for (int i = 0; i < 4; i++)
        {
            deck.Draw(1);
            CollectionAssert.AreEqual(new[] { "heal" }, deck.Hand);
            Assert.AreEqual(0, deck.DrawPileCount);
            Assert.AreEqual(0, deck.DiscardPileCount);
            Assert.AreEqual("heal", deck.Play(0));
            Assert.AreEqual(1, deck.DiscardPileCount);
        }
    }

    [Test]
    public void Deck_Random_Insertion_Preserves_Existing_Order_And_Duplicate_Copies()
    {
        foreach (long seed in new long[] { 1, 7, 17, 37, 73 })
        {
            var defs = new[] { ("a", 1), ("b", 1), ("c", 1), ("d", 1) };
            var rng = new Rng(seed);
            var control = new Rng(seed);
            var deck = new Deck(rng, defs);
            var other = new Deck(control, defs);
            var expected = new List<string>();
            while (other.DrawPileCount > 0)
            {
                other.Draw(1);
                expected.Insert(0, other.Remove(0));
            }
            for (int i = 0; i < 3; i++)
            {
                expected.Insert(control.NextInt(expected.Count + 1), "heal");
                deck.InsertIntoDrawPile("heal");
            }
            Assert.AreEqual(7, deck.DrawPileCount);
            var actual = new List<string>();
            while (deck.DrawPileCount > 0)
            {
                deck.Draw(1);
                actual.Add(deck.Remove(0));
            }
            expected.Reverse();
            CollectionAssert.AreEqual(expected, actual);
            Assert.AreEqual(3, actual.Count(id => id == "heal"));
            Assert.AreEqual(0, deck.Hand.Count + deck.DrawPileCount + deck.DiscardPileCount);
            Assert.AreEqual(control.NextULong(), rng.NextULong());
        }
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
        Assert.AreEqual(2, eng.Hand.Count);
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
        Assert.AreEqual(2, eng.Hand.Count);
        if (!afterConfirm) Assert.IsTrue(eng.Confirm(eng.BattleId, last.Sequence));
        var ult = eng.Advance(); Assert.AreEqual("a_ult", ult.ActionId);
        Assert.AreEqual(2, eng.Ctx.FindStatus("a", StatusType.AttackUp).Duration);
        Assert.AreEqual(2, eng.Hand.Count);
        Assert.IsTrue(eng.Confirm(eng.BattleId, ult.Sequence));
        Assert.IsNull(eng.Advance()); Assert.IsNull(eng.Advance());
        Assert.AreEqual(BattleFlow.Input, eng.Flow); Assert.AreEqual(3, eng.Hand.Count);
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
        Assert.IsTrue(eng.HasUltimateRequest("a"));
        var ult = eng.Advance(); Assert.IsNotNull(ult); Assert.AreEqual(recharge, actor.Ult);
        Assert.IsTrue(eng.HasUltimateRequest("a"));
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
    public void Manual_Cancel_And_Explicit_Target_Apis_Are_Removed()
    {
        Assert.IsNull(typeof(BattleEngine).GetMethod("CancelUltimate"));
        Assert.IsNull(typeof(BattleEngine).GetMethod("CanCancelUltimate"));
        Assert.AreEqual(1, typeof(BattleEngine).GetMethod("RequestUltimate").GetParameters().Length);
        Assert.AreEqual(typeof(Deck), typeof(SkillResolver).GetMethod("Resolve").GetParameters().Last().ParameterType);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Heal_Injection_Does_Not_Select_Or_Heal_An_Ally(bool dies)
    {
        var config = C("a", 100, 10, 1);
        var card = GeneratedCard(); card.effect = new CardEffectSpec { type = "heal", target = "single", amount = 30 };
        var eng = Make(1, new[] { config, C("b", 100, 2, 1), C("c", 100, 1, 1) }, new[] { E("foe", 1000, 1, 1) }, card);
        eng.StartBattle(); eng.Players[0].GainUlt(3);
        eng.Players[1].Hp = 10;
        Assert.IsTrue(eng.RequestUltimate("a"));
        eng.Players[1].Hp = dies ? 0 : 90; eng.Players[2].Hp = 20;
        var result = eng.Advance();
        Assert.IsNotNull(result); Assert.AreEqual(20, eng.Players[2].Hp);
        Assert.AreEqual(dies ? 0 : 90, eng.Players[1].Hp); Assert.AreEqual(0, eng.Players[0].Ult);
        Assert.IsTrue(eng.Confirm(eng.BattleId, result.Sequence));
        eng.Deck.Draw(1);
        Assert.IsTrue(eng.PlayCard(0, "a"), "可手选自身，而不是强制最低血量");
        Assert.AreEqual(20, eng.Players[2].Hp);
    }

    [Test]
    public void Generated_Single_Attack_Is_Hand_Targeted_Regardless_Of_Taunt()
    {
        var config = C("a", 100, 10, 1);
        var eng = Make(1, new[] { config }, new[] { E("foe", 1000, 1, 1), E("other", 1000, 1, 1) });
        eng.StartBattle(); eng.Players[0].GainUlt(3);
        Assert.IsTrue(eng.RequestUltimate("a"));
        eng.Ctx.FormationOf(Team.Enemy).GetSlot("front2").AddModifier(100, 1);
        var result = eng.Advance();
        Assert.IsEmpty(result.Hits);
        Assert.IsTrue(eng.Confirm(eng.BattleId, result.Sequence));
        eng.Deck.Draw(1);
        Assert.IsTrue(eng.PlayCard(0, "foe_0"));
        Assert.AreEqual("foe_0", eng.ActionResults.Last().Hits.Single().TargetUnitId);
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
        eng.Players[0].GainUlt(-1);
        Assert.AreEqual(2, eng.Players[0].Ult);
        Assert.IsFalse(eng.RequestUltimate("a"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Winner_Clears_Normal_But_Preserves_Current_For_Playback(bool playerWins)
    {
        var eng = Make(1, new[] { C("a", 100, 10, 1000), C("b", 100, 2, 1) },
            new[] { E("foe", playerWins ? 10 : 10000, playerWins ? 1 : 20, playerWins ? 1 : 1000) });
        eng.StartBattle(); Assert.IsTrue(eng.BeginActions());
        if (!playerWins) eng.Ctx.GetUnit("a").Hp = 0;
        ActionResult current = eng.Advance();
        Assert.IsTrue(eng.IsFinished); Assert.AreSame(current, eng.CurrentAction);
        Assert.AreEqual(playerWins ? Team.Player : Team.Enemy, eng.Winner);
        Assert.AreEqual(0, eng.PendingUltimateCount);
        if (playerWins) Assert.AreEqual(0, eng.Ctx.GetUnit("b").Ult);
        Assert.IsFalse(eng.RequestUltimate("b")); Assert.IsNull(eng.Advance());
        Assert.IsFalse(eng.Confirm(System.Guid.NewGuid(), current.Sequence));
        Assert.IsTrue(eng.Confirm(eng.BattleId, current.Sequence));
        Assert.IsFalse(eng.HasUltimateRequest("a"));
        Assert.IsNull(eng.Advance()); Assert.IsFalse(eng.BeginActions());
        Assert.AreEqual(1, eng.ActionResults.Count);
    }

    [Test]
    public void Dead_Normal_Queue_Entry_Is_Skipped_After_Basic_With_Inserted_Ultimate()
    {
        var config = C("a", 100, 10, 20);
        var eng = Make(1, new[] { config }, new[] { E("foe", 20, 5, 1), E("other", 1000, 1, 1) });
        eng.StartBattle(); Assert.IsTrue(eng.BeginActions());
        eng.Players[0].GainUlt(3); Assert.IsTrue(eng.RequestUltimate("a"));
        eng.Ctx.FormationOf(Team.Enemy).GetSlot("front1").AddModifier(100, 1);
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
                    Assert.IsFalse(a.RequestUltimate("b")); Assert.IsFalse(b.RequestUltimate("b"));
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
        Assert.IsFalse(eng.RequestUltimate("a"));
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
        Assert.AreEqual(0, eng.PendingUltimateCount); Assert.AreEqual(2, eng.Hand.Count);
        foreach (var actor in eng.Players)
        {
            Assert.AreEqual(3, actor.Ult); Assert.IsFalse(eng.HasUltimateRequest(actor.Id));
            actor.GainUlt(-1);
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
            new Dictionary<string, EnemyConfig> { { "grunt", enemy } }, new[] { GeneratedCard() },
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
            new Dictionary<string, EnemyConfig> { { "grunt", enemy } }, new[] { GeneratedCard() }, TestFormation());

        Assert.AreEqual("back1", eng.Players[0].SlotId);
        Assert.AreEqual("back3", eng.Enemies[0].SlotId);
    }

    [Test]
    public void HealSingle_Is_Injected_Then_Heals_Chosen_Ally()
    {
        var healer = new CharacterConfig
        {
            id = "healer", name = "治疗", role = "support", maxHp = 100, baseSpeed = 10,
            maxUlt = 3, ultChargeTrigger = "hit", ultChargeAmount = 1, defaultSlot = "back1",
            skills = new List<SkillSpec>
            {
                new SkillSpec { id = "healer_ult", name = "治疗", type = "ult",
                    injectCards = new List<CardInjectionSpec> { new CardInjectionSpec { cardId = "test_skill", count = 1 } } }
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

        var card = GeneratedCard(); card.effect = new CardEffectSpec { type = "heal", target = "single", amount = 30 };
        var eng = Make(1, new[] { healer, buddy }, new[] { E("foe", 999, 0, 0) }, card);
        eng.StartBattle();
        eng.Ctx.GetUnit("buddy").TakeDamage(50);

        eng.Players[0].GainUlt(3);
        Assert.IsTrue(BattleTestDriver.RequestAndPlay(eng, "healer"));

        BattleTestDriver.CompleteRound(eng);

        Assert.AreEqual(30, eng.Ctx.GetUnit("buddy").Hp, "注牌本身不治疗");
        Assert.IsTrue(eng.PlayCard(0, "buddy"));
        Assert.AreEqual(60, eng.Ctx.GetUnit("buddy").Hp, "治疗牌回 30→60");
    }
    private static CardConfig AttackCard(string target, int damage) => new CardConfig
    {
        id = "damage", name = "damage", count = 1,
        effect = new CardEffectSpec { type = "damage", target = target, amount = damage }
    };

    [Test]
    public void Single_Attack_Card_Uses_Chosen_Target_Shield_And_Hit_Charging_Without_Attack_Charging()
    {
        var eng = Make(7, new[] { C("a", 100, 10, 1) },
            new[] { E("foe", 100, 1, 1), E("other", 100, 1, 1) }, AttackCard("single", 20));
        eng.StartBattle();
        var target = eng.Enemies[1];
        target.UltChargeTrigger = "hit"; target.UltChargeAmount = 1; target.MaxUlt = 3;
        eng.Ctx.FormationOf(Team.Enemy).GetSlot("front1").AddModifier(100, 1);
        eng.Ctx.AddStatus(target.Id, new StatusEffect { Type = StatusType.Vulnerable, Percent = 50, Duration = 1, Turn = "round" });
        eng.Ctx.AddStatus(target.Id, new StatusEffect { Type = StatusType.Shield, Amount = 20, Turn = "once" });
        RefusesWithoutMutation(eng, () =>
        {
            Assert.IsFalse(eng.PlayGroupCard(0));
            Assert.IsFalse(eng.PlayCard(0, "a"));
        });
        Assert.IsTrue(eng.PlayCard(0, target.Id));
        Assert.AreEqual(100, eng.Enemies[0].Hp);
        Assert.AreEqual(90, target.Hp);
        Assert.AreEqual(1, target.Ult);
        Assert.AreEqual(0, eng.Players[0].Ult);
        var result = eng.ActionResults.Single();
        Assert.IsNull(result.ActorId);
        Assert.AreEqual(BattleEngine.CardSourceId, result.Hits.Single().SourceUnitId);
        Assert.AreEqual(20, result.Hits.Single().ShieldAbsorbed);
        Assert.IsNull(eng.CurrentAction);
        Assert.AreEqual(1, eng.Deck.DiscardPileCount);
    }

    [TestCase("damage")]
    [TestCase("weaken")]
    [TestCase("vulnerable")]
    public void Group_Cards_Require_Idle_Input_And_Reject_Unit_And_Slot_Entrypoints(string type)
    {
        var card = Card(type);
        card.effect.target = "all";
        var eng = Make(7, new[] { C("a", 100, 10, 1) },
            new[] { E("foe", 1000, 1, 1), E("other", 1000, 1, 1) }, card);
        RefusesWithoutMutation(eng, () => Assert.IsFalse(eng.PlayGroupCard(0)));
        eng.StartBattle();
        RefusesWithoutMutation(eng, () =>
        {
            foreach (int index in new[] { -1, eng.Hand.Count, int.MaxValue })
            {
                Assert.IsFalse(eng.CanPlayGroupCard(index));
                Assert.IsFalse(eng.PlayGroupCard(index));
            }
            Assert.IsFalse(eng.PlayCard(0, eng.Enemies[0].Id));
            Assert.IsFalse(eng.PlaySlotCard(0, Team.Enemy, "front1"));
        });
        Assert.IsTrue(eng.BeginActions());
        RefusesWithoutMutation(eng, () => Assert.IsFalse(eng.PlayGroupCard(0)));
        var current = eng.Advance();
        RefusesWithoutMutation(eng, () => Assert.IsFalse(eng.PlayGroupCard(0)));
        Assert.IsTrue(eng.Confirm(eng.BattleId, current.Sequence));
        BattleTestDriver.Drain(eng);
        Assert.IsTrue(eng.CanPlayGroupCard(0));
        Assert.IsTrue(eng.PlayGroupCard(0));
        Assert.IsEmpty(eng.Hand);
        Assert.AreEqual(1, eng.Deck.DiscardPileCount);
        var result = eng.ActionResults.Last();
        Assert.AreEqual(Team.Enemy, result.TargetTeam);
        Assert.IsNull(result.TargetUnitId);
        Assert.IsNull(result.TargetSlotId);
        Assert.IsNull(eng.CurrentAction);
        foreach (var enemy in eng.Enemies)
        {
            if (type == "damage")
            {
                Assert.GreaterOrEqual(enemy.Hp, 959);
                Assert.LessOrEqual(enemy.Hp, 960);
            }
            else Assert.IsTrue(eng.Ctx.HasStatus(enemy.Id, type == "weaken" ? StatusType.Weaken : StatusType.Vulnerable));
        }
        if (type == "damage") Assert.AreEqual(1919, eng.Enemies.Sum(enemy => enemy.Hp));
        Assert.IsEmpty(eng.Ctx.GetStatuses("a"));
    }

    [TestCase("single")]
    [TestCase("all")]
    public void Lethal_Card_Finishes_During_Input_Without_Starting_Automatic_Actions(string target)
    {
        var eng = Make(7, new[] { C("a", 100, 10, 1) }, new[] { E("foe", 10, 1, 1) }, AttackCard(target, 20));
        eng.StartBattle();
        Assert.IsTrue(target == "all" ? eng.PlayGroupCard(0) : eng.PlayCard(0, eng.Enemies[0].Id));
        Assert.IsTrue(eng.IsFinished);
        Assert.AreEqual(Team.Player, eng.Winner);
        Assert.AreEqual(BattleFlow.Finished, eng.Flow);
        Assert.AreEqual(0, eng.Round);
        Assert.AreEqual(1, eng.ActionResults.Count);
        Assert.IsFalse(eng.BeginActions());
        Assert.IsNull(eng.Advance());
        RefusesWithoutMutation(eng, () => Assert.IsFalse(eng.PlayGroupCard(0)));
    }

    [Test]
    public void Group_Vulnerable_Then_Attack_Combines_Without_Hitting_Dead_Units_Or_Allies()
    {
        var vulnerable = Card("vulnerable");
        vulnerable.effect.target = "all";
        vulnerable.effect.percent = 50;
        var eng = Make(7, new[] { C("a", 100, 10, 1) },
            new[] { E("foe", 100, 1, 1), E("other", 100, 1, 1) }, vulnerable, AttackCard("all", 10));
        eng.Enemies[1].Hp = 0;
        eng.StartBattle();
        Assert.IsTrue(eng.PlayGroupCard(eng.Hand.ToList().IndexOf("vulnerable")));
        Assert.IsTrue(eng.PlayGroupCard(eng.Hand.ToList().IndexOf("damage")));
        Assert.AreEqual(85, eng.Enemies[0].Hp);
        Assert.AreEqual(0, eng.Enemies[1].Hp);
        Assert.IsEmpty(eng.Ctx.GetStatuses(eng.Enemies[1].Id));
        Assert.AreEqual(100, eng.Players[0].Hp);
        Assert.AreEqual(1, eng.ActionResults.Last().Hits.Count);
        BattleTestDriver.CompleteRound(eng);
        Assert.IsFalse(eng.Ctx.HasStatus(eng.Enemies[0].Id, StatusType.Vulnerable));
    }

    private static CardConfig Card(string type) => new CardConfig
    {
        id = type, name = type, count = 1,
        effect = new CardEffectSpec { type = type, percent = 25, amount = 40, duration = 2 }
    };

    // Every refused command must preserve the deck, observable events and action sequence.
    private static void RefusesWithoutMutation(BattleEngine eng, System.Action command)
    {
        var hand = eng.Hand.ToArray();
        int draw = eng.Deck.DrawPileCount, discard = eng.Deck.DiscardPileCount;
        var results = eng.ActionResults.ToArray();
        var events = eng.RoundEvents.ToArray();
        var current = eng.CurrentAction;
        string State() => string.Join(";", eng.Ctx.Units.Select(u =>
            u.Id + ":" + u.Hp + ":" + u.Ult + ":" + eng.Ctx.GetHaste(u.Id) + ":" +
            eng.Ctx.EffectiveTaunt(u) + ":" + string.Join(",", eng.Ctx.GetStatuses(u.Id).Select(s =>
                s.Type + "/" + s.Duration + "/" + s.Percent + "/" + s.Amount))));
        string before = State();
        command();
        CollectionAssert.AreEqual(hand, eng.Hand);
        Assert.AreEqual(draw, eng.Deck.DrawPileCount);
        Assert.AreEqual(discard, eng.Deck.DiscardPileCount);
        CollectionAssert.AreEqual(results, eng.ActionResults);
        CollectionAssert.AreEqual(events, eng.RoundEvents);
        Assert.AreSame(current, eng.CurrentAction);
        Assert.AreEqual(before, State());
    }

    [TestCase("interrupt", Team.Enemy)]
    [TestCase("weaken", Team.Enemy)]
    [TestCase("slow", Team.Enemy)]
    [TestCase("haste", Team.Player)]
    [TestCase("shield", Team.Player)]
    [TestCase("attackUp", Team.Player)]
    public void Unit_Cards_Validate_Before_Discard_And_Record_Only_Success(string type, Team targetTeam)
    {
        var eng = Make(7, new[] { C("a", 100, 10, 1), C("b", 100, 9, 1) },
            new[] { E("foe", 1000, 1, 1), E("other", 1000, 1, 1) }, Card(type));
        eng.StartBattle();
        var valid = targetTeam == Team.Player ? eng.Players[0] : eng.Enemies[0];
        var dead = targetTeam == Team.Player ? eng.Players[1] : eng.Enemies[1];
        var wrong = targetTeam == Team.Player ? eng.Enemies[0] : eng.Players[0];
        dead.TakeDamage(dead.MaxHp);
        RefusesWithoutMutation(eng, () =>
        {
            foreach (int index in new[] { -1, eng.Hand.Count, int.MaxValue })
            {
                Assert.IsFalse(eng.CanPlayCard(index, valid.Id));
                Assert.IsFalse(eng.PlayCard(index, valid.Id));
            }
            foreach (var id in new[] { null, "", "missing", wrong.Id, dead.Id })
            {
                Assert.IsFalse(eng.CanPlayCard(0, id));
                Assert.IsFalse(eng.PlayCard(0, id));
            }
            foreach (var team in new[] { Team.Player, Team.Enemy })
            {
                Assert.IsFalse(eng.CanPlaySlotCard(0, team, "front1"));
                Assert.IsFalse(eng.PlaySlotCard(0, team, "front1"));
            }
        });
        Assert.IsTrue(eng.CanPlayCard(0, valid.Id));
        Assert.IsTrue(eng.PlayCard(0, valid.Id));
        Assert.IsEmpty(eng.Hand);
        Assert.AreEqual(1, eng.Deck.DiscardPileCount);
        var result = eng.ActionResults.Single();
        Assert.AreEqual(1L, result.Sequence, "拒绝出牌不能消耗事件序号");
        Assert.AreEqual(BattleEventKind.Card, result.Kind);
        Assert.AreEqual(type, result.ActionId);
        Assert.IsNull(result.TargetTeam);
        Assert.IsNull(result.TargetSlotId);
        Assert.AreEqual(1, eng.RoundEvents.Count);
    }

    [TestCase("tauntUp")]
    [TestCase("tauntDown")]
    public void Slot_Cards_Reject_Invalid_Indices_Teams_Slots_And_Unit_Entry(string type)
    {
        var eng = Make(7, new[] { C("a", 100, 10, 1) }, new[] { E("foe", 1000, 1, 1) }, Card(type));
        eng.StartBattle();
        RefusesWithoutMutation(eng, () =>
        {
            foreach (int index in new[] { -1, eng.Hand.Count, int.MaxValue })
            {
                Assert.IsFalse(eng.CanPlaySlotCard(index, Team.Player, "front1"));
                Assert.IsFalse(eng.PlaySlotCard(index, Team.Player, "front1"));
            }
            foreach (var team in new[] { (Team)(-1), (Team)2, (Team)int.MaxValue })
            {
                Assert.IsFalse(eng.CanPlaySlotCard(0, team, "front1"));
                Assert.IsFalse(eng.PlaySlotCard(0, team, "front1"));
            }
            foreach (var team in new[] { Team.Player, Team.Enemy })
                foreach (var slot in new[] { null, "", "front3", "back4", "Front1", "enemy/front1" })
                {
                    Assert.IsFalse(eng.CanPlaySlotCard(0, team, slot));
                    Assert.IsFalse(eng.PlaySlotCard(0, team, slot));
                }
            foreach (var id in new[] { "a", "foe_0", null })
            {
                Assert.IsFalse(eng.CanPlayCard(0, id));
                Assert.IsFalse(eng.PlayCard(0, id));
            }
        });
        Assert.IsTrue(eng.PlaySlotCard(0, Team.Enemy, "back3"));
        Assert.AreEqual(1L, eng.ActionResults.Single().Sequence);
        Assert.AreEqual(Team.Enemy, eng.ActionResults.Single().TargetTeam);
        Assert.AreEqual("back3", eng.ActionResults.Single().TargetSlotId);
    }

    [TestCase("setup")]
    [TestCase("queued")]
    [TestCase("occupied")]
    [TestCase("resolving")]
    [TestCase("finished")]
    public void Both_Card_Entrypoints_Require_Idle_Strategy_Window(string state)
    {
        foreach (var type in new[] { "interrupt", "tauntUp" })
        {
            var eng = Make(7, new[] { C("a", 100, 10, 1) }, new[] { E("foe", 1000, 1, 1) }, Card(type));
            if (state == "setup") eng.Deck.Draw(1);
            else eng.StartBattle();
            if (state == "queued" || state == "occupied")
            {
                eng.Players[0].GainUlt(3);
                Assert.IsTrue(eng.RequestUltimate("a"));
                if (state == "occupied") Assert.IsNotNull(eng.Advance());
            }
            if (state == "resolving") Assert.IsTrue(eng.BeginActions());
            if (state == "finished")
            {
                eng.Enemies[0].TakeDamage(9999);
                Assert.IsFalse(eng.BeginActions());
                Assert.IsTrue(eng.IsFinished);
            }
            RefusesWithoutMutation(eng, () =>
            {
                Assert.IsFalse(eng.CanPlayCards);
                Assert.IsFalse(eng.CanPlayCard(0, "foe_0"));
                Assert.IsFalse(eng.PlayCard(0, "foe_0"));
                Assert.IsFalse(eng.CanPlaySlotCard(0, Team.Player, "back3"));
                Assert.IsFalse(eng.PlaySlotCard(0, Team.Player, "back3"));
            });
            if (state == "finished") continue;
            if (state == "setup") eng.StartBattle();
            else BattleTestDriver.Drain(eng);
            Assert.IsTrue(eng.CanPlayCards);
            long previous = eng.ActionResults.LastOrDefault()?.Sequence ?? 0;
            Assert.IsTrue(type == "interrupt" ? eng.PlayCard(0, "foe_0") : eng.PlaySlotCard(0, Team.Player, "back3"));
            Assert.AreEqual(previous + 1, eng.ActionResults.Last().Sequence);
        }
    }

    [TestCase(Team.Player, "tauntUp")]
    [TestCase(Team.Enemy, "tauntUp")]
    [TestCase(Team.Player, "tauntDown")]
    [TestCase(Team.Enemy, "tauntDown")]
    public void Slot_Cards_Accept_All_Five_Slots_Including_Empty_And_Record_Address(Team team, string type)
    {
        foreach (var slotId in SlotIds)
        {
            var eng = Make(7, new[] { C("a", 100, 10, 1) }, new[] { E("foe", 1000, 1, 1) }, Card(type));
            eng.StartBattle();
            var slot = eng.Ctx.FormationOf(team).GetSlot(slotId);
            var occupant = slot.Occupant;
            Assert.IsTrue(eng.CanPlaySlotCard(0, team, slotId));
            Assert.IsTrue(eng.PlaySlotCard(0, team, slotId));
            Assert.AreSame(occupant, slot.Occupant);
            Assert.AreEqual(occupant == null ? 0 : 100 + (type == "tauntUp" ? 40 : -40), slot.EffectiveTaunt);
            var result = eng.ActionResults.Single();
            Assert.AreEqual(team, result.TargetTeam);
            Assert.AreEqual(slotId, result.TargetSlotId);
            Assert.AreEqual(type, result.ActionId);
            Assert.AreEqual(BattleEventKind.Card, result.Kind);
            Assert.AreEqual(1L, result.Sequence);
            Assert.AreEqual(1, eng.Deck.DiscardPileCount);
            Assert.IsEmpty(eng.Hand);
            Assert.IsEmpty(result.Hits);
            Assert.IsNull(eng.CurrentAction, "卡牌即时生效，不占动作播放锁");
        }
    }

    private static void AssertFormation(BattleEngine eng)
    {
        foreach (var team in new[] { Team.Player, Team.Enemy })
        {
            var formation = eng.Ctx.FormationOf(team);
            foreach (var unit in eng.Ctx.Units.Where(u => u.Team == team))
                Assert.AreSame(unit, formation.GetSlot(unit.SlotId).Occupant);
            foreach (var slot in formation.Slots)
                if (slot.Occupant != null)
                {
                    Assert.AreEqual(team, slot.Occupant.Team);
                    Assert.AreEqual(slot.Id, slot.Occupant.SlotId);
                }
        }
    }

    [Test]
    public void Setup_Move_Swap_And_Invalid_Moves_Preserve_Both_Sides_Of_Occupancy()
    {
        var eng = Make(1, new[] { C("a", 100, 10, 1), C("b", 100, 2, 1) }, new[] { E("foe", 1000, 1, 1) });
        Assert.AreEqual(BattleFlow.Setup, eng.Flow);
        Assert.IsFalse(eng.RequestUltimate("a")); Assert.IsFalse(eng.BeginActions());
        Assert.IsTrue(eng.TryMovePlayer("a", "back3"));
        Assert.IsNull(eng.Ctx.FormationOf(Team.Player).GetSlot("front1").Occupant);
        AssertFormation(eng);
        Assert.IsTrue(eng.TryMovePlayer("b", "back3"));
        Assert.AreEqual("front2", eng.Players[0].SlotId);
        Assert.AreEqual("back3", eng.Players[1].SlotId);
        AssertFormation(eng);
        var before = string.Join(";", eng.Ctx.Units.Select(u => u.Id + u.SlotId));
        foreach (var actor in new[] { null, "missing", "foe_0" }) Assert.IsFalse(eng.TryMovePlayer(actor, "front1"));
        foreach (var slot in new[] { null, "", "enemy/front1", "front3" }) Assert.IsFalse(eng.TryMovePlayer("a", slot));
        Assert.AreEqual(before, string.Join(";", eng.Ctx.Units.Select(u => u.Id + u.SlotId)));
        AssertFormation(eng);
        Assert.IsTrue(eng.TryMovePlayer("a", "front2")); AssertFormation(eng);
        eng.Ctx.FormationOf(Team.Player).GetSlot("front2").AddModifier(30, 1);
        Assert.IsTrue(eng.TryMovePlayer("a", "back1"));
        Assert.AreEqual(50, eng.Ctx.EffectiveTaunt(eng.Players[0]));
        Assert.AreEqual(100, eng.Ctx.FormationOf(Team.Player).GetSlot("front2").BaseTaunt);
        eng.StartBattle(); Assert.IsFalse(eng.TryMovePlayer("a", "front1"));
        Assert.IsTrue(eng.BeginActions()); Assert.IsFalse(eng.TryMovePlayer("a", "front1"));
        AssertFormation(eng);
    }

    [Test]
    public void Slot_Target_Hit_Area_Is_At_Feet_Not_Above_Unit()
    {
        var go = new UnityEngine.GameObject("SlotTargetTest");
        try
        {
            var controller = go.AddComponent<CQ.Runtime.BattleController>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(CQ.Runtime.BattleController);
            var position = type.GetMethod("SlotTargetPosition", flags);
            var contains = type.GetMethod("SlotContainsPoint", flags);
            foreach (var team in new[] { Team.Player, Team.Enemy })
                foreach (var slotId in new[] { "front1", "front2", "back1", "back2", "back3" })
                {
                    var center = (UnityEngine.Vector3)position.Invoke(controller, new object[] { team, slotId });
                    var unit = center + UnityEngine.Vector3.up * (0.78f * 0.72f);
                    Assert.Less(center.y, unit.y);
                    Assert.IsTrue((bool)contains.Invoke(controller, new object[] { team, slotId, (UnityEngine.Vector2)center }));
                    Assert.IsFalse((bool)contains.Invoke(controller, new object[] { team, slotId,
                        (UnityEngine.Vector2)(unit + UnityEngine.Vector3.up * 0.86f) }));
                }
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void Controller_Restores_Cyclic_Formation_Into_Fresh_Slots_Without_Effects()
    {
        var chars = new[] { C("a", 100, 10, 1), C("b", 100, 2, 1), C("c", 100, 1, 1) };
        var old = Make(1, chars, new[] { E("foe", 1000, 1, 1) });
        Assert.IsTrue(old.TryMovePlayer("a", "front2")); Assert.IsTrue(old.TryMovePlayer("b", "back1"));
        old.Ctx.FormationOf(Team.Player).GetSlot("front2").AddModifier(900, 3);
        var go = new UnityEngine.GameObject("FormationRestoreTest");
        try
        {
            var controller = go.AddComponent<CQ.Runtime.BattleController>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(CQ.Runtime.BattleController);
            type.GetField("_engine", flags).SetValue(controller, old);
            type.GetMethod("SaveFormation", flags).Invoke(controller, null);
            var fresh = Make(2, chars, new[] { E("foe", 1000, 1, 1) });
            type.GetField("_engine", flags).SetValue(controller, fresh);
            type.GetMethod("RestoreFormation", flags).Invoke(controller, null);
            CollectionAssert.AreEqual(old.Players.Select(u => u.SlotId), fresh.Players.Select(u => u.SlotId));
            AssertFormation(fresh);
            Assert.AreEqual(BattleFlow.Setup, fresh.Flow);
            Assert.AreEqual(100, fresh.Ctx.FormationOf(Team.Player).GetSlot("front2").EffectiveTaunt);
            Assert.AreNotSame(old.Ctx.FormationOf(Team.Player).GetSlot("front2"), fresh.Ctx.FormationOf(Team.Player).GetSlot("front2"));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

}
