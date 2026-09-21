#if UNITY_EDITOR && !ODIN_INSPECTOR
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Vtcong.Core.Example
{
    /// <summary>
    /// Inspector dự phòng khi project chưa cài Odin: vẽ nút cho mọi method có [Button].
    /// Khi có Odin, file này không được biên dịch và Odin lo phần hiển thị.
    /// </summary>
    [CustomEditor(typeof(FastSaveExample))]
    public sealed class FastSaveExampleEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var target = (FastSaveExample)base.target;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Trạng thái", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("File", FastSaveData.FilePath, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Dung lượng", FastSaveData.FileSize.ToString("N0") + " bytes");
            EditorGUILayout.LabelField("Số entry", FastSaveData.Count.ToString());
            EditorGUILayout.LabelField("Chưa lưu", FastSaveData.IsDirty ? "có" : "không");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Hành động", EditorStyles.boldLabel);

            var methods = typeof(FastSaveExample).GetMethods(BindingFlags.Instance | BindingFlags.Public);
            foreach (var method in methods)
            {
                if (method.GetParameters().Length > 0) continue;
                if (!method.IsDefined(typeof(ButtonAttribute), true)) continue;

                if (!GUILayout.Button(ObjectNames.NicifyVariableName(method.Name))) continue;
                method.Invoke(target, null);
                Repaint();
            }
        }
    }
}
#endif
