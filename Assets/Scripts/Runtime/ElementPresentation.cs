using UnityEngine;

namespace CQ.Runtime
{
    /// <summary>伙伴栏、场上徽标与伤害数字共用五行色；不参与伤害计算。</summary>
    public static class ElementPresentation
    {
        public static Color ColorOf(string element)
        {
            switch (element)
            {
                case "metal": return new Color(0.95f, 0.97f, 1f);
                case "wood": return new Color(0.35f, 0.82f, 0.46f);
                case "water": return new Color(0.30f, 0.62f, 1f);
                case "fire": return new Color(0.98f, 0.30f, 0.29f);
                case "earth": return new Color(1f, 0.83f, 0.26f);
                default: return Color.white;
            }
        }

        public static Color DamageColor(string element, int multiplierPercent)
        {
            var color = ColorOf(element);
            if (multiplierPercent > 100)
                return Color.Lerp(color, new Color(1f, 0.99f, 0.90f), 0.35f);
            if (multiplierPercent < 100)
                return Color.Lerp(color, new Color(0.38f, 0.40f, 0.44f), 0.5f);
            return color;
        }

        public static string RelationLabel(int multiplierPercent) =>
            multiplierPercent > 100 ? "克制" : multiplierPercent < 100 ? "抵抗" : "";
    }
}
