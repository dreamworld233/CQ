using UnityEngine;

namespace CQ.Runtime
{
    /// <summary>轻量程序化演出：命中斩痕、治疗十字与技能扩散环。寿命跟随本地播放倍率。</summary>
    public sealed class BattleEffect : MonoBehaviour
    {
        private static Sprite _ring;
        private SpriteRenderer _renderer;
        private SpriteRenderer _cross;
        private UnitView _view;
        private Color _color;
        private Vector3 _scale;
        private Vector3 _position;
        private float _life;
        private float _age;
        private bool _rise;

        public static void Spawn(UnitView view, string kind, Color color, float strength = 1f)
        {
            var go = new GameObject("Effect_" + kind);
            go.transform.SetParent(view.transform, false);
            var effect = go.AddComponent<BattleEffect>();
            effect._view = view;
            effect._color = color;
            effect._life = kind == "ring" ? 0.55f : 0.30f;
            effect._rise = kind == "heal";
            effect._renderer = go.AddComponent<SpriteRenderer>();
            effect._renderer.sortingOrder = 28;
            effect._renderer.color = color;
            effect._renderer.sprite = kind == "ring" ? RingSprite() : UnitView.WhiteSprite();
            effect._scale = kind == "slash" ? new Vector3(0.10f, 1.15f * strength, 1f) :
                kind == "heal" ? new Vector3(0.12f, 0.52f, 1f) : new Vector3(1.25f * strength, 1.25f * strength, 1f);
            if (kind == "slash") go.transform.localRotation = Quaternion.Euler(0f, 0f, -38f);
            if (kind == "heal")
            {
                go.transform.localPosition = new Vector3(0.45f, -0.2f, 0f);
                var cross = new GameObject("CrossArm");
                cross.transform.SetParent(go.transform, false);
                cross.transform.localScale = new Vector3(4.3f, 0.23f, 1f);
                var renderer = cross.AddComponent<SpriteRenderer>();
                renderer.sprite = UnitView.WhiteSprite();
                renderer.color = color;
                renderer.sortingOrder = 28;
                effect._cross = renderer;
            }
            effect._position = go.transform.localPosition;
            go.transform.localScale = effect._scale * 0.6f;
        }

        private void Update()
        {
            _age += Time.deltaTime * (_view != null ? _view.PlaybackSpeed : 1f);
            float t = Mathf.Clamp01(_age / _life);
            transform.localScale = _scale * Mathf.Lerp(0.6f, 1.35f, t);
            if (_rise) transform.localPosition = _position + Vector3.up * (0.7f * t);
            var color = _color;
            color.a *= 1f - t;
            _renderer.color = color;
            if (_cross != null) _cross.color = color;
            if (_age >= _life) Destroy(gameObject);
        }

        private static Sprite RingSprite()
        {
            if (_ring != null) return _ring;
            const int size = 64;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = (new Vector2(x + 0.5f, y + 0.5f) - Vector2.one * size * 0.5f).magnitude / (size * 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((0.98f - r) * 40f) * Mathf.Clamp01((r - 0.79f) * 30f));
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "BattlePulseRing" };
            texture.SetPixels(pixels);
            texture.Apply();
            _ring = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            return _ring;
        }
    }
}
