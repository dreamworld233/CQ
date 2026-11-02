using CQ.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CQ.Editor
{
    public static class BattleSceneMenu
    {
        [MenuItem("CQ/运行正式流程")]
        public static void PlayApplication()
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");
            EditorApplication.isPlaying = true;
        }

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

            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            // 独立临时场景，避免误把测试控制器保存进正式Bootstrap/菜单场景。
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("BattleController");
            go.AddComponent<BattleController>();
            Selection.activeGameObject = go;
            Debug.Log("[CQ.Editor] 已创建 BattleController GameObject。进入 Play 后可布阵并开战。");
        }
    }
}