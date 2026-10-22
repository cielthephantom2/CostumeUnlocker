# Combat Assist sau khi disconnect room — dev.55 / protocol44

Sửa các đường có thể giữ Assist ở trạng thái pause hoặc ngắt lượt chạy sau khi Join rồi disconnect:

- Disconnect đóng F4 root và trang Mod Settings. Trước đó bấm nút Disconnect vẫn để F4 mở, khiến `otherPanel` giữ Assist ở pause.
- Local Assist tick trước phần polling Gift/Shared Stash/network. Exception ở phần room không còn bỏ qua lượt cập nhật Assist.
- StopRoom detach session ngay từ đầu, hủy pending join và dispose transport. Từng bước cleanup được bắt lỗi riêng và log rõ tên, nên native view lỗi không giữ session lỗi hoặc chặn phần còn lại.
- Sau disconnect, Assist nhả channel/input block do nó sở hữu, xóa target native cũ, làm mới bindings/read timers và recovery cooldown. Giữ nguyên Armed, F1 Open, settings và profile. Assist đã Stop không tự Start; đổi nhân vật vẫn theo Profile guard cũ.
- Không dọn các hệ thống shared world/combat trong release inventory-only, vì các hệ thống đó không được bật. Room vẫn có F2, filters, ladder.
- Diagnostics có `DisconnectRefreshes`. Bản cập nhật UI tiếp theo cho phép chạy khi F1/F4/menu mở; mất focus, chết/loading, cinematic và game time pause vẫn giữ kiểm tra.

Kiểm chứng: Release build0 warnings/errors;13 actual-source lifecycle checks pass (đang chạy/đang Stop/F1 mở/native object bị hủy/cleanup lỗi/lặp disconnect),153 compiled/native audit checks pass. Audit kiểm tra thứ tự local tick trước network, không Suspend/Dispose Assist lúc disconnect, không ghi đè Armed/profile và đóng F4. Chưa xác nhận disconnect thực tế trong game.

Kiểm tra: bật Assist trên nhân vật offline, Join room, Disconnect; sau khi giao diện đóng Assist đang Start phải tiếp tục. Thử Host tắt room/mất mạng, F2 đang mở, lặp Join/Disconnect; trường hợp Assist đang Stop phải giữ Stop. Nếu vẫn dừng, kiểm tra `autoPlay.Status`, `pauseReason`, `Armed`, `DisconnectRefreshes` trong runtime report và log `Room disconnect (...)`.

Giữ version1.1.0-dev.55/protocol44. Đóng game rồi chạy `update.bat` để nhận cặp DLL mới.
