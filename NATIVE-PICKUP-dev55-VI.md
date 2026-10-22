# Auto Pickup hiện tại — dev.55 / protocol44

- Giữ bán kính5 world units trên mặt phẳngXZ. Không yêu cầu nhân vật di chuyển.
- Bộ quét chạy tối đa5lượt/giây (mỗi200ms), xét tối đa5ứng viên trên toàn bộ các danh sách trong một lượt, gửi tối đa1lệnh nhặt native/lượt.
- Xoay vòng nhóm bắt đầu Gold/Potion/Ancient Bones/Items để danh sách dài không chặn nhóm khác. Cursor tiếp tục ở lượt sau.
- Một lệnh native pickup/vacuum có thể lấy cả nhóm key/material/Woven Echoes hợp lệ đã duyệt trong cùng lượt, tối đa5ứng viên. Không gọi vacuum riêng cho từng món của nhóm.
- Phạm vi và toggle được kiểm tra trước khi phê duyệt ứng viên; native filter chỉ cho nhóm được phê duyệt đi qua. Giữ đường All→vacuum→storage gốc đã hoạt động. Không chuyển loot về chân nhân vật, không cộng trực tiếp vào túi.
- Có thêm **Auto Woven Echoes: radius5**, bật mặc định, tùy chọn riêng trong F4 Mod Settings. Nhận diện bằng helper native `Item.isWovenEcho` (base type107), lưu qua pickup/storage gốc.
- Bỏ log chẩn đoán Auto Pickup, dòng trạng thái debug trong F4 và counters trong runtime report. Nhãn toggle bỏ chữ `including while standing`.
- Potion đầy, equipment và tutorial item vẫn được để lại. Click thủ công/Range Pickup giữ hành vi hiện tại. World Assist vẫn đã gỡ; Combat Assist giữ bản tối ưu và sửa disconnect.

Hạn mức5ứng viên là phần quét chọn mục tiêu của mod. Native vacuum vẫn duyệt danh sách nội bộ; scope có một lượt xác minh identity để tránh ItemData chia sẻ làm lọt ground id chưa được phê duyệt. Không phải toàn bộ engine chỉ đọc5entry và chưa có số đoFPS.

Kiểm chứng: Release build0 warnings/errors;62 actual-source pickup checks (5Hz/shared budget5/action1, batch, radius, Woven toggle, category fairness, scope cleanup, không log),163 compiled/native audit checks pass. Người dùng đã xác nhận cơ chế pickup của bản trước hoạt động; thay đổi giảm tải/batch/Woven mới chưa được xác nhận trong game.

Đóng game rồi chạy `update.bat` để nhận cả hai DLL, giữ1.1.0-dev.55/protocol44.
