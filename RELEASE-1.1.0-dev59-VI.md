# TM Last Epoch Co-op Mod 1.1.0-dev.59

Protocol44. Cập nhật cả Mods/LastEpochLAN.dll và UserLibs/LastEpochLAN.Core.dll khi game đã đóng.

Spirit Xylem được thay bằng các hiệu ứng không phụ thuộc so sánh ATT/INT:

- +3–4 All Skills.
- Your Attunement is equal to your Intelligence: ATT bằng INT cuối sau bonus+10–20 INT, kể cả khi phải giảm ATT gốc.
- +10–20 Intelligence.
- +3–6 Health và Mana mỗi Attunement.
- +1 flat Spell Damage mỗi Intelligence (không còn chia2).

Bỏ reduced health cost và hai dòngIf. Giữ hai hiệu ứng boss/rare damage/defense theo mana, implicit, item identity, roll, LP và affix. Mana mới cộng với +2 Mana/ATT gốc, thành +5–8 Mana/ATT trước các modifier khác. Giữ mật độ gốc ngoài Monolith từdev58; Monolith vẫn theo Corruption.

## Cập nhật bố cục tooltip (giữ nguyên dev.59)

Tách năm hiệu ứng Spirit Xylem thành năm mục native riêng. Mỗi Range nằm ngay dưới hiệu ứng tương ứng: All Skills → Range 3–4; Intelligence → Range 10–20; Health/Mana per Attunement → Range 3–6. Hai hiệu ứng boss/rare vẫn giữ nguyên. Không thay đổi công thức stat, roll hoặc version.

Bản sửa bố cục: Release build 0 warnings/errors, fixture Spirit 37 và compiled/native audit 185 PASS. Chưa chạy lại game QA hoặc xác nhận hình render cho bản sửa bố cục; 152 kiểm tra native dưới đây thuộc bản dev.59 trước sửa bố cục.

## Kiểm tra

Release build0 warnings/errors. Spirit actual-source fixture34, Core1053, density fixture21, compiled/native audit185 và updater fixture15 đều PASS. Native IL2CPP runner trên profile/nhân vật QA riêng kiểm tra152 assertions: tooltip parser, native equip mods, INT/ATT, all-skill points cho Fireball/Thorn Totem/Puncture/Rive, HP/MP, cộng Mana gốc, damage builder,100refreshes và unequip; gồm regression Meteor/Stardust/Woven.

Ví dụ native: INT nền50 +roll20→70INT/70ATT; roll6 cho420HP và420Mana từitem, cộng140Mana gốc từATT thành560Mana doATT. Spell damage builder100→170, không tăng melee. Có kiểm tra All Attributes0/5 và ATT>INT/<INT/tie trước item. Phạm vi là native consumers/damage builder, chưa đo hit lên quái hoặc chụp tooltip render.

## Cài đặt

Thoát game, dùng update.bat hoặc copy đúng cặp DLL trong ZIP. Updater kiểm tra SHA256 và backup. Không kèm helper QA. Cập nhật cả hai máy trước khi kết nối.
