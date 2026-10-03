using UnityEngine;

namespace CQ.Runtime
{
    /// <summary>
    /// 世界空间飘字（伤害/治疗数值）：上浮 + 淡出，到时自毁。纯表现，不影响战斗状态。
    /// </summary>
    public sealed class FloatingText : MonoBehaviour
    {
        private const float Life = 1.1f;
        private const float RiseSpeed = 1.1f;

        private TextMesh _tm;
        private float _age;

        public static void Spawn(Transform parent, string text, Color color, float localY)
        {
            if (parent == null || string.IsNullOrEmpty(text)) return;

            var go = new GameObject("Float");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, localY, 0f);

            var tm = go.AddComponent<TextMesh>();
            tm.font = UiKit.Font;          // CJK：默认 Arial 无中文字形
            tm.text = text;
            tm.fontSize = 56;
            tm.characterSize = 0.07f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 60;

            var ft = go.AddComponent<FloatingText>();
            ft._tm = tm;
        }

        private void Update()
        {
            _age += Time.deltaTime;

            transform.localPosition += Vector3.up * (RiseSpeed * Time.deltaTime);

            if (_tm != null)
            {
                var c = _tm.color;
                c.a = Mathf.Clamp01(1f - _age / Life);
                _tm.color = c;
            }

            if (_age >= Life) Destroy(gameObject);
        }
    }
}
