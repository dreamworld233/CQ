using System;

namespace CQ.Core.Combat
{
    public enum StatusType
    {
        Weaken,
        Shield,
        AttackUp,
        Slow,
        Interrupt
    }

    public static class StatusTypes
    {
        public static StatusType Parse(string id)
        {
            switch (id)
            {
                case "weaken": return StatusType.Weaken;
                case "shield": return StatusType.Shield;
                case "attackUp": return StatusType.AttackUp;
                case "slow": return StatusType.Slow;
                case "interrupt": return StatusType.Interrupt;
                default: throw new ArgumentException("未知状态 id: " + id);
            }
        }
    }
}