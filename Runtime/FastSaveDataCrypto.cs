using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Vtcong.Core
{
    /// <summary>
    /// Bảo vệ file save.
    ///
    /// Container v2:
    ///   [0]      version container (2)
    ///   [1]      <see cref="FastSaveEncryption"/>
    ///   [2..5]   int32 độ dài payload gốc
    ///   [6..9]   uint32 checksum của payload gốc (có trộn key)
    ///   [10..]   payload (đã mã hoá)
    ///
    /// Lưu ý thực tế: None/Obfuscate chỉ chống người chơi sửa file bằng hex editor.
    /// Secret nằm trong binary nên không chống được kẻ có kỹ năng reverse.
    /// Nếu dữ liệu thật sự nhạy cảm thì phải xác thực phía server.
    /// </summary>
    public static class FastSaveDataCrypto
    {
        public const int HeaderSize = 10;
        private const byte ContainerVersion = 2;
        private const string DefaultSecret = "CHANGE_THIS_FAST_SAVE_DATA_SECRET_2026";

        private static readonly Dictionary<string, byte[]> KeyCache = new(StringComparer.Ordinal);

        // =========================================================
        // PROTECT
        // =========================================================

        /// <summary>
        /// Mã hoá tại chỗ. <paramref name="buffer"/> phải chừa sẵn <see cref="HeaderSize"/> byte đầu,
        /// payload nằm ở <c>buffer[HeaderSize .. HeaderSize + payloadLength)</c>.
        /// </summary>
        public static ArraySegment<byte> Protect(byte[] buffer, int payloadLength, string secret,
            FastSaveEncryption mode)
        {
            ValidateSecret(secret);
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (buffer.Length < HeaderSize + payloadLength)
                throw new ArgumentException("FastSaveData: buffer thiếu chỗ cho header.", nameof(buffer));

            var key = GetKey(secret);
            var checksum = Checksum(buffer, HeaderSize, payloadLength, key);

            if (mode == FastSaveEncryption.Aes)
            {
                var cipher = EncryptAes(buffer, HeaderSize, payloadLength, key);
                var result = new byte[HeaderSize + cipher.Length];
                WriteHeader(result, mode, payloadLength, checksum);
                Buffer.BlockCopy(cipher, 0, result, HeaderSize, cipher.Length);
                return new ArraySegment<byte>(result, 0, result.Length);
            }

            if (mode == FastSaveEncryption.Obfuscate)
                ApplyKeystream(buffer, HeaderSize, payloadLength, key);

            WriteHeader(buffer, mode, payloadLength, checksum);
            return new ArraySegment<byte>(buffer, 0, HeaderSize + payloadLength);
        }

        // =========================================================
        // UNPROTECT
        // =========================================================

        public static byte[] Unprotect(byte[] data, string secret)
        {
            ValidateSecret(secret);
            if (data == null || data.Length < 8) throw new InvalidDataException("FastSaveData: file quá ngắn.");

            return data[0] == ContainerVersion && data.Length >= HeaderSize
                ? UnprotectV2(data, secret)
                : UnprotectV1(data, secret);
        }

        private static byte[] UnprotectV2(byte[] data, string secret)
        {
            var mode = (FastSaveEncryption)data[1];
            var payloadLength = BitConverter.ToInt32(data, 2);
            var checksum = BitConverter.ToUInt32(data, 6);

            if (payloadLength < 0 || payloadLength > 512 * 1024 * 1024)
                throw new InvalidDataException("FastSaveData: độ dài payload không hợp lệ.");

            var key = GetKey(secret);
            byte[] payload;

            if (mode == FastSaveEncryption.Aes)
            {
                payload = DecryptAes(data, HeaderSize, data.Length - HeaderSize, key);
                if (payload.Length != payloadLength)
                    throw new InvalidDataException("FastSaveData: độ dài sau giải mã không khớp.");
            }
            else
            {
                if (payloadLength != data.Length - HeaderSize)
                    throw new InvalidDataException("FastSaveData: độ dài payload không khớp kích thước file.");

                payload = new byte[payloadLength];
                Buffer.BlockCopy(data, HeaderSize, payload, 0, payloadLength);
                if (mode == FastSaveEncryption.Obfuscate) ApplyKeystream(payload, 0, payload.Length, key);
            }

            if (Checksum(payload, 0, payload.Length, key) != checksum)
                throw new InvalidDataException("FastSaveData: checksum sai, file đã bị sửa hoặc hỏng.");

            return payload;
        }

        /// <summary>Đọc file của bản đầu tiên (XOR key 32 byte lặp lại) để không mất save cũ.</summary>
        private static byte[] UnprotectV1(byte[] data, string secret)
        {
            var key = CreateLegacyKey(secret);
            var expectedLength = BitConverter.ToInt32(data, 4);
            if (expectedLength < 0 || expectedLength != data.Length - 8)
                throw new InvalidDataException("FastSaveData: file không đúng định dạng.");

            var result = new byte[expectedLength];
            for (var i = 0; i < result.Length; i++)
                result[i] = (byte)(data[i + 8] ^ key[i % key.Length]);

            if (LegacyChecksum(result, key) != BitConverter.ToUInt32(data, 0))
                throw new InvalidDataException("FastSaveData: checksum v1 sai.");

            return result;
        }

        // =========================================================
        // INTERNALS
        // =========================================================

        private static void WriteHeader(byte[] target, FastSaveEncryption mode, int payloadLength, uint checksum)
        {
            target[0] = ContainerVersion;
            target[1] = (byte)mode;
            target[2] = (byte)payloadLength;
            target[3] = (byte)(payloadLength >> 8);
            target[4] = (byte)(payloadLength >> 16);
            target[5] = (byte)(payloadLength >> 24);
            target[6] = (byte)checksum;
            target[7] = (byte)(checksum >> 8);
            target[8] = (byte)(checksum >> 16);
            target[9] = (byte)(checksum >> 24);
        }

        private static byte[] GetKey(string secret)
        {
            lock (KeyCache)
            {
                if (KeyCache.TryGetValue(secret, out var cached)) return cached;
                using var sha = SHA256.Create();
                var key = sha.ComputeHash(Encoding.UTF8.GetBytes("FastSaveData/v2/" + secret));
                KeyCache[secret] = key;
                return key;
            }
        }

        /// <summary>
        /// Keystream xorshift128 seed bằng key. Không lặp theo chu kỳ 32 byte như bản cũ,
        /// nên biết plaintext của header cũng không lộ được keystream phần sau.
        /// </summary>
        private static void ApplyKeystream(byte[] data, int offset, int count, byte[] key)
        {
            unchecked
            {
                var x = BitConverter.ToUInt32(key, 0) | 1u;
                var y = BitConverter.ToUInt32(key, 4);
                var z = BitConverter.ToUInt32(key, 8);
                var w = BitConverter.ToUInt32(key, 12);

                var end = offset + count;
                var i = offset;
                while (i < end)
                {
                    var t = x ^ (x << 11);
                    x = y;
                    y = z;
                    z = w;
                    w = w ^ (w >> 19) ^ t ^ (t >> 8);

                    var block = w;
                    for (var b = 0; b < 4 && i < end; b++, i++) data[i] ^= (byte)(block >> (b * 8));
                }
            }
        }

        private static uint Checksum(byte[] data, int offset, int count, byte[] key)
        {
            unchecked
            {
                var hash = 2166136261u;
                var end = offset + count;
                for (var i = offset; i < end; i++)
                {
                    hash ^= (uint)(data[i] ^ key[(i - offset) & 31]);
                    hash *= 16777619u;
                    hash ^= hash >> 13;
                }

                return hash;
            }
        }

        private static byte[] EncryptAes(byte[] data, int offset, int count, byte[] key)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();

            using var transform = aes.CreateEncryptor();
            var cipher = transform.TransformFinalBlock(data, offset, count);

            var result = new byte[aes.IV.Length + cipher.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(cipher, 0, result, aes.IV.Length, cipher.Length);
            return result;
        }

        private static byte[] DecryptAes(byte[] data, int offset, int count, byte[] key)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            const int ivSize = 16;
            if (count < ivSize) throw new InvalidDataException("FastSaveData: thiếu IV.");

            var iv = new byte[ivSize];
            Buffer.BlockCopy(data, offset, iv, 0, ivSize);
            aes.IV = iv;

            using var transform = aes.CreateDecryptor();
            return transform.TransformFinalBlock(data, offset + ivSize, count - ivSize);
        }

        private static void ValidateSecret(string secret)
        {
            if (string.IsNullOrWhiteSpace(secret) || secret == DefaultSecret)
                throw new InvalidOperationException("FastSaveData: đổi Secret trước khi build.");
        }

        // ---------------------------------------------------------
        // LEGACY V1
        // ---------------------------------------------------------

        private static byte[] CreateLegacyKey(string secret)
        {
            unchecked
            {
                var hash = 2166136261u;
                for (var i = 0; i < secret.Length; i++)
                {
                    hash ^= secret[i];
                    hash *= 16777619u;
                }

                var key = new byte[32];
                for (var i = 0; i < key.Length; i++)
                {
                    hash ^= (uint)(i * 0x9E3779B9);
                    hash *= 16777619u;
                    hash ^= hash >> 13;
                    key[i] = (byte)(hash >> ((i & 3) * 8));
                }

                return key;
            }
        }

        private static uint LegacyChecksum(byte[] data, byte[] key)
        {
            unchecked
            {
                var hash = 2166136261u;
                for (var i = 0; i < data.Length; i++)
                {
                    hash ^= (uint)(data[i] ^ key[i % key.Length]);
                    hash *= 16777619u;
                    hash ^= hash >> 13;
                }

                return hash;
            }
        }
    }
}
