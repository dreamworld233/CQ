using System;
using System.Linq;
using System.Reflection;
using CQ.Core.Battle;
using CQ.Core.Cards;
using CQ.Core.Combat;
using CQ.Core.Config;
using CQ.Runtime;
using NUnit.Framework;
using UnityEngine;

public class TutorialLevelTests
{
    private static LevelConfig Level(int id) => ConfigLoader.LoadLevels().items.Single(level => level.id == id);

    private static BattleEngine Build(int id, long seed) => BattleEngine.Create(seed,
        ConfigLoader.LoadCharacters().items, Level(id), ConfigLoader.LoadEnemies().items.ToDictionary(enemy => enemy.id),
        ConfigLoader.LoadCards().items, ConfigLoader.LoadFormation().items[0]);

    private static int Card(BattleEngine engine, string id)
    {
        int index = engine.Hand.ToList().IndexOf(id);
        Assert.GreaterOrEqual(index, 0, "手牌缺少 " + id);
        return index;
    }

    private static void CompleteRound(BattleEngine engine, string requestActor)
    {
        Assert.IsTrue(engine.BeginActions());
        for (int step = 0; step < 100; step++)
        {
            var result = engine.CurrentAction ?? engine.Advance();
            if (result == null) return;
            if (engine.CanRequestUltimate(requestActor)) Assert.IsTrue(engine.RequestUltimate(requestActor));
            Assert.IsTrue(engine.Confirm(engine.BattleId, result.Sequence));
        }
        Assert.Fail("教学动作循环超出上限");
    }

    [TestCase(7)]
    [TestCase(17)]
    [TestCase(37)]
    [TestCase(73)]
    [TestCase(101)]
    [TestCase(12345)]
    public void First_Lesson_Interrupt_Leaves_Guard_To_Finish_Second_Round(int seed)
    {
        var engine = Build(1, seed);
        CollectionAssert.AreEqual(new[] { "sword", "guard" }, engine.Players.Select(player => player.Id));
        engine.StartBattle();
        CollectionAssert.AreEqual(new[] { "weaken", "interrupt" }, engine.Hand);
        var enemy = engine.Enemies.Single();
        CollectionAssert.AreEqual(new[] { "sword", enemy.Id, "guard" }, engine.CurrentOrder.Select(unit => unit.Id));
        Assert.AreEqual("all", engine.CurrentIntentOf(enemy.Id).target);
        Assert.IsTrue(engine.PlayGroupCard(Card(engine, "weaken")));
        BattleTestDriver.CompleteRound(engine);
        Assert.AreEqual(2, engine.CurrentRound);
        Assert.AreEqual(20, enemy.Hp);
        Assert.AreEqual(70, engine.Ctx.GetUnit("sword").Hp);
        Assert.AreEqual(130, engine.Ctx.GetUnit("guard").Hp);
        Assert.AreEqual("single", engine.CurrentIntentOf(enemy.Id).target);
        Assert.Greater(engine.CurrentIntentOf(enemy.Id).damage, engine.Players.Max(player => player.Hp));
        Assert.IsTrue(engine.PlayCard(Card(engine, "interrupt"), enemy.Id));
        BattleTestDriver.CompleteRound(engine);
        Assert.AreEqual(Team.Player, engine.Winner);
        Assert.AreEqual(2, engine.CurrentRound);
        Assert.AreEqual("guard", engine.ActionResults.Last().ActorId);
        Assert.AreEqual(70, engine.Ctx.GetUnit("sword").Hp);
        Assert.AreEqual(130, engine.Ctx.GetUnit("guard").Hp);
    }

    [TestCase(7)]
    [TestCase(17)]
    [TestCase(37)]
    [TestCase(73)]
    [TestCase(101)]
    [TestCase(12346)]
    public void Second_Lesson_Third_Attack_Injection_Is_Drawn_Next_Round_And_Bypasses_Front(int seed)
    {
        var engine = Build(2, seed);
        engine.StartBattle();
        var lord = engine.Enemies.Single(enemy => enemy.Id.StartsWith("lord"));
        Assert.IsTrue(engine.PlayGroupCard(Card(engine, "weaken")));
        BattleTestDriver.CompleteRound(engine);
        Assert.IsTrue(engine.PlayCard(Card(engine, "interrupt"), lord.Id));
        BattleTestDriver.CompleteRound(engine);
        Assert.AreEqual(2, engine.Ctx.GetUnit("sword").Ult);
        Assert.IsFalse(engine.CanRequestUltimate("sword"));
        CompleteRound(engine, "sword");
        Assert.AreEqual(4, engine.CurrentRound);
        Assert.AreEqual(1, engine.ActionResults.Count(action => action.InjectedCardIds.Contains("gale_slash")));
        Assert.AreEqual(0, engine.Ctx.GetUnit("sword").Ult);
        Assert.AreEqual(40, lord.Hp, "普攻不越过仍存活的前排小弟");
        Assert.IsTrue(engine.Enemies.All(enemy => !enemy.IsDead));
        Assert.Less(engine.Ctx.EffectiveTaunt(lord), engine.Ctx.EffectiveTaunt(engine.Enemies[1]));
        Assert.IsTrue(engine.PlayCard(Card(engine, "gale_slash"), lord.Id));
        Assert.IsTrue(lord.IsDead);
        Assert.AreEqual(1, engine.Deck.DiscardPileContents["gale_slash"]);
        Assert.IsFalse(engine.IsFinished, "领主死亡后仍需处理小弟");
        Assert.AreEqual(6, engine.Hand.Count + engine.Deck.DrawPileCount + engine.Deck.DiscardPileCount);
    }

    [TestCase(7)]
    [TestCase(17)]
    [TestCase(37)]
    [TestCase(73)]
    [TestCase(101)]
    [TestCase(12347)]
    public void Third_Lesson_Slag_Precedes_Lightning_And_Group_Combo_Finishes_Two_Enemies(int seed)
    {
        var engine = Build(3, seed);
        engine.StartBattle();
        Assert.AreEqual(3, engine.Players.Count);
        Assert.IsTrue(engine.PlayGroupCard(Card(engine, "weaken")));
        BattleTestDriver.CompleteRound(engine);
        Assert.IsTrue(engine.PlayCard(Card(engine, "interrupt"), engine.Enemies[0].Id));
        BattleTestDriver.CompleteRound(engine);
        Assert.AreEqual(2, engine.Ctx.GetUnit("mage").Ult);
        CompleteRound(engine, "mage");
        Assert.AreEqual(4, engine.CurrentRound);
        Assert.IsTrue(engine.Hand.Contains("slag"));
        Assert.IsFalse(engine.Hand.Contains("lightning"));
        Assert.AreEqual(1, engine.Deck.DrawPileContents["lightning"]);
        Assert.IsTrue(engine.PlayRemovalCard(Card(engine, "slag")));
        Assert.AreEqual(1, engine.Deck.RemovedCount);
        Assert.IsFalse(engine.Deck.DiscardPileContents.ContainsKey("slag"));
        BattleTestDriver.CompleteRound(engine);
        Assert.AreEqual(5, engine.CurrentRound);
        Assert.IsTrue(engine.Hand.Contains("lightning"));
        Assert.AreEqual(2, engine.Enemies.Count(enemy => !enemy.IsDead));
        Assert.IsTrue(engine.PlayGroupCard(Card(engine, "sweep")));
        Assert.IsTrue(engine.Enemies.Where(enemy => !enemy.IsDead).All(enemy => enemy.Hp <= 30));
        Assert.IsTrue(engine.PlayGroupCard(Card(engine, "lightning")));
        Assert.AreEqual(Team.Player, engine.Winner);
        Assert.AreEqual(5, engine.CurrentRound);
        Assert.AreEqual(1, engine.Deck.DiscardPileContents["lightning"]);
        Assert.AreEqual(7, engine.Hand.Count + engine.Deck.DrawPileCount + engine.Deck.DiscardPileCount + engine.Deck.RemovedCount);
    }

    [Test]
    public void Missed_First_Lesson_Interrupt_Can_Continue_With_Current_Intent_And_Recovery_Hint()
    {
        var engine = Build(1, 7);
        Assert.IsTrue(engine.TryMovePlayer("sword", "back1"));
        engine.StartBattle();
        Assert.IsTrue(engine.PlayGroupCard(Card(engine, "weaken")));
        BattleTestDriver.CompleteRound(engine);
        BattleTestDriver.CompleteRound(engine);
        Assert.AreEqual(3, engine.CurrentRound);
        Assert.IsTrue(engine.Ctx.GetUnit("guard").IsDead);
        Assert.IsFalse(engine.IsFinished);
        Assert.AreEqual("all", engine.CurrentIntentOf(engine.Enemies[0].Id).target);
        var lesson = Level(1);
        Assert.GreaterOrEqual(lesson.tutorialHints.Length, 3);
        StringAssert.Contains("当前意图", lesson.tutorialHints[2]);
        Assert.IsTrue(engine.PlayCard(Card(engine, "strike"), engine.Enemies[0].Id));
        Assert.AreEqual(Team.Player, engine.Winner);
        Assert.AreEqual(3, engine.CurrentRound);
    }

    [Test]
    public void Tutorial_Draw_Uses_Only_Existing_Cards_And_Waits_Through_Full_Hand()
    {
        var deck = new Deck(new Rng(1), new[] { ("a", 3) }, new[] { "a", "a", "a", "skill" });
        deck.Draw(3);
        deck.InsertIntoDrawPile("skill");
        deck.Draw(1);
        Assert.AreEqual(1, deck.DrawPileCount);
        deck.Play(0);
        deck.Draw(1);
        Assert.IsTrue(deck.Hand.Contains("skill"));
        Assert.AreEqual(0, deck.DrawPileCount);
        Assert.AreEqual(4, deck.Hand.Count + deck.DiscardPileCount);
    }

    [Test]
    public void Missing_Scheduled_Injection_Falls_Back_Without_Creating_Or_Blocking_Cards()
    {
        var deck = new Deck(new Rng(1), new[] { ("base", 1) }, new[] { "skill" });
        deck.Draw(2);
        CollectionAssert.AreEqual(new[] { "base" }, deck.Hand);
        deck.Play(0); deck.Draw(1);
        CollectionAssert.AreEqual(new[] { "base" }, deck.Hand);
        deck.InsertIntoDrawPile("skill");
        deck.Draw(1);
        CollectionAssert.AreEqual(new[] { "base", "skill" }, deck.Hand);
    }

    [Test]
    public void Ordinary_Battle_Has_No_Tutorial_Draw_Order_And_Level_Does_Not_Mutate_Catalogs()
    {
        var characters = ConfigLoader.LoadCharacters().items;
        var enemies = ConfigLoader.LoadEnemies().items.ToDictionary(enemy => enemy.id);
        var cards = ConfigLoader.LoadCards().items;
        var level = Level(2);
        var engine = BattleEngine.Create(7, characters, level, enemies, cards);
        Assert.AreEqual(3, characters.Count);
        Assert.AreEqual(200, enemies["lord"].maxHp);
        Assert.AreEqual(60, enemies["claw"].maxHp);
        Assert.IsTrue(cards.Where(card => card.count > 0).All(card => card.count == 1));
        level.tutorialDrawOrder = null;
        var ordinary = BattleEngine.Create(7, characters, level, enemies, cards);
        var control = BattleEngine.Create(7, characters.Where(character => character.id != "mage"),
            level.waves[0], enemies, cards);
        ordinary.StartBattle(); control.StartBattle();
        CollectionAssert.AreEqual(control.Hand, ordinary.Hand);
        Assert.AreEqual(control.Rng.NextInt(10000), ordinary.Rng.NextInt(10000));
        Assert.AreEqual(40, engine.Enemies[0].MaxHp);
    }

    [TestCase("players")]
    [TestCase("enemy")]
    [TestCase("card")]
    [TestCase("hp")]
    [TestCase("duplicate")]
    [TestCase("disclosure")]
    public void Invalid_Tutorial_Config_Is_Rejected_Before_Battle(string invalid)
    {
        var level = Level(2);
        switch (invalid)
        {
            case "players": level.players[0] = "missing"; break;
            case "enemy": level.waves[0].enemies[0] = "missing"; break;
            case "card": level.tutorialDrawOrder[0] = "missing"; break;
            case "hp": level.waves[0].maxHp = new[] { -1 }; break;
            case "duplicate": level.players = new[] { "sword", "sword" }; break;
            case "disclosure": level.tutorialIntro = null; break;
        }
        Assert.Throws<ArgumentException>(() => BattleEngine.Create(7, ConfigLoader.LoadCharacters().items, level,
            ConfigLoader.LoadEnemies().items.ToDictionary(enemy => enemy.id), ConfigLoader.LoadCards().items));
    }

    [Test]
    public void New_Mage_Does_Not_Overwrite_Saved_Two_Player_Formation()
    {
        var owner = new GameObject("TutorialFormationTest");
        try
        {
            var controller = owner.AddComponent<BattleController>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var field = typeof(BattleController).GetField("_engine", flags);
            var old = Build(2, 7);
            Assert.IsTrue(old.TryMovePlayer("sword", "back1"));
            Assert.IsTrue(old.TryMovePlayer("guard", "front1"));
            field.SetValue(controller, old);
            typeof(BattleController).GetMethod("SaveFormation", flags).Invoke(controller, null);
            var next = Build(3, 7);
            field.SetValue(controller, next);
            typeof(BattleController).GetMethod("RestoreFormation", flags).Invoke(controller, null);
            Assert.AreEqual("back1", next.Ctx.GetUnit("sword").SlotId);
            Assert.AreEqual("front1", next.Ctx.GetUnit("guard").SlotId);
            Assert.AreEqual(3, next.Players.Select(player => player.SlotId).Distinct().Count());
            Assert.IsNotNull(next.Ctx.GetUnit("mage"));
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }
}
