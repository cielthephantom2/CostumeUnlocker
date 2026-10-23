# TM Last Epoch Co-op Mod 1.1.0-dev.56

Protocol44. Cập nhật cả Mods/LastEpochLAN.dll và UserLibs/LastEpochLAN.Core.dll. Chơi Offline.

- Spirit Xylem: hai nhánh Attunement/Intelligence với skill levels, thuộc tính và HP/MP hoặc spell damage. +1 Spell Damage mỗi2 INT. Sửa tooltip mất đoạn Attunement do localization ghi đè, tính thêm All Attributes. Giữ item ID/roll/LP/legendary affix.
- Stardust Meteor: max1 điểm, mỗi10 Maximum Mana cho1% more damage mọi hệ, nhân với modifier khác.1000 max mana→x2. Phân bổ cũ4→1 hoàn3 điểm.
- Lerinne: sửa Respecialize Class với5 class, giữ level/đồ/tiến trình; reset mastery/passive/skill. Backup trước khi lưu, chuyển qua character selection rồi vào lại.
- Sửa gold Auto Pickup toàn map sau0.5s. Giữ native currency/storage và tùy chọn từng loại.
- Thay +50% increased regen bằng x2 base HP/MP regen; không nhân maxHP/maxMP hoặc flat regen từ đồ.
- Gỡ Default Meteor save repair gây spam log và toàn bộ Vilatria visual override.
- Dọn helper không còn caller. Giữ144 release hook classes;66 shared-world/QA hook classes bị policy khóa. Room hiện hỗ trợ Shared Stash, Loot Filters, Ladder; không bật shared-world combat/travel.

## Xác minh

Build Release0 warnings/errors. Bản chức năng dev.55 trước nâng version đã đạt79/79 kiểm tra engine IL2CPP trong profile QA riêng: tooltip localization/parser, native equip/stat/core attribute, skill max points, All Attributes, unequip,20 lần recalc không stack và Stardust MORE trên Fire/Cold/Physical ở0/500/1000/2000 max mana.

Core1053, Spirit33, Meteor/Arcane106, Archmage94, class/regen47, pickup39, Forgotten Knights59, Fortune40, stability45, stash priority12 và audit189 đã đạt ở các vòng kiểm tra gần nhất. Bản dev.56 đã chạy lại79/79 native engine checks, Core1053, audit189, Spirit33, Meteor106, class47 và updater15. Bằng chứng trong validation/dev56-*.

Chưa đo combat hit lên quái hoặc chụp tooltip render; không coi các kiểm tra này là gameplay acceptance của mọi tính năng trong mod.

[Danh sách đầy đủ tính năng và rà code](MOD-FEATURES-AND-CODE-AUDIT-dev55-VI.md). [Chi tiết Spirit Xylem](SPIRIT-XYLEM-dev55-VI.md). [Chi tiết Stardust/Lerinne](LERINNE-STARDUST-dev55-VI.md).

## Cài đặt

Thoát game hoàn toàn, chạy update.bat hoặc copy đúng cặp DLL từ ZIP. Updater kiểm tra hash và sao lưu. Giữ protocol44, không cần đổi save nhân vật.
