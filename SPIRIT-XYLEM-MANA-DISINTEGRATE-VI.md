# Spirit Xylem và channel movement — 2026-10-24

## Nội dung hiện tại

Spirit Xylem có 6 hiệu ứng:
- +3–4 All Skills; chú thích riêng Range: 3 to 4.
- +10–16 Attunement; chú thích riêng Range: 10 to 16.
- +3–6 Health and Mana per Attunement; chú thích range và cộng dồn +2 Mana/ATT gốc.
- +1 Spell Damage per 25 Maximum Mana; chú thích dùng tổng maximum Mana, không dùng current Mana. Ví dụ 1000 maximum Mana = +40 Spell Damage.
- Giữ nguyên native 10–20% more damage tới boss/rare khi trên nửa Mana.
- Giữ nguyên native 10–20% less damage nhận từ boss/rare khi dưới nửa Mana.

Đã bỏ INT bonus, ATT=INT, spell damage theo INT và giảm health cost. Không sửa ID, rolls, LP hoặc affix của item cũ. Mỗi dòng custom có localization Alt riêng; không còn lặp ghi chú ATT=INT.

Disintegrate dùng native mobile-channel path, đổi hướng di chuyển/target và tốc độ80% normal movement. Không dùng cast speed để tăng tốc đi bộ. Arcane Ascendance đã bỏ hook sửa roots và description; giữ khóa di chuyển gốc. Tranquility regen là thay đổi khác, vẫn giữ.

## Kiểm tra

- Build Release: 0 warnings, 0 errors. Phát hành 1.1.0-dev.63 theo yêu cầu nâng version/upload tiếp theo.
- 46 kiểm tra actual-source Xylem; 100 Meteor/Tranquility; 14 Disintegrate.
- 195 compiled audits, gồm loại debug/probe/smoke khỏi release DLL.
- Native lượt đầu:151 kiểm tra đạt cho stat/attribute/native tooltip parser và Meteor.
- Native lượt hai:164 kiểm tra đạt, thêm từng Alt text và Disintegrate native getters/speed80% sau100 lần update, phục hồi tốc độ.
- Các kiểm tra speed/getters không thay cho quan sát thực tế vừa giữ tia vừa đi trong combat. Chưa xác nhận hình tooltip render.
- Native lượt cuối:176 kiểm tra đạt, gồm12 trường hợp ApplyExternalStats tự cập nhật maximum Mana và Spell Damage,8 ghi chú đúng cho bốn dòng ở roll min/max. Không có ERROR/Exception trong log Melon. Kết quả: validation/xylem-mana-native.json.
- Đã backup/copy cặp DLL vào Steam và xác minh SHA256; chi tiết validation/xylem-mana-steam.json. Không copy helper QA.

Bản đầu kiểm tra ở candidate dev62; bản phát hành dev63 có manifest latest.json và validation/dev63-native.json, validation/dev63-package.json. QA chỉ chạy trong research/item-node-native-runtime, GuestProfile riêng. Không copy helper/test DLL vào Steam.
