


## 0.4.23

Fix portrait resource ownership for Social party and on-screen party panels. Leave the native VFX registry under game ownership during scene transitions. Wait for ready player identities/models before attaching character FX and resolve authored joints across the visual hierarchy.

Release build passed with 0 warnings/errors; 69 regression checks passed. Native and online validation remains pending. Graph ID, client/server commit SHA mismatch and kill count errors are not confirmed resolved. Extended pickup from 0.4.22 remains reverted.

## 0.4.24

Thêm F4 slider phạm vi nhặt crafting material 0.5–10, mặc định 1.5, có Reset; server vẫn xác nhận yêu cầu online.

File `<game>/UserData/CostumeUnlocker.Features.json` tự tạo lần đầu, mọi nhóm mặc định true. Sửa rồi khởi động lại game. `Enabled: false` tắt toàn bộ hook/tác vụ, kể cả F4, để lấy log baseline. DLL và MelonLoader vẫn được nạp. Chỉ đổi một nhóm mỗi lượt thử và lưu log riêng. Tắt nhóm là không đăng ký native hook của nhóm đó, không chỉ ẩn UI. Config sai kiểu/khóa lạ/trùng khóa hoặc JSON sai sẽ tắt mọi module và báo lỗi, giữ nguyên file để sửa.

| Khóa | Chức năng |
|---|---|
| Enabled | Công tắc toàn bộ |
| Cosmetics | Appearance UI mô phỏng, preset và bảo vệ cache |
| SkillEffects | Skin skill/cast/projectile và định tuyến VFX |
| CharacterEffects | Slot CharacterFx và gắn FX lên model |
| CosmeticSharing | Host/Join, truyền cosmetic và mô phỏng người cùng room |
| TransitionHandling | Restore/reapply cosmetic khi chuyển map |
| NetworkVisualLifecycle | Sửa thứ tự tạo/hủy và tái sử dụng network VFX |
| PartyPortraits | Vòng đời ảnh party trên màn hình |
| SocialPortraits | Vòng đời ảnh trong Social party |
| WeaponVisuals | Chờ dữ liệu model vũ khí sẵn sàng |
| AbilityBarLifecycle | Chờ danh sách skill của thanh kỹ năng |
| CosmeticUiLifecycle | Xử lý xóa cosmetic khỏi slot UI |
| ResonanceSnapshot | Chuẩn hóa bản tin Resonance |
| MapReveal | Lớp bản đồ chưa khám phá màu xám |
| EnemyMinimapDots | Chấm quái |
| MinimapZoom | Mở rộng nút +/- minimap |
| CameraZoom | Slider zoom camera và phím , / . |
| MaterialPickup | Tự yêu cầu nhặt material gần người |
| VfxDiagnostics | Hook trace/ghi log các tuyến VFX |
| Panel | Panel F4 |

SkillEffects, CharacterEffects, CosmeticSharing, TransitionHandling và CosmeticUiLifecycle cần Cosmetics. NetworkVisualLifecycle/VfxDiagnostics cần Cosmetics + SkillEffects. Các nhóm còn lại độc lập. Khi tắt SkillEffects/CharacterEffects, preset của các slot tương ứng được giữ trên disk nhưng không được mod áp dụng. Hiệu ứng mặc định của game vẫn do game chạy. Bảo vệ cache đi cùng Cosmetics.

Mỗi nhóm có trạng thái hiệu lực ghi trong MelonLoader/Latest.log; nhóm bị tắt cũng liệt kê trên F4. Slider và lựa chọn tiện ích thường vẫn lưu trong CostumeUnlocker.Panel.json.


