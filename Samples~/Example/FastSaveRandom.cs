using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Vtcong.Core.Example
{
    /// <summary>
    /// Sinh giá trị ngẫu nhiên cho bất kỳ kiểu nào mà FastSave lưu được.
    ///
    /// Dùng chung một quy ước field với FastSave (public, hoặc private có
    /// <c>[SerializeField]</c>/<c>[FastSaveInclude]</c>; bỏ qua <c>[FastSaveIgnore]</c>)
    /// để dữ liệu random luôn khớp đúng những gì sẽ được ghi xuống đĩa —
    /// nhờ vậy bước Verify Round Trip mới có ý nghĩa.
    /// </summary>
    public static class FastSaveRandom
    {
        private const int MaxDepth = 6;

        private static readonly string[] Words =
        {
            "rong", "bao", "sao", "gio", "lua", "bang", "sam", "may",
            "sword", "shield", "potion", "gem", "scroll", "boots", "ring", "bow"
        };

        // Dùng System.Random thay cho UnityEngine.Random để có thể seed cố định
        // (tái tạo lại đúng bộ dữ liệu khi cần debug một lỗi round trip).
        private static System.Random _rng = new();

        /// <summary>Đặt seed cố định để sinh lại đúng bộ dữ liệu cũ. Bỏ trống để random theo thời gian.</summary>
        public static void Seed(int? seed = null) => _rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

        /// <summary>Random mọi field lưu được của một object đã có sẵn.</summary>
        public static void Fill(object target, int depth = 0)
        {
            if (target == null) return;

            foreach (var field in GetSavableFields(target.GetType()))
                field.SetValue(target, Value(field.FieldType, depth + 1));
        }

        /// <summary>Sinh một giá trị ngẫu nhiên hợp lệ cho <paramref name="type"/>.</summary>
        public static object Value(Type type, int depth = 0)
        {
            if (type == null) return null;

            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null) return Value(underlying, depth);

            if (type == typeof(bool)) return _rng.Next(2) == 1;
            if (type == typeof(byte)) return (byte)_rng.Next(0, 256);
            if (type == typeof(sbyte)) return (sbyte)_rng.Next(-128, 128);
            if (type == typeof(short)) return (short)_rng.Next(short.MinValue, short.MaxValue);
            if (type == typeof(ushort)) return (ushort)_rng.Next(0, ushort.MaxValue);
            if (type == typeof(int)) return _rng.Next(-1000000, 1000000);
            if (type == typeof(uint)) return (uint)_rng.Next(0, int.MaxValue);
            if (type == typeof(long)) return (long)_rng.Next(-1000000, 1000000) * 1_000_003L;
            if (type == typeof(ulong)) return (ulong)_rng.Next(0, int.MaxValue) * 1_000_003UL;
            if (type == typeof(float)) return Rf();
            if (type == typeof(double)) return _rng.NextDouble() * 1e9d;
            if (type == typeof(decimal)) return Math.Round((decimal)(_rng.NextDouble() * 100000d), 4);
            if (type == typeof(char)) return (char)_rng.Next('A', 'Z' + 1);
            if (type == typeof(string)) return Word();

            if (type == typeof(DateTime))
                return new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(_rng.Next(0, 3_000_000));
            if (type == typeof(TimeSpan)) return TimeSpan.FromSeconds(_rng.Next(0, 5_000_000));
            if (type == typeof(DateTimeOffset))
                return new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(7))
                    .AddMinutes(_rng.Next(0, 3_000_000));
            if (type == typeof(Guid)) return Guid.NewGuid();

            if (type == typeof(Vector2)) return new Vector2(Rf(), Rf());
            if (type == typeof(Vector3)) return new Vector3(Rf(), Rf(), Rf());
            if (type == typeof(Vector4)) return new Vector4(Rf(), Rf(), Rf(), Rf());
            if (type == typeof(Vector2Int)) return new Vector2Int(_rng.Next(-100, 100), _rng.Next(-100, 100));
            if (type == typeof(Vector3Int))
                return new Vector3Int(_rng.Next(-100, 100), _rng.Next(-100, 100), _rng.Next(-100, 100));
            if (type == typeof(Quaternion)) return RandomRotation();
            if (type == typeof(Color)) return new Color(Rn(), Rn(), Rn(), 1f);
            if (type == typeof(Color32))
                return new Color32((byte)_rng.Next(0, 256), (byte)_rng.Next(0, 256), (byte)_rng.Next(0, 256), 255);
            if (type == typeof(Rect)) return new Rect(Rf(), Rf(), Math.Abs(Rf()), Math.Abs(Rf()));
            if (type == typeof(Bounds))
                return new Bounds(new Vector3(Rf(), Rf(), Rf()), new Vector3(Rf(), Rf(), Rf()) * 0.1f);

            if (type.IsEnum) return RandomEnum(type);
            if (type == typeof(byte[])) return RandomBytes(_rng.Next(8, 64));

            if (depth >= MaxDepth) return Default(type);

            if (type.IsArray) return RandomArray(type.GetElementType(), depth);

            if (type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition();
                if (definition == typeof(List<>)) return RandomList(type, depth);
                if (definition == typeof(Dictionary<,>)) return RandomDictionary(type, depth);
            }

            if (type.IsInterface || type.IsAbstract) return null;

            return RandomObject(type, depth);
        }

        // ---------------------------------------------------------
        // FIELDS
        // ---------------------------------------------------------

        /// <summary>
        /// Field mà FastSave sẽ ghi xuống đĩa. Đây là "schema" thực sự của một kiểu.
        /// </summary>
        public static List<FieldInfo> GetSavableFields(Type type)
        {
            var result = new List<FieldInfo>();
            var chain = new List<Type>();
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
                chain.Add(current);
            chain.Reverse();

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.DeclaredOnly;

            foreach (var current in chain)
            foreach (var field in current.GetFields(flags))
            {
                if (field.IsStatic || field.IsLiteral || field.IsInitOnly) continue;
                if (field.IsDefined(typeof(FastSaveIgnoreAttribute), true)) continue;
                if (field.IsDefined(typeof(NonSerializedAttribute), true)) continue;

                if (field.IsPublic ||
                    field.IsDefined(typeof(FastSaveIncludeAttribute), true) ||
                    field.IsDefined(typeof(SerializeField), true))
                    result.Add(field);
            }

            return result;
        }

        // ---------------------------------------------------------
        // INTERNALS
        // ---------------------------------------------------------

        private static object RandomObject(Type type, int depth)
        {
            object instance;
            try
            {
                instance = Activator.CreateInstance(type, true);
            }
            catch
            {
                return null;
            }

            foreach (var field in GetSavableFields(type))
                field.SetValue(instance, Value(field.FieldType, depth + 1));

            return instance;
        }

        private static object RandomList(Type listType, int depth)
        {
            var elementType = listType.GetGenericArguments()[0];
            var list = (IList)Activator.CreateInstance(listType);
            var size = _rng.Next(2, 7);
            for (var i = 0; i < size; i++) list.Add(Value(elementType, depth + 1));
            return list;
        }

        private static object RandomArray(Type elementType, int depth)
        {
            var size = _rng.Next(2, 7);
            var array = Array.CreateInstance(elementType, size);
            for (var i = 0; i < size; i++) array.SetValue(Value(elementType, depth + 1), i);
            return array;
        }

        private static object RandomDictionary(Type dictionaryType, int depth)
        {
            var arguments = dictionaryType.GetGenericArguments();
            var map = (IDictionary)Activator.CreateInstance(dictionaryType);

            // Key phải duy nhất: enum thì duyệt hết, còn lại thử tối đa vài lần.
            if (arguments[0].IsEnum)
            {
                foreach (var key in Enum.GetValues(arguments[0]))
                    map[key] = Value(arguments[1], depth + 1);
                return map;
            }

            var size = _rng.Next(2, 6);
            for (var i = 0; i < size * 3 && map.Count < size; i++)
            {
                var key = Value(arguments[0], depth + 1);
                if (key == null || map.Contains(key)) continue;
                map[key] = Value(arguments[1], depth + 1);
            }

            return map;
        }

        private static object RandomEnum(Type type)
        {
            var values = Enum.GetValues(type);
            return values.Length == 0 ? Activator.CreateInstance(type) : values.GetValue(_rng.Next(0, values.Length));
        }

        private static byte[] RandomBytes(int size)
        {
            var bytes = new byte[size];
            _rng.NextBytes(bytes);
            return bytes;
        }

        private static Quaternion RandomRotation()
        {
            // Quaternion đơn vị ngẫu nhiên, không gọi Random.rotation của Unity
            // để helper này chạy được cả ngoài Unity (unit test).
            var x = Rf();
            var y = Rf();
            var z = Rf();
            var w = Rf();
            var length = (float)Math.Sqrt(x * x + y * y + z * z + w * w);
            if (length < 0.0001f) return Quaternion.identity;
            return new Quaternion(x / length, y / length, z / length, w / length);
        }

        private static object Default(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;

        /// <summary>Số thực trong [-100, 100].</summary>
        private static float Rf() => (float)(_rng.NextDouble() * 200d - 100d);

        /// <summary>Số thực trong [0, 1].</summary>
        private static float Rn() => (float)_rng.NextDouble();

        private static string Word() =>
            $"{Words[_rng.Next(0, Words.Length)]}_{Words[_rng.Next(0, Words.Length)]}{_rng.Next(10, 9999)}";
    }
}
