# TM Last Epoch Co-op Mod 1.1.0-dev.79

Combat Assist thêm mục tiêu **Own minion / companion**, kèm danh sách chọn loại minion đang hiện hữu.

- Trong F1, chọn skill đã xác nhận, chuyển **Target** sang **Own minion / companion**, rồi mở danh sách bên phải để chọn Bear, Wolf hoặc loại minion đang có. Có thể chọn **Any own minion**.
- Danh sách hiển thị loại và số lượng minion đang sống của chính nhân vật. Nếu nhiều con cùng loại, Assist chọn một con đang sống trong tầm.
- Giữ lựa chọn theo loại khi triệu hồi lại. Khi không có mục tiêu phù hợp trong tầm, Assist chờ, không chuyển sang tự cast lên bản thân hoặc kẻ địch.
- **Detection radius** giới hạn khoảng cách cast; **Only when buff missing** và danh sách buff đọc trạng thái trên minion được chọn. Điều kiện HP/MP/Ward vẫn theo nhân vật như trước.
- Giữ quy tắc target, cooldown, chi phí và tác dụng gốc của từng skill. Cấu hình Combat Assist cũ được giữ nguyên.

## Kiểm tra

- 18 kiểm tra native trên Primalist QA riêng: Eterra’s Blessing thực sự hồi máu cho đúng Bear/Wolf; chọn Wolf bằng sự kiện pointer và kiểm tra vùng click; chặn cast khi Wolf biến mất; chọn lại Wolf sau khi triệu hồi; phân biệt buff trên chủ và buff trên minion.
- Đã xem ảnh render danh sách chọn và giao diện target. Release build không warning/error; 1.065 self-test và 220 compiled audit đạt.
- Gói chỉ chứa file phân phối, không kèm mã nguồn, profile, log hay công cụ QA. Chưa kiểm thử mọi skill, loại minion hoặc controller.

Protocol 44. Đóng Last Epoch rồi chạy `update.bat`, hoặc cập nhật đủ `Mods/LastEpochLAN.dll` và `UserLibs/LastEpochLAN.Core.dll`. Các máy cùng phòng nên dùng cùng phiên bản.
