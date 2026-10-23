# TM Last Epoch Co-op Mod 1.1.0-dev.61

Protocol 44. Cập nhật cả cặp DLL trên Host và Guest khi game đã đóng.

- Name lấy từ tên nhân vật đang đăng nhập. `Doomsday` tự host TCP 7777; các nhân vật khác tự join `27.70.239.115:7777`. Join thất bại thử lại sau 30 giây; không tạo thêm attempt khi đang kết nối. Đổi nhân vật chọn lại vai trò; Disconnect tạm dừng, Reconnect thử ngay.
- Bỏ ô Name/IP/Port/Room code và nút chọn Host/Join khỏi UI. Không yêu cầu room code; vẫn kiểm tra protocol/build khi handshake.
- Sửa cổng aggro x3 trong echo loại nhầm actor ở scene pool/persistent khác player; giữ native activation gates và tầm đánh. Thêm bộ đếm rejected/expanded/activation để kiểm tra thực tế.
- Giảm đợt tạo item đồng bộ của Prophecy: mỗi frame xử lý một đơn vị trang bị; giữ đủ thưởng, charge, double reward và roll lens theo từng đợt. Duplication/Scales giữ hành vi native, gồm Grole Egg thay cả đợt. Dùng cầu nối native có kiểm tra layout, tránh lỗi ABI Nullable của Harmony.
- NonEquipment/BossLoot giữ nguyên khối mỗi callback ở từng frame. Chuyển map/exit khi còn queue sẽ hoàn tất đồng bộ trước native cleanup; có thể chậm lúc chuyển map sớm. Queue không được phục hồi sau process crash/kill.
- Bộ đo death hitch và runtime report tự cập nhật khi offline gameplay sẵn sàng, không cần mở F1.

## Kiểm tra và giới hạn

Build Release không warning/error; 1.053 managed/TCP checks, 245 actual-source feature checks, 188 compiled/native metadata audit, 153 native assertions và 15 updater fixture checks PASS. Native QA trên profile riêng kiểm tra actor khác scene → acquire → Activate, nullable lens rỗng + 12 lens, các nhánh Duplication/Scales, native trigger 12 charge dùng 4 còn 8, đủ 80 item qua 81 frame, flush không nhân đôi. Ground delivery được chặn sau native generation trong QA; không phải phép đo hitch/pursuit thực tế. Kết nối Internet giữa hai máy cần xác nhận khi chơi.

## Cài đặt

Thoát game, chạy update.bat hoặc giải nén ZIP vào thư mục game. Updater dùng manifest và SHA256 để cập nhật cặp DLL, có backup. ZIP không chứa helper QA hay profile nhân vật. Cập nhật cả hai máy trước khi kết nối.
