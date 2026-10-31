using System;
using System.Linq;
using CQ.Core.Combat;
using CQ.Core.Config;
using NUnit.Framework;

public class FormationTests
{
    [Test]
    public void EachTeamHasFiveOrderedSlots_AndTeamLocalOccupancy()
    {
        var engine = AutoBattleTestData.Engine();
        foreach (var team in new[] { Team.Player, Team.Enemy })
        {
            var formation = engine.Ctx.FormationOf(team);
            CollectionAssert.AreEqual(new[] { "front1", "front2", "back1", "back2", "back3" },
                formation.Slots.Select(s => s.Id));
            Assert.AreEqual(team, formation.Team);
            Assert.AreSame(team == Team.Player ? engine.Players[0] : engine.Enemies[0], formation.GetSlot("front1").Occupant);
            Assert.IsNull(formation.GetSlot("back3").Occupant);
            Assert.IsNull(formation.GetSlot("invalid"));
        }
    }

    [TestCase("front1", "front")]
    [TestCase("front2", "front")]
    [TestCase("back1", "back")]
    [TestCase("back2", "back")]
    [TestCase("back3", "back")]
    public void RowIsDerivedFromSlot(string slot, string row)
    {
        Assert.AreEqual(row, new Unit("unit", Team.Player) { SlotId = slot }.Row);
    }

    [Test]
    public void MovingToOccupiedSlotSwapsBothUnitAndSlotReferences()
    {
        var engine = AutoBattleTestData.Engine(heroes: new[]
        {
            AutoBattleTestData.Hero("a", "front1"), AutoBattleTestData.Hero("b", "back3")
        });
        var formation = engine.Ctx.FormationOf(Team.Player);
        var a = engine.Players[0];
        var b = engine.Players[1];
        Assert.IsTrue(formation.TryMove(a, "back3"));
        Assert.AreEqual("back3", a.SlotId);
        Assert.AreEqual("front1", b.SlotId);
        Assert.AreSame(a, formation.GetSlot("back3").Occupant);
        Assert.AreSame(b, formation.GetSlot("front1").Occupant);
        Assert.IsTrue(formation.TryMove(a, "back1"));
        Assert.IsNull(formation.GetSlot("back3").Occupant);
        Assert.AreSame(a, formation.GetSlot("back1").Occupant);
    }

    [Test]
    public void InvalidMoveDoesNotMutatePlacement()
    {
        var engine = AutoBattleTestData.Engine();
        var formation = engine.Ctx.FormationOf(Team.Player);
        Assert.IsFalse(formation.TryMove(engine.Enemies[0], "back1"));
        Assert.IsFalse(formation.TryMove(engine.Players[0], "missing"));
        Assert.IsFalse(formation.TryMove(null, "back1"));
        Assert.AreSame(engine.Players[0], formation.GetSlot("front1").Occupant);
    }

    [Test]
    public void DeadUnitStaysInOriginalSlot_WithoutAdvancingBackRow()
    {
        var engine = AutoBattleTestData.Engine(enemies: new[]
        {
            AutoBattleTestData.Enemy("front", "front1"), AutoBattleTestData.Enemy("back", "back1")
        });
        engine.Enemies[0].TakeDamage(engine.Enemies[0].Hp);
        Assert.AreSame(engine.Enemies[0], engine.Ctx.FormationOf(Team.Enemy).GetSlot("front1").Occupant);
        Assert.AreEqual("back1", engine.Enemies[1].SlotId);
        Assert.IsNull(engine.Ctx.FormationOf(Team.Enemy).GetSlot("front2").Occupant);
    }

    [Test]
    public void DuplicateCharacterSlotsAreRejectedAtBattleBoundary()
    {
        Assert.Throws<ArgumentException>(() => AutoBattleTestData.Engine(heroes: new[]
        {
            AutoBattleTestData.Hero("a", "front1"), AutoBattleTestData.Hero("b", "front1")
        }));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("front3")]
    [TestCase("back4")]
    public void InvalidDefaultSlotIsRejected(string slot)
    {
        Assert.IsNotNull(ConfigValidator.ValidateCharacter(AutoBattleTestData.Hero(slot: slot)));
    }
}
