using NUnit.Framework;
using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;
using CQ.Runtime;

public class BalanceTests
{
    private static BattleEngine BuildLevel(int id, long seed)
    {
        var enemies = ConfigLoader.LoadEnemies();
        var cards = ConfigLoader.LoadCards();
        var levels = ConfigLoader.LoadLevels();
        var characters = ConfigLoader.LoadCharacters();
        var formation = ConfigLoader.LoadFormation();

        Assert.IsEmpty(characters.errors);
        Assert.IsEmpty(enemies.errors);
        Assert.IsEmpty(cards.errors);
        Assert.IsEmpty(levels.errors);
        Assert.IsEmpty(formation.errors);
        Assert.AreEqual(1, formation.items.Count);

        var catalog = enemies.items.ToDictionary(e => e.id);
        var level = levels.items.First(l => l.id == id);
        return BattleEngine.Create(seed, characters.items, level.waves[0], catalog, cards.items, formation.items[0]);
    }

    [Test]
    public void Level1_AutoBasic_IsWinnable()
    {
        var eng = BuildLevel(1, 7);
        eng.StartBattle();

        for (int i = 0; i < 60 && !eng.IsFinished; i++) BattleTestDriver.CompleteRound(eng);

        Assert.IsTrue(eng.IsFinished, "第 1 关应能在 60 回合内结束");
        Assert.AreEqual(Team.Player, eng.Winner, "第 1 关纯普攻应可胜");
    }

    [Test]
    public void Level1_TurnOrder_IsMixed()
    {
        var eng = BuildLevel(1, 7);
        eng.StartBattle();

        var order = BattleTestDriver.CompleteRound(eng);

        int firstP = -1, lastP = -1, firstE = -1, lastE = -1;
        for (int i = 0; i < order.Count; i++)
        {
            if (order[i].Team == Team.Player) { if (firstP < 0) firstP = i; lastP = i; }
            else { if (firstE < 0) firstE = i; lastE = i; }
        }

        bool purePlayerFirst = lastP < firstE;   // 全部我方先
        bool pureEnemyFirst = lastE < firstP;    // 全部敌方先
        Assert.IsFalse(purePlayerFirst || pureEnemyFirst, "第 1 关出手顺序应敌我混排（教速度排序）");
    }

    [Test]
    public void Lord_Has_Interruptible_Charge()
    {
        var enemies = ConfigLoader.LoadEnemies();
        Assert.IsEmpty(enemies.errors);

        var lord = enemies.items.First(e => e.id == "lord");
        var charge = lord.intents.First(i => i.type == "charge");
        Assert.IsTrue(charge.interruptible, "领主蓄力必须可被打断（第 2 关教学点）");
    }

    [Test]
    public void CurrentOrder_Available_DuringInput()
    {
        var eng = BuildLevel(1, 7);
        eng.StartBattle();

        Assert.AreEqual(BattleFlow.Input, eng.Flow, "开局应进入输入阶段");
        Assert.AreEqual(0, eng.TurnOrder.Count, "结算前 TurnOrder 应空（BUG-A 旧读法）");
        Assert.Greater(eng.CurrentOrder.Count, 0, "输入阶段行动条应读 CurrentOrder 得本回合顺序");
    }

    [Test]
    public void All_Wave_Enemy_Ids_Resolve()
    {
        var enemies = ConfigLoader.LoadEnemies();
        var levels = ConfigLoader.LoadLevels();
        Assert.IsEmpty(enemies.errors);
        Assert.IsEmpty(levels.errors);

        var ids = enemies.items.Select(e => e.id).ToHashSet();
        foreach (var level in levels.items)
            foreach (var wave in level.waves)
            {
                Assert.IsNotNull(wave.slots, level.name + " 波次缺 slots");
                Assert.AreEqual(wave.enemies.Length, wave.slots.Length, level.name + " 敌人与槽位须一一对应");
                Assert.LessOrEqual(wave.enemies.Length, Formation.Capacity);
                Assert.AreEqual(wave.slots.Length, wave.slots.Distinct().Count(), level.name + " 波次槽位重复");
                foreach (var slot in wave.slots)
                    Assert.IsTrue(Formation.IsValidSlot(slot), level.name + " 槽位非法: " + slot);
                foreach (var enemyId in wave.enemies)
                    Assert.IsTrue(ids.Contains(enemyId), level.name + " 引用了未定义的敌人 " + enemyId);
            }
    }

    // Strategy tie-breaks are stable IDs; never consume engine.Rng to decide commands.
    private static void PlayLegalHand(BattleEngine eng)
    {
        int budget = eng.Hand.Count;
        for (int index = 0; index < eng.Hand.Count && budget-- > 0;)
        {
            var card = eng.GetCard(eng.Hand[index]);
            var players = eng.Players.Where(u => !u.IsDead).ToArray();
            var enemies = eng.Enemies.Where(u => !u.IsDead).ToArray();
            bool played = false;
            if (eng.CanPlayRemovalCard(index))
            {
                Assert.IsTrue(eng.PlayRemovalCard(index));
                played = true;
            }
            else if (eng.CanPlayGroupCard(index))
            {
                Assert.IsTrue(eng.PlayGroupCard(index));
                played = true;
            }
            else if (card.effect.type == "tauntUp" || card.effect.type == "tauntDown")
            {
                // Concentrate attacks on the healthy tank; protect the most fragile survivor.
                var target = card.effect.type == "tauntUp"
                    ? players.OrderByDescending(u => u.Hp).ThenBy(u => u.Id, System.StringComparer.Ordinal).FirstOrDefault()
                    : players.OrderBy(u => u.Hp).ThenBy(u => u.Id, System.StringComparer.Ordinal).FirstOrDefault();
                if (target != null && eng.CanPlaySlotCard(index, Team.Player, target.SlotId))
                {
                    Assert.IsTrue(eng.PlaySlotCard(index, Team.Player, target.SlotId));
                    played = true;
                }
            }
            else
            {
                System.Collections.Generic.IEnumerable<Unit> targets;
                switch (card.effect.type)
                {
                    case "damage":
                    case "vulnerable":
                    case "interrupt":
                    case "weaken":
                    case "slow":
                        targets = enemies.OrderByDescending(u => eng.CurrentIntentOf(u.Id)?.type == "charge")
                            .ThenByDescending(u => eng.CurrentIntentOf(u.Id)?.nextDamage ?? 0)
                            .ThenByDescending(u => eng.CurrentIntentOf(u.Id)?.damage ?? 0)
                            .ThenByDescending(u => u.BaseSpeed).ThenBy(u => u.Id, System.StringComparer.Ordinal);
                        break;
                    case "heal":
                        targets = players.OrderBy(u => (double)u.Hp / u.MaxHp)
                            .ThenBy(u => u.Id, System.StringComparer.Ordinal);
                        break;
                    case "shield":
                        targets = players.OrderByDescending(u => eng.Ctx.EffectiveTaunt(u))
                            .ThenBy(u => u.Hp).ThenBy(u => u.Id, System.StringComparer.Ordinal);
                        break;
                    default:
                        targets = players.OrderByDescending(u => eng.SkillsOf(u.Id)?.FirstOrDefault(s => s.type == "basic")?.damage?.amount ?? 0)
                            .ThenBy(u => u.Id, System.StringComparer.Ordinal);
                        break;
                }
                var target = targets.FirstOrDefault(u => eng.CanPlayCard(index, u.Id));
                if (target != null)
                {
                    Assert.IsTrue(eng.PlayCard(index, target.Id));
                    played = true;
                }
            }
            if (!played) index++;
        }
    }

    private static void RequestReadyUltimates(BattleEngine eng)
    {
        foreach (var player in eng.Players.OrderBy(u => u.Id, System.StringComparer.Ordinal))
        {
            if (!eng.CanRequestUltimate(player.Id)) continue;
            Assert.IsTrue(eng.RequestUltimate(player.Id), "合法就绪终结技应允许请求: " + player.Id);
        }
    }

    private static string RemainingHp(BattleEngine eng) =>
        string.Join(",", eng.Ctx.Units.Select(u => u.Id + "=" + u.Hp + "/" + u.MaxHp));

    [TestCase(1, 7)]
    [TestCase(1, 17)]
    [TestCase(1, 37)]
    [TestCase(1, 73)]
    [TestCase(1, 101)]
    [TestCase(2, 7)]
    [TestCase(2, 17)]
    [TestCase(2, 37)]
    [TestCase(2, 73)]
    [TestCase(2, 101)]
    [TestCase(3, 7)]
    [TestCase(3, 17)]
    [TestCase(3, 37)]
    [TestCase(3, 73)]
    [TestCase(3, 101)]
    public void All_Levels_Legal_Cards_And_Requested_Ultimates_Are_Winnable(int level, int seed)
    {
        var eng = BuildLevel(level, seed);
        eng.StartBattle();
        const int maxSteps = 10000;
        const int maxRounds = 200;
        int lastLoggedRound = -1;
        int step = 0;
        for (; step < maxSteps && !eng.IsFinished && eng.Round < maxRounds; step++)
        {
            if (eng.CanPlayCards)
            {
                if (lastLoggedRound != eng.Round)
                {
                    TestContext.WriteLine($"level={level} seed={seed} round={eng.Round} HP: {RemainingHp(eng)}");
                    lastLoggedRound = eng.Round;
                }
                PlayLegalHand(eng);
            }
            RequestReadyUltimates(eng);
            if (eng.CanPlayCards) Assert.IsTrue(eng.BeginActions());
            var result = eng.CurrentAction ?? eng.Advance();
            if (result == null) continue;
            // Check after the entire action (including multi-hit charging), before Confirm.
            // This permits newly-ready ultimates at every action boundary, not only each round.
            RequestReadyUltimates(eng);
            Assert.IsTrue(eng.Confirm(eng.BattleId, result.Sequence));
        }
        int injected = eng.ActionResults.Sum(action => action.InjectedCardIds.Count);
        int usedSkills = eng.ActionResults.Count(action => action.Kind == BattleEventKind.Card &&
            (action.ActionId == "gale_slash" || action.ActionId == "heal" || action.ActionId == "lightning"));
        TestContext.WriteLine($"injected={injected} skillCardsUsed={usedSkills} slagRemoved={eng.Deck.RemovedCount}");
        Assert.AreEqual(5 + injected, eng.Hand.Count + eng.Deck.DrawPileCount + eng.Deck.DiscardPileCount + eng.Deck.RemovedCount);
        if (level == 3) Assert.Greater(usedSkills, 0, "最终关至少兑现一次生成技能牌");
        string summary = $"level={level} seed={seed} round={eng.Round} steps={step} winner={eng.Winner} HP: {RemainingHp(eng)}";
        TestContext.WriteLine(summary);
        Assert.IsTrue(eng.IsFinished, "模拟必须在步数/回合上限内结束: " + summary);
        Assert.IsNull(eng.CurrentAction, "最后一个致胜动作也必须完成确认");
        Assert.AreEqual(Team.Player, eng.Winner, "合法卡牌与主动终结技策略应可胜: " + summary);
        Assert.Greater(eng.Players.Sum(u => u.Hp), 0, summary);
        Assert.AreEqual(0, eng.Enemies.Sum(u => u.Hp), summary);
    }

    [Test]
    public void All_Levels_Terminate_Under_AutoBasic()
    {
        for (int id = 1; id <= 3; id++)
        {
            var eng = BuildLevel(id, 7);
            eng.StartBattle();

            int guard = 0;
            while (!eng.IsFinished && guard < 200) { BattleTestDriver.CompleteRound(eng); guard++; }

            Assert.IsTrue(eng.IsFinished, "第 " + id + " 关应在 200 回合内结束（无死循环）");
        }
    }
}