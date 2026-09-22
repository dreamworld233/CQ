using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;

public class DeterminismTests
{
    private static CharacterConfig Char(string id, int hp, int speed, int atk) => new CharacterConfig
    {
        id = id,
        name = id,
        role = "dps",
        maxHp = hp,
        baseSpeed = speed,
        maxEnergy = 3,
        maxUlt = 3,
        row = "front",
        aggroWeight = 2,
        skills = new List<SkillSpec>
        {
            new SkillSpec { id = id + "_basic", name = "普攻", type = "basic", energyDelta = 1, ultDelta = 1,
                damage = new DamageSpec { kind = "physical", target = "single", amount = atk } },
            new SkillSpec { id = id + "_skill", name = "技能", type = "skill", energyCost = 2, ultDelta = 1,
                damage = new DamageSpec { kind = "physical", target = "single", amount = atk * 2 } },
            new SkillSpec { id = id + "_ult", name = "终结技", type = "ult",
                damage = new DamageSpec { kind = "physical", target = "all", amount = atk * 3 } }
        }
    };

    private static EnemyConfig Lord() => new EnemyConfig
    {
        id = "lord",
        name = "领主",
        maxHp = 200,
        baseSpeed = 7,
        row = "front",
        aggroWeight = 2,
        intentSequence = new[] { "charge", "attack" },
        intents = new List<IntentConfig>
        {
            new IntentConfig { id = "charge", type = "charge", nextDamage = 50, interruptible = true },
            new IntentConfig { id = "attack", type = "attack", target = "single", damage = 20 }
        }
    };

    private static EnemyConfig Claw() => new EnemyConfig
    {
        id = "claw",
        name = "爪牙",
        maxHp = 90,
        baseSpeed = 5,
        row = "front",
        aggroWeight = 2,
        intentSequence = new[] { "slash", "multi" },
        intents = new List<IntentConfig>
        {
            new IntentConfig { id = "slash", type = "attack", target = "single", damage = 14 },
            new IntentConfig { id = "multi", type = "multi", target = "single", damage = 6, hits = 3 }
        }
    };

    private static CardConfig Card(string id, string type, int percent = 0, int amount = 0, int count = 1) =>
        new CardConfig { id = id, name = id, effect = new CardEffectSpec { type = type, percent = percent, amount = amount }, count = count };

    private static BattleEngine Make(long seed, List<CharacterConfig> chars, List<EnemyConfig> enemies, List<CardConfig> cards)
    {
        var ids = enemies.Select(e => e.id).ToList();
        var catalog = enemies.ToDictionary(e => e.id);
        return BattleEngine.Create(seed, chars, ids, catalog, cards);
    }

    private static string Snap(BattleEngine e)
    {
        var sb = new StringBuilder();
        sb.Append(e.Winner?.ToString() ?? "-").Append('|').Append(e.IsFinished).Append('|').Append(e.Round).Append('\n');

        foreach (var u in e.Ctx.Units)
        {
            sb.Append(u.Id).Append(':').Append(u.Hp).Append(':').Append(u.Energy).Append(':').Append(u.Ult).Append(';');
            var fxs = e.Ctx.GetStatuses(u.Id);
            foreach (var fx in fxs)
                sb.Append('[').Append(fx.Type).Append(',').Append(fx.Duration).Append(',').Append(fx.Percent)
                  .Append(',').Append(fx.Amount).Append(',').Append(fx.Turn).Append(']');
            sb.Append('|');
        }
        sb.Append(" hand=").Append(string.Join(",", e.Hand));
        return sb.ToString();
    }

    private static void PlayRoundScripted(BattleEngine e)
    {
        var target = e.Enemies.FirstOrDefault(x => !x.IsDead);
        if (target == null) { e.EndRound(); return; }
        foreach (var p in e.Players)
            if (!p.IsDead)
                e.ChooseSkill(p.Id, p.Id + "_basic", target.Id);
        e.EndRound();
    }

    [Test]
    public void SameSeed_ScriptedBattle_IdenticalStateEveryRound()
    {
        var chars = new List<CharacterConfig> { Char("cA", 100, 10, 12), Char("cB", 160, 6, 8) };
        var enemies = new List<EnemyConfig> { Lord(), Claw() };
        var cards = new List<CardConfig> { Card("interrupt", "interrupt", count: 2), Card("weaken", "weaken", percent: 25) };

        var a = Make(20240601, chars, enemies, cards);
        var b = Make(20240601, chars, enemies, cards);
        a.StartBattle();
        b.StartBattle();
        Assert.AreEqual(Snap(a), Snap(b), "开局快照应一致");

        for (int i = 0; i < 12; i++)
        {
            PlayRoundScripted(a);
            PlayRoundScripted(b);
            Assert.AreEqual(Snap(a), Snap(b), "第 " + (i + 1) + " 回合后状态应一致");
            if (a.IsFinished) break;
        }

        Assert.AreEqual(a.Winner, b.Winner);
    }
}