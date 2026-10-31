using System;
using CQ.Core.Combat;

namespace CQ.Core.Battle
{
    public enum PersonalActionKind
    {
        Basic,
        Special,
        Ultimate,
        Skipped
    }

    public sealed class PersonalActionCycle
    {
        private readonly Unit _unit;
        private readonly int _actionEnergy;
        private readonly int _hitEnergy;
        private readonly int _specialInterval;
        private int _lastSpecial;
        private PersonalActionKind? _active;

        public int Opportunities { get; private set; }
        public bool IsOccupied => _active.HasValue;
        public bool IsSpecialDue => Opportunities + 1 - _lastSpecial >= _specialInterval;

        public PersonalActionCycle(Unit unit, int initialEnergy, int maxEnergy,
            int actionEnergy, int hitEnergy, int specialInterval)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (maxEnergy <= 0 || initialEnergy < 0 || initialEnergy > maxEnergy ||
                actionEnergy < 0 || hitEnergy < 0 || specialInterval <= 0)
                throw new ArgumentOutOfRangeException(nameof(initialEnergy), "行动周期配置无效");
            _unit = unit;
            _actionEnergy = actionEnergy;
            _hitEnergy = hitEnergy;
            _specialInterval = specialInterval;
            unit.MaxUlt = maxEnergy;
            unit.Ult = initialEnergy;
        }

        public PersonalActionKind Begin(bool stunned)
        {
            if (IsOccupied || _unit.IsDead)
                throw new InvalidOperationException("单位不能开始新行动");
            Opportunities++;
            PersonalActionKind kind;
            if (stunned) kind = PersonalActionKind.Skipped;
            else if (_unit.Ult >= _unit.MaxUlt)
            {
                kind = PersonalActionKind.Ultimate;
                _unit.GainUlt(-_unit.MaxUlt);
            }
            else if (Opportunities - _lastSpecial >= _specialInterval)
            {
                kind = PersonalActionKind.Special;
                _lastSpecial = Opportunities;
            }
            else kind = PersonalActionKind.Basic;
            _active = kind;
            return kind;
        }

        public void Complete()
        {
            if (!IsOccupied) throw new InvalidOperationException("没有进行中的个人行动");
            if (!_unit.IsDead && (_active == PersonalActionKind.Basic || _active == PersonalActionKind.Special))
                _unit.GainUlt(_actionEnergy);
            _active = null;
        }

        public void ReceiveAttack()
        {
            if (!_unit.IsDead) _unit.GainUlt(_hitEnergy);
        }
    }
}
