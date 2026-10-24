# TM Last Epoch Co-op Mod 1.1.0-dev.63

Protocol44. Cập nhật cả hai DLL trên Host và Guest khi game đã đóng.

- Spirit Xylem: +3–4 All Skills; +10–16 Attunement; +3–6 Health and Mana per Attunement; +1 Spell Damage per25 Maximum Mana. Giữ hai hiệu ứng gốc tăng damage lên boss/rare khi trên nửa Mana và giảm damage nhận từ boss/rare khi dưới nửa Mana.
- Bỏ +10–20 Intelligence, ATT bằng INT, Spell Damage theo INT và giảm health cost. HP/Mana theo ATT cộng dồn với +2 Mana/ATT gốc. Spell Damage tự cập nhật theo tổng maximum Mana, không theo current Mana, không tăng damage melee.
- Mỗi dòng tooltip có chú thích riêng đúng khoảng roll/cơ chế; bỏ ghi chú ATT=INT cũ lặp lại.
- Disintegrate dùng native mobile channel, cho phép di chuyển với80% tốc độ bình thường; cast speed không tăng tốc đi bộ. Arcane Ascendance trả về khóa di chuyển và mô tả gốc; giữ thay đổi Tranquility regen riêng.
- Giữ sửa loading echo mật độ cao, aggro x3, Prophecy chia thưởng qua nhiều frame và tự host/join của dev.62. Loại debug/probe/smoke/test tools khỏi DLL/ZIP release.

## Kiểm tra

Build Release không warning/error. 1.053 managed/TCP checks và15 updater fixture checks đạt. 46 actual-source Xylem,100 Meteor/Tranquility,14 Disintegrate và195 compiled audits đạt. Native profile QA riêng:176 kiểm tra đạt, gồm roll min/max, skill points, ATT/INT độc lập, HP/MP, native +2 Mana/ATT cộng dồn, tự cập nhật maximum Mana và Spell Damage, không cộng chồng sau100 lần tính lại, tháo item và từng chú thích.

Native Disintegrate getters và speed consumer xác nhận80% sau100 update và phục hồi tốc độ khi ngừng channel; chưa nghiệm thu trực quan vừa giữ tia vừa đi trong combat. Hai hiệu ứng boss/rare xác nhận modifier gốc được trang bị, chưa đo damage thực chiến. Tooltip kiểm tra bằng native parser, chưa xác nhận ảnh render.

## Cài đặt

Thoát game, chạy update.bat hoặc giải nén ZIP vào thư mục game. ZIP chỉ có Mods/LastEpochLAN.dll, UserLibs/LastEpochLAN.Core.dll, latest.json, update.bat, README và release notes; không chứa helper QA, log hay profile. Updater backup và xác minh SHA256.
