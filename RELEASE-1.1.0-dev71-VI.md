# TM Last Epoch Co-op Mod 1.1.0-dev.71

Protocol44. Cập nhật cả hai DLL khi game đã đóng.

- Minion/companion của nhân vật offline ở stance Aggressive: tăng phạm vi phát hiện và tìm mục tiêu từ x2 lên x5 so với native.
- Áp dụng hệ số lên tổng phạm vi hiện có, giữ các modifier gốc. Ví dụ giới hạn tìm mục tiêu 30 thành150; modifier gốc +50% trở thành tổng7.5 lần base khi tìm mục tiêu.
- Giữ cách chọn mục tiêu, di chuyển và quay về theo người triệu hồi của AI gốc. Không tăng tầm đánh của kỹ năng. Protect, quái địch và minion của người khác giữ nguyên.
- Khôi phục trường native sau mỗi lần tìm mục tiêu, kể cả khi có exception; không tăng chồng qua các lần gọi. Giữ toàn bộ thay đổi dev.70.

Build Release không warning/error;17 actual-source minion checks và205 compiled audits đạt. Kiểm tra x5, ownership/stance, phạm vi hữu hạn/vô hạn, exception và100 lần tìm liên tiếp không tăng chồng. Chưa kiểm tra hành vi minion trực tiếp trong engine ở bản này.

ZIP chỉ gồm cặp DLL, manifest, update.bat, README và release notes; không kèm helper QA, debug/probe/smoke, log hay profile. ZIP/updater được kiểm tra và tải public để xác minh SHA256.
