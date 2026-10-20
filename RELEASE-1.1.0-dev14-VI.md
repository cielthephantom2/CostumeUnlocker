# Bản 1.1.0-dev.14 — Shared Stash F2 và phản hồi giao dịch

Đóng game trên **Host và tất cả Client**, sau đó chạy `update.bat`. Bản này đổi cả Mod lẫn Core; không chỉ chép riêng Mod. Protocol **35**, thêm các field tương thích JSON.

## Thay đổi

- Thêm StashReady để trạng thái inventory không phụ thuộc việc borrowed Echo đã đồng bộ world hay chưa. Khi inventory thực sự sẵn sàng, giữ định danh giao dịch; vẫn giữ Available/Cooperative=false ở nhánh world chưa sẵn sàng. Loading/travel thực sự vẫn chặn giao dịch.
- Lấy/cất đồ hiển thị rõ điều kiện đang chặn: trạng thái nhân vật, identity, cycle/mode, solo mode, save, giao dịch đang chờ/held hoặc Host view cũ.
- Host trả lý do từ chối cho Client. Reply lỗi bị giới hạn độ dài, không chứa item/grant, không thay đổi journal hoặc ledger và không làm lộ đồ của cycle khác.
- Thông báo F2 có Canvas riêng để tránh bị che/cắt bởi giao diện native. Runtime report bổ sung số lần tương tác, request, giao dịch, lý do chặn và số giao dịch theo stage.
- Giữ nguyên custody, reservation, save ACK và chống lặp giao dịch. Không tự cấp/hoàn đồ hoặc xóa giao dịch Held.
- Bao gồm sửa icon/slider F1 của dev.13. Encounter vẫn tạm gác.

## Kiểm chứng và giới hạn

Build Release đạt **0 warning/error**, diff check đạt. Chưa chạy managed/native/visual hoặc nghiệm thu nhiều máy cho dev.14.

Người dùng báo room 1 Host + 2 Client: một Client giao dịch được, một Client chỉ xem được F2 và không thấy trạng thái. Chưa tái hiện hay xác nhận nguyên nhân cụ thể trên máy Client lỗi; **không khẳng định bản này đã xử lý dứt điểm**. Các kết quả dev.12 là lịch sử, không phải kiểm chứng Core mới của dev.14. Lỗi ActorVisuals trước đây chưa được giải quyết trong bản này.

SHA256 Mod: `4DDB810B6B0CB9165EFDC26EF1751165BFCB7FBD0B6A496F2CB4675E974AAFF6`

SHA256 Core: `4AE855E63CCD75E91099119387B3E295DBFC7E6C8AD7711F65502DCDD4631F6E`
