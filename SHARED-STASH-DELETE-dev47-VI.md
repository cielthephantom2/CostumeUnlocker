# Shared Stash dev47 — Delete cho tab con

Giữ toàn bộ Meteor dev46, gồm tooltip cũ/V2: Extinction33% mỗi điểm, Heat Flux2 Frost Claw mỗi điểm và100% mana bình thường cho mỗi Frost Claw. Không thay đổi bonus crit khác của Extinction.

## Delete Tab

Hộp Configure Tab của hàng dưới (Unique/Set/...) sử dụng nút Delete gốc, nhãn **DELETE TAB**. Nút chỉ cho phép xóa khi tab rỗng, không có đồ đang được Host giữ chờ xác nhận rút, và vẫn còn ít nhất một tab khác. Host kiểm tra lại mọi điều kiện tại thời điểm xử lý, kể cả khi preview trên Client đã cũ. Category hàng trên tiếp tục dùng Delete Category; General gốc được giữ.

Chỉ xóa cấu hình tab rỗng; không di chuyển hoặc xóa đồ. Các tab khác giữ nguyên ID, vị trí lưu đồ và lịch sử giao dịch. Ngăn đã xóa được ẩn khỏi hàng tab, danh sách chọn và routing priority. Nếu tab đang được chọn bị người khác xóa, Client chuyển sang tab còn hoạt động.

Nút+ tái sử dụng vị trí trống với **generation mới**, không đổi ID của các ngăn còn lại và không làm cạn giới hạn24 ngăn sau nhiều lượt xóa/thêm. Generation đi cùng deposit/withdraw/ack và được lưu trong client journal. Giao dịch cũ chưa từng được Host nhận không được gửi vào một ngăn thay thế; thao tác rút chưa cấp đồ bị hủy, deposit đã đưa vào escrow được giữ để xem xét. Receipt của giao dịch đã hoàn tất vẫn có thể trả lại mà không nhân bản đồ. Cấu hình modal cũ không được ghi đè lên tab đã được thay thế.

**Protocol44: cập nhật cả hai DLL trên Host và mọi Client.** Dữ liệu cũ mặc định active/generation0, không reset hòm. Sao lưu DLL và SharedChest trước cài. Không dùng DLL cũ ghi lên dữ liệu sau khi đã thực hiện xóa/thêm tab bằng bản mới; khi cần quay lại, dùng bộ sao lưu tương ứng.

Nhãn, trạng thái nút và callback modal được phục hồi sau khi đóng, để hộp cấu hình hòm cá nhân tiếp tục dùng trạng thái native. Không dựng panel/nút đồ họa mới.

## Kiểm tra

Build0warning/error;1.037 managed/TCP checks và118 compiled/native audits đạt. Trong đó có23 kiểm tra mới về xóa, reservations, giữ ID, tái dùng hơn24 lượt, generation, restart/journal, receipt cũ, ngăn cuối cùng và Host+haiClientTCP. Fixture từ nguồn thực: Meteor34, Weaver43, layout18, lifecycle11, priority12 đều đạt. Các kiểm tra này chưa phải nghiệm thu render/click hoặc chiến đấu trong game. Cần mở Configure của tab rỗng và tab có đồ để kiểm tra nút, rồi kiểm tra Meteor có đủ điểm.

Dev47 được phát hành theo yêu cầu người dùng; xem RELEASE-1.1.0-dev47-VI.md. Không tự mở hoặc đóng game, không hẹn tắt máy. Bằng chứng cài Steam trong validation/dev47-steam-install.json.
