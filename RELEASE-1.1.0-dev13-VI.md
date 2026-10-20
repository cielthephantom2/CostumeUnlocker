# Bản 1.1.0-dev.13 — sửa icon và slider Combat Assist F1

Protocol **35**, Core giữ nguyên bản đã phát hành dev.12. Đóng game trên từng máy rồi chạy `update.bat` để cập nhật.

- Icon skill: chỉ nhận icon HUD đang active có cùng ability.Pointer với skill thực tế; nếu không có, tải icon từ abilitySpriteSoftRef của chính skill. Bỏ fallback chỉ theo số slot gây lặp/lệch icon E/R/F/RMB.
- Toàn bộ slider F1 dùng chung helper đã sửa: đọc slider.value native thay cho tham số float qua callback IL2CPP; đồng bộ lại nút kéo và số hiển thị với giá trị sau khi làm tròn theo bước. Căn giữa pivot nút kéo.
- Footer hiển thị Master ON/OFF và trạng thái pause khi panel mở. Skill chưa xác nhận hiển thị Confirmation required.
- Giữ nguyên luật cast, potion, settings từng nhân vật, Shared Stash, LootFilter và protocol. Encounter vẫn tạm gác.

Build Release đạt **0 warning/error**. Không chạy test theo yêu cầu người dùng; chưa xác nhận icon đủ 5 slot, đổi skill và kéo slider trong game trên dev.13. Các kết quả dev.12 trong tài liệu cũ là baseline lịch sử, không phải kết quả dev.13. Lỗi ActorVisuals trước đây chưa được xử lý trong bản này.

SHA256 Mod: `D262419521A5DCD2471380D7D865AB1649ED7F568EF952872C3C97FA821F146C`

SHA256 Core: `6547E098776B12DBDD58DB7CF097D8A75972FE361726087A61135ADDD9B3D59F`

[Hướng dẫn cấu hình và ảnh lịch sử dev.12](RELEASE-1.1.0-dev12-VI.md).
