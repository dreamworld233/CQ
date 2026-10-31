using System;
using System.Collections.Generic;
using CQ.Core.Config;

namespace CQ.Runtime
{
    public static class ConfigLoader
    {
        public static LoadResult<CharacterConfig> LoadCharacters() =>
            LoadCatalog<CharacterConfig>("Data/Characters", ConfigValidator.ValidateCharacter, item => item.id);

        public static LoadResult<EnemyConfig> LoadEnemies() =>
            LoadCatalog<EnemyConfig>("Data/Enemies", ConfigValidator.ValidateEnemy, item => item.id);

        public static LoadResult<LevelConfig> LoadLevels() =>
            LoadCatalog<LevelConfig>("Data/Levels", ConfigValidator.ValidateLevel, item => item.id.ToString());

        public static LoadResult<FormationConfig> LoadFormation()
        {
            var result = ConfigJson.LoadAllFromResources<FormationConfig>("Data/Formation", ConfigValidator.ValidateFormation);
            if (result.items.Count != 1)
                result.errors.Add("Data/Formation 必须包含且仅包含一份有效阵型配置，实际: " + result.items.Count);
            return result;
        }

        private static LoadResult<T> LoadCatalog<T>(string path, Func<T, string> validate, Func<T, string> idOf) where T : class
        {
            var result = ConfigJson.LoadAllFromResources(path, validate);
            var ids = new HashSet<string>();
            foreach (var item in result.items)
            {
                string id = idOf(item);
                if (!ids.Add(id)) result.errors.Add(path + " id 重复: " + id);
            }
            if (result.items.Count == 0) result.errors.Add(path + " 无有效配置");
            return result;
        }
    }
}
