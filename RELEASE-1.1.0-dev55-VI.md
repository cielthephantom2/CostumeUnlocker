# TM Last Epoch Co-op Mod 1.1.0-dev.55

Sửa tooltip Attunement Spirit Xylem qua đúng khóa localization; gỡ toàn bộ Default Meteor save repair gây spam. Báo cáo đầy đủ tính năng và rà code: [MOD-FEATURES-AND-CODE-AUDIT-dev55-VI.md](MOD-FEATURES-AND-CODE-AUDIT-dev55-VI.md).

Cập nhật mới: sửa menu Respecialize Class tại Lerinne; gỡ Vilatria visual override; Stardust tối đa 1 điểm, 1% more damage mỗi 10 Maximum Mana. [Chi tiết và kiểm tra](LERINNE-STARDUST-dev55-VI.md).

Spirit Xylem: sửa modifiers và tooltip thành hai nhánh Attunement/Intelligence, giữ item ID/roll/LP và affix legendary. Xem [chi tiết và kiểm tra](SPIRIT-XYLEM-dev55-VI.md).

Bổ sung: sửa gold toàn map0.5s, x2 base hồi mana/máu và Respecialize Class tại Lerinne. Chi tiết, sao lưu và checklist: [GOLD-REGEN-CLASS-dev55-VI.md](GOLD-REGEN-CLASS-dev55-VI.md).

Tranquility của Arcane Ascendance (arcas, node19): bỏ hiệu ứng -100% hồi mana khi kỹ năng đang bật. Giữ bonus giảm mana cost20% mỗi điểm và các hiệu ứng còn lại. Chỉ sửa mutator của người chơi local ở Play Offline.

Sửa mô tả để chỉ còn lợi ích giảm mana cost; bỏ dòng Cannot Regenerate Mana ở cả tooltip cũ và V2, giữ layout và dòng hiệu quả mana gốc. Danh sách stat tooltip chỉ lọc tạm trong khi render, được khôi phục kể cả khi native ném exception.

Native audit xác nhận node tạo ManaRegen property18 với more=-1 trong statsWhileActive, cùng danh sách được đọc lúc activation. Correction chạy sau cập nhật cây và trước cast để bao phủ lúc cây tải sớm chưa có local actor. Không sửa BaseMana, không bật hồi mana toàn cục.

Build Release0warnings/errors.79 kiểm tra actual-source Meteor/Arcane (gồm28 kiểm tra Tranquility mới),94 Archmage và132 compiled/native audit đạt. Chưa xác nhận gameplay/render trực tiếp trong game.

Bao gồm toàn bộ dev.54: đã gỡ UI Shortcut, giữ các sửa Meteor, Arcane movement, Archmage/text và hook Monolith. Protocol44. Bản bổ sung CoF x20 dùng Core mới; cập nhật cả hai DLL theo manifest hiện tại.

Bổ sung Forgotten Knights: Rank1 100%, Rank2 25%, Rank5 50%, đồng bộ mô tả. Xem [báo cáo](FORGOTTEN-KNIGHTS-dev55-VI.md). Default Meteor đã gỡ.

Bổ sung x2 cả hai dòng Stability trong Echo Rewards mọi loại Echo và x2 Temporal Keystone, đồng bộ tooltip/thanh Stability. Xem [báo cáo Stability](TIMELINE-STABILITY-X2-dev55-VI.md).

Đóng game rồi chạy update.bat để cập nhật cả hai DLL. Game đang chạy khi đóng gói nên chưa ghi đè DLL trong Steam.


Bổ sung faction/pickup: CoF Rank7 x15 Experimental, Rank10 +300% boss loot riêng, Rank11 Rare→Exalted50%; The Woven Rank7 reforge30%, Rank9 Memory Amber50%. Auto pickup quay lại bộ quét cũ với bán kính cố định5, nhặt cả khi đứng yên; bỏ trigger bổ sung. World Assist đã gỡ toàn bộ. Xem [báo cáo](FACTIONS-PICKUP-dev55-VI.md).

Sửa pickup và gỡ World Assist: xem [báo cáo hiện tại](NATIVE-PICKUP-dev55-VI.md).

## Combat Assist khi mở UI

Khi Start, Assist tiếp tục chạy mỗi frame khi F1/F4/menu/chat/các panel mở, không tự nhả channel do UI và F1 không khóa input toàn game. Xem [chi tiết và kiểm chứng](ASSIST-UI-CONTINUE-dev55-VI.md). Giữ Auto Pickup nhẹ 5Hz/5 ứng viên/1 lệnh, Woven Echoes và bỏ log. Version/protocol không đổi.

## Icon người chơi trên minimap

Thay bằng chấm xanh dương có viền giống NPC, lớn hơn50% đường kính trên minimap. Tab overlay giữ icon gốc. Xem [chi tiết](PLAYER-MINIMAP-DOT-dev55-VI.md).

## Auto Pickup phản hồi theo trigger native

Thay bộ quét5Hz/5ứng viên bằng callback trigger gold/potion native; Keys/Materials/Woven Echoes nhận qua native pickup/vacuum/storage. Giữ radius5, không đợi cursor đi qua loot xa, không yêu cầu di chuyển.44 fixture và169 audit checks đạt; chưa test physics/tốc độ trong game. [Chi tiết hiện tại](NATIVE-PICKUP-dev55-VI.md).

## Chấm người chơi minimap tăng gấp đôi

Scale chấm xanh dương từ0.45 lên0.90 (đường kính gấp3 chấm NPC). Giữ minimap góc màn hình, Tab icon gốc và các thay đổi trước. Tra cứu Mesembria: mỗi ô Prophecy tối đa99 completed charges; mỗi lần trigger dùng tối đa6 khi tích từ30. Không thay đổi cơ chế charge.

## Khôi phục cơ chế charge Prophecy gốc

Đã gỡ hoàn toàn code dùng hết charge, sửa helper/UI charge và phần thử nghiệm thông báo. Prophecy dùng lại ngưỡng tiêu thụ và thông báo gốc của game. Giữ các thay đổi mod có trước phần charge. Build0 warnings/errors;170 compiled/native audit checks pass.

## Auto Pickup cách cũ + delay0,5s

Tự nhận đồ mới rơi toàn map sau0,5giây, giữ6loại đã bật và potion đầy để lại; bỏ radius5/native trigger bridge, không thêm chia lượt. F4/changelog đồng bộ.35 fixture và169 audit checks đạt; chưa đoFPS hoặc xác nhận gameplay mới. [Chi tiết](NATIVE-PICKUP-dev55-VI.md).
