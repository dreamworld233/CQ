using CQ.Core.Combat;
using UnityEngine;

namespace CQ.Runtime
{
    /// <summary>
    /// 世界单位占位表现：SpriteRenderer 色块 + TextMesh 名字/血量 + 意图标签（敌人）。
    /// 正式美术在 T11/T13，这里只给「地形可看 + 数据可读」。
    /// </summary>
    public sealed class UnitView : MonoBehaviour
    {
        private static Sprite _sprite;
        private SpriteRenderer _sr;
        private TextMesh _label;
        private TextMesh _intent;

        public string UnitId { get; private set; }

        public void Setup(Unit unit, Color color)
        {
            UnitId = unit.Id;

            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = WhiteSprite();
            _sr.color = color;
            _sr.sortingOrder = unit.Row == "front" ? 10 : 5;

            _label = MakeText("label", new Vector3(0f, 0.7f, 0f));
            _intent = MakeText("intent", new Vector3(0f, 1.05f, 0f));
            _intent.color = Color.yellow;

            Refresh(unit, "");
        }

        public void Refresh(Unit unit, string intentText)
        {
            if (_label != null)
            {
                _label.text = unit.Name + "\n" + unit.Hp + "/" + unit.MaxHp
                    + (unit.Team == Team.Player ? "\n能" + unit.Energy + " 大" + unit.Ult : "");
            }
            if (_intent != null)
            {
                _intent.text = intentText;
                _intent.gameObject.SetActive(intentText.Length > 0);
            }
        }

        public void SetColor(Color color)
        {
            if (_sr != null) _sr.color = color;
        }

        private TextMesh MakeText(string goName, Vector3 localPos)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.fontSize = 48;
            tm.characterSize = 0.06f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            return tm;
        }

        private static Sprite WhiteSprite()
        {
            if (_sprite != null) return _sprite;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            _sprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            return _sprite;
        }
    }
}