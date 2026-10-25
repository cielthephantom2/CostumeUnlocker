# TM Last Epoch Co-op Mod 1.1.0-dev.76

Thêm 5 ô Specialization bên dưới 5 ô gốc, tổng cộng 10 ô theo bố cục 2 hàng × 5. Thu gọn icon, căn lại khoảng cách và giữ khoảng trống với tiêu đề mastery.

- Giữ mốc mở ô level 4 / 8 / 20 / 35 / 50; mỗi mốc mở hai ô cùng cột, một ô mỗi hàng.
- Sửa luồng bấm ô mới rồi chọn skill: các ô 6–10 dùng đường kiểm tra/lưu native của nhân vật offline; đồng bộ trạng thái icon đúng slot sau mỗi refresh.
- Giữ nguyên 5 ô đang có và cây skill đã lưu. Ô mới dùng XP, lưu/tải và bỏ specialization của game; không thêm phím dùng skill vào thanh kỹ năng.
- Intimidating Cry trở về hành vi, mô tả và icon gốc; bỏ Earthquake tự cast mỗi giây khi Rampage.
- Tremor Slam: Maul gọi Earthquake thay Upheaval, dùng cây chuyên hóa Earthquake và tiêu 100% chi phí sau modifier dưới dạng Rage.
- Seismic Tide: giữ 3 wave, cooldown và cơ chế chi phí gốc; bỏ modifier damage riêng của từng wave. Radius tăng lần lượt +30% / +60% / +120%.

Protocol 44. Đóng Last Epoch trước khi cập nhật cả `Mods/LastEpochLAN.dll` và `UserLibs/LastEpochLAN.Core.dll`; các máy trong cùng phòng nên dùng cùng phiên bản.

## Kiểm tra

- 54 kiểm tra native cho 10 ô Specialization: mốc level, lệnh chọn skill, XP, serialization/load, bỏ specialization, chặn skill trùng và refresh UI. Bố cục được render bằng engine ở 1440×900 trên nhân vật QA riêng.
- Thêm 11 kiểm tra native cho thao tác nút: đủ chuỗi nhấn–nhả–click ở từng ô 6–10 và nút skill; cả 5 lần gắn đúng skill, tăng đủ 10 cây. Đây là sự kiện pointer trong engine, không phải thao tác chuột OS trên save người dùng.
- Bản sửa Earthquake/Seismic Tide đã qua 20 kiểm tra native, gồm chuỗi Maul tạo 3 Earthquake, chi phí, radius và payload không còn bonus/phạt damage riêng theo wave.
- Release build không warning/error; 221 compiled audits đạt. Gói phát hành chỉ chứa file phân phối, không kèm mã nguồn hoặc công cụ QA.

Chưa test thao tác chuột/controller thủ công hoặc mọi độ phân giải. Phần damage theo wave được xác minh qua payload native, chưa benchmark sát thương lên quái.
