using System;

namespace Vtcong.Core
{
    /// <summary>
    /// Chế độ bảo vệ file save.
    /// </summary>
    public enum FastSaveEncryption : byte
    {
        /// <summary>Ghi thẳng, nhanh nhất, file đọc được bằng hex editor.</summary>
        None = 0,

        /// <summary>Keystream xorshift128 seed bằng SHA256(secret). Nhanh, chống sửa file bằng tay. Mặc định.</summary>
        Obfuscate = 1,

        /// <summary>AES-256-CBC + IV ngẫu nhiên. Chậm hơn ~5x nhưng vẫn dưới 1ms cho save cỡ 100KB.</summary>
        Aes = 2
    }

    /// <summary>
    /// Bỏ qua field này khi FastSave tự động serialize class bằng reflection.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class FastSaveIgnoreAttribute : Attribute
    {
    }

    /// <summary>
    /// Buộc serialize field private này (tương đương [SerializeField] với FastSave).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class FastSaveIncludeAttribute : Attribute
    {
    }

    /// <summary>
    /// Implement interface này để tự ghi/đọc nhị phân, bỏ qua hoàn toàn reflection.
    /// Đây là đường nhanh nhất và gọn nhất để lưu một class.
    /// Class phải có constructor không tham số.
    /// </summary>
    public interface IFastSaveSerializable
    {
        void FastSaveWrite(FastSaveWriter writer);
        void FastSaveRead(FastSaveReader reader);
    }

    /// <summary>
    /// Mã tag của từng kiểu trong stream. Không được đổi giá trị đã phát hành.
    /// </summary>
    internal static class FastSaveTag
    {
        public const byte Null = 0;
        public const byte Bool = 1;
        public const byte Int = 2;
        public const byte Long = 3;
        public const byte Float = 4;
        public const byte Double = 5;
        public const byte String = 6;
        public const byte Byte = 7;
        public const byte Short = 8;
        public const byte UInt = 9;
        public const byte ULong = 10;
        public const byte UShort = 11;
        public const byte SByte = 12;
        public const byte Decimal = 13;
        public const byte Char = 14;
        public const byte DateTime = 15;
        public const byte Guid = 16;
        public const byte Vector2 = 17;
        public const byte Vector3 = 18;
        public const byte Vector4 = 19;
        public const byte Quaternion = 20;
        public const byte Color = 21;
        public const byte Color32 = 22;
        public const byte ObjectList = 23;
        public const byte ObjectArray = 24;
        public const byte ByteArray = 25;
        public const byte Enum = 26;
        public const byte ObjectDictionary = 27;
        public const byte Vector2Int = 28;
        public const byte Vector3Int = 29;
        public const byte Rect = 30;
        public const byte Bounds = 31;
        public const byte TimeSpan = 32;
        public const byte DateTimeOffset = 33;
        public const byte Custom = 34;
        public const byte Reflected = 35;
        public const byte TypedList = 36;
        public const byte TypedArray = 37;
        public const byte TypedDictionary = 38;
    }
}
