using System;
using System.Collections.Generic;
using System.Linq;
using CQ.Core.Config;
using CQ.Core.Persistence;
using CQ.Runtime.Persistence;
using UnityEngine;

namespace CQ.Runtime.App
{
    public sealed class GameSession
    {
        private readonly LocalSaveStore _store;
        public int Slot { get; }
        public GameSave Data { get; private set; }

        public GameSession(LocalSaveStore store, int slot, GameSave data)
        { _store = store; Slot = slot; Data = data ?? throw new ArgumentNullException(nameof(data)); }

        public static string ValidateContent(GameSave save, IList<LevelConfig> levels, IList<CharacterConfig> characters)
        {
            string error = save.Validate();
            if (error != null) return error;
            var level = levels.FirstOrDefault(item => item.id == save.levelId);
            if (level == null || save.completedLevels.Any(id => !levels.Any(item => item.id == id))) return "存档关卡在当前内容中不存在";
            var available = new HashSet<string>(level.players ?? characters.Select(character => character.id).ToArray());
            if (save.partners.Any(partner => !available.Contains(partner.id))) return "存档伙伴在当前关卡不可用";
            return null;
        }

        public bool SaveCheckpoint(int levelId, PartnerSave[] partners, int? completedLevel, out string error)
        {
            // 先写副本，失败不把未落盘的进度伪装为已保存。
            var next = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(Data));
            next.levelId = levelId;
            next.partners = partners;
            next.savedUtcTicks = Math.Max(next.createdUtcTicks, DateTime.UtcNow.Ticks);
            if (completedLevel.HasValue) next.CompleteLevel(completedLevel.Value);
            if (!_store.WriteSlot(Slot, next, false, out error)) return false;
            Data = next;
            return true;
        }
    }
}
