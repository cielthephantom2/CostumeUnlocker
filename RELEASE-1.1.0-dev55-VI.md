# TM Last Epoch Co-op Mod 1.1.0-dev.55

Tranquility của Arcane Ascendance (arcas, node19): bỏ hiệu ứng -100% hồi mana khi kỹ năng đang bật. Giữ bonus giảm mana cost20% mỗi điểm và các hiệu ứng còn lại. Chỉ sửa mutator của người chơi local ở Play Offline.

Sửa mô tả để chỉ còn lợi ích giảm mana cost; bỏ dòng Cannot Regenerate Mana ở cả tooltip cũ và V2, giữ layout và dòng hiệu quả mana gốc. Danh sách stat tooltip chỉ lọc tạm trong khi render, được khôi phục kể cả khi native ném exception.

Native audit xác nhận node tạo ManaRegen property18 với more=-1 trong statsWhileActive, cùng danh sách được đọc lúc activation. Correction chạy sau cập nhật cây và trước cast để bao phủ lúc cây tải sớm chưa có local actor. Không sửa BaseMana, không bật hồi mana toàn cục.

Build Release0warnings/errors.79 kiểm tra actual-source Meteor/Arcane (gồm28 kiểm tra Tranquility mới),94 Archmage và132 compiled/native audit đạt. Chưa xác nhận gameplay/render trực tiếp trong game.

Bao gồm toàn bộ dev.54: đã gỡ UI Shortcut, giữ các sửa Meteor, Arcane movement, Archmage/text và hook Monolith. Protocol44, giữ nguyên Core47.

Bổ sung Forgotten Knights: Rank1 100%, Rank2 100%, Rank5 50%, đồng bộ mô tả. Xem [báo cáo](FORGOTTEN-KNIGHTS-dev55-VI.md). Default Meteor đã gỡ.

Đóng game rồi chạy update.bat để cập nhật cả hai DLL. Game đang chạy khi đóng gói nên chưa ghi đè DLL trong Steam.
