using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CQ.Runtime
{
    /// <summary>
    /// uGUI 构建小工具：统一 TMP 字体（思源黑体 HW SC SDF）+ 快捷建 Text/Button/Rect。
    /// 全部代码构建，不依赖场景资产。
    /// </summary>
    public static class UiKit
    {
        /// <summary>正文字体（TMP SDF，Dynamic 图集，运行时逐字栅格化）。</summary>
        public static TMP_FontAsset Font => TmpKit.Regular;

        /// <summary>粗体字体（数值/标题）。</summary>
        public static TMP_FontAsset FontBold => TmpKit.Bold;

        public static RectTransform AddRect(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        public static TextMeshProUGUI MakeText(Transform parent, string text, int size, Color color,
            TextAnchor anchor, bool bold = false)
        {
            var rt = AddRect(parent, "Text", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = bold ? FontBold : Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = ToTmp(anchor);
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Legacy TextAnchor → TMP 对齐。锚点只用到这几个。</summary>
        public static TextAlignmentOptions ToTmp(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                default: return TextAlignmentOptions.BottomRight;
            }
        }

        public static Button MakeButton(Transform parent, string label, int size, Color bg)
        {
            var rt = AddRect(parent, "Button", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = bg;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var labelT = MakeText(rt, label, size, Color.white, TextAnchor.MiddleCenter, true);
            var lrt = (RectTransform)labelT.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            return btn;
        }

        /// <summary>
        /// 圆形技能按钮：纯代码生成圆图，热区为整块方形（移动端好点）。
        /// icon 非空时图标居上、文字下沉；缺图标则只剩居中文字（原有表现）。
        /// </summary>
        public static Button MakeCircleButton(Transform parent, string label, int fontSize, Color bg,
            float diameter, Sprite icon = null)
        {
            var rt = AddRect(parent, "CircleButton",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(diameter, diameter));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = CircleSprite();
            img.color = bg;
            img.raycastTarget = true;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var labelT = MakeText(rt, label, fontSize, Color.white, TextAnchor.MiddleCenter, true);
            labelT.enableWordWrapping = true;
            TmpKit.SetOutline(labelT, new Color(0f, 0f, 0f, 0.85f), 0.18f);

            if (icon != null) LayoutCircleIcon(rt, icon, labelT, diameter);
            return btn;
        }

        /// <summary>圆钮内图标 + 文字两行布局（图标居上、文字下沉）。</summary>
        private static void LayoutCircleIcon(RectTransform button, Sprite icon, TMP_Text label, float diameter)
        {
            float box = diameter * 0.44f;
            var iconRt = AddRect(button, "Icon",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, diameter * 0.18f), new Vector2(box, box));
            var iconImg = iconRt.gameObject.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = new Vector2(0.5f, 0.5f);
            lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(diameter * 0.92f, diameter * 0.34f);
            lrt.anchoredPosition = new Vector2(0f, -diameter * 0.24f);
            label.fontSize = Mathf.Max(12f, Mathf.RoundToInt(label.fontSize * 0.8f));
        }

        private static Sprite _circle;

        /// <summary>128×128 白色实心圆（1.5px 软边），供圆形按钮/圆点复用。</summary>
        public static Sprite CircleSprite()
        {
            if (_circle != null) return _circle;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - 1.5f - d);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            _circle = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return _circle;
        }

        public static Image MakePanel(Transform parent, Color bg)
        {
            var rt = AddRect(parent, "Panel", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = bg;
            img.raycastTarget = true;
            return img;
        }
    }
}
