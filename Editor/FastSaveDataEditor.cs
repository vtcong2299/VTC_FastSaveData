#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vtcong.Core
{
    public static class FastSaveDataEditor
    {
        [MenuItem("Window/Fast Save Data/Inspector", false, 0)]
        private static void OpenInspector() => FastSaveDataWindow.Open();

        [MenuItem("Window/Fast Save Data/Open Save Folder", false, 20)]
        private static void OpenFolder()
        {
            var folder = Path.GetDirectoryName(FastSaveData.FilePath);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder);
        }

        [MenuItem("Window/Fast Save Data/Clear Data", false, 21)]
        private static void ClearData()
        {
            if (!EditorUtility.DisplayDialog("Clear FastSaveData",
                    "Xoá toàn bộ file save và reset dữ liệu local?", "Clear", "Cancel")) return;
            FastSaveData.ClearData();
            Debug.Log("[FastSaveData] Đã xoá save data.");
            FastSaveDataWindow.RepaintIfOpen();
        }
    }

    /// <summary>
    /// Cửa sổ xem nhanh nội dung file save: key, kiểu, giá trị, dung lượng.
    /// </summary>
    public sealed class FastSaveDataWindow : EditorWindow
    {
        private Vector2 _scroll;
        private string _filter = string.Empty;

        public static void Open() => GetWindow<FastSaveDataWindow>("FastSaveData").Show();

        public static void RepaintIfOpen()
        {
            if (HasOpenInstances<FastSaveDataWindow>()) GetWindow<FastSaveDataWindow>().Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawSummary();
            DrawEntries();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    FastSaveData.Reload();

                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    FastSaveData.Save(true);

                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(60)) &&
                    EditorUtility.DisplayDialog("Clear FastSaveData", "Xoá toàn bộ save?", "Clear", "Cancel"))
                    FastSaveData.ClearData();

                GUILayout.FlexibleSpace();
                _filter = GUILayout.TextField(_filter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
            }
        }

        private static void DrawSummary()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("File", FastSaveData.FilePath, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Dung lượng", $"{FastSaveData.FileSize:N0} bytes   |   {FastSaveData.Count} entry" +
                                                     (FastSaveData.IsDirty ? "   |   chưa lưu" : string.Empty));
            EditorGUILayout.Space(4);
        }

        private void DrawEntries()
        {
            using var scope = new EditorGUILayout.ScrollViewScope(_scroll);
            _scroll = scope.scrollPosition;

            var keys = FastSaveData.GetAllKeys();
            keys.Sort(StringComparer.Ordinal);

            foreach (var key in keys)
            {
                if (!string.IsNullOrEmpty(_filter) &&
                    key.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                var type = FastSaveData.GetValueType(key);
                using (new EditorGUILayout.HorizontalScope("box"))
                {
                    EditorGUILayout.LabelField(key, EditorStyles.boldLabel, GUILayout.Width(160));
                    EditorGUILayout.LabelField(type == null ? "null" : Pretty(type), GUILayout.Width(150));
                    EditorGUILayout.LabelField(Preview(FastSaveData.Get<object>(key)));
                    if (GUILayout.Button("X", GUILayout.Width(22))) FastSaveData.DeleteKey(key);
                }
            }

            if (keys.Count == 0) EditorGUILayout.HelpBox("Chưa có dữ liệu.", MessageType.Info);
        }

        private static string Pretty(Type type)
        {
            if (!type.IsGenericType) return type.Name;
            var arguments = type.GetGenericArguments();
            var names = new string[arguments.Length];
            for (var i = 0; i < arguments.Length; i++) names[i] = Pretty(arguments[i]);
            return $"{type.Name.Split('`')[0]}<{string.Join(", ", names)}>";
        }

        private static string Preview(object value)
        {
            switch (value)
            {
                case null: return "null";
                case string text: return text.Length > 64 ? text.Substring(0, 64) + "…" : text;
                case byte[] bytes: return $"byte[{bytes.Length}]";
                case IDictionary map: return $"{map.Count} cặp";
                case IList list:
                {
                    var items = new List<string>();
                    for (var i = 0; i < list.Count && i < 6; i++) items.Add(list[i]?.ToString() ?? "null");
                    return $"[{string.Join(", ", items)}{(list.Count > 6 ? ", …" : string.Empty)}] ({list.Count})";
                }
                default:
                    return value.GetType().IsPrimitive || value is ValueType
                        ? value.ToString()
                        : JsonPreview(value);
            }
        }

        private static string JsonPreview(object value)
        {
            try
            {
                var json = JsonUtility.ToJson(value);
                return string.IsNullOrEmpty(json) || json == "{}" ? value.ToString() : json;
            }
            catch
            {
                return value.ToString();
            }
        }
    }
}
#endif
