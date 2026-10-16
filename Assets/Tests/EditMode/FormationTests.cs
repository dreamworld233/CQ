using System;
using System.Linq;
using NUnit.Framework;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;

public class FormationTests
{
    private static Unit U(string id, Team team, string slot) =>
        new Unit(id, team) { MaxHp = 100, Hp = 100, SlotId = slot };

    private static FormationConfig Rules() => new FormationConfig { frontBaseTaunt = 100, backBaseTaunt = 50 };

    [Test]
    public void Each_Team_Has_Five_Fixed_Slots_With_Injected_BaseTaunt()
    {
        var ctx = new CombatContext(new Rng(1), new Unit[0], Rules());
        foreach (var team in new[] { Team.Player, Team.Enemy })
        {
            var formation = ctx.FormationOf(team);
            CollectionAssert.AreEqual(new[] { "front1", "front2", "back1", "back2", "back3" }, formation.Slots.Select(s => s.Id));
            foreach (var slot in formation.Slots)
            {
                Assert.IsNull(slot.Occupant);
                Assert.AreEqual(slot.Id.StartsWith("front") ? 100 : 50, slot.BaseTaunt);
                Assert.AreEqual(0, slot.EffectiveTaunt, "Empty slots never attract attacks.");
            }
            Assert.IsNull(formation.GetSlot("missing"));
        }
    }

    [Test]
    public void Omitted_Config_Defaults_To_Zero_Not_Hardcoded_Balance()
    {
        var player = U("p", Team.Player, "front1");
        var enemy = U("e", Team.Enemy, "back3");
        var ctx = new CombatContext(new Rng(1), new[] { player, enemy });
        foreach (var team in new[] { Team.Player, Team.Enemy })
            foreach (var slot in ctx.FormationOf(team).Slots)
                Assert.AreEqual(0, slot.BaseTaunt);
        Assert.AreEqual(0, ctx.EffectiveTaunt(player));
        Assert.AreEqual(0, ctx.EffectiveTaunt(enemy));
    }

    [TestCase("front1", "front")]
    [TestCase("front2", "front")]
    [TestCase("back1", "back")]
    [TestCase("back2", "back")]
    [TestCase("back3", "back")]
    public void Unit_Row_Is_Derived_From_Slot(string slot, string row)
    {
        var unit = U("p", Team.Player, slot);
        Assert.AreEqual(row, unit.Row);
    }

    [Test]
    public void Five_Per_Team_And_Same_Slot_Across_Teams_Are_Valid()
    {
        var slots = new[] { "front1", "front2", "back1", "back2", "back3" };
        var units = slots.Select(s => U("p_" + s, Team.Player, s))
            .Concat(slots.Select(s => U("e_" + s, Team.Enemy, s))).ToArray();
        var ctx = new CombatContext(new Rng(1), units, Rules());
        foreach (var unit in units)
        {
            Assert.AreSame(unit, ctx.FormationOf(unit.Team).GetSlot(unit.SlotId).Occupant);
            Assert.AreEqual(unit.Row == "front" ? 100 : 50, ctx.EffectiveTaunt(unit));
        }
    }

    [TestCase(Team.Player)]
    [TestCase(Team.Enemy)]
    public void Duplicate_Slot_Is_Rejected_Even_For_Dead_Units(Team team)
    {
        var dead = U("dead", team, "front1");
        dead.TakeDamage(100);
        Assert.Throws<ArgumentException>(() => new CombatContext(new Rng(1), new[] { dead, U("other", team, "front1") }, Rules()));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("front")]
    [TestCase("front3")]
    [TestCase("back4")]
    public void Invalid_Or_Missing_Slot_Is_Rejected_On_All_Teams(string slot)
    {
        foreach (var team in new[] { Team.Player, Team.Enemy })
            Assert.Throws<ArgumentException>(() => new CombatContext(new Rng(1), new[] { U("u", team, slot) }, Rules()));
    }

    [TestCase(Team.Player)]
    [TestCase(Team.Enemy)]
    public void Sixth_Unit_Cannot_Occupy_A_Team(Team team)
    {
        var slots = new[] { "front1", "front2", "back1", "back2", "back3", "front1" };
        var units = slots.Select((slot, i) => U("u" + i, team, slot)).ToArray();
        Assert.Throws<ArgumentException>(() => new CombatContext(new Rng(1), units, Rules()));
    }

    [Test]
    public void Modifiers_Stack_Clamp_At_Zero_And_Expire_Independently()
    {
        var unit = U("p", Team.Player, "front1");
        var ctx = new CombatContext(new Rng(1), new[] { unit }, Rules());
        var slot = ctx.FormationOf(Team.Player).GetSlot("front1");
        slot.AddModifier(30, 1);
        slot.AddModifier(-150, 2);
        Assert.AreEqual(0, slot.EffectiveTaunt);
        slot.Tick();
        Assert.AreEqual(0, ctx.EffectiveTaunt(unit));
        slot.Tick();
        Assert.AreEqual(100, slot.EffectiveTaunt);
        slot.AddModifier(20, 2);
        slot.AddModifier(30, 1);
        Assert.AreEqual(150, slot.EffectiveTaunt);
        slot.Tick();
        Assert.AreEqual(120, slot.EffectiveTaunt);
        slot.Tick();
        Assert.AreEqual(100, slot.EffectiveTaunt);
        Assert.AreEqual(100, slot.BaseTaunt, "Modifiers must not mutate the base.");
    }

    [Test]
    public void RoundEnd_Expires_Modifiers_On_Both_Formations()
    {
        var player = U("p", Team.Player, "front1");
        var enemy = U("e", Team.Enemy, "back1");
        var ctx = new CombatContext(new Rng(1), new[] { player, enemy }, Rules());
        ctx.FormationOf(Team.Player).GetSlot("front1").AddModifier(-80, 1);
        ctx.FormationOf(Team.Enemy).GetSlot("back1").AddModifier(100, 2);
        CQ.Core.Status.StatusResolver.RoundEndTick(ctx);
        Assert.AreEqual(100, ctx.EffectiveTaunt(player));
        Assert.AreEqual(150, ctx.EffectiveTaunt(enemy));
        CQ.Core.Status.StatusResolver.RoundEndTick(ctx);
        Assert.AreEqual(50, ctx.EffectiveTaunt(enemy));
    }

    [TestCase(Team.Player)]
    [TestCase(Team.Enemy)]
    public void Slot_Cards_Stack_On_Empty_Slot_And_Priority_Follows_Independent_Expiry(Team team)
    {
        var front = U("front", team, "front1");
        var back = U("back", team, "back1");
        var ctx = new CombatContext(new Rng(17), new[] { front, back }, Rules());
        var formation = ctx.FormationOf(team);
        var empty = formation.GetSlot("back3");
        CQ.Core.Cards.CardEffectResolver.ResolveSlot(
            new CardEffectSpec { type = "tauntUp", amount = 100, duration = 2 }, ctx, team, "back3");
        CQ.Core.Cards.CardEffectResolver.ResolveSlot(
            new CardEffectSpec { type = "tauntUp", amount = 30, duration = 1 }, ctx, team, "back3");
        CQ.Core.Cards.CardEffectResolver.ResolveSlot(
            new CardEffectSpec { type = "tauntDown", amount = 60, duration = 1 }, ctx, team, "back3");
        Assert.IsNull(empty.Occupant);
        Assert.AreEqual(0, empty.EffectiveTaunt, "空格可挂效果，但不可成为攻击候选");
        Assert.AreSame(front, TargetSelector.PickTarget(ctx, new[] { front, back }));
        Assert.IsTrue(formation.TryMove(back, "back3"));
        Assert.AreEqual(120, empty.EffectiveTaunt, "50 + 100 + 30 - 60，效果属于格而非单位");
        Assert.AreSame(back, TargetSelector.PickTarget(ctx, new[] { front, back }));
        CQ.Core.Status.StatusResolver.RoundEndTick(ctx);
        Assert.AreEqual(150, empty.EffectiveTaunt, "一回合的增减同时到期，两回合的增加保留");
        Assert.AreSame(back, TargetSelector.PickTarget(ctx, new[] { front, back }));
        CQ.Core.Status.StatusResolver.RoundEndTick(ctx);
        Assert.AreEqual(50, empty.EffectiveTaunt);
        Assert.AreSame(front, TargetSelector.PickTarget(ctx, new[] { front, back }));
        Assert.AreEqual(50, empty.BaseTaunt);
        Assert.AreSame(back, empty.Occupant);
    }

    [Test]
    public void Death_And_Empty_Slots_Have_Zero_Taunt_Without_Shifting_Occupants()
    {
        var front = U("front", Team.Player, "front1");
        var back = U("back", Team.Player, "back3");
        var ctx = new CombatContext(new Rng(1), new[] { front, back }, Rules());
        var formation = ctx.FormationOf(Team.Player);
        var frontSlot = formation.GetSlot("front1");
        var empty = formation.GetSlot("front2");
        frontSlot.AddModifier(500, 2);
        empty.AddModifier(500, 2);
        front.TakeDamage(100);
        Assert.AreEqual(0, frontSlot.EffectiveTaunt);
        Assert.AreEqual(0, ctx.EffectiveTaunt(front));
        Assert.AreEqual(0, empty.EffectiveTaunt);
        Assert.AreSame(front, frontSlot.Occupant);
        Assert.IsNull(empty.Occupant);
        Assert.AreSame(back, formation.GetSlot("back3").Occupant);
        Assert.AreEqual("back3", back.SlotId);
        Assert.AreEqual("back", back.Row);
        Assert.AreEqual(50, ctx.EffectiveTaunt(back));
        frontSlot.Tick();
        frontSlot.Tick();
        Assert.AreEqual(0, frontSlot.EffectiveTaunt);
        Assert.AreSame(front, frontSlot.Occupant);
    }
}
