# Combat Assist phản ứng theo frame — dev.55 / protocol44

- Khi Assist đang chạy và F1 đóng: bỏ hạn chế đọc25ms toàn bộ runtime. HP, mana, ward, potion và trạng thái cooldown của5slot được kiểm tra mỗi Unity frame. F1 mở vẫn refresh100ms.
- Potion được xét ngay sau điều kiện local/gameplay/input, trước registry mục tiêu, danh sách buff và5slot. Giữ ngưỡng HP, reserve, interval và API `UsePotion` gốc; một lượt gọi potion sẽ không gửi thêm cast trong cùng tick.
- Registry mục tiêu100ms (trước150ms); chọn gần nhất/rare/boss trong cache25ms (trước50ms). Refresh registry kéo lượt chọn mục tiêu lên ngay tick đó. Vẫn loại mục tiêu xa bằng squared distance trước các native health/alignment/scene reads. Khi pause và F1 đóng thì bỏ target/skill reads nặng.
- Tên/ID/label kỹ năng cache500ms, đổi pointer hotbar cập nhật ngay; cooldown và điều kiện vẫn đọc từng frame. Bind key label giữ1giây, buff names100ms.
- Channel do Assist khởi động có thể nhường cho kỹ năng khác đủ điều kiện và `Repeat interval > 0`. Chỉ nhường khi channel đã thực sự active, không ngắt wind-up/manual hold. Nhả channel trong một tick; chờ game Idle/Moving, kiểm tra lại slot ưu tiên rồi mới gửi cast. Slot Hold interval0 khác không tranh giành channel.
- Giữ round robin, per-skill repeat/cooldown, khoảng gửi request tối thiểu25ms và chặn trạng thái native đang cast/manual input. Không có thread phụ đọc/cast Unity/IL2CPP.
- Priority chờ được xóa khi pause, lỗi native, đổi character/scene hoặc disconnect. Giữ bản sửa disconnect, pickup radius5 và các bonus trước.
- Help F1 cập nhật quy tắc Hold. Diagnostics thêm `ChannelYields` và `prioritySlot`.

Kiểm chứng: Release build0 warnings/errors;1.051 managed/TCP self-tests,13 actual-source disconnect checks,159 compiled/native audit checks pass.13 test mới gồm read mỗi frame trước deadline25ms cũ, nhịpF1, ưu tiên periodic khi channel, không tranh giữaHold, revalidate cooldown/HP/disable/interval và resumeHold. Chưa đo FPS hoặc xác nhận độ phản ứng/channel/potion trong game với DLL mới.

Kiểm tra: ngưỡng HP/potion khi đang channel, buff phòng thủ có interval>0, hai slotHold0, native cooldown/charges, mởF1/F4/menugame, mất focus, Join→Disconnect. Chỉ skill đã bật và thỏa điều kiện mới có quyền nhường channel.

Giữ version1.1.0-dev.55/protocol44. Đóng game rồi chạy `update.bat` để cập nhật cả hai DLL.
