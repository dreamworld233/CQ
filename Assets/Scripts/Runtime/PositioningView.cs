using System;
using CQ.Core.Combat;
using UnityEngine;

namespace CQ.Runtime
{
    [Serializable]
    public class BattleLayout
    {
        public float teamGap = 2.8f;
        public float arenaShift = 0f;
        public float baseOffset = 1.5f;
        public float frontY = 0f;
        public float backY = 0f;
    }

    /// <summary>Mirrored two-column formations: player left, enemy right, fronts toward center.</summary>
    public static class PositioningView
    {
        public static Vector3 Position(Unit unit, BattleLayout layout) => Position(unit.Team, unit.SlotId, layout);

        public static Vector3 Position(Team team, string slotId, BattleLayout layout)
        {
            if (!Formation.IsValidSlot(slotId)) throw new ArgumentException("未知阵型槽位: " + slotId, nameof(slotId));
            if (layout == null) layout = new BattleLayout();
            bool front = Formation.RowOf(slotId) == "front";
            int index = slotId[slotId.Length - 1] - '1';
            float side = team == Team.Player ? -1f : 1f;
            float x = layout.arenaShift + side * (layout.baseOffset + (front ? 0f : 2.5f));
            float y = (front ? layout.frontY : layout.backY) + ((front ? 0.5f : 1f) - index) * layout.teamGap;
            return new Vector3(x, y, front ? -0.6f : 0.4f);
        }
    }
}
