# Gold, Vilatria, hồi phục và Respecialize Class — dev.55

Sửa tooltip Attunement Spirit Xylem qua đúng khóa localization; gỡ toàn bộ Default Meteor save repair gây spam. Báo cáo đầy đủ tính năng và rà code: [MOD-FEATURES-AND-CODE-AUDIT-dev55-VI.md](MOD-FEATURES-AND-CODE-AUDIT-dev55-VI.md).

Cập nhật mới: sửa menu Respecialize Class tại Lerinne; gỡ Vilatria visual override; Stardust tối đa 1 điểm, 1% more damage mỗi 10 Maximum Mana. [Chi tiết và kiểm tra](LERINNE-STARDUST-dev55-VI.md).

Giữ version 1.1.0-dev.55 và protocol44.

- Auto Gold: giữ toàn map + delay0.5s. Sửa kiểm tra khoảng cách trong hàm nhặt native: chỉ đặt tạm tọa độ ground record cần nhặt tại người chơi trong transaction; khôi phục vị trí nếu thất bại. Cộng gold, xóa/pool gold và save vẫn do game xử lý; không cộng tiền trực tiếp.
Tùy chọn Vilatria VFX đã được gỡ hoàn toàn theo yêu cầu; game xử lý VFX gốc.
- Bỏ buff increased mana/health regen50%. Thay bằng x2 base regen tại bước native khởi tạo stat, trước flat từ đồ/passive và các increased/more modifier. Base8 →16; ví dụ base8, flat từ đồ4, increased50%: (16+4)×1.5=30, không phải (8+4)×1.5×2. Dọn hai buff cũ khi vào nhân vật.
- Chronomancer Lerinne: thêm dòng có icon `Respecialize Class` bên cạnh hai dòng respec gốc. Chọn Mage/Rogue/Primalist/Sentinel/Acolyte, xác nhận rồi trở về character selection. Giữ ID nhân vật, level/XP, đồ/gold, quest, faction, Weaver, waypoint và Monolith; reset mastery, passive, specialisation và thanh skill. Điểm passive cũ được hoàn lại. Game nạp model/class mới khi vào lại nhân vật.
- Class change đợi native lưu/thoát nhân vật và tải xong danh sách offline; sao lưu file trong `Saves` vào `UserData/LastEpochLAN/backups/class-respec-*` trước khi sửa. Dùng save native và đợi hoàn thành; lỗi lưu khôi phục các field class trong bộ nhớ. Không sửa nhân vật nào nếu chưa chọn/xác nhận trong menu.

## Kiểm tra

Build Release0warnings/errors; Auto Pickup39 actual-source checks; class/regen/Vilatria46 actual-source checks; Core1053 checks; audit169 checks/198 patch classes. Fixture mô phỏng lớp game; chưa xác nhận giao diện/gameplay trực tiếp trong game.

## Vào game thử

1. Để gold rơi xa, chờ0.5s và kiểm tra tiền tăng đúng một lần.
3. Kiểm tra hồi mana/máu cơ bản (base8 thành16), rồi trang bị đồ có regen để xem modifier vẫn tính bình thường.
4. Nói chuyện Lerinne → Respecialize Class → lớp mới → xác nhận. Vào lại nhân vật, kiểm tra level/đồ/quest/Monolith, point passive hoàn lại, mastery và skill chọn lại.
