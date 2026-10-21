# dev.41 — bố trí Shared Stash theo giao diện gốc

Người dùng xác nhận dev40 khởi động và Shared Stash cất/rút được; ảnh cho thấy nút Configure Category thừa, nền chữ nhật đặc, icon chồng chữ và dấu+ đè tên category.

- Bỏ việc bind/hiện lại nút ConfigureActiveCategory trong thanh dưới. Có fallback nhận diện label Configure Category; cấu hình category qua chuột phải hàng category như trước.
- Bề mặt Image được thêm chỉ để bắt click giữ alpha.001. Nếu Button dùng chính surface đó, tắt ColorTint trên surface để trạng thái normal không đổi nó thành hình chữ nhật đặc. Nếu Button trỏ vào artwork native khác thì giữ transition gốc.
- Khôi phục toàn bộ SimpleLayoutGroup native trên panel và các mẫu tab/category sau khi gỡ controller riêng. Copy spacing/padding/alignment, updateMode, adaptive/minimum size và từng element, remap RectTransform/layout reference sang clone. Tạo danh sách/cache mới, bỏ event nguồn; không giữ tham chiếu vào hòm riêng.
- Mỗi bản sao tab/category cũng remap danh sách phần tử riêng. Refresh ContentSizeFitter và layout từ con lên cha, rồi hàng tab/category đặt dấu+ theo nội dung hiện tại. Không dùng bề rộng cố định hoặc padding ước lượng từ tên tab cũ; icon0 dùng quy tắc native là ẩn GameObject.
- Chỉ đưa tab thuộc category đang chọn vào layout hàng; V2 giữ layout header, chỉ dựng lại danh sách ngăn/folder động. Không thay cửa sổ, artwork, font hoặc tạo thanh nút tự chế.

Mod1.1.0-dev.41, protocol43; Core không đổi so với dev39/dev40. Bao gồm sửa crash dev40 và stash/tooltip dev39. Không đổi data/journal/transfer hoặc Corruption.

Build0warnings/errors;10actual-source layout stub checks;95compiled/native audit checks. Kiểm remap phần tử/cache/delegate, kế thừa cấu hình gốc, thứ tự reflow, không nhân đôi component và không tô đặc hit surface. Audit giữ chặn unstripping và native shared-noop detour. Không chạy lại toàn bộ managed/TCP vì Core/data không thay đổi.

User đã thoát game để cài. Không tự mở/đóng game; chưa xác nhận hình render mới trong Unity. Cần nghiệm thu đổi tên dài/ngắn, chọn/bỏ icon, thay category và dấu+; xem validation/dev41-steam-install.json cho SHA/backup. Gói local-releases/TM-Coop-1.1.0-dev.41-stash-layout-candidate.zip. Không tự phát hành GitHub; root/latest vẫn dev35.
