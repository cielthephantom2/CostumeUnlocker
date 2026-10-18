


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



## 0.4.25

Tự Host/Join với vai trò cố định. F4 chọn **This PC hosts** trên máy chủ, **This PC joins** trên máy bạn; máy Join nhập IPv4 của host một lần, cùng TCP port 7777. Bật Auto connect rồi **Save & connect now**. Vai trò/IP/port được lưu riêng từng máy. Cài mới mặc định Join; chưa có IP thì chờ cấu hình. Chủ repo được đặt Host trên máy cá nhân, không nhúng IP hay nhận diện PC vào DLL công khai.

Mở game là tự thử kết nối; nếu thất bại thì thử lại mỗi **60 giây**. Kết nối đã hoạt động không bị ngắt để thử lại. Pause/disconnect tạm dừng, Resume bật lại. Phải giữ CosmeticSharing và Cosmetics bật trong Features.json. Host vẫn cần TCP port forwarding nếu kết nối Internet.

Tối ưu: tái sử dụng preset, roster và bộ nhớ quét nhân vật; không tạo lại danh sách member hay áp lại cosmetic chỉ vì heartbeat. Heartbeat từ 2 lên 8 giây, vẫn tương thích giao thức v3. Cache công tắc feature và tham số khởi động thay vì đọc/đổi tên enum mỗi update. Dấu quái minimap chỉ lấy alignment nhân vật một lần mỗi pass, bỏ kiểm tra lặp cho actor đã đăng ký. Giữ ngân sách/tần suất quét minimap cũ. Sửa trạng thái host giả khi bind port thất bại để có thể tự retry.

108 kiểm tra hồi quy qua, gồm TCP nhiều peer, mất host/reconnect, timer 60 giây, không chồng job, cache roster và port bị chiếm. Chưa có số đo FPS thực hoặc kiểm thử Internet giữa hai PC cho bản này.



## 0.4.26

Bỏ hoàn toàn hệ thống file feature true/false. DLL không còn đọc/tạo CostumeUnlocker.Features.json; file cũ dù tồn tại cũng bị bỏ qua. Tất cả 88 lớp native patch được đăng ký mặc định. Các lựa chọn sử dụng thông thường trong F4, preset cosmetic, IP/port và vai trò Host/Join vẫn được giữ.

Sửa panel bị cắt ngay sau dòng Cosmetic sharing: log xác nhận GUILayout.Toolbar ném NotSupportedException: Method unstripping failed. Thay Toolbar bằng hai nút Host/Join sử dụng GUILayout.Button. Không còn dòng Feature config/Disabled trên panel. Có bảo vệ đóng panel nếu xảy ra lỗi vẽ bất ngờ để tránh spam exception mỗi frame; cuộn ngoài được giải phóng qua finally.

Giữ tự Host/Join, retry 60 giây và các tối ưu 0.4.25. Build không warning/error; 93 kiểm tra qua, gồm kiểm tra metadata DLL không còn FeatureSwitches/FeatureOptions, chuỗi đường dẫn config feature hoặc lời gọi Toolbar. Native startup đăng ký đủ patch. QA GUI riêng bị timeout và chưa xác nhận trực tiếp panel render/click trong game; không tuyên bố đã qua QA giao diện hay online.


