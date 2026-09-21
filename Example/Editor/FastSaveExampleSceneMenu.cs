#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vtcong.Core.Example
{
    /// <summary>
    /// Mở nhanh scene example mà không cần thêm nó vào Build Settings.
    /// </summary>
    public static class FastSaveExampleSceneMenu
    {
        private const string ScenePath =
            "Assets/VTC_Package/VTC_FastSave/Example/Scenes/FastSaveExample.unity";

        [MenuItem("Window/Fast Save Data/Open Example Scene", false, 1)]
        private static void OpenExampleScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (scene == null)
            {
                Debug.LogError($"[FastSaveData] Không tìm thấy scene example tại '{ScenePath}'.");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }
}
#endif
