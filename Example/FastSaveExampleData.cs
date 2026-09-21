using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace Vtcong.Core.Example
{
    /// <summary>
    /// Khai báo toàn bộ dữ liệu có thể lưu của game mẫu.
    ///
    /// Mỗi field public khai báo ở đây chính là một key trong file save
    /// (key = <c>"example." + tên field</c>). Randomize, Save và Load đều duyệt
    /// đúng danh sách field này bằng reflection, nên **thêm một field mới vào đây
    /// là nó tự động được random, lưu và đọc lại** — không phải sửa thêm dòng code nào.
    ///
    /// Với Odin, class kế thừa <c>SerializedScriptableObject</c> nên Dictionary,
    /// DateTime, Guid, decimal... hiển thị và lưu được ngay trong asset.
    /// Không có Odin thì những kiểu đó vẫn chạy đúng lúc runtime, chỉ là
    /// Unity không lưu được chúng vào file .asset.
    /// </summary>
    [CreateAssetMenu(fileName = "FastSaveExampleData", menuName = "Vtcong/FastSave/Example Data")]
    public class FastSaveExampleData
    {
        public const string KeyPrefix = "example.";

        // =========================================================
        // SCHEMA - các trường có thể lưu
        // =========================================================

        [Header("Số nguyên & số thực")]
        public bool boolValue;
        public byte byteValue;
        public sbyte sbyteValue;
        public short shortValue;
        public ushort ushortValue;
        public int intValue;
        public uint uintValue;
        public long longValue;
        public ulong ulongValue;
        public float floatValue;
        public double doubleValue;
        public decimal decimalValue;
        public char charValue;
        public string stringValue;

        [Header("Kiểu Unity")]
        public Vector2 vector2Value;
        public Vector3 vector3Value;
        public Vector4 vector4Value;
        public Vector2Int vector2IntValue;
        public Vector3Int vector3IntValue;
        public Quaternion quaternionValue;
        public Color colorValue;
        public Color32 color32Value;
        public Rect rectValue;
        public Bounds boundsValue;

        [Header("Thời gian & định danh")]
        public DateTime dateTimeValue;
        public TimeSpan timeSpanValue;
        public DateTimeOffset dateTimeOffsetValue;
        public Guid guidValue;

        [Header("Enum")]
        public PlayerRank rankValue;
        public UnlockFlags unlockFlags;

        [Header("Collection")]
        public List<int> intList;
        public List<string> stringList;
        public List<Vector3> vector3List;
        public int[] intArray;
        public string[] stringArray;
        public byte[] byteArray;
        public List<List<int>> nestedIntList;
        public Dictionary<string, int> resources;
        public Dictionary<PlayerRank, string> rankNames;

        [Header("Class")]
        public InventoryItem singleItem;
        public List<InventoryItem> inventory;
        public GameSettings settings;
        public PlayerProfile profile;
        public CompactStats compactStats;

        // =========================================================
        // FIELD LIST
        // =========================================================
        public FastSaveExampleData()
        {
        }

        private static FieldInfo[] _fields;

        /// <summary>
        /// Danh sách field được lưu: chỉ các field public khai báo trực tiếp trong class này.
        /// Cố tình không duyệt base class để không đụng vào field nội bộ của Odin.
        /// </summary>
        public static IReadOnlyList<FieldInfo> SavableFields =>
            _fields ??= typeof(FastSaveExampleData).GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

        public static string KeyOf(FieldInfo field) => KeyPrefix + field.Name;

        // =========================================================
        // RANDOM / SAVE / LOAD
        // =========================================================

        /// <summary>Sinh giá trị ngẫu nhiên cho mọi field đã khai báo.</summary>
        public void Randomize()
        {
            foreach (var field in SavableFields)
                field.SetValue(this, FastSaveRandom.Value(field.FieldType));
            MarkAssetDirty();
        }

        /// <summary>Đưa mọi field vào FastSaveData. Chưa ghi xuống đĩa, gọi Save() sau.</summary>
        public int PushToSave()
        {
            foreach (var field in SavableFields)
                FastSaveData.Set(KeyOf(field), field.GetValue(this));
            return SavableFields.Count;
        }

        /// <summary>Đọc mọi field từ FastSaveData về lại asset này.</summary>
        public int PullFromSave()
        {
            var loaded = 0;
            foreach (var field in SavableFields)
            {
                var key = KeyOf(field);
                if (!FastSaveData.HasKey(key)) continue;
                field.SetValue(this, FastSaveData.Get(key, field.FieldType));
                loaded++;
            }

            MarkAssetDirty();
            return loaded;
        }

        /// <summary>Đưa mọi field về giá trị mặc định, để thấy rõ dữ liệu được load lại từ đĩa.</summary>
        public void ResetToDefault()
        {
            foreach (var field in SavableFields)
                field.SetValue(this,
                    field.FieldType.IsValueType ? Activator.CreateInstance(field.FieldType) : null);
            MarkAssetDirty();
        }

        /// <summary>Chụp lại giá trị hiện tại của mọi field để so sánh sau khi load.</summary>
        public Dictionary<string, object> Capture()
        {
            var snapshot = new Dictionary<string, object>(SavableFields.Count, StringComparer.Ordinal);
            foreach (var field in SavableFields) snapshot[KeyOf(field)] = field.GetValue(this);
            return snapshot;
        }

        /// <summary>Kiểm tra field nào chưa lưu được, gọi lúc setup để phát hiện sớm.</summary>
        public static List<string> FindUnsupportedFields()
        {
            var problems = new List<string>();
            foreach (var field in SavableFields)
                if (!FastSaveDataSerializer.IsSupported(field.FieldType, out var reason))
                    problems.Add($"{field.Name}: {reason}");
            return problems;
        }

        private void MarkAssetDirty()
        {
#if UNITY_EDITOR
            // UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public override string ToString() =>
            $"{stringValue} | int {intValue} | {rankValue} | {inventory?.Count ?? 0} item | " +
            $"{resources?.Count ?? 0} resource | profile {(profile == null ? "null" : profile.DisplayName)}";
    }
}
