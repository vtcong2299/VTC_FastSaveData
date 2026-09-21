# VTC FastSave

Save game nhị phân cho Unity: nhanh, file gọn, hỗ trợ đa dạng kiểu dữ liệu và class tự định nghĩa.

## Cài đặt

Unity ▸ **Window ▸ Package Manager ▸ + ▸ Add package from git URL**:

```
https://github.com/vtcong2299/VTC_FastSaveData.git
```

Không phụ thuộc package nào khác. Odin là tuỳ chọn — chỉ ảnh hưởng phần Example.

## Dùng nhanh

```csharp
FastSaveData.SetInt("coin", 1000);
FastSaveData.SetVector3("checkpoint", transform.position);
FastSaveData.SetList("unlocked", new List<string> { "map1", "map2" });
FastSaveData.SetClass("player", profile);
FastSaveData.Save();

var coin = FastSaveData.GetInt("coin");
var profile = FastSaveData.GetClass<PlayerProfile>("player");
```

Không cần gọi `Save()` khi thoát game: package tự lưu ở `OnApplicationQuit`,
`OnApplicationPause(true)` và khi mất focus. Tắt bằng `FastSaveData.AutoSave = false`.

## Trước khi build

Đổi hằng `Secret` trong `Runtime/FastSaveData.cs`. Package sẽ ném exception nếu vẫn để
secret mặc định.

## Kiểu hỗ trợ

| Nhóm | Kiểu |
|---|---|
| Số | `bool` `byte` `sbyte` `short` `ushort` `int` `uint` `long` `ulong` `float` `double` `decimal` `char` |
| Chuỗi / định danh | `string` `Guid` |
| Thời gian | `DateTime` (giữ `Kind`) `TimeSpan` `DateTimeOffset` |
| Unity | `Vector2/3/4` `Vector2Int` `Vector3Int` `Quaternion` `Color` `Color32` `Rect` `Bounds` |
| Enum | mọi enum, kể cả `[Flags]` |
| Collection | `List<T>` `T[]` `Dictionary<K,V>` `byte[]`, lồng nhau tuỳ ý |
| Class / struct | bất kỳ kiểu có constructor không tham số |

Không lưu được (bị chặn ngay tại `Set` kèm thông báo rõ): `UnityEngine.Object`,
delegate, con trỏ, `HashSet`/`Queue`/`Stack` (chuyển sang `List<T>`).

## Lưu class

Mặc định dùng reflection, quy ước giống Unity:

```csharp
public class PlayerProfile
{
    public int Level;                              // lưu
    [SerializeField] private int _score;           // lưu
    private int _temp;                             // bỏ qua
    [FastSaveIgnore] public float RuntimeCache;    // bỏ qua
    [FastSaveInclude] private int _forced;         // lưu
}
```

Mỗi field được ghi kèm hash tên, nên **thêm, xoá hoặc đổi thứ tự field vẫn đọc được
save cũ** (field mới nhận giá trị mặc định, field đã xoá bị bỏ qua).

Với class được lưu rất nhiều, implement `IFastSaveSerializable` để bỏ qua reflection
và không ghi tên field:

```csharp
public class CompactStats : IFastSaveSerializable
{
    public int Kills;
    public string Map;

    public void FastSaveWrite(FastSaveWriter w) { w.WriteInt(Kills); w.WriteString(Map); }
    public void FastSaveRead(FastSaveReader r)  { Kills = r.ReadInt(); Map = r.ReadString(); }
}
```

## Lưu ý về reference type

`Get<T>` trả về chính object đang nằm trong bộ nhớ. Nếu bạn sửa nó mà không gọi `Set`
lại, hãy gọi `FastSaveData.MarkDirty()`. Auto-save lúc thoát game sẽ tự ghi cưỡng bức
khi có entry reference type nên trường hợp này vẫn an toàn.

## API chính

| | |
|---|---|
| `Set<T>` / `Get<T>` | tổng quát cho mọi kiểu |
| `SetInt/Float/Bool/String/Vector3/Color/DateTime/Enum/Bytes/...` | tiện dụng |
| `SetList/GetList`, `SetArray/GetArray`, `SetDictionary/GetDictionary` | collection |
| `SetClass/GetClass` | class |
| `Save(force)` | ghi đồng bộ, trả về là dữ liệu đã nằm trên đĩa |
| `SaveAsync(force)` | serialize ở main thread, ghi file ở background thread |
| `WaitForPendingWrites(ms)` | chờ mọi `SaveAsync` đang bay ghi xong |
| `HasPendingWrites` | còn `SaveAsync` chưa ghi xong hay không |
| `Reload()` | đọc lại từ đĩa |
| `HasKey` `DeleteKey` `ClearData` `GetAllKeys` `GetValueType` `MarkDirty` | quản lý |
| `Encryption` `AutoSave` `AutoSaveInterval` | cấu hình |
| `OnBeforeSave` `OnAfterSave` `OnAfterLoad` | sự kiện |

### Save hay SaveAsync

Đo trên Windows/NVMe: ghi đĩa chiếm hơn 90% chi phí của `Save()`, còn serialize +
mã hoá chỉ 0.2–0.9 ms. `SaveAsync()` giữ phần serialize trên thread gọi rồi đẩy phần
ghi đĩa sang background.

| | `Save()` | `SaveAsync()` |
|---|---|---|
| Main thread bị khoá (44 field) | 3.05 ms | **0.22 ms** |
| Main thread bị khoá (5000 entry) | 7.73 ms | **0.84 ms** |
| Trả về = chắc chắn đã trên đĩa | **có** | không |

Dùng `SaveAsync()` trong lúc chơi; dùng `Save()` lúc thoát game và trước những thao tác
cần chắc chắn (IAP, gọi server). Auto-save đã làm đúng như vậy sẵn.

`OnAfterSave` luôn bắn **trên main thread** sau khi dữ liệu thật sự nằm trên đĩa, với
cả hai hàm — nên dùng được API Unity bên trong callback.

Bị kill giữa lúc `SaveAsync` đang ghi thì **không hỏng file**: file tạm chưa được
`File.Replace` nên file chính vẫn là bản cũ nguyên vẹn. Và auto-save lúc thoát game
kiểm tra `HasPendingWrites` nên sẽ ghi lại đồng bộ, không bỏ sót lần lưu cuối.

## Mã hoá

`FastSaveData.Encryption`:

- `None` — nhanh nhất, dùng khi debug.
- `Obfuscate` (mặc định) — keystream xorshift128 seed bằng SHA256(secret) + checksum.
  Chặn sửa file bằng hex editor. **Không** chống được người biết reverse binary.
- `Aes` — AES-256-CBC, IV ngẫu nhiên mỗi lần lưu.

Dữ liệu thật sự quan trọng (tiền tệ trả phí, xếp hạng) vẫn phải xác thực phía server.

## Editor

`Window > Fast Save Data`:

- **Inspector** — xem key, kiểu, giá trị, dung lượng; xoá từng key.
- **Open Example Scene** — mở scene test (chỉ có sau khi import sample).
- **Open Save Folder** — mở thư mục `persistentDataPath`.
- **Clear Data** — xoá toàn bộ save.

## Example

Import sample **Example** qua Package Manager (xem mục cuối), rồi mở
`Window ▸ Fast Save Data ▸ Open Example Scene`. Scene có sẵn GameObject
**FastSave Example**.

Menu đó nằm trong sample nên chỉ xuất hiện sau khi import.

### Schema nằm trong ScriptableObject

`FastSaveExampleData` khai báo **44 field** phủ mọi kiểu FastSave hỗ trợ. Mỗi field
public trong đó là một key trong file save (`"example." + tên field`).

Randomize, Save và Load đều duyệt đúng danh sách field này bằng reflection, nên
**thêm một field mới vào ScriptableObject là nó tự động được random, lưu và đọc lại** —
không phải sửa thêm dòng code nào:

```csharp
public class FastSaveExampleData : SerializedScriptableObject
{
    public int intValue;
    public Dictionary<string, int> resources;
    public PlayerProfile profile;
    public List<InventoryItem> inventory;
    // thêm field ở đây là xong
}
```

`FastSaveRandom` sinh giá trị ngẫu nhiên cho bất kỳ kiểu nào FastSave lưu được, kể cả
class lồng nhau, và tôn trọng đúng quy ước field của FastSave (`[FastSaveIgnore]` bị bỏ
qua, `[SerializeField] private` được tính) nên bước Verify mới so được chính xác.
Gọi `FastSaveRandom.Seed(123)` nếu muốn sinh lại đúng bộ dữ liệu cũ khi debug.

### Nút bấm

Có ở hai chỗ: Inspector (Odin, hoặc inspector dự phòng nếu chưa cài Odin) và bảng GUI
trên màn hình khi Play — bảng GUI để test được cả trên máy thật.

| Nút | Việc |
|---|---|
| 1. Randomize Data | random mọi field khai báo trong asset |
| 2. Save | đẩy từng field thành một key rồi ghi xuống đĩa |
| 3. Reset & Load | xoá trắng asset, đọc lại từ đĩa — dữ liệu phải hiện lại đủ |
| 4. Verify Round Trip | làm tự động cả 3 bước trên rồi so từng field, báo PASS/FAIL |
| Save Async / Clear Save | ghi ở background thread, xoá file save |
| Validate Schema | kiểm tra mọi field khai báo có lưu được không |
| Benchmark / Dump / Test Unsupported | đo hiệu năng, in toàn bộ entry, thử kiểu bị chặn |

**Test auto-save:** bấm *1. Randomize Data*, thoát Play mode mà **không** bấm Save,
Play lại rồi bấm *3. Reset & Load* — dữ liệu phải còn nguyên.

> Không cài Odin thì asset vẫn chạy đúng lúc runtime, nhưng Unity không lưu được
> `Dictionary`/`DateTime`/`Guid`/`decimal` vào file `.asset`, nên những field đó sẽ về
> mặc định sau khi reload editor. Có Odin (`SerializedScriptableObject`) thì lưu đủ.

## Format

File v2. Số nguyên dùng varint zig-zag, mọi chuỗi (kể cả key và tên type) dùng chung
một bảng nên chỉ lưu một lần, collection có kiểu phần tử cố định được ghi packed
(không tốn byte tag cho từng phần tử). File v1 của bản đầu tiên vẫn đọc được.

Ghi xuống đĩa theo kiểu ghi file tạm rồi `File.Replace` nguyên tử, có file `.bak`
dự phòng, và `Flush(true)` để ép dữ liệu xuống đĩa thật.

## Số đo tham khảo

5000 entry hỗn hợp (int/float/string/Vector3), Windows, .NET 8:

```
Serialize      4.1 ms
Obfuscate      0.8 ms   (AES-256: 0.4 ms)
Deserialize    1.2 ms
File         114 KB  (22.9 byte/entry)
```

`List<int>` 1000 giá trị nhỏ: **1539 byte** (format cũ cần hơn 5005 byte).

## Mở scene sample

Sample **không** được import sẵn. Vào **Window ▸ Package Manager ▸ VTC Fast Save ▸ Samples ▸ Import**.

Unity copy sample vào `Assets/Samples/VTC Fast Save/<version>/Example/`, từ đó mở scene bình thường.

> **Vì sao phải import?** Package cài qua git URL nằm trong `Library/PackageCache` và ở
> chế độ read-only. Unity **cấm mở scene nằm trong package read-only** — báo
> *"It is not allowed to open a scene in a read-only package"*. Cơ chế Samples của UPM
> sinh ra chính để giải quyết việc này: nó copy sample sang `Assets/` nơi file ghi được.
