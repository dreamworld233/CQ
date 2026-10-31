using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CQ.Core.Combat;
using CQ.Core.Config;
using CQ.Runtime;
using NUnit.Framework;
using UnityEngine;

public class ConfigLoaderTests
{
    [Test]
    public void ResourcesLoadAllCurrentData_WithFourSkillsAndSharedEnemyRules()
    {
        var characters = ConfigLoader.LoadCharacters();
        var enemies = ConfigLoader.LoadEnemies();
        var levels = ConfigLoader.LoadLevels();
        var formation = ConfigLoader.LoadFormation();
        Assert.IsEmpty(characters.errors);
        Assert.IsEmpty(enemies.errors);
        Assert.IsEmpty(levels.errors);
        Assert.IsEmpty(formation.errors);
        Assert.AreEqual(3, characters.items.Count);
        Assert.AreEqual(3, enemies.items.Count);
        Assert.AreEqual(3, levels.items.Count);
        Assert.AreEqual(1, formation.items.Count);
        var all = characters.items.Concat(enemies.items.Cast<CharacterConfig>()).ToArray();
        foreach (var unit in all)
        {
            Assert.IsNull(ConfigValidator.ValidateCharacter(unit), unit.id);
            CollectionAssert.AreEquivalent(new[] { "basic", "special", "ult", "passive" }, unit.skills.Select(s => s.type));
            Assert.IsTrue(unit.skills.All(s => s.effects != null && s.effects.Count > 0));
            Assert.AreEqual(20, unit.initialEnergy);
            Assert.AreEqual(100, unit.maxEnergy);
            Assert.AreEqual(40, unit.actionEnergy);
            Assert.AreEqual(10, unit.hitEnergy);
            Assert.AreEqual(2, unit.specialInterval);
        }
        Assert.AreEqual(characters.items.Count, characters.items.Select(c => c.defaultSlot).Distinct().Count());
        var catalog = enemies.items.ToDictionary(e => e.id);
        foreach (var level in levels.items)
            Assert.IsNull(ConfigValidator.ValidateLevelReferences(level, characters.items, catalog), level.name);
    }

    [TestCase("attackUp")]
    [TestCase("defenseUp")]
    public void PermanentPassive_RoundTripsWithoutDuration(string type)
    {
        var hero = AutoBattleTestData.Hero();
        var passive = hero.skills[3];
        passive.trigger = "permanent";
        passive.effects[0] = AutoBattleTestData.Effect(type, "self", multiplier: 0, hits: 0, percent: 10);
        var actual = JsonUtility.FromJson<CharacterConfig>(JsonUtility.ToJson(hero));
        Assert.IsNull(ConfigValidator.ValidateCharacter(actual));
        Assert.AreEqual("permanent", actual.skills[3].trigger);
        Assert.AreEqual(0, actual.skills[3].effects[0].duration);
    }

    [TestCase("energy")]
    [TestCase("allyAll")]
    [TestCase("duration")]
    [TestCase("temporaryWithoutDuration")]
    public void InvalidPermanentOrTemporaryPassive_IsRejected(string problem)
    {
        var hero = AutoBattleTestData.Hero();
        var passive = hero.skills[3];
        passive.trigger = "permanent";
        var effect = passive.effects[0];
        effect.duration = 0;
        switch (problem)
        {
            case "energy": effect.type = "energy"; effect.percent = 0; effect.amount = 10; break;
            case "allyAll": effect.target = "allyAll"; break;
            case "duration": effect.duration = 1; break;
            case "temporaryWithoutDuration": passive.trigger = "roundStart"; break;
        }
        Assert.IsNotNull(ConfigValidator.ValidateCharacter(hero));
    }

    [Test]
    public void RemovedCardsIntentsAndInjectionAreAbsentFromCurrentContracts()
    {
        foreach (var method in new[] { "LoadCards", "LoadStatuses" }) Assert.IsNull(typeof(ConfigLoader).GetMethod(method));
        foreach (var field in new[] { "damage", "heal", "injectCards" }) Assert.IsNull(typeof(SkillSpec).GetField(field));
        foreach (var field in new[] { "intentSequence", "intents" }) Assert.IsNull(typeof(EnemyConfig).GetField(field));
        Assert.IsNull(typeof(LevelConfig).GetField("tutorialDrawOrder"));
        Assert.IsNull(typeof(FormationConfig).GetField("frontBaseTaunt"));
    }

    [TestCase("hp")]
    [TestCase("attack")]
    [TestCase("defense")]
    [TestCase("speed")]
    [TestCase("maxEnergy")]
    [TestCase("negativeInitial")]
    [TestCase("overflowInitial")]
    [TestCase("actionEnergy")]
    [TestCase("hitEnergy")]
    [TestCase("interval")]
    [TestCase("slot")]
    [TestCase("missingSkill")]
    [TestCase("duplicateSkill")]
    [TestCase("duplicateSkillId")]
    [TestCase("passiveTrigger")]
    [TestCase("emptyEffects")]
    [TestCase("unknownTarget")]
    [TestCase("unknownEffect")]
    [TestCase("zeroCount")]
    [TestCase("zeroHits")]
    [TestCase("zeroMultiplier")]
    public void InvalidCharacterConfigurationIsRejected(string problem)
    {
        var unit = AutoBattleTestData.Hero();
        Assert.IsNull(ConfigValidator.ValidateCharacter(unit));
        switch (problem)
        {
            case "hp": unit.maxHp = 0; break;
            case "attack": unit.baseAttack = 0; break;
            case "defense": unit.baseDefense = -1; break;
            case "speed": unit.baseSpeed = 0; break;
            case "maxEnergy": unit.maxEnergy = 0; break;
            case "negativeInitial": unit.initialEnergy = -1; break;
            case "overflowInitial": unit.initialEnergy = 101; break;
            case "actionEnergy": unit.actionEnergy = -1; break;
            case "hitEnergy": unit.hitEnergy = -1; break;
            case "interval": unit.specialInterval = 0; break;
            case "slot": unit.defaultSlot = "missing"; break;
            case "missingSkill": unit.skills.RemoveAt(0); break;
            case "duplicateSkill": unit.skills[1].type = "basic"; break;
            case "duplicateSkillId": unit.skills[1].id = unit.skills[0].id; break;
            case "passiveTrigger": unit.skills[3].trigger = "roundEnd"; break;
            case "emptyEffects": unit.skills[0].effects.Clear(); break;
            case "unknownTarget": unit.skills[0].effects[0].target = "manual"; break;
            case "unknownEffect": unit.skills[0].effects[0].type = "inject"; break;
            case "zeroCount": unit.skills[0].effects[0].count = 0; break;
            case "zeroHits": unit.skills[0].effects[0].hits = 0; break;
            case "zeroMultiplier": unit.skills[0].effects[0].multiplierPercent = 0; break;
        }
        Assert.IsNotNull(ConfigValidator.ValidateCharacter(unit), problem);
    }

    [Test]
    public void JsonRoundTripPreservesEffectsAndEnergyContracts()
    {
        var expected = AutoBattleTestData.Hero();
        var actual = JsonUtility.FromJson<CharacterConfig>(JsonUtility.ToJson(expected));
        Assert.IsNull(ConfigValidator.ValidateCharacter(actual));
        Assert.AreEqual(expected.initialEnergy, actual.initialEnergy);
        Assert.AreEqual(expected.skills.Count, actual.skills.Count);
        Assert.AreEqual(200, actual.skills.Single(s => s.type == "special").effects.Single().multiplierPercent);
        Assert.AreEqual("roundStart", actual.skills.Single(s => s.type == "passive").trigger);
    }

    [Test]
    public void FilesystemLoaderReportsMalformedJsonAndValidationErrorsWithoutReturningPartialObjects()
    {
        string directory = Path.Combine(Path.GetTempPath(), "cq_auto_config_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "valid.json"), JsonUtility.ToJson(AutoBattleTestData.Hero()));
            File.WriteAllText(Path.Combine(directory, "invalid.json"), "{}");
            File.WriteAllText(Path.Combine(directory, "broken.json"), "{ not valid json");
            var result = ConfigJson.LoadAll<CharacterConfig>(directory, ConfigValidator.ValidateCharacter);
            Assert.AreEqual(1, result.items.Count);
            Assert.AreEqual(2, result.errors.Count);
            Assert.IsTrue(result.errors.Any(e => e.Contains("invalid.json")));
            Assert.IsTrue(result.errors.Any(e => e.Contains("broken.json")));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public void MissingResourceDirectoryDoesNotPretendToLoadCharacters()
    {
        var result = ConfigJson.LoadAll<CharacterConfig>(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            ConfigValidator.ValidateCharacter);
        Assert.IsEmpty(result.items);
        Assert.IsNotEmpty(result.errors);
    }
}
