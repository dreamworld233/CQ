using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CQ.Runtime.App
{
    public static class AppUi
    {
        public static readonly Color Background = new Color(0.035f, 0.065f, 0.075f);
        public static readonly Color Card = new Color(0.07f, 0.13f, 0.14f, 0.98f);
        public static readonly Color Button = new Color(0.13f, 0.27f, 0.26f);
        public static readonly Color Accent = new Color(0.85f, 0.91f, 0.70f);
        public static readonly Color Secondary = new Color(0.64f, 0.75f, 0.74f);

        public static RectTransform Canvas(Transform owner, string name, int order)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(owner, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return (RectTransform)go.transform;
        }

        public static RectTransform Box(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var rect = UiKit.AddRect(parent, name, Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            UiKit.UseRoundedBackground(image);
            image.color = color;
            return rect;
        }

        public static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string value, int font,
            Vector2 position, Vector2 size, Color? color = null, bool centered = false)
        {
            var text = UiKit.MakeText(parent, value, font, color ?? Color.white,
                centered ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft);
            text.name = name;
            text.richText = false;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            Place((RectTransform)text.transform, position, size);
            return text;
        }

        public static Button MakeButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Action action)
        {
            var button = UiKit.MakeButton(parent, label, 28, Button);
            button.name = name;
            Place((RectTransform)button.transform, position, size);
            button.onClick.AddListener(() => action?.Invoke());
            return button;
        }
    }
}
