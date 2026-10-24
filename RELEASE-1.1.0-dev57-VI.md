# TM Last Epoch Co-op Mod 1.1.0-dev.57

Protocol44. Cập nhật cả Mods/LastEpochLAN.dll và UserLibs/LastEpochLAN.Core.dll khi game đã đóng. Chơi Offline.

- Sửa Enemy minimap dots: giữ texture/sprite qua quá trình dọn asset khi tải cảnh; tạo lại sprite đã mất và khôi phục icon bị gỡ khỏi native minimap. Không tạo marker trùng.
- Spirit Xylem: bỏ khoảng min/max dư thừa, chỉ hiển thị số roll thực tế. Giữ chính xác +1 Spell Damage mỗi2 Intelligence; bonus dùng native Spell Damage tag. Giữ item identity/roll/LP và hai nhánh so sánh thuộc tính.
- Reset Class chuyển từ NPC sang F4 → Mod Settings → Reset Class. Chọn Mage/Rogue/Primalist/Sentinel/Acolyte, xác nhận rồi về màn chọn nhân vật; sao lưu trước khi đổi dữ liệu và chờ native save hoàn tất. Giữ level/đồ/tiến trình; reset mastery/passive/skill/actionbar.
- Woven Potential: 1 LP →2 LP:50%, fail về0; 2 LP →3 LP:25%, fail về1; 3 LP →4 LP:25%, fail về1. Giữ native crafting eligibility/tiêu hao, item identity và serialization.

## Kiểm tra

Release build0 warnings/errors; Core1053, Spirit35, class/regen45 và compiled/native audit185 kiểm tra đạt. Native QA dùng profile và nhân vật riêng: kiểm tra tooltip parser, damage builder đúng +1 Spell Damage mỗi2 INT, skill points, stat refresh/unequip, Stardust, Woven RNG boundaries/serialization và icon quái qua forced UnloadUnusedAssets, mất registration, hồi phục, chống trùng và cleanup. Bằng chứng dev57-native-qa.json và dev57-native-assets.json trong validation.

Luồng native Reset Class đã xác minh trên nhân vật QA ở lượt trước: exit, backup, native flush và save file sau đổi lớp; fixture actual-source kiểm tra bảo toàn dữ liệu nhân vật đã phát triển, rollback và confirmation gates. Chưa nhìn screenshot minimap render hoặc đo hit lên quái; không coi native object checks là kiểm thử gameplay đầy đủ.

## Cài đặt

Thoát game hoàn toàn, dùng update.bat hoặc copy đúng cặp DLL từ ZIP. Updater xác minh SHA256 và backup. Không kèm helper QA. Protocol vẫn44.
