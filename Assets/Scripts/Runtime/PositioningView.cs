using System;
using CQ.Core.Combat;
using UnityEngine;

namespace CQ.Runtime
{
    /// <summary>
    /// 阵型布局参数。Play 中可在 BattleController 的 Inspector 里实时调，
    /// 调好把数值抄回代码默认值即可（不依赖手工场景）。
    /// </summary>
    [Serializable]
    public class BattleLayout
    {
        [Tooltip("同侧单位间距（世界单位）。必须大于单位碰撞体宽，否则点选会命中错单位")]
        public float teamGap = 2.0f;

        [Tooltip("阵型整体右移量，避开左侧行动条")]
        public float arenaShift = 0.9f;

        [Tooltip("最前排到阵型中心的距离")]
        public float baseOffset = 1.8f;

        [Tooltip("前排 y（低且近）")]
        public float frontY = -1.2f;

        [Tooltip("后排 y（高且远）")]
        public float backY = 1.2f;
    }

    /// <summary>
    /// 前后排 + 双方站位（2.5D 框架）。玩家右侧、敌方左侧；前排低且近，后排高且远；
    /// 同侧单位按槽位横排。视觉差异玩法本期不做，只站位。
    /// </summary>
    public static class PositioningView
    {
        public static Vector3 Position(Unit unit, int slot, BattleLayout layout)
        {
            if (layout == null) layout = new BattleLayout();
            float side = unit.Team == Team.Player ? 1f : -1f;
            float x = layout.arenaShift + side * (layout.baseOffset + slot * layout.teamGap);
            bool front = unit.Row == "front";
            float y = front ? layout.frontY : layout.backY;
            float z = front ? -0.6f : 0.4f;   // 前排更靠近相机
            return new Vector3(x, y, z);
        }
    }
}
