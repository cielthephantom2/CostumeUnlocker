# dev47 — Meteor và Delete Tab cho Shared Stash

**Protocol44. Host và mọi Client phải cập nhật cả hai DLL**, dùng cùng bản game, rồi chọn Play Offline. Thoát game trước khi chạy `update.bat` hoặc chép `Mods/LastEpochLAN.dll` và `UserLibs/LastEpochLAN.Core.dll` từ ZIP.

## Meteor

- **Extinction:** Large Meteor chance10%→33% mỗi điểm;1/2/3 điểm tương ứng33%/66%/99%, tối đa100%. Giữ các bonus crit/damage khác của node.
- **Heat Flux:**1→2 Frost Claw mỗi điểm. Mỗi lần dùng100% chi phí mana bình thường của Frost Claw, thay120%; tổng mana phụ thuộc số lần cast. Giữ yêu cầu mục tiêu, các modifier mana và kiểm tra khả năng chi trả của game. Frost Claw dùng trực tiếp không bị thay đổi.
- Tooltip cũ và V2 khớp các chỉ số mới, giữ giao diện gốc. Điều chỉnh mana chỉ trong scope impact Meteor của nhân vật local offline, được dọn bằng finalizer. Không sửa asset, điểm kỹ năng, RNG chung hoặc hoàn mana sau khi cast.

## Shared Stash

- Tab con hàng dưới như Unique/Set có **DELETE TAB** trong hộp Configure gốc. Chỉ cho phép xóa tab rỗng, không còn đồ đang chờ xác nhận rút; luôn giữ ít nhất một tab hoạt động. Host kiểm tra lại trạng thái tại thời điểm xử lý.
- Không đánh lại ID của các ngăn còn lại. Nút+ tái sử dụng vị trí trống với generation mới; giao dịch cũ không được gửi đồ vào ngăn thay thế. Receipt của giao dịch đã hoàn tất không tạo thêm đồ. Client đang xem tab bị xóa sẽ chuyển sang ngăn còn hoạt động.
- Tab đã xóa không tham gia priority hoặc danh sách chọn. Nhãn/trạng thái/callback modal được phục hồi khi đóng để hòm cá nhân tiếp tục dùng đúng trạng thái native. Category hàng trên giữ Delete Category, General được bảo vệ.
- Dữ liệu cũ dùng mặc định active/generation0, không reset hòm. Sao lưu trước cập nhật. Nếu cần quay về DLL cũ sau khi đã xóa/thêm tab bằng bản mới, khôi phục dữ liệu từ bộ sao lưu tương ứng.

## Giữ các tính năng dev45

Sealed12% với1 điểm, Warlord6%, Echoing công thức x2 và hiển thị tối đa100%; Commoner x2 công thức, Woven/Loomed x2 trọng số. Vào Echo mới để map nhận xác suất Sealed. Room Ladder, minion tấn công tìm mục tiêu x2, Shared Stash native layout/config/priority và sửa crash/unstripping được giữ.

## Kiểm tra và giới hạn

Build Release0warning/error. **1.037 managed/TCP checks**, **118 compiled/native audits**, fixture từ nguồn thực **Meteor34 / Weaver43 / layout18 / lifecycle11 / priority12** đạt. Kiểm tra mới bao gồm xóa tab đang có đồ/reservation, giữ ID, tái dùng hơn24 lượt, generation, restart/journal và Host+haiClientTCP. Native audit kiểm tra đúng overload mana, consumer RNG/copy count và tránh hook shared no-op.

Các kiểm tra này chưa xác nhận render/click hay chiến đấu thực trong engine. Search Shared Stash vẫn chỉ tìm trang lưới đang hiển thị. Shared world/combat/travel vẫn tắt. Trường hợp Corruption100+60→400 vẫn đang được hoãn kiểm tra, không tuyên bố đã sửa. Gói phát hành không kèm interop hoặc công cụ sửa MelonLoader.

Chi tiết: [Meteor](https://github.com/cielthephantom2/CostumeUnlocker/blob/main/METEOR-NODES-dev46-VI.md), [Delete Tab và dữ liệu](https://github.com/cielthephantom2/CostumeUnlocker/blob/main/SHARED-STASH-DELETE-dev47-VI.md). Đối chiếu cặp DLL bằng `latest.json`; báo cáo kiểm tra trong `validation/dev47-*` và `validation/release-dev47-*`.
