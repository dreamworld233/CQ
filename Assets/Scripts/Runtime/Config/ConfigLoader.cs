using CQ.Core.Config;

namespace CQ.Runtime
{
    /// <summary>
    /// 五类配置的统一入口。返回 LoadResult 同时带 items 与 errors。
    /// 全部走 Resources 同步加载，跨平台（含 Android 真机）安全。
    /// </summary>
    public static class ConfigLoader
    {
        public static LoadResult<CharacterConfig> LoadCharacters() =>
            ConfigJson.LoadAllFromResources<CharacterConfig>("Data/Characters", ConfigValidator.ValidateCharacter);

        public static LoadResult<EnemyConfig> LoadEnemies() =>
            ConfigJson.LoadAllFromResources<EnemyConfig>("Data/Enemies", ConfigValidator.ValidateEnemy);

        public static LoadResult<CardConfig> LoadCards() =>
            ConfigJson.LoadAllFromResources<CardConfig>("Data/Cards", ConfigValidator.ValidateCard);

        public static LoadResult<StatusConfig> LoadStatuses() =>
            ConfigJson.LoadAllFromResources<StatusConfig>("Data/Statuses", ConfigValidator.ValidateStatus);

        public static LoadResult<LevelConfig> LoadLevels() =>
            ConfigJson.LoadAllFromResources<LevelConfig>("Data/Levels", ConfigValidator.ValidateLevel);
    }
}