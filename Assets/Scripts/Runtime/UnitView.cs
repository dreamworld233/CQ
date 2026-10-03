using CQ.Core.Combat;
using TMPro;
using UnityEngine;

namespace CQ.Runtime
{
    /// <summary>
    /// 世界单位表现：示意图 sprite（缺图回退纯色块）+ 加粗血条/能量条/大招条 + 条上数值
    /// + 敌人意图图标 + 选中高亮方框 + 受击闪白/抖动。附 BoxCollider2D 供点选。
    /// 布局全部用世界单位常量；文字用「目标世界高度」定标（TmpKit.FitWorldHeight）。
    /// </summary>
    public sealed class UnitView : MonoBehaviour
    {
        private const float ArtHeight = 1.5f;      // 示意图目标世界高
        private const float IconHeight = 0.4f;     // 意图图标目标世界高
        private const float FrameHalf = 0.8f;      // 高亮框半宽
        private const float FrameFull = 1.72f;     // 高亮框边长
        private const float FrameThick = 0.1f;     // 高亮框杆粗
        private const float ColliderW = 1.4f;      // 点选碰撞体宽（须小于同侧间距 teamGap）
        private const float ColliderH = 1.7f;      // 点选碰撞体高

        // 数值条布局（世界单位）。BarW 须小于 teamGap(2.0)，否则相邻单位的条视觉粘连。
        private const float BarW = 1.9f;           // 条全宽
        private const float BarInset = 0.12f;      // 填充相对底框的左右内缩（形成描边观感）
        private const float HpY = -1.00f;          // 血条 y
        private const float HpBgH = 0.42f;         // 血条底框高
        private const float HpFillH = 0.30f;       // 血条填充高
        private const float EnergyY = -1.34f;      // 能量条 y（仅我方）
        private const float UltY = -1.52f;         // 大招条 y（仅我方）
        private const float SubBgH = 0.20f;        // 能量/大招底框高
        private const float SubFillH = 0.12f;      // 能量/大招填充高
        private const float LabelY = -1.80f;       // 名字 y
        private const float StatY = -2.06f;        // 能/大 数值 y（仅我方）
        private const float FloatY = 2.05f;        // 飘字起始 y

        private const float HpTextH = 0.30f;       // 血条上数值的目标世界高
        private const float LabelTextH = 0.28f;
        private const float StatTextH = 0.24f;
        private const float IntentTextH = 0.36f;

        private const float FlashDur = 0.28f;      // 受击闪白时长
        private const float ShakeDur = 0.22f;      // 受击抖动时长
        private const float ShakeAmp = 0.14f;      // 受击抖动幅度（世界单位）

        private static Sprite _white;

        private SpriteRenderer _sr;
        private SpriteRenderer _intentIcon;
        private TextMeshPro _label;
        private TextMeshPro _stat;
        private TextMeshPro _hpText;
        private TextMeshPro _intent;
        private Transform _frame;
        private readonly SpriteRenderer[] _frameBars = new SpriteRenderer[4];
        private bool _teamTint;

        private SpriteRenderer _hpFill;
        private SpriteRenderer _energyFill;
        private SpriteRenderer _ultFill;

        private Color _tint = Color.white;
        private Color _hpColor = Color.white;
        private bool _lowHp;
        private float _flashT;
        private float _shakeT;
        private Color _flashColor = Color.white;
        private Vector3 _home;

        public string UnitId { get; private set; }

        public void Setup(Unit unit, Color teamColor)
        {
            UnitId = unit.Id;
            _home = transform.position;

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
            _sr.sortingOrder = unit.Row == "front" ? 10 : 5;

            _intentIcon = MakeRenderer("IntentIcon", new Vector3(0f, 1.34f, 0f), 40);
            _intent = TmpKit.MakeWorldText(transform, "IntentText", new Vector3(0f, 1.02f, 0f),
                UiKit.FontBold, IntentTextH, Color.yellow, 55, FontStyles.Normal, "意");
            TmpKit.SetOutline(_intent, new Color(0f, 0f, 0f, 0.9f), 0.2f);

            MakeBars(unit.Team == Team.Player);
            _label = TmpKit.MakeWorldText(transform, "Label", new Vector3(0f, LabelY, 0f),
                UiKit.FontBold, LabelTextH, Color.white, 55, FontStyles.Normal, unit.Name);
            TmpKit.SetOutline(_label, new Color(0f, 0f, 0f, 0.9f), 0.2f);

            _frame = MakeFrame();

            var col = gameObject.AddComponent<BoxCollider2D>();
            col.size = new Vector2(ColliderW, ColliderH);

            Refresh(unit, "", "");
        }

        public void Refresh(Unit unit, string intentText, string intentType)
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

            if (unit.Team == Team.Player)
            {
                SetFill(_energyFill, unit.MaxEnergy > 0 ? (float)unit.Energy / unit.MaxEnergy : 0f, BarW - BarInset);
                SetFill(_ultFill, unit.MaxUlt > 0 ? (float)unit.Ult / unit.MaxUlt : 0f, BarW - BarInset);
            }

            if (_hpText != null)
            {
                _hpText.text = unit.Hp + "/" + unit.MaxHp;
                TmpKit.FitWorldHeight(_hpText, HpTextH);   // 位数变化后重新定标
            }

            if (_label != null)
                _label.text = unit.Name;

            if (_stat != null && unit.Team == Team.Player)
            {
                _stat.text = "能 " + unit.Energy + "/" + unit.MaxEnergy + "  大 " + unit.Ult + "/" + unit.MaxUlt;
                TmpKit.FitWorldHeight(_stat, StatTextH);
            }

            var icon = ArtLibrary.IntentSprite(intentType);
            if (_intentIcon != null)
            {
                _intentIcon.sprite = icon;
                if (icon != null) FitSpriteHeight(_intentIcon.transform, icon, IconHeight);
                _intentIcon.gameObject.SetActive(icon != null);
            }

            if (_intent != null)
            {
                _intent.text = intentText;
                _intent.gameObject.SetActive(!string.IsNullOrEmpty(intentText));
            }
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

        /// <summary>有示意图时保持原色（死亡转灰）；无图时用队伍色块。</summary>
        public void SetAppearance(Color teamColor, bool dead)
        {
            if (_sr == null) return;
            if (dead) { _tint = new Color(0.35f, 0.35f, 0.35f, 0.85f); _sr.color = _tint; return; }
            _tint = _teamTint ? teamColor : Color.white;
            if (_flashT <= 0f) _sr.color = _tint;
        }

        /// <summary>0=无框，1=选中（黄），2=可作目标（青）。</summary>
        public void SetHighlight(int mode)
        {
            if (_frame == null) return;
            if (mode == 0) { _frame.gameObject.SetActive(false); return; }
            _frame.gameObject.SetActive(true);
            var c = mode == 1 ? new Color(1f, 0.85f, 0.2f) : new Color(0.3f, 0.9f, 1f);
            foreach (var b in _frameBars) if (b != null) b.color = c;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_flashT > 0f && _sr != null)
            {
                _flashT -= dt;
                float k = Mathf.Clamp01(_flashT / FlashDur);
                _sr.color = Color.Lerp(_tint, _flashColor, k);
                if (_flashT <= 0f) _sr.color = _tint;
            }

            if (_shakeT > 0f)
            {
                _shakeT -= dt;
                float k = Mathf.Clamp01(_shakeT / ShakeDur);
                if (_shakeT <= 0f) transform.position = _home;
                else transform.position = _home + new Vector3(Mathf.Sin(Time.time * 70f) * ShakeAmp * k, 0f, 0f);
            }

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

        // ---------- 构建 ----------

        /// <summary>血条（双方，带底框/刻度/条上数值）+ 能量条/大招条（仅我方）。纯代码，不依赖素材。</summary>
        private void MakeBars(bool isPlayer)
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

            if (!isPlayer) return;

            MakeBar("EnergyBg", EnergyY, SubBgH, BarW, new Color(0.05f, 0.06f, 0.09f, 0.98f), 35);
            _energyFill = MakeBar("EnergyFill", EnergyY, SubFillH, BarW - BarInset, new Color(0.35f, 0.8f, 1f), 36);
            MakeBar("UltBg", UltY, SubBgH, BarW, new Color(0.05f, 0.06f, 0.09f, 0.98f), 35);
            _ultFill = MakeBar("UltFill", UltY, SubFillH, BarW - BarInset, new Color(1f, 0.85f, 0.25f), 36);

            _stat = TmpKit.MakeWorldText(transform, "StatText", new Vector3(0f, StatY, 0f),
                UiKit.FontBold, StatTextH, new Color(0.75f, 0.92f, 1f), 55, FontStyles.Normal, "能 0/0  大 0/0");
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

        private Transform MakeFrame()
        {
            var root = new GameObject("Highlight");
            root.transform.SetParent(transform, false);
            AddFrameBar(root.transform, 0, new Vector3(0f, FrameHalf, 0f), new Vector3(FrameFull, FrameThick, 1f));
            AddFrameBar(root.transform, 1, new Vector3(0f, -FrameHalf, 0f), new Vector3(FrameFull, FrameThick, 1f));
            AddFrameBar(root.transform, 2, new Vector3(-FrameHalf, 0f, 0f), new Vector3(FrameThick, FrameFull, 1f));
            AddFrameBar(root.transform, 3, new Vector3(FrameHalf, 0f, 0f), new Vector3(FrameThick, FrameFull, 1f));
            root.SetActive(false);
            return root.transform;
        }

        private void AddFrameBar(Transform parent, int index, Vector3 localPos, Vector3 scale)
        {
            var go = new GameObject("Bar" + index);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = WhiteSprite();
            sr.color = Color.yellow;
            sr.sortingOrder = 30;
            _frameBars[index] = sr;
        }

        private SpriteRenderer MakeRenderer(string goName, Vector3 localPos, int order)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            sr.color = Color.white;
            go.SetActive(false);
            return sr;
        }

        private static void FitSpriteHeight(Transform t, Sprite s, float targetHeight)
        {
            float h = s.rect.height / Mathf.Max(1f, s.pixelsPerUnit);
            float k = h > 0.0001f ? targetHeight / h : 1f;
            t.localScale = Vector3.one * Mathf.Clamp(k, 0.02f, 40f);
        }

        private static Sprite WhiteSprite()
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
