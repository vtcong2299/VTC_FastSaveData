# Changelog

Tất cả thay đổi đáng chú ý của VTC Fast Save được ghi ở đây.
Định dạng theo [Keep a Changelog](https://keepachangelog.com/vi/1.1.0/),
đánh số theo [Semantic Versioning](https://semver.org/lang/vi/).

## [1.0.1] - 2026-09-21

### Fixed
- Chuyển thư mục sample sang `Samples~` và khai báo trong `package.json`.
  Trước đó scene sample nằm thẳng trong package nên Unity từ chối mở với thông báo
  "It is not allowed to open a scene in a read-only package" khi cài qua git URL.
  Giờ import sample qua Package Manager, Unity copy sang `Assets/Samples/` nên mở được.

## [1.0.0] - 2026-09-21

### Added
- Phát hành đầu tiên dưới dạng package UPM độc lập.
