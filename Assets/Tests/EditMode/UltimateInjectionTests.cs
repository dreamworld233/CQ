using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using CQ.Core.Battle;
using CQ.Core.Cards;
using CQ.Core.Combat;
using CQ.Core.Config;
using CQ.Runtime;

public class UltimateInjectionTests
{
    private static BattleEngine Build(long seed = 7, bool initialDeck = false)
    {
        var characters = ConfigLoader.LoadCharacters();
        var cards = ConfigLoader.LoadCards();
        Assert.IsEmpty(characters.errors); Assert.IsEmpty(cards.errors);
        if (!initialDeck) foreach (var card in cards.items) card.count = 0;
        var enemy = new EnemyConfig { id = "bag", name = "沙包", maxHp = 99999, baseSpeed = 1,
            intentSequence = new[] { "idle", "idle" }, intents = new List<IntentConfig>
            { new IntentConfig { id = "idle", type = "attackUp", buffPercent = 1, buffDuration = 1 } } };
        return BattleEngine.Create(seed, characters.items, new[] { "bag" },
            new Dictionary<string, EnemyConfig> { { "bag", enemy } }, cards.items);
    }

    private static ActionResult Inject(BattleEngine engine, string actorId)
    {
        var actor = engine.Ctx.GetUnit(actorId);
        actor.GainUlt(actor.MaxUlt);
        Assert.IsTrue(engine.RequestUltimate(actorId));
        var result = engine.Advance();
        Assert.IsNotNull(result);
        Assert.IsTrue(engine.Confirm(engine.BattleId, result.Sequence));
        return result;
    }

    private static int Total(BattleEngine engine) => engine.Hand.Count + engine.Deck.DrawPileCount +
        engine.Deck.DiscardPileCount + engine.Deck.RemovedCount;

    [TestCase("sword", "gale_slash", 40)]
    [TestCase("guard", "heal", 30)]
    [TestCase("mage", "lightning", 30)]
    public void Real_Config_Injection_Only_Changes_Deck_Not_Hp_Hand_Or_Charges(string actorId, string cardId, int amount)
    {
        var engine = Build(initialDeck: true);
        engine.StartBattle(); engine.Deck.Draw(1);
        var hand = engine.Hand.ToArray();
        var hp = engine.Ctx.Units.Select(u => u.Hp).ToArray();
        int before = engine.Deck.DrawPileCount;
        var result = Inject(engine, actorId);
        var expected = actorId == "mage" ? new[] { "lightning", "slag" } : new[] { cardId };
        CollectionAssert.AreEqual(expected, result.InjectedCardIds);
        CollectionAssert.AreEqual(hand, engine.Hand);
        CollectionAssert.AreEqual(hp, engine.Ctx.Units.Select(u => u.Hp));
        Assert.AreEqual(before + expected.Length, engine.Deck.DrawPileCount);
        Assert.AreEqual(amount, engine.GetCard(cardId).effect.amount);
        Assert.AreEqual(0, engine.GetCard(cardId).count);
        Assert.IsEmpty(result.Hits); Assert.IsEmpty(result.Charges);
        Assert.AreEqual(0, engine.Ctx.GetUnit(actorId).Ult);
        Assert.AreEqual(BattleFlow.Input, engine.Flow);
        Assert.IsNull(engine.Advance(), "策略窗口注牌不启动自动行动或额外抽牌");
        CollectionAssert.AreEqual(expected, engine.RoundEvents.Select(e => e.InjectedCardId));
        Assert.IsTrue(engine.RoundEvents.All(e => e.InjectedCardCount == 1 && e.HpDelta == 0));
        Assert.AreEqual(5 + expected.Length, Total(engine));
    }

    [Test]
    public void Empty_Draw_Pile_Injection_Is_Drawn_Before_Discard_Is_Recycled()
    {
        var engine = Build(); engine.StartBattle();
        Inject(engine, "sword"); engine.Deck.Draw(1);
        Assert.IsTrue(engine.PlayCard(0, engine.Enemies[0].Id));
        Assert.AreEqual(1, engine.Deck.DiscardPileCount);
        Inject(engine, "guard");
        Assert.AreEqual(1, engine.Deck.DrawPileCount);
        Assert.AreEqual(1, engine.Deck.DiscardPileCount);
        engine.Deck.Draw(1);
        CollectionAssert.AreEqual(new[] { "heal" }, engine.Hand);
        Assert.AreEqual(1, engine.Deck.DiscardPileCount);
        engine.Deck.Draw(1);
        CollectionAssert.AreEqual(new[] { "heal", "gale_slash" }, engine.Hand);
        Assert.AreEqual(0, engine.Deck.DiscardPileCount);
    }

    [TestCase("sword", "gale_slash")]
    [TestCase("guard", "heal")]
    [TestCase("mage", "lightning")]
    public void Generated_Cards_Remain_Playable_After_Source_Death_And_Return_To_Discard(string actorId, string cardId)
    {
        var engine = Build(); engine.StartBattle();
        Inject(engine, actorId);
        engine.Ctx.GetUnit(actorId).TakeDamage(999);
        engine.Deck.Draw(3);
        int index = engine.Hand.ToList().IndexOf(cardId);
        var ally = engine.Players.First(u => !u.IsDead); ally.TakeDamage(35);
        int hp = ally.Hp;
        bool played = cardId == "lightning" ? engine.PlayGroupCard(index) :
            engine.PlayCard(index, cardId == "heal" ? ally.Id : engine.Enemies[0].Id);
        Assert.IsTrue(played);
        Assert.AreEqual(1, engine.Deck.DiscardPileCount);
        if (cardId == "heal") Assert.AreEqual(hp + 30, ally.Hp);
        Assert.IsEmpty(engine.ActionResults.Last().Charges, "卡牌不为来源角色攻击充能");
        engine.Deck.Draw(1);
        Assert.Contains(cardId, engine.Hand.ToArray(), "普通技能牌继续回洗");
    }

    [Test]
    public void Heal_Rejects_Enemy_Dead_Missing_And_Group_Entry_Without_Consuming_Anything()
    {
        var engine = Build(); engine.StartBattle(); Inject(engine, "guard"); engine.Deck.Draw(1);
        engine.Ctx.GetUnit("sword").TakeDamage(999);
        int actions = engine.ActionResults.Count;
        foreach (var target in new[] { "sword", engine.Enemies[0].Id, "missing", null })
        {
            Assert.IsFalse(engine.CanPlayCard(0, target));
            Assert.IsFalse(engine.PlayCard(0, target));
        }
        Assert.IsFalse(engine.PlayGroupCard(0)); Assert.IsFalse(engine.PlayRemovalCard(0));
        Assert.AreEqual(actions, engine.ActionResults.Count);
        Assert.AreEqual(1, engine.Hand.Count); Assert.AreEqual(0, engine.Deck.DiscardPileCount);
        engine.Ctx.GetUnit("guard").TakeDamage(10);
        Assert.IsTrue(engine.PlayCard(0, "guard"));
        Assert.AreEqual(160, engine.Ctx.GetUnit("guard").Hp);
        Assert.IsEmpty(engine.ActionResults.Last().Hits); Assert.IsEmpty(engine.ActionResults.Last().Charges);
        Assert.AreEqual(10, engine.ActionResults.Last().UnitChanges.Single(c => c.UnitId == "guard").HpDelta);
    }

    [TestCase(7)]
    [TestCase(17)]
    [TestCase(73)]
    public void Repeated_Mage_Injections_Keep_Duplicates_Conserve_Count_And_Remove_Slag_Only_Once(int seed)
    {
        var engine = Build(seed); engine.StartBattle();
        for (int i = 0; i < 3; i++) Inject(engine, "mage");
        Assert.AreEqual(6, Total(engine));
        int slag = 0, lightning = 0;
        for (int i = 0; i < 15; i++)
        {
            engine.Deck.Draw(1);
            Assert.AreEqual(1, engine.Hand.Count);
            if (engine.Hand[0] == "slag")
            {
                Assert.IsFalse(engine.PlayCard(0, engine.Enemies[0].Id));
                Assert.IsFalse(engine.PlayGroupCard(0));
                int discard = engine.Deck.DiscardPileCount;
                Assert.IsTrue(engine.PlayRemovalCard(0));
                Assert.AreEqual(discard, engine.Deck.DiscardPileCount);
                Assert.AreEqual("slag", engine.ActionResults.Last().RemovedCardId);
                Assert.AreEqual("slag", engine.RoundEvents.Last().RemovedCardId);
                Assert.IsFalse(engine.PlayRemovalCard(0));
                slag++;
            }
            else
            {
                Assert.AreEqual("lightning", engine.Hand[0]);
                Assert.IsTrue(engine.PlayGroupCard(0));
                lightning++;
            }
            Assert.AreEqual(6, Total(engine));
        }
        Assert.AreEqual(3, slag); Assert.AreEqual(3, engine.Deck.RemovedCount);
        Assert.AreEqual(12, lightning, "落雷回洗，但雷渣永久移除");
        Assert.AreEqual(3, engine.Deck.DrawPileCount + engine.Deck.DiscardPileCount);
    }

    [TestCase("setup")]
    [TestCase("queued")]
    [TestCase("occupied")]
    [TestCase("resolving")]
    [TestCase("finished")]
    public void Removal_Requires_Idle_Strategy_Window_And_Invalid_Indices_Do_Nothing(string state)
    {
        var engine = Build();
        engine.Deck.InsertIntoDrawPile("slag"); engine.Deck.Draw(1);
        if (state != "setup") engine.StartBattle();
        if (state == "queued" || state == "occupied")
        {
            engine.Ctx.GetUnit("sword").GainUlt(3);
            Assert.IsTrue(engine.RequestUltimate("sword"));
            if (state == "occupied") Assert.IsNotNull(engine.Advance());
        }
        if (state == "resolving") Assert.IsTrue(engine.BeginActions());
        if (state == "finished")
        {
            engine.Enemies[0].TakeDamage(999999);
            Assert.IsFalse(engine.BeginActions());
        }
        int total = Total(engine), actions = engine.ActionResults.Count;
        foreach (var index in new[] { -1, 0, 1, int.MaxValue })
        {
            Assert.IsFalse(engine.CanPlayRemovalCard(index));
            Assert.IsFalse(engine.PlayRemovalCard(index));
        }
        Assert.AreEqual(actions, engine.ActionResults.Count);
        Assert.AreEqual(total, Total(engine)); Assert.AreEqual(0, engine.Deck.RemovedCount);
        Assert.AreEqual("slag", engine.Hand.Single());
    }

    [Test]
    public void Invalid_Removal_Indices_Do_Not_Consume_Rng_In_Open_Window()
    {
        var a = Build(); var b = Build(); a.StartBattle(); b.StartBattle();
        foreach (var engine in new[] { a, b }) { Inject(engine, "mage"); engine.Deck.Draw(2); }
        foreach (var index in new[] { -1, 2, int.MaxValue }) Assert.IsFalse(a.PlayRemovalCard(index));
        Assert.AreEqual(b.Rng.NextInt(100000), a.Rng.NextInt(100000));
        CollectionAssert.AreEqual(b.Hand, a.Hand);
    }

    [Test]
    public void Generated_Card_Is_Not_Usable_During_Actions_And_Is_Drawn_Next_Round()
    {
        var engine = Build(); engine.StartBattle(); Assert.IsTrue(engine.BeginActions());
        var basic = engine.Advance();
        engine.Ctx.GetUnit("sword").GainUlt(3);
        Assert.IsTrue(engine.RequestUltimate("sword"));
        Assert.IsNull(engine.Advance());
        Assert.IsTrue(engine.Confirm(engine.BattleId, basic.Sequence));
        var injection = engine.Advance();
        CollectionAssert.AreEqual(new[] { "gale_slash" }, injection.InjectedCardIds);
        Assert.IsEmpty(engine.Hand); Assert.IsFalse(engine.CanPlayCards);
        Assert.IsTrue(engine.Confirm(engine.BattleId, injection.Sequence));
        BattleTestDriver.Drain(engine);
        Assert.AreEqual(BattleFlow.Input, engine.Flow);
        CollectionAssert.AreEqual(new[] { "gale_slash" }, engine.Hand);
        Assert.IsTrue(engine.PlayCard(0, engine.Enemies[0].Id));
    }

    [Test]
    public void Fresh_Battle_Clears_Generated_Removed_And_Pending_State_And_Rejects_Old_Confirm()
    {
        var old = Build(); old.StartBattle();
        Inject(old, "mage"); old.Deck.Draw(2);
        Assert.IsTrue(old.PlayRemovalCard(old.Hand.ToList().IndexOf("slag")));
        Assert.AreEqual(1, old.Deck.RemovedCount);
        old.Ctx.GetUnit("sword").GainUlt(3); Assert.IsTrue(old.RequestUltimate("sword"));
        var action = old.Advance();
        old.Ctx.GetUnit("guard").GainUlt(2); Assert.IsTrue(old.RequestUltimate("guard"));
        Assert.AreEqual(1, old.PendingUltimateCount);
        var fresh = Build(initialDeck: true); fresh.StartBattle();
        Assert.AreEqual(5, Total(fresh)); Assert.AreEqual(2, fresh.Hand.Count);
        Assert.AreEqual(0, fresh.Deck.RemovedCount); Assert.AreEqual(0, fresh.PendingUltimateCount);
        Assert.IsEmpty(fresh.ActionResults); Assert.IsFalse(fresh.Confirm(old.BattleId, action.Sequence));
        Assert.IsTrue(fresh.Players.All(u => u.Ult == 0 && u.Hp == u.MaxHp));
    }

    [TestCase("missing")]
    [TestCase("empty")]
    [TestCase("nullEntry")]
    [TestCase("emptyId")]
    [TestCase("zero")]
    [TestCase("negative")]
    [TestCase("damage")]
    [TestCase("heal")]
    public void Invalid_Injection_Does_Not_Spend_Charge_Change_Deck_Or_Consume_Rng(string problem)
    {
        var engine = Build(); engine.StartBattle();
        var skill = engine.UltimateOf("mage");
        switch (problem)
        {
            case "missing": skill.injectCards = null; break;
            case "empty": skill.injectCards.Clear(); break;
            case "nullEntry": skill.injectCards[1] = null; break;
            case "emptyId": skill.injectCards[1].cardId = " "; break;
            case "zero": skill.injectCards[1].count = 0; break;
            case "negative": skill.injectCards[1].count = -1; break;
            case "damage": skill.damage = new DamageSpec { amount = 5 }; break;
            case "heal": skill.heal = new HealSpec { amount = 5 }; break;
        }
        Assert.IsNotNull(ConfigValidator.ValidateInjection(skill));
        var actor = engine.Ctx.GetUnit("mage"); actor.GainUlt(3);
        Assert.IsFalse(engine.RequestUltimate("mage"));
        Assert.AreEqual(3, actor.Ult); Assert.AreEqual(0, Total(engine));
        Assert.IsEmpty(engine.ActionResults);
        Assert.AreEqual(new Rng(7).NextInt(100000), engine.Rng.NextInt(100000));
    }

    [Test]
    public void Unknown_Injected_Card_Reference_Is_Rejected_Before_Battle_Creation()
    {
        var chars = ConfigLoader.LoadCharacters().items;
        var cards = ConfigLoader.LoadCards().items;
        chars[0].skills.Single(s => s.type == "ult").injectCards[0].cardId = "missing";
        Assert.IsNotNull(ConfigValidator.ValidateCardReferences(chars, cards.ToDictionary(c => c.id)));
        Assert.Throws<ArgumentException>(() => BattleEngine.Create(7, chars, new string[0],
            new Dictionary<string, EnemyConfig>(), cards));
    }

    [Test]
    public void Duplicate_Injection_Entries_Are_Not_Deduplicated_And_Event_Count_Is_Exact()
    {
        var engine = Build(); engine.StartBattle();
        var skill = engine.UltimateOf("sword");
        skill.injectCards[0].count = 2;
        skill.injectCards.Add(new CardInjectionSpec { cardId = "gale_slash", count = 1 });
        var result = Inject(engine, "sword");
        Assert.AreEqual(3, engine.Deck.DrawPileCount);
        CollectionAssert.AreEqual(new[] { "gale_slash", "gale_slash", "gale_slash" }, result.InjectedCardIds);
        Assert.AreEqual("gale_slash", engine.RoundEvents.Single().InjectedCardId);
        Assert.AreEqual(3, engine.RoundEvents.Single().InjectedCardCount);
        skill.injectCards.Clear();
        Assert.AreEqual(3, result.InjectedCardIds.Count, "结果不随配置或后续牌堆变化");
        engine.Deck.Draw(3);
        Assert.IsTrue(engine.PlayCard(1, engine.Enemies[0].Id));
        CollectionAssert.AreEqual(new[] { "gale_slash", "gale_slash" }, engine.Hand);
        Assert.AreEqual(1, engine.Deck.DiscardPileCount);
    }

    [TestCase(7)]
    [TestCase(73)]
    public void Same_Seed_With_Real_Injections_Play_Removal_And_PreViews_Has_Identical_Trace(int seed)
    {
        var a = Build(seed, true); var b = Build(seed, true);
        a.StartBattle(); b.StartBattle();
        for (int round = 0; round < 10; round++)
        {
            foreach (var enemy in a.Enemies)
                for (int i = 0; i < 5; i++) a.GetIntentPreview(enemy.Id);
            foreach (var engine in new[] { a, b })
            {
                while (engine.Hand.Count > 0)
                {
                    if (engine.CanPlayRemovalCard(0)) Assert.IsTrue(engine.PlayRemovalCard(0));
                    else if (engine.CanPlayGroupCard(0)) Assert.IsTrue(engine.PlayGroupCard(0));
                    else Assert.IsTrue(engine.PlayCard(0, engine.GetCard(engine.Hand[0]).effect.type == "heal"
                        ? "guard" : engine.Enemies[0].Id));
                }
                Inject(engine, "sword"); Inject(engine, "guard"); Inject(engine, "mage");
                BattleTestDriver.CompleteRound(engine);
            }
            CollectionAssert.AreEqual(a.Hand, b.Hand);
            Assert.AreEqual(a.Deck.DrawPileCount, b.Deck.DrawPileCount);
            Assert.AreEqual(a.Deck.DiscardPileCount, b.Deck.DiscardPileCount);
            Assert.AreEqual(a.Deck.RemovedCount, b.Deck.RemovedCount);
            CollectionAssert.AreEqual(a.Ctx.Units.Select(u => u.Id + ":" + u.Hp + ":" + u.Ult),
                b.Ctx.Units.Select(u => u.Id + ":" + u.Hp + ":" + u.Ult));
            CollectionAssert.AreEqual(a.ActionResults.Select(ActionTrace), b.ActionResults.Select(ActionTrace));
        }
        Assert.AreEqual(a.Rng.NextInt(100000), b.Rng.NextInt(100000));
    }

    private static string ActionTrace(ActionResult action) => action.Sequence + ":" + action.ActorId + ":" +
        action.ActionId + ":" + string.Join(",", action.InjectedCardIds) + ":" + action.RemovedCardId + ":" +
        string.Join(",", action.Hits.Select(hit => hit.TargetUnitId + "=" + hit.HpDamage));

    [TestCase("target")]
    [TestCase("amount")]
    [TestCase("percent")]
    [TestCase("duration")]
    public void Slag_Rejects_Extra_Effects(string problem)
    {
        var card = new CardConfig { id = "slag", name = "雷渣", effect = new CardEffectSpec { type = "remove" } };
        Assert.IsNull(ConfigValidator.ValidateCard(card));
        switch (problem)
        {
            case "target": card.effect.target = "single"; break;
            case "amount": card.effect.amount = 1; break;
            case "percent": card.effect.percent = 1; break;
            case "duration": card.effect.duration = 1; break;
        }
        Assert.IsNotNull(ConfigValidator.ValidateCard(card));
    }
}
