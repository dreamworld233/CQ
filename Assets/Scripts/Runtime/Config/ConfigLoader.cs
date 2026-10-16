using CQ.Core.Config;

namespace CQ.Runtime
{
    /// <summary>
    /// 配置的统一入口。返回 LoadResult 同时带 items 与 errors。
    /// 全部走 Resources 同步加载，跨平台（含 Android 真机）安全。
    /// </summary>
    public static class ConfigLoader
    {
        public static LoadResult<CharacterConfig> LoadCharacters() =>
            ConfigJson.LoadAllFromResources<CharacterConfig>("Data/Characters", ValidateLoadedCharacter);

        public static string ValidateLoadedCharacter(CharacterConfig character)
        {
            // JsonUtility 会为缺省的内联引用创建空对象，须在进入 Core 前还原可选效果。
            if (character?.skills != null)
                foreach (var skill in character.skills)
                {
                    if (skill == null) continue;
                    var damage = skill.damage;
                    if (damage != null && string.IsNullOrEmpty(damage.kind) &&
                        string.IsNullOrEmpty(damage.target) && damage.amount == 0)
                        skill.damage = null;
                    var heal = skill.heal;
                    if (heal != null && string.IsNullOrEmpty(heal.target) && heal.amount == 0)
                        skill.heal = null;
                }
            return ConfigValidator.ValidateCharacter(character);
        }

        public static LoadResult<EnemyConfig> LoadEnemies() =>
            ConfigJson.LoadAllFromResources<EnemyConfig>("Data/Enemies", ConfigValidator.ValidateEnemy);

        public static LoadResult<CardConfig> LoadCards() =>
            ConfigJson.LoadAllFromResources<CardConfig>("Data/Cards", ConfigValidator.ValidateCard);

        public static LoadResult<StatusConfig> LoadStatuses() =>
            ConfigJson.LoadAllFromResources<StatusConfig>("Data/Statuses", ConfigValidator.ValidateStatus);

        public static LoadResult<LevelConfig> LoadLevels() =>
            ConfigJson.LoadAllFromResources<LevelConfig>("Data/Levels", ConfigValidator.ValidateLevel);

        public static LoadResult<FormationConfig> LoadFormation()
        {
            var result = ConfigJson.LoadAllFromResources<FormationConfig>("Data/Formation", ConfigValidator.ValidateFormation);
            if (result.items.Count != 1)
                result.errors.Add("Data/Formation 必须包含且仅包含一份有效阵型配置，实际: " + result.items.Count);
            return result;
        }
    }
}