using System;
using CQ.Core.Config;

namespace CQ.Core.Combat
{
    /// <summary>
    /// 单个敌人的意图循环游标。intentSequence 循环，
    /// CurrentIntent = intents 中 id == intentSequence[index] 的 IntentConfig。
    /// 回合开始暴露 CurrentIntent（亮意图）；结算后 Advance() 使 index 前进并回绕。
    /// </summary>
    public sealed class IntentModel
    {
        private readonly EnemyConfig _config;
        private int _index;

        public int Index => _index;

        public string CurrentIntentId => _config.intentSequence[_index];

        public IntentConfig CurrentIntent
        {
            get
            {
                string id = CurrentIntentId;
                if (_config.intents != null)
                    foreach (var it in _config.intents)
                        if (it.id == id) return it;
                return null;
            }
        }

        public IntentModel(EnemyConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _index = 0;
        }

        public void Advance()
        {
            var seq = _config.intentSequence;
            if (seq == null || seq.Length == 0) return;
            _index = (_index + 1) % seq.Length;
        }
    }
}