# Spirit Xylem — dev.55

Xác minh mới:79 kiểm tra engine IL2CPP cho Spirit Xylem/Stardust đã đạt, gồm tooltip parser, equip/stat/skill max points, All Attributes, không stack và more-damage native. [Báo cáo chi tiết](MOD-FEATURES-AND-CODE-AUDIT-dev55-VI.md). Chưa đo hit lên monster hoặc chụp UI render.

Sửa tooltip Attunement Spirit Xylem qua đúng khóa localization; gỡ toàn bộ Default Meteor save repair gây spam. Báo cáo đầy đủ tính năng và rà code: [MOD-FEATURES-AND-CODE-AUDIT-dev55-VI.md](MOD-FEATURES-AND-CODE-AUDIT-dev55-VI.md).

Sửa unique ID445, giữ version1.1.0-dev.55 và protocol44.

## Modifiers mới

- 10–20% Reduced Health Cost of Spells.
- 10–20% more Damage to Bosses and Rares when above half Mana.
- 20–10% less Damage Taken from Bosses and Rare Enemies while below half Mana.
- Nếu Attunement > Intelligence: +3–4 level cho Attunement Skills, +10–20 Attunement, +3–6 Health và Mana mỗi Attunement.
- Nếu Intelligence > Attunement: +3–4 level cho Intelligence Skills, +10–20 Intelligence, +1 Spell Damage mỗi2 Intelligence.
- Bằng nhau: không có nhánh có điều kiện nào. So sánh thuộc tính trước bonus có điều kiện của chính Spirit Xylem, tránh bonus tự giữ/đổi nhánh. Các phép tính per-attribute dùng thuộc tính cuối sau bonus.

Thay toàn bộ unique modifiers theo danh sách trên; giữ implicit, tên/lore, ID, base/subtype, yêu cầu/cost, primordial status, LP và affix legendary. Dòng reduced Mana Cost gốc không nằm trong danh sách mới. Không đổi byte ID, roll hoặc sửa save item.

## Cách áp dụng

Ba modifier chung giữ nguyên object native: giảm health cost (AbilityProperty253/2), more damage (GlobalConditionalDamage40) và less damage taken (PlayerProperty598). Điều kiện nửa mana do game xử lý; không đổi damage/defense toàn cục.

Hai nhánh đọc đúng trang bị có StatsApplied=true của người chơi offline local. Bỏ qua bản trong inventory/stash, item bị native vô hiệu hóa hoặc vượt primordial cap. Unique rolls gốc được giữ: skill level dùng slot5, attribute slot6, HP/MP-per-Attunement slot2. Nhánh INT và Attunement dùng chung roll cho cùng loại bonus vì loại trừ nhau. Native getValue xử lý giá trị roll; tăng hiệu lực item đi qua globalStatModifier gốc.

Stat nhánh được đưa vào trước ApplyCoreAttributeModifiers; native tính thuộc tính và các hiệu ứng phát sinh, HP/MP, spell damage, bonus skill points. Nguồn nhánh trước được gỡ trước mỗi lần tính lại để không cộng dồn và không đưa bonus vào phép so sánh. Native event cập nhật khi đổi đồ, roll, passive hoặc buff; không quét toàn map hoặc polling inventory mỗi frame. Dọn nguồn khi đổi scene/actor và khôi phục registry khi ngừng offline.

Tooltip dùng cả modifier native và hai đoạn mô tả có roll native, kèm giải thích phép so sánh ở Alt text. Không tạo cosmetic hoặc item ID mới.

## Kiểm tra

27 actual-source fixture checks: min/max roll hai nhánh, switch/tie không tự giữ nhánh, không cộng dồn20 lần recalculation, giữ nguyên ID/roll/LP/legendary affix, loại native-disabled item, unequip, actor swap, cleanup/online, item effect, malformed rolls. Fixture giả lập API game; gameplay và tooltip render cần kiểm tra trực tiếp.

Native asset evidence: research/dev55-spirit-xylem-assets.json (UniqueList trong resources.assets, đọc bằng typetree từ DLL của game); native method evidence trong research/dev55-xylem-*-native.txt.
