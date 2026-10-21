# dev.35 — Shared Stash dùng giao diện gốc

Người dùng cung cấp ảnh dev.34: hàng số1–6 và nút phẳng phía trên bị chồng với hàng tab và nút Sort/Configure/+ native. Yêu cầu giữ giao diện gốc, không thêm UI tự tạo.

## Thay đổi

- Nhận diện cả StashPanel/StashTabbedUIControls và StashPanelV2/NewStashTabbedUIControls. Dev.34 chỉ nhận bộ mới nên Stash cũ rơi vào nhánh dựng nút/tab thay thế.
- Lấy trực tiếp Sort/Configure/+ từ bộ điều khiển hoặc navigation native; lấy tabPrefab/ảnh/icon/màu/trạng thái chọn của native. Legacy dùng lại SimpleLayoutGroup với cấu hình gốc và danh sách phần tử mới; giữ ScrollRect và nút cuộn tab gốc.
- Xóa nhánh tạo hàng số, nút phẳng, nút X, nút phân trang, nhãn trang/số món và overlay trạng thái riêng. Không còn phương thức dựng Button/Label/AddControls. Thiếu control native sẽ báo lỗi qua trạng thái/log hiện có, không tự tạo giao diện thay thế.
- Giữ Unity Button cùng sprite/color/transition, thay event bằng đối tượng mới để loại cả persistent callback hòm cá nhân. Sort/config/add/tab tiếp tục gửi lệnh Host; không gắn projection vào save hòm riêng.
- Giữ ô TMP Search gốc, bỏ callback hòm cá nhân và dùng ItemContainerUIWithSearch riêng cho projection. Search áp dụng trang đang hiển thị, không tìm đồ hòm cá nhân hoặc toàn bộ tab từ Host.
- Giữ một category native mang tên Shared Stash. Folder/add-folder, cycle merge và view dọc cá nhân không được hỗ trợ và được ẩn hoặc bỏ callback. Không tuyên bố đã sao chép mọi chức năng stash cá nhân.
- Không thêm nút cho các trang lưới tràn: cuộn chuột trên lưới để xem trang khác nếu96 món không vừa diện tích. Dữ liệu96 món/ngăn và custody/journal không đổi.

## Kiểm chứng và giới hạn

- Build Release **0 warnings/errors**, source Mod **1.1.0-dev.35**, protocol **41**; Core không đổi so với dev.34.
- **969 managed/TCP checks PASS**; **59 compiled audit checks PASS**, gồm59 ràng buộc về code/DLL và việc không tạo UI thay thế. 139 patch classes,72 chọn/67 bỏ.
- Evidence: validation/dev35-native-stash-*. Các kiểm tra này không chạy Unity, không xác minh click/raycast/scroll/search/modal/bố cục hoặc FPS trong game. **Chưa nghiệm thu native UI/gameplay**.
- Báo cáo này mô tả triển khai UI trước game update. Bản kết hợp mới nhất thêm SHA Steam26087601, đã được cài local theo yêu cầu người dùng. Xem RELEASE-1.1.0-dev35-VI.md và validation/release-dev35-* cho bản phát hành; không sửa Corruption/save. selftest-result.json cũ giữ nguyên.

Gói phát hành kết hợp mới nhất: TM-Coop-1.1.0-dev.35.zip, gồm sửa UI và hỗ trợ game update. Các ZIP native-stash-candidate cũ chưa thêm SHA game mới, không dùng để cập nhật client sau game update.
