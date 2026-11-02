using System;
using System.Collections.Generic;
using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;
using CQ.Runtime;
using NUnit.Framework;

public class PartyRosterTests
{
    [Test]
    public void NewPartnerBecomesAvailableWithoutAutomaticallyDeploying_AndBenchChoicePersists()
    {
        var chars = ConfigLoader.LoadCharacters().items;
        var first = new PartyRoster(chars, new[] { "sword", "guard" });
        Assert.AreEqual(2, first.Deployment.Count);
        first.TryToggle("guard", out _);
        var next = new PartyRoster(chars, new[] { "sword", "guard", "mage" }, first);
        Assert.AreEqual(3, next.Available.Count);
        CollectionAssert.AreEquivalent(new[] { "sword" }, next.Deployment.Keys);
        Assert.IsFalse(next.IsDeployed("mage"));
        var retry = new PartyRoster(chars, new[] { "sword", "guard", "mage" }, next);
        CollectionAssert.AreEquivalent(next.Deployment, retry.Deployment);
    }

    [Test]
    public void CatalogCanExceedFive_OnlyFiveCanDeploy_AndOccupiedPreferencesUseEmptySlots()
    {
        var chars = Characters(7);
        var party = new PartyRoster(chars, chars.Select(character => character.id));
        Assert.AreEqual(7, party.Available.Count);
        Assert.AreEqual(5, party.Deployment.Count);
        Assert.AreEqual(5, party.Deployment.Values.Distinct().Count());
        Assert.IsFalse(party.TryToggle("hero5", out var error));
        StringAssert.Contains("已满", error);
        party.TryToggle("hero0", out _);
        Assert.IsTrue(party.TryToggle("hero5", out error));
        Assert.IsNull(error);
        Assert.AreEqual(5, party.Deployment.Count);
        Assert.IsFalse(party.IsDeployed("hero0"));
    }

    [Test]
    public void BenchAndRedeployRememberDraggedPosition_WithoutBringingBackBenchedUnits()
    {
        var chars = Characters(2);
        var party = new PartyRoster(chars, null);
        var engine = Build(chars, party.Deployment);
        engine.TryMovePlayer("hero0", "back3");
        party.CapturePositions(engine.Players);
        party.TryToggle("hero0", out _);
        party.CapturePositions(engine.Players);
        Assert.IsFalse(party.IsDeployed("hero0"));
        var rebuilt = new PartyRoster(chars, null, party);
        rebuilt.TryToggle("hero0", out _);
        Assert.AreEqual("back3", rebuilt.Deployment["hero0"]);
        Assert.AreEqual(2, rebuilt.Deployment.Values.Distinct().Count());
    }

    [Test]
    public void EmptyDeploymentIsEditableSetup_NotAnAutomaticDefeat_AndCannotStart()
    {
        var chars = Characters(1);
        var party = new PartyRoster(chars, null);
        party.TryToggle("hero0", out _);
        Assert.IsFalse(party.CanStart);
        var engine = Build(chars, party.Deployment);
        Assert.IsEmpty(engine.Players);
        Assert.IsFalse(engine.StartBattle());
        Assert.AreEqual(BattleFlow.Setup, engine.Flow);
        Assert.IsFalse(engine.IsFinished);
        Assert.IsNull(engine.Winner);
        Assert.AreEqual(0, engine.Round);
        party.TryToggle("hero0", out _);
        Assert.IsTrue(Build(chars, party.Deployment).StartBattle());
    }

    [Test]
    public void ExplicitDeploymentSupportsLargeCatalogAndDuplicateDefaultSlots()
    {
        var chars = Characters(8);
        var party = new PartyRoster(chars, null);
        var engine = Build(chars, party.Deployment);
        Assert.AreEqual(5, engine.Players.Count);
        foreach (var unit in engine.Players) Assert.AreEqual(party.Deployment[unit.Id], unit.SlotId);
        Assert.IsTrue(engine.StartBattle());
    }

    [TestCase("unknown")]
    [TestCase(null)]
    public void UnavailablePartnerCannotBeToggled(string id)
    {
        var party = new PartyRoster(Characters(2), new[] { "hero0" });
        Assert.IsFalse(party.TryToggle(id, out _));
        Assert.AreEqual(1, party.Deployment.Count);
        Assert.IsFalse(party.TryToggle("hero1", out _));
    }

    [Test]
    public void ExplicitDeploymentRejectsUnknownUnitOrInvalidDuplicateSlots()
    {
        var chars = Characters(2);
        Assert.Throws<ArgumentException>(() => Build(chars, new Dictionary<string, string> { { "missing", "front1" } }));
        Assert.Throws<ArgumentException>(() => Build(chars, new Dictionary<string, string> { { "hero0", "front3" } }));
        Assert.Throws<ArgumentException>(() => Build(chars, new Dictionary<string, string> { { "hero0", "front1" }, { "hero1", "front1" } }));
        Assert.Throws<ArgumentException>(() => Build(Characters(6), Enumerable.Range(0, 6).ToDictionary(i => "hero" + i, i => "front1")));
    }

    [Test]
    public void InvalidPositionCaptureDoesNotPartiallyMutateRoster()
    {
        var party = new PartyRoster(Characters(2), null);
        var before = party.Deployment.ToDictionary(pair => pair.Key, pair => pair.Value);
        Assert.Throws<ArgumentException>(() => party.CapturePositions(new[] { new Unit("hero0", Team.Player) { SlotId = before["hero1"] } }));
        CollectionAssert.AreEquivalent(before, party.Deployment);
    }

    [TestCase("metal")]
    [TestCase("wood")]
    [TestCase("water")]
    [TestCase("fire")]
    [TestCase("earth")]
    public void ElementChangesOnlyMetadata_NotBattleStatsOrSkillOutcomes(string element)
    {
        var chars = Characters(1);
        chars[0].element = element;
        Assert.IsNull(ConfigValidator.ValidateCharacter(chars[0]));
        var engine = Build(chars, new Dictionary<string, string> { { "hero0", "front1" } });
        engine.StartBattle();
        Assert.AreEqual(10, engine.Players[0].BaseAttack);
        ActionResult result;
        do
        {
            result = engine.Advance();
            Assert.IsNotNull(result);
            engine.Confirm(engine.BattleId, result.Sequence);
        } while (result.Hits.Count == 0);
        Assert.AreEqual(10, result.Hits.Single().HpDamage);
    }

    [Test]
    public void InvalidElementIsRejected_AndCurrentOwnedCharactersHaveValidClassifications()
    {
        var hero = AutoBattleTestData.Hero();
        hero.element = "light";
        StringAssert.Contains("element", ConfigValidator.ValidateCharacter(hero));
        Assert.IsTrue(ConfigLoader.LoadCharacters().items.All(character => CharacterElements.IsValid(character.element)));
    }

    internal static CharacterConfig[] Characters(int count) => Enumerable.Range(0, count).Select(i =>
    {
        var character = AutoBattleTestData.Hero("hero" + i);
        character.element = CharacterElements.All[i % CharacterElements.All.Count];
        return character;
    }).ToArray();

    private static BattleEngine Build(CharacterConfig[] chars, IReadOnlyDictionary<string, string> deployment)
    {
        var enemy = AutoBattleTestData.Enemy();
        var level = new LevelConfig
        {
            id = 1, name = "部署测试", players = chars.Select(character => character.id).ToArray(),
            waves = new List<WaveConfig> { new WaveConfig { enemies = new[] { "enemy" }, slots = new[] { "front1" } } }
        };
        return BattleEngine.Create(7, chars, level, new Dictionary<string, EnemyConfig> { { enemy.id, enemy } }, deployment: deployment);
    }
}
