# Auto Pickup hiện tại — dev.55 / protocol44

Bỏ bộ quét5Hz/5ứng viên gây chậm: nó phải đi qua trang bị và đồ ở xa trước khi tới món cạnh người chơi. Bản này dùng callback trigger và condition handler native của gold/potion thay cho việc quét theo cursor mỗi200ms.

- Gold/Potion/Ancient Bones giữ đường tự nhặt native. Collider potion được mở đủ để phát hiện radius5; khoảng cáchXZ cuối cùng vẫn được kiểm tra trước hành động.
- Keys, Materials, Woven Echoes dùng trigger gold lấy từ pool của game, đổi dispatch sang pickupItem(All) → vacuum → storage native. Không ghi thẳng inventory/currency, không dịch chuyển loot và không cấp vật phẩm khi spawn.
- Không còn hạn mức5ứng viên hoặc5món mỗi batch. Khi trigger hợp lệ kích hoạt, batch phê duyệt mọi stackable đã bật trong radius5. Giữ bộ lọc đã hoạt động để không vacuum đồ xa/loại bị tắt; shared ItemData với ground id chưa phê duyệt vẫn bị loại.
- Tối đa1lệnh item pickup mỗi frame, không thêm cooldown khi thành công. Gold/potion/bones dùng lịch callback của game. Giữ thời gian chờ rơi/physics của game; không cam kết đo được latency bằng gold khi chưa test trong game.
- Nếu storage từ chối (ví dụ túi đầy), thử lại sau250ms; lỗi native backoff500ms. Không quét/cast liên tục mỗi frame khi nhặt thất bại.
- Trigger mới được active và không parent trước initialise để native Awake gắn ConditionHandler. Giữ độc lập với item visuals bị loot filter ẩn/tắt; pooling/destroy/unload/zone hooks trả trigger về pool. Additive scene-loaded không xóa trigger đang sống.
- Kiểm tra gameplay inventory, không yêu cầu model/network Ready. Không yêu cầu di chuyển. Toggle từng loại, tutorial exclusion, online ownership guard và Range Pickup giữ hành vi hiện có.
- Không log mỗi lần nhặt; chỉ báo lỗi native một lần khi nội dung lỗi thay đổi.

Kiểm chứng: Release build0 warnings/errors;44 actual-source fixture checks và169 compiled/native audit checks pass (191 patch classes inspected). Fixture có512 trang bị xa trước key gần,20 vật liệu cùng batch, không đợi200ms, init trước inventory ready, visual inactive, lỗi/pooling/cleanup/toggle/radius. Native inspection xác nhận SpawnGroundItem đặt vị trí trước GroundItemVisuals.initialise; callback OnUpdateTick giữ delay/collision và gọi PickUp. Fixture không chạy physics/gameplay thật. Chưa đo FPS hoặc xác nhận tốc độ trong game với DLL mới.

Giữ Combat Assist chạy khi UI mở, chấm người chơi xanh dương lớn hơn50% trên minimap và các bonus trước. Version1.1.0-dev.55/protocol44; đóng game rồi chạy update.bat để nhận cả hai DLL. Game đang chạy nên chưa thay DLL Steam.
