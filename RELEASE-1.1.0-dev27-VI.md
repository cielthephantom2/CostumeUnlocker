# TM Co-op 1.1.0-dev.27 — protocol36

Đóng game rồi chạy update.bat. Cập nhật cả Host và Client. Bản25/26 chỉ cài local trước đó;27 gom các sửa độc lập và gỡ toàn bộ phần tùy chỉnh Disintegrate.

- Gỡ channel mobility/WASD, RMB/aim movement hooks, Playables animation, root/Arcane Ascendance integration và helpers/tests đi kèm. Disintegrate/Arcane Ascendance dùng hành vi gốc. Không tắt khóa di chuyển gốc của Arcane Ascendance.
- Giữ Combat Assist Low ward, Hold0, vòng detection radius và nhãn MP before cast; giảm read/dispatch cadence xuống25ms, bỏ confirmation tạo global delay, retry lệnh bị từ chối150ms. Cooldown/cast restrictions vẫn do game quyết định. Không có benchmark độ trễ/FPS hay native gameplay acceptance cho những sửa này.
- Mọi slider F1 có ô nhập số trực tiếp: Enter/bấm ra ngoài để lưu; nhận decimal dot/comma, clamp giới hạn và từ chối giá trị sai/NaN/Infinity.
- Minimap nhỏ: dùng ApplyScaling và actual anchor scale thay scaleMultiplier không có hiệu lực trên renderer hiện tại; dots30% kích thước trước đó, Tab overlay giữ gốc. Chưa có native visual verification sau sửa.
- DPS ẩn sau15s không gây damage, tự hiện lại khi có damage mới và giữ total/skills. Cửa sổ DPS/5s Total vẫn5s; giao diện trong suốt, thanh xanh và stats dọc giữ từ24.
- Encounter đang tạm dừng, không đưa nhánh dev23/protocol37 hoặc QA helper vào release. Giữ mật độ quái và không có party HP scaling.

Kiểm chứng: Release build0 warning/error, managed772 PASS, compiled audit11 PASS/113 Harmony classes. Audit xác nhận không còn override class/Core helper/direct native API refs tới Disintegrate/Ascendance/rotation penalty. Đây là kiểm chứng logic/compiled scope, không phải nghiệm thu native gameplay/physical input/visual/two-PC. Evidence: validation/release-dev27-*. Updater xác minh manifest/DLL pair và giữ backup, không tự đóng game.