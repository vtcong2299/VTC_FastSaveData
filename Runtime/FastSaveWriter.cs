using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Vtcong.Core
{
    /// <summary>
    /// Ghi dữ liệu vào stream FastSave. Số nguyên dùng varint nên giá trị nhỏ chỉ tốn 1 byte,
    /// chuỗi dùng bảng string dùng chung nên chuỗi lặp lại chỉ tốn 1 varint.
    /// </summary>
    public sealed class FastSaveWriter
    {
        internal const int MaxDepth = 32;

        private readonly Dictionary<string, int> _stringMap = new(StringComparer.Ordinal);
        private readonly List<string> _strings = new();

        internal readonly BinaryWriter Raw;
        internal int Depth;

        internal FastSaveWriter(BinaryWriter raw)
        {
            Raw = raw;
        }

        internal List<string> Strings => _strings;

        // ---------------------------------------------------------
        // VARINT
        // ---------------------------------------------------------

        internal void WriteVarULong(ulong value)
        {
            while (value >= 0x80)
            {
                Raw.Write((byte)(value | 0x80));
                value >>= 7;
            }

            Raw.Write((byte)value);
        }

        internal void WriteVarLong(long value) => WriteVarULong((ulong)((value << 1) ^ (value >> 63)));

        // ---------------------------------------------------------
        // PRIMITIVES
        // ---------------------------------------------------------

        public void WriteBool(bool value) => Raw.Write(value);
        public void WriteByte(byte value) => Raw.Write(value);
        public void WriteSByte(sbyte value) => Raw.Write(value);
        public void WriteShort(short value) => WriteVarLong(value);
        public void WriteUShort(ushort value) => WriteVarULong(value);
        public void WriteInt(int value) => WriteVarLong(value);
        public void WriteUInt(uint value) => WriteVarULong(value);
        public void WriteLong(long value) => WriteVarLong(value);
        public void WriteULong(ulong value) => WriteVarULong(value);
        public void WriteFloat(float value) => Raw.Write(value);
        public void WriteDouble(double value) => Raw.Write(value);
        public void WriteDecimal(decimal value) => Raw.Write(value);
        public void WriteChar(char value) => WriteVarULong(value);

        public void WriteString(string value)
        {
            if (value == null)
            {
                WriteVarULong(0);
                return;
            }

            WriteVarULong((ulong)(GetStringId(value) + 1));
        }

        public void WriteDateTime(DateTime value)
        {
            Raw.Write(value.Ticks);
            Raw.Write((byte)value.Kind);
        }

        public void WriteTimeSpan(TimeSpan value) => Raw.Write(value.Ticks);

        public void WriteGuid(Guid value) => Raw.Write(value.ToByteArray());

        public void WriteVector2(Vector2 value)
        {
            Raw.Write(value.x);
            Raw.Write(value.y);
        }

        public void WriteVector3(Vector3 value)
        {
            Raw.Write(value.x);
            Raw.Write(value.y);
            Raw.Write(value.z);
        }

        public void WriteVector4(Vector4 value)
        {
            Raw.Write(value.x);
            Raw.Write(value.y);
            Raw.Write(value.z);
            Raw.Write(value.w);
        }

        public void WriteQuaternion(Quaternion value)
        {
            Raw.Write(value.x);
            Raw.Write(value.y);
            Raw.Write(value.z);
            Raw.Write(value.w);
        }

        public void WriteColor(Color value)
        {
            Raw.Write(value.r);
            Raw.Write(value.g);
            Raw.Write(value.b);
            Raw.Write(value.a);
        }

        public void WriteColor32(Color32 value)
        {
            Raw.Write(value.r);
            Raw.Write(value.g);
            Raw.Write(value.b);
            Raw.Write(value.a);
        }

        public void WriteVector2Int(Vector2Int value)
        {
            WriteVarLong(value.x);
            WriteVarLong(value.y);
        }

        public void WriteVector3Int(Vector3Int value)
        {
            WriteVarLong(value.x);
            WriteVarLong(value.y);
            WriteVarLong(value.z);
        }

        public void WriteBytes(byte[] value)
        {
            if (value == null)
            {
                WriteVarULong(0);
                return;
            }

            WriteVarULong((ulong)value.Length + 1);
            Raw.Write(value);
        }

        /// <summary>
        /// Ghi một giá trị bất kỳ kèm tag kiểu. Dùng cho field có thể null hoặc đa hình.
        /// </summary>
        public void WriteObject(object value) => FastSaveDataSerializer.WriteValue(this, value);

        // ---------------------------------------------------------
        // STRING TABLE
        // ---------------------------------------------------------

        internal int GetStringId(string value)
        {
            if (_stringMap.TryGetValue(value, out var id)) return id;
            id = _strings.Count;
            _strings.Add(value);
            _stringMap[value] = id;
            return id;
        }
    }

    /// <summary>
    /// Đọc dữ liệu từ stream FastSave. Đối xứng hoàn toàn với <see cref="FastSaveWriter"/>.
    /// </summary>
    public sealed class FastSaveReader
    {
        internal readonly BinaryReader Raw;
        internal string[] StringTable = Array.Empty<string>();
        internal int Depth;

        internal FastSaveReader(BinaryReader raw)
        {
            Raw = raw;
        }

        // ---------------------------------------------------------
        // VARINT
        // ---------------------------------------------------------

        internal ulong ReadVarULong()
        {
            ulong result = 0;
            var shift = 0;
            while (true)
            {
                if (shift > 63) throw new InvalidDataException("FastSaveData: varint hỏng.");
                var b = Raw.ReadByte();
                result |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return result;
                shift += 7;
            }
        }

        internal long ReadVarLong()
        {
            var raw = ReadVarULong();
            return (long)(raw >> 1) ^ -(long)(raw & 1);
        }

        internal int ReadCount()
        {
            var value = ReadVarULong();
            if (value > FastSaveDataSerializer.MaxCollectionSize)
                throw new InvalidDataException("FastSaveData: kích thước collection không hợp lệ.");
            return (int)value;
        }

        // ---------------------------------------------------------
        // PRIMITIVES
        // ---------------------------------------------------------

        public bool ReadBool() => Raw.ReadBoolean();
        public byte ReadByte() => Raw.ReadByte();
        public sbyte ReadSByte() => Raw.ReadSByte();
        public short ReadShort() => (short)ReadVarLong();
        public ushort ReadUShort() => (ushort)ReadVarULong();
        public int ReadInt() => (int)ReadVarLong();
        public uint ReadUInt() => (uint)ReadVarULong();
        public long ReadLong() => ReadVarLong();
        public ulong ReadULong() => ReadVarULong();
        public float ReadFloat() => Raw.ReadSingle();
        public double ReadDouble() => Raw.ReadDouble();
        public decimal ReadDecimal() => Raw.ReadDecimal();
        public char ReadChar() => (char)ReadVarULong();

        public string ReadString()
        {
            var id = ReadVarULong();
            if (id == 0) return null;
            var index = (int)(id - 1);
            if (index >= StringTable.Length) throw new InvalidDataException("FastSaveData: string index ngoài bảng.");
            return StringTable[index];
        }

        public DateTime ReadDateTime()
        {
            var ticks = Raw.ReadInt64();
            var kind = (DateTimeKind)Raw.ReadByte();
            return new DateTime(ticks, kind);
        }

        public TimeSpan ReadTimeSpan() => new(Raw.ReadInt64());

        public Guid ReadGuid() => new(Raw.ReadBytes(16));

        public Vector2 ReadVector2() => new(Raw.ReadSingle(), Raw.ReadSingle());
        public Vector3 ReadVector3() => new(Raw.ReadSingle(), Raw.ReadSingle(), Raw.ReadSingle());
        public Vector4 ReadVector4() => new(Raw.ReadSingle(), Raw.ReadSingle(), Raw.ReadSingle(), Raw.ReadSingle());

        public Quaternion ReadQuaternion() =>
            new(Raw.ReadSingle(), Raw.ReadSingle(), Raw.ReadSingle(), Raw.ReadSingle());

        public Color ReadColor() => new(Raw.ReadSingle(), Raw.ReadSingle(), Raw.ReadSingle(), Raw.ReadSingle());
        public Color32 ReadColor32() => new(Raw.ReadByte(), Raw.ReadByte(), Raw.ReadByte(), Raw.ReadByte());
        public Vector2Int ReadVector2Int() => new((int)ReadVarLong(), (int)ReadVarLong());
        public Vector3Int ReadVector3Int() => new((int)ReadVarLong(), (int)ReadVarLong(), (int)ReadVarLong());

        public byte[] ReadBytes()
        {
            var length = ReadVarULong();
            if (length == 0) return null;
            var size = (int)(length - 1);
            if (size > FastSaveDataSerializer.MaxCollectionSize)
                throw new InvalidDataException("FastSaveData: byte[] quá lớn.");
            return Raw.ReadBytes(size);
        }

        /// <summary>
        /// Đọc một giá trị bất kỳ đã ghi bằng <see cref="FastSaveWriter.WriteObject"/>.
        /// </summary>
        public object ReadObject() => FastSaveDataSerializer.ReadValue(this);
    }
}
