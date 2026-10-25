# TM Last Epoch Co-op Mod 1.1.0-dev.81

- **Mật độ quái Monolith tối đa ×5**, đạt trần từ 500 Corruption. Ở C2000 dùng ×5 thay vì ×20. Campaign và Dungeon giữ mật độ gốc.
- **Memory Amber và Ancient Bones từ quái:** dưới C500 giữ nguyên; từ C500, mỗi 10 Corruption tăng 1% số lượng: C500 +50%, C1000 +100%, C2000 +200%. Tăng lượng trong mỗi đống, không tăng số đống. Giữ bonus native/faction hiện có; phần lẻ làm tròn theo xác suất. Không áp dụng lên túi, rương hoặc thưởng hoàn thành Echo.
- **Tối ưu thanh shortcut:** thay tìm resource toàn cục khi HUD chưa hiện bằng registry icon native; tránh cập nhật cùng icon hai lần mỗi frame và gán lại nhãn phím không đổi. Giữ cập nhật cooldown/charge, chọn skill, lưu skill và channel.
- **Sửa nhánh lỗi âm thanh khi chuyển map:** bỏ qua riêng âm thanh khi cast callback thiếu đối tượng hoặc ActorSync đã bị dọn; giữ đường xử lý cast. Phím 2–6 chờ actor visuals khởi tạo xong.

## Kiểm tra

- Release build sạch, 1.076 self-test và 225 compiled audit đạt; gói không kèm profiler/công cụ QA hoặc mã nguồn.
- 61 kiểm tra native thanh 10 ô đạt, gồm pointer, picker, lưu/gắn lại, tạo ability, Combat Assist và giữ/thả channel.
- 22 kiểm tra native currency đạt: các mốc C499/500/509/510/1000/2000, chống nhân lặp, loại trừ nguồn ngoài quái/online, khôi phục context và lưu lượng Bones thật trên đất. Nhánh loot quái native tại C1000 xác nhận Bones 186 → 372, chỉ một lần drop.
- Tái hiện nhánh lỗi sound thiếu ActorSync trước sửa; 7 kiểm tra native sau sửa đạt.
- Đã đo tải Echo C2000 trên nhiều scene với hơn 2.000 actor được tạo, không mất queue. Trong một so sánh, phần cập nhật icon giảm khoảng 35% thời gian; đây không phải mức tăng FPS toàn game. Chưa xác nhận full combat nhiều hit/loot burst/GPU benchmark. Vẫn còn spike sau tải map chưa xác định chính xác nguồn; không kết luận đã hết lag.

Protocol 44. Đóng Last Epoch trước khi chạy `update.bat` hoặc thay đủ `Mods/LastEpochLAN.dll` và `UserLibs/LastEpochLAN.Core.dll`. Các máy cùng phòng nên dùng cùng phiên bản.
