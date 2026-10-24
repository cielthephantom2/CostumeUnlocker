# Dev50 — gỡ Delete Tab khỏi Shared Stash

Theo yêu cầu người dùng, gỡ chức năng thêm nút **DELETE TAB** cho tab con hàng dưới như Unique/Set: không còn code ép hiện nút, đổi nhãn, bật phần tử cha, di chuyển nút, hook OnModalOpen hoặc lệnh xóa tab từ Client. Giữ **Delete Category** gốc của nhóm hàng trên; General vẫn được bảo vệ.

Không xóa/reset dữ liệu hòm. Giữ cấu trúc Deleted/Generation và bộ xử lý dữ liệu protocol44 trong Core để đọc dữ liệu hiện có và giữ an toàn các giao dịch cũ; giao diện bản này không gửi lệnh xóa tab con.

Giữ thay đổi Meteor: Extinction33% mỗi điểm; Heat Flux2 Frost Claw mỗi điểm, mỗi lần dùng100% chi phí mana bình thường. Tooltip sửa trực tiếp số của dòng gốc và bỏ đoạn Offline bonus riêng cho Meteor. Giữ các thay đổi layout chữ gần nhất, nhưng lỗi chữ bị cắt do người dùng báo chưa được xác nhận đã hết trong engine.

Các tính năng Imprint, Room Ladder, minion tìm mục tiêu x2 và Shared Stash còn lại giữ nguyên. Search chỉ tìm trang lưới đang hiển thị. Trường hợp Corruption100+60→400 vẫn hoãn, không tuyên bố đã sửa.

**Protocol44 — cập nhật cả hai DLL trên Host và mọi Client.** Thoát game trước khi chạy update.bat hoặc chép DLL. Gói không chứa interop hay công cụ sửa MelonLoader. Chưa nghiệm thu render/click/chiến đấu trong game. Chi tiết build và kiểm tra trong validation/dev50-* và validation/release-dev50-*.
