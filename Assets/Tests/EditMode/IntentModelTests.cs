using NUnit.Framework;
using System.Collections.Generic;
using CQ.Core.Combat;
using CQ.Core.Config;

public class IntentModelTests
{
    private static IntentConfig Intent(string id, string type) =>
        new IntentConfig { id = id, type = type, target = "single" };

    private static EnemyConfig Config()
    {
        return new EnemyConfig
        {
            id = "e",
            intentSequence = new string[] { "basic", "charge" },
            intents = new List<IntentConfig>
            {
                Intent("basic", "attack"),
                Intent("charge", "charge")
            }
        };
    }

    [Test]
    public void Reading_Revealed_Intent_Does_Not_Advance_Sequence()
    {
        var model = new IntentModel(Config());
        var revealed = model.CurrentIntent;
        for (int i = 0; i < 5; i++)
        {
            Assert.AreSame(revealed, model.CurrentIntent);
            Assert.AreEqual("basic", model.CurrentIntentId);
            Assert.AreEqual(0, model.Index);
        }
    }

    [Test]
    public void Resolve_Model_Executes_Current_Intent_Then_Advances()
    {
        var config = Config();
        config.intents[0].damage = 12;
        var model = new IntentModel(config);
        var player = new Unit("p", Team.Player) { MaxHp = 100, Hp = 100, SlotId = "front1" };
        var enemy = new Unit("e", Team.Enemy) { MaxHp = 100, Hp = 100, SlotId = "front1" };
        var ctx = new CombatContext(new CQ.Core.Battle.Rng(1), new[] { player, enemy });

        new EnemyActionResolver().Resolve(ctx, "e", model);

        Assert.AreEqual(88, player.Hp);
        Assert.AreEqual("charge", model.CurrentIntentId);
        Assert.AreEqual(1, model.Index);
    }

    [Test]
    public void Sequence_Cycles_Back_To_First_After_Two_Advances()
    {
        var model = new IntentModel(Config());

        Assert.AreEqual("basic", model.CurrentIntent.id);
        Assert.AreEqual("attack", model.CurrentIntent.type);

        model.Advance();
        Assert.AreEqual("charge", model.CurrentIntent.id);
        Assert.AreEqual("charge", model.CurrentIntent.type);

        model.Advance();
        Assert.AreEqual("basic", model.CurrentIntent.id);
        Assert.AreEqual(0, model.Index);
    }
}