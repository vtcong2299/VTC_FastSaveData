using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace Vtcong.Core.Example
{
    /// <summary>
    /// Scene test cho FastSaveData.
    ///
    /// Toàn bộ dữ liệu được khai báo trong ScriptableObject <see cref="FastSaveExampleData"/>.
    /// Script này chỉ là bộ điều khiển: random, lưu, đọc lại, so sánh.
    /// Thêm field mới vào ScriptableObject là tự động có mặt trong mọi bước, không sửa gì ở đây.
    ///
    /// Quy trình test:
    ///   1. Randomize Data   -> random mọi field khai báo trong asset
    ///   2. Save             -> đẩy từng field thành một key rồi ghi xuống đĩa
    ///   3. Reset &amp; Load    -> xoá trắng asset, đọc lại từ đĩa, dữ liệu phải hiện lại đủ
    ///   4. Verify Round Trip-> làm tự động cả 3 bước trên và so từng field
    /// </summary>
    [AddComponentMenu("Vtcong/FastSave Example")]
    public class FastSaveExample : SerializedMonoBehaviour
    {
        [Tooltip("ScriptableObject khai báo các trường dữ liệu sẽ được lưu.")]
        [SerializeField] private FastSaveExampleData data;

        [SerializeField] private int benchmarkEntryCount = 5000;

        [Tooltip("Hiện bảng điều khiển trên màn hình khi Play, để test được cả trên máy thật.")]
        [SerializeField] private bool showRuntimeGui = true;

        [ShowInInspector] [ReadOnly] public string FilePath => FastSaveData.FilePath;

        [ShowInInspector] [ReadOnly] public string FileSize => $"{FastSaveData.FileSize:N0} bytes";

        [ShowInInspector] [ReadOnly] public int EntryCount => FastSaveData.Count;

        [ShowInInspector] [ReadOnly] public bool HasUnsavedChanges => FastSaveData.IsDirty;

        [ShowInInspector] [ReadOnly]
        public string Summary => data == null ? "Chưa gán FastSaveExampleData" : data.ToString();

        private void Awake()
        {
            // Đổi sang FastSaveEncryption.Aes nếu cần mã hoá thật, hoặc None để debug bằng hex editor.
            FastSaveData.Encryption = FastSaveEncryption.Obfuscate;
            FastSaveData.AutoSave = true;
            FastSaveData.AutoSaveInterval = 0f;
            data = new FastSaveExampleData();

            if (data != null) data.PullFromSave();
        }

        private bool Ready()
        {
            if (data != null) return true;
            Debug.LogError("[Example] Chưa gán FastSaveExampleData vào field 'data'.");
            return false;
        }

        // =========================================================
        // CÁC BƯỚC CHÍNH
        // =========================================================

        [Button("1. Randomize Data")]
        [ButtonGroup("Flow")]
        public void RandomizeData()
        {
            if (!Ready()) return;

            data.Randomize();
            // Debug.Log($"[Example] Đã random {FastSaveExampleData.SavableFields.Count} field khai báo trong " +
            //           $"{data.name}.\n{data}");
        }

        [Button("2. Save")]
        [ButtonGroup("Flow")]
        public void SaveNow()
        {
            if (!Ready()) return;

            var watch = Stopwatch.StartNew();
            var count = data.PushToSave();
            FastSaveData.Save(true);
            watch.Stop();

            Debug.Log($"[Example] Lưu {count} field trong {watch.Elapsed.TotalMilliseconds:F2} ms " +
                      $"-> {FastSaveData.FileSize:N0} bytes.");
        }

        [Button("3. Reset & Load")]
        [ButtonGroup("Flow")]
        public void ResetAndLoad()
        {
            if (!Ready()) return;

            data.ResetToDefault();

            var watch = Stopwatch.StartNew();
            FastSaveData.Reload();
            var loaded = data.PullFromSave();
            watch.Stop();

            Debug.Log($"[Example] Xoá trắng asset rồi đọc lại {loaded} field từ đĩa trong " +
                      $"{watch.Elapsed.TotalMilliseconds:F2} ms.\n{data}");
        }

        [Button("4. Verify Round Trip")]
        [ButtonGroup("Flow")]
        public void VerifyRoundTrip()
        {
            if (!Ready()) return;

            var fields = FastSaveExampleData.SavableFields;
            var snapshot = data.Capture();

            data.PushToSave();
            FastSaveData.Save(true);

            data.ResetToDefault();
            FastSaveData.Reload();
            data.PullFromSave();

            var failed = 0;
            var report = new StringBuilder();

            foreach (var field in fields)
            {
                var key = FastSaveExampleData.KeyOf(field);
                var expected = snapshot[key];
                var actual = field.GetValue(data);

                if (DeepEquals(expected, actual)) continue;
                failed++;
                report.AppendLine($"  ✗ {field.Name} ({Pretty(field.FieldType)}): " +
                                  $"mong đợi '{Describe(expected)}', nhận '{Describe(actual)}'");
            }

            if (failed == 0)
                Debug.Log($"[Example] Round trip PASS: {fields.Count}/{fields.Count} field khớp, " +
                          $"file {FastSaveData.FileSize:N0} bytes.");
            else
                Debug.LogError($"[Example] Round trip FAIL: {failed}/{fields.Count} field sai.\n{report}");
        }

        // =========================================================
        // PHỤ
        // =========================================================

        [Button("Save Async")]
        [ButtonGroup("IO")]
        public async void SaveAsyncNow()
        {
            if (!Ready()) return;

            data.PushToSave();
            var watch = Stopwatch.StartNew();
            await FastSaveData.SaveAsync(true);
            watch.Stop();

            Debug.Log($"[Example] SaveAsync {watch.Elapsed.TotalMilliseconds:F2} ms " +
                      $"-> {FastSaveData.FileSize:N0} bytes.");
        }

        [Button("Clear Save")]
        [ButtonGroup("IO")]
        public void ClearSave()
        {
            FastSaveData.ClearData();
            Debug.Log("[Example] Đã xoá toàn bộ file save.");
        }

        [Button("Validate Schema")]
        [ButtonGroup("Debug")]
        public void ValidateSchema()
        {
            var problems = FastSaveExampleData.FindUnsupportedFields();
            if (problems.Count == 0)
                Debug.Log($"[Example] Cả {FastSaveExampleData.SavableFields.Count} field khai báo đều lưu được.");
            else
                Debug.LogError($"[Example] {problems.Count} field không lưu được:\n  " +
                               string.Join("\n  ", problems));
        }

        [Button("Benchmark")]
        [ButtonGroup("Debug")]
        public void Benchmark()
        {
            var sample = new Dictionary<string, object>(benchmarkEntryCount, StringComparer.Ordinal);
            for (var i = 0; i < benchmarkEntryCount; i++)
                sample["entry.key." + i] = (i % 4) switch
                {
                    0 => Random.Range(0, 1000),
                    1 => Random.Range(0f, 1000f),
                    2 => "value_" + Random.Range(0, 100),
                    _ => Random.insideUnitSphere
                };

            var watch = Stopwatch.StartNew();
            var packed = FastSaveDataSerializer.Serialize(sample);
            var serializeMs = watch.Elapsed.TotalMilliseconds;

            var copy = new byte[packed.Count];
            Buffer.BlockCopy(packed.Array!, packed.Offset, copy, 0, packed.Count);

            watch.Restart();
            var restored = FastSaveDataSerializer.Deserialize(copy);
            var deserializeMs = watch.Elapsed.TotalMilliseconds;

            watch.Restart();
            FastSaveData.Save(true);
            var saveMs = watch.Elapsed.TotalMilliseconds;

            Debug.Log(
                $"[Example] Benchmark {benchmarkEntryCount:N0} entry:" +
                $"\n  Serialize   {serializeMs:F2} ms -> {copy.Length:N0} bytes " +
                $"({copy.Length / (float)benchmarkEntryCount:F1} byte/entry)" +
                $"\n  Deserialize {deserializeMs:F2} ms -> {restored.Count:N0} entry" +
                $"\n  Save dữ liệu thật (gồm mã hoá + ghi đĩa) {saveMs:F2} ms -> {FastSaveData.FileSize:N0} bytes");
        }

        [Button("Dump To Console")]
        [ButtonGroup("Debug")]
        public void DumpToConsole()
        {
            var keys = FastSaveData.GetAllKeys();
            keys.Sort(StringComparer.Ordinal);

            var builder = new StringBuilder($"[Example] {keys.Count} entry, {FastSaveData.FileSize:N0} bytes\n");
            foreach (var key in keys)
            {
                var type = FastSaveData.GetValueType(key);
                builder.AppendLine($"  {key,-34} {Pretty(type),-28} {Describe(FastSaveData.Get<object>(key))}");
            }

            Debug.Log(builder.ToString());
        }

        [Button("Test Unsupported Type")]
        [ButtonGroup("Debug")]
        public void TestUnsupportedType()
        {
            // Kiểu không lưu được phải báo lỗi ngay tại Set, không được để nổ lúc Save
            // rồi làm hỏng cả file.
            Expect(() => FastSaveData.Set("demo.invalid", gameObject), "UnityEngine.Object");
            Expect(() => FastSaveData.Set("demo.invalid", new HashSet<int> { 1, 2, 3 }), "HashSet<int>");
            Expect(() => FastSaveData.Set("demo.invalid", data), "ScriptableObject");
        }

        private static void Expect(Action action, string label)
        {
            try
            {
                action();
                Debug.LogError($"[Example] '{label}' đáng lẽ phải bị chặn tại Set.");
            }
            catch (NotSupportedException e)
            {
                Debug.Log($"[Example] Chặn đúng '{label}': {e.Message}");
            }
        }

        // =========================================================
        // RUNTIME GUI
        // =========================================================

        private void OnGUI()
        {
            if (!showRuntimeGui) return;

            var scale = Mathf.Max(1f, Screen.height / 720f);
            GUIUtility.ScaleAroundPivot(Vector2.one * scale, Vector2.zero);

            using (new GUILayout.AreaScope(new Rect(10, 10, 440, Screen.height / scale - 20), string.Empty,
                       GUI.skin.box))
            {
                GUILayout.Label("<b>FastSave Example</b>", RichLabel());
                // GUILayout.Label($"Schema: {FastSaveExampleData.SavableFields.Count} field khai báo trong " +
                //                 $"{(data == null ? "(chưa gán asset)" : data.name)}");
                GUILayout.Label($"Entry: {FastSaveData.Count}    File: {FastSaveData.FileSize:N0} bytes" +
                                (FastSaveData.IsDirty ? "    [chưa lưu]" : string.Empty));
                GUILayout.Label(data == null ? string.Empty : data.ToString());

                GUILayout.Space(6);
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("1. Randomize")) RandomizeData();
                    if (GUILayout.Button("2. Save")) SaveNow();
                }

                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("3. Reset & Load")) ResetAndLoad();
                    if (GUILayout.Button("4. Verify Round Trip")) VerifyRoundTrip();
                }

                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Save Async")) SaveAsyncNow();
                    if (GUILayout.Button("Validate Schema")) ValidateSchema();
                }

                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Benchmark")) Benchmark();
                    if (GUILayout.Button("Dump")) DumpToConsole();
                }

                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Test Unsupported")) TestUnsupportedType();
                    if (GUILayout.Button("Clear Save")) ClearSave();
                }

                GUILayout.Space(6);
                GUILayout.Label("Kết quả chi tiết in ra Console.\n" +
                                "Test auto-save: bấm 1 rồi thoát Play mode mà KHÔNG bấm 2,\n" +
                                "Play lại và bấm 3 - dữ liệu phải còn nguyên.");
            }

            GUI.matrix = Matrix4x4.identity;
        }

        private static GUIStyle _richLabel;

        private static GUIStyle RichLabel() => _richLabel ??= new GUIStyle(GUI.skin.label) { richText = true };

        // =========================================================
        // HELPERS
        // =========================================================

        /// <summary>
        /// So sánh sâu bằng chính serializer: hai giá trị bằng nhau khi byte ghi ra giống hệt nhau.
        /// </summary>
        private static bool DeepEquals(object a, object b)
        {
            if (a == null || b == null) return a == null && b == null;

            var left = FastSaveDataSerializer.Serialize(Wrap(a));
            var leftCopy = new byte[left.Count];
            Buffer.BlockCopy(left.Array!, left.Offset, leftCopy, 0, left.Count);

            var right = FastSaveDataSerializer.Serialize(Wrap(b));
            if (leftCopy.Length != right.Count) return false;

            for (var i = 0; i < leftCopy.Length; i++)
                if (leftCopy[i] != right.Array![right.Offset + i])
                    return false;

            return true;
        }

        private static Dictionary<string, object> Wrap(object value) =>
            new(StringComparer.Ordinal) { ["v"] = value };

        private static string Pretty(Type type)
        {
            if (type == null) return "null";
            if (!type.IsGenericType) return type.Name;

            var arguments = type.GetGenericArguments();
            var names = new string[arguments.Length];
            for (var i = 0; i < arguments.Length; i++) names[i] = Pretty(arguments[i]);
            return $"{type.Name.Split('`')[0]}<{string.Join(", ", names)}>";
        }

        private static string Describe(object value)
        {
            switch (value)
            {
                case null: return "null";
                case string text: return text.Length > 40 ? text.Substring(0, 40) + "…" : text;
                case byte[] bytes: return $"byte[{bytes.Length}]";
                case System.Collections.IDictionary map: return $"Dictionary({map.Count})";
                case System.Collections.IList list: return $"List({list.Count})";
                default: return value.ToString();
            }
        }
    }
}
