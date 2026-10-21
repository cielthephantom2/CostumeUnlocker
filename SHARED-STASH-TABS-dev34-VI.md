# Shared Stash nhiều ngăn + native controls — dev.34 candidate / protocol41

Bản kết hợp dev.34/protocol41 được chuẩn bị phát hành theo yêu cầu người dùng. Root DLL pair và latest.json đã cập nhật dev.34; xem RELEASE-1.1.0-dev34-VI.md và validation/release-dev34-* cho trạng thái phát hành và bằng chứng. Bao gồm Weaver Imprint x2. Chưa cài Steam, chưa nghiệm thu native UI/gameplay.

## Hoạt động F2

- Mặc định **6 ngăn chung ×96 món =576 món**. Cả Host và các Client đều có thể dùng mọi ngăn trong cùng cycle/mode.
- Nút **+** thêm ngăn, tối đa **24 ngăn /2.304 món**. Thêm ngăn của hòm chung không trừ vàng hòm riêng.
- **SORT** dùng ItemContainer.TrySort native trên một buffer hiển thị tách riêng đủ chứa toàn bộ món của ngăn, kể cả các trang lưới tràn. Host chỉ nhận hoán vị ID hợp lệ và lưu thứ tự; không gửi thêm item hoặc thay bytes/NativeId.
- **CONFIGURE TAB** dùng StashConfigureTabModalV2 của game để đổi **tên, màu và icon**. Các mục folder/priority/delete/duplicate chưa thuộc mô hình hòm chung được ẩn trong lần mở modal này; khôi phục trạng thái widget sau khi đóng để không ảnh hưởng hòm riêng.
- Bản V2 sao chép artwork của tab và ba nút native, thay callback bằng thao tác hòm chung. Không giữ controller hòm riêng. ScrollRect/Scrollbar thuần UI được giữ, callback serialized cũ bị tắt. Tab có hit target riêng luôn tồn tại khi mất highlight. Ẩn count/priority của hòm riêng bị sao chép; số món của ngăn chung hiển thị ở footer.
- Với prefab cũ không có NewStashTabbedUIControls, có thanh tab cuộn và nút dự phòng; không cam kết giao diện V1 giống V2 hoàn toàn.

## Lưu đồ và đồng bộ

- Ledger cũ không có thông tin tab được đọc vào **ngăn 1**. Giữ ChestId, item bytes, NativeId, operation IDs, các lượt rút đang reserved và client escrow cũ. Không xóa hoặc tái tạo hòm.
- Một ledger duy nhất giữ custody xuyên các ngăn. Không thể gửi cùng NativeId vào hai ngăn; chuyển món đã rút sang ngăn khác vẫn cần đúng custody ticket.
- Client journal lưu tab gốc của giao dịch. Đổi ngăn/config/sort bị chặn trong lúc đang lấy/cất hoặc save chưa xong.
- Chỉ gửi nội dung **ngăn đang xem**, tối đa96 món mỗi reply. Sáu hoặc24 ngăn không bị gửi nguyên khối mỗi lần. Hai Client có thể xem các ngăn khác nhau.
- Thêm/config/sort có operation ID và được Host lưu bền vững. Retry sau mất kết nối/Host restart không tạo thêm tab hoặc replay giao dịch item. Revision cũ trả conflict để tránh ghi đè thao tác mới của người khác.
- Reply của ngăn cũ không thay view đang chọn. Khi người khác đổi ngăn khác, không rebuild lưới item nếu nội dung ngăn hiện tại không đổi.
- Protocol tăng **40→41**: Host và tất cả Client cần cặp DLL cùng bản mới khi cài. Chưa cài vào game đang chạy.

## Kiểm chứng

- Build Release **0 warnings/errors**.
- **969 managed/TCP checks PASS**: đủ576 món, per-tab capacity/reservations, restart, cross-tab custody, invalid query/sort, rename/color/icon persistence, concurrent stale edits/adds, retry, legacy JSON và legacy reserved withdrawal, Host +2 Client TCP xem ngăn riêng biệt.
- Hai reply gồm96 item với payload tối đa mỗi reply vẫn dưới600KB, thay vì gửi toàn bộ nhiều ngăn; giữ giới hạn frame hiện có.
- **53 compiled audit checks PASS**,139 lớp patch,72 chọn/67 bỏ. Kiểm native Sort, native Configure modal, callback Host, hit targets, selected tab và protocol41; giữ shared-world/Encounter disabled.
- **20 Weaver actual-source/stub fixture checks PASS**, giữ tính năng Imprint x2.
- **Chưa chạy native gameplay/UI/FPS hoặc nghiệm thu hình ảnh**. Các kiểm tra .NET/TCP/metadata không chứng minh bố cục, tương tác modal và native Sort hoạt động đúng trong engine. Không mở hoặc đóng bất kỳ game nào.

Bằng chứng: validation/dev34-shared-tabs-*. Gói ứng viên: local-releases/TM-Coop-1.1.0-dev.34-shared-tabs-candidate.zip. Khi cài sau này cần thay cả Mods/LastEpochLAN.dll và UserLibs/LastEpochLAN.Core.dll trên mọi máy.
