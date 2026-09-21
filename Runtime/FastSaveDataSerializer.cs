using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Vtcong.Core
{
    /// <summary>
    /// Serializer nhị phân của FastSaveData.
    ///
    /// Format v2:
    ///   [0..3]   "FSDL"
    ///   [4..7]   int32 version
    ///   [8..11]  int32 offset của bảng string (tính từ đầu payload)
    ///   [12..]   varint số entry, rồi từng entry: string-id của key + giá trị có tag
    ///   [offset] varint số string, rồi từng string UTF8
    ///
    /// Tối ưu dung lượng: số nguyên dùng varint zig-zag, mọi chuỗi (kể cả key và tên type)
    /// dùng chung một bảng nên chỉ lưu đúng một lần, collection có kiểu phần tử cố định
    /// được ghi "packed" (không tốn 1 byte tag cho từng phần tử).
    ///
    /// File v1 vẫn đọc được để không mất save của người chơi khi nâng cấp.
    /// </summary>
    public static class FastSaveDataSerializer
    {
        public const int Version = 2;
        internal const int MaxCollectionSize = 1_000_000;

        private static readonly byte[] Magic = { (byte)'F', (byte)'S', (byte)'D', (byte)'L' };
        private static readonly Dictionary<Type, string> SupportCache = new();

        // =========================================================
        // SERIALIZE
        // =========================================================

        /// <summary>
        /// Serialize toàn bộ entry. <paramref name="reserve"/> byte đầu buffer được chừa trống
        /// để lớp crypto ghi header tại chỗ, tránh phải copy lại mảng.
        /// </summary>
        public static ArraySegment<byte> Serialize(IReadOnlyDictionary<string, object> entries, int reserve = 0)
        {
            var stream = new MemoryStream(Mathf.Max(256, entries.Count * 24 + reserve));
            if (reserve > 0)
            {
                stream.SetLength(reserve);
                stream.Position = reserve;
            }

            using var raw = new BinaryWriter(stream, Encoding.UTF8, true);
            var writer = new FastSaveWriter(raw);

            raw.Write(Magic);
            raw.Write(Version);
            raw.Write(0); // chỗ trống cho offset bảng string

            writer.WriteVarULong((ulong)entries.Count);
            foreach (var pair in entries)
            {
                writer.WriteString(pair.Key);
                WriteValue(writer, pair.Value);
            }

            raw.Flush();
            var tableOffset = (int)(stream.Position - reserve);

            var strings = writer.Strings;
            writer.WriteVarULong((ulong)strings.Count);
            for (var i = 0; i < strings.Count; i++) raw.Write(strings[i]);
            raw.Flush();

            var end = (int)stream.Position;

            stream.Position = reserve + 8;
            raw.Write(tableOffset);
            raw.Flush();

            return new ArraySegment<byte>(stream.GetBuffer(), reserve, end - reserve);
        }

        // =========================================================
        // DESERIALIZE
        // =========================================================

        public static Dictionary<string, object> Deserialize(byte[] data)
        {
            if (data == null || data.Length < 8) throw new InvalidDataException("FastSaveData: file rỗng hoặc hỏng.");

            using var stream = new MemoryStream(data, 0, data.Length, false, true);
            using var raw = new BinaryReader(stream, Encoding.UTF8, true);

            if (raw.ReadByte() != Magic[0] || raw.ReadByte() != Magic[1] ||
                raw.ReadByte() != Magic[2] || raw.ReadByte() != Magic[3])
                throw new InvalidDataException("FastSaveData: sai magic header.");

            var version = raw.ReadInt32();
            return version switch
            {
                1 => DeserializeV1(raw),
                2 => DeserializeV2(stream, raw),
                _ => throw new InvalidDataException(
                    $"FastSaveData: file được ghi bằng version {version}, bản hiện tại chỉ đọc tới {Version}.")
            };
        }

        private static Dictionary<string, object> DeserializeV2(MemoryStream stream, BinaryReader raw)
        {
            var tableOffset = raw.ReadInt32();
            if (tableOffset < 12 || tableOffset > stream.Length)
                throw new InvalidDataException("FastSaveData: offset bảng string không hợp lệ.");

            var reader = new FastSaveReader(raw);

            stream.Position = tableOffset;
            var stringCount = reader.ReadCount();
            var table = new string[stringCount];
            for (var i = 0; i < stringCount; i++) table[i] = raw.ReadString();
            reader.StringTable = table;

            stream.Position = 12;
            var count = reader.ReadCount();
            var result = new Dictionary<string, object>(count, StringComparer.Ordinal);
            for (var i = 0; i < count; i++)
            {
                var key = reader.ReadString();
                var value = ReadValue(reader);
                if (!string.IsNullOrEmpty(key)) result[key] = value;
            }

            return result;
        }

        // =========================================================
        // WRITE
        // =========================================================

        internal static void WriteValue(FastSaveWriter writer, object value)
        {
            if (value == null)
            {
                writer.Raw.Write(FastSaveTag.Null);
                return;
            }

            switch (value)
            {
                case bool v:
                    writer.Raw.Write(FastSaveTag.Bool);
                    writer.WriteBool(v);
                    return;
                case int v:
                    writer.Raw.Write(FastSaveTag.Int);
                    writer.WriteInt(v);
                    return;
                case long v:
                    writer.Raw.Write(FastSaveTag.Long);
                    writer.WriteLong(v);
                    return;
                case float v:
                    writer.Raw.Write(FastSaveTag.Float);
                    writer.WriteFloat(v);
                    return;
                case double v:
                    writer.Raw.Write(FastSaveTag.Double);
                    writer.WriteDouble(v);
                    return;
                case string v:
                    writer.Raw.Write(FastSaveTag.String);
                    writer.WriteString(v);
                    return;
                case byte v:
                    writer.Raw.Write(FastSaveTag.Byte);
                    writer.WriteByte(v);
                    return;
                case short v:
                    writer.Raw.Write(FastSaveTag.Short);
                    writer.WriteShort(v);
                    return;
                case uint v:
                    writer.Raw.Write(FastSaveTag.UInt);
                    writer.WriteUInt(v);
                    return;
                case ulong v:
                    writer.Raw.Write(FastSaveTag.ULong);
                    writer.WriteULong(v);
                    return;
                case ushort v:
                    writer.Raw.Write(FastSaveTag.UShort);
                    writer.WriteUShort(v);
                    return;
                case sbyte v:
                    writer.Raw.Write(FastSaveTag.SByte);
                    writer.WriteSByte(v);
                    return;
                case decimal v:
                    writer.Raw.Write(FastSaveTag.Decimal);
                    writer.WriteDecimal(v);
                    return;
                case char v:
                    writer.Raw.Write(FastSaveTag.Char);
                    writer.WriteChar(v);
                    return;
                case DateTime v:
                    writer.Raw.Write(FastSaveTag.DateTime);
                    writer.WriteDateTime(v);
                    return;
                case TimeSpan v:
                    writer.Raw.Write(FastSaveTag.TimeSpan);
                    writer.WriteTimeSpan(v);
                    return;
                case DateTimeOffset v:
                    writer.Raw.Write(FastSaveTag.DateTimeOffset);
                    writer.Raw.Write(v.Ticks);
                    writer.Raw.Write(v.Offset.Ticks);
                    return;
                case Guid v:
                    writer.Raw.Write(FastSaveTag.Guid);
                    writer.WriteGuid(v);
                    return;
                case Vector2 v:
                    writer.Raw.Write(FastSaveTag.Vector2);
                    writer.WriteVector2(v);
                    return;
                case Vector3 v:
                    writer.Raw.Write(FastSaveTag.Vector3);
                    writer.WriteVector3(v);
                    return;
                case Vector4 v:
                    writer.Raw.Write(FastSaveTag.Vector4);
                    writer.WriteVector4(v);
                    return;
                case Quaternion v:
                    writer.Raw.Write(FastSaveTag.Quaternion);
                    writer.WriteQuaternion(v);
                    return;
                case Color v:
                    writer.Raw.Write(FastSaveTag.Color);
                    writer.WriteColor(v);
                    return;
                case Color32 v:
                    writer.Raw.Write(FastSaveTag.Color32);
                    writer.WriteColor32(v);
                    return;
                case Vector2Int v:
                    writer.Raw.Write(FastSaveTag.Vector2Int);
                    writer.WriteVector2Int(v);
                    return;
                case Vector3Int v:
                    writer.Raw.Write(FastSaveTag.Vector3Int);
                    writer.WriteVector3Int(v);
                    return;
                case Rect v:
                    writer.Raw.Write(FastSaveTag.Rect);
                    writer.Raw.Write(v.x);
                    writer.Raw.Write(v.y);
                    writer.Raw.Write(v.width);
                    writer.Raw.Write(v.height);
                    return;
                case Bounds v:
                    writer.Raw.Write(FastSaveTag.Bounds);
                    writer.WriteVector3(v.center);
                    writer.WriteVector3(v.size);
                    return;
                case byte[] v:
                    writer.Raw.Write(FastSaveTag.ByteArray);
                    writer.WriteBytes(v);
                    return;
            }

            if (writer.Depth >= FastSaveWriter.MaxDepth)
                throw new InvalidOperationException(
                    $"FastSaveData: lồng quá {FastSaveWriter.MaxDepth} cấp. Kiểm tra tham chiếu vòng trong '{value.GetType().FullName}'.");

            writer.Depth++;
            try
            {
                WriteComplex(writer, value);
            }
            finally
            {
                writer.Depth--;
            }
        }

        private static void WriteComplex(FastSaveWriter writer, object value)
        {
            var type = value.GetType();

            if (type.IsEnum)
            {
                writer.Raw.Write(FastSaveTag.Enum);
                writer.WriteString(FastSaveDataReflection.GetTypeName(type));
                writer.WriteLong(Convert.ToInt64(value));
                return;
            }

            if (value is IFastSaveSerializable custom)
            {
                WriteCustom(writer, type, custom);
                return;
            }

            if (type.IsArray)
            {
                if (type.GetArrayRank() != 1)
                    throw new NotSupportedException("FastSaveData: chỉ hỗ trợ mảng một chiều.");

                var array = (Array)value;
                var elementType = type.GetElementType();
                writer.Raw.Write(FastSaveTag.TypedArray);
                writer.WriteString(FastSaveDataReflection.GetTypeName(elementType));
                WriteElements(writer, array, array.Length, elementType);
                return;
            }

            if (type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition();

                if (definition == typeof(List<>))
                {
                    var list = (IList)value;
                    var elementType = type.GetGenericArguments()[0];
                    writer.Raw.Write(FastSaveTag.TypedList);
                    writer.WriteString(FastSaveDataReflection.GetTypeName(elementType));
                    WriteElements(writer, list, list.Count, elementType);
                    return;
                }

                if (definition == typeof(Dictionary<,>))
                {
                    var map = (IDictionary)value;
                    var arguments = type.GetGenericArguments();
                    writer.Raw.Write(FastSaveTag.TypedDictionary);
                    writer.WriteString(FastSaveDataReflection.GetTypeName(arguments[0]));
                    writer.WriteString(FastSaveDataReflection.GetTypeName(arguments[1]));
                    writer.WriteVarULong((ulong)map.Count);
                    var enumerator = map.GetEnumerator();
                    while (enumerator.MoveNext())
                    {
                        WriteValue(writer, enumerator.Key);
                        WriteValue(writer, enumerator.Value);
                    }

                    return;
                }
            }

            if (value is IDictionary untypedMap)
            {
                writer.Raw.Write(FastSaveTag.ObjectDictionary);
                writer.WriteVarULong((ulong)untypedMap.Count);
                var enumerator = untypedMap.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    WriteValue(writer, enumerator.Key);
                    WriteValue(writer, enumerator.Value);
                }

                return;
            }

            if (value is IList untypedList)
            {
                writer.Raw.Write(FastSaveTag.ObjectList);
                writer.WriteVarULong((ulong)untypedList.Count);
                for (var i = 0; i < untypedList.Count; i++) WriteValue(writer, untypedList[i]);
                return;
            }

            if (value is IEnumerable)
                throw new NotSupportedException(
                    $"FastSaveData: '{type.FullName}' là collection chưa hỗ trợ. Dùng List<T>, T[] hoặc Dictionary<K,V>.");

            WriteReflected(writer, type, value);
        }

        private static void WriteCustom(FastSaveWriter writer, Type type, IFastSaveSerializable value)
        {
            writer.Raw.Write(FastSaveTag.Custom);
            writer.WriteString(FastSaveDataReflection.GetTypeName(type));

            // Ghi sẵn độ dài để lúc đọc có thể bỏ qua khối này nếu type đã bị xoá khỏi game.
            writer.Raw.Flush();
            var stream = writer.Raw.BaseStream;
            var lengthPosition = stream.Position;
            writer.Raw.Write(0);

            value.FastSaveWrite(writer);

            writer.Raw.Flush();
            var end = stream.Position;
            stream.Position = lengthPosition;
            writer.Raw.Write((int)(end - lengthPosition - 4));
            writer.Raw.Flush();
            stream.Position = end;
        }

        private static void WriteReflected(FastSaveWriter writer, Type type, object value)
        {
            var slot = FastSaveDataReflection.GetTypeSlot(type);
            writer.Raw.Write(FastSaveTag.Reflected);
            writer.WriteString(FastSaveDataReflection.GetTypeName(type));
            writer.WriteVarULong((ulong)slot.Fields.Length);

            for (var i = 0; i < slot.Fields.Length; i++)
            {
                var field = slot.Fields[i];
                writer.Raw.Write(field.NameHash);
                WriteValue(writer, field.Field.GetValue(value));
            }
        }

        private static void WriteElements(FastSaveWriter writer, IList items, int count, Type elementType)
        {
            var packed = TryGetPrimitiveTag(elementType, out var tag);
            writer.Raw.Write(packed ? tag : (byte)0);
            writer.WriteVarULong((ulong)count);

            if (packed)
                for (var i = 0; i < count; i++)
                    WritePrimitive(writer, tag, items[i]);
            else
                for (var i = 0; i < count; i++)
                    WriteValue(writer, items[i]);
        }

        // =========================================================
        // READ
        // =========================================================

        internal static object ReadValue(FastSaveReader reader)
        {
            var tag = reader.Raw.ReadByte();
            switch (tag)
            {
                case FastSaveTag.Null: return null;
                case FastSaveTag.ObjectList:
                {
                    var count = reader.ReadCount();
                    var list = new List<object>(count);
                    for (var i = 0; i < count; i++) list.Add(ReadValue(reader));
                    return list;
                }
                case FastSaveTag.ObjectArray:
                {
                    var count = reader.ReadCount();
                    var array = new object[count];
                    for (var i = 0; i < count; i++) array[i] = ReadValue(reader);
                    return array;
                }
                case FastSaveTag.ObjectDictionary:
                {
                    var count = reader.ReadCount();
                    var map = new Dictionary<object, object>(count);
                    for (var i = 0; i < count; i++)
                    {
                        var key = ReadValue(reader);
                        var value = ReadValue(reader);
                        if (key != null) map[key] = value;
                    }

                    return map;
                }
                case FastSaveTag.Enum:
                {
                    var typeName = reader.ReadString();
                    var raw = reader.ReadLong();
                    var type = FastSaveDataReflection.ResolveType(typeName);
                    return type is { IsEnum: true } ? Enum.ToObject(type, raw) : raw;
                }
                case FastSaveTag.Custom: return ReadCustom(reader);
                case FastSaveTag.Reflected: return ReadReflected(reader);
                case FastSaveTag.TypedList: return ReadTypedList(reader);
                case FastSaveTag.TypedArray: return ReadTypedArray(reader);
                case FastSaveTag.TypedDictionary: return ReadTypedDictionary(reader);
                default: return ReadPrimitive(reader, tag);
            }
        }

        private static object ReadCustom(FastSaveReader reader)
        {
            var typeName = reader.ReadString();
            var length = reader.Raw.ReadInt32();
            var stream = reader.Raw.BaseStream;
            var start = stream.Position;

            if (length < 0 || start + length > stream.Length)
                throw new InvalidDataException("FastSaveData: độ dài khối custom không hợp lệ.");

            var type = FastSaveDataReflection.ResolveType(typeName);
            object result = null;

            if (type != null && typeof(IFastSaveSerializable).IsAssignableFrom(type))
            {
                try
                {
                    var instance = (IFastSaveSerializable)FastSaveDataReflection.CreateInstance(type);
                    instance.FastSaveRead(reader);
                    result = instance;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[FastSaveData] Không đọc được '{typeName}': {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"[FastSaveData] Bỏ qua type không còn tồn tại: '{typeName}'.");
            }

            // Luôn nhảy tới đúng cuối khối, kể cả khi FastSaveRead của người dùng đọc thiếu/thừa.
            stream.Position = start + length;
            return result;
        }

        private static object ReadReflected(FastSaveReader reader)
        {
            var typeName = reader.ReadString();
            var fieldCount = reader.ReadCount();
            var type = FastSaveDataReflection.ResolveType(typeName);

            if (type == null)
            {
                for (var i = 0; i < fieldCount; i++)
                {
                    reader.Raw.ReadUInt32();
                    ReadValue(reader);
                }

                Debug.LogWarning($"[FastSaveData] Bỏ qua type không còn tồn tại: '{typeName}'.");
                return null;
            }

            var slot = FastSaveDataReflection.GetTypeSlot(type);
            if (!slot.CanCreate)
            {
                for (var i = 0; i < fieldCount; i++)
                {
                    reader.Raw.ReadUInt32();
                    ReadValue(reader);
                }

                Debug.LogError($"[FastSaveData] '{type.FullName}' thiếu constructor không tham số.");
                return null;
            }

            var instance = FastSaveDataReflection.CreateInstance(type);
            for (var i = 0; i < fieldCount; i++)
            {
                var hash = reader.Raw.ReadUInt32();
                var value = ReadValue(reader);

                // Field bị xoá hoặc đổi tên: giá trị đã được đọc hết nên stream vẫn đúng vị trí.
                if (!slot.ByHash.TryGetValue(hash, out var field)) continue;

                var converted = ConvertTo(value, field.FieldType);
                if (converted != null || !field.FieldType.IsValueType) field.Field.SetValue(instance, converted);
            }

            return instance;
        }

        private static object ReadTypedList(FastSaveReader reader)
        {
            var elementType = FastSaveDataReflection.ResolveType(reader.ReadString()) ?? typeof(object);
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
            ReadElements(reader, elementType, (index, value) => list.Add(value));
            return list;
        }

        private static object ReadTypedArray(FastSaveReader reader)
        {
            var elementType = FastSaveDataReflection.ResolveType(reader.ReadString()) ?? typeof(object);
            Array array = null;
            ReadElements(reader, elementType,
                (index, value) => array.SetValue(value, index),
                count => array = Array.CreateInstance(elementType, count));
            return array;
        }

        private static object ReadTypedDictionary(FastSaveReader reader)
        {
            var keyType = FastSaveDataReflection.ResolveType(reader.ReadString()) ?? typeof(object);
            var valueType = FastSaveDataReflection.ResolveType(reader.ReadString()) ?? typeof(object);
            var count = reader.ReadCount();
            var map = (IDictionary)Activator.CreateInstance(
                typeof(Dictionary<,>).MakeGenericType(keyType, valueType), count);

            for (var i = 0; i < count; i++)
            {
                var key = ConvertTo(ReadValue(reader), keyType);
                var value = ConvertTo(ReadValue(reader), valueType);
                if (key != null) map[key] = value;
            }

            return map;
        }

        private static void ReadElements(FastSaveReader reader, Type elementType, Action<int, object> setter,
            Action<int> allocate = null)
        {
            var tag = reader.Raw.ReadByte();
            var count = reader.ReadCount();
            allocate?.Invoke(count);

            for (var i = 0; i < count; i++)
            {
                var value = tag == 0 ? ReadValue(reader) : ReadPrimitive(reader, tag);
                setter(i, ConvertTo(value, elementType));
            }
        }

        // =========================================================
        // PRIMITIVE HELPERS
        // =========================================================

        private static bool TryGetPrimitiveTag(Type type, out byte tag)
        {
            tag = 0;
            if (type == typeof(bool)) tag = FastSaveTag.Bool;
            else if (type == typeof(int)) tag = FastSaveTag.Int;
            else if (type == typeof(long)) tag = FastSaveTag.Long;
            else if (type == typeof(float)) tag = FastSaveTag.Float;
            else if (type == typeof(double)) tag = FastSaveTag.Double;
            else if (type == typeof(string)) tag = FastSaveTag.String;
            else if (type == typeof(byte)) tag = FastSaveTag.Byte;
            else if (type == typeof(short)) tag = FastSaveTag.Short;
            else if (type == typeof(uint)) tag = FastSaveTag.UInt;
            else if (type == typeof(ulong)) tag = FastSaveTag.ULong;
            else if (type == typeof(ushort)) tag = FastSaveTag.UShort;
            else if (type == typeof(sbyte)) tag = FastSaveTag.SByte;
            else if (type == typeof(decimal)) tag = FastSaveTag.Decimal;
            else if (type == typeof(char)) tag = FastSaveTag.Char;
            else if (type == typeof(DateTime)) tag = FastSaveTag.DateTime;
            else if (type == typeof(TimeSpan)) tag = FastSaveTag.TimeSpan;
            else if (type == typeof(DateTimeOffset)) tag = FastSaveTag.DateTimeOffset;
            else if (type == typeof(Guid)) tag = FastSaveTag.Guid;
            else if (type == typeof(Vector2)) tag = FastSaveTag.Vector2;
            else if (type == typeof(Vector3)) tag = FastSaveTag.Vector3;
            else if (type == typeof(Vector4)) tag = FastSaveTag.Vector4;
            else if (type == typeof(Quaternion)) tag = FastSaveTag.Quaternion;
            else if (type == typeof(Color)) tag = FastSaveTag.Color;
            else if (type == typeof(Color32)) tag = FastSaveTag.Color32;
            else if (type == typeof(Vector2Int)) tag = FastSaveTag.Vector2Int;
            else if (type == typeof(Vector3Int)) tag = FastSaveTag.Vector3Int;
            else if (type == typeof(Rect)) tag = FastSaveTag.Rect;
            else if (type == typeof(Bounds)) tag = FastSaveTag.Bounds;
            return tag != 0;
        }

        private static void WritePrimitive(FastSaveWriter writer, byte tag, object value)
        {
            switch (tag)
            {
                case FastSaveTag.Bool:
                    writer.WriteBool((bool)value);
                    return;
                case FastSaveTag.Int:
                    writer.WriteInt((int)value);
                    return;
                case FastSaveTag.Long:
                    writer.WriteLong((long)value);
                    return;
                case FastSaveTag.Float:
                    writer.WriteFloat((float)value);
                    return;
                case FastSaveTag.Double:
                    writer.WriteDouble((double)value);
                    return;
                case FastSaveTag.String:
                    writer.WriteString((string)value);
                    return;
                case FastSaveTag.Byte:
                    writer.WriteByte((byte)value);
                    return;
                case FastSaveTag.Short:
                    writer.WriteShort((short)value);
                    return;
                case FastSaveTag.UInt:
                    writer.WriteUInt((uint)value);
                    return;
                case FastSaveTag.ULong:
                    writer.WriteULong((ulong)value);
                    return;
                case FastSaveTag.UShort:
                    writer.WriteUShort((ushort)value);
                    return;
                case FastSaveTag.SByte:
                    writer.WriteSByte((sbyte)value);
                    return;
                case FastSaveTag.Decimal:
                    writer.WriteDecimal((decimal)value);
                    return;
                case FastSaveTag.Char:
                    writer.WriteChar((char)value);
                    return;
                case FastSaveTag.DateTime:
                    writer.WriteDateTime((DateTime)value);
                    return;
                case FastSaveTag.TimeSpan:
                    writer.WriteTimeSpan((TimeSpan)value);
                    return;
                case FastSaveTag.DateTimeOffset:
                    var offset = (DateTimeOffset)value;
                    writer.Raw.Write(offset.Ticks);
                    writer.Raw.Write(offset.Offset.Ticks);
                    return;
                case FastSaveTag.Guid:
                    writer.WriteGuid((Guid)value);
                    return;
                case FastSaveTag.Vector2:
                    writer.WriteVector2((Vector2)value);
                    return;
                case FastSaveTag.Vector3:
                    writer.WriteVector3((Vector3)value);
                    return;
                case FastSaveTag.Vector4:
                    writer.WriteVector4((Vector4)value);
                    return;
                case FastSaveTag.Quaternion:
                    writer.WriteQuaternion((Quaternion)value);
                    return;
                case FastSaveTag.Color:
                    writer.WriteColor((Color)value);
                    return;
                case FastSaveTag.Color32:
                    writer.WriteColor32((Color32)value);
                    return;
                case FastSaveTag.Vector2Int:
                    writer.WriteVector2Int((Vector2Int)value);
                    return;
                case FastSaveTag.Vector3Int:
                    writer.WriteVector3Int((Vector3Int)value);
                    return;
                case FastSaveTag.Rect:
                    var rect = (Rect)value;
                    writer.Raw.Write(rect.x);
                    writer.Raw.Write(rect.y);
                    writer.Raw.Write(rect.width);
                    writer.Raw.Write(rect.height);
                    return;
                case FastSaveTag.Bounds:
                    var bounds = (Bounds)value;
                    writer.WriteVector3(bounds.center);
                    writer.WriteVector3(bounds.size);
                    return;
                default:
                    throw new InvalidDataException($"FastSaveData: tag primitive không hợp lệ ({tag}).");
            }
        }

        private static object ReadPrimitive(FastSaveReader reader, byte tag)
        {
            switch (tag)
            {
                case FastSaveTag.Bool: return reader.ReadBool();
                case FastSaveTag.Int: return reader.ReadInt();
                case FastSaveTag.Long: return reader.ReadLong();
                case FastSaveTag.Float: return reader.ReadFloat();
                case FastSaveTag.Double: return reader.ReadDouble();
                case FastSaveTag.String: return reader.ReadString();
                case FastSaveTag.Byte: return reader.ReadByte();
                case FastSaveTag.Short: return reader.ReadShort();
                case FastSaveTag.UInt: return reader.ReadUInt();
                case FastSaveTag.ULong: return reader.ReadULong();
                case FastSaveTag.UShort: return reader.ReadUShort();
                case FastSaveTag.SByte: return reader.ReadSByte();
                case FastSaveTag.Decimal: return reader.ReadDecimal();
                case FastSaveTag.Char: return reader.ReadChar();
                case FastSaveTag.DateTime: return reader.ReadDateTime();
                case FastSaveTag.TimeSpan: return reader.ReadTimeSpan();
                case FastSaveTag.DateTimeOffset:
                    return new DateTimeOffset(reader.Raw.ReadInt64(), new TimeSpan(reader.Raw.ReadInt64()));
                case FastSaveTag.Guid: return reader.ReadGuid();
                case FastSaveTag.Vector2: return reader.ReadVector2();
                case FastSaveTag.Vector3: return reader.ReadVector3();
                case FastSaveTag.Vector4: return reader.ReadVector4();
                case FastSaveTag.Quaternion: return reader.ReadQuaternion();
                case FastSaveTag.Color: return reader.ReadColor();
                case FastSaveTag.Color32: return reader.ReadColor32();
                case FastSaveTag.Vector2Int: return reader.ReadVector2Int();
                case FastSaveTag.Vector3Int: return reader.ReadVector3Int();
                case FastSaveTag.Rect:
                    return new Rect(reader.Raw.ReadSingle(), reader.Raw.ReadSingle(), reader.Raw.ReadSingle(),
                        reader.Raw.ReadSingle());
                case FastSaveTag.Bounds: return new Bounds(reader.ReadVector3(), reader.ReadVector3());
                case FastSaveTag.ByteArray: return reader.ReadBytes();
                default:
                    throw new InvalidDataException($"FastSaveData: tag không nhận dạng được ({tag}).");
            }
        }

        // =========================================================
        // CONVERT
        // =========================================================

        /// <summary>
        /// Ép giá trị vừa đọc về đúng kiểu đích. Dùng cho field đổi kiểu giữa hai phiên bản game
        /// và cho dữ liệu cũ (v1) vốn lưu collection dưới dạng List&lt;object&gt;.
        /// </summary>
        internal static object ConvertTo(object value, Type target)
        {
            if (target == null || target == typeof(object)) return value;

            if (value == null) return target.IsValueType ? Activator.CreateInstance(target) : null;
            if (target.IsInstanceOfType(value)) return value;

            var underlying = Nullable.GetUnderlyingType(target);
            if (underlying != null) return ConvertTo(value, underlying);

            try
            {
                if (target.IsEnum) return Enum.ToObject(target, Convert.ToInt64(value));

                if (value is IList source)
                {
                    if (target.IsArray)
                    {
                        var elementType = target.GetElementType();
                        var array = Array.CreateInstance(elementType!, source.Count);
                        for (var i = 0; i < source.Count; i++) array.SetValue(ConvertTo(source[i], elementType), i);
                        return array;
                    }

                    if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        var elementType = target.GetGenericArguments()[0];
                        var list = (IList)Activator.CreateInstance(target, source.Count);
                        for (var i = 0; i < source.Count; i++) list.Add(ConvertTo(source[i], elementType));
                        return list;
                    }
                }

                if (value is IDictionary sourceMap && target.IsGenericType &&
                    target.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    var arguments = target.GetGenericArguments();
                    var map = (IDictionary)Activator.CreateInstance(target);
                    var enumerator = sourceMap.GetEnumerator();
                    while (enumerator.MoveNext())
                    {
                        var key = ConvertTo(enumerator.Key, arguments[0]);
                        if (key != null) map[key] = ConvertTo(enumerator.Value, arguments[1]);
                    }

                    return map;
                }

                if (value is IConvertible) return Convert.ChangeType(value, target);
            }
            catch
            {
                // Dữ liệu cũ không tương thích: trả default thay vì làm hỏng cả file.
            }

            return target.IsValueType ? Activator.CreateInstance(target) : null;
        }

        // =========================================================
        // VALIDATION
        // =========================================================

        /// <summary>
        /// Kiểm tra kiểu có lưu được không. Gọi ngay lúc Set để lỗi hiện đúng chỗ,
        /// thay vì nổ ở Save() và làm hỏng toàn bộ save.
        /// </summary>
        public static bool IsSupported(Type type, out string reason)
        {
            if (type == null)
            {
                reason = null;
                return true;
            }

            lock (SupportCache)
            {
                if (SupportCache.TryGetValue(type, out var cached))
                {
                    reason = cached;
                    return cached == null;
                }
            }

            var result = Validate(type, 0);

            lock (SupportCache)
            {
                SupportCache[type] = result;
            }

            reason = result;
            return result == null;
        }

        private static string Validate(Type type, int depth)
        {
            if (depth > FastSaveWriter.MaxDepth) return $"'{type.FullName}' lồng quá sâu.";
            if (type == typeof(object) || TryGetPrimitiveTag(type, out _) || type.IsEnum) return null;
            if (type == typeof(byte[])) return null;
            if (typeof(IFastSaveSerializable).IsAssignableFrom(type)) return null;

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return $"'{type.FullName}' là UnityEngine.Object. Hãy lưu id/đường dẫn của asset thay vì tham chiếu.";
            if (typeof(Delegate).IsAssignableFrom(type)) return $"'{type.FullName}' là delegate, không lưu được.";
            if (type.IsPointer || type == typeof(IntPtr) || type == typeof(UIntPtr))
                return $"'{type.FullName}' là con trỏ, không lưu được.";

            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null) return Validate(underlying, depth + 1);

            if (type.IsArray)
                return type.GetArrayRank() != 1
                    ? "Chỉ hỗ trợ mảng một chiều."
                    : Validate(type.GetElementType(), depth + 1);

            if (type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition();
                if (definition == typeof(List<>)) return Validate(type.GetGenericArguments()[0], depth + 1);
                if (definition == typeof(Dictionary<,>))
                {
                    var arguments = type.GetGenericArguments();
                    return Validate(arguments[0], depth + 1) ?? Validate(arguments[1], depth + 1);
                }
            }

            if (type.IsInterface || type.IsAbstract) return null; // Kiểu thật sẽ được kiểm tra lúc chạy.

            if (typeof(IEnumerable).IsAssignableFrom(type))
                return $"'{type.FullName}' là collection chưa hỗ trợ. Dùng List<T>, T[] hoặc Dictionary<K,V>.";

            var slot = FastSaveDataReflection.GetTypeSlot(type);
            if (!slot.CanCreate)
                return $"'{type.FullName}' thiếu constructor không tham số nên không thể load lại.";

            foreach (var field in slot.Fields)
            {
                if (field.FieldType == type) continue; // Tự tham chiếu: để depth limit lúc chạy xử lý.
                var reason = Validate(field.FieldType, depth + 1);
                if (reason != null) return $"Field '{type.Name}.{field.Field.Name}': {reason}";
            }

            return null;
        }

        internal static void ClearCache()
        {
            lock (SupportCache) SupportCache.Clear();
            FastSaveDataReflection.ClearCache();
        }

        // =========================================================
        // LEGACY V1
        // =========================================================

        private static Dictionary<string, object> DeserializeV1(BinaryReader reader)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxCollectionSize) throw new InvalidDataException("FastSaveData: entry count v1 hỏng.");

            var result = new Dictionary<string, object>(count, StringComparer.Ordinal);
            for (var i = 0; i < count; i++)
            {
                var key = reader.ReadString();
                result[key] = ReadValueV1(reader);
            }

            return result;
        }

        private static object ReadValueV1(BinaryReader reader)
        {
            switch (reader.ReadByte())
            {
                case 0: return null;
                case 1: return reader.ReadBoolean();
                case 2: return reader.ReadInt32();
                case 3: return reader.ReadInt64();
                case 4: return reader.ReadSingle();
                case 5: return reader.ReadDouble();
                case 6: return reader.ReadString();
                case 7: return reader.ReadByte();
                case 8: return reader.ReadInt16();
                case 9: return reader.ReadUInt32();
                case 10: return reader.ReadUInt64();
                case 11: return reader.ReadUInt16();
                case 12: return reader.ReadSByte();
                case 13: return reader.ReadDecimal();
                case 14: return reader.ReadChar();
                case 15: return new DateTime(reader.ReadInt64());
                case 16: return new Guid(reader.ReadBytes(16));
                case 17: return new Vector2(reader.ReadSingle(), reader.ReadSingle());
                case 18: return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                case 19:
                    return new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                        reader.ReadSingle());
                case 20:
                    return new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                        reader.ReadSingle());
                case 21:
                    return new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                        reader.ReadSingle());
                case 22: return new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                case 23:
                case 24:
                    var count = reader.ReadInt32();
                    if (count < 0 || count > MaxCollectionSize)
                        throw new InvalidDataException("FastSaveData: collection v1 hỏng.");
                    var list = new List<object>(count);
                    for (var i = 0; i < count; i++) list.Add(ReadValueV1(reader));
                    return list;
                default:
                    throw new InvalidDataException("FastSaveData: tag v1 không nhận dạng được.");
            }
        }
    }
}
