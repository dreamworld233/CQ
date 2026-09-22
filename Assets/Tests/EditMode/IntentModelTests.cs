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