# Auto Pickup hiện tại — toàn map, delay0,5s — dev.55

Khôi phục hành vi tự nhận các loại đã bật trên toàn map khi đồ mới rơi, thêm chờ0,5 giây trước khi thực thi. Không còn radius5, điều kiện di chuyển, bộ quét5ứng viên hoặc giới hạn số món/lượt. Mỗi món có deadline riêng; đây không phải chỉ chạy2lượt/giây. Mọi yêu cầu đã đủ0,5s được xử lý trong lượt Unity hiện tại.

Gold/Potion/Ancient Bones: giữ ground record/gameplay object native, xếp ID khi initialise và gọi pickup gốc sau deadline. Không cộng tiền, xương hoặc potion trực tiếp vào tracker. Nếu potion đầy lúc đến hạn thì để lại theo game. Native auto interaction không được vượt deadline của món đang chờ.

Keys/Materials/Woven Echoes: như code cũ, hook dropItemForPlayer đặt vị trí drop tại vị trí người chơi, giữ sound flag cũ; sau khi native tạo ground record thì xếp đúng ID/pointer/source list vào hàng chờ. Khi đủ0,5s gọi pickupItem(All) → vacuum → storage native. Scope chỉ cho các món đã đủ delay và đúng toggle đi qua vacuum, nên món mới rơi sau không bị nhặt sớm nhờ món cũ. Loot có từ trước mà không đi qua hook drop không được quét toàn map để bổ sung, đúng tính chất drop-driven của bản cũ.

Giữ multiplier gold hiện có, toggles riêng (kể cả Woven Echoes), loại trừ equipment/tutorial item và offline/local ownership. Range Pickup click vẫn thực hiện ngay. Không thêm chia lượt hoặc retry theo timer. Nhặt tay trước deadline không nhận lại món/currency; pool/ID reuse, đổi nhân vật/manager/zone có guard. Pre-unload/zone/dispose hủy hàng chờ; native ground data vẫn thuộc lifecycle của game. Túi không nhận được món thì món còn trên đất, không cộng bù hoặc replay lệnh lỗi.

F4 Mod Settings và changelog cập nhật thành all map /0,5s. Không log từng lần pickup; chỉ báo lỗi native nếu gặp lỗi.

Kiểm chứng: Release build0 warnings/errors;35 actual-source checks và169 compiled/native audit checks pass,188 patch classes inspected. Kiểm tra mốc499/500ms, deadline riêng, vacuum không lấy món mới sớm, đi xa1000units,25món cùng lúc không cap, gold/potion/bones ngoài400units, potion đầy, toggles, duplicate/manual pickup, storage readiness, pool/ID reuse, đổi nhân vật/manager/zone, exception và scope cleanup. Chưa xác nhận gameplay mới trong game và chưa đo FPS. Delay đơn thuần không được báo cáo như một cải thiện FPS đã đo.

Giữ version1.1.0-dev.55 / protocol44. Combat Assist, minimap chấm xanh scale0.90 và các bonus khác giữ nguyên; Prophecy vẫn là cơ chế charge gốc. Đóng game rồi chạy update.bat để nhận cả hai DLL.
