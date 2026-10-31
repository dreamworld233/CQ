using System;
using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Combat;
using NUnit.Framework;

public class ActionStatusesTests
{
    private static void Tick(ActionStatuses statuses)
    {
        statuses.BeginAction();
        statuses.CompleteAction();
    }

    [TestCase(StatusType.AttackUp)]
    [TestCase(StatusType.DefenseUp)]
    public void PermanentBuff_SurvivesActions_AndResumesAfterStrongerTemporaryBuff(StatusType type)
    {
        var statuses = new ActionStatuses();
        statuses.ApplyPermanent(type, 10);
        statuses.ApplyPermanent(type, 10);
        Assert.AreEqual(1, statuses.Snapshot().Count);
        statuses.Apply(type, 25, 2);
        Assert.AreEqual(25, statuses.StrongestPercent(type));
        Tick(statuses);
        Tick(statuses);
        for (int i = 0; i < 100; i++) Tick(statuses);
        Assert.AreEqual(10, statuses.StrongestPercent(type));
        Assert.AreEqual("permanent", statuses.Snapshot().Single().Turn);
        statuses.Apply(type, 10, 1);
        Tick(statuses);
        Assert.AreEqual("permanent", statuses.Snapshot().Single().Turn);
    }

    [Test]
    public void EqualStrength_RefreshesToMaximumDuration_WithoutStacking()
    {
        var statuses = new ActionStatuses();
        statuses.Apply(StatusType.AttackUp, 25, 2);
        statuses.Apply(StatusType.AttackUp, 25, 1);
        Assert.AreEqual(2, statuses.Snapshot().Single().Duration);
        Tick(statuses);
        statuses.Apply(StatusType.AttackUp, 25, 2);
        Assert.AreEqual(2, statuses.Snapshot().Single().Duration);
        Assert.AreEqual(25, statuses.StrongestPercent(StatusType.AttackUp));
        Tick(statuses);
        Tick(statuses);
        Assert.IsFalse(statuses.Has(StatusType.AttackUp));
    }

    [Test]
    public void DifferentStrengths_TickIndependently_AndWeakerResumes()
    {
        var statuses = new ActionStatuses();
        statuses.Apply(StatusType.AttackUp, 25, 1);
        statuses.Apply(StatusType.AttackUp, 15, 2);
        Assert.AreEqual(25, statuses.StrongestPercent(StatusType.AttackUp));
        Tick(statuses);
        Assert.AreEqual(15, statuses.StrongestPercent(StatusType.AttackUp));
        Assert.AreEqual(1, statuses.Snapshot().Single().Duration);
        Tick(statuses);
        Assert.IsFalse(statuses.Has(StatusType.AttackUp));
    }

    [Test]
    public void ReapplyingWeakerBuff_DoesNotExtendStrongerBuff()
    {
        var statuses = new ActionStatuses();
        statuses.Apply(StatusType.AttackUp, 25, 1);
        statuses.Apply(StatusType.AttackUp, 15, 2);
        statuses.Apply(StatusType.AttackUp, 15, 5);
        Tick(statuses);
        Assert.AreEqual(15, statuses.StrongestPercent(StatusType.AttackUp));
        Assert.AreEqual(4, statuses.Snapshot().Single().Duration);
    }

    [Test]
    public void BuffAppliedDuringOwnAction_StartsCountingNextAction()
    {
        var statuses = new ActionStatuses();
        statuses.BeginAction();
        statuses.Apply(StatusType.DefenseUp, 25, 2);
        statuses.CompleteAction();
        Assert.AreEqual(2, statuses.Snapshot().Single().Duration);
        Tick(statuses);
        Tick(statuses);
        Assert.IsFalse(statuses.Has(StatusType.DefenseUp));
    }

    [Test]
    public void BuffRefreshedDuringOwnAction_DoesNotLoseNewDuration()
    {
        var statuses = new ActionStatuses();
        statuses.Apply(StatusType.AttackUp, 25, 1);
        statuses.BeginAction();
        statuses.Apply(StatusType.AttackUp, 25, 2);
        statuses.CompleteAction();
        Assert.AreEqual(2, statuses.Snapshot().Single().Duration);
    }

    [Test]
    public void EqualDurationRefreshDuringOwnAction_PreservesNewDuration()
    {
        var statuses = new ActionStatuses();
        statuses.Apply(StatusType.AttackUp, 25, 2);
        statuses.BeginAction();
        statuses.Apply(StatusType.AttackUp, 25, 2);
        statuses.CompleteAction();
        Assert.AreEqual(2, statuses.Snapshot().Single().Duration);
    }

    [Test]
    public void ShorterRefreshDuringOwnAction_DoesNotFreezeExistingDuration()
    {
        var statuses = new ActionStatuses();
        statuses.Apply(StatusType.AttackUp, 25, 3);
        statuses.BeginAction();
        statuses.Apply(StatusType.AttackUp, 25, 1);
        statuses.CompleteAction();
        Assert.AreEqual(2, statuses.Snapshot().Single().Duration);
    }

    [Test]
    public void AnotherUnitsAction_DoesNotConsumeTargetsBuff()
    {
        var target = new ActionStatuses();
        var other = new ActionStatuses();
        other.BeginAction();
        target.Apply(StatusType.DefenseUp, 25, 2);
        other.CompleteAction();
        Assert.AreEqual(2, target.Snapshot().Single().Duration);
        Tick(other);
        Assert.AreEqual(2, target.Snapshot().Single().Duration);
        Tick(target);
        Assert.AreEqual(1, target.Snapshot().Single().Duration);
    }

    [Test]
    public void StunnedAction_ConsumesBuffsButPreservesUltimate()
    {
        var unit = new Unit("unit", Team.Player) { Hp = 100, MaxHp = 100 };
        var cycle = new PersonalActionCycle(unit, 100, 100, 40, 10, 2);
        var statuses = new ActionStatuses();
        statuses.Apply(StatusType.AttackUp, 25, 1);
        statuses.Apply(StatusType.Stun, 0, 1);
        statuses.BeginAction();
        Assert.AreEqual(PersonalActionKind.Skipped, cycle.Begin(statuses.Has(StatusType.Stun)));
        cycle.Complete();
        statuses.CompleteAction();
        Assert.IsEmpty(statuses.Snapshot());
        Assert.AreEqual(100, unit.Ult);
        Assert.AreEqual(PersonalActionKind.Ultimate, cycle.Begin(statuses.Has(StatusType.Stun)));
    }

    [Test]
    public void Attributes_RecomputeFromBase_WithoutDriftOrPrematureRounding()
    {
        var unit = new Unit("unit", Team.Player) { BaseAttack = 101, BaseDefense = 51 };
        var statuses = new ActionStatuses();
        statuses.Apply(StatusType.AttackUp, 25, 2);
        statuses.Apply(StatusType.DefenseUp, 25, 2);
        for (int i = 0; i < 100; i++)
        {
            Assert.AreEqual(126.25m, statuses.EffectiveAttack(unit));
            Assert.AreEqual(63.75m, statuses.EffectiveDefense(unit));
        }
        Assert.AreEqual(101, unit.BaseAttack);
        Assert.AreEqual(51, unit.BaseDefense);
        Tick(statuses);
        Tick(statuses);
        Assert.AreEqual(101m, statuses.EffectiveAttack(unit));
        Assert.AreEqual(51m, statuses.EffectiveDefense(unit));
    }

    [TestCase(100, 25, 0, 200, 50, 200)]
    [TestCase(100, 0, 25, 100, 80, 1)]
    [TestCase(1, 0, 0, 100, 100, 1)]
    [TestCase(100, 0, 0, 100, 100, 1)]
    [TestCase(101, 25, 25, 200, 51, 189)]
    [TestCase(101, 25, 0, 400, 0, 505)]
    public void Damage_UsesEffectiveAttributes_AndFloorsOnlyAtDamageBoundary(
        int attack, int attackUp, int defenseUp, int multiplier, int defense, int expected)
    {
        var source = new Unit("source", Team.Player) { BaseAttack = attack };
        var target = new Unit("target", Team.Enemy) { BaseDefense = defense };
        var sourceStatuses = new ActionStatuses();
        var targetStatuses = new ActionStatuses();
        if (attackUp > 0) sourceStatuses.Apply(StatusType.AttackUp, attackUp, 2);
        if (defenseUp > 0) targetStatuses.Apply(StatusType.DefenseUp, defenseUp, 2);
        Assert.AreEqual(expected, ActionStatuses.CalculateDamage(source, sourceStatuses, target, targetStatuses, multiplier));
    }

    [Test]
    public void MaximumValidDamageInputsClampBeforeIntegerConversion()
    {
        var attacker = new Unit("source", Team.Player) { BaseAttack = 1000000 };
        var defender = new Unit("target", Team.Enemy) { BaseDefense = 1000000 };
        var attackStatuses = new ActionStatuses();
        var defenseStatuses = new ActionStatuses();
        attackStatuses.Apply(StatusType.AttackUp, 10000, 2);
        defenseStatuses.Apply(StatusType.DefenseUp, 10000, 2);
        Assert.AreEqual(int.MaxValue, ActionStatuses.CalculateDamage(attacker, attackStatuses,
            defender, defenseStatuses, 10000));
        Assert.AreEqual(1000000, attacker.BaseAttack);
        Assert.AreEqual(1000000, defender.BaseDefense);
    }

    [Test]
    public void Snapshot_IsDetachedFromInternalState()
    {
        var statuses = new ActionStatuses();
        statuses.Apply(StatusType.AttackUp, 25, 2);
        var snapshot = statuses.Snapshot();
        snapshot[0].Percent = 999;
        snapshot[0].Duration = 999;
        Assert.AreEqual(25, statuses.StrongestPercent(StatusType.AttackUp));
        Assert.AreEqual(2, statuses.Snapshot().Single().Duration);
    }

    [Test]
    public void Boundaries_RejectUnmatchedOrNestedActions()
    {
        var statuses = new ActionStatuses();
        Assert.Throws<InvalidOperationException>(() => statuses.CompleteAction());
        statuses.BeginAction();
        Assert.Throws<InvalidOperationException>(() => statuses.BeginAction());
        statuses.CompleteAction();
        Assert.Throws<InvalidOperationException>(() => statuses.CompleteAction());
    }

    [TestCase(-1, 2)]
    [TestCase(25, 0)]
    [TestCase(25, -1)]
    public void InvalidStatus_IsRejected(int percent, int duration)
    {
        var statuses = new ActionStatuses();
        Assert.Throws<ArgumentOutOfRangeException>(() => statuses.Apply(StatusType.AttackUp, percent, duration));
        Assert.IsEmpty(statuses.Snapshot());
    }
}
