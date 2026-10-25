# TM Last Epoch Co-op Mod 1.1.0-dev.77

Thêm hàng 5 ô dùng skill phía trên thanh kỹ năng gốc, với hotkey **2 / 3 / 4 / 5 / 6**. Combat Assist hỗ trợ đủ 10 ô. Gỡ tính năng ladder mod offline.

- Ô mới có khung, icon, tooltip, cooldown, charge và nhãn số riêng; có thể bấm ô trống rồi chọn skill bằng menu gốc.
- Giữ nguyên Q / E / R / F / RMB và vị trí né tránh. Lựa chọn của hàng mới lưu riêng theo nhân vật và dạng biến hình, không mở rộng thanh 5 ô trong save gốc.
- Sửa lưu/khôi phục lựa chọn khi game dựng lại thanh kỹ năng, vùng click của ô trống và nhãn phím bị bộ prompt gốc ghi đè.
- Combat Assist có 10 ô cấu hình, nhận đúng hotkey và dùng đúng vị trí skill. Giữ các thiết lập 5 ô cũ; 5 ô mới mặc định tắt. Bộ đếm thời gian theo đúng ô khi một skill được gắn nhiều lần.
- Gỡ hook, thu thập dữ liệu và hiển thị ladder mod offline. Giữ các thay đổi Specialization và Druid đã phát hành ở dev.76.

Protocol 44. Đóng Last Epoch trước khi cập nhật cả `Mods/LastEpochLAN.dll` và `UserLibs/LastEpochLAN.Core.dll`, hoặc dùng `update.bat` trong thư mục game. Các máy cùng phòng nên dùng cùng phiên bản.

## Kiểm tra

- 61 kiểm tra tự động trong engine trên nhân vật QA riêng: tọa độ và vùng click trên cùng của từng ô mới, chọn skill qua sự kiện pointer, nhãn 2–6, lưu/đọc lại và dựng lại thanh, cast qua từng ô mới và từng ô Combat Assist, giữ/đổi hướng/thả channel Disintegrate.
- 1.055 self-test và 217 compiled audit đạt; Release build không warning/error.
- Kiểm tra hình ảnh hàng ô mới và bảng Combat Assist 10 ô. Gói phát hành không chứa mã nguồn, profile, log hoặc công cụ QA.

Đây là kiểm tra native tự động; chưa kiểm thử bàn phím/controller thủ công, mọi skill, dạng biến hình hoặc độ phân giải.
