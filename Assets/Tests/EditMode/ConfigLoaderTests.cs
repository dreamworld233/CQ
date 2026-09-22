using NUnit.Framework;
using System.IO;
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
        Assert.AreEqual(5, statuses.items.Count, "状态应为 5");
        Assert.AreEqual(3, levels.items.Count, "关卡应为 3");

        int cardCopies = 0;
        foreach (var c in cards.items) cardCopies += c.count;
        Assert.AreEqual(8, cardCopies, "卡总张数应为 8");
    }

    [Test]
    public void Characters_Have_Row_And_AggroWeight()
    {
        var characters = ConfigLoader.LoadCharacters();
        Assert.IsEmpty(characters.errors);

        foreach (var c in characters.items)
        {
            Assert.IsFalse(string.IsNullOrEmpty(c.row), c.id + " 缺 row");
            Assert.GreaterOrEqual(c.aggroWeight, 0, c.id + " aggroWeight 不可负");
        }
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