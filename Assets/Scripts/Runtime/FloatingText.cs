using TMPro;
using UnityEngine;

namespace CQ.Runtime
{
    /// <summary>
    /// 世界空间飘字（伤害/治疗数值）：放大弹入 + 上浮 + 淡出，到时自毁。纯表现，不影响战斗状态。
    /// 数值越大字越大（一眼判断这一下有多重）。
    /// </summary>
    public sealed class FloatingText : MonoBehaviour
    {
        private const float Life = 1.4f;          // 存活时长
        private const float RiseSpeed = 1.25f;    // 上浮速度（世界单位/秒）
        private const float PopDur = 0.16f;       // 弹入时长
        private const float PopFrom = 0.6f;       // 弹入起始缩放
        private const float PopPeak = 1.18f;      // 弹入峰值缩放
        private const float FadeStart = 0.55f;    // 存活比例后开始淡出
        private const float MinH = 0.46f;         // 最小字高（世界单位）
        private const float MaxH = 0.92f;         // 最大字高

        private TextMeshPro _tmp;
        private Vector3 _baseScale = Vector3.one;
        private float _age;

        public static void Spawn(Transform parent, string text, Color color, float localY, float magnitude = 0f)
        {
            if (parent == null || string.IsNullOrEmpty(text)) return;

            float h = Mathf.Clamp(MinH + Mathf.Abs(magnitude) / 60f, MinH, MaxH);
            var tmp = TmpKit.MakeWorldText(parent, "Float", new Vector3(0f, localY, 0f),
                UiKit.FontBold, h, color, 60, FontStyles.Bold, text);
            TmpKit.SetOutline(tmp, new Color(0f, 0f, 0f, 0.95f), 0.3f);

            var ft = tmp.gameObject.AddComponent<FloatingText>();
            ft._tmp = tmp;
            ft._baseScale = tmp.transform.localScale;
            tmp.transform.localScale = ft._baseScale * PopFrom;
        }

        private void Update()
        {
            _age += Time.deltaTime;

            transform.localPosition += Vector3.up * (RiseSpeed * Time.deltaTime);

            float pop = _age <= PopDur
                ? Mathf.Lerp(PopFrom, PopPeak, _age / PopDur)
                : Mathf.Lerp(PopPeak, 1f, Mathf.Clamp01((_age - PopDur) / 0.15f));
            transform.localScale = _baseScale * pop;

            if (_tmp != null)
            {
                var c = _tmp.color;
                c.a = Mathf.Clamp01(1f - Mathf.Clamp01((_age / Life - FadeStart) / (1f - FadeStart)));
                _tmp.color = c;
            }

            if (_age >= Life) Destroy(gameObject);
        }
    }
}
