# NPC Client và teleport — 1.0.9-dev.15 / protocol 28

Người dùng xác nhận Lightning Blast đã chain đúng; NPC trượt và khựng chỉ xảy ra trên Client. Báo lỗi teleport xảy ra ở mọi map đã thử.

## Thay đổi

- Chỉ ghi nhận animation đã áp dụng sau khi Animator controller và layer tải xong. Thử lại trạng thái đang giữ mỗi frame; tải controller trễ hoặc thay controller không còn làm mất lệnh di chuyển.
- Sau khi animation bản sao sẵn sàng, chặn hàng đợi animation cục bộ ghi đè trạng thái từ Host trên NPC Client còn sống. Host, solo, quá trình khởi tạo và xác chết giữ luồng native.
- Dùng chuyển động thực tế của pose để bổ sung locomotion khi snapshot vẫn Idle; không thay thế trạng thái đánh của Host. Driver bị tắt vẫn được tick theo frame, driver native đang bật không bị tick hai lần.
- Bộ đệm chuyển động NPC thích nghi với jitter trong khoảng 100–250ms; điều chỉnh đồng hồ từ từ để tránh nhảy lùi. Đây là đánh đổi độ trễ hiển thị lấy chuyển động liên tục; không tăng tốc quyết định gameplay của Host.
- Bỏ hạn chế loại map xuất phát khi teleport đến map thường/thị trấn/hub. Sau chuyển map, đợi nhân vật và visual spawn xong, màn loading đóng rồi mới đặt cạnh đồng đội. Hủy khi đổi phiên, mất kết nối hoặc đồng đội rời map.
- Vào lượt Monolith/Arena/Dungeon/Pinnacle đang chạy vẫn chưa hỗ trợ vì phải truyền run context. Báo lỗi đúng lý do thay cho thông báo phải cùng map. Không tuyên bố teleport mọi map đã hoàn thiện.
- Giữ density x5 ngoài Monolith và DPS tối đa 8 skill, mặc định góc trên trái.

## Kiểm nghiệm

Release build: 0 warning, 0 error. 461 kiểm tra managed/network và 16 kiểm tra adapter dùng source thật với API stub đều đạt, gồm controller tải trễ, layer tải trễ, thay controller, tránh lặp đòn đánh, jitter clock và chính sách travel.

Chưa kiểm nghiệm native animation, FPS hoặc co-op hai máy cho bản này. Stub không chứng minh hình ảnh trong Unity đã mượt. Không cài vào game đang chạy, không đóng game và không sửa save. Lightning Blast chain là kết quả người dùng xác nhận trên bản trước, không phải kết quả mới của dev.15.

Cập nhật cả hai DLL trên cả hai máy sau khi thoát game. Kiểm tra Client với NPC vừa spawn, chạy/dừng/quay/đánh/trúng đòn/chết, đổi map và reconnect; thử teleport Town ↔ map thường. Nếu còn lỗi, runtime report có readyAnimationOwners, SuppressedNativeAnimationEvents và bufferDelayMs để phân biệt controller chưa sẵn sàng với xung đột animation hoặc thiếu snapshot.
