using System.Collections.Generic;
using System.Linq;
using CQ.Core.Combat;

namespace CQ.Core.Battle
{
    /// <summary>脱离可变战场的状态快照。</summary>
    public sealed class StatusSnapshot
    {
        public StatusType Type { get; }
        public int Duration { get; }
        public string Turn { get; }
        public int Percent { get; }
        public int Amount { get; }
        public string SourceUnitId { get; }
        internal StatusSnapshot(StatusEffect status)
        {
            Type = status.Type; Duration = status.Duration; Turn = status.Turn;
            Percent = status.Percent; Amount = status.Amount; SourceUnitId = status.SourceUnitId;
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
        public int Haste { get; }
        public int EffectiveTaunt { get; }
        public bool IsDead => Hp <= 0;
        public IReadOnlyList<StatusSnapshot> Statuses { get; }
        internal UnitSnapshot(Unit unit, CombatContext ctx)
        {
            UnitId = unit.Id; Hp = unit.Hp; MaxHp = unit.MaxHp; Ult = unit.Ult; MaxUlt = unit.MaxUlt;
            SlotId = unit.SlotId; Haste = ctx.GetHaste(unit.Id); EffectiveTaunt = ctx.EffectiveTaunt(unit);
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

    /// <summary>每个实际攻击段一条；盾全吸收仍命中，攻击前已死亡者不记录。</summary>
    public sealed class AttackHit
    {
        public string SourceUnitId { get; }
        public string TargetUnitId { get; }
        public int HpDamage { get; }
        public int ShieldAbsorbed { get; }
        public bool TargetDead { get; }
        internal AttackHit(string source, string target, int hpDamage, int shieldAbsorbed, bool dead)
        {
            SourceUnitId = source; TargetUnitId = target; HpDamage = hpDamage;
            ShieldAbsorbed = shieldAbsorbed; TargetDead = dead;
        }
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

    /// <summary>一个完整动作的不可变结果，不参与调度，无 FIFO 或事件总线。</summary>
    public sealed class ActionResult
    {
        public long Sequence { get; }
        public BattleEventKind Kind { get; }
        public string ActorId { get; }
        public string ActionId { get; }
        public IReadOnlyList<UnitChange> UnitChanges { get; }
        public IReadOnlyList<AttackHit> Hits { get; }
        public IReadOnlyList<string> HitTargetIds { get; }
        public IReadOnlyList<UltCharge> Charges { get; }
        internal ActionResult(long sequence, BattleEventKind kind, string actorId, string actionId,
            IEnumerable<UnitChange> changes, IEnumerable<AttackHit> hits, IEnumerable<UltCharge> charges)
        {
            Sequence = sequence; Kind = kind; ActorId = actorId; ActionId = actionId;
            UnitChanges = changes.ToList().AsReadOnly(); Hits = hits.ToList().AsReadOnly();
            HitTargetIds = Hits.Select(h => h.TargetUnitId).Distinct().ToList().AsReadOnly();
            Charges = charges.ToList().AsReadOnly();
        }
    }
}
