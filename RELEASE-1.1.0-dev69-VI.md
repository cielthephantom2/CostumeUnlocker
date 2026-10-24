# TM Last Epoch Co-op Mod 1.1.0-dev.69

Protocol44. Cập nhật cả hai DLL khi game đã đóng.

- Bỏ hoàn toàn hook nhân tốc độ di chuyển Disintegrate với0.8 và bỏ SpeedMultiplier. Channel dùng tốc độ di chuyển bình thường theo stat nhân vật; mô tả skill đã cập nhật.
- Giữ cách điều khiển dev.68: chuột ngắm tia, WASD theo binding/hướng camera native hoặc lệnh đi riêng mới di chuyển; không tự chạy về phía chuột. Thả phím dừng đi nhưng vẫn giữ tia.
- Giữ native root, bao gồm khóa di chuyển của Arcane Ascendance. Không thay damage, mana hoặc tốc độ cast; cast speed không dùng để tăng tốc đi bộ.
- Giữ các thay đổi khác của dev.68.

## Kiểm tra

Build Release không warning/error; 32 actual-source movement/input regression checks và202 compiled audits đạt, xác nhận hook giảm tốc độ và multiplier đã bị loại. ZIP/updater kiểm tra fixture và tải public xác minh SHA256. Không chạy native gameplay cho bản này; game Steam đang chạy.

ZIP chỉ gồm cặp DLL, manifest, update.bat, README và release notes; không có helper QA, debug/probe/smoke, log hay profile. Thoát game trước khi cập nhật.
