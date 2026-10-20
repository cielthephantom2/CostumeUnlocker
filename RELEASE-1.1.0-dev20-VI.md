# Bản 1.1.0-dev.20 — Disintegrate WASD khi channel

Đóng game trên Host và mọi Client rồi chạy `update.bat`. Cập nhật cả hai DLL. Protocol 35; dùng Core đã chạy cùng candidate dev.19/20, logic source Core không đổi.

- Nối đường keyboard native ProcessKeyboardMovement → SetInput với movement khi đang channel Disintegrate local offline. Giữ binding của game và hướng theo camera, hướng chéo được native chuẩn hóa.
- Thả phím cuối dừng di chuyển nhưng vẫn channel. Xóa movement buffer để tránh phát lại lệnh đi sau channel.
- Bao gồm các sửa dev.17–19: không tự chạy theo hướng ngắm; scope chặn cập nhật đích từ aim, vẫn cập nhật tia và nhận lệnh di chuyển riêng.
- Giữ guard focus/loading/UI/inputfield/cinematic/root/dead/cursor. Không ghi Transform hoặc sửa mana/damage trong production.
- Giữ tối ưu dev.15, mật độ quái và bỏ party HP scaling. Encounter vẫn tạm gác.

## Kiểm chứng và giới hạn

Build candidate Release đạt 0 warning/error; diff check đạt. Sandbox riêng gọi keyboard processor native cho bốn hướng và hai đường chéo: cả sáu di chuyển đúng hướng, giữ channel và trôi 0 sau thả input. Aim A/B tái hiện tự đi khi tắt lọc (7 đơn vị), drift 0 khi bật lọc; lệnh đi riêng, mana, aim và dừng skill đạt. Đây là gọi API native, chưa kiểm tra bàn phím/chuột vật lý hoặc Host+Client.

Feature observations passed=true, keyboardPassed=true. Full native gate vẫn failed với 76 runtime errors khởi tạo/service/assets/identity/actor-null; không coi kết quả từng tính năng là toàn bộ mod đạt. Fixture QA riêng có chỉnh level/mana/input binding và bypass focus cho cửa sổ ẩn; guard production giữ nguyên. Không dùng save thật. Lượt fixture đầu thiếu input binding đã thất bại trước lượt chạy lại.

SHA256 Mod: `9C7EE0865A995B77C0C8D3321B4633335748B891407FCE7C9638DDDE0A03C359`

SHA256 Core: `DC51937DA87B1459DD4F8BF429E4D654C9A3D9F821CF53F1CC5AF4DE38222B9D`
