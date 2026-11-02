using System.Collections.Generic;
using CQ.Core.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CQ.Runtime
{
    /// <summary>附着角色的只读技能气泡；不遮暗战场，不拦截气泡外的操作。</summary>
    public sealed class SkillDetailsView : MonoBehaviour
    {
        public const float DisplayDuration = 5f;
        public const float FadeInDuration = 0.14f;
        public const float FadeOutDuration = 0.18f;
        private CanvasGroup _group;
        private float _remaining;
        private const float Width = 380f;
        private const float Height = 350f;
        private static readonly Color Background = new Color(0.055f, 0.09f, 0.13f, 0.98f);
        private static readonly string[] Types = { "special", "ult", "passive" };
        private TextMeshProUGUI _title;
        private readonly TextMeshProUGUI[] _names = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] _effects = new TextMeshProUGUI[3];
        private RectTransform _card;
        private RectTransform _tail;
        public bool Visible => gameObject.activeSelf;
        public string UnitId { get; private set; }
        public bool OnRight { get; private set; }

        public static SkillDetailsView Build(RectTransform parent)
        {
            var root = UiKit.AddRect(parent, "SkillDetails", Vector2.zero, Vector2.one,
                Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
            var view = root.gameObject.AddComponent<SkillDetailsView>();
            view.BuildAll();
            view.Hide();
            return view;
        }

        private void BuildAll()
        {
            _group = gameObject.AddComponent<CanvasGroup>();
            _tail = UiKit.AddRect(transform, "BubbleTail", Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, Vector2.zero, Vector2.one * 20f);
            _tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var tailImage = _tail.gameObject.AddComponent<Image>();
            tailImage.color = Background;
            tailImage.raycastTarget = false;
            _card = UiKit.AddRect(transform, "SkillCard", Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.zero, Vector2.zero, new Vector2(Width, Height));
            var image = _card.gameObject.AddComponent<Image>();
            UiKit.UseRoundedBackground(image);
            image.color = Background;
            var outline = _card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.44f, 0.60f, 0.65f, 0.65f);
            outline.effectDistance = Vector2.one;
            // 仅气泡本身消费点击，外部可直接切换角色或拖拽。
            var blocker = _card.gameObject.AddComponent<Button>();
            blocker.transition = Selectable.Transition.None;
            blocker.targetGraphic = image;
            _title = Label(_card, "UnitTitle", "", 24, new Vector2(20f, -12f), new Vector2(340f, 42f), Color.white);
            for (int i = 0; i < Types.Length; i++)
            {
                var slot = UiKit.AddRect(_card, "Skill_" + Types[i], new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(0f, 1f), new Vector2(20f, -66f - i * 92f), new Vector2(-40f, 84f));
                Color accent = i == 1 ? new Color(1f, 0.82f, 0.38f) : i == 2 ? new Color(0.55f, 0.90f, 0.70f) : new Color(0.56f, 0.79f, 1f);
                _names[i] = Label(slot, "SkillName", "", 21, Vector2.zero, new Vector2(340f, 34f), accent);
                _effects[i] = Label(slot, "SkillEffects", "", 20, new Vector2(0f, -34f), new Vector2(340f, 50f), new Color(0.88f, 0.92f, 0.94f));
                _effects[i].enableWordWrapping = true;
            }
        }

        public void Show(string unitId, string unitName, bool enemy, IReadOnlyList<SkillSpec> skills)
        {
            UnitId = unitId;
            _title.text = unitName + (enemy ? " · 敌方" : "");
            for (int i = 0; i < Types.Length; i++)
            {
                SkillSpec skill = null;
                if (skills != null)
                    foreach (var entry in skills)
                        if (entry.type == Types[i]) { skill = entry; break; }
                string label = SkillDescription.TypeLabel(Types[i]);
                _names[i].text = label + (skill == null || skill.name == label ? "" : " · " + skill.name);
                _effects[i].text = SkillDescription.Summary(skill);
            }
            _remaining = DisplayDuration;
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            gameObject.SetActive(true);
        }

        public static bool PreferRight(bool enemy, bool front) => enemy ? !front : front;

        public void Follow(Camera camera, Bounds bounds, bool preferRight)
        {
            var root = (RectTransform)transform;
            Vector2 center = camera.WorldToScreenPoint(bounds.center);
            Vector2 left = camera.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.center.y, bounds.center.z));
            Vector2 right = camera.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.center.y, bounds.center.z));
            Rect safe = Screen.safeArea;
            Rect viewport = camera.pixelRect;
            Vector2 min = new Vector2(Mathf.Max(safe.xMin, viewport.xMin), Mathf.Max(safe.yMin, viewport.yMin));
            Vector2 max = new Vector2(Mathf.Min(safe.xMax, viewport.xMax), Mathf.Min(safe.yMax, viewport.yMax));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, min, null, out var localMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, max, null, out var localMax);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, new Vector2(left.x, center.y), null, out var localLeft);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, new Vector2(right.x, center.y), null, out var localRight);
            PlaceNear(localLeft, localRight, preferRight, Rect.MinMaxRect(localMin.x + 20f, localMin.y + 24f, localMax.x - 20f, localMax.y - 24f));
        }

        public void PlaceNear(Vector2 left, Vector2 right, bool preferRight, Rect available)
        {
            const float gap = 18f;
            float rightX = right.x + gap;
            float leftX = left.x - gap - Width;
            bool fitsRight = rightX + Width <= available.xMax;
            bool fitsLeft = leftX >= available.xMin;
            OnRight = preferRight ? fitsRight || !fitsLeft : !fitsLeft && fitsRight;
            float x = Mathf.Clamp(OnRight ? rightX : leftX, available.xMin, Mathf.Max(available.xMin, available.xMax - Width));
            float y = Mathf.Clamp((left.y + right.y) * 0.5f - Height * 0.5f, available.yMin, Mathf.Max(available.yMin, available.yMax - Height));
            _card.anchoredPosition = new Vector2(x, y);
            _tail.anchoredPosition = new Vector2(OnRight ? x : x + Width,
                Mathf.Clamp((left.y + right.y) * 0.5f, y + 28f, y + Height - 28f));
        }

        private void Update() => AdvanceLifetime(Time.unscaledDeltaTime);

        public void AdvanceLifetime(float unscaledDeltaTime)
        {
            if (!Visible) return;
            _remaining -= Mathf.Max(0f, unscaledDeltaTime);
            if (_remaining <= 0f) { Hide(); return; }
            float fadeIn = Mathf.Clamp01((DisplayDuration - _remaining) / FadeInDuration);
            float fadeOut = Mathf.Clamp01(_remaining / FadeOutDuration);
            _group.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Min(fadeIn, fadeOut));
            _group.blocksRaycasts = _remaining > FadeOutDuration && _group.alpha > 0.01f;
            _group.interactable = _group.blocksRaycasts;
        }

        public void Hide()
        {
            _remaining = 0f;
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            gameObject.SetActive(false);
            UnitId = null;
        }

        private static TextMeshProUGUI Label(Transform parent, string name, string text, int size,
            Vector2 position, Vector2 dimensions, Color color)
        {
            var label = UiKit.MakeText(parent, text, size, color, TextAnchor.UpperLeft);
            label.name = name;
            var rect = (RectTransform)label.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            return label;
        }
    }
}
