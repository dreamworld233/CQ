using System.IO;
using CQ.Core.Config;
using UnityEngine;

namespace CQ.Runtime
{
    /// <summary>
    /// 五类配置的统一入口。返回 LoadResult 同时带 items 与 errors。
    /// </summary>
    public static class ConfigLoader
    {
        public static string DataRoot => Path.Combine(Application.streamingAssetsPath, "Data");

        public static LoadResult<CharacterConfig> LoadCharacters() =>
            ConfigJson.LoadAll<CharacterConfig>(Path.Combine(DataRoot, "Characters"), ConfigValidator.ValidateCharacter);

        public static LoadResult<EnemyConfig> LoadEnemies() =>
            ConfigJson.LoadAll<EnemyConfig>(Path.Combine(DataRoot, "Enemies"), ConfigValidator.ValidateEnemy);

        public static LoadResult<CardConfig> LoadCards() =>
            ConfigJson.LoadAll<CardConfig>(Path.Combine(DataRoot, "Cards"), ConfigValidator.ValidateCard);

        public static LoadResult<StatusConfig> LoadStatuses() =>
            ConfigJson.LoadAll<StatusConfig>(Path.Combine(DataRoot, "Statuses"), ConfigValidator.ValidateStatus);

        public static LoadResult<LevelConfig> LoadLevels() =>
            ConfigJson.LoadAll<LevelConfig>(Path.Combine(DataRoot, "Levels"), ConfigValidator.ValidateLevel);
    }
}