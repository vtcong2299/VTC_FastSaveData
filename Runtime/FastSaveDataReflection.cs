using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Vtcong.Core
{
    /// <summary>
    /// Cache reflection cho đường serialize class tự động.
    /// Toàn bộ FieldInfo và Type đều được cache một lần cho mỗi kiểu, không quét lại ở mỗi lần save.
    /// Dùng FieldInfo thay vì expression tree để an toàn với IL2CPP/AOT (iOS, console).
    /// Nếu cần tốc độ tối đa cho class nóng, hãy implement <see cref="IFastSaveSerializable"/>.
    /// </summary>
    internal static class FastSaveDataReflection
    {
        internal sealed class FieldSlot
        {
            public uint NameHash;
            public FieldInfo Field;
            public Type FieldType;
        }

        internal sealed class TypeSlot
        {
            public Type Type;
            public FieldSlot[] Fields;
            public Dictionary<uint, FieldSlot> ByHash;
            public bool CanCreate;
        }

        private static readonly Dictionary<Type, TypeSlot> TypeCache = new();
        private static readonly Dictionary<Type, string> NameCache = new();
        private static readonly Dictionary<string, Type> ResolveCache = new(StringComparer.Ordinal);

        // ---------------------------------------------------------
        // TYPE NAME
        // ---------------------------------------------------------

        public static string GetTypeName(Type type)
        {
            lock (NameCache)
            {
                if (NameCache.TryGetValue(type, out var name)) return name;
                name = type.FullName + ", " + type.Assembly.GetName().Name;
                NameCache[type] = name;
                return name;
            }
        }

        public static Type ResolveType(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            lock (ResolveCache)
            {
                if (ResolveCache.TryGetValue(name, out var cached)) return cached;
            }

            var type = Type.GetType(name, false);
            if (type == null)
            {
                // Type đã bị đổi assembly hoặc namespace: thử quét theo FullName.
                var comma = name.IndexOf(',');
                var fullName = comma > 0 ? name.Substring(0, comma) : name;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = assembly.GetType(fullName, false);
                    if (type != null) break;
                }
            }

            lock (ResolveCache)
            {
                ResolveCache[name] = type;
            }

            return type;
        }

        // ---------------------------------------------------------
        // FIELDS
        // ---------------------------------------------------------

        public static TypeSlot GetTypeSlot(Type type)
        {
            lock (TypeCache)
            {
                if (TypeCache.TryGetValue(type, out var slot)) return slot;
            }

            var fields = new List<FieldSlot>();
            var byHash = new Dictionary<uint, FieldSlot>();

            // Duyệt từ base class xuống để field kế thừa luôn đứng trước, thứ tự ổn định giữa các build.
            var chain = new List<Type>();
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
                chain.Add(current);
            chain.Reverse();

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.DeclaredOnly;

            foreach (var current in chain)
            foreach (var field in current.GetFields(flags))
            {
                if (!ShouldSerialize(field)) continue;

                var hash = Hash(field.Name);
                if (byHash.ContainsKey(hash))
                {
                    Debug.LogError(
                        $"[FastSaveData] Trùng hash tên field '{field.Name}' trong {type.FullName}. Đổi tên một trong hai field.");
                    continue;
                }

                var entry = new FieldSlot { NameHash = hash, Field = field, FieldType = field.FieldType };
                fields.Add(entry);
                byHash[hash] = entry;
            }

            var result = new TypeSlot
            {
                Type = type,
                Fields = fields.ToArray(),
                ByHash = byHash,
                CanCreate = type.IsValueType || type.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null) != null
            };

            lock (TypeCache)
            {
                TypeCache[type] = result;
            }

            return result;
        }

        private static bool ShouldSerialize(FieldInfo field)
        {
            if (field.IsStatic || field.IsLiteral || field.IsInitOnly) return false;
            if (field.IsDefined(typeof(FastSaveIgnoreAttribute), true)) return false;
            if (field.IsDefined(typeof(NonSerializedAttribute), true)) return false;
            if (field.IsDefined(typeof(FastSaveIncludeAttribute), true)) return true;
            if (field.IsPublic) return true;
            return field.IsDefined(typeof(SerializeField), true);
        }

        public static object CreateInstance(Type type)
        {
            // Activator xử lý được cả struct lẫn class có ctor không tham số (kể cả private).
            return Activator.CreateInstance(type, true);
        }

        internal static uint Hash(string value)
        {
            unchecked
            {
                var hash = 2166136261u;
                for (var i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return hash;
            }
        }

        internal static void ClearCache()
        {
            lock (TypeCache) TypeCache.Clear();
            lock (NameCache) NameCache.Clear();
            lock (ResolveCache) ResolveCache.Clear();
        }
    }
}
