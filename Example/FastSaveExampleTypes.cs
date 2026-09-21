using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vtcong.Core.Example
{
    public enum PlayerRank
    {
        Bronze = 0,
        Silver = 1,
        Gold = 2,
        Diamond = 3,
        Legend = 4
    }

    [Flags]
    public enum UnlockFlags
    {
        None = 0,
        Shop = 1 << 0,
        Arena = 1 << 1,
        Guild = 1 << 2,
        Raid = 1 << 3
    }

    /// <summary>
    /// Class lồng bên trong profile. Không cần attribute gì, chỉ cần có constructor không tham số.
    /// </summary>
    [Serializable]
    public class InventoryItem
    {
        public string Id;
        public int Amount;
        public int Level;
        public bool Equipped;
        public Color Tint;

        public override string ToString() => $"{Id} x{Amount} (Lv{Level})";
    }

    [Serializable]
    public class GameSettings
    {
        public float Music = 1f;
        public float Sfx = 1f;
        public bool Haptic = true;
        public string Language = "vi";
        public Vector2Int Resolution = new(1920, 1080);
    }

    /// <summary>
    /// Class chính dùng để test lưu/đọc. Gom gần như mọi kiểu FastSave hỗ trợ.
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        public string DisplayName;
        public int Level;
        public long Coin;
        public double Exp;
        public float Hp;
        public bool IsVip;
        public PlayerRank Rank;
        public UnlockFlags Unlocked;

        public Vector3 LastPosition;
        public Quaternion LastRotation;
        public Color TeamColor;
        public Vector2Int GridPosition;
        public Bounds Area;

        public DateTime LastLogin;
        public TimeSpan TotalPlayTime;
        public Guid DeviceId;

        public List<InventoryItem> Inventory = new();
        public Dictionary<string, int> Resources = new();
        public int[] SkillLevels = Array.Empty<int>();
        public string[] FriendIds = Array.Empty<string>();
        public List<List<int>> BoardHistory = new();
        public byte[] Thumbnail = Array.Empty<byte>();

        // Field private có [SerializeField] vẫn được lưu, giống quy ước của Unity.
        [SerializeField] private int _hiddenScore;

        // Field này bị bỏ qua hoàn toàn: dữ liệu runtime, không cần ghi xuống đĩa.
        [FastSaveIgnore] public float RuntimeCache;

        public int HiddenScore
        {
            get => _hiddenScore;
            set => _hiddenScore = value;
        }

        public GameSettings Settings = new();

        public override string ToString() =>
            $"{DisplayName} | Lv{Level} | {Coin} coin | {Rank} | {Inventory.Count} item | {Resources.Count} resource";
    }

    /// <summary>
    /// Ví dụ đường nhanh nhất: tự ghi/đọc nhị phân, không dùng reflection,
    /// không ghi tên field nên dung lượng nhỏ hơn nhiều so với bản reflection.
    /// Dùng cho class được lưu rất thường xuyên hoặc có số lượng lớn.
    /// </summary>
    public class CompactStats : IFastSaveSerializable
    {
        public int Kills;
        public int Deaths;
        public int Assists;
        public float BestTime;
        public string LastMap;

        public void FastSaveWrite(FastSaveWriter writer)
        {
            writer.WriteInt(Kills);
            writer.WriteInt(Deaths);
            writer.WriteInt(Assists);
            writer.WriteFloat(BestTime);
            writer.WriteString(LastMap);
        }

        public void FastSaveRead(FastSaveReader reader)
        {
            Kills = reader.ReadInt();
            Deaths = reader.ReadInt();
            Assists = reader.ReadInt();
            BestTime = reader.ReadFloat();
            LastMap = reader.ReadString();
        }

        public override string ToString() => $"{Kills}/{Deaths}/{Assists} | best {BestTime:F2}s | {LastMap}";
    }
}
