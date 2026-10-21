# dev45 — Shared Stash, Ladder phòng, minion và Imprint

Protocol **43**. Host và tất cả Client phải cập nhật **cả hai DLL**, dùng cùng bản game và chọn Play Offline. Thoát game rồi chạy `update.bat` trong thư mục game, hoặc chép cặp DLL từ ZIP. Gói này không kèm interop hay công cụ sửa MelonLoader.

## Thay đổi từ bản public dev35

- Shared Stash dùng artwork và hộp Configure gốc: tên, icon, màu, category và các điều kiện priority. Duplicate tạo ngăn rỗng sao chép cấu hình; không nhân bản đồ. Category có cấu hình bằng chuột phải, thêm/xóa/chuyển tab; dữ liệu do Host lưu. Giữ6 ngăn ban đầu, tối đa24 ngăn, mỗi ngăn96 món.
- Sửa lỗi Method unstripping failed khi cuộn, khôi phục layout gốc và bề mặt bắt click trong suốt, bỏ nút Configure Category thừa. Đo lại chiều rộng nhãn theo font gốc để sửa Unique→Un và Shared→Sh. Chuẩn bị hòm cá nhân ở trạng thái ẩn và tái dùng UI Shared khi bấm F2.
- Ladder dùng bảng gốc với dữ liệu nhân vật của thành viên hiện đang trong phòng: Arena, level/XP, Corruption và Omen. Lọc class/mode/cycle, cập nhật khi đổi nhân vật hoặc rời phòng. Không có lịch sử toàn server hoặc điểm party2/3/4 giả lập.
- Minion/companion thuộc nhân vật local ở stance tấn công có phạm vi phát hiện mục tiêu x2 trong hai đường tìm mục tiêu đã kiểm tra. Giữ tầm đánh gốc và stance bảo vệ; hoàn trả trường tạm khi kết thúc lời gọi.
- Sửa thông báo lưu category thành công bị ghi rejected và mô tả phòng chưa nhắc Ladder.
- Giữ sửa crash lúc khởi động: không detour hàm Ladder HandleSeasonEventUI dùng chung native stub.

## Imprint

| Node | Mức áp dụng |
|---|---|
| Sealed Riches |12% với1 điểm, 0 điểm vẫn0. Chuẩn hóa theo tooltip gốc6% người dùng kiểm tra, thay cho5.8% trong bản cũ.|
| Warlord's Riches |6% mỗi điểm, từ3%.|
| Echoing Riches |Công thức xác suất nhận pile khi nhận thưởng Echo x2; tooltip Maximum chance50%→100%. Không đảm bảo mọi Echo đều rơi.|
| Commoner's Riches |Tử số xác suất theo spawner/enemy/Corruption x2; không có tỷ lệ cố định chung cho mọi map.|
| Woven / Loomed Riches |Trọng số spawn tự nhiên của Echo imprint x2, giữ điều kiện đủ; tỷ lệ cuối sau chuẩn hóa không nhất thiết nhân đôi tuyệt đối.|

**Vào Echo mới sau khi cập nhật** để Sealed nhận xác suất map mới. Native vẫn chứa hằng số cũ .029; dev45 chủ động đặt mục tiêu .12 từ mức6% hiển thị, không tiếp tục tuyên bố .058 khớp yêu cầu. Runtime có `cacheChance` và `cacheRollChance`; với1 điểm ở Echo mới, cần kiểm tra cả hai khoảng0.12. Tooltip cũ và V2 đều có đường sửa. Xem [phân tích đầy đủ](WEAVER-RATE-CORRECTION-dev45-VI.md).

## Kiểm tra và giới hạn

Build Release0warning/error; **1.014 managed/TCP checks**, **103 compiled/native audit checks**, **43 Weaver fixture checks** đạt. Đợt rà soát cùng nguồn Shared Stash/Ladder/minion còn có79 fixture checks đạt: layout18, lifecycle11, geometry5, priority11, Ladder18, minion16. Fixture dùng stub không phải kiểm thử render hay xác suất rơi bằng thực nghiệm. Audit có đối chiếu native sao chép xác suất faction→map và cache đọc trường map cho RNG. Không tuyên bố nghiệm thu gameplay hoàn chỉnh.

Search Shared Stash hiện chỉ áp dụng trang lưới đang hiển thị. Shared world/combat/travel vẫn tắt. Trường hợp Corruption100+60→400 người dùng đã hoãn kiểm tra, chưa tuyên bố sửa xong và không sửa save. Chỉ hỗ trợ các SHA GameAssembly đã được kiểm tra; không mở khóa mọi phiên bản game.

Đối chiếu cặp DLL với `latest.json`; bằng chứng build/test/package trong `validation/dev45-*` và `validation/release-dev45-*`. Bản Steam local đã được sao lưu và cài cùng cặp DLL này, không tự mở/đóng game hoặc đổi trạng thái MelonLoader.
