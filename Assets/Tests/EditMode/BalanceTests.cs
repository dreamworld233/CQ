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

        Assert.IsEmpty(characters.errors);
        Assert.IsEmpty(enemies.errors);
        Assert.IsEmpty(cards.errors);
        Assert.IsEmpty(levels.errors);

        var catalog = enemies.items.ToDictionary(e => e.id);
        var level = levels.items.First(l => l.id == id);
        return BattleEngine.Create(seed, characters.items, level.waves[0].enemies, catalog, cards.items);
    }

    [Test]
    public void Level1_AutoBasic_IsWinnable()
    {
        var eng = BuildLevel(1, 7);
        eng.StartBattle();

        for (int i = 0; i < 60 && !eng.IsFinished; i++) eng.EndRound();

        Assert.IsTrue(eng.IsFinished, "第 1 关应能在 60 回合内结束");
        Assert.AreEqual(Team.Player, eng.Winner, "第 1 关纯普攻应可胜");
    }

    [Test]
    public void Level1_TurnOrder_IsMixed()
    {
        var eng = BuildLevel(1, 7);
        eng.StartBattle();

        var order = eng.EndRound();

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
                foreach (var enemyId in wave.enemies)
                    Assert.IsTrue(ids.Contains(enemyId), level.name + " 引用了未定义的敌人 " + enemyId);
    }

    [Test]
    public void All_Levels_Terminate_Under_AutoBasic()
    {
        for (int id = 1; id <= 3; id++)
        {
            var eng = BuildLevel(id, 7);
            eng.StartBattle();

            int guard = 0;
            while (!eng.IsFinished && guard < 200) { eng.EndRound(); guard++; }

            Assert.IsTrue(eng.IsFinished, "第 " + id + " 关应在 200 回合内结束（无死循环）");
        }
    }
}