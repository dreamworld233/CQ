using CQ.Core.Combat;
using UnityEngine;

namespace CQ.Runtime
{
    /// <summary>
    /// 世界单位表现：示意图 sprite（缺图回退纯色块）+ 名字/血量 + 敌人意图图标 + 选中高亮方框。
    /// 附 BoxCollider2D 供点选。
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
        private const float HpY = -0.95f;          // 血条 y
        private const float EnergyY = -1.19f;      // 能量条 y（仅我方）
        private const float UltY = -1.32f;         // 大招条 y（仅我方）
        private const float LabelY = -1.62f;       // 名字/数值 y
        private const float FloatY = 1.95f;        // 飘字起始 y

        private static Sprite _white;

        private SpriteRenderer _sr;
        private SpriteRenderer _intentIcon;
        private TextMesh _label;
        private TextMesh _intent;
        private Transform _frame;
        private readonly SpriteRenderer[] _bars = new SpriteRenderer[4];
        private bool _teamTint;

        // 数值条：血条（双方）+ 能量条/大招条（我方）
        private SpriteRenderer _hpFill;
        private SpriteRenderer _energyFill;
        private SpriteRenderer _ultFill;

        public string UnitId { get; private set; }

        public void Setup(Unit unit, Color teamColor)
        {
            UnitId = unit.Id;

            var art = ArtLibrary.UnitSprite(unit.Id);
            _teamTint = art == null;

            var artGo = new GameObject("Art");
            artGo.transform.SetParent(transform, false);
            _sr = artGo.AddComponent<SpriteRenderer>();
            if (art != null)
            {
                _sr.sprite = art;
                _sr.color = Color.white;
                FitHeight(artGo.transform, art, ArtHeight);
            }
            else
            {
                _sr.sprite = WhiteSprite();
                _sr.color = teamColor;
            }
            _sr.sortingOrder = unit.Row == "front" ? 10 : 5;

            _intentIcon = MakeRenderer("IntentIcon", new Vector3(0f, 1.34f, 0f), 40);
            _intent = MakeText("IntentText", new Vector3(0f, 1.0f, 0f));
            _intent.color = Color.yellow;

            MakeBars(unit.Team == Team.Player);
            _label = MakeText("Label", new Vector3(0f, LabelY, 0f));

            _frame = MakeFrame();

            var col = gameObject.AddComponent<BoxCollider2D>();
            col.size = new Vector2(ColliderW, ColliderH);

            Refresh(unit, "", "");
        }

        public void Refresh(Unit unit, string intentText, string intentType)
        {
            float hpRatio = unit.MaxHp > 0 ? (float)unit.Hp / unit.MaxHp : 0f;
            SetFill(_hpFill, hpRatio, BarW);
            if (_hpFill != null)
                _hpFill.color = hpRatio <= 0.3f
                    ? new Color(0.95f, 0.35f, 0.25f)
                    : (unit.Team == Team.Player ? new Color(0.35f, 0.9f, 0.45f) : new Color(0.95f, 0.4f, 0.35f));

            if (unit.Team == Team.Player)
            {
                SetFill(_energyFill, unit.MaxEnergy > 0 ? (float)unit.Energy / unit.MaxEnergy : 0f, BarW);
                SetFill(_ultFill, unit.MaxUlt > 0 ? (float)unit.Ult / unit.MaxUlt : 0f, BarW);
            }

            if (_label != null)
            {
                _label.text = unit.Name + "\n" + unit.Hp + "/" + unit.MaxHp
                    + (unit.Team == Team.Player ? "\n能 " + unit.Energy + "/" + unit.MaxEnergy
                        + "  大 " + unit.Ult + "/" + unit.MaxUlt : "");
            }

            var icon = ArtLibrary.IntentSprite(intentType);
            if (_intentIcon != null)
            {
                _intentIcon.sprite = icon;
                if (icon != null) FitHeight(_intentIcon.transform, icon, IconHeight);
                _intentIcon.gameObject.SetActive(icon != null);
            }

            if (_intent != null)
            {
                _intent.text = intentText;
                _intent.gameObject.SetActive(!string.IsNullOrEmpty(intentText));
            }
        }

        /// <summary>飘字（伤害/治疗数值），1 秒后自动销毁。</summary>
        public void FloatText(string text, Color color)
        {
            FloatingText.Spawn(transform, text, color, FloatY);
        }

        /// <summary>有示意图时保持原色（死亡转灰）；无图时用队伍色块。</summary>
        public void SetAppearance(Color teamColor, bool dead)
        {
            if (_sr == null) return;
            if (dead) { _sr.color = new Color(0.35f, 0.35f, 0.35f, 0.85f); return; }
            _sr.color = _teamTint ? teamColor : Color.white;
        }

        /// <summary>0=无框，1=选中（黄），2=可作目标（青）。</summary>
        public void SetHighlight(int mode)
        {
            if (_frame == null) return;
            if (mode == 0) { _frame.gameObject.SetActive(false); return; }
            _frame.gameObject.SetActive(true);
            var c = mode == 1 ? new Color(1f, 0.85f, 0.2f) : new Color(0.3f, 0.9f, 1f);
            foreach (var b in _bars) if (b != null) b.color = c;
        }

        // ---------- 构建 ----------

        private const float BarW = 1.5f;

        /// <summary>血条（双方）+ 能量条/大招条（仅我方）。纯代码，不依赖素材。</summary>
        private void MakeBars(bool isPlayer)
        {
            MakeBar("HpBg", HpY, 0.2f, new Color(0.08f, 0.09f, 0.12f, 0.95f), 35);
            _hpFill = MakeBar("HpFill", HpY, 0.16f, Color.white, 36);
            SetFill(_hpFill, 1f, BarW);

            if (!isPlayer) return;

            MakeBar("EnergyBg", EnergyY, 0.1f, new Color(0.08f, 0.09f, 0.12f, 0.95f), 35);
            _energyFill = MakeBar("EnergyFill", EnergyY, 0.07f, new Color(0.35f, 0.8f, 1f), 36);
            MakeBar("UltBg", UltY, 0.1f, new Color(0.08f, 0.09f, 0.12f, 0.95f), 35);
            _ultFill = MakeBar("UltFill", UltY, 0.07f, new Color(1f, 0.85f, 0.25f), 36);
        }

        private SpriteRenderer MakeBar(string goName, float y, float height, Color color, int order)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.transform.localScale = new Vector3(BarW, height, 1f);
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
            AddBar(root.transform, 0, new Vector3(0f, FrameHalf, 0f), new Vector3(FrameFull, FrameThick, 1f));
            AddBar(root.transform, 1, new Vector3(0f, -FrameHalf, 0f), new Vector3(FrameFull, FrameThick, 1f));
            AddBar(root.transform, 2, new Vector3(-FrameHalf, 0f, 0f), new Vector3(FrameThick, FrameFull, 1f));
            AddBar(root.transform, 3, new Vector3(FrameHalf, 0f, 0f), new Vector3(FrameThick, FrameFull, 1f));
            root.SetActive(false);
            return root.transform;
        }

        private void AddBar(Transform parent, int index, Vector3 localPos, Vector3 scale)
        {
            var go = new GameObject("Bar" + index);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = WhiteSprite();
            sr.color = Color.yellow;
            sr.sortingOrder = 30;
            _bars[index] = sr;
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

        private TextMesh MakeText(string goName, Vector3 localPos)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = UiKit.Font;          // CJK：默认 Arial 无中文字形
            tm.fontSize = 48;
            tm.characterSize = 0.06f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 50;   // 盖在世界 sprite 之上
            return tm;
        }

        private static void FitHeight(Transform t, Sprite s, float targetHeight)
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
