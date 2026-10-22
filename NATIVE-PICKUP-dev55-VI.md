# Gỡ World Assist và sửa pickup — dev.55

Giữ version **1.1.0-dev.55 / protocol 44**. Cài cả hai DLL theo manifest mới.

- Đã xóa toàn bộ source World Assist: danger areas, world targets, routes, dự đoán vị trí, settings và các hook ability/map. F4 vẫn mở Mod Settings; submenu World Assist đã bỏ. F1 Combat Assist và các tiện ích minimap cũ giữ nguyên.
- Xóa bộ quét ground-item mỗi frame, mẫu chuyển động, điều kiện chỉ nhặt khi di chuyển và hook chặn auto pickup native của bản trước.
- Gold, potion và Ancient Bones dùng tương tác nhặt có sẵn của game. Collider trigger lấy bán kính từ prefab gốc và nhân **3**, không nhân dồn khi tái dùng pool. Tắt enhancement trả về phạm vi gốc; potion vẫn để game xử lý số charges.
- Keys và crafting materials dùng lại trigger gold trong pool native, cùng phạm vi x3 của gold. Khi chạm trigger, chuyển sang `pickupItem` gốc với storage native. Đây là phần nối cho hai loại chưa có auto pickup gốc; không thêm managed Update hoặc quét danh sách loot. Không cộng đồ lúc spawn và không kéo loot về nhân vật.
- Trigger được gắn vào vị trí loot, giữ world scale và layer va chạm của local actor giống luồng gold gốc. Trả về pool khi loot được nhặt, visual bị pool/hủy, hoặc trước khi đổi zone. Cleanup không chạy sau khi scene mới đã tạo lại đồ.
- Mỗi key/material có trigger riêng; chặn secondary vacuum chỉ trong lượt tự nhặt đó để không hút đồ ngoài trigger hoặc loại đã tắt. Click thủ công và Range Pickup vẫn dùng hành vi hiện tại.
- CoF, The Woven, Forgotten Knights, stability, Meteor, Archmage và Tranquility giữ nguyên các chỉnh sửa trước.

## Kiểm tra trong game

1. Đóng game, chạy `update.bat`, mở lại game và xác nhận F4 không còn World Assist.
2. Thử gold, potion khi thiếu charges, Ancient Bones ở khoảng cách xa hơn trước. Phạm vi là x3 bán kính native từng loại, không phải 3 distance cố định.
3. Đến gần Keys/Materials rồi đứng yên: vẫn tự nhặt theo trigger native; kiểm tra storage tăng đúng và loot biến mất một lần. Equipment/tutorial item không tự nhặt.
4. Potion đầy vẫn nằm lại. Tắt từng enhancement: gold/potion/bones về phạm vi gốc; Keys/Materials ngừng auto.
5. Đi qua lại zone/echo có loot còn lại và thử nhặt bằng click với Range Pickup bật/tắt; kiểm tra FPS và log MelonLoader.

## Kiểm chứng kỹ thuật

Prefab native đã được đọc trực tiếp từ `PermaLoad.bundle`: gold/potion/bones đều có SphereCollider trigger, WorldAreaEnterListener nghe tag Player và PickupGroundObjectInteraction. Radius gốc được đọc từ chính prefab runtime, không hardcode theo số trong text. Native gold tạo object bằng pool, đặt layer theo actor rồi initialise; luồng mới giữ cùng trình tự.

Fixture actual-source kiểm tra radius x3, không nhân dồn, reset khi tái dùng pool, ownership, collision layer, keys/materials, cleanup và exception. Audit compiled xác nhận World Assist và scanner cũ vắng mặt, các hook native mới được chọn cài đặt. **Chưa xác nhận physics/pickup thực tế hoặc FPS trong game với DLL này.**

Release build: 0 warnings/errors. **34 actual-source pickup checks** và **145 compiled/native audit checks** pass (190 patch classes inspected). Bằng chứng trong `validation/dev55-native-pickup-*`.
