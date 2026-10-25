# TM Last Epoch Co-op Mod 1.1.0-dev.82

Khôi phục bước native hoàn tất hàng đợi spawn trước khi vào map, nhằm xử lý khoảng trống quái có thể hoạt động trong lúc phần hiển thị chưa khởi tạo xong.

- Bỏ hook ngăn game hoàn tất hàng đợi spawn ở bước kết thúc tải Echo. Game chạy lại bước completion drain gốc; không để hàng đợi này tiếp tục sau màn tải theo cách mod cũ.
- Giữ cơ chế enqueue native, số lượng quái và giới hạn mật độ ×5. Thời gian chờ tải có thể dài hơn do game hoàn tất phần khởi tạo còn lại.
- Giữ các thay đổi dev81: bonus Amber/Bones từ quái, tối ưu shortcut và sửa nhánh lỗi âm thanh.
- Không thêm chặn cast/sát thương hoặc cơ chế bất tử tạm thời.

Production Release build sạch. Theo yêu cầu, không chạy thêm test/gameplay cho bản sửa này; chưa xác nhận hồi quy đầy đủ lỗi quái chưa hiện hình trên mọi map. Gói chỉ chứa file phân phối, không có mã nguồn, profiler hay công cụ QA.

Protocol 44. Đóng game trước khi chạy `update.bat` hoặc thay đủ `Mods/LastEpochLAN.dll` và `UserLibs/LastEpochLAN.Core.dll`. Các máy cùng phòng nên dùng cùng phiên bản.
