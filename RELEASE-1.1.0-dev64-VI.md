# TM Last Epoch Co-op Mod 1.1.0-dev.64

Protocol44. Cập nhật cả hai DLL khi game đã đóng.

- Tăng giới hạn Woven Enchanter lên10 lượt mỗi altar, bao gồm các tier native khác nhau. Đồng bộ giới hạn thực tế và bộ đếm UI.
- Giữ nguyên số lượt đã dùng: altar đã dùng2 lượt sẽ còn8/10, không tự hoàn trả lượt khi mở lại panel. Hết10 lượt thì vẫn bị giới hạn native chặn.
- Giữ điều kiện hoàn thành echo, unlock crafting, loại idol hợp lệ, Memory Amber cost và số affix enchant của item. Không áp dụng cho crafting station khác hoặc online chính thức.
- Giữ các thay đổi Spirit Xylem, Disintegrate, Arcane Ascendance và loading echo của dev.63.

## Kiểm tra

Build Release;18 actual-source Enchanter checks;197 compiled audits, gồm release loại QA/debug. Đối chiếu native CalculateMaxUses, UsesRemaining, CanCraft, OnUse và GetUsesValuesForUI. Chưa chạy native crafting/ảnh render cho thay đổi Enchanter vì phiên Steam của người dùng đang chạy. Các kiểm tra native Xylem/movement176 assertions thuộc dev.63, không tính là native QA cho Enchanter.

ZIP chỉ gồm cặp DLL, manifest, update.bat, README và release notes; không chứa helper QA, debug/probe/smoke, log hay profile. Thoát game trước khi cập nhật.
