# dev.35 — Shared Stash dùng UI gốc và hỗ trợ game update

Protocol **41**. Host và tất cả Client cập nhật cặp DLL; mọi máy phải dùng cùng bản game. Thêm hỗ trợ Steam build **26087601** / Unity6000.4.8f1 bằng đúng SHA256 GameAssembly đã kiểm tra. Giữ hỗ trợ build cũ, tiếp tục chặn các build chưa biết.

## Shared Stash

- Dùng nút **SORT / CONFIGURE TAB / + / đóng**, hàng tab và ô **Search** của game, giữ artwork/bố cục native cho cả Stash cũ và Stash V2.
- Bỏ hàng số1–6, nút phẳng, nút phân trang và nhãn/overlay tự tạo bị chồng với UI gốc ở dev.34.
- Tab dùng tên/màu/icon và trạng thái chọn native; Sort/config/add tiếp tục thông qua Host, không ghi vào hòm cá nhân.
- Search áp dụng trang lưới đang hiển thị. Nếu đồ tràn diện tích lưới, cuộn chuột trên lưới để xem trang khác. Folder/priority/cycle merge/view dọc cá nhân không được sao chép thành chức năng hòm chung.
- Giữ6ngăn×96món, nút+ mở rộng tối đa24ngăn, dữ liệu/journal/custody và Weaver Imprint×2 của dev.34.

## Hỗ trợ bản game mới

Sửa cảnh báo **Unsupported game version** bằng cách bổ sung đúng GameAssembly SHA256 của Steam26087601:

`323E9183F3BEE600B6C9E1C469218B9DE926E9044F2CA3D44FAE8A0EC9A91464`

Không mở hỗ trợ mọi phiên bản. Gói phát hành chỉ gồm mod, updater và tài liệu; không kèm bộ DLL hoặc công cụ sửa interop.

## Kiểm chứng và giới hạn

Build Release **0 warnings/errors** với interop của game mới; **969 managed/TCP checks** và **60 compiled audit checks PASS**. 139 patch classes,72 chọn/67 bỏ. Bằng chứng updater/SHA256 ở `validation/release-dev35-*`.

Log sau sửa interop đã ghi nhận mod35 nạp được, support module nạp được và72hooks cài thành công trước khi bổ sung SHA mới. **Chưa nghiệm thu native Shared Stash UI/gameplay/FPS** hoặc khởi động lại game sau bản hỗ trợ SHA mới. Kiểm tra .NET/metadata không thay thế kiểm thử trong engine.

Giữ Corruption hiện tại; không sửa save hay tuyên bố đã giải quyết trường hợp client100+60→400. Encounter/shared-world vẫn disabled, Internet giữ F2+Loot Filters. Lượt build/publish không mở/đóng game hoặc thay DLL đang cài Steam.

Thoát game khi thuận tiện rồi chạy `update.bat`, hoặc chép cả `Mods/LastEpochLAN.dll` và `UserLibs/LastEpochLAN.Core.dll` từ ZIP vào thư mục game trên mỗi máy.
