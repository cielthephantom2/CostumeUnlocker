# TM Last Epoch Co-op Mod 1.1.0-dev.72

Protocol44. Cập nhật cả hai DLL khi game đã đóng.

- Sửa lỗi đỏ NullReferenceException trong TargetFinder.updateTarget khi minion Aggressive tìm mục tiêu với giới hạn khoảng cách rỗng.
- Đọc nullable qua backing field native: giá trị rỗng giữ hành vi không giới hạn; giá trị hữu hạn được đọc đúng dạng Single và nhân x5. Scope lưu float gốc và khôi phục đúng sau mỗi lượt, kể cả khi có exception.
- Giữ phạm vi phát hiện/tìm mục tiêu x5 và điều kiện offline, ownership, Aggressive; ally/self giữ nguyên. Không thay đổi host/join/retry.

## Kiểm tra

Build Release:0 warning/error;20 actual-source fixture checks và206 compiled audits đạt.

Hồi quy native trên bản sửa trước khi nâng version: bản cũ tạo100 lỗi trampoline trong100 lượt nullable rỗng; bản sửa chạy200 lượt với0 lỗi. Observer xác nhận100 lượt rỗng giữ không giới hạn,100 lượt hữu hạn có khoảng cách30 được mở rộng thành150 ngay trong lúc tìm mục tiêu và khôi phục30 sau mỗi lượt.9 kiểm tra native đạt. QA dùng object tạm với eligibility được cưỡng chế riêng để cô lập lỗi nullable; chưa kiểm tra minion đuổi/đánh quái thực tế.

ZIP/updater được kiểm tra và tải public xác minh SHA256. ZIP chỉ chứa cặp DLL, manifest, update.bat, README và release notes; không kèm helper QA, debug/probe/smoke, log hoặc profile.
