# Bản 1.1.0-dev.21 — Disintegrate rotation và Arcane Ascendance

Đóng game trên Host và mọi Client rồi chạy `update.bat`. Protocol 35; Core giữ nguyên dev.20.

- Bỏ hệ số phạt sát thương âm theo góc xoay trên instance tia Disintegrate của nhân vật local offline. Phạt ailment chance do xoay dùng cùng hệ số nên cũng được bỏ; giữ chance cơ bản, hệ số dương và các chỉ số khác của skill tree.
- Postfix DisintegrateMutator.Mutate sửa component trên instance, không ghi prefab/asset chung. Nhánh twin beam đi qua cùng đường Mutate cũng áp dụng.
- Khi Arcane Ascendance/root khóa di chuyển, xóa đích đi cũ trước native MovementStep và giữ channel. Khi hết root, WASD có thể nhận đích mới.
- Giữ di chuyển WASD và bộ lọc hướng ngắm dev.20, mật độ quái, tối ưu dev.15 và bỏ party HP scaling. Encounter vẫn tạm gác.

## Kiểm chứng và giới hạn

Build candidate Release 0 warning/error; diff check đạt. Feature observations native passed=true: hệ số rotation -0.003 thành 0, buff Ascendance/root native hoạt động, drift 0 khi giữ input trong root, channel giữ nguyên và di chuyển trở lại sau EndAbility. Bốn hướng, hai hướng chéo/release, aim, mana và dừng channel tiếp tục đạt.

Full native gate vẫn failed với 76 runtime errors bootstrap/service/assets/identity/actor-null. Chưa kiểm DPS trên mục tiêu, bàn phím/chuột vật lý, toàn bộ skill tree hay hai máy. Fixture tạo buff Ascendance bằng constructor native vì player cast bị từ chối trên nhân vật QA chưa chọn mastery; chưa nghiệm thu thao tác bật skill trên Sorcerer thật. Hai lượt fixture trước chưa đạt đã được lưu riêng; không dùng để khẳng định thành công.

SHA256 Mod: `71327927E951AA67BFA31B5CB5D56290524879F148A99078303AD35E1E8F35D1`

SHA256 Core: `DC51937DA87B1459DD4F8BF429E4D654C9A3D9F821CF53F1CC5AF4DE38222B9D`
