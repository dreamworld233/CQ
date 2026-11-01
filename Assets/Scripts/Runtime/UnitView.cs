using System.Collections.Generic;
using System.Linq;
using CQ.Core.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace CQ.Runtime
{
    public sealed class UnitView : MonoBehaviour
    {
        private const float ArtHeight = 1.5f;      // 示意图目标世界高
        private const float ColliderW = 1.4f;      // 点选碰撞体宽（须小于同侧间距 teamGap）
        private const float ColliderH = 1.7f;      // 点选碰撞体高

        // 数值条布局（世界单位）。BarW 须小于 teamGap(2.0)，否则相邻单位的条视觉粘连。
        private const float BarW = 1.9f;           // 条全宽
        private const float BarInset = 0.12f;      // 填充相对底框的左右内缩（形成描边观感）
        private const float HpY = 1.13f;           // 头顶血条，与立绘顶部留出间隙
        private const float HpBgH = 0.30f;
        private const float HpFillH = 0.22f;
        private const float UltY = 0.83f;
        private const float SubBgH = 0.20f;
        private const float SubFillH = 0.14f;
        private const float LabelY = -1.04f;
        private const float FloatY = 0.15f;         // 数值从躯干上浮，避开头顶HUD

        private const float HpTextH = 0.20f;
        private const float LabelTextH = 0.25f;
        private const float StatTextH = 0.14f;
        private const float ActionTextH = 0.36f;

        private const float FlashDur = 0.28f;      // 受击闪白时长
        private const float ShakeDur = 0.22f;      // 受击抖动时长
        private const float ShakeAmp = 0.14f;      // 受击抖动幅度（世界单位）

        private static Sprite _white;

        private SpriteRenderer _sr;
        private TextMeshPro _label;
        private TextMeshPro _stat;
        private TextMeshPro _hpText;
        private TextMeshPro _action;
        private readonly List<StatusIconView> _statusIcons = new List<StatusIconView>();
        private SortingGroup _formationSorting;
        private SpriteRenderer _shadow;
        private bool _formationLifted;
        private bool _formationTarget;
        private Renderer[] _formationHudRenderers;
        private bool _teamTint;

        private SpriteRenderer _hpFill;
        private SpriteRenderer _ultFill;

        private Color _tint = Color.white;
        private Color _hpColor = Color.white;
        private bool _lowHp;
        private float _flashT;
        private float _shakeT;
        private Color _flashColor = Color.white;
        private Vector3 _artScale;
        private float _actionT;
        private float _actionDuration;
        private float _actionStrength;
        private float _direction;
        private bool _melee;
        private Vector3 _approachOffset;
        private int _idleSortOrder;
        private bool _dead;
        private float _deathAge;

        public string UnitId { get; private set; }
        public float PlaybackSpeed { get; set; } = 1f;
        public bool FeedbackComplete => _flashT <= 0f && _shakeT <= 0f && _actionT <= 0f && (!_dead || _deathAge >= 0.45f);

        public void Setup(Unit unit, Color teamColor)
        {
            UnitId = unit.Id;
            _direction = unit.Team == Team.Player ? 1f : -1f;
            string id = unit.Id?.Split('_')[0];
            _melee = id == "sword" || id == "guard" || id == "grunt" || id == "claw" || id == "lord";

            var art = ArtLibrary.UnitSprite(unit.Id);
            _teamTint = art == null;

            var artGo = new GameObject("Art");
            artGo.transform.SetParent(transform, false);
            _sr = artGo.AddComponent<SpriteRenderer>();
            if (art != null)
            {
                _sr.sprite = art;
                _sr.color = Color.white;
                FitSpriteHeight(artGo.transform, art, ArtHeight);
            }
            else
            {
                _sr.sprite = WhiteSprite();
                _sr.color = teamColor;
            }
            _artScale = artGo.transform.localScale;
            _sr.sortingOrder = _idleSortOrder = unit.Row == "front" ? 10 : 5;

            _action = TmpKit.MakeWorldText(transform, "ActionText", new Vector3(0f, 2.12f, 0f),
                UiKit.FontBold, ActionTextH, Color.yellow, 55);
            TmpKit.SetOutline(_action, new Color(0f, 0f, 0f, 0.9f), 0.2f);

            MakeBars();
            _label = TmpKit.MakeWorldText(transform, "Label", new Vector3(0f, LabelY, 0f),
                UiKit.FontBold, LabelTextH, Color.white, 55, FontStyles.Normal, unit.Name);
            TmpKit.SetOutline(_label, new Color(0f, 0f, 0f, 0.9f), 0.2f);
            foreach (StatusType type in System.Enum.GetValues(typeof(StatusType)))
                _statusIcons.Add(StatusIconView.Build(transform, type, _statusIcons.Count));

            _formationSorting = gameObject.AddComponent<SortingGroup>();
            _formationSorting.sortingOrder = 100;
            _formationSorting.enabled = false;
            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(transform, false);
            shadow.transform.localPosition = new Vector3(0f, -0.78f, 0f);
            shadow.transform.localScale = new Vector3(0.85f, 0.18f, 1f);
            _shadow = shadow.AddComponent<SpriteRenderer>();
            _shadow.sprite = UiKit.CircleSprite();
            _shadow.color = new Color(0.02f, 0.04f, 0.06f, 0.22f);
            _shadow.sortingOrder = 2;

            var col = gameObject.AddComponent<BoxCollider2D>();
            col.size = new Vector2(ColliderW, ColliderH);

            Refresh(unit, "");
            _formationHudRenderers = GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer != _sr && renderer != _shadow).ToArray();
        }

        public void SetPosition(Vector3 position, Unit unit)
        {
            transform.position = position;
            _shakeT = 0f;
            _actionT = 0f;
            _approachOffset = Vector3.zero;
            if (_sr != null)
            {
                _sr.transform.localPosition = Vector3.zero;
                _sr.sortingOrder = _idleSortOrder = unit.Row == "front" ? 10 : 5;
            }
        }

        public void Refresh(Unit unit, string actionText, IReadOnlyList<StatusEffect> statuses = null)
        {
            float hpRatio = unit.MaxHp > 0 ? (float)unit.Hp / unit.MaxHp : 0f;
            if (_hpFill != null)
            {
                SetFill(_hpFill, hpRatio, BarW - BarInset);
                _lowHp = !unit.IsDead && hpRatio <= 0.3f;
                _hpColor = _lowHp
                    ? new Color(1f, 0.25f, 0.18f)
                    : (unit.Team == Team.Player ? new Color(0.35f, 0.92f, 0.42f) : new Color(0.95f, 0.36f, 0.30f));
                _hpFill.color = _hpColor;
            }

            SetFill(_ultFill, unit.MaxUlt > 0 ? (float)unit.Ult / unit.MaxUlt : 0f, BarW - BarInset);

            if (_hpText != null)
            {
                _hpText.text = unit.Hp + "/" + unit.MaxHp;
                TmpKit.FitWorldHeight(_hpText, HpTextH);   // 位数变化后重新定标
            }

            if (_label != null)
                _label.text = unit.Name;

            if (_stat != null)
            {
                _stat.text = "能量 " + unit.Ult + "/" + unit.MaxUlt;
                TmpKit.FitWorldHeight(_stat, StatTextH);
            }

            _ultFill.color = unit.Ult >= unit.MaxUlt && !unit.IsDead
                ? new Color(1f, 0.82f, 0.22f) : new Color(0.20f, 0.66f, 1f);
            int visible = 0;
            foreach (var icon in _statusIcons)
            {
                var status = unit.IsDead || statuses == null ? null : statuses.Where(s => s.Type == icon.Type)
                    .OrderByDescending(s => s.Percent).FirstOrDefault();
                icon.Refresh(status);
                if (status != null)
                    icon.SetSlot(visible++);
            }

            SetActionText(actionText);
        }

        public void SetActionText(string actionText)
        {
            if (_action == null) return;
            _action.text = actionText;
            _action.gameObject.SetActive(!string.IsNullOrEmpty(actionText));
            FitReadableText(_action, ActionTextH, 2.7f);
        }

        private static void FitReadableText(TextMeshPro text, float height, float maxWidth)
        {
            if (text == null || !text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) return;
            TmpKit.FitWorldHeight(text, height);
            float width = text.textBounds.size.x * text.transform.localScale.x;
            if (width > maxWidth) text.transform.localScale *= maxWidth / width;
        }

        /// <summary>飘字（伤害/治疗数值），到时自动销毁。magnitude 越大字越大。</summary>
        public void FloatText(string text, Color color, float magnitude = 0f)
        {
            FloatingText.Spawn(transform, text, color, FloatY, magnitude);
        }

        /// <summary>受击/受治疗反馈：闪白（伤害用白、治疗用绿）+ 伤害附带横向抖动。</summary>
        public void Flash(int hpDelta)
        {
            if (hpDelta == 0) return;
            _flashColor = hpDelta < 0 ? new Color(1f, 1f, 1f, 1f) : new Color(0.5f, 1f, 0.6f, 1f);
            _flashT = FlashDur;
            if (hpDelta < 0) _shakeT = ShakeDur;
        }

        /// <summary>近战接近本次实际目标再回位；返回命中前等待时间，包含在原有动作时长内。</summary>
        public float PlayAction(string actionType, Vector3? targetPosition = null)
        {
            if (_dead || (actionType != "basic" && actionType != "special" && actionType != "ult")) return 0f;
            bool approach = _melee && targetPosition.HasValue;
            _actionDuration = actionType == "ult" ? 0.60f : approach ? (actionType == "special" ? 0.40f : 0.38f) :
                actionType == "special" ? 0.34f : 0.24f;
            _actionT = _actionDuration;
            _actionStrength = actionType == "ult" ? 0.24f : actionType == "special" ? 0.16f : 0.10f;
            _approachOffset = Vector3.zero;
            if (approach)
            {
                Vector3 delta = targetPosition.Value - transform.position;
                delta.z = 0f;
                float distance = delta.magnitude;
                if (distance > 0.9f)
                    _approachOffset = transform.InverseTransformVector(delta * ((distance - 0.9f) / distance));
            }
            if (actionType != "basic")
                BattleEffect.Spawn(this, "ring", actionType == "ult" ? new Color(1f, 0.79f, 0.20f) :
                    new Color(0.28f, 0.78f, 1f), actionType == "ult" ? 1.25f : 0.85f);
            return _approachOffset.sqrMagnitude > 0f ? _actionDuration * 0.35f : 0f;
        }

        /// <summary>有示意图时保持原色（死亡转灰）；无图时用队伍色块。</summary>
        public void SetAppearance(Color teamColor, bool dead)
        {
            if (_sr == null) return;
            if (dead && !_dead) { _deathAge = 0f; _actionT = 0f; }
            _dead = dead;
            _tint = dead ? new Color(0.35f, 0.35f, 0.35f, 0.65f) : _teamTint ? teamColor : Color.white;
            if (_flashT <= 0f) _sr.color = _tint;
        }

        public void SetFormationFeedback(bool lifted, bool target)
        {
            _formationLifted = lifted;
            _formationTarget = target;
            if (_formationSorting != null) _formationSorting.enabled = lifted;
            if (_formationHudRenderers != null)
                foreach (var renderer in _formationHudRenderers)
                    if (renderer != null) renderer.forceRenderingOff = target;
            if (_sr != null && _flashT <= 0f)
                _sr.color = target ? new Color(_tint.r, _tint.g, _tint.b, _tint.a * 0.4f) : _tint;
            if (_shadow != null)
            {
                _shadow.transform.localScale = lifted ? new Vector3(0.65f, 0.13f, 1f) : new Vector3(0.85f, 0.18f, 1f);
                _shadow.color = new Color(0.02f, 0.04f, 0.06f, lifted ? 0.12f : 0.22f);
            }
            UpdateMotion(0f);
        }

        public void SetFormationInteraction(bool dragging)
        {
            if (dragging) SetActionText("");
            foreach (var icon in _statusIcons) icon.SetInteractionSuppressed(dragging);
        }

        private void Update()
        {
            float dt = Time.deltaTime * PlaybackSpeed;

            if (_flashT > 0f && _sr != null)
            {
                _flashT -= dt;
                float k = Mathf.Clamp01(_flashT / FlashDur);
                _sr.color = Color.Lerp(_tint, _flashColor, k);
                if (_flashT <= 0f) _sr.color = _tint;
            }

            UpdateMotion(dt);

            // 低血脉冲：≤30% 时血条呼吸，残血一眼可见
            if (_hpFill != null)
            {
                if (_lowHp)
                {
                    float p = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(Time.time * 5f));
                    _hpFill.color = new Color(_hpColor.r * p, _hpColor.g * p, _hpColor.b * p, 1f);
                }
                else if (_hpFill.color != _hpColor)
                {
                    _hpFill.color = _hpColor;
                }
            }
        }

        private void UpdateMotion(float dt)
        {
            _shakeT = Mathf.Max(0f, _shakeT - dt);
            _actionT = Mathf.Max(0f, _actionT - dt);
            if (_sr == null) return;
            float progress = _actionDuration > 0f ? 1f - _actionT / _actionDuration : 1f;
            float punch = _actionT > 0f ? Mathf.Sin(progress * Mathf.PI) : 0f;
            float shake = _shakeT > 0f ? Mathf.Sin(Time.time * 70f) * ShakeAmp * (_shakeT / ShakeDur) : 0f;
            _deathAge = _dead ? Mathf.Min(0.45f, _deathAge + dt) : 0f;
            float fall = _dead ? _deathAge / 0.45f : 0f;
            float travel = progress < 0.35f ? Mathf.SmoothStep(0f, 1f, progress / 0.35f) :
                progress < 0.52f ? 1f : Mathf.SmoothStep(1f, 0f, (progress - 0.52f) / 0.48f);
            Vector3 offset = _approachOffset.sqrMagnitude > 0f ? _approachOffset * travel :
                new Vector3(_direction * punch * _actionStrength, 0f, 0f);
            _sr.transform.localPosition = offset + new Vector3(shake, -0.18f * fall, 0f);
            float formationScale = _formationLifted ? 1.08f : _formationTarget ? 1.035f : 1f;
            _sr.transform.localScale = _artScale * formationScale * (1f + punch * _actionStrength * 0.35f - fall * 0.12f);
            _sr.transform.localRotation = Quaternion.Euler(0f, 0f, -_direction * fall * 12f);
            _sr.sortingOrder = _actionT > 0f && _approachOffset.sqrMagnitude > 0f ? 25 : _idleSortOrder;
        }

        // ---------- 构建 ----------

        private void MakeBars()
        {
            MakeBar("HpBg", HpY, HpBgH, BarW, new Color(0.05f, 0.06f, 0.09f, 0.98f), 35);
            MakeBar("HpBorder", HpY, HpBgH + 0.06f, BarW + 0.06f, new Color(0.55f, 0.60f, 0.68f, 0.9f), 34);
            _hpFill = MakeBar("HpFill", HpY, HpFillH, BarW - BarInset, Color.white, 36);
            SetFill(_hpFill, 1f, BarW - BarInset);

            // 25/50/75% 刻度：读条不必数像素
            for (int i = 1; i <= 3; i++)
            {
                float x = -BarW * 0.5f + BarW * (i * 0.25f);
                MakeBar("HpTick" + i, HpY, HpBgH, 0.035f, new Color(0.02f, 0.02f, 0.03f, 0.85f), 37, x);
            }

            _hpText = TmpKit.MakeWorldText(transform, "HpText", new Vector3(0f, HpY, 0f),
                UiKit.FontBold, HpTextH, Color.white, 56, FontStyles.Normal, "0/0");
            TmpKit.SetOutline(_hpText, new Color(0f, 0f, 0f, 1f), 0.28f);

            MakeBar("UltBg", UltY, SubBgH, BarW, new Color(0.05f, 0.06f, 0.09f, 0.98f), 35);
            _ultFill = MakeBar("UltFill", UltY, SubFillH, BarW - BarInset, new Color(1f, 0.85f, 0.25f), 36);

            _stat = TmpKit.MakeWorldText(transform, "StatText", new Vector3(0f, UltY, 0f),
                UiKit.FontBold, StatTextH, Color.white, 56, FontStyles.Normal, "能量 0/0");
            TmpKit.SetOutline(_stat, new Color(0f, 0f, 0f, 0.9f), 0.2f);
        }

        private SpriteRenderer MakeBar(string goName, float y, float height, float width, Color color, int order, float x = 0f)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            go.transform.localScale = new Vector3(width, height, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = WhiteSprite();
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>从左端对齐的填充条：改宽度并保持左边贴齐。</summary>
        private static void SetFill(SpriteRenderer fill, float ratio, float fullW)
        {
            if (fill == null) return;
            ratio = Mathf.Clamp01(ratio);
            var t = fill.transform;
            t.localScale = new Vector3(fullW * ratio, t.localScale.y, 1f);
            var p = t.localPosition;
            p.x = -fullW * 0.5f + fullW * ratio * 0.5f;
            t.localPosition = p;
        }

        private static void FitSpriteHeight(Transform t, Sprite s, float targetHeight)
        {
            float h = s.rect.height / Mathf.Max(1f, s.pixelsPerUnit);
            float k = h > 0.0001f ? targetHeight / h : 1f;
            t.localScale = Vector3.one * Mathf.Clamp(k, 0.02f, 40f);
        }

        internal static Sprite WhiteSprite()
        {
            if (_white != null) return _white;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            _white = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            return _white;
        }
    }
}
