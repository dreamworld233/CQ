using System;
using CQ.Core.Battle;
using CQ.Core.Combat;
using NUnit.Framework;

public class PersonalActionCycleTests
{
    private static Unit Unit() => new Unit("unit", Team.Player) { Hp = 100, MaxHp = 100 };
    private static PersonalActionCycle Cycle(Unit unit) => new PersonalActionCycle(unit, 20, 100, 40, 10, 2);

    [Test]
    public void FirstThreeActions_ThenSlowerUltimates_WithIndependentSpecialTimer()
    {
        var unit = Unit();
        var cycle = Cycle(unit);
        var expected = new[] { PersonalActionKind.Basic, PersonalActionKind.Special,
            PersonalActionKind.Ultimate, PersonalActionKind.Special, PersonalActionKind.Basic,
            PersonalActionKind.Special, PersonalActionKind.Ultimate, PersonalActionKind.Special };
        var energy = new[] { 60, 100, 0, 40, 80, 100, 0, 40 };
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.AreEqual(expected[i], cycle.Begin(false), "action " + (i + 1));
            cycle.Complete();
            Assert.AreEqual(energy[i], unit.Ult);
        }
        Assert.AreEqual(8, cycle.Opportunities);
    }

    [Test]
    public void RepeatedUltimates_DeferSpecialUntilItActuallyExecutes()
    {
        var unit = Unit();
        var cycle = Cycle(unit);
        cycle.Begin(false);
        cycle.Complete();
        for (int i = 0; i < 3; i++)
        {
            unit.GainUlt(100);
            Assert.AreEqual(PersonalActionKind.Ultimate, cycle.Begin(false));
            cycle.Complete();
        }
        Assert.AreEqual(PersonalActionKind.Special, cycle.Begin(false));
        cycle.Complete();
        Assert.AreEqual(PersonalActionKind.Basic, cycle.Begin(false));
        cycle.Complete();
        Assert.AreEqual(PersonalActionKind.Special, cycle.Begin(false));
    }

    [Test]
    public void ActionEnergy_IsOnlyGrantedAtCompletion()
    {
        var unit = Unit();
        var cycle = Cycle(unit);
        cycle.Begin(false);
        Assert.AreEqual(20, unit.Ult);
        cycle.Complete();
        Assert.AreEqual(60, unit.Ult);
    }

    [Test]
    public void StunnedUltimate_RetainsEnergy_AndDoesNotGainActionEnergy()
    {
        var unit = Unit();
        var cycle = Cycle(unit);
        unit.GainUlt(80);
        Assert.AreEqual(PersonalActionKind.Skipped, cycle.Begin(true));
        cycle.Complete();
        Assert.AreEqual(100, unit.Ult);
        Assert.AreEqual(PersonalActionKind.Ultimate, cycle.Begin(false));
        Assert.AreEqual(0, unit.Ult);
        cycle.Complete();
        Assert.AreEqual(0, unit.Ult);
    }

    [Test]
    public void Stun_AdvancesSpecialTimer_ButDoesNotConsumeSpecial()
    {
        var unit = Unit();
        var cycle = Cycle(unit);
        cycle.Begin(true);
        cycle.Complete();
        Assert.IsTrue(cycle.IsSpecialDue);
        cycle.Begin(true);
        cycle.Complete();
        Assert.AreEqual(20, unit.Ult);
        Assert.AreEqual(PersonalActionKind.Special, cycle.Begin(false));
        cycle.Complete();
        Assert.AreEqual(PersonalActionKind.Basic, cycle.Begin(false));
    }

    [Test]
    public void FullEnergyDuringAnAction_DoesNotReplaceThatAction()
    {
        var unit = Unit();
        var cycle = Cycle(unit);
        var action = cycle.Begin(false);
        for (int i = 0; i < 20; i++) cycle.ReceiveAttack();
        Assert.AreEqual(PersonalActionKind.Basic, action);
        Assert.IsTrue(cycle.IsOccupied);
        cycle.Complete();
        Assert.AreEqual(100, unit.Ult);
        Assert.AreEqual(PersonalActionKind.Ultimate, cycle.Begin(false));
        cycle.Complete();
        Assert.AreEqual(PersonalActionKind.Special, cycle.Begin(false));
    }

    [Test]
    public void HitEnergy_CanChargeDuringUltimate_WithoutSelfCharge()
    {
        var unit = Unit();
        var cycle = Cycle(unit);
        unit.GainUlt(80);
        cycle.Begin(false);
        cycle.ReceiveAttack();
        cycle.Complete();
        Assert.AreEqual(10, unit.Ult);
    }

    [Test]
    public void Death_DoesNotGrantCompletionOrHitEnergy()
    {
        var unit = Unit();
        var cycle = Cycle(unit);
        cycle.Begin(false);
        unit.TakeDamage(100);
        cycle.ReceiveAttack();
        cycle.Complete();
        Assert.AreEqual(20, unit.Ult);
        Assert.Throws<InvalidOperationException>(() => cycle.Begin(false));
    }

    [Test]
    public void Boundaries_RejectDuplicateBeginAndComplete_WithoutExtraCharge()
    {
        var unit = Unit();
        var cycle = Cycle(unit);
        Assert.Throws<InvalidOperationException>(() => cycle.Complete());
        cycle.Begin(false);
        Assert.Throws<InvalidOperationException>(() => cycle.Begin(false));
        Assert.AreEqual(1, cycle.Opportunities);
        cycle.Complete();
        Assert.Throws<InvalidOperationException>(() => cycle.Complete());
        Assert.AreEqual(60, unit.Ult);
    }

    [TestCase(-1, 100, 40, 10, 2)]
    [TestCase(101, 100, 40, 10, 2)]
    [TestCase(0, 0, 40, 10, 2)]
    [TestCase(20, 100, -1, 10, 2)]
    [TestCase(20, 100, 40, -1, 2)]
    [TestCase(20, 100, 40, 10, 0)]
    public void InvalidRules_AreRejectedBeforeMutatingUnit(int initial, int max, int action, int hit, int interval)
    {
        var unit = Unit();
        unit.Ult = 7;
        Assert.Throws<ArgumentOutOfRangeException>(() => new PersonalActionCycle(unit, initial, max, action, hit, interval));
        Assert.AreEqual(7, unit.Ult);
    }

    [Test]
    public void NewBattle_CreatesFreshEnergyAndCounters()
    {
        var previous = Cycle(Unit());
        previous.Begin(false);
        previous.Complete();
        var unit = Unit();
        var fresh = Cycle(unit);
        Assert.AreEqual(20, unit.Ult);
        Assert.AreEqual(0, fresh.Opportunities);
        Assert.IsFalse(fresh.IsOccupied);
        Assert.AreEqual(PersonalActionKind.Basic, fresh.Begin(false));
    }
}
