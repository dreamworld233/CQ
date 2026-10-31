using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CQ.Runtime
{
    public sealed class BattleReportView : MonoBehaviour
    {
        private static readonly Color[] MetricColors =
        {
            new Color32(0xe6, 0x67, 0x67, 255),
            new Color32(0x39, 0x87, 0xe5, 255),
            new Color32(0x19, 0x9e, 0x70, 255)
        };
        private static readonly string[] MetricNames = { "DamageDealt", "DamageTaken", "HealingDone" };
        private static readonly string[] MetricLabels = { "输出", "承伤", "治疗" };
        private const float ColumnWidth = 280f;
        private const float RowHeight = 44f;
        private RectTransform _rows;
        private ScrollRect _scroll;
        private Button _exactButton;
        private TextMeshProUGUI _exactLabel;
        private bool _exact;
        private readonly List<ValueLabel> _values = new List<ValueLabel>();
        private static Sprite _barSprite;

        private sealed class ValueLabel
        {
            public long Value;
            public TextMeshProUGUI Label;
        }

        public static BattleReportView Build(Transform parent)
        {
            var rect = UiKit.AddRect(parent, "BattleReport", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(1140f, 442f));
            var view = rect.gameObject.AddComponent<BattleReportView>();
            view.BuildAll(rect);
            return view;
        }

        private void BuildAll(RectTransform rect)
        {
            for (int metric = 0; metric < 3; metric++)
            {
                float x = 180f + metric * 320f;
                var swatch = UiKit.AddRect(rect, "MetricColor_" + metric, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(0f, 1f), new Vector2(x, -10f), new Vector2(16f, 16f));
                swatch.gameObject.AddComponent<Image>().color = MetricColors[metric];
                var title = UiKit.MakeText(rect, MetricLabels[metric], 22, Color.white, TextAnchor.MiddleLeft, true);
                title.name = "MetricHeader_" + metric;
                Place((RectTransform)title.transform, x + 26f, 0f, ColumnWidth - 26f, 36f);
            }
            _exactButton = UiKit.MakeButton(rect, "完整数值", 20, new Color(0.16f, 0.22f, 0.3f));
            _exactButton.name = "ExactValuesButton";
            Place((RectTransform)_exactButton.transform, 0f, 0f, 142f, 36f);
            _exactLabel = _exactButton.GetComponentInChildren<TextMeshProUGUI>();
            _exactButton.onClick.AddListener(() =>
            {
                _exact = !_exact;
                RefreshValues();
            });
            var viewport = UiKit.AddRect(rect, "ReportViewport", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(0f, -44f), new Vector2(1140f, 352f));
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            _rows = UiKit.AddRect(viewport, "ReportRows", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 352f));
            _scroll = viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = viewport;
            _scroll.content = _rows;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 36f;
            var note = UiKit.MakeText(rect, "每列独立比较，仅统计实际生效值，不含溢出", 20,
                new Color(0.72f, 0.78f, 0.84f), TextAnchor.MiddleCenter);
            Place((RectTransform)note.transform, 0f, -404f, 1140f, 32f);
        }

        public void Show(IReadOnlyList<UnitBattleStatistics> rows)
        {
            Clear();
            var maxima = new[] { rows.Select(row => row.DamageDealt).DefaultIfEmpty().Max(),
                rows.Select(row => row.DamageTaken).DefaultIfEmpty().Max(),
                rows.Select(row => row.HealingDone).DefaultIfEmpty().Max() };
            float y = 0f;
            foreach (var team in new[] { Team.Player, Team.Enemy })
            {
                var members = rows.Where(row => row.Team == team).ToList();
                if (members.Count == 0) continue;
                var heading = UiKit.MakeText(_rows, team == Team.Player ? "我方伙伴" : "敌方", 22,
                    new Color(0.75f, 0.82f, 0.89f), TextAnchor.MiddleLeft, true);
                Place((RectTransform)heading.transform, 0f, y, 1140f, 36f);
                y -= 36f;
                foreach (var unit in members)
                {
                    var row = UiKit.AddRect(_rows, "ReportRow_" + unit.UnitId, new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(1140f, RowHeight));
                    var name = UiKit.MakeText(row, unit.Name, 24, Color.white, TextAnchor.MiddleLeft, true);
                    name.name = "UnitName";
                    Place((RectTransform)name.transform, 0f, 0f, 160f, RowHeight);
                    name.enableAutoSizing = true;
                    name.fontSizeMin = 18f;
                    name.fontSizeMax = 24f;
                    var values = new[] { unit.DamageDealt, unit.DamageTaken, unit.HealingDone };
                    for (int metric = 0; metric < 3; metric++) AddBar(row, metric, values[metric], maxima[metric]);
                    y -= RowHeight;
                }
                y -= 8f;
            }
            _rows.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(352f, -y));
            _scroll.verticalNormalizedPosition = 1f;
            RefreshValues();
        }

        private void AddBar(RectTransform row, int metric, long value, long maximum)
        {
            var track = UiKit.AddRect(row, MetricNames[metric], new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(180f + metric * 320f, -10f), new Vector2(ColumnWidth, 24f));
            track.gameObject.AddComponent<Image>().color = new Color(0.16f, 0.20f, 0.26f);
            var fill = UiKit.AddRect(track, "Fill", Vector2.zero, new Vector2(FillRatio(value, maximum), 1f),
                Vector2.zero, Vector2.zero, Vector2.zero);
            var image = fill.gameObject.AddComponent<Image>();
            image.sprite = BarSprite();
            image.type = Image.Type.Sliced;
            image.color = MetricColors[metric];
            image.raycastTarget = false;
            fill.gameObject.SetActive(value > 0);
            var label = UiKit.MakeText(track, "", 21, Color.white, TextAnchor.MiddleCenter, true);
            label.name = "Value";
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(ColumnWidth, 36f);
            TmpKit.SetOutline(label, new Color(0.02f, 0.03f, 0.05f), 0.35f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 21f;
            _values.Add(new ValueLabel { Value = value, Label = label });
        }

        private void RefreshValues()
        {
            _exactLabel.text = _exact ? "简写数值" : "完整数值";
            foreach (var value in _values)
                value.Label.text = _exact ? value.Value.ToString(CultureInfo.InvariantCulture) : FormatValue(value.Value);
        }

        public void Clear()
        {
            _values.Clear();
            _exact = false;
            _exactLabel.text = "完整数值";
            _scroll.StopMovement();
            for (int i = _rows.childCount - 1; i >= 0; i--)
            {
                var child = _rows.GetChild(i);
                child.gameObject.SetActive(false);
                child.SetParent(null);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
            _rows.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 352f);
        }

        public static float FillRatio(long value, long maximum) =>
            value <= 0 || maximum <= 0 ? 0f : Mathf.Clamp01((float)((double)value / maximum));

        public static string FormatValue(long value)
        {
            decimal divisor;
            string suffix;
            if (value >= 1000000000L) { divisor = 1000000000m; suffix = "b"; }
            else if (value >= 1000000L) { divisor = 1000000m; suffix = "m"; }
            else if (value >= 1000L) { divisor = 1000m; suffix = "k"; }
            else return value.ToString(CultureInfo.InvariantCulture);
            return (decimal.Truncate(value / divisor * 10m) / 10m).ToString("0.#", CultureInfo.InvariantCulture) + suffix;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static Sprite BarSprite()
        {
            if (_barSprite != null) return _barSprite;
            var texture = new Texture2D(12, 12, TextureFormat.RGBA32, false);
            var pixels = new Color[144];
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 12; x++)
                {
                    float dx = Mathf.Max(0f, x - 7.5f);
                    float dy = Mathf.Max(0f, Mathf.Abs(y - 5.5f) - 2f);
                    pixels[y * 12 + x] = dx * dx + dy * dy <= 16f ? Color.white : Color.clear;
                }
            texture.SetPixels(pixels);
            texture.Apply();
            _barSprite = Sprite.Create(texture, new Rect(0f, 0f, 12f, 12f), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, new Vector4(0f, 4f, 4f, 4f));
            return _barSprite;
        }
    }
}
