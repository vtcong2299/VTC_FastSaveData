# Changelog

Tất cả thay đổi đáng chú ý của VTC Fast Save được ghi ở đây.
Định dạng theo [Keep a Changelog](https://keepachangelog.com/vi/1.1.0/),
đánh số theo [Semantic Versioning](https://semver.org/lang/vi/).

## [1.2.2] - 2026-10-27

### Added
- Menu `MyTool > Clear Data` với phím tắt Ctrl+Shift+Q (macOS: Cmd+Shift+Q), cùng chức năng
  `Window > Fast Save Data > Clear Data`.

## [1.2.1] - 2026-10-26

### Fixed
- Bộ tự cập nhật nâng bản cũ bằng git URL không gắn tag. Trước đó URL bị ghim `#v1.1.0` nên nút
  Update của Package Manager chỉ tải lại đúng bản 1.1.0.
- Bộ tự cập nhật không còn nhận nhầm `package.json` cũ trong cache của GitHub ngay sau khi phát hành.

## [1.2.0] - 2026-10-26

### Added
- `FastSaveSettings` trong `Resources` giữ secret riêng của từng game, tạo bằng
  `Window > Fast Save Data > Create Settings`. Save mã hoá bằng secret dùng chung cũ vẫn đọc
  được và được mã hoá lại ở lần Save sau.
- `FastSaveData.LoadStatus` cho biết lần đọc file gần nhất: `NoFile`, `Loaded`,
  `MigratedLegacy`, `RestoredFromBackup` hoặc `Corrupted`.

### Fixed
- Một `SaveAsync` cũ không còn ghi đè bản mới hơn của `Save()` hay `SaveAsync()` gọi sau nó.
- `ClearData()` khi còn `SaveAsync` đang chờ không còn để dữ liệu cũ bị ghi lại.
- `Reload()` không đọc file khi một `SaveAsync` đang ghi dở.
- File save và backup đều hỏng thì bản sao được giữ ở `.corrupt` thay vì bị ghi đè âm thầm.
- Khôi phục từ backup thì file chính được ghi lại ngay, không còn chỉ một bản tốt.

## [1.1.0] - 2026-10-26

### Added
- Tự kiểm tra cập nhật mỗi lần mở Editor cho mọi package `com.vtcong.*` cài qua git URL.
  Bản thấp hơn 1.1.0 được tự nâng lên 1.1.0; bản mới hơn thì hỏi người dùng trước khi cập nhật.

## [1.0.2] - 2026-10-26

### Fixed
- Tạo GUID mới cho toàn bộ file `.meta` của package.
  GUID cũ trùng với bản code gốc, nên ở project đã có sẵn bản đó Unity bỏ qua asset của package
  ("conflicts with ... The asset will be ignored") và các class trong package bị mất.

## [1.0.1] - 2026-09-21

### Fixed
- Chuyển thư mục sample sang `Samples~` và khai báo trong `package.json`.
  Trước đó scene sample nằm thẳng trong package nên Unity từ chối mở với thông báo
  "It is not allowed to open a scene in a read-only package" khi cài qua git URL.
  Giờ import sample qua Package Manager, Unity copy sang `Assets/Samples/` nên mở được.

## [1.0.0] - 2026-09-21

### Added
- Phát hành đầu tiên dưới dạng package UPM độc lập.
