# Timeline Stability x2 — dev.55 / protocol44

- Forgotten Knights Rank2: từ10% lên25% cơ hội nhận gấp đôi Stability khi hoàn thành Echo. Rank1 và5 vẫn100%/50%; sửa mô tả Rank2 thành25%.
- x2 Timeline Stability cơ bản của Echo Rewards, không lọc loại Echo: Normal, Arena, Shade, Woven, các Echo đặc biệt và chain khi game có thưởng.
- x2 Bonus Timeline Stability thực nhận của Echo Rewards, gồm bonus từ quái, wave, tiến độ Woven và High Stability Chains.
- Temporal Keystone: x2 khoản native đã roll, giữ tiêu thụ một key, giới hạn timeline và số thông báo theo lượng thực nhận.

Ví dụ ảnh:24 Timeline Stability →48; Up to50 Bonus Timeline Stability → Up to100. Nếu Forgotten Knights Rank2 kích hoạt, phần cơ bản48 được game nhân đôi thành96 (25% cơ hội); phần bonus không được nhân faction thêm lần nữa.

Thưởng cơ bản sửa sau kết quả faction native. Bonus và Keystone sửa riêng đầu vào AddStability trong scope của các hàm chi trả native, có cleanup bằng finalizer và chỉ nhận một lần cho mỗi run trong scope. Không nhân toàn cục mọi lệnh AddStability. Balance đã có và bản ghi reward không bị nhân lại. Unstable Rift/Stabilize the Echo vẫn dùng khoản cộng gốc riêng của mục tiêu.

Hiển thị Echo Rewards sửa hai dòng theo icon Stability gốc, giữ nguyên chữ bản địa hóa, màu và reward item khác. Thanh preview Echo và số bonus trong map hiển thị gấp đôi; dữ liệu tiến độ native và tỉ lệ tính mốc rương không đổi. Preview Keystone sửa ở GetStabilityRange riêng, không nhân lại ở đường preview Echo.

Build Release0warnings/errors.45 kiểm tra actual-source Stability,59 Forgotten Knights và140 audit compiled/native đạt. Các kiểm tra bao gồm scope lồng nhau, native exception, giới hạn, tràn số, không nhân nguồn ngoài scope, preview300–420, gần cap, ảnh24/50 và faction proc24→48→96. Chưa chạy gameplay/render trực tiếp trong game.

Giữ version1.1.0-dev.55, protocol44 và Core47. Default Meteor đã gỡ. Game đang chạy khi đóng gói nên chưa chép vào Steam. Đóng game rồi chạy update.bat; updater kiểm tra hash dù version không đổi.
