using CQ.Core.Config;
using CQ.Runtime;
using UnityEditor;
using UnityEngine;

namespace CQ.Editor
{
    public static class ConfigInspectMenu
    {
        [MenuItem("CQ/打印配置计数")]
        public static void DumpConfigCounts()
        {
            var characters = ConfigLoader.LoadCharacters();
            var enemies = ConfigLoader.LoadEnemies();
            var levels = ConfigLoader.LoadLevels();
            var formation = ConfigLoader.LoadFormation();
            Debug.Log($"[CQ.Config] 角色 {characters.items.Count} / 敌人 {enemies.items.Count} / 关卡 {levels.items.Count} / 阵型 {formation.items.Count}");
            DumpErrors("角色", characters);
            DumpErrors("敌人", enemies);
            DumpErrors("关卡", levels);
            DumpErrors("阵型", formation);
        }

        private static void DumpErrors<T>(string label, LoadResult<T> result)
        {
            foreach (var error in result.errors)
                Debug.LogError($"[CQ.Config][{label}] {error}");
        }
    }
}
