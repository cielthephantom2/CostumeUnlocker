# TM Last Epoch Co-op Mod 1.1.0-dev.58

Protocol44. Cập nhật cả Mods/LastEpochLAN.dll và UserLibs/LastEpochLAN.Core.dll khi game đã đóng.

- Bỏ tăng mật độ quái x5 ở mọi map ngoài Monolith, gồm campaign và Dungeon như Temporal Sanctum. Các phòng ngoài Monolith chạy nguyên native roll, không sửa spawnerDensityModifier/intendedSpawnerDensity hoặc chặn sinh quái qua policy Monolith.
- Monolith giữ mật độ max(1, Corruption/100), giữ modifier gốc của phòng và khôi phục trường sau mỗi lần roll để không nhân chồng. Không đổi save hay tiến trình.
- Giữ Enemy minimap dots, Spirit Xylem, Reset Class trong F4 và Woven Potential từ dev.57.

## Kiểm tra

Release build0 warnings/errors. Core1053 checks,21 kiểm tra actual-source DensityFixture, compiled/native audit185 và updater fixture15 đều đạt. Fixture xác minh ngoài Monolith không ghi bất kỳ trường placement nào, kể cả khi còn ActiveRun cũ hoặc singleton đã unload; Monolith giữ hệ số ở corruption0/100/250/500/1000, rollback khi native fail và100 lần reroll không cộng dồn.

Thay đổi nhằm giảm tải sinh quái/preload ở Dungeon; chưa đo lại thời gian tải Temporal Sanctum hoặc chạy kiểm tra gameplay A/B. Không khởi chạy game QA riêng trong khi người dùng đang chơi. Các kiểm tra native89 của dev.57 là bằng chứng phiên bản trước, không được coi là đã kiểm tra native dev.58.

## Cài đặt

Thoát game hoàn toàn, dùng update.bat hoặc copy đúng cặp DLL trong ZIP. Updater xác minh SHA256 và backup. Không kèm helper QA. Protocol vẫn44.
