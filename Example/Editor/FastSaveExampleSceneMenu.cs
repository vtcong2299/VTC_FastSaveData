#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vtcong.Core.Example
{
    /// <summary>
    /// Mở nhanh scene example mà không cần thêm nó vào Build Settings.
    ///
    /// Tra đường dẫn theo GUID chứ không hardcode: package có thể nằm ở
    /// Assets/... (khi dev) hoặc Packages/com.vtcong.fastsave/... (khi cài qua UPM),
    /// hardcode đường dẫn thì chỉ đúng được một trong hai.
    /// </summary>
    public static class FastSaveExampleSceneMenu
    {
        private const string SceneGuid = "3c7a1f5e9b2d4a1e8f6c0d5b7e93a241";
        private const string SceneName = "FastSaveExample";

        [MenuItem("Window/Fast Save Data/Open Example Scene", false, 1)]
        private static void OpenExampleScene()
        {
            var path = ResolveScenePath();
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError(
                    $"[FastSaveData] Không tìm thấy scene '{SceneName}.unity'. " +
                    "Nếu bạn cài package qua Package Manager, hãy kiểm tra thư mục Example có được import không.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        private static string ResolveScenePath()
        {
            // Ưu tiên GUID: đúng dù package nằm trong Assets hay trong Packages.
            var path = AssetDatabase.GUIDToAssetPath(SceneGuid);
            if (!string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                return path;

            // Dự phòng khi GUID đổi (ví dụ ai đó copy scene ra chỗ khác).
            foreach (var guid in AssetDatabase.FindAssets($"{SceneName} t:SceneAsset"))
            {
                var candidate = AssetDatabase.GUIDToAssetPath(guid);
                if (candidate.EndsWith($"/{SceneName}.unity")) return candidate;
            }

            return null;
        }
    }
}
#endif
