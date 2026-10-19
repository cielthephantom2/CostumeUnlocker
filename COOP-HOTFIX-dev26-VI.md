# Co-op hotfix 1.0.9-dev.26 — protocol 31

## NullReference khi dọn hiệu ứng skill từ xa

Log chỉ đúng đường lỗi `NativeSkillVisualReplica.Apply → GameObject.SetActive → LanMod.UpdateMarkers`. Hai đường dọn hiệu ứng (gói kết thúc và hiệu ứng không còn trong snapshot) trước đây gọi `SetActive(false)` mà không kiểm tra đối tượng Unity còn sống. Khi scene unload hoặc native visual bị hủy, wrapper C# có thể vẫn còn trong dictionary; gọi qua wrapper đó gây lỗi và ngắt lượt cập nhật party.

- Kiểm tra trạng thái native Unity của root và prefab child; bỏ tracking đối tượng đã chết trước khi dọn. Gói kết thúc lặp không dựng lại skill đã kết thúc.
- Dọn root còn sống bằng một đường chung cho kết thúc, biến mất khỏi snapshot, lỗi render và Dispose. Lỗi deactivation/destroy được ghi vào diagnostics riêng của visual; không giữ entry hỏng để lượt sau tiếp tục gây lỗi.
- Nếu snapshot vẫn báo skill đang hoạt động nhưng visual đã mất, bỏ bản hỏng và chờ ít nhất một giây trước khi thử dựng lại từ snapshot mới. Gói kết thúc hoặc mất khỏi danh sách sẽ hủy retry.
- Lỗi cập nhật một skill được cách ly, tiếp tục xử lý skill khác trong cùng packet. Bỏ qua optional beam/particle/stretcher đã bị hủy.
- Giữ render-frame smoothing và clock của snapshot trùng. Hiệu ứng NPC khỏe vẫn giữ thời gian hiển thị tối thiểu; không giữ wrapper đã chết chỉ vì chưa hết lifetime.

Tách file capture, vòng đời replica và tạo/render asset để fixture gọi trực tiếp các hàm Apply/Tick/Dispose của production. Không thay protocol hay cơ chế gameplay/loot. Sửa Echo của dev.25 và updater tránh REST API rate limit vẫn được giữ.

## Kiểm nghiệm

- Release build: 0 warning, 0 error.
- 521 kiểm tra managed/network: đạt.
- 22 kiểm tra vòng đời VFX với recording Unity API: đạt. Gồm wrapper còn sau native destruction, terminal/absent snapshot, render tick, mất model con, retry, lỗi SetActive/transform/pose, phục hồi, packet trùng, optional renderer, Dispose lặp và minimum lifetime của NPC.
- 46 kiểm tra adapter Monolith và 16 kiểm tra adapter animation: đạt.
- Bổ sung tình huống DestroyImmediate root rồi nhận terminal packet vào native motion smoke. Native QA chưa chạy được do game đang mở (PID3128); không coi fixture là xác nhận render/Unity/IL2CPP hoặc co-op hai máy thật. Không dừng game hoặc ghi đè DLL đang dùng.

## Cập nhật và kiểm tra trong game

Thoát game, chạy update.bat mới để cập nhật đủ hai DLL dev.26 trên cả Host và Client, mở lại và tạo phòng mới. Protocol giữ nguyên 31.

Thử dùng nhiều projectile/channel/beam, đợi skill kết thúc, chuyển map rồi vào Echo cùng nhau. Kiểm tra không còn Adapter error tại SetActive, hiệu ứng từ xa vẫn di chuyển và cập nhật party không bị ngắt. Nếu lỗi khác xuất hiện, gửi log đầy đủ cùng phiên bản đang chạy. Chưa công bố đã xác nhận mọi prefab skill thực tế.
