# dev.37 / protocol42 — Shared Stash configuration and lifecycle rework

Người dùng yêu cầu hoàn thiện Shared Stash sau khi ảnh dev36 cho thấy Configure bị thiếu Category/Priority/Duplicate và hòm cá nhân hiện trước F2.

## Thay đổi chức năng

- Dùng modal native đúng kiểu Stash cũ/V2. Bỏ việc truyền `IsLegacy=true` (đây là cờ dữ liệu hòm legacy của game, không phải lựa chọn phiên bản UI) và bỏ ẩn Category/Priority. Không dựng nút, cửa sổ hoặc thanh tab mới bằng giao diện tự chế.
- Tab lưu tên, màu, icon, category, priority và cờ bỏ qua priority của folder. Các điều kiện native được chuyển sang schema managed có giới hạn: Unique IDs, rarity, type/subtype, class, level, faction, Corruption, LP, Forging Potential, Weaver's Will và Weaver's Touch. Mở lại Configure đọc cấu hình từ Host.
- Hàng category dùng artwork/nút+ native. Chuột phải category để đổi tên/màu/icon/priority; chuyển tab qua Select Category. V2 dùng folder native và ngăn con; nút+ native: trái thêm tab, phải thêm category. Hòm cũ dùng hai nút+ ở hai hàng như game gốc.
- Duplicate tab sao chép **cấu hình sang một ngăn rỗng**, không sao chép món đồ. Duplicate category sao chép nhóm và cấu hình các ngăn vào các ngăn rỗng; nếu vượt24tabs thì từ chối toàn bộ. Xóa category chuyển các tab về General và giữ nguyên đồ; General không bị xóa.
- Các thao tác cấu hình lưu trên Host với operation ID, revision conflict và chống retry tạo trùng. Client khác nhìn thấy chung dữ liệu category/priority. 6tabs ban đầu, tối đa24tabs×96items và custody/journal cũ được giữ.
- Khi gửi đồ, ưu tiên tab có rule riêng khớp trước, rồi rule category được kế thừa, rồi ngăn đang chọn. Bỏ các ngăn đã đầy theo occupancy Host, gồm cả withdrawal reserved. Gọi native StashPriority.Match; chuyển tab và chờ snapshot trước khi bắt đầu giao dịch cất đồ. Nếu item đã di chuyển khỏi nguồn trong lúc chờ, không bắt đầu transfer. Nếu Host capacity đổi do concurrent deposits sau đó, giao dịch giữ theo cơ chế an toàn hiện có.
- F2 đóng chỉ ẩn projection để tái sử dụng trong cùng nhân vật/manager. Lần chuẩn bị UI đầu tiên vẫn cần native panel khởi tạo, nhưng hook có scope che toàn bộ CanvasGroup/Canvas nguồn trước render, không nhận click, rồi khôi phục trạng thái khi đóng nguồn. Không hiển thị hòm riêng như một bước thao tác. Load/cancel được dọn; character/manager/disconnect vẫn dispose.
- Bảo toàn nút/native layout, đo kích thước tên theo font native để giảm cắt chữ, cuộn đến tab đang chọn theo vị trí RectTransform thay vì chỉ số toàn bộ tab. Giữ Search native trên **trang đang hiển thị**; chưa triển khai tìm kiếm toàn bộ các tab hoặc view dọc đầy đủ như hòm riêng. Không tuyên bố mọi chức năng hòm cá nhân đã được sao chép.

## Tương thích và kiểm chứng

- Mod **1.1.0-dev.37**, protocol **42** vì cấu trúc category/priority mới. Host và mọi Client phải cập nhật **cả hai DLL**. Dữ liệu cũ thiếu category/rules mặc định về General, giữ ID và item/reservations. Hỗ trợ hai SHA game đã pin ở dev35, gồm Steam26087601.
- Build Release **0 warnings/errors**.
- **995 managed/TCP checks PASS**: Host+2Client metadata, sửa stale đồng thời, persist/restart, duplicate không sao chép vật phẩm, delete category giữ đồ, migration và maximum frame dưới1MB. Chỉ một reply stash mỗi frame để tránh metadata lớn làm tràn hai reply gộp.
- **70 compiled audit checks PASS**, 140lớp Harmony, 73chọn/67bỏ. Audit là metadata/IL, không nghiệm thu Unity.
- **11 actual-source native-priority stub checks PASS**; **10 actual-source lifecycle Unity-state stub checks PASS**. Kiểm codec đủ8condition messages, native Match dispatch/cache/routing và alpha/Canvas/input/cancel/restore. Stub không thay thế engine.
- Bản sao của ledger Host thực tế: **1ledger/33món/0reserved** đọc được bằng Core mới, dữ liệu file không bị viết lại khi đọc. Kiểm tra trên bản sao, không sửa trực tiếp ledger đang dùng.
- **Chưa chạy kiểm thử native UI/gameplay/FPS trong engine**. Cần nghiệm thu click, lựa chọn các rule, save/reopen, routing đồ thật, F2 nhiều lần và điều khiển gamepad. Không gọi bản này “hoàn mỹ” chỉ dựa trên build/test .NET.

## Cài và kiểm tra

Ứng viên kết hợp: `local-releases/TM-Coop-1.1.0-dev.37-stash-rework-candidate.zip`. Source/root/latest/GitHub vẫn tách: root/latest/GitHub hiện dev35, không tự phát hành. Nếu đã cài Steam, xem validation/dev37-steam-install.json để biết SHA và backup.

Kiểm thử khi người dùng mở game: F2 không hiện hòm riêng, Configure tab có Category/Priority, lưu rules rồi mở lại, thêm/rename category, duplicate tạo ngăn rỗng, cất món phù hợp vào đúng ngăn và restart Host kiểm tra33món cũ. Không đổi Corruption, không sửa save nhân vật; không tự mở/đóng game.
