using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Combat;
using NUnit.Framework;

public class RoundLimitTests
{
    private static BattleEngine Stalemate()
    {
        var hero = AutoBattleTestData.Hero(defense: 1000);
        var enemy = AutoBattleTestData.Enemy(defense: 1000);
        foreach (var config in new CQ.Core.Config.CharacterConfig[] { hero, enemy })
        {
            config.skills[3].trigger = "permanent";
            config.skills[3].effects[0].duration = 0;
        }
        return AutoBattleTestData.Engine(heroes: new[] { hero }, enemies: new[] { enemy });
    }

    [Test]
    public void TwentiethRoundPlaysEveryAction_ThenTimesOutWithoutRoundTwentyOne()
    {
        var engine = Stalemate();
        Assert.IsTrue(engine.StartBattle());
        for (int i = 0; i < (TurnManager.RoundLimit - 1) * 2; i++) BattleTestDriver.Step(engine);
        Assert.AreEqual(19, engine.Round);
        var first = engine.Advance();
        Assert.IsNotNull(first);
        Assert.AreEqual(20, engine.Round);
        Assert.IsFalse(engine.IsFinished, "不能在第20回合开始时判负");
        Assert.IsTrue(engine.Confirm(engine.BattleId, first.Sequence));
        var last = engine.Advance();
        Assert.IsNotNull(last);
        Assert.AreNotEqual(first.ActorId, last.ActorId);
        Assert.IsFalse(engine.IsFinished);
        Assert.IsNull(engine.Advance(), "最后动作的播放确认前不能判超时");
        Assert.IsTrue(engine.IsActionOccupied);
        Assert.IsTrue(engine.Confirm(engine.BattleId, last.Sequence));
        Assert.IsNull(engine.Advance());
        Assert.IsTrue(engine.IsFinished);
        Assert.IsTrue(engine.RoundLimitReached);
        Assert.AreEqual(Team.Enemy, engine.Winner);
        Assert.AreEqual(BattleFlow.Finished, engine.Flow);
        Assert.AreEqual(20, engine.Round);
        Assert.IsFalse(engine.IsActionOccupied);
        Assert.IsTrue(engine.Ctx.Units.All(unit => !unit.IsDead), "超时不能伪造死亡或扣血");
        int count = engine.ActionResults.Count;
        for (int i = 0; i < 3; i++) Assert.IsNull(engine.Advance());
        Assert.AreEqual(count, engine.ActionResults.Count);
        Assert.AreEqual(20, engine.Round);
        Assert.IsFalse(engine.StartBattle());
        var retry = Stalemate();
        Assert.AreEqual(0, retry.Round);
        Assert.IsFalse(retry.RoundLimitReached);
        Assert.IsTrue(retry.StartBattle());
    }

    [TestCase(false)]
    [TestCase(true)]
    public void KillInLastRoundKeepsNormalWinner_NotTimeout(bool enemyWins)
    {
        var engine = Stalemate();
        engine.StartBattle();
        for (int i = 0; i < (TurnManager.RoundLimit - 1) * 2; i++) BattleTestDriver.Step(engine);
        if (enemyWins)
        {
            BattleTestDriver.Step(engine);
            engine.Players[0].Hp = 1;
        }
        else engine.Enemies[0].Hp = 1;
        var hit = engine.Advance();
        Assert.IsNotNull(hit);
        Assert.AreEqual(20, engine.Round);
        Assert.IsTrue(engine.IsFinished);
        Assert.AreEqual(enemyWins ? Team.Enemy : Team.Player, engine.Winner);
        Assert.IsFalse(engine.RoundLimitReached);
        Assert.IsTrue(engine.IsActionOccupied, "正常胜负仍应等待末次演出确认");
        Assert.IsTrue(engine.Confirm(engine.BattleId, hit.Sequence));
        Assert.IsNull(engine.Advance());
    }

    [Test]
    public void PassiveAndStunActionsDoNotConsumeExtraRounds()
    {
        var engine = AutoBattleTestData.Engine();
        engine.StartBattle();
        engine.Ctx.StatesOf("hero").Apply(StatusType.Stun, 0, 100);
        BattleTestDriver.Drain(engine);
        Assert.IsTrue(engine.RoundLimitReached);
        Assert.AreEqual(20, engine.Round);
        Assert.AreEqual(40, engine.ActionResults.Count(action => action.Kind == BattleEventKind.Passive));
        Assert.AreEqual(20, engine.ActionResults.Count(action => action.Kind == BattleEventKind.Skipped));
        Assert.AreEqual(Team.Enemy, engine.Winner);
    }
}
