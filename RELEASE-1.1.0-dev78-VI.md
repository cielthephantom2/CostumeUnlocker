# TM Last Epoch Co-op Mod 1.1.0-dev.78

Sửa Summon Bear / Summon Wolf không triệu hồi được khi nhấn phím 2–6 ở hàng shortcut mới.

- Các shortcut mới lấy điểm cast bằng raycast world gốc của game và truyền đúng mục tiêu trúng raycast. Trước đây luồng nhận phím truyền tọa độ pixel màn hình vào lệnh cast, làm điểm triệu hồi không hợp lệ.
- Sửa chung đường nhắm vị trí cho cả 5 ô mới, gồm cả cập nhật hướng khi giữ channel. Giữ cấu hình skill đã lưu, 10 ô Combat Assist và các thay đổi dev.77.

## Kiểm tra

- Đã thử phím Windows thật trên Primalist QA riêng: phím 2 triệu hồi Bear, phím 3 triệu hồi Wolf; cả hai tồn tại trong summon tracker. Nhấn lại phím 2 sau khi Bear xuất hiện cũng được native chấp nhận để dùng kỹ năng companion.
- Kiểm tra gọi trực tiếp và giữ lệnh với Bear/Wolf trên cả 5 vị trí mới. Thêm kiểm tra hồi quy để luồng phím phải dùng mục tiêu world thay vì pixel màn hình.
- Release build không warning/error; 1.055 self-test và 218 compiled audit đạt. Không kèm mã nguồn hoặc công cụ QA trong gói phát hành.

Protocol 44. Đóng Last Epoch trước khi cập nhật cả hai DLL, hoặc chạy `update.bat` trong thư mục game. Các máy cùng phòng nên dùng cùng phiên bản. Chưa kiểm thử mọi skill, controller hoặc mọi dạng biến hình.
