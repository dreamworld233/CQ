using System.Collections.Generic;
using System.Linq;
using CQ.Core.Combat;

namespace CQ.Core.Battle
{
    public sealed class StatusSnapshot
    {
        public StatusType Type { get; }
        public int Duration { get; }
        public string Turn { get; }
        public int Percent { get; }
        internal StatusSnapshot(StatusEffect status)
        {
            Type = status.Type; Duration = status.Duration; Turn = status.Turn;
            Percent = status.Percent;
        }
    }

    public sealed class UnitSnapshot
    {
        public string UnitId { get; }
        public int Hp { get; }
        public int MaxHp { get; }
        public int Ult { get; }
        public int MaxUlt { get; }
        public string SlotId { get; }
        public decimal Attack { get; }
        public decimal Defense { get; }
        public int Speed { get; }
        public bool IsDead => Hp <= 0;
        public IReadOnlyList<StatusSnapshot> Statuses { get; }
        internal UnitSnapshot(Unit unit, CombatContext ctx)
        {
            UnitId = unit.Id; Hp = unit.Hp; MaxHp = unit.MaxHp; Ult = unit.Ult; MaxUlt = unit.MaxUlt;
            SlotId = unit.SlotId; Speed = unit.BaseSpeed;
            Attack = ctx.StatesOf(unit.Id).EffectiveAttack(unit);
            Defense = ctx.StatesOf(unit.Id).EffectiveDefense(unit);
            Statuses = ctx.GetStatuses(unit.Id).Select(s => new StatusSnapshot(s)).ToList().AsReadOnly();
        }
    }

    public sealed class UnitChange
    {
        public UnitSnapshot Before { get; }
        public UnitSnapshot After { get; }
        public string UnitId => After.UnitId;
        public int HpDelta => After.Hp - Before.Hp;
        public int UltDelta => After.Ult - Before.Ult;
        internal UnitChange(UnitSnapshot before, UnitSnapshot after) { Before = before; After = after; }
    }

    public sealed class AttackHit
    {
        public string SourceUnitId { get; }
        public string TargetUnitId { get; }
        public int HpDamage { get; }
        public bool TargetDead { get; }
        internal AttackHit(string source, string target, int hpDamage, bool dead)
        {
            SourceUnitId = source; TargetUnitId = target; HpDamage = hpDamage; TargetDead = dead;
        }
    }

    public sealed class HealingHit
    {
        public string SourceUnitId { get; }
        public string TargetUnitId { get; }
        public int HpRestored { get; }
        internal HealingHit(string source, string target, int restored)
        { SourceUnitId = source; TargetUnitId = target; HpRestored = restored; }
    }

    public sealed class UltCharge
    {
        public string UnitId { get; }
        public string Trigger { get; }
        public int Before { get; }
        public int After { get; }
        public int Amount => After - Before;
        internal UltCharge(string unitId, string trigger, int before, int after)
        { UnitId = unitId; Trigger = trigger; Before = before; After = after; }
    }

    public sealed class ActionResult
    {
        public long Sequence { get; }
        public BattleEventKind Kind { get; }
        public string ActorId { get; }
        public string ActionId { get; }
        public string ActionName { get; }
        public string ActionType { get; }
        public IReadOnlyList<UnitChange> UnitChanges { get; }
        public IReadOnlyList<AttackHit> Hits { get; }
        public IReadOnlyList<string> HitTargetIds { get; }
        public IReadOnlyList<UltCharge> Charges { get; }
        public IReadOnlyList<HealingHit> Heals { get; }
        internal ActionResult(long sequence, BattleEventKind kind, string actorId, string actionId,
            string actionName, string actionType, IEnumerable<UnitChange> changes,
            IEnumerable<AttackHit> hits, IEnumerable<UltCharge> charges, IEnumerable<HealingHit> heals)
        {
            Sequence = sequence; Kind = kind; ActorId = actorId; ActionId = actionId;
            ActionName = actionName; ActionType = actionType;
            UnitChanges = changes.ToList().AsReadOnly(); Hits = hits.ToList().AsReadOnly();
            HitTargetIds = Hits.Select(h => h.TargetUnitId).Distinct().ToList().AsReadOnly();
            Charges = charges.ToList().AsReadOnly();
            Heals = heals.ToList().AsReadOnly();
        }
    }
}
