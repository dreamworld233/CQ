using System;
using System.Collections.Generic;
using System.Text;
using CQ.Core.Config;
using UnityEngine;
using UnityEngine.UI;

namespace CQ.Runtime
{
    /// <summary>
    /// 选中角色后在其「头顶上方」沿弧弹出的圆形技能按钮（每角色 3 个）。
    /// 世界锚点 → 屏幕点 → 画布局部点；按钮沿弧均布，位置全代码控制。
    /// 全部代码构建，不依赖场景资产与美术素材。
    /// </summary>
    public sealed class RadialSkillMenu : MonoBehaviour
    {
        public Action<SkillSpec> OnPick;

        [SerializeField] private bool verbose = true;

        private readonly List<GameObject> _buttons = new List<GameObject>();
        private RectTransform _canvas;
        private Camera _cam;

        private const float Diameter = 190f;    // 圆钮直径（移动端触控热区）
        private const float Radius = 195f;      // 弧半径
        private const float HeadOffset = 105f;  // 从角色中心再上移到头顶
        private const float SweepStart = 140f;  // 弧起点角（度，左上）
        private const float SweepEnd = 40f;     // 弧终点角（度，右上）

        private static readonly Color UsableBg = new Color(0.16f, 0.32f, 0.50f, 0.98f);
        private static readonly Color DisabledBg = new Color(0.30f, 0.30f, 0.30f, 0.70f);

        public static RadialSkillMenu Build(Transform owner)
        {
            var go = new GameObject("RadialSkillMenu", typeof(RadialSkillMenu));
            go.transform.SetParent(owner, false);
            return go.GetComponent<RadialSkillMenu>();
        }

        public void Bind(RectTransform canvas, Camera cam)
        {
            _canvas = canvas;
            _cam = cam;
        }

        public void Show(Vector3 worldAnchor, IReadOnlyList<SkillSpec> skills, Func<SkillSpec, bool> canUse)
        {
            Clear();
            gameObject.SetActive(true);

            if (_canvas == null || _cam == null)
            {
                Debug.LogWarning("[CQ.Radial] 未 Bind canvas/cam，跳过弹出");
                return;
            }
            int n = skills != null ? skills.Count : 0;
            if (n <= 0)
            {
                Debug.LogWarning("[CQ.Radial] 该角色无技能，跳过弹出");
                return;
            }

            Vector2 anchor = WorldToCanvas(worldAnchor) + new Vector2(0f, HeadOffset);
            var log = verbose ? new StringBuilder() : null;
            if (log != null)
                log.Append("[CQ.Radial] canvas=").Append(_canvas.rect.size)
                   .Append(" world=").Append(worldAnchor.ToString("F2"))
                   .Append(" local=").Append(anchor.ToString("F0"))
                   .Append(" n=").Append(n);

            for (int i = 0; i < n; i++)
            {
                SkillSpec s = skills[i];
                float t = n == 1 ? 0.5f : (float)i / (n - 1);
                float rad = Mathf.Lerp(SweepStart, SweepEnd, t) * Mathf.Deg2Rad;
                Vector2 pos = anchor + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * Radius;

                bool usable = canUse != null && canUse(s);
                // 不可用标记用 ×(U+00D7)：✕(U+2715) 不在思源黑体字形表里，TMP 会换成 □ 并刷警告
                string label = usable ? s.name : s.name + "\n×";
                var btn = UiKit.MakeCircleButton(_canvas, label, 30, usable ? UsableBg : DisabledBg, Diameter);
                btn.name = "Skill_" + s.id;
                btn.interactable = usable;

                var rt = (RectTransform)btn.transform;
                rt.anchoredPosition = pos;

                SkillSpec captured = s;
                btn.onClick.AddListener(() => OnPick?.Invoke(captured));
                _buttons.Add(btn.gameObject);

                if (log != null) log.Append("\n  [").Append(i).Append("] ").Append(s.name)
                    .Append(" pos=").Append(pos.ToString("F0")).Append(" usable=").Append(usable);
            }

            if (log != null) Debug.Log(log.ToString());
        }

        public void Hide()
        {
            Clear();
            gameObject.SetActive(false);
        }

        private void Clear()
        {
            foreach (var b in _buttons) { if (b != null) Destroy(b); }
            _buttons.Clear();
        }

        private Vector2 WorldToCanvas(Vector3 world)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(_cam, world);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out local);
            return local;
        }
    }
}
