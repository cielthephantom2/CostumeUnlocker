# TM Last Epoch Co-op Mod 1.1.0-dev.68

Protocol44. Cập nhật cả hai DLL khi game đã đóng.

- Khôi phục cơ chế điều khiển Disintegrate của dev.20/21: chuột ngắm tia, WASD theo binding/hướng camera native hoặc lệnh đi riêng mới di chuyển nhân vật. Giữ tốc độ đi khi channel bằng80% bình thường.
- Khi bắt đầu channel, đích đi được đặt tại vị trí hiện tại. Tắt tự theo chuột; cập nhật hướng ngắm không ghi đè đích di chuyển. Lệnh MoveToPoint do chính skill tạo ra không được xem là lệnh đi riêng.
- Thả phím di chuyển dừng đi nhưng vẫn giữ tia; xóa movement buffer. Giữ kiểm tra focus, loading, chat/input, UI, cursor item, chết và root. Arcane Ascendance vẫn khóa di chuyển gốc, kể cả khi bật giữa lúc đang di chuyển/channel.
- Không thay damage, mana, phạt damage khi xoay hoặc rotation modifier. Không ghi Transform và không thêm quét actor mỗi frame. Không đưa diagnostic counters/probe bypass cũ vào DLL release.
- Giữ sửa login Waypoint Traveler’s Rest, Woven Enchanter vô hạn lượt và ẩn banner nhặt đồ của dev.67.

## Kiểm tra

Build Release không warning/error;36 actual-source movement/input checks và202 compiled audits đạt. Regression checks: mouse aim không tạo lệnh đi; initial cast không chạy tới chuột; lệnh đi riêng và các hướng keyboard hoạt động; thả phím giữ channel; scope cleanup khi exception; root/loading/chat/UI/cursor/focus guards;80% speed không cộng chồng.

Theo yêu cầu phát hành ngay của người dùng, không chạy lại native gameplay cho bản khôi phục này. Kết quả native lịch sử dev.20/21 không thay thế nghiệm thu thao tác trên bản hiện tại. ZIP/updater được kiểm tra fixture và tải public xác minh SHA256.

ZIP chỉ gồm cặp DLL, manifest, update.bat, README và release notes; không có helper QA, debug/probe/smoke, log hay profile. Source helper QA được giữ riêng trong tools và không được phân phối trong ZIP.
