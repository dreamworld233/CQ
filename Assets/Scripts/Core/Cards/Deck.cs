using System.Collections.Generic;
using CQ.Core.Battle;

namespace CQ.Core.Cards
{
    /// <summary>
    /// 卡组/手牌/弃牌（回合态）。构建时按 count 展开副本并洗牌；
    /// 抽牌不足时洗回弃牌，弃牌亦空则本轮不抽。全部走注入的 Rng，零硬编码数值。
    /// </summary>
    public sealed class Deck
    {
        private readonly Rng _rng;
        private readonly List<string> _drawPile = new List<string>();
        private readonly List<string> _hand = new List<string>();
        private readonly List<string> _discard = new List<string>();

        public IReadOnlyList<string> Hand => _hand;
        public int DrawPileCount => _drawPile.Count;
        public int DiscardPileCount => _discard.Count;

        public Deck(Rng rng, IEnumerable<(string id, int count)> defs)
        {
            _rng = rng;
            foreach (var (id, count) in defs)
                for (int i = 0; i < count; i++)
                    _drawPile.Add(id);
            Shuffle(_drawPile);
        }

        /// <summary>抽 n 张到手牌。抽牌堆空时先洗回弃牌；仍不足则停。</summary>
        public void Draw(int n)
        {
            for (int k = 0; k < n; k++)
            {
                if (_drawPile.Count == 0)
                {
                    if (_discard.Count == 0) return;
                    _drawPile.AddRange(_discard);
                    _discard.Clear();
                    Shuffle(_drawPile);
                }
                string id = _drawPile[_drawPile.Count - 1];
                _drawPile.RemoveAt(_drawPile.Count - 1);
                _hand.Add(id);
            }
        }

        /// <summary>打出索引 handIndex 的牌。返回卡 id；索引非法返回 null。</summary>
        public string Play(int handIndex)
        {
            if (handIndex < 0 || handIndex >= _hand.Count) return null;
            string id = _hand[handIndex];
            _hand.RemoveAt(handIndex);
            _discard.Add(id);
            return id;
        }

        private void Shuffle(IList<string> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.NextInt(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}