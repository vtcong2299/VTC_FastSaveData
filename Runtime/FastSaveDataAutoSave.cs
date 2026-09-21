using UnityEngine;

namespace Vtcong.Core
{
    /// <summary>
    /// Tự động gọi <see cref="FastSaveData.Save"/> ở những thời điểm game có thể bị tắt.
    /// Được tạo tự động, không cần kéo vào scene.
    ///
    /// Trên mobile <c>OnApplicationQuit</c> thường KHÔNG chạy khi hệ điều hành kill app,
    /// nên mốc đáng tin nhất là <c>OnApplicationPause(true)</c> - lúc người chơi bấm Home.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [AddComponentMenu("")]
    public sealed class FastSaveDataAutoSave : MonoBehaviour
    {
        private static FastSaveDataAutoSave _instance;
        private float _timer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;

            // Chỉ HideInHierarchy: HideAndDontSave sẽ không bị huỷ khi thoát Play mode
            // trong editor và gây rò object qua mỗi lần Play.
            var host = new GameObject("[FastSaveData]")
            {
                hideFlags = HideFlags.HideInHierarchy
            };
            _instance = host.AddComponent<FastSaveDataAutoSave>();
            DontDestroyOnLoad(host);
        }

        private void Update()
        {
            // Đưa callback của SaveAsync (OnAfterSave) về main thread.
            FastSaveData.PumpMainThread();

            var interval = FastSaveData.AutoSaveInterval;
            if (interval <= 0f) return;

            _timer += Time.unscaledDeltaTime;
            if (_timer < interval) return;

            _timer = 0f;
            if (FastSaveData.IsDirty) FastSaveData.SaveAsync();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) FastSaveData.AutoSaveNow("pause");
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) FastSaveData.AutoSaveNow("focus lost");
        }

        private void OnApplicationQuit()
        {
            FastSaveData.AutoSaveNow("quit");
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
