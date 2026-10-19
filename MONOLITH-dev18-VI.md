# Monolith co-op — dev.18 / protocol 29

Đây là bản phát triển cho **Echo thường**, chưa phải xác nhận gameplay Monolith hai máy đã hoàn thiện.

## Luồng đã triển khai

Host chọn/mở Echo bằng giao diện game. Client trong phòng LAN tự nhận bản sao run và Echo metadata, seed và loại mục tiêu rồi dùng luồng native LoadEchoAsync → InitialiseOnEchoStart → MovePlayer(Echo). Chỉ đặt cạnh Host sau khi nhân vật spawn và loading đã kết thúc. Nút teleport tới Host cũng yêu cầu vào đúng lượt thay vì chỉ so tên map.

Mỗi lượt có RunId riêng. RunId đi cùng player và world snapshot; Client chỉ bật combat/replica khi world của đúng lượt đã tới. Host từ chối hit từ Client còn thuộc lượt cũ dù tên map giống nhau. Đổi Echo liên tiếp cùng map phải exit/unload lượt trước; pending UniTask được quan sát và lấy kết quả một lần. Khi Host rời lượt hoặc mất kết nối, Client còn sống sẽ về Rest, unload và khôi phục context trước đó. Nếu Client đang chết/chưa spawn, cleanup đợi nhân vật sẵn sàng.

Mục tiêu, bonus stability, hoàn thành và đích portal do Host gửi. Context lớn chỉ gửi khi vào lượt/late join; cập nhật tiến độ bỏ các blob không đổi để giảm lưu lượng TCP. Client bị chặn tự chọn Echo riêng khi đang ở phòng LAN. F4 hiển thị trạng thái Monolith; runtime report có Entries, Restores, RewardGrants, Busy, Borrowing, Message và Error.

## Phần thưởng và dữ liệu nhân vật

Run nhập từ Host là object tách biệt, không được gắn vào danh sách run lưu của Client. Hook chặn ghi web/quest/timeline của run mượn; khôi phục ActiveRun, metadata, Host identity, RNG và tham chiếu respawn sau unload. Không copy save nhân vật của Host.

Đã nối API native ProcessEchoCompletionForPlayer và khởi tạo reward tại Rest. Host chốt danh sách nhân vật đủ điều kiện từ snapshot đang sẵn sàng trong đúng RunId khi hoàn thành. Client cần có cả quyền từ Host và dấu tham gia trước hoàn thành. Ledger tại UserData/LanEchoRewards ghi claim trước khi gọi native để không cấp lặp khi reconnect/crash. Nếu native grant lỗi giữa chừng, không tự thử cấp lại; cần xem log. Đây là cơ chế tối đa một lần, không bảo đảm phục hồi phần thưởng sau crash. Earned stability có thể được API native lưu cho nhân vật; không phải sao chép tiến độ Host.

## Giới hạn còn lại

- Quest Echo/boss quest và mở khóa timeline chưa có shared quest context; từ chối trước khi đổi globals/load. Không tuyên bố đồng bộ tiến độ boss quest.
- Woven/Tombs có sub-instance context riêng, hiện từ chối. Arena/Dungeon/Pinnacle vẫn ngoài phạm vi.
- Luồng nhận thưởng, rương ở Rest, native generation, chết/respawn/reconnect thực tế và độ mượt cần kiểm nghiệm hai máy. Test giả lập không chứng minh hình ảnh/gameplay Unity.
- Auto-follow đi theo Host. Host nên đợi đồng đội nhận thưởng ở Rest trước khi mở Echo tiếp.
- Chưa thay đổi kiến trúc combat hoặc chứng minh NPC trượt đã hết. Lightning Blast chain là kết quả người dùng xác nhận từ bản trước.

## Kiểm nghiệm và cách thử

Release: 0 warning, 0 error. 489 managed/network checks; 30 source-linked adapter Monolith checks với API ghi lệnh giả lập; 16 adapter animation checks đều đạt. Có kiểm tra TCP thật: late join, progress bỏ blob, context baseline, giả mạo context bị từ chối, hit sai RunId bị loại và đúng RunId được nhận. Fixture Monolith kiểm tra thứ tự load/enter/exit/unload, cùng map khác lượt, chờ spawn, khôi phục globals/respawn, disconnect, reward tối đa một lần và từ chối Quest/Woven.

Sau khi game người dùng đã đóng, đã chạy Test-NativeAdapter.ps1 trong qa-adapter-runtime riêng: bootstrap/inactive-component pose/HP/damage-detour smoke đạt, không có lỗi theo gate của script. Không load campaign, không chạy Echo trong native QA này. Không thay DLL trong thư mục game đang dùng và không sửa save người dùng bằng công cụ phát triển.

Cập nhật **cả hai DLL trên cả hai máy** và khởi động lại vì protocol tăng 28 → 29. Host vào Echo thường; Client join phòng/tự theo Host. Thử hai Echo liên tiếp (cả cùng tên map), teleport tới Host, hoàn thành/nhận thưởng ở Rest, chết rồi respawn, disconnect/reconnect. Đối chiếu runtime report khi có lỗi. Giữ density x5, DPS 8 skill góc trên trái, thông báo bản mới đỏ và bản hiện tại xanh.
