# Player minimap dot — dev.55

Icon người chơi local trên minimap góc màn hình được thay bằng chấm xanh dương (#1A80FF) có viền tối, dùng chung kiểu sprite16×16 với Enemy/NPC dots. Đường kính hiện tại bằng300% chấm NPC: scale minimap0.90 thay vì0.3, gấp đôi bản chấm xanh trước (0.45). Normalize scale của child anchor để kích thước prefab nhân vật không làm chấm lớn/nhỏ khác nhau.

Chỉ tạo1 DMMapIcon, gắn vào ActorVisuals local; renderer native xử lý vị trí, clipping và zoom như chấm NPC. Không quét scene hoặc tìm NPC mới. Tính năng vẫn hoạt động khi tắt Enemy Map Dots. Tab overlay giữ icon người chơi gốc. Khi mở Tab, đổi nhân vật/scene, mất local actor hoặc thoát mod: gỡ marker và khôi phục icon gốc do tính năng đã tạm ẩn. Chỉ đổi trạng thái component minimap gốc; không disable nhân vật hoặc đổi scale transform của nhân vật.

Native inspection xác nhận DMMapIcon.OnDisable gọi MinimapIconManager.Remove, OnEnable gọi Add; marker mới dùng cùng cách ApplyScaling/normalization với EnemyMapDots đang có. Build Release0 warnings/errors,165 compiled/native audit checks pass. Chưa xác nhận hiển thị trong game với DLL mới; game đang chạy nên chưa thay DLL trong Steam.

Giữ version1.1.0-dev.55 / protocol44. Bao gồm Combat Assist tiếp tục khi UI mở và Auto Pickup5Hz/5 ứng viên/1 lệnh, radius5, Woven Echoes, không log pickup. Cập nhật cả hai DLL bằng update.bat sau khi đóng game.

Cập nhật sau: Auto Pickup đã chuyển sang trigger native, thay thế giới hạn5Hz/5ứng viên. Xem [báo cáo hiện tại](NATIVE-PICKUP-dev55-VI.md).

Cập nhật kích thước: tăng gấp đôi chấm người chơi theo yêu cầu; cùng màu xanh dương và sprite. Build0 warnings/errors và169 compiled/native audit checks pass; chưa xác nhận hiển thị mới trong game.
