using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;

public class DeterminismTests
{
    private static readonly string[] SlotIds = { "front1", "front2", "back1", "back2", "back3" };

    private static FormationConfig TestFormation() =>
        new FormationConfig { frontBaseTaunt = 100, backBaseTaunt = 50 };

    private static void AssignSlots(IReadOnlyList<CharacterConfig> characters)
    {
        Assert.LessOrEqual(characters.Count, SlotIds.Length);
        var used = new HashSet<string>(characters.Where(c => c.defaultSlot != null).Select(c => c.defaultSlot));
        foreach (var c in characters)
            if (c.defaultSlot == null)
            {
                c.defaultSlot = SlotIds.First(slot => !used.Contains(slot));
                used.Add(c.defaultSlot);
            }
        Assert.AreEqual(characters.Count, characters.Select(c => c.defaultSlot).Distinct().Count());
        Assert.IsTrue(characters.All(c => SlotIds.Contains(c.defaultSlot)));
    }

    private static CharacterConfig Char(string id, int hp, int speed, int atk) => new CharacterConfig
    {
        id = id,
        name = id,
        role = "dps",
        maxHp = hp,
        baseSpeed = speed,
        ultChargeTrigger = "attack", ultChargeAmount = 1,
        maxUlt = 3,
        skills = new List<SkillSpec>
        {
            new SkillSpec { id = id + "_basic", name = "普攻", type = "basic",
                damage = new DamageSpec { kind = "physical", target = "single", amount = atk } },
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
        AssignSlots(chars);
        var ids = enemies.Select(e => e.id).ToList();
        var catalog = enemies.ToDictionary(e => e.id);
        return BattleEngine.Create(seed, chars, ids, catalog, cards,
            SlotIds.Take(ids.Count).ToArray(), TestFormation());
    }

    private static string Snap(BattleEngine e)
    {
        var sb = new StringBuilder();
        sb.Append(e.Winner?.ToString() ?? "-").Append('|').Append(e.IsFinished).Append('|').Append(e.Round).Append('\n');

        foreach (var u in e.Ctx.Units)
        {
            sb.Append(u.Id).Append(':').Append(u.SlotId).Append(':').Append(u.Row).Append(':')
              .Append(u.Hp).Append(':').Append(u.Ult).Append(';');
            var fxs = e.Ctx.GetStatuses(u.Id);
            foreach (var fx in fxs)
                sb.Append('[').Append(fx.Type).Append(',').Append(fx.Duration).Append(',').Append(fx.Percent)
                  .Append(',').Append(fx.Amount).Append(',').Append(fx.Turn).Append(']');
            sb.Append('|');
        }
        sb.Append(" hand=").Append(string.Join(",", e.Hand));
        foreach (var ev in e.RoundEvents)
            sb.Append(" event=").Append(ev.Kind).Append(':').Append(ev.ActorId).Append(':')
              .Append(ev.ActionId).Append(':').Append(ev.TargetId).Append(':')
              .Append(ev.HpDelta).Append(':').Append(ev.TargetDead);
        foreach (var action in e.ActionResults)
        {
            sb.Append(" action=").Append(action.Sequence).Append(':').Append(action.ActorId).Append(':').Append(action.ActionId);
            foreach (var hit in action.Hits) sb.Append(" hit=").Append(hit.TargetUnitId).Append(':').Append(hit.HpDamage).Append(':').Append(hit.ShieldAbsorbed);
            foreach (var charge in action.Charges) sb.Append(" charge=").Append(charge.UnitId).Append(':').Append(charge.After);
        }
        foreach (var team in new[] { Team.Player, Team.Enemy })
            foreach (var slot in e.Ctx.FormationOf(team).Slots)
                sb.Append(" slot=").Append(team).Append(':').Append(slot.Id).Append(':').Append(slot.EffectiveTaunt);
        return sb.ToString();
    }

    private static void PlayRoundScripted(BattleEngine e)
    {
        var target = e.Enemies.FirstOrDefault(x => !x.IsDead);
        if (target == null) { BattleTestDriver.CompleteRound(e); return; }
        foreach (var p in e.Players)
            if (!p.IsDead)
                BattleTestDriver.RequestAndPlay(e, p.Id);
        BattleTestDriver.CompleteRound(e);
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