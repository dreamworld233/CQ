using System.Collections.Generic;
using CQ.Core.Combat;

namespace CQ.Core.Battle
{
    /// <summary>
    /// 排序后的行动队列。按序逐个弹出单位。
    /// </summary>
    public sealed class ActionQueue
    {
        private readonly List<Unit> _order;
        private int _index;

        public ActionQueue(IReadOnlyList<Unit> orderedUnits)
        {
            _order = new List<Unit>(orderedUnits ?? new List<Unit>());
            _index = 0;
        }

        public bool HasNext => _index < _order.Count;

        public int Remaining => _order.Count - _index;

        public IReadOnlyList<Unit> Order => _order;

        public Unit Peek() => HasNext ? _order[_index] : null;

        public Unit Next() => HasNext ? _order[_index++] : null;
    }
}