using NUnit.Framework;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using CQ.Core.Combat;
using CQ.Core.Config;
using CQ.Runtime;

public class ConfigLoaderTests
{
    [Test]
    public void Loads_All_Sample_Data_With_Expected_Counts()
    {
        var characters = ConfigLoader.LoadCharacters();
        var enemies = ConfigLoader.LoadEnemies();
        var cards = ConfigLoader.LoadCards();
        var statuses = ConfigLoader.LoadStatuses();
        var levels = ConfigLoader.LoadLevels();

        Assert.IsEmpty(characters.errors, "角色配置有错");
        Assert.IsEmpty(enemies.errors, "敌人配置有错");
        Assert.IsEmpty(cards.errors, "卡配置有错");
        Assert.IsEmpty(statuses.errors, "状态配置有错");
        Assert.IsEmpty(levels.errors, "关卡配置有错");

        Assert.AreEqual(3, characters.items.Count, "角色应为 3");
        Assert.AreEqual(3, enemies.items.Count, "敌人应为 3");
        Assert.AreEqual(6, statuses.items.Count, "含易伤共6种状态");
        Assert.AreEqual(3, levels.items.Count, "关卡应为 3");

        Assert.AreEqual(15, cards.items.Count, "目录包含基础牌、旧卡与四种生成牌");
        CollectionAssert.AreEquivalent(new[]
        {
            "strike", "sweep", "interrupt", "vulnerable", "weaken"
        }, cards.items.Where(c => c.count > 0).Select(c => c.id));
        Assert.AreEqual(5, cards.items.Sum(c => c.count));
        Assert.IsTrue(cards.items.All(c => c.count == 0 || c.count == 1));
        foreach (var card in cards.items.Where(c => c.effect.type == "tauntUp" || c.effect.type == "tauntDown"))
        {
            Assert.Greater(card.effect.amount, 0, card.id);
            Assert.Greater(card.effect.duration, 0, card.id);
        }
    }

    [Test]
    public void Characters_Have_Valid_Unique_DefaultSlots()
    {
        var characters = ConfigLoader.LoadCharacters();
        Assert.IsEmpty(characters.errors);
        Assert.LessOrEqual(characters.items.Count, Formation.Capacity);

        foreach (var c in characters.items)
            Assert.IsTrue(Formation.IsValidSlot(c.defaultSlot), c.id + " defaultSlot 非法");
        Assert.AreEqual(characters.items.Count, characters.items.Select(c => c.defaultSlot).Distinct().Count());
    }

    [Test]
    public void Loads_Formation_With_Valid_Nonnegative_Taunt()
    {
        var formation = ConfigLoader.LoadFormation();
        Assert.IsEmpty(formation.errors);
        Assert.AreEqual(1, formation.items.Count, "阵型规则资源应恰好一份");
        Assert.IsNull(ConfigValidator.ValidateFormation(formation.items[0]));
        Assert.GreaterOrEqual(formation.items[0].frontBaseTaunt, 0);
        Assert.GreaterOrEqual(formation.items[0].backBaseTaunt, 0);
    }

    [Test]
    public void Formation_Defaults_Are_Zero_And_Valid()
    {
        var config = new FormationConfig();
        Assert.AreEqual(0, config.frontBaseTaunt);
        Assert.AreEqual(0, config.backBaseTaunt);
        Assert.IsNull(ConfigValidator.ValidateFormation(config));
        Assert.IsNotNull(ConfigValidator.ValidateFormation(null));
    }

    [Test]
    public void Formation_Json_Omitted_Fields_Load_As_Valid_Zero_Defaults()
    {
        string dir = Path.Combine(Path.GetTempPath(), "cq_formation_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "formation.json"), "{}");
            var result = ConfigJson.LoadAll<FormationConfig>(dir, ConfigValidator.ValidateFormation);
            Assert.IsEmpty(result.errors);
            Assert.AreEqual(1, result.items.Count);
            Assert.AreEqual(0, result.items[0].frontBaseTaunt);
            Assert.AreEqual(0, result.items[0].backBaseTaunt);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [TestCase(-1, 0)]
    [TestCase(0, -1)]
    public void Formation_Rejects_Negative_Taunt(int front, int back)
    {
        Assert.IsNotNull(ConfigValidator.ValidateFormation(
            new FormationConfig { frontBaseTaunt = front, backBaseTaunt = back }));
    }

    private static CharacterConfig ValidCharacter() => new CharacterConfig
    {
        id = "hero", name = "Hero", maxHp = 100, baseSpeed = 10, defaultSlot = "front1",
        maxUlt = 3, ultChargeTrigger = "attack", ultChargeAmount = 1,
        skills = new List<SkillSpec>
        {
            new SkillSpec { id = "basic", name = "Basic", type = "basic",
                damage = new DamageSpec { kind = "physical", target = "single", amount = 1 } },
            new SkillSpec { id = "ult", name = "Ult", type = "ult",
                injectCards = new List<CardInjectionSpec> { new CardInjectionSpec { cardId = "heal", count = 1 } } }
        }
    };

    [TestCase("inject", true)]
    [TestCase("injectEmptyEffects", true)]
    [TestCase("damage", false)]
    [TestCase("heal", false)]
    [TestCase("damageEmptyHeal", false)]
    [TestCase("healEmptyDamage", false)]
    [TestCase("both", false)]
    [TestCase("missing", false)]
    [TestCase("empty", false)]
    [TestCase("partialDamage", false)]
    [TestCase("partialHeal", false)]
    public void JsonUtility_Character_Load_Normalizes_Only_Empty_Optional_Effects(string mode, bool valid)
    {
        const string damage = @"""damage"": { ""kind"": ""magic"", ""target"": ""single"", ""amount"": 10 }";
        const string heal = @"""heal"": { ""target"": ""all"", ""amount"": 20 }";
        const string inject = @", ""injectCards"": [{ ""cardId"": ""heal"", ""count"": 1 }]";
        string effects;
        switch (mode)
        {
            case "inject": effects = inject; break;
            case "injectEmptyEffects": effects = inject + @", ""damage"": {}, ""heal"": {}"; break;
            case "damage": effects = "," + damage; break;
            case "heal": effects = "," + heal; break;
            case "damageEmptyHeal": effects = "," + damage + @", ""heal"": {}"; break;
            case "healEmptyDamage": effects = "," + heal + @", ""damage"": {}"; break;
            case "both": effects = "," + damage + "," + heal; break;
            case "missing": effects = ""; break;
            case "empty": effects = @", ""damage"": {}, ""heal"": {}"; break;
            case "partialDamage": effects = "," + heal + @", ""damage"": { ""kind"": ""physical"" }"; break;
            default: effects = "," + damage + @", ""heal"": { ""target"": ""all"" }"; break;
        }
        string json = @"{
            ""id"": ""hero"", ""name"": ""Hero"", ""maxHp"": 100, ""baseSpeed"": 10,
            ""defaultSlot"": ""front1"", ""maxUlt"": 3,
            ""ultChargeTrigger"": ""attack"", ""ultChargeAmount"": 1,
            ""skills"": [
                { ""id"": ""basic"", ""name"": ""Basic"", ""type"": ""basic"",
                  ""damage"": { ""kind"": ""physical"", ""target"": ""single"", ""amount"": 1 } },
                { ""id"": ""ult"", ""name"": ""Ult"", ""type"": ""ult""" + effects + @" }
            ]
        }";
        string dir = Path.Combine(Path.GetTempPath(), "cq_character_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "hero.json"), json);
            var result = ConfigJson.LoadAll<CharacterConfig>(dir, ConfigLoader.ValidateLoadedCharacter);
            Assert.AreEqual(valid ? 1 : 0, result.items.Count, string.Join("; ", result.errors));
            Assert.AreEqual(valid ? 0 : 1, result.errors.Count);
            if (valid)
            {
                var character = result.items.Single();
                Assert.IsNull(character.skills[0].heal);
                var ult = character.skills[1];
                Assert.IsNull(ult.damage); Assert.IsNull(ult.heal);
                Assert.AreEqual("heal", ult.injectCards.Single().cardId);
                Assert.IsNull(ConfigValidator.ValidateCharacter(character));
            }
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [TestCase("trigger")]
    [TestCase("amountZero")]
    [TestCase("amountNegative")]
    [TestCase("maxUlt")]
    [TestCase("missingUlt")]
    [TestCase("duplicateBasic")]
    [TestCase("duplicateId")]
    [TestCase("nullSkill")]
    [TestCase("oldSkill")]
    [TestCase("badDamageTarget")]
    [TestCase("basicSnipe")]
    [TestCase("ultSnipe")]
    [TestCase("badKind")]
    [TestCase("zeroDamage")]
    [TestCase("badHealTarget")]
    [TestCase("zeroHeal")]
    [TestCase("noEffect")]
    [TestCase("mixedEffect")]
    public void Character_Rejects_Invalid_Schema(string problem)
    {
        var c = ValidCharacter();
        switch (problem)
        {
            case "trigger": c.ultChargeTrigger = "round"; break;
            case "amountZero": c.ultChargeAmount = 0; break;
            case "amountNegative": c.ultChargeAmount = -1; break;
            case "maxUlt": c.maxUlt = 0; break;
            case "missingUlt": c.skills.RemoveAt(1); break;
            case "duplicateBasic": c.skills[1].type = "basic"; break;
            case "duplicateId": c.skills[1].id = c.skills[0].id; break;
            case "nullSkill": c.skills[1] = null; break;
            case "oldSkill": c.skills[1].type = "skill"; break;
            case "badDamageTarget": c.skills[0].damage.target = "friend"; break;
            case "ultSnipe": c.skills[0].damage.target = "snipe"; break;
            case "basicSnipe": c.skills[0].damage.target = "snipe"; break;
            case "badKind": c.skills[0].damage.kind = "unknown"; break;
            case "zeroDamage": c.skills[0].damage.amount = 0; break;
            case "badHealTarget":
                c.skills[1].damage = null; c.skills[1].heal = new HealSpec { target = "enemy", amount = 1 }; break;
            case "zeroHeal":
                c.skills[1].damage = null; c.skills[1].heal = new HealSpec { target = "all", amount = 0 }; break;
            case "noEffect": c.skills[1].injectCards = null; break;
            case "mixedEffect": c.skills[1].heal = new HealSpec { target = "all", amount = 1 }; break;
        }
        Assert.IsNotNull(ConfigValidator.ValidateCharacter(c), problem);
    }

    [TestCase("attack")]
    [TestCase("hit")]
    public void Character_Accepts_Per_Character_Charging_And_Snipe(string trigger)
    {
        var c = ValidCharacter(); c.ultChargeTrigger = trigger;
        c.maxUlt = 7; c.ultChargeAmount = 12;
        Assert.IsNull(ConfigValidator.ValidateCharacter(c), "允许配置充能溢出后截断");
    }

    [TestCase("self")]
    [TestCase("single")]
    [TestCase("all")]
    public void Character_Rejects_Legacy_Heal_Ultimate_Modes(string mode)
    {
        var c = ValidCharacter();
        c.skills[1].injectCards = null;
        c.skills[1].heal = new HealSpec { target = mode, amount = 20 };
        Assert.IsNotNull(ConfigValidator.ValidateCharacter(c));
    }

    [TestCase("front1")]
    [TestCase("front2")]
    [TestCase("back1")]
    [TestCase("back2")]
    [TestCase("back3")]
    public void Character_Accepts_Each_Formation_Slot(string slot)
    {
        var character = ValidCharacter();
        character.defaultSlot = slot;
        Assert.IsNull(ConfigValidator.ValidateCharacter(character));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("front")]
    [TestCase("front3")]
    [TestCase("back4")]
    [TestCase("Front1")]
    public void Character_Rejects_Missing_Or_Invalid_DefaultSlot(string slot)
    {
        var character = ValidCharacter();
        character.defaultSlot = slot;
        Assert.IsNotNull(ConfigValidator.ValidateCharacter(character));
    }

    private static LevelConfig LevelWithSlots(string[] slots, int enemies = 2) => new LevelConfig
    {
        id = 1, name = "Slot validation",
        waves = new List<WaveConfig>
        {
            new WaveConfig { enemies = Enumerable.Repeat("grunt", enemies).ToArray(), slots = slots }
        }
    };

    [Test]
    public void Wave_Accepts_Repeated_Enemy_Ids_With_Unique_Slots()
    {
        Assert.IsNull(ConfigValidator.ValidateLevel(LevelWithSlots(new[] { "front2", "back3" })));
        Assert.IsNull(ConfigValidator.ValidateLevel(LevelWithSlots(
            new[] { "front1", "front2", "back1", "back2", "back3" }, 5)));
    }

    [Test]
    public void Wave_Rejects_Missing_Mismatched_Duplicate_Invalid_Or_Excess_Slots()
    {
        var invalid = new[]
        {
            LevelWithSlots(null),
            LevelWithSlots(new string[0]),
            LevelWithSlots(new[] { "front1" }),
            LevelWithSlots(new[] { "front1", "front2", "back1" }),
            LevelWithSlots(new[] { "front1", "front1" }),
            LevelWithSlots(new[] { "front1", "front3" }),
            LevelWithSlots(new[] { "front1", (string)null }),
            LevelWithSlots(new[] { "front1", "" }),
            LevelWithSlots(new[] { "front1", "front2", "back1", "back2", "back3", "back4" }, 6)
        };
        for (int i = 0; i < invalid.Length; i++)
            Assert.IsNotNull(ConfigValidator.ValidateLevel(invalid[i]), "非法波次用例 " + i);
    }

    [TestCase("tauntUp", 30, 2, true)]
    [TestCase("tauntDown", 30, 2, true)]
    [TestCase("tauntUp", 0, 2, false)]
    [TestCase("tauntDown", -30, 2, false)]
    [TestCase("tauntUp", 30, 0, false)]
    [TestCase("tauntDown", 30, -1, false)]
    public void Slot_Card_Requires_Positive_Amount_And_Duration(string type, int amount, int duration, bool valid)
    {
        var card = new CardConfig
        {
            id = type, name = type, count = 1,
            effect = new CardEffectSpec { type = type, amount = amount, duration = duration }
        };
        Assert.AreEqual(valid, ConfigValidator.ValidateCard(card) == null);
    }

    [TestCase("damage", "single", 20, 0, true)]
    [TestCase("damage", "all", 10, 0, true)]
    [TestCase("damage", null, 20, 0, false)]
    [TestCase("damage", "enemy", 20, 0, false)]
    [TestCase("damage", "single", 0, 0, false)]
    [TestCase("damage", "all", -10, 0, false)]
    [TestCase("vulnerable", "all", 0, 50, true)]
    [TestCase("vulnerable", "all", 0, 0, false)]
    [TestCase("weaken", "all", 0, 25, true)]
    [TestCase("weaken", "all", 0, 101, false)]
    [TestCase("interrupt", "all", 0, 0, false)]
    public void New_Card_Types_Validate_Scope_And_Numbers(string type, string target, int amount, int percent, bool valid)
    {
        var card = new CardConfig
        {
            id = "test", name = "test", count = 1,
            effect = new CardEffectSpec { type = type, target = target, amount = amount, percent = percent }
        };
        Assert.AreEqual(valid, ConfigValidator.ValidateCard(card) == null);
    }

    [Test]
    public void Catalog_Cards_Can_Start_With_Zero_Copies_But_Not_Negative()
    {
        var card = new CardConfig
        {
            id = "test", name = "test", count = 0,
            effect = new CardEffectSpec { type = "damage", target = "all", amount = 10 }
        };
        Assert.IsNull(ConfigValidator.ValidateCard(card));
        card.count = -1;
        Assert.IsNotNull(ConfigValidator.ValidateCard(card));
    }

    [Test]
    public void Bad_Json_Reports_Error()
    {
        string dir = Path.Combine(Path.GetTempPath(), "cq_test_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        // 缺字段：校验报缺字段。
        File.WriteAllText(Path.Combine(dir, "missing.json"), "{ \"id\": \"ghost\" }");

        // 非法 JSON：FromJson 抛异常，被捕获进 errors。
        File.WriteAllText(Path.Combine(dir, "broken.json"), "{ not valid json ");

        var result = ConfigJson.LoadAll<CharacterConfig>(dir, ConfigValidator.ValidateCharacter);

        Assert.AreEqual(0, result.items.Count, "坏配置不应进入 items");
        Assert.AreEqual(2, result.errors.Count, "应报 2 条错误");

        Directory.Delete(dir, true);
    }
}