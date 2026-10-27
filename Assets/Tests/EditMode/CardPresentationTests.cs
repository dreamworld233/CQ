using System.Collections.Generic;
using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Cards;
using CQ.Core.Config;
using CQ.Runtime;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardPresentationTests
{
    [Test]
    public void Pile_Contents_Aggregate_Copies_In_Id_Order_And_Are_Detached()
    {
        var deck = new Deck(new Rng(7), new[] { ("z", 2), ("a", 1) });
        var snapshot = deck.DrawPileContents;
        CollectionAssert.AreEqual(new[] { "a", "z" }, snapshot.Keys);
        Assert.AreEqual(2, snapshot["z"]);
        deck.Draw(1);
        Assert.AreEqual(3, snapshot.Values.Sum());
        Assert.AreEqual(2, deck.DrawPileContents.Values.Sum());
        ((IDictionary<string, int>)snapshot)["z"] = 99;
        Assert.AreEqual(2, deck.DrawPileCount);
        Assert.LessOrEqual(deck.DrawPileContents["z"], 2);
    }

    [Test]
    public void Pile_Inspection_Does_Not_Change_Draws_Or_Random_State()
    {
        var rng = new Rng(73);
        var controlRng = new Rng(73);
        var deck = new Deck(rng, new[] { ("a", 3), ("b", 3) });
        var control = new Deck(controlRng, new[] { ("a", 3), ("b", 3) });
        for (int i = 0; i < 15; i++)
        {
            Assert.AreEqual(deck.DrawPileCount, deck.DrawPileContents.Values.Sum());
            Assert.AreEqual(deck.DiscardPileCount, deck.DiscardPileContents.Values.Sum());
            deck.Draw(1); control.Draw(1);
            CollectionAssert.AreEqual(control.Hand, deck.Hand);
            Assert.AreEqual(control.Play(0), deck.Play(0));
        }
        Assert.AreEqual(controlRng.NextInt(100000), rng.NextInt(100000));
    }

    [Test]
    public void Removed_Cards_Are_Not_In_Discard_Composition()
    {
        var deck = new Deck(new Rng(1), new[] { ("slag", 1) });
        deck.Draw(1); deck.Remove(0);
        Assert.IsEmpty(deck.DrawPileContents);
        Assert.IsEmpty(deck.DiscardPileContents);
        Assert.AreEqual(1, deck.RemovedCount);
        deck.InsertIntoDrawPile("skill");
        deck.Draw(1); deck.Play(0);
        Assert.AreEqual(1, deck.DiscardPileContents["skill"]);
    }

    [Test]
    public void Hand_And_Pile_Controls_Reset_And_Respect_Action_Window()
    {
        var existingEventSystem = EventSystem.current;
        var owner = new GameObject("UiTest");
        try
        {
            var ui = BattleUi.Build(owner.transform);
            if (ui.CanvasRect == null)
                typeof(BattleUi).GetMethod("BuildAll", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).Invoke(ui, null);
            var characters = ConfigLoader.LoadCharacters().items;
            var cards = ConfigLoader.LoadCards().items;
            var enemy = new EnemyConfig { id = "bag", name = "沙包", maxHp = 99999, baseSpeed = 1,
                intentSequence = new[] { "attack" }, intents = new List<IntentConfig>
                { new IntentConfig { id = "attack", type = "attack", target = "single", damage = 1 } } };
            var engine = BattleEngine.Create(7, characters, new[] { "bag" },
                new Dictionary<string, EnemyConfig> { { "bag", enemy } }, cards);
            engine.StartBattle();
            ui.RefreshHand(engine, -1, "");
            var hand = ui.CanvasRect.Find("Hand/HandViewport/Cards");
            Assert.AreEqual(2, hand.childCount);
            Assert.IsTrue(hand.GetChild(0).GetComponent<Button>().interactable);
            StringAssert.Contains("<b>", hand.GetChild(0).GetComponentInChildren<TextMeshProUGUI>().text);
            var pile = ui.CanvasRect.GetComponentsInChildren<Button>().Single(b =>
                b.GetComponentInChildren<TextMeshProUGUI>().text.StartsWith("查看牌堆"));
            bool cancelled = false;
            ui.OnCancelCard = () => cancelled = true;
            pile.onClick.Invoke();
            Assert.IsTrue(cancelled);
            Assert.IsTrue(ui.CanvasRect.Find("PileOverlay").gameObject.activeSelf);
            var lesson = ConfigLoader.LoadLevels().items.Single(level => level.id == 1);
            ui.RefreshTutorial(lesson, engine);
            var tutorial = ui.CanvasRect.Find("Tutorial");
            Assert.IsTrue(tutorial.gameObject.activeSelf);
            Assert.IsTrue(tutorial.GetComponentsInChildren<TextMeshProUGUI>().Any(text => text.text == lesson.tutorialHints[0]));
            engine.BeginActions();
            ui.RefreshHand(engine, -1, "");
            ui.RefreshTutorial(lesson, engine);
            Assert.IsFalse(hand.GetChild(0).GetComponent<Button>().interactable);
            Assert.IsTrue(tutorial.GetComponentsInChildren<TextMeshProUGUI>().Any(text => text.text == lesson.tutorialHints[0]));
            BattleTestDriver.Drain(engine);
            ui.RefreshTutorial(lesson, engine);
            Assert.IsTrue(tutorial.GetComponentsInChildren<TextMeshProUGUI>().Any(text => text.text == lesson.tutorialHints[1]));
            Assert.IsTrue(engine.BeginActions());
            BattleTestDriver.Drain(engine);
            ui.RefreshTutorial(lesson, engine);
            Assert.IsTrue(engine.SkippedRoundDraw);
            Assert.IsTrue(tutorial.GetComponentsInChildren<TextMeshProUGUI>().Any(text =>
                text.text == lesson.tutorialHints[2] + " 本轮因满手跳抽，腾位不会补抽。"));
            ui.RefreshTutorial(null, engine);
            Assert.IsFalse(tutorial.gameObject.activeSelf);
            ui.RefreshHand(null, -1, "");
            Assert.IsFalse(ui.CanvasRect.Find("PileOverlay").gameObject.activeSelf);
            Assert.IsFalse(pile.interactable);
            Assert.IsFalse(hand.GetChild(0).gameObject.activeSelf);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            if (existingEventSystem == null && EventSystem.current != null)
                Object.DestroyImmediate(EventSystem.current.gameObject);
        }
    }
}
