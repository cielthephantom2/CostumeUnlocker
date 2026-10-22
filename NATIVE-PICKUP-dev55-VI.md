# Auto Pickup radius 5 — dev.55 / protocol 44

Đã quay lại cơ chế quét pickup cũ theo yêu cầu mới. **Bán kính cố định 5 world units trên mặt phẳng XZ**, cùng đơn vị vòng Combat Assist; nhặt cả khi đứng yên. Không dùng hệ số x3 của trigger native.

- Gold, Potion, Keys, crafting Materials và Ancient Bones tự nhặt trong vòng5; ngoài vòng giữ trên đất.
- Xóa toàn bộ cơ chế tạo/parent trigger gold cho Keys/Materials, các hook initialise/pooling/scene của cơ chế đó và mẫu chuyển động. Không còn yêu cầu di chuyển.
- Bộ quét chạy tối đa mỗi0.05giây, tối đa8 thao tác; giới hạn số phần tử duyệt mỗi lượt. Nhặt qua API gốc của game theo id, không cộng trực tiếp vào túi hoặc kéo loot về nhân vật.
- Giữ delay nhặt gốc. Potion đầy vẫn nằm lại. Equipment/tutorial item không tự nhặt. Bật/tắt từng loại trong F4 → Mod Settings.
- Lượt auto key/material không kích hoạt vacuum mở rộng ra ngoài vòng5. Click thủ công và Range Pickup giữ hành vi trước đó.
- World Assist vẫn được gỡ toàn bộ. F1 Combat Assist và các chỉnh sửa faction/skill/stability giữ nguyên.

Kiểm chứng: build Release 0 warnings/errors; 30 actual-source pickup checks kiểm tra đứng yên, biên5/5.001, các loại loot, toggle, delay, scope và giới hạn8 thao tác. Compiled/native audit xác nhận radius5, không còn motion gate hoặc trigger bridge. Gameplay trong game chưa được xác nhận với DLL mới.

Đóng game rồi chạy `update.bat` để nhận cặp DLL mới, vẫn version1.1.0-dev.55/protocol44.

## Sửa điều kiện sẵn sàng của gameplay

Ảnh tiếp theo cho thấy shard/rune vẫn trên đất dù bản radius5 đã được nạp đúng, các toggle bật và log không có exception. Kiểm tra source phát hiện pickup dùng chung `OfflineToolkit.Ready`, gồm cả `actorVisuals.HasBeenInitialized` và character-data readiness; điều kiện này có thể chặn toàn bộ bộ quét mà không báo lỗi. Bản mới chỉ yêu cầu offline, đúng local actor và `itemContainersManager` tồn tại. Không thay điều kiện Ready của các tính năng khác.

Bộ quét đọc trực tiếp `GroundItemManager.offlineItems` giống nhánh offline native, bỏ bước chọn list qua out-parameter. Cursor không bỏ qua phần tử bị dồn lên sau khi nhặt. Material nhận diện bằng helper native: types101–103 (crafting),104 (key), đã đối chiếu mã native; không đổi xác suất hoặc nhân loot.

Thêm dòng trạng thái pickup trong F4 Mod Settings và log `Auto Pickup:`. Log báo rõ waiting/active; khi có đồ sẽ ghi tối đa mỗi5giây: ground count, số special đã duyệt, khoảng cách bình phương gần nhất, attempts và collected (theo phần tử thực sự bị xóa khỏi list). Giữ bán kính5 và nhặt khi đứng yên.

36 actual-source pickup checks pass, gồm inventory có sẵn nhưng model/network Ready=false, cả3 crafting types, chặn online và kết quả native list. Build0 warnings/errors. Chưa xác nhận bản sửa điều kiện này trong game; log cũ chưa có dữ liệu để khẳng định cờ readiness là nguyên nhân duy nhất.
