# TM Last Epoch Co-op Mod 1.1.0-dev.87

- Sửa crash AccessViolationException khi dùng Aura of Decay / Blood Font: bỏ ghi vào hằng số native, áp dụng phần bleed chance còn thiếu trực tiếp vào hiệu ứng aura. Giữ Bleed Chance Effectiveness ở 100%.
- Sửa IndexOutOfRangeException của telemetry khi nhân vật có specialization ở ô 6–10.

Build Release, kiểm tra cấu trúc patch và đóng gói DLL production. Bản sửa đã qua 23 kiểm tra native tập trung, gồm tạo hiệu ứng Aura of Decay (75% → 100%), telemetry 10 ô và chỉ số/tooltip Carrion of Creation; chưa phải kiểm thử chiến đấu toàn diện.

Đóng game trước khi cập nhật cả hai DLL. Các máy cùng phòng nên dùng cùng phiên bản.
