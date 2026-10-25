# TM Last Epoch Co-op Mod 1.1.0-dev.80

- Thêm **F4 → Connection settings**: nhập IPv4 host và TCP port, chọn **Save & reconnect** để lưu và kết nối lại. Mỗi máy lưu cấu hình riêng; mở lại menu sẽ hiện giá trị đã lưu.
- Nhân vật Doomsday tiếp tục làm host trên cổng đã chọn; các nhân vật khác kết nối tới IP/port đã lưu. Cấu hình cũ mặc định dùng địa chỉ host đã cập nhật và cổng 7777.
- Kiểm tra địa chỉ và cổng hợp lệ. Nhập sai hoặc lỗi ghi file giữ nguyên kết nối hiện tại. Khi nhập cấu hình, phím số không kích hoạt shortcut gameplay.
- Dời hàng buff/debuff lên trên hàng shortcut 2–6, chừa khoảng cách để không che icon. Vị trí không bị cộng dồn khi HUD cập nhật; khôi phục vị trí gốc khi tháo hàng shortcut.

## Kiểm tra

Release build sạch, 1.065 self-test, 37 kiểm tra cấu hình/kết nối và 220 compiled audit đạt. Trong game QA riêng: 16 kiểm tra lưu cấu hình, đổi host/cổng, lỗi lưu và chặn input; 6 kiểm tra vị trí buff với icon native thực tế. Khoảng cách đo ở 1080p khoảng 16 px. Kiểm tra kết nối thực hiện trên loopback, chưa xác nhận từ mạng ngoài; ảnh chụp runtime ẩn không xác nhận được hình thức menu.

Protocol 44. Đóng game trước khi chạy `update.bat` hoặc cập nhật đủ hai DLL. Các máy cùng phòng nên dùng cùng phiên bản. Gói chỉ chứa file phân phối, không kèm mã nguồn hoặc công cụ QA.
