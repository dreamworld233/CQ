using CQ.Core.Combat;
using TMPro;
using UnityEngine;

namespace CQ.Runtime
{
    /// <summary>头顶状态格：剑/盾/眩晕星，正负色底；角标为剩余个人行动次数，常驻用小菱形。</summary>
    public sealed class StatusIconView : MonoBehaviour
    {
        public static readonly Color PositiveColor = new Color(0.72f, 0.16f, 0.25f);
        public static readonly Color NegativeColor = new Color(0.12f, 0.36f, 0.72f);
        private static readonly Sprite[] Icons = new Sprite[3];
        private TextMeshPro _duration;
        private TextMeshPro _detail;
        private GameObject _permanent;
        private bool _pinned;
        private bool _interactionSuppressed;

        public StatusType Type { get; private set; }
        public string Description { get; private set; }

        public static StatusIconView Build(Transform parent, StatusType type, int index)
        {
            var go = new GameObject("Status_" + type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(-0.7f + index * 0.49f, 1.55f, 0f);
            var view = go.AddComponent<StatusIconView>();
            view.Type = type;
            var border = view.MakeSprite("Border", UnitView.WhiteSprite(), new Color(0.03f, 0.05f, 0.08f), 45);
            border.transform.localScale = new Vector3(0.44f, 0.44f, 1f);
            var background = view.MakeSprite("Tile", UnitView.WhiteSprite(),
                type == StatusType.Stun ? NegativeColor : PositiveColor, 46);
            background.transform.localScale = new Vector3(0.38f, 0.38f, 1f);
            var icon = view.MakeSprite("Symbol", Icon(type), Color.white, 47);
            icon.transform.localScale = Vector3.one * 0.30f;
            view._duration = TmpKit.MakeWorldText(go.transform, "Duration", new Vector3(0.16f, -0.14f, 0f),
                UiKit.FontBold, 0.16f, Color.white, 49, FontStyles.Normal, "1");
            TmpKit.SetOutline(view._duration, Color.black, 0.3f);
            var permanent = view.MakeSprite("Permanent", UnitView.WhiteSprite(), new Color(1f, 0.84f, 0.35f), 49);
            permanent.transform.localPosition = new Vector3(0.14f, -0.13f, 0f);
            permanent.transform.localScale = new Vector3(0.09f, 0.09f, 1f);
            permanent.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            view._permanent = permanent.gameObject;
            view._detail = TmpKit.MakeWorldText(go.transform, "Detail", new Vector3(0.7f - index * 0.49f, 0.43f, 0f),
                UiKit.FontBold, 0.22f, Color.white, 70);
            TmpKit.SetOutline(view._detail, Color.black, 0.3f);
            view._detail.gameObject.SetActive(false);
            go.AddComponent<BoxCollider2D>().size = Vector2.one * 0.44f;
            go.SetActive(false);
            return view;
        }

        public void SetSlot(int index)
        {
            float x = -0.7f + index * 0.49f;
            transform.localPosition = new Vector3(x, 1.55f, 0f);
            _detail.transform.localPosition = new Vector3(-x, 0.43f, 0f);
        }

        public void Refresh(StatusEffect status)
        {
            if (status == null)
            {
                _pinned = false;
                _detail.gameObject.SetActive(false);
                gameObject.SetActive(false);
                return;
            }
            gameObject.SetActive(true);
            bool permanent = status.Turn == "permanent";
            _duration.gameObject.SetActive(!permanent);
            _permanent.SetActive(permanent);
            _duration.text = status.Duration.ToString();
            if (!permanent) TmpKit.FitWorldHeight(_duration, 0.16f);
            string label = Type == StatusType.AttackUp ? "攻击 +" + status.Percent + "%" :
                Type == StatusType.DefenseUp ? "防御 +" + status.Percent + "%" : "眩晕";
            Description = label + (permanent ? " · 常驻" : " · 剩余 " + status.Duration + " 次行动");
            _detail.text = Description;
            if (_detail.gameObject.activeSelf) TmpKit.FitWorldHeight(_detail, 0.22f);
        }

        public void SetInteractionSuppressed(bool suppressed)
        {
            _interactionSuppressed = suppressed;
            if (!suppressed) return;
            _pinned = false;
            ShowDetail(false);
        }

        private void OnMouseEnter() { if (!_interactionSuppressed) ShowDetail(true); }
        private void OnMouseExit() { if (!_pinned) ShowDetail(false); }
        private void OnMouseDown()
        {
            if (_interactionSuppressed) return;
            _pinned = !_pinned;
            ShowDetail(_pinned);
        }

        private void ShowDetail(bool visible)
        {
            _detail.gameObject.SetActive(visible);
            if (visible) TmpKit.FitWorldHeight(_detail, 0.22f);
        }

        private SpriteRenderer MakeSprite(string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Sprite Icon(StatusType type)
        {
            int index = (int)type;
            if (Icons[index] != null) return Icons[index];
            const int size = 48;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    bool ink;
                    if (type == StatusType.AttackUp)
                    {
                        // 竖剑：剑尖、剑身、护手、剑柄。
                        ink = (v > -0.25f && v < 0.8f && Mathf.Abs(u) < Mathf.Min(0.18f, (0.85f - v) * 0.65f)) ||
                            (v > -0.38f && v < -0.23f && Mathf.Abs(u) < 0.52f) ||
                            (v > -0.75f && v < -0.38f && Mathf.Abs(u) < 0.11f);
                    }
                    else if (type == StatusType.DefenseUp)
                    {
                        float width = v >= -0.1f ? 0.65f : 0.65f * (v + 0.85f) / 0.75f;
                        ink = v > -0.85f && v < 0.65f && Mathf.Abs(u) < width &&
                            (Mathf.Abs(u) > width - 0.16f || v > 0.48f || v < -0.61f || Mathf.Abs(u) < 0.07f);
                    }
                    else
                    {
                        float r = Mathf.Sqrt(u * u + v * v);
                        float angle = Mathf.Atan2(v, u);
                        float edge = 0.48f + 0.22f * Mathf.Cos(5f * angle);
                        ink = r < edge || ((u + 0.63f) * (u + 0.63f) + (v - 0.58f) * (v - 0.58f) < 0.025f);
                    }
                    pixels[y * size + x] = ink ? Color.white : Color.clear;
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "StatusIcon_" + type };
            texture.SetPixels(pixels);
            texture.Apply();
            Icons[index] = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            return Icons[index];
        }
    }
}
