using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Vtcong.Core
{
    /// <summary>
    /// Đọc/ghi file save. Ghi theo kiểu write-temp rồi thay thế nguyên tử,
    /// nên mất điện giữa chừng cũng không bao giờ để lại file chính rỗng.
    /// </summary>
    public static class FastSaveDataStorage
    {
        private static string _filePath;
        private static string _backupPath;
        private static string _tempPath;

        public static string FileName { get; set; } = "FastSaveData.dat";

        public static string FilePath
        {
            get
            {
                if (_filePath == null) CachePaths();
                return _filePath;
            }
        }

        public static string BackupPath
        {
            get
            {
                if (_backupPath == null) CachePaths();
                return _backupPath;
            }
        }

        private static string TempPath
        {
            get
            {
                if (_tempPath == null) CachePaths();
                return _tempPath;
            }
        }

        /// <summary>
        /// Gọi khi đổi <see cref="FileName"/> để tính lại đường dẫn.
        /// </summary>
        public static void InvalidatePaths()
        {
            _filePath = null;
            _backupPath = null;
            _tempPath = null;
        }

        private static void CachePaths()
        {
            // Application.persistentDataPath chỉ đọc được trên main thread nên cache một lần.
            _filePath = Path.Combine(Application.persistentDataPath, FileName);
            _backupPath = _filePath + ".bak";
            _tempPath = _filePath + ".tmp";
        }

        /// <summary>
        /// Chuẩn bị mọi thứ cần main thread trước khi có thể ghi từ background thread.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void Prepare()
        {
            CachePaths();
        }

        // =========================================================
        // SAVE
        // =========================================================

        /// <summary>
        /// Serialize + mã hoá. Kết quả có thể đem ghi file ở thread khác.
        /// </summary>
        public static ArraySegment<byte> Pack(IReadOnlyDictionary<string, object> entries, string secret,
            FastSaveEncryption mode)
        {
            var payload = FastSaveDataSerializer.Serialize(entries, FastSaveDataCrypto.HeaderSize);
            return FastSaveDataCrypto.Protect(payload.Array, payload.Count, secret, mode);
        }

        public static void Save(IReadOnlyDictionary<string, object> entries, string secret, bool backup,
            FastSaveEncryption mode)
        {
            WritePacked(Pack(entries, secret, mode), backup);
        }

        /// <summary>
        /// Ghi dữ liệu đã pack xuống đĩa. An toàn để gọi từ background thread
        /// miễn là đường dẫn đã được cache (<see cref="Prepare"/>).
        /// </summary>
        public static void WritePacked(ArraySegment<byte> packed, bool backup)
        {
            var filePath = FilePath;
            var tempPath = TempPath;
            var backupPath = BackupPath;

            var folder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) Directory.CreateDirectory(folder);

            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(packed.Array, packed.Offset, packed.Count);
                    stream.Flush(true); // Ép xuống đĩa, không để nằm trong cache của OS.
                }

                if (!File.Exists(filePath))
                {
                    File.Move(tempPath, filePath);
                    return;
                }

                try
                {
                    File.Replace(tempPath, filePath, backup ? backupPath : null, true);
                }
                catch (PlatformNotSupportedException)
                {
                    ReplaceFallback(tempPath, filePath, backupPath, backup);
                }
                catch (IOException)
                {
                    ReplaceFallback(tempPath, filePath, backupPath, backup);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (IOException)
                    {
                        // File tạm còn sót lại không ảnh hưởng lần save sau.
                    }
            }
        }

        private static void ReplaceFallback(string tempPath, string filePath, string backupPath, bool backup)
        {
            if (backup) File.Copy(filePath, backupPath, true);
            File.Copy(tempPath, filePath, true);
        }

        // =========================================================
        // LOAD
        // =========================================================

        public static Dictionary<string, object> Load(string secret)
        {
            if (TryLoad(FilePath, secret, out var entries)) return entries;

            if (File.Exists(BackupPath))
            {
                Debug.LogWarning("[FastSaveData] File chính hỏng, đang khôi phục từ backup.");
                if (TryLoad(BackupPath, secret, out entries)) return entries;
            }

            return new Dictionary<string, object>(StringComparer.Ordinal);
        }

        private static bool TryLoad(string path, string secret, out Dictionary<string, object> entries)
        {
            entries = null;
            if (!File.Exists(path)) return false;

            try
            {
                entries = FastSaveDataSerializer.Deserialize(FastSaveDataCrypto.Unprotect(File.ReadAllBytes(path), secret));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[FastSaveData] Không đọc được '{path}': {e.Message}");
                return false;
            }
        }

        public static long GetFileSize()
        {
            var info = new FileInfo(FilePath);
            return info.Exists ? info.Length : 0;
        }

        // =========================================================
        // CLEAR
        // =========================================================

        public static void Clear()
        {
            DeleteIfExists(FilePath);
            DeleteIfExists(BackupPath);
            DeleteIfExists(TempPath);
        }

        private static void DeleteIfExists(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[FastSaveData] Không xoá được '{path}': {e.Message}");
            }
        }
    }
}
