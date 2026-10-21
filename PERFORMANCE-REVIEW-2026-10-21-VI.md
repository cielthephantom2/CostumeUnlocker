# Rà soát hiệu năng co-op — 21/10/2026

Encounter đã tạm lưu riêng tại `D:\New folder\LastEpochLAN-EncounterRewrite\ENCOUNTER-PAUSED-2026-10-21-VI.md`. Các sửa dưới đây chỉ nằm trong source public dev27/protocol36, không ghép Encounter dev28/protocol39. Chưa cài Steam, chưa commit/push/release.

## Bằng chứng bản đang cài

- Người dùng báo Host + 1 Client, DPS Tracker + Combat Assist + Mod Settings: mod 30–45 FPS, không MelonLoader 70–80 FPS. Tương ứng khoảng 22–33 ms so với 12,5–14,3 ms/frame; chưa có capture A/B cùng cảnh để quy chi phí từng hệ thống.
- Steam chỉ nạp một mod LastEpochLAN dev27. SHA256 DLL `C65876D76009AF58CFFFF88593C313D1216B6C15EE38A6103C706CFA688911D6`, đúng bản phát hành. Latest.log không có exception lặp/spam theo frame; có cảnh báo travel/stash rời rạc.
- Runtime report cuối lúc 13:40:10 giờ Bangkok: Update 600 mẫu, trung bình **5,8576 ms**, p95 **16,3564 ms**, p99 **22,099 ms**, max **28,9042 ms**. Managed allocation trung bình **1.072.289 bytes/callback** (~1,02 MiB), max 2.552.480 bytes. Đây là chi phí đáng kể và áp lực GC; bộ đếm GC tích lũy không cho biết tốc độ GC hiện tại.
- LateUpdate của mod trung bình 0,00998 ms, không có managed allocation trong mẫu. Không bao gồm toàn bộ native game/GPU work phát sinh từ icon, vật thể hay renderer do mod tạo.
- Actor read Host trung bình tích lũy 1,4648 ms/lần capture, max 72,276 ms. World capture chạy khoảng 10 Hz; không được coi số này là ms mỗi rendered frame.
- Report chụp tại **M_Rest**, DPS setting false, Assist Armed nhưng Paused-panel/chat/focus, Client Cooperative=false tại thời điểm đó. Những trạng thái này không phủ nhận trải nghiệm đi map trước đó, nhưng report không phải profile đi map cùng 1 Client đang combat. FPS 72,61 trong report là trung bình dài hạn, không chứng minh mức FPS lúc giao chiến.
- Bằng chứng đã lọc thông tin nhân vật/mã phòng/IP: `validation/performance-installed-dev27-2026-10-21.json`.

## Các đường tốn tài nguyên và mức chắc chắn

1. **Combat Assist:** quét registry quái mỗi 150 ms và đọc điều kiện từng quái mỗi 50 ms, kể cả trước khi xét pause menu/chat. Vẫn quét khi chỉ tự uống bình hoặc buff bản thân không có điều kiện quái. Mỗi quái còn đọc lại scene của actor local. Đây là công việc dư xác nhận từ code; chưa đo riêng số ms trên native map.
2. **DPS:** hook hit/DoT chạy trong đường combat native, ngoài thời gian Melon OnUpdate. Trước đây mỗi hit lấy UnitHealth trước khi loại nguồn không thuộc người chơi; kiểm tra nested scope bằng captured LINQ predicate tạo allocation. DoT duyệt danh sách ailment và truy nguyên creator; không được giảm tần suất tùy tiện vì có thể sai damage.
3. **VFX/Host world:** đọc renderer/pose/particle metadata, validation và deep copy snapshot khi publish. Có nhiều native interop và allocation. Bộ đếm `nativeParticleValuesCaptured=123792394` là tổng **particleCount metadata được tính vào descriptor**, không phải 123 triệu particle positions đọc bằng GetParticles, cũng không phải số mỗi frame. Không thể dùng bộ đếm này để kết luận VFX chiếm toàn bộ FPS giảm. Thêm profiler riêng cả Read được gọi từ hook tạo/kết thúc VFX và CaptureFor.
4. **Enemy dots:** code đã có budget 12 registry visits + 24 status checks, tối đa 2 icon creations/frame. Tuy nhiên mỗi quái vẫn có native DMMapIcon riêng; native minimap/canvas tiếp tục cập nhật các icon đó ngoài Tick của mod. Reveal map còn thêm một lớp RawImage/material. Cần kiểm tra native CPU/GPU hoặc A/B bật/tắt riêng để xác định tải; không tự tắt tính năng.
5. **Mật độ:** giữ nguyên ngoài Monolith ×5, Monolith theo Corruption như người dùng đã yêu cầu. Bản không mod có thể có ít quái hơn, nên so FPS hai bản không chỉ đo chi phí MelonLoader: AI, VFX, loot và UI native cũng khác. Không quy toàn bộ chênh lệch cho loader.

## Sửa và bổ sung đã chuẩn bị

- Assist bỏ quét quái khi panel/chat/focus đang pause và F1 đóng; potion-only hoặc self-buff không cần quái cũng bỏ quét. Khi chạy lại, scan ngay; các điều kiện Enemy/Rare/Boss vẫn được đọc. Giữ nhịp đọc/cast 25 ms và Hold 0s. Cache scene local một lần trong lượt duyệt.
- DPS loại nguồn hit không thuộc local trước bước lấy UnitHealth khi không có nested scope; vẫn giữ scope ngoại lai cần để trừ nested damage. Dùng foreach thay captured predicate; kiểm tra HUD hidden trước native readiness trong input hit-test.
- Thêm `featurePerformance` vào runtime report: Toolkit, AutoAssist, AutoTargets, PlayerPublish, WorldSync, RemotePresentation, VisualCapture, VisualRead, DpsCapture, DpsGui, MapFog, EnemyDots, Objectives, Gui, ReportCapture.
- Profiler cộng tất cả lần gọi/hit/OnGUI trong cùng rendered frame, gồm frame không có công việc, dùng cửa sổ 600 frame hoàn tất. Có avg/p95/p99/max và managed bytes. Không cấp phát object mỗi sample. Các vùng lồng nhau là inclusive, không cộng tổng các vùng. Chỉ đo main-thread managed/native calls được bọc; không đo GPU, network worker, toàn bộ vanilla native hoặc GC pause bên ngoài vùng.
- Không đổi mật độ, damage/HP quái, loot, native AI, Disintegrate/Arcane, protocol hoặc Encounter runtime.

## Kiểm tra và giới hạn

- Release build: **0 warnings, 0 errors**.
- Full managed/TCP suite: **781 PASS**, gồm 9 kiểm tra mới cho scan policy và profiler frame/burst/allocation. Không mở game hay chạy QA engine thứ hai.
- Compiled patch audit: **11 PASS**, proof `validation/patch-audit-performance-2026-10-21.json`; Disintegrate/Arcane tiếp tục vắng mặt, Encounter native mutations tiếp tục tắt.
- `git diff --check` đạt. DLL candidate ở `src/LastEpochLAN.Mod/bin/Release/net6.0/LastEpochLAN.dll`, Core đi kèm ở thư mục build Core. Đây là build sửa local chưa phát hành, vẫn dùng metadata dev27; phân biệt bằng SHA trong audit, không dùng metadata version để giả định giống DLL Steam.
- **Chưa xác nhận tăng FPS native.** Source sửa có hiệu lực sau khi được cài và game nạp lại. Không thay DLL Steam hoặc tác động phiên chơi hiện tại trong lượt này.

## Cách đọc số liệu lần đi map tiếp theo

Sau khi cài candidate, để Host + 1 Client cùng combat một map, cùng zoom/mật độ/cài đặt, lấy report khi cả hai đang trong map (không report ở M_Rest). So p95/p99 + bytes của từng vùng, frame time thực và số quái/icon; DPS capture nằm riêng ngoài Update. Một lượt bật/tắt riêng DPS, Assist, enemy dots, reveal map ở cùng điều kiện sẽ phân biệt phần tải. Không tắt MelonLoader hoặc đóng game đang chơi để ép làm A/B. Chỉ kết luận mức FPS khôi phục sau khi có số đo native tương ứng.
