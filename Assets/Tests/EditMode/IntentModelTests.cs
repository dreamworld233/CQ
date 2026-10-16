using NUnit.Framework;
using System.Collections.Generic;
using CQ.Core.Combat;
using CQ.Core.Config;

public class IntentModelTests
{
    private static IntentConfig Intent(string id, string type) =>
        new IntentConfig
        {
            id = id, type = type, target = "single", damage = type == "attack" ? 1 : 0,
            nextDamage = type == "charge" ? 25 : 0, interruptible = type == "charge"
        };

    private static EnemyConfig Config()
    {
        return new EnemyConfig
        {
            id = "e", name = "Enemy", maxHp = 100, baseSpeed = 5,
            intentSequence = new string[] { "basic", "charge" },
            intents = new List<IntentConfig>
            {
                Intent("basic", "attack"),
                Intent("charge", "charge")
            }
        };
    }

    [TestCase("attack")]
    [TestCase("multi")]
    [TestCase("attackUp")]
    [TestCase("charge")]
    [TestCase("debuff")]
    public void Validator_Accepts_All_Five_Independent_Types(string type)
    {
        var c = Config();
        c.intents[0] = new IntentConfig
        {
            id = "basic", type = type, target = "single",
            damage = type == "attack" || type == "multi" ? 10 : 0,
            hits = type == "multi" ? 3 : 0,
            buffPercent = type == "attackUp" ? 25 : 0, buffDuration = type == "attackUp" ? 2 : 0,
            nextDamage = type == "charge" ? 25 : 0, interruptible = type == "charge",
            debuffType = type == "debuff" ? "slow" : null,
            debuffPercent = type == "debuff" ? 25 : 0, debuffDuration = type == "debuff" ? 2 : 0
        };
        Assert.IsNull(ConfigValidator.ValidateEnemy(c));
    }

    [TestCase("attack", "single", "weaken")]
    [TestCase("attack", "all", "slow")]
    [TestCase("multi", "single", "slow")]
    public void Validator_Accepts_Complete_Composite_Effects(string type, string target, string debuff)
    {
        var c = Config();
        var it = c.intents[0];
        it.type = type; it.target = target; it.hits = type == "multi" ? 3 : 0;
        it.buffPercent = 25; it.buffDuration = 2;
        it.debuffType = debuff; it.debuffPercent = 25; it.debuffDuration = 2;
        Assert.IsNull(ConfigValidator.ValidateEnemy(c));
    }

    [TestCase("nullIntent")]
    [TestCase("duplicateId")]
    [TestCase("emptyId")]
    [TestCase("missingReference")]
    [TestCase("nullReference")]
    [TestCase("shortSequence")]
    [TestCase("longSequence")]
    [TestCase("unknownType")]
    [TestCase("zeroDamage")]
    [TestCase("negativeDamage")]
    [TestCase("missingTarget")]
    [TestCase("badTarget")]
    [TestCase("attackHits")]
    [TestCase("multiZeroHits")]
    [TestCase("multiNegativeHits")]
    [TestCase("multiAll")]
    [TestCase("buffPercentOnly")]
    [TestCase("buffDurationOnly")]
    [TestCase("negativeBuff")]
    [TestCase("negativeBuffDuration")]
    [TestCase("debuffTypeOnly")]
    [TestCase("debuffPercentOnly")]
    [TestCase("debuffDurationOnly")]
    [TestCase("debuffTooLarge")]
    [TestCase("debuffNegative")]
    [TestCase("debuffNegativeDuration")]
    [TestCase("debuffShield")]
    [TestCase("debuffInterrupt")]
    [TestCase("debuffAttackUp")]
    [TestCase("unrelatedNextDamage")]
    [TestCase("unrelatedInterruptible")]
    [TestCase("chargeZeroDamage")]
    [TestCase("chargeNotInterruptible")]
    [TestCase("chargeBuff")]
    [TestCase("chargeDebuff")]
    [TestCase("chargeDamage")]
    [TestCase("chargeHits")]
    [TestCase("chargeAll")]
    [TestCase("standaloneBuffMissingDuration")]
    [TestCase("standaloneDebuffMissingDuration")]
    [TestCase("standaloneDebuffAll")]
    public void Validator_Rejects_Invalid_Enemy_Schema(string problem)
    {
        var c = Config();
        var it = c.intents[0];
        var charge = c.intents[1];
        switch (problem)
        {
            case "nullIntent": c.intents[0] = null; break;
            case "duplicateId": charge.id = it.id; break;
            case "emptyId": it.id = " "; break;
            case "missingReference": c.intentSequence[0] = "missing"; break;
            case "nullReference": c.intentSequence[0] = null; break;
            case "shortSequence": c.intentSequence = new[] { "basic" }; break;
            case "longSequence": c.intentSequence = new[] { "basic", "basic", "basic", "basic", "basic" }; break;
            case "unknownType": it.type = "unknown"; break;
            case "zeroDamage": it.damage = 0; break;
            case "negativeDamage": it.damage = -1; break;
            case "missingTarget": it.target = null; break;
            case "badTarget": it.target = "snipe"; break;
            case "attackHits": it.hits = 2; break;
            case "multiZeroHits": it.type = "multi"; break;
            case "multiNegativeHits": it.type = "multi"; it.hits = -1; break;
            case "multiAll": it.type = "multi"; it.hits = 2; it.target = "all"; break;
            case "buffPercentOnly": it.buffPercent = 25; break;
            case "buffDurationOnly": it.buffDuration = 2; break;
            case "negativeBuff": it.buffPercent = -1; it.buffDuration = 2; break;
            case "negativeBuffDuration": it.buffPercent = 25; it.buffDuration = -1; break;
            case "debuffTypeOnly": it.debuffType = "weaken"; break;
            case "debuffPercentOnly": it.debuffPercent = 25; break;
            case "debuffDurationOnly": it.debuffDuration = 2; break;
            case "debuffTooLarge": it.debuffType = "weaken"; it.debuffPercent = 101; it.debuffDuration = 2; break;
            case "debuffNegative": it.debuffType = "slow"; it.debuffPercent = -1; it.debuffDuration = 2; break;
            case "debuffNegativeDuration": it.debuffType = "slow"; it.debuffPercent = 25; it.debuffDuration = -1; break;
            case "debuffShield": it.debuffType = "shield"; it.debuffPercent = 25; it.debuffDuration = 2; break;
            case "debuffInterrupt": it.debuffType = "interrupt"; it.debuffPercent = 25; it.debuffDuration = 2; break;
            case "debuffAttackUp": it.debuffType = "attackUp"; it.debuffPercent = 25; it.debuffDuration = 2; break;
            case "unrelatedNextDamage": it.nextDamage = 10; break;
            case "unrelatedInterruptible": it.interruptible = true; break;
            case "chargeZeroDamage": charge.nextDamage = 0; break;
            case "chargeNotInterruptible": charge.interruptible = false; break;
            case "chargeBuff": charge.buffPercent = 25; charge.buffDuration = 2; break;
            case "chargeDebuff": charge.debuffType = "slow"; charge.debuffPercent = 25; charge.debuffDuration = 2; break;
            case "chargeDamage": charge.damage = 1; break;
            case "chargeHits": charge.hits = 1; break;
            case "chargeAll": charge.target = "all"; break;
            case "standaloneBuffMissingDuration": it.type = "attackUp"; it.damage = 0; it.buffPercent = 25; break;
            case "standaloneDebuffMissingDuration": it.type = "debuff"; it.damage = 0; it.debuffType = "slow"; it.debuffPercent = 25; break;
            case "standaloneDebuffAll": it.type = "debuff"; it.damage = 0; it.target = "all"; it.debuffType = "slow"; it.debuffPercent = 25; it.debuffDuration = 2; break;
        }
        Assert.IsNotNull(ConfigValidator.ValidateEnemy(c), problem);
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