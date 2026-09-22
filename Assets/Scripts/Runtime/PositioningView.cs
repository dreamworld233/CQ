using CQ.Core.Combat;
using UnityEngine;

namespace CQ.Runtime
{
    /// <summary>
    /// 前后排 + 双方站位（2.5D 框架）。玩家右侧、敌方左侧；前排低且近，后排高且远；
    /// 同侧单位按槽位横排。视觉差异玩法本期不做，只站位。
    /// </summary>
    public static class PositioningView
    {
        private const float TeamSpacing = 1.4f;

        public static Vector3 Position(Unit unit, int slot)
        {
            float side = unit.Team == Team.Player ? 1f : -1f;
            float x = side * (2.2f + slot * TeamSpacing);
            bool front = unit.Row == "front";
            float y = front ? -1.2f : 1.2f;
            float z = front ? -0.6f : 0.4f;   // 前排更靠近相机
            return new Vector3(x, y, z);
        }
    }
}