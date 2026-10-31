using System;
using System.Collections.Generic;
using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Config;
using CQ.Runtime;
using NUnit.Framework;

public class TutorialLevelTests
{
    private static LevelConfig Level() => new LevelConfig
    {
        id = 1, name = "自动战斗测试", players = new[] { "hero" },
        waves = new List<WaveConfig>
        {
            new WaveConfig { enemies = new[] { "enemy" }, slots = new[] { "front1" } }
        }
    };

    [Test]
    public void CurrentLevelsReferenceOnlyExistingCharactersEnemiesAndUniqueSlots()
    {
        var heroes = ConfigLoader.LoadCharacters();
        var enemies = ConfigLoader.LoadEnemies();
        var levels = ConfigLoader.LoadLevels();
        Assert.IsEmpty(heroes.errors);
        Assert.IsEmpty(enemies.errors);
        Assert.IsEmpty(levels.errors);
        foreach (var level in levels.items)
        {
            Assert.IsNull(ConfigValidator.ValidateLevelReferences(level, heroes.items, enemies.items.ToDictionary(e => e.id)));
            var engine = BattleEngine.Create(7, heroes.items, level, enemies.items.ToDictionary(e => e.id));
            Assert.Greater(engine.Players.Count, 0);
            Assert.Greater(engine.Enemies.Count, 0);
            Assert.LessOrEqual(engine.Players.Count, 5);
            Assert.LessOrEqual(engine.Enemies.Count, 5);
            Assert.AreEqual(engine.Enemies.Count, engine.Enemies.Select(e => e.SlotId).Distinct().Count());
        }
    }

    [TestCase("missingHero")]
    [TestCase("missingEnemy")]
    [TestCase("duplicateRoster")]
    [TestCase("emptyRoster")]
    [TestCase("slotLength")]
    [TestCase("invalidSlot")]
    [TestCase("duplicateSlot")]
    [TestCase("hpLength")]
    [TestCase("negativeHp")]
    [TestCase("emptyWave")]
    [TestCase("extraWave")]
    public void InvalidLevelIsRejectedBeforeCreatingBattle(string problem)
    {
        var level = Level();
        switch (problem)
        {
            case "missingHero": level.players = new[] { "missing" }; break;
            case "missingEnemy": level.waves[0].enemies[0] = "missing"; break;
            case "duplicateRoster": level.players = new[] { "hero", "hero" }; break;
            case "emptyRoster": level.players = Array.Empty<string>(); break;
            case "slotLength": level.waves[0].slots = Array.Empty<string>(); break;
            case "invalidSlot": level.waves[0].slots[0] = "front3"; break;
            case "duplicateSlot":
                level.waves[0].enemies = new[] { "enemy", "enemy" };
                level.waves[0].slots = new[] { "front1", "front1" }; break;
            case "hpLength": level.waves[0].maxHp = Array.Empty<int>(); break;
            case "negativeHp": level.waves[0].maxHp = new[] { -1 }; break;
            case "emptyWave": level.waves.Clear(); break;
            case "extraWave": level.waves.Add(level.waves[0]); break;
        }
        var roster = new[] { AutoBattleTestData.Hero() };
        var catalog = new Dictionary<string, EnemyConfig> { { "enemy", AutoBattleTestData.Enemy() } };
        Assert.IsNotNull(ConfigValidator.ValidateLevelReferences(level, roster, catalog), problem);
        Assert.Throws<ArgumentException>(() => BattleEngine.Create(7, roster, level, catalog), problem);
    }

    [Test]
    public void PlayerAndGeneratedEnemyRuntimeIdCollisionIsRejectedAtConfigurationBoundary()
    {
        var hero = AutoBattleTestData.Hero("grunt_0");
        var enemy = AutoBattleTestData.Enemy("grunt");
        var level = Level();
        level.players = new[] { hero.id };
        level.waves[0].enemies = new[] { enemy.id };
        var roster = new[] { hero };
        var catalog = new Dictionary<string, EnemyConfig> { { enemy.id, enemy } };
        string error = ConfigValidator.ValidateLevelReferences(level, roster, catalog);
        Assert.IsNotNull(error);
        StringAssert.Contains("grunt_0", error);
        Assert.Throws<ArgumentException>(() => BattleEngine.Create(7, roster, level, catalog));
    }

    [Test]
    public void RosterSubsetAndWaveHpOverridesDoNotMutateCatalogs()
    {
        var hero = AutoBattleTestData.Hero();
        var reserve = AutoBattleTestData.Hero("reserve", "back1");
        var enemy = AutoBattleTestData.Enemy();
        var level = Level();
        level.waves[0].maxHp = new[] { 123 };
        var catalog = new Dictionary<string, EnemyConfig> { { enemy.id, enemy } };
        var engine = BattleEngine.Create(7, new[] { reserve, hero }, level, catalog);
        CollectionAssert.AreEqual(new[] { "hero" }, engine.Players.Select(u => u.Id));
        Assert.AreEqual(123, engine.Enemies[0].MaxHp);
        Assert.AreEqual(123, engine.Enemies[0].Hp);
        Assert.AreEqual(10000, enemy.maxHp);
        Assert.AreEqual("back1", reserve.defaultSlot);
    }
}
