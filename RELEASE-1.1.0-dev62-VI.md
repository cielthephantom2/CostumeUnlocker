# TM Last Epoch Co-op Mod 1.1.0-dev.62

Protocol 44. Cập nhật cả hai DLL trên Host và Guest khi game đã đóng.

- Sửa đợt tạo quái đồng bộ quá lâu khi vào echo mật độ cao: dùng native amortized queue thay instant queue, tránh callback sau load rút cạn queue trong một frame. Giữ mật độ max(1, Corruption/100), số lượng, generator và callback native; trả lại cờ spawner sau mỗi lần enqueue.
- Chỉ áp dụng trong Monolith offline Host/local có mật độ >x1. Campaign, dungeon, online và Guest không bị đổi lịch tạo quái bởi bản sửa này.
- Loại bộ đo frame/allocation/death hitch, trace aggro, probe/smoke test và runtime-report writer khỏi DLL release. Source QA vẫn có thể build riêng bằng EnableQaTools=true; helper test không đóng trong ZIP.
- Giữ tự host/join theo tên nhân vật, aggro x3 và Prophecy chia thưởng qua nhiều frame của dev.61.

## Kiểm tra

Build Release 0 warning/error. 1.053 managed/TCP checks; 259 actual-source feature checks; 194 compiled/native metadata audits và 15 native assertions trên đúng cặp DLL release PASS. Native QA dùng profile riêng, có đồ họa, M_M70 ở 1000 Corruption/x10, seed100024. Native queue hoàn tất và kiểm tra đủ mọi quái mà các spawner được theo dõi yêu cầu, không tạo thất bại/remaining/pending. Xem validation/dev62-native.json.

Lượt điều tra trước có frame tạo quái hơn100s. Sau sửa, các lượt QA vào map khoảng5s; một phần quái hoàn tất sau khi nhân vật vào map. Các roll/loại quái giữa lượt cũ và mới khác nhau, không dùng chúng để khẳng định tỷ lệ tăng tốc A/B. Native queue vẫn có thể gây frame khựng ngắn trong giai đoạn hoàn tất quái. Chưa phải nghiệm thu mọi map Woven/Tombs hay multiplayer hai PC.

## Cài đặt

Thoát game, chạy update.bat hoặc giải nén ZIP vào thư mục game. ZIP chỉ gồm DLL pair, manifest, updater, README và release notes; không chứa helper QA, log hay profile nhân vật. Có backup và xác minh SHA256 khi cập nhật.
