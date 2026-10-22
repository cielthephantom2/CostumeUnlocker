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
