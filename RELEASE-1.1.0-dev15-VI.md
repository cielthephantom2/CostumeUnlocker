# Bản 1.1.0-dev.15 — giảm xử lý nền khi solo

Đóng game trên Host và tất cả Client, sau đó chạy `update.bat`. Restart để bỏ HP override của bản cũ. Protocol 35; Core giữ nguyên dev.14.

- Bỏ tăng HP quái theo số người; giữ mật độ quái hiện tại.
- Auto Play khi panel đóng và master OFF bỏ quét quái/cooldown/buff/skill; vẫn lưu settings, nhả input và nhận F1.
- DPS khi ẩn hoặc Pause bỏ capture hit/DoT và đọc source/ownership/ailments tương ứng; giữ dữ liệu cũ.
- Solo/Host tránh đọc native actor không cần thiết ở hook AI/HP/stun. Zoom kiểm tra phím trước UI/native reads.
- Cache command-line arguments. Gift/Shared Stash bỏ tick khi solo không room/panel; vẫn hoàn tất save/giao dịch đang chờ và giữ F2.

Build Release candidate đạt 0 warning/error. Chưa benchmark FPS trước/sau, native regression hay nghiệm thu hai máy cho dev.15; chưa xác nhận mức cải thiện FPS. VFX Harmony object[] còn cần profile. Các giới hạn dev.14 vẫn áp dụng.

SHA256 Mod: `260A8970A7D7AA4C4A9E9FD0F374C0CD6E1172C75D6E0D2ABA77EF6D960C2928`

SHA256 Core: `4AE855E63CCD75E91099119387B3E295DBFC7E6C8AD7711F65502DCDD4631F6E`
