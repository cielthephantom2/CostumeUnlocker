# Bản 1.1.0-dev.5 — Shared Stash dùng giao diện native

Phát hành lên main theo yêu cầu ngày 20/10/2026. Protocol 33. Đây là đúng cặp DLL dev.5 đã cài local, không rebuild. Đóng game trên cả hai máy rồi chạy `update.bat` để cập nhật.

## Cách dùng

- F2 mở/đóng Shared Stash, đồng thời mở inventory native. Escape hoặc nút X đóng hòm chung.
- Dùng khung Stash, ô vật phẩm, icon, hover/tooltip của game. Dữ liệu hiển thị là bản chiếu riêng từ hòm Host; không nối vào stash cá nhân.
- Kéo món từ inventory lên con trỏ rồi thả vào ô hòm để gửi. Shift-click hoặc chuột phải món trong inventory cũng gửi khi hòm chung đang mở.
- Bấm món trong hòm để nhận vào inventory; bắt đầu kéo món trong hòm cũng yêu cầu nhận vào inventory. Việc nhận phải chờ Host giữ chỗ và native save, không tạo bản đồ giả trực tiếp trên con trỏ.
- Tự chia trang theo kích thước vật phẩm; dùng nút `<` / `>` ở dưới. Giới hạn vẫn 96 món trang bị/Idol đủ điều kiện Gift, mỗi món x1. Không có mua tab, sort hoặc mở rộng stash cá nhân trong hòm chung.
- Join chỉ vào party; không cần Teleport hoặc cùng map để dùng hòm. Lưu trữ Host, journal, escrow và xác nhận save của dev.4 được giữ nguyên.

## Tách dữ liệu và giao dịch

Clone phần hiển thị từ prefab Stash trong một parent đang tắt, bỏ controller/callback Stash gốc trước khi kích hoạt. Tạo `ItemContainerUI` và các container mới cho bản chiếu. Không đăng ký chúng vào manager lưu nhân vật. Chặn thao tác native sửa/tháo/chuyển vật phẩm bản chiếu; hành động người dùng chuyển thành yêu cầu gửi/lấy qua journal đã có.

Không xóa `UserData/LastEpochLAN/SharedChest/Host` hoặc `Client`. Dữ liệu dev.4 tiếp tục dùng được. Không sửa save thật hoặc hệ thống encounter trong bản này.

## Kiểm nghiệm

- Build Release sạch, 696 kiểm tra managed đạt, gồm các mô phỏng mất kết nối/crash/replay/custody đã có.
- Native sandbox bản cuối quan sát đạt: clone Stash tạo ô vật phẩm, đúng native item ID, không bind personal stash, chặn thay đổi/chuyển trực tiếp và không đưa bản chiếu vào SavedItems. Hai vòng gửi/lấy gồm gửi từ inventory, gửi lại từ cursor qua `TryDropFromCursor` native giữ đúng một bản và nguyên serialized bytes; peer khác map vẫn query được và không tự vào Echo.
- QA render riêng lỗi bootstrap `CharacterSelect` đang transition; ảnh batch mode đen. Chưa xác nhận bố cục, tooltip và kéo/thả bằng chuột trên màn hình thật hoặc hai máy vật lý. Không coi smoke có container/item UI là nghiệm thu hình ảnh.
- Raw full campaign QA giữ nguyên trạng thái FAIL: 5 lỗi bootstrap đã biết và thêm `AnimationLoader.SetOverrideController` null trước khi bắt đầu kiểm thử hòm. Không xác nhận lỗi animation này là baseline 5 lỗi mà người dùng đã chấp nhận; giữ nguyên log để kiểm tra riêng. Kết quả cuối và SHA được ghi riêng trong `validation/native-stash-final-*`.
