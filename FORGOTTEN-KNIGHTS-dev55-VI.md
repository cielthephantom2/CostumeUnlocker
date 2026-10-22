# Forgotten Knights — dev.55 / protocol44

Sửa giá trị thực tế và mô tả ba rank:

| Rank | Bonus | Cũ | Mới |
|---|---|---|---|
|1|Nemesis có Egg of the Forgotten|+40%|+100%|
|2|Cơ hội nhận gấp đôi Stability khi hoàn thành Echo|+10%|+25%|
|5|Cơ hội rơi thêm Temporal Keystone từ Harbingers, gồm Aberroth|20%|50%|

Gán trực tiếp ba field native của ForgottenKnights sau ToggleRank, với kiểm tra thành viên, rank đã đạt, trạng thái bật/tắt bonus và actor local. Tick kiểm tra bổ sung khi dữ liệu faction đã được tải trước lúc nhận diện người chơi. Giá trị tuyệt đối không cộng dồn khi cập nhật lại. Game vẫn thực hiện RNG, tính thưởng và giới hạn Stability của nó.

Text sửa trực tiếp tỷ lệ trong mô tả gốc, giữ dấu+, định dạng, dòng và nội dung còn lại. Rank2 có25% cơ hội nhân đôi completion gốc của faction. Bản cập nhật Stability bổ sung x2 Echo Rewards và Temporal Keystone, xem TIMELINE-STABILITY-X2-dev55-VI.md.

Build Release0warnings/errors;59 fixture actual-source và135 audit compiled/native đạt. Đã xác nhận switch native và hằng số gốc40%/10%/20%, cùng các field tương ứng0x88/0x8C/0x98. Chưa kiểm thử gameplay hoặc render trực tiếp trong game.

Giữ version1.1.0-dev.55, protocol44 và Core47. Không có Default Meteor trong DLL này. Game đang chạy khi đóng gói nên chưa ghi đè cặp DLL Steam. Đóng game rồi chạy update.bat để cập nhật dù version hiển thị vẫn giống nhau.
