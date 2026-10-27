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
        public const int MaxHandSize = 3;

        private readonly Rng _rng;
        private readonly List<string> _drawPile = new List<string>();
        private readonly List<string> _hand = new List<string>();
        private readonly List<string> _discard = new List<string>();
        private readonly Queue<string> _tutorialDrawOrder;

        public IReadOnlyList<string> Hand => _hand;
        public int DrawPileCount => _drawPile.Count;
        public int DiscardPileCount => _discard.Count;
        public int RemovedCount { get; private set; }

        public IReadOnlyDictionary<string, int> DrawPileContents => CountCards(_drawPile);
        public IReadOnlyDictionary<string, int> DiscardPileContents => CountCards(_discard);

        private static IReadOnlyDictionary<string, int> CountCards(IEnumerable<string> cards)
        {
            var counts = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
            foreach (string id in cards)
                counts[id] = counts.TryGetValue(id, out int count) ? count + 1 : 1;
            return counts;
        }

        public Deck(Rng rng, IEnumerable<(string id, int count)> defs, IEnumerable<string> tutorialDrawOrder = null)
        {
            _rng = rng;
            _tutorialDrawOrder = new Queue<string>(tutorialDrawOrder ?? System.Array.Empty<string>());
            foreach (var (id, count) in defs)
                for (int i = 0; i < count; i++)
                    _drawPile.Add(id);
            Shuffle(_drawPile);
        }

        /// <summary>抽 n 张到手牌。抽牌堆空时先洗回弃牌；仍不足则停。</summary>
        public void Draw(int n)
        {
            for (int k = 0; k < n && _hand.Count < MaxHandSize; k++)
            {
                if (_drawPile.Count == 0)
                {
                    if (_discard.Count == 0) return;
                    _drawPile.AddRange(_discard);
                    _discard.Clear();
                    Shuffle(_drawPile);
                }
                int index = _drawPile.Count - 1;
                if (_tutorialDrawOrder.Count > 0)
                {
                    // 未生成的教学牌不补发，缺牌时仍按现有牌堆继续抽取。
                    int scheduled = _drawPile.IndexOf(_tutorialDrawOrder.Peek());
                    if (scheduled >= 0)
                    {
                        index = scheduled;
                        _tutorialDrawOrder.Dequeue();
                    }
                }
                string id = _drawPile[index];
                _drawPile.RemoveAt(index);
                _hand.Add(id);
            }
        }

        /// <summary>打出索引 handIndex 的牌。返回卡 id；索引非法返回 null。</summary>
        public string Play(int handIndex)
        {
            string id = TakeFromHand(handIndex);
            if (id != null) _discard.Add(id);
            return id;
        }

        public string Remove(int handIndex)
        {
            string id = TakeFromHand(handIndex);
            if (id != null) RemovedCount++;
            return id;
        }

        private string TakeFromHand(int handIndex)
        {
            if (handIndex < 0 || handIndex >= _hand.Count) return null;
            string id = _hand[handIndex];
            _hand.RemoveAt(handIndex);
            return id;
        }

        public void InsertIntoDrawPile(string cardId)
        {
            _drawPile.Insert(_rng.NextInt(_drawPile.Count + 1), cardId);
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