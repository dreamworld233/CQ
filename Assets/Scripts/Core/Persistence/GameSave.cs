using System;
using System.Collections.Generic;
using System.Linq;
using CQ.Core.Combat;

namespace CQ.Core.Persistence
{
    [Serializable]
    public sealed class PartnerSave
    {
        public string id;
        public string slot;
        public bool deployed;
    }

    [Serializable]
    public sealed class GameSave
    {
        public const int CurrentVersion = 1;
        public const string Signature = "CQ.GameSave";
        public string signature;
        public int version;
        public string name;
        public long createdUtcTicks;
        public long savedUtcTicks;
        public long seed;
        public int levelId;
        public int[] completedLevels;
        public PartnerSave[] partners;

        public static GameSave Create(string name, int firstLevel, long seed, long utcTicks) => new GameSave
        {
            signature = Signature, version = CurrentVersion, name = name.Trim(),
            createdUtcTicks = utcTicks, savedUtcTicks = utcTicks, seed = seed, levelId = firstLevel,
            completedLevels = new int[0], partners = new PartnerSave[0]
        };

        public string Validate()
        {
            if (signature != Signature || version != CurrentVersion) return "存档格式或版本不受支持";
            if (string.IsNullOrWhiteSpace(name) || name.Length > 20 || name.Any(char.IsControl)) return "存档名称无效";
            if (createdUtcTicks <= 0 || savedUtcTicks < createdUtcTicks || savedUtcTicks > DateTime.MaxValue.Ticks)
                return "存档时间无效";
            if (levelId <= 0 || completedLevels == null || completedLevels.Any(id => id <= 0) ||
                completedLevels.Distinct().Count() != completedLevels.Length) return "关卡进度无效";
            if (partners == null || partners.Any(p => p == null || string.IsNullOrEmpty(p.id) || !Formation.IsValidSlot(p.slot)) ||
                partners.Select(p => p.id).Distinct().Count() != partners.Length) return "伙伴记录无效";
            var active = partners.Where(p => p.deployed).ToArray();
            if (active.Length > Formation.Capacity || active.Select(p => p.slot).Distinct().Count() != active.Length)
                return "上阵站位无效";
            return null;
        }

        public void CompleteLevel(int id)
        {
            if (!completedLevels.Contains(id)) completedLevels = completedLevels.Concat(new[] { id }).OrderBy(value => value).ToArray();
        }
    }

    [Serializable]
    public sealed class UserSettings
    {
        public const string Signature = "CQ.Settings";
        public string signature;
        public int version;
        public float masterVolume;
        public float effectsVolume;
        public bool muted;

        public static UserSettings Defaults() => new UserSettings
        {
            signature = Signature, version = 1, masterVolume = 1f, effectsVolume = 1f, muted = false
        };

        public string Validate() => signature != Signature || version != 1 ||
            float.IsNaN(masterVolume) || float.IsInfinity(masterVolume) || masterVolume < 0f || masterVolume > 1f ||
            float.IsNaN(effectsVolume) || float.IsInfinity(effectsVolume) || effectsVolume < 0f || effectsVolume > 1f
                ? "设置格式、版本或音量无效" : null;
    }
}
