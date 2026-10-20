# Bản 1.1.0-dev.8 - Sửa lấy đồ Shared Stash

Phát hành main theo yêu cầu. Đây là đúng cặp DLL đã kiểm thử và cài trong Steam, không rebuild khi phát hành. Protocol 33: đóng game trên cả Host/Client rồi chạy update.bat.

- F2 mở Shared Stash và inventory. Host/Join chỉ vào party; không cần Teleport hoặc cùng map để dùng hòm.
- Gửi trang bị/Idol hợp lệ bằng cách thả từ cursor vào hòm, hoặc Shift-click/chuột phải trong inventory.
- Bấm chuột trái món trong hòm để nhận tự động vào inventory sau khi Host giữ chỗ và nhân vật lưu thành công. Chừa ô trống và đợi "Item received and saved". F2/Esc/X đóng hòm.
- Sửa nạp prefab: lấy đúng StashPanel/StashPanelV2 từ PanelSystem, không phụ thuộc singleton thông báo Stash cũ. Timeout 20 giây có thông báo thử lại.
- Sửa click đi xuyên xuống map: tạo raycaster riêng cho canvas hòm và canvas từng icon; bật hit target của icon và gắn native Button với đúng ID vật phẩm. Các graphic trang trí không chặn click. Bấm đôi không tạo thêm giao dịch.
- Chữ Shared Stash dùng tiếng Anh/ASCII, có lý do rõ khi giao dịch đang chờ/giữ, dữ liệu chưa sẵn sàng hoặc nhân vật không đủ điều kiện.
- Giữ nguyên Host ledger, reservation, journal, custody ticket và save ACK. Không xóa thư mục SharedChest Host/Client để reset giao dịch; đồ dev.4-dev.6 vẫn dùng được.

## Kiểm nghiệm và giới hạn

Build sạch; 7 audit binary đạt. Native smoke cơ bản đạt trên đúng cặp phát hành. Sandbox có render xác minh EventSystem.RaycastAll tại icon -> ExecuteEvents pointer click -> native Button -> Host withdrawal -> native inventory save; bấm đôi chỉ một request; cuối cùng đúng một bản, nguyên serialized bytes. Cursor gửi lại/lấy lại, projection không được di chuyển trực tiếp hoặc lọt vào SavedItems, query khác map và Join không tự vào Echo vẫn đạt.

Full rendered campaign gate vẫn FAIL vì lỗi khởi tạo sandbox, gồm native service bị khởi tạo hai lần; không coi các lỗi thêm là baseline đã được chấp nhận. Giữ nguyên raw failures và phân biệt với observationsPassed. Ảnh chụp vẫn đen nên chưa nghiệm thu đầy đủ bố cục/hover/drag bằng mắt hoặc hai inventory native trên hai máy vật lý. Không sửa hệ thống encounter trong bản này; encounter co-op mới vẫn chưa tích hợp.

Xem validation/native-stash-withdraw-final-*, native-stash-withdraw-assessment.json và native-stash-withdraw-basic-final.json. Các báo cáo cũ thuộc binary cũ.
