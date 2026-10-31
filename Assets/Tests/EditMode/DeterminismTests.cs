using System.Linq;
using System.Text;
using CQ.Core.Battle;
using CQ.Core.Combat;
using NUnit.Framework;

public class DeterminismTests
{
    private static BattleEngine Build(long seed)
    {
        var hero = AutoBattleTestData.Hero("hero", "front1");
        AutoBattleTestData.SetSkill(hero, AutoBattleTestData.Skill("special",
            AutoBattleTestData.Effect(target: "enemyRandom", count: 2, hits: 2)));
        AutoBattleTestData.SetSkill(hero, AutoBattleTestData.Skill("passive",
            AutoBattleTestData.Effect("energy", "allyLowestEnergy", multiplier: 0, hits: 0, amount: 10)));
        return AutoBattleTestData.Engine(seed, new[] { hero, AutoBattleTestData.Hero("support", "back1") },
            new[] { AutoBattleTestData.Enemy("a", "front1"), AutoBattleTestData.Enemy("b", "front2"),
                AutoBattleTestData.Enemy("c", "back1") });
    }

    private static string Trace(BattleEngine engine, bool extraReads)
    {
        var text = new StringBuilder();
        engine.StartBattle();
        for (int i = 0; i < 60; i++)
        {
            if (extraReads)
                for (int repeat = 0; repeat < 4; repeat++)
                {
                    var order = engine.CurrentOrder.ToArray();
                    foreach (var unit in engine.Ctx.Units)
                    {
                        engine.Ctx.GetStatuses(unit.Id).ToArray();
                        engine.Ctx.StatesOf(unit.Id).EffectiveAttack(unit);
                        engine.Ctx.StatesOf(unit.Id).EffectiveDefense(unit);
                        engine.SkillsOf(unit.Id).ToArray();
                    }
                }
            var result = engine.Advance();
            Assert.IsNotNull(result);
            text.Append(engine.CurrentRound).Append(':').Append(result.Sequence).Append(':')
                .Append(result.ActorId).Append(':').Append(result.ActionType).Append('|');
            foreach (var hit in result.Hits)
                text.Append(hit.TargetUnitId).Append('/').Append(hit.HpDamage).Append(';');
            foreach (var unit in engine.Ctx.Units)
            {
                text.Append(unit.Id).Append('/').Append(unit.Hp).Append('/').Append(unit.Ult).Append('/');
                foreach (var fx in engine.Ctx.GetStatuses(unit.Id))
                    text.Append(fx.Type).Append(',').Append(fx.Percent).Append(',').Append(fx.Duration).Append(';');
            }
            text.AppendLine();
            Assert.IsTrue(engine.Confirm(engine.BattleId, result.Sequence));
        }
        return text.ToString();
    }

    [TestCase(7)]
    [TestCase(17)]
    [TestCase(73)]
    [TestCase(12345)]
    public void SameSeedAndActionBoundariesProduceSameTrace_DespiteExtraPresentationReads(long seed)
    {
        Assert.AreEqual(Trace(Build(seed), false), Trace(Build(seed), true));
    }

    [Test]
    public void DifferentSeedsActuallyChangeRandomTargetTrace()
    {
        Assert.AreNotEqual(Trace(Build(7), false), Trace(Build(73), false));
    }
}
