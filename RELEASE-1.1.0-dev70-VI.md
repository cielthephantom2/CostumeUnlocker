# TM Last Epoch Co-op Mod 1.1.0-dev.70

Protocol44. Cập nhật cả hai DLL khi game đã đóng.

- Disintegrate — Extension: tăng range từ12% lên30% mỗi điểm. Giữ tối đa3 điểm:0/30/60/90% increased range từ node.
- Áp dụng vào increasedRange của mutator theo allocation thực tế; sửa cả khi cập nhật cây và trước cast để tránh bỏ sót lúc load. Không cộng chồng sau nhiều lần cast, twin beam hoặc tính lại cây; respec về0 bỏ đúng bonus node.
- Cập nhật giá trị và mô tả tooltip ở cả legacy/V2, giữ format và hậu tố per point. Các nguồn range khác vẫn do native consumer xử lý.
- Giữ di chuyển Disintegrate ở tốc độ bình thường, chuột chỉ ngắm và WASD/lệnh đi riêng điều khiển di chuyển. Giữ toàn bộ thay đổi dev.69.

## Kiểm tra

Build Release không warning/error;53 actual-source checks (32 movement/input,21 Extension) và204 compiled audits đạt. Đối chiếu asset Global Tree Data: dig5/node25, Disintegrate Range, max3; native updateMutator dùng hệ số0.12 và field increasedRange. Xác nhận nguồn contribution riêng của node, tính lại không cộng chồng, respec và cả hai tooltip paths.

Game Steam đang chạy nên chưa đo chiều dài tia hoặc render tooltip trực tiếp bằng engine trên bản này. ZIP/updater được kiểm tra fixture và tải public xác minh SHA256.

ZIP chỉ gồm cặp DLL, manifest, update.bat, README và release notes; không có helper QA, debug/probe/smoke, log hay profile. Thoát game trước khi cập nhật.
