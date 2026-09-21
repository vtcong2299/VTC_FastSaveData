using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Vtcong.Core
{
    /// <summary>
    /// Save game nhị phân nhanh, gọn, hỗ trợ kiểu dữ liệu đa dạng và class tự định nghĩa.
    ///
    /// Dùng như PlayerPrefs:
    ///     FastSaveData.SetInt("coin", 100);
    ///     FastSaveData.Save();
    ///
    /// Lưu class:
    ///     FastSaveData.SetClass("player", profile);
    ///     var profile = FastSaveData.GetClass&lt;PlayerProfile&gt;("player");
    ///
    /// Tự động lưu khi thoát game hoặc khi app bị đưa xuống nền (xem <see cref="AutoSave"/>).
    /// </summary>
    public static class FastSaveData
    {
        // ĐỔI CHUỖI NÀY TRƯỚC KHI BUILD.
        private const string Secret = "FastSave_8F29Kx7Qm2P6";
        private const bool Backup = true;

        private static readonly object Gate = new();
        private static readonly object WriteGate = new();
        private static readonly Queue<Action> MainThreadQueue = new();

        private static Dictionary<string, object> _entries;
        private static HashSet<string> _referenceKeys;
        private static bool _initialized;
        private static bool _dirty;
        private static int _pendingWrites;
        private static int _mainThreadId;

        /// <summary>Chế độ bảo vệ file. Đổi trước lần Save đầu tiên.</summary>
        public static FastSaveEncryption Encryption { get; set; } = FastSaveEncryption.Obfuscate;

        /// <summary>Bật/tắt tự động lưu khi quit, pause, mất focus.</summary>
        public static bool AutoSave { get; set; } = true;

        /// <summary>Tự động lưu định kỳ khi đang chạy. Đặt 0 để tắt.</summary>
        public static float AutoSaveInterval { get; set; } = 0f;

        public static bool IsDirty
        {
            get
            {
                lock (Gate) return _dirty;
            }
        }

        public static int Count
        {
            get
            {
                lock (Gate) return Entries().Count;
            }
        }

        public static string FilePath => FastSaveDataStorage.FilePath;
        public static long FileSize => FastSaveDataStorage.GetFileSize();

        /// <summary>
        /// True khi có ít nhất một entry là reference type. Những entry đó có thể bị sửa
        /// bên ngoài mà cờ dirty không biết, nên auto-save sẽ ghi cưỡng bức.
        /// </summary>
        public static bool HasMutableEntries
        {
            get
            {
                lock (Gate)
                {
                    Ensure();
                    return _referenceKeys.Count > 0;
                }
            }
        }

        /// <summary>
        /// Còn lần <see cref="SaveAsync"/> nào chưa ghi xong xuống đĩa hay không.
        /// Dữ liệu trong khoảng này đã được đóng gói nhưng chưa chắc đã nằm trên đĩa.
        /// </summary>
        public static bool HasPendingWrites => Volatile.Read(ref _pendingWrites) > 0;

        public static event Action OnBeforeSave;

        /// <summary>
        /// Bắn sau khi dữ liệu đã thực sự nằm trên đĩa, kể cả khi dùng <see cref="SaveAsync"/>.
        /// Luôn được gọi trên main thread nên dùng được API của Unity bên trong.
        /// </summary>
        public static event Action OnAfterSave;

        public static event Action OnAfterLoad;

        // =========================================================
        // INIT
        // =========================================================

        private static Dictionary<string, object> Entries()
        {
            Ensure();
            return _entries;
        }

        private static void Ensure()
        {
            if (_initialized) return;
            _entries = FastSaveDataStorage.Load(Secret);
            _referenceKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in _entries)
                if (IsMutable(pair.Value))
                    _referenceKeys.Add(pair.Key);
            _initialized = true;
            _dirty = false;
            OnAfterLoad?.Invoke();
        }

        private static bool IsMutable(object value) => value != null && !(value is ValueType) && !(value is string);

        // =========================================================
        // TYPED SETTERS / GETTERS
        // =========================================================

        public static void SetInt(string key, int value) => Set(key, value);
        public static int GetInt(string key, int defaultValue = 0) => Get(key, defaultValue);

        public static void SetFloat(string key, float value) => Set(key, value);
        public static float GetFloat(string key, float defaultValue = 0f) => Get(key, defaultValue);

        public static void SetDouble(string key, double value) => Set(key, value);
        public static double GetDouble(string key, double defaultValue = 0d) => Get(key, defaultValue);

        public static void SetLong(string key, long value) => Set(key, value);
        public static long GetLong(string key, long defaultValue = 0L) => Get(key, defaultValue);

        public static void SetBool(string key, bool value) => Set(key, value);
        public static bool GetBool(string key, bool defaultValue = false) => Get(key, defaultValue);

        public static void SetString(string key, string value) => Set(key, value);
        public static string GetString(string key, string defaultValue = "") => Get(key, defaultValue);

        public static void SetByte(string key, byte value) => Set(key, value);
        public static byte GetByte(string key, byte defaultValue = 0) => Get(key, defaultValue);

        public static void SetShort(string key, short value) => Set(key, value);
        public static short GetShort(string key, short defaultValue = 0) => Get(key, defaultValue);

        public static void SetUInt(string key, uint value) => Set(key, value);
        public static uint GetUInt(string key, uint defaultValue = 0) => Get(key, defaultValue);

        public static void SetULong(string key, ulong value) => Set(key, value);
        public static ulong GetULong(string key, ulong defaultValue = 0) => Get(key, defaultValue);

        public static void SetVector2(string key, Vector2 value) => Set(key, value);
        public static Vector2 GetVector2(string key, Vector2 defaultValue = default) => Get(key, defaultValue);

        public static void SetVector3(string key, Vector3 value) => Set(key, value);
        public static Vector3 GetVector3(string key, Vector3 defaultValue = default) => Get(key, defaultValue);

        public static void SetColor(string key, Color value) => Set(key, value);
        public static Color GetColor(string key, Color defaultValue = default) => Get(key, defaultValue);

        public static void SetDateTime(string key, DateTime value) => Set(key, value);
        public static DateTime GetDateTime(string key, DateTime defaultValue = default) => Get(key, defaultValue);

        public static void SetEnum<T>(string key, T value) where T : struct, Enum => Set(key, value);
        public static T GetEnum<T>(string key, T defaultValue = default) where T : struct, Enum =>
            Get(key, defaultValue);

        public static void SetBytes(string key, byte[] value) => Set(key, value);
        public static byte[] GetBytes(string key, byte[] defaultValue = null) => Get(key, defaultValue);

        public static void SetList<T>(string key, List<T> value) => Set(key, value);
        public static List<T> GetList<T>(string key, List<T> defaultValue = null) => Get(key, defaultValue);

        public static void SetArray<T>(string key, T[] value) => Set(key, value);
        public static T[] GetArray<T>(string key, T[] defaultValue = null) => Get(key, defaultValue);

        public static void SetDictionary<TKey, TValue>(string key, Dictionary<TKey, TValue> value) => Set(key, value);

        public static Dictionary<TKey, TValue> GetDictionary<TKey, TValue>(string key,
            Dictionary<TKey, TValue> defaultValue = null) => Get(key, defaultValue);

        /// <summary>Lưu một class/struct bất kỳ. Alias của <see cref="Set{T}"/>, đặt tên cho dễ tìm.</summary>
        public static void SetClass<T>(string key, T value) where T : class => Set(key, value);

        /// <summary>Đọc lại class đã lưu. Trả về <paramref name="defaultValue"/> nếu chưa có hoặc dữ liệu hỏng.</summary>
        public static T GetClass<T>(string key, T defaultValue = null) where T : class => Get(key, defaultValue);

        // =========================================================
        // CORE
        // =========================================================

        public static void Set<T>(string key, T value)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("FastSaveData: key không được rỗng.");

            if (value != null && !FastSaveDataSerializer.IsSupported(value.GetType(), out var reason))
                throw new NotSupportedException($"FastSaveData: không lưu được key '{key}'. {reason}");

            lock (Gate)
            {
                var data = Entries();
                var mutable = IsMutable(value);

                // Chỉ bỏ qua khi giá trị là immutable và thật sự không đổi.
                // Với reference type, so sánh tham chiếu sẽ nói "không đổi" ngay cả khi
                // nội dung vừa bị sửa, nên luôn đánh dấu dirty.
                if (!mutable && data.TryGetValue(key, out var old) && !_referenceKeys.Contains(key) &&
                    Equals(old, value))
                    return;

                data[key] = value;
                if (mutable) _referenceKeys.Add(key);
                else _referenceKeys.Remove(key);
                _dirty = true;
            }
        }

        public static T Get<T>(string key, T defaultValue = default)
        {
            if (string.IsNullOrEmpty(key)) return defaultValue;

            lock (Gate)
            {
                var data = Entries();
                if (!data.TryGetValue(key, out var value) || value == null) return defaultValue;
                if (value is T typed) return typed;

                var converted = FastSaveDataSerializer.ConvertTo(value, typeof(T));
                if (converted is not T result) return defaultValue;

                // Collection/class từ file cũ phải dựng lại bằng reflection, khá tốn.
                // Ghi đè bản đã convert để những lần Get sau đi thẳng vào fast path.
                if (IsMutable(converted))
                {
                    data[key] = converted;
                    _referenceKeys.Add(key);
                }

                return result;
            }
        }

        /// <summary>
        /// Bản không generic của <see cref="Get{T}"/>, dùng khi kiểu chỉ biết lúc chạy
        /// (ví dụ khi duyệt field bằng reflection).
        /// </summary>
        public static object Get(string key, Type type)
        {
            if (string.IsNullOrEmpty(key) || type == null) return null;

            lock (Gate)
            {
                var data = Entries();
                if (!data.TryGetValue(key, out var value) || value == null)
                    return type.IsValueType ? Activator.CreateInstance(type) : null;

                if (type.IsInstanceOfType(value)) return value;

                var converted = FastSaveDataSerializer.ConvertTo(value, type);
                if (IsMutable(converted))
                {
                    data[key] = converted;
                    _referenceKeys.Add(key);
                }

                return converted;
            }
        }

        public static bool HasKey(string key)
        {
            lock (Gate) return !string.IsNullOrEmpty(key) && Entries().ContainsKey(key);
        }

        public static Type GetValueType(string key)
        {
            lock (Gate)
            {
                return Entries().TryGetValue(key, out var value) ? value?.GetType() : null;
            }
        }

        public static List<string> GetAllKeys()
        {
            lock (Gate) return new List<string>(Entries().Keys);
        }

        public static void DeleteKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            lock (Gate)
            {
                if (!Entries().Remove(key)) return;
                _referenceKeys.Remove(key);
                _dirty = true;
            }
        }

        public static void DeleteAll() => ClearData();

        /// <summary>
        /// Báo dữ liệu đã đổi khi bạn sửa trực tiếp object lấy từ <see cref="Get{T}"/>
        /// mà không gọi Set lại.
        /// </summary>
        public static void MarkDirty()
        {
            lock (Gate) _dirty = true;
        }

        // =========================================================
        // SAVE / LOAD
        // =========================================================

        /// <summary>
        /// Ghi xuống đĩa ngay trên thread hiện tại. Bỏ qua nếu không có thay đổi,
        /// trừ khi <paramref name="force"/> = true.
        /// </summary>
        public static void Save(bool force = false)
        {
            ArraySegment<byte> packed;

            lock (Gate)
            {
                Ensure();
                if (!force && !_dirty) return;
                OnBeforeSave?.Invoke();
                packed = FastSaveDataStorage.Pack(_entries, Secret, Encryption);
                _dirty = false;
            }

            try
            {
                // Chờ luôn mọi SaveAsync đang bay để không có hai luồng cùng ghi.
                lock (WriteGate) FastSaveDataStorage.WritePacked(packed, Backup);
                InvokeOnMainThread(OnAfterSave);
            }
            catch (Exception e)
            {
                lock (Gate) _dirty = true;
                Debug.LogError($"[FastSaveData] Save thất bại: {e}");
            }

            PumpMainThread();
        }

        /// <summary>
        /// Serialize trên thread gọi (nhanh, an toàn với dữ liệu đang thay đổi)
        /// rồi đẩy phần ghi file sang background thread để không làm khựng frame.
        /// </summary>
        public static Task SaveAsync(bool force = false)
        {
            ArraySegment<byte> packed;

            lock (Gate)
            {
                Ensure();
                if (!force && !_dirty) return Task.CompletedTask;
                OnBeforeSave?.Invoke();
                packed = FastSaveDataStorage.Pack(_entries, Secret, Encryption);
                _dirty = false;
            }

            // Chạm vào FilePath khi còn ở main thread: Application.persistentDataPath
            // không đọc được từ background thread.
            _ = FastSaveDataStorage.FilePath;

            // Đếm trước khi đẩy task đi: từ đây tới lúc ghi xong, dữ liệu đã đóng gói
            // nhưng chưa chắc nằm trên đĩa. Auto-save lúc thoát game dựa vào cờ này
            // để không bỏ sót lần lưu cuối.
            Interlocked.Increment(ref _pendingWrites);

            return Task.Run(() =>
            {
                try
                {
                    lock (WriteGate) FastSaveDataStorage.WritePacked(packed, Backup);
                    InvokeOnMainThread(OnAfterSave);
                }
                catch (Exception e)
                {
                    lock (Gate) _dirty = true;
                    Debug.LogError($"[FastSaveData] SaveAsync thất bại: {e}");
                }
                finally
                {
                    Interlocked.Decrement(ref _pendingWrites);
                }
            });
        }

        /// <summary>
        /// Chặn thread hiện tại cho tới khi mọi <see cref="SaveAsync"/> đang bay ghi xong.
        /// Trả về false nếu hết thời gian chờ.
        /// </summary>
        public static bool WaitForPendingWrites(int timeoutMs = 3000)
        {
            if (!HasPendingWrites) return true;

            var watch = Stopwatch.StartNew();
            while (HasPendingWrites)
            {
                if (watch.ElapsedMilliseconds >= timeoutMs)
                {
                    Debug.LogWarning("[FastSaveData] Hết thời gian chờ SaveAsync ghi xong.");
                    return false;
                }

                Thread.Sleep(1);
            }

            PumpMainThread();
            return true;
        }

        public static void Reload()
        {
            lock (Gate)
            {
                _initialized = false;
                _entries = null;
                _referenceKeys = null;
                Ensure();
            }
        }

        public static void ClearData()
        {
            lock (Gate)
            {
                lock (WriteGate) FastSaveDataStorage.Clear();
                _entries = new Dictionary<string, object>(StringComparer.Ordinal);
                _referenceKeys = new HashSet<string>(StringComparer.Ordinal);
                _initialized = true;
                _dirty = false;
            }
        }

        // =========================================================
        // AUTO SAVE
        // =========================================================

        internal static void AutoSaveNow(string trigger)
        {
            if (!AutoSave) return;

            // Ba lý do phải ghi cưỡng bức lúc này:
            //  - Entry reference type có thể đã bị sửa mà cờ dirty không biết.
            //  - Còn SaveAsync đang bay: _dirty đã bị đặt false nhưng dữ liệu chưa
            //    chắc nằm trên đĩa, mà process thì sắp chết.
            var force = HasMutableEntries || HasPendingWrites;
            if (!force && !IsDirty) return;

            Save(force);
#if UNITY_EDITOR
            Debug.Log($"[FastSaveData] Auto-save ({trigger}).");
#endif
        }

        // =========================================================
        // MAIN THREAD DISPATCH
        // =========================================================

        /// <summary>
        /// Chạy các callback đã xếp hàng từ background thread.
        /// <see cref="FastSaveDataAutoSave"/> gọi mỗi frame.
        /// </summary>
        internal static void PumpMainThread()
        {
            while (true)
            {
                Action action;
                lock (MainThreadQueue)
                {
                    if (MainThreadQueue.Count == 0) return;
                    action = MainThreadQueue.Dequeue();
                }

                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[FastSaveData] Callback lỗi: {e}");
                }
            }
        }

        private static void InvokeOnMainThread(Action action)
        {
            if (action == null) return;

            // _mainThreadId == 0 nghĩa là chưa qua RuntimeInitializeOnLoadMethod
            // (ví dụ script editor chạy ngoài play mode): gọi thẳng, không xếp hàng
            // vì sẽ không có ai pump.
            if (_mainThreadId == 0 || Thread.CurrentThread.ManagedThreadId == _mainThreadId)
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[FastSaveData] Callback lỗi: {e}");
                }

                return;
            }

            lock (MainThreadQueue) MainThreadQueue.Enqueue(action);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;

#if UNITY_EDITOR
            // Domain reload bị tắt thì static field vẫn còn từ lần Play trước.
            // Reset để mỗi lần Play đều đọc lại từ file.
            _initialized = false;
            _entries = null;
            _referenceKeys = null;
            _dirty = false;
            _pendingWrites = 0;
            lock (MainThreadQueue) MainThreadQueue.Clear();
#endif
        }
    }
}
