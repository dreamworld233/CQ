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
            var cards = ConfigLoader.LoadCards();
            var statuses = ConfigLoader.LoadStatuses();
            var levels = ConfigLoader.LoadLevels();

            int cardCopies = 0;
            foreach (var c in cards.items) cardCopies += c.count;

            Debug.Log($"[CQ.Config] 角色 {characters.items.Count} / 卡 {cards.items.Count} 定义、{cardCopies} 张 / 敌 {enemies.items.Count} / 状态 {statuses.items.Count} / 关卡 {levels.items.Count}");

            DumpErrors("角色", characters);
            DumpErrors("敌人", enemies);
            DumpErrors("卡", cards);
            DumpErrors("状态", statuses);
            DumpErrors("关卡", levels);
        }

        private static void DumpErrors<T>(string label, LoadResult<T> result)
        {
            foreach (var error in result.errors)
                Debug.LogError($"[CQ.Config][{label}] {error}");
        }
    }
}