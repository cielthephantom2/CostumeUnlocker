# TM Last Epoch Co-op Mod 1.1.0-dev.67

Protocol44. Cập nhật cả hai DLL khi game đã đóng.

- Sửa vị trí đăng nhập tại Traveler’s Rest: chọn đúng spawn marker Waypoint gate0, giống khi người chơi teleport bằng waypoint, thay vì default-login spawn.
- Dùng transform hiện tại của marker cùng quy trình đặt nhân vật/navmesh/respawn native; không ghi tọa độ cứng, không thêm lần chuyển map, không sửa save. Chỉ áp dụng EnterGame vào M_Rest trong offline; các lần teleport, hồi sinh và map khác giữ nguyên.
- Giữ Woven Enchanter vô hạn lượt, phí Memory Amber, đăng nhập Traveler’s Rest và ẩn banner nhặt đồ của dev.66.

## Kiểm tra

Build Release không warning/error;16 actual-source login/spawn checks và200 compiled audits đạt, gồm loại QA/debug khỏi DLL. Đối chiếu native EnterGame/Waypoint CompatibleWithSpawn và asset M_Rest: waypoint marker gate0, TransitionType.Waypoint. Không tính đây là kiểm tra vị trí thực tế trong engine; game Steam đang chạy nên chưa chạy native login/ảnh render cho bản sửa này.

ZIP chỉ gồm cặp DLL, manifest, update.bat, README và release notes; không có helper QA, log hay profile. Gói phát hành/updater được kiểm tra fixture và tải public xác minh SHA256. Thoát game trước khi cập nhật.
