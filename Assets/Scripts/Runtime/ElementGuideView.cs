using CQ.Core.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CQ.Runtime
{
    /// <summary>正五边形沿边箭头表示相克；文字与位置同时编码，不只依赖颜色。</summary>
    public sealed class ElementGuideView : MonoBehaviour
    {
        public static ElementGuideView Build(RectTransform parent)
        {
            var rect = UiKit.AddRect(parent, "ElementGuide", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-16f, -12f), new Vector2(220f, 244f));
            var view = rect.gameObject.AddComponent<ElementGuideView>();
            var cycle = CharacterElements.CounterCycle;
            var points = new Vector2[cycle.Count];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = (90f - i * 72f) * Mathf.Deg2Rad;
                points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 57f;
            }
            var center = new Vector2(0f, -130f);
            for (int i = 0; i < points.Length; i++)
            {
                var direction = (points[(i + 1) % points.Length] - points[i]).normalized;
                var from = center + points[i] + direction * 13f;
                var to = center + points[(i + 1) % points.Length] - direction * 13f;
                string edge = cycle[i] + "_" + cycle[(i + 1) % points.Length];
                Line(rect, edge, from, to);
                var normal = new Vector2(-direction.y, direction.x);
                Line(rect, edge + "_head1", to, to - direction * 8f + normal * 5f);
                Line(rect, edge + "_head2", to, to - direction * 8f - normal * 5f);
                var node = UiKit.AddRect(rect, "Node_" + cycle[i], new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    Vector2.one * 0.5f, center + points[i], Vector2.one * 18f);
                var image = node.gameObject.AddComponent<Image>();
                image.sprite = UiKit.CircleSprite();
                image.color = ElementPresentation.ColorOf(cycle[i]);
                image.raycastTarget = false;
                Label(rect, "Label_" + cycle[i], CharacterElements.Label(cycle[i]),
                    center + points[i] + points[i].normalized * 25f, new Vector2(30f, 32f), 22);
            }
            return view;
        }

        private static TextMeshProUGUI Label(Transform parent, string name, string text, Vector2 position, Vector2 size, int fontSize)
        {
            var label = UiKit.MakeText(parent, text, fontSize, Color.white, TextAnchor.MiddleCenter, true);
            label.name = name;
            var rect = (RectTransform)label.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            label.outlineColor = new Color(0.02f, 0.03f, 0.05f, 1f);
            label.outlineWidth = 0.2f;
            return label;
        }

        private static void Line(Transform parent, string name, Vector2 from, Vector2 to)
        {
            var delta = to - from;
            var rect = UiKit.AddRect(parent, "Arrow_" + name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                Vector2.one * 0.5f, (from + to) * 0.5f, new Vector2(delta.magnitude, 2f));
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.79f, 0.84f, 0.90f);
            image.raycastTarget = false;
        }
    }
}
