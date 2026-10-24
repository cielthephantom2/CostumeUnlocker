# TM Last Epoch Co-op Mod 1.1.0-dev.66

Protocol44. Cập nhật cả hai DLL khi game đã đóng.

- Woven Enchanter không giới hạn lượt, thay giới hạn10 bằng chế độ unlimited native. UI nhận cùng cờ unlimited; không dùng số lượt giả cực lớn và không reset bộ đếm.
- Altar đã hết lượt cũng dùng tiếp được sau cập nhật. Giữ điều kiện hoàn thành echo, unlock crafting, idol hợp lệ, phí Memory Amber và giới hạn affix enchant của item. Các crafting station khác và online chính thức giữ nguyên.
- Nhân vật offline đã qua phần mở đầu đăng nhập ở Traveler’s Rest (M_Rest), chọn đích trước khi load map. Giữ intro native cho nhân vật mới và luồng hoàn thành campaign riêng; không ghi đè LastVisitedTownScene trong save.
- Giữ ẩn banner nhặt đồ và toàn bộ thay đổi dev.65.

## Kiểm tra

Build Release không warning/error;15 actual-source unlimited Enchanter checks,9 login destination checks và199 compiled audits. Đối chiếu native CanCraft có nhánh limitedUses và UI đọc cờ này. Không chứa công cụ QA/debug trong DLL hoặc ZIP.

Chưa chạy native crafting/đăng nhập và kiểm tra ảnh render cho hai thay đổi mới vì game Steam đang chạy. ZIP/updater được kiểm tra bằng fixture và tải public xác minh SHA256. Các kết quả native của dev.63 không thay thế QA cho hai thay đổi này.

ZIP chỉ gồm cặp DLL, manifest, update.bat, README và release notes; không có helper QA, log hay profile. Thoát game trước khi cập nhật.
