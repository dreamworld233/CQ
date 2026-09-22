using CQ.Runtime;
using UnityEditor;
using UnityEngine;

namespace CQ.Editor
{
    public static class BattleSceneMenu
    {
        [MenuItem("CQ/创建战斗测试场景")]
        public static void CreateBattleObject()
        {
            var existing = Object.FindObjectOfType<BattleController>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("[CQ.Editor] 已存在 BattleController，已选中。");
                return;
            }

            var go = new GameObject("BattleController");
            go.AddComponent<BattleController>();
            Selection.activeGameObject = go;
            Debug.Log("[CQ.Editor] 已创建 BattleController GameObject。运行后 IMGUI 即可交互。");
        }
    }
}