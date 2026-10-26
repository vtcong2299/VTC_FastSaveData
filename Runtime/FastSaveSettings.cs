using UnityEngine;

namespace Vtcong.Core
{
    /// <summary>
    /// Cấu hình riêng của từng project, đặt tại <c>Assets/Resources/FastSaveSettings.asset</c>.
    /// Tạo bằng menu <c>Window > Fast Save Data > Create Settings</c>.
    /// </summary>
    public sealed class FastSaveSettings : ScriptableObject
    {
        public const string ResourcePath = "FastSaveSettings";

        [Tooltip("Key mã hoá file save của game. KHÔNG đổi sau khi đã phát hành, save của người chơi sẽ không đọc được nữa.")]
        [SerializeField] private string secret;

        public string Secret => secret;
    }
}
