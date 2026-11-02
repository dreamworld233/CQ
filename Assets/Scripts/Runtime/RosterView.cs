using System;
using System.Collections.Generic;
using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CQ.Runtime
{
    /// <summary>战前伙伴栏：分类只影响列表，勾选始终取真实部署状态。</summary>
    public sealed class RosterView : MonoBehaviour
    {
        public const float PanelWidth = 356f;
        private static readonly Color CardColor = new Color(0.15f, 0.21f, 0.28f);
        private static readonly Color ActiveColor = new Color(0.12f, 0.30f, 0.28f);
        private static readonly Dictionary<int, Sprite> Portraits = new Dictionary<int, Sprite>();
        private static Sprite _checkSprite;
        private readonly Dictionary<string, Button> _filters = new Dictionary<string, Button>();
        private readonly List<GameObject> _cards = new List<GameObject>();
        private RectTransform _content;
        private TextMeshProUGUI _count;
        private TextMeshProUGUI _notice;
        private TextMeshProUGUI _empty;
        private ScrollRect _scroll;
        private PartyRoster _party;
        private string _newId;
        private string _filter = "";
        public Action<string> OnToggle;
        public string Filter => _filter;
        public int VisibleCount { get; private set; }
        public RectTransform PanelRect => (RectTransform)transform;

        public static RosterView Build(RectTransform parent)
        {
            var rect = UiKit.AddRect(parent, "RosterPanel", new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 0.5f), Vector2.zero, new Vector2(PanelWidth, 0f));
            rect.gameObject.AddComponent<Image>().color = new Color(0.055f, 0.085f, 0.12f, 0.98f);
            var view = rect.gameObject.AddComponent<RosterView>();
            view.BuildAll();
            rect.gameObject.SetActive(false);
            return view;
        }

        private void BuildAll()
        {
            Label(transform, "Title", "伙伴", 30, new Vector2(18f, -18f), new Vector2(160f, 44f));
            _count = Label(transform, "DeployedCount", "上阵 0 / 5", 22, new Vector2(180f, -22f), new Vector2(158f, 40f));
            var line = UiKit.AddRect(transform, "Divider", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(-32f, 2f));
            line.gameObject.AddComponent<Image>().color = new Color(0.32f, 0.47f, 0.52f, 0.6f);
            string[] elements = new[] { "" }.Concat(CharacterElements.All).ToArray();
            for (int i = 0; i < elements.Length; i++)
            {
                string element = elements[i];
                var button = UiKit.MakeButton(transform, element == "" ? "全部" : "",
                    18, new Color(0.15f, 0.22f, 0.29f));
                button.name = "Filter_" + (element == "" ? "all" : element);
                Place((RectTransform)button.transform, new Vector2(14f, -104f - i * 66f), new Vector2(54f, 54f));
                if (element != "")
                {
                    var icon = UiKit.AddRect(button.transform, "ElementIcon", Vector2.one * 0.5f, Vector2.one * 0.5f,
                        Vector2.one * 0.5f, Vector2.zero, new Vector2(28f, 28f));
                    AddElementIcon(icon, element);
                }
                _filters.Add(element, button);
                button.onClick.AddListener(() => SetFilter(element));
            }
            var viewport = UiKit.AddRect(transform, "RosterViewport", new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            viewport.offsetMin = new Vector2(80f, 160f);
            viewport.offsetMax = new Vector2(-12f, -100f);
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            viewport.gameObject.AddComponent<RectMask2D>();
            _content = UiKit.AddRect(viewport, "RosterContent", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            _scroll = viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = viewport;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 38f;
            _empty = UiKit.MakeText(viewport, "暂无伙伴", 21, new Color(0.67f, 0.76f, 0.82f), TextAnchor.UpperCenter);
            _empty.name = "EmptyFilter";
            ((RectTransform)_empty.transform).offsetMin = new Vector2(8f, 0f);
            ((RectTransform)_empty.transform).offsetMax = new Vector2(-8f, -30f);
            _empty.enableWordWrapping = true;
            _notice = Label(transform, "RosterNotice", "点击头像上下阵\n拖动场上伙伴调整站位", 20,
                new Vector2(18f, 0f), new Vector2(320f, 130f));
            var noticeRect = (RectTransform)_notice.transform;
            noticeRect.anchorMin = noticeRect.anchorMax = noticeRect.pivot = new Vector2(0f, 0f);
            noticeRect.anchoredPosition = new Vector2(18f, 18f);
            _notice.enableWordWrapping = true;
            _notice.alignment = TextAlignmentOptions.TopLeft;
        }

        public void Show(PartyRoster party, string newId)
        {
            bool newcomer = newId != null && newId != _newId;
            _party = party;
            _newId = newId;
            if (newcomer) _filter = "";
            gameObject.SetActive(true);
            Refresh();
        }

        public void SetFilter(string element)
        {
            if (element != "" && !CharacterElements.IsValid(element)) return;
            _filter = element;
            _scroll.StopMovement();
            _content.anchoredPosition = Vector2.zero;
            Refresh();
        }

        private void Refresh()
        {
            if (_party == null) return;
            foreach (var card in _cards)
            {
                card.SetActive(false);
                if (Application.isPlaying) Destroy(card); else DestroyImmediate(card);
            }
            _cards.Clear();
            foreach (var pair in _filters)
                pair.Value.GetComponent<Image>().color = pair.Key == _filter ? new Color(0.25f, 0.49f, 0.48f) : new Color(0.15f, 0.22f, 0.29f);
            var characters = _party.Available.Where(character => _filter == "" || character.element == _filter).ToList();
            VisibleCount = characters.Count;
            _count.text = "上阵 " + _party.Deployment.Count + " / 5";
            _empty.text = _filter == "" ? "暂无伙伴" : "暂无" + CharacterElements.Label(_filter) + "属性伙伴";
            _empty.gameObject.SetActive(characters.Count == 0);
            for (int i = 0; i < characters.Count; i++) BuildCard(characters[i], i);
            _content.sizeDelta = new Vector2(0f, Mathf.Ceil(characters.Count / 2f) * 176f + 6f);
            var newcomer = _party.Available.FirstOrDefault(character => character.id == _newId && !_party.IsDeployed(character.id));
            _notice.text = newcomer != null ? "新伙伴：" + newcomer.name + "\n点击头像，自行上阵\n拖动场上伙伴调整站位\n点击场上角色查看技能" :
                !_party.CanStart ? "尚未上阵伙伴\n点击头像上阵后开战" : "点击头像上下阵\n拖动场上伙伴调整站位\n点击场上角色查看技能";
        }

        private void BuildCard(CharacterConfig character, int index)
        {
            bool deployed = _party.IsDeployed(character.id);
            var button = UiKit.MakeButton(_content, "", 20, deployed ? ActiveColor : CardColor);
            button.name = "RosterCard_" + character.id;
            Place((RectTransform)button.transform, new Vector2(6f + index % 2 * 124f, -6f - index / 2 * 176f), new Vector2(112f, 164f));
            _cards.Add(button.gameObject);
            var outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = deployed ? new Color(0.44f, 0.86f, 0.65f) : new Color(0.3f, 0.4f, 0.49f);
            outline.effectDistance = Vector2.one * 2f;
            var iconRect = UiKit.AddRect(button.transform, "Portrait", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(96f, 90f));
            var portrait = iconRect.gameObject.AddComponent<Image>();
            portrait.sprite = Portrait(character.id);
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            portrait.color = deployed ? new Color(0.8f, 0.88f, 0.84f, 0.68f) : Color.white;
            if (portrait.sprite == null)
            {
                portrait.enabled = false;
                UiKit.MakeText(iconRect, character.name.Substring(0, 1), 38, Color.white, TextAnchor.MiddleCenter, true);
            }
            var badge = UiKit.AddRect(button.transform, "ElementBadge", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(3f, -3f), new Vector2(32f, 32f));
            AddElementIcon(badge, character.element);
            var check = UiKit.AddRect(button.transform, "DeployedCheck", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(34f, 34f));
            var checkImage = check.gameObject.AddComponent<Image>();
            checkImage.sprite = CheckSprite();
            checkImage.raycastTarget = false;
            check.gameObject.SetActive(deployed);
            Label(button.transform, "Name", character.name, 20, new Vector2(4f, -100f), new Vector2(104f, 32f));
            var state = Label(button.transform, "DeploymentState", deployed ? "已上阵" : character.id == _newId ? "新伙伴" : "待命", 16,
                new Vector2(4f, -132f), new Vector2(104f, 28f));
            state.color = deployed ? new Color(0.61f, 0.95f, 0.73f) : character.id == _newId ? new Color(1f, 0.85f, 0.48f) : new Color(0.67f, 0.75f, 0.82f);
            button.onClick.AddListener(() => OnToggle?.Invoke(character.id));
        }

        private static TextMeshProUGUI Label(Transform parent, string name, string text, int size, Vector2 position, Vector2 dimensions)
        {
            var label = UiKit.MakeText(parent, text, size, Color.white, TextAnchor.MiddleLeft, true);
            label.name = name;
            Place((RectTransform)label.transform, position, dimensions);
            return label;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void AddElementIcon(RectTransform rect, string element)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiKit.CircleSprite();
            image.color = ElementColor(element);
            image.raycastTarget = false;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.025f, 0.04f, 0.06f, 0.95f);
            outline.effectDistance = Vector2.one * 2f;
        }

        public static Color ElementColor(string element)
        {
            switch (element)
            {
                case "metal": return new Color(0.95f, 0.97f, 1f);
                case "wood": return new Color(0.35f, 0.82f, 0.46f);
                case "water": return new Color(0.30f, 0.62f, 1f);
                case "fire": return new Color(0.98f, 0.30f, 0.29f);
                case "earth": return new Color(1f, 0.83f, 0.26f);
                default: return Color.gray;
            }
        }

        private static Sprite Portrait(string id)
        {
            var source = ArtLibrary.UnitSprite(id);
            if (source == null) return null;
            int key = source.GetInstanceID();
            if (Portraits.TryGetValue(key, out var portrait)) return portrait;
            var rect = source.rect;
            float height = rect.height * 0.58f;
            float size = Mathf.Min(rect.width, height);
            rect = new Rect(rect.center.x - size * 0.5f, rect.yMax - height, size, height);
            portrait = Sprite.Create(source.texture, rect, Vector2.one * 0.5f, source.pixelsPerUnit);
            portrait.name = "RosterPortrait_" + id;
            Portraits[key] = portrait;
            return portrait;
        }

        private static Sprite CheckSprite()
        {
            if (_checkSprite != null) return _checkSprite;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f);
                    bool ink = SegmentDistance(point, new Vector2(13f, 32f), new Vector2(27f, 18f)) < 4.5f ||
                        SegmentDistance(point, new Vector2(27f, 18f), new Vector2(52f, 47f)) < 4.5f;
                    pixels[y * size + x] = ink ? new Color(0.55f, 1f, 0.63f) :
                        Vector2.Distance(point, Vector2.one * 32f) < 30f ? new Color(0.025f, 0.08f, 0.08f, 0.94f) : Color.clear;
                }
            texture.SetPixels(pixels);
            texture.Apply();
            _checkSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            return _checkSprite;
        }

        private static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b) =>
            Vector2.Distance(point, a + (b - a) * Mathf.Clamp01(Vector2.Dot(point - a, b - a) / (b - a).sqrMagnitude));
    }
}
