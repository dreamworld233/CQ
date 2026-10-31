using System;

namespace CQ.Core.Combat
{
    public enum StatusType
    {
        AttackUp,
        DefenseUp,
        Stun
    }

    public static class StatusTypes
    {
        public static StatusType Parse(string id)
        {
            switch (id)
            {
                case "attackUp": return StatusType.AttackUp;
                case "defenseUp": return StatusType.DefenseUp;
                case "stun": return StatusType.Stun;
                default: throw new ArgumentException("未知状态 id: " + id);
            }
        }
    }
}
