using UnityEngine;
using UnityEngine.UI;

namespace CQ.Runtime
{
    /// <summary>
    /// uGUI 构建小工具：统一字体（内置 CJK 回退）+ 快捷建 Text/Button/Rect。
    /// 全部代码构建，不依赖场景资产。
    /// </summary>
    public static class UiKit
    {
        private static Font _font;

        /// <summary>优先 Resources 内置 CJK 字体；缺则 OS 动态字体回退链。</summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.Load<Font>("Fonts/SimHei");
                if (_font == null)
                    _font = Font.CreateDynamicFontFromOSFont(
                        new[] { "Microsoft YaHei", "SimHei", "Arial Unicode MS", "Noto Sans CJK SC", "Droid Sans Fallback" }, 16);
                return _font;
            }
        }

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

        public static Text MakeText(Transform parent, string text, int size, Color color, TextAnchor anchor)
        {
            var rt = AddRect(parent, "Text", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button MakeButton(Transform parent, string label, int size, Color bg)
        {
            var rt = AddRect(parent, "Button", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = bg;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var labelT = MakeText(rt, label, size, Color.white, TextAnchor.MiddleCenter);
            var lrt = (RectTransform)labelT.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            return btn;
        }

        /// <summary>圆形技能按钮：纯代码生成圆图，热区为整块方形（移动端好点）。</summary>
        public static Button MakeCircleButton(Transform parent, string label, int fontSize, Color bg, float diameter)
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

            MakeText(rt, label, fontSize, Color.white, TextAnchor.MiddleCenter);
            return btn;
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