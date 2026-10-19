# Co-op hotfix 1.0.9-dev.22 — protocol 30

## Nội dung sửa

- Projectile/skill snapshot của người chơi từ xa bật cập nhật vị trí mỗi khung hình, cùng cơ chế dự đoán ngắn đang dùng cho projectile NPC. Trước đây luồng người chơi còn gán vị trí trực tiếp theo gói tin nên dễ giật. Không thay đổi cơ chế chuyển động/animation NPC.
- Nemesis: callback native với chủ nhân `null` thực tế không sinh thưởng cho người hỗ trợ. Thay bằng bộ sinh loot native, đọc hệ số guest của game: vật phẩm x6, vàng x3 trên bảng rơi cơ bản của enemy. Đây là loot hỗ trợ; bộ vật phẩm/Empower cá nhân vẫn thuộc người kích hoạt encounter. Người hỗ trợ không bị tiêu hao bộ vật phẩm Nemesis cá nhân.
- Nemesis: xử lý receipt trước bước tải prefab/cập nhật visual; encounter đã bị xóa vẫn chuyển được kết quả trận đấu. Host giữ descriptor cuối trong tombstone nếu đối tượng biến mất giữa hai lần chụp trạng thái. Mỗi nhân vật đủ điều kiện có receipt trước khi sinh thưởng, chặn nhận lại sau gói tin trùng. Giao dịch lỗi một phần được giữ lại để kiểm tra, không tự cấp lại.
- Monolith Echo: bỏ cặp serializer legacy `serialise/fromSerialisation`, vốn không mang EchoWeb và gây `NullReferenceException` trong `MonolithRun.updateCorruptionMod`. Chuyển sang cặp codec network native đầy đủ `WriteToBuffer/TryReadFromBuffer`, mang cả shared run data và EchoWeb. Kiểm tra đọc hết buffer trước khi thay context Client.
- Khi dữ liệu Echo chưa tới, nút teleport báo chờ context Host; không chuyển sang đường teleport map thường rồi báo Monolith chưa hỗ trợ. Thêm thời gian chờ giữa các lần thử lại sau lỗi.

## Kiểm nghiệm

- Release build: 0 warning, 0 error.
- 499 kiểm tra managed/network: đạt.
- 36 kiểm tra adapter Monolith với recording stubs: đạt, gồm codec đầy đủ thay legacy, dữ liệu thừa bị từ chối, chờ context, cooldown, vào/ra Echo, đổi generation cùng tên map, cleanup khi disconnect và chống nhận thưởng Echo trùng.
- 6 kiểm tra adapter loot Nemesis với recording stubs: đạt; xác minh lời gọi roller native, hệ số, level, scene và tọa độ. Đây không phải kiểm nghiệm loot thật trong game.
- Hồi quy animation adapter: 16/16; Fortune adapter: 22/22.
- Đã đối chiếu disassembly của bản game local để xác định lỗi serializer và callback null-owner. Chưa chạy QA native mới vì game đang mở. Chưa xác nhận độ mượt projectile, rơi đồ Nemesis và vào Echo qua hai máy với bản này.

## Cập nhật và kiểm tra trong game

Thoát game rồi cập nhật **cả hai DLL trên cả Host và Client**. Protocol tăng từ 29 lên 30 do thay định dạng run; hai phiên bản không kết nối lẫn nhau.

1. Client quan sát Host bắn projectile và dùng Lightning Blast khi di chuyển; kiểm tra độ mượt và chain.
2. Đánh Nemesis do Host kích hoạt: Host nhận phần thưởng lựa chọn riêng, Client nhận loot hỗ trợ. Sau đó đổi lượt để Client kích hoạt và kiểm tra cả hai phía. Empower có thể giữ lại vật phẩm cá nhân theo luật native; loot hỗ trợ được xử lý riêng.
3. Host mở Echo thường, Client theo vào, nhặt đồ, hoàn thành rồi trở về Rest. Kiểm tra thêm hai Echo liên tiếp dùng cùng map và Client join muộn.

Quest Echo/timeline boss progression, Woven/Tombs, Arena/Dungeon/Pinnacle vẫn chưa hỗ trợ đầy đủ. Host nên chờ đồng đội nhặt đồ trước khi chọn Echo tiếp theo. Không công bố bản này đã đạt toàn bộ hành vi online.
