# TM Last Epoch Co-op Mod 1.1.0-dev.65

Protocol44. Cập nhật cả hai DLL khi game đã đóng.

- Ẩn banner nhặt shard, rune/glyph, vật liệu, key và Woven Echo dạng “sent to the Forge/Storage” trong offline.
- Chỉ chặn năm hàm tạo thông báo tương ứng; giữ quy trình nhặt đồ, cộng vào kho và xử lý buffer native. Các thông báo nhiệm vụ, lên cấp, cảnh báo và sự kiện khác vẫn hoạt động.
- Giữ Woven Enchanter10 lượt và toàn bộ thay đổi dev.64.

## Kiểm tra

Build Release không warning/error;198 compiled audits đạt, gồm kiểm tra đúng năm notification targets và không chứa công cụ QA/debug. Đối chiếu native caller và năm hàm chỉ tạo queued notification. Chưa kiểm tra trực quan trong engine cho bản ẩn banner vì game Steam đang chạy. 15 updater fixture checks đạt; gói phát hành và tải public được xác minh SHA256.

ZIP chỉ gồm cặp DLL, manifest, update.bat, README và release notes; không có helper QA, log hay profile. Thoát game trước khi cập nhật.
