# dev.34 — Shared Stash nhiều ngăn và Weaver Imprint ×2

Protocol **41**. Host và tất cả Client phải cập nhật **cả hai DLL**; không tương thích với protocol40.

## Shared Stash F2

- Mặc định **6 ngăn ×96 món =576 món**, nút **+** mở rộng tối đa **24 ngăn /2.304 món**. Mọi thành viên dùng chung các ngăn.
- **SORT** dùng thuật toán sắp xếp native trên vùng hiển thị riêng; Host lưu thứ tự món. **CONFIGURE TAB** đổi tên, màu và icon qua modal native.
- Dữ liệu hòm cũ vào ngăn1, giữ đồ và giao dịch đang xử lý. Một ledger giữ custody xuyên các ngăn; retry thêm/config/sort không tạo lặp giao dịch.
- Mỗi Client chỉ nhận ngăn đang xem. Khóa chuyển ngăn khi giao dịch hoặc lưu chưa hoàn tất.

## Weaver Imprint ×2

| Node | Thay đổi |
| --- | --- |
| Commoner's Riches | Xác suất từ công thức native ×2 |
| Sealed Riches | 5,8% mỗi điểm |
| Echoing Riches | Xác suất từ công thức native ×2 |
| Warlord's Riches | 6% mỗi điểm |
| Woven Riches | Trọng số xuất hiện ×2 |
| Loomed Riches | Trọng số xuất hiện ×2 |

Giữ điều kiện vật phẩm/node/Corruption, số món và affix roll. Trọng số Woven/Loomed không bảo đảm số Echo thực nhận tăng đúng hai lần. Giữ tỷ lệ Woven Potential dev.33 và điều kiện không nhận đồ0LP.

## Phạm vi và kiểm chứng

Build Release **0 warnings/errors**; **969 managed/TCP**, **53 compiled audit**, **20 actual-source/stub Weaver fixture checks PASS**. 139 lớp Harmony,72 được chọn/67 bỏ. Updater và SHA256 được lưu trong `validation/release-dev34-*`.

**Chưa nghiệm thu native UI/gameplay/FPS**, gồm Sort và Configure Tab trong game. Các kiểm tra .NET/stub/TCP không thay thế kiểm thử native.

Giữ Assist/DPS, Bloodrage và Corruption hiện có. Báo cáo client100→400 dù hiển thị+60 đang chờ người dùng tự test; bản này **không sửa và không tuyên bố đã giải quyết** trường hợp đó. Không tự chỉnh save. Encounter/shared-world vẫn disabled; phòng Internet giữ F2 và Loot Filters.

Không cài Steam, không mở hoặc đóng game trong lượt phát hành. Khi thuận tiện, thoát game rồi chạy `update.bat` hoặc chép cả `Mods/LastEpochLAN.dll` và `UserLibs/LastEpochLAN.Core.dll` từ ZIP vào thư mục game trên mọi máy.

## Phát hành đã xác minh

GitHub prerelease: https://github.com/cielthephantom2/CostumeUnlocker/releases/tag/v1.1.0-dev.34 . Đã tải lại ZIP và xác minh SHA256 của ZIP/cặp DLL. Updater fixture15/live17 checks PASS. Code commit7ced72d; bằng chứng validation/release-dev34-github-download.json và release-dev34-updater-live.json.
