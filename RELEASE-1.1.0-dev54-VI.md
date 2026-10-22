# TM Last Epoch Co-op Mod 1.1.0-dev.54

Gỡ toàn bộ UI Shortcut theo yêu cầu: nút trong F4, hotkey, runtime, binding và diagnostics. Mod Settings trở lại nút đầy chiều rộng. File cấu hình Shortcut cũ của người dùng được giữ nguyên nhưng không còn được đọc.

Giữ các sửa Meteor Extinction33%/điểm, Heat Flux2 Frost Claw/điểm và100% mana Frost Claw; Arcane Ascendance cho phép di chuyển; Archmage cùng text ở5điểm: dưới300 max mana +5 spell damage/35% hoàn20%,300–999 +10/70% hoàn40%, từ1000 +15/100% hoàn60%.

Lỗi MelonLoader Empowered → Normal Monolith: bỏ hook trực tiếp IncreaseChanceForSpecificEchoTypes có tham số ref float; chuyển scope Woven Imprint sang EchoWeb.getIslandTypeForNewIsland. Audit native xác nhận hàm ngoài bao quanh consumer trọng số, giữ x2 và cleanup khi exception. Nguyên nhân sâu bên trong cầu nối chưa được xác nhận; cần thử lại chuyển Monolith trong game.

Build Release:0warnings,0errors. Weaver61, Archmage94, Meteor/Arcane51 và audit compiled/native128 kiểm tra đạt. Các fixture không thay thế kiểm tra thực tế trong game. Protocol44, giữ nguyên Core47.

Đóng game rồi chạy update.bat để cập nhật cả hai DLL. Bản phát hành này chưa được chép vào Steam vì game đang chạy khi đóng gói.
