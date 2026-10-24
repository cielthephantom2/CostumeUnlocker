# TM Co-op 1.1.0-dev.29 — Internet room chỉ F2 + Loot Filter

Protocol **40**. Cả Host và mọi Client cần cập nhật cùng bản; dev27/protocol36 và nhánh Encounter dev28/protocol39 không kết nối chung. Kết nối **public IPv4 + port qua Internet** giữ nguyên, không bị giới hạn thành LAN.

## Giữ lại

- Host/Join, mã phòng và thành viên đã xác thực.
- F2 Shared Stash lưu trên Host, lấy/cất đồ khi khác map, native inventory readiness, cycle/mode checks, journal/escrow/custody/save/ACK chống trùng giao dịch. Giữ hook khóa đồ và projection của F2.
- Loot Filter chia sẻ trong phòng, cập nhật thay đổi/chọn/tắt filter, native Match và nhãn nguồn filter. Không ghi đè filter riêng của người khác.
- F1 Combat Assist, potion, DPS Tracker, toàn bộ tiện ích cá nhân trong Mod Settings. Giữ mật độ quái và bonus offline hiện có. Disintegrate/Arcane Ascendance tiếp tục dùng hành vi gốc.

## Tạm tắt để giảm tải

- Capture/publish/apply quái, combat, HP/status, đồng bộ thưởng; model/animation/cosmetic/pet/VFX của đồng đội.
- Teleport, map travel chung, Monolith chung, party HUD/native party replica và Encounter.
- Không cài 67 lớp hook không dùng; chỉ cài 47 lớp hook F2/filter/tiện ích cá nhân. Melon tự PatchAll bị tắt và installer dùng allowlist. Thiết lập co-op cũ trong JSON không bật lại các hệ thống này.
- OnUpdate đi theo nhánh inventory-only rồi return trước cập nhật thế giới. OnLateUpdate return trước toàn bộ trình diễn co-op. Presence chỉ gửi readiness và identity/group của inventory mỗi 250 ms; không đọc avatar, VFX, summon, đồ rơi, tọa độ hay HP để đồng bộ.

Mỗi máy tự chơi map/quái/Encounter và nhận thưởng native của mình. Không tuyên bố đây là gameplay đi chung hoàn chỉnh.

## Sửa hiệu năng giữ từ lượt rà soát

- Assist bỏ quét quái khi pause/menu/chat/focus hoặc chỉ potion/self-buff không cần quái; giữ nhịp cast 25 ms và Hold 0s. Self-buff có điều kiện Enemy/Rare/Boss vẫn quét đúng.
- DPS giảm lookup component/predicate allocation dư; giữ nested damage accounting và ghi nhận hit/DoT.
- Runtime report thêm profiler từng hệ thống theo 600 rendered frames hoàn tất, gồm hook DPS và GUI nằm ngoài Update. Vùng lồng nhau là inclusive, không cộng chúng thành tổng. Không đo toàn bộ native/GPU workload.
- Không cam kết khôi phục 70–80 FPS: cần profile native cùng map/mật độ/cài đặt sau khi cập nhật.

## Kiểm chứng

- Release build: **0 warnings / 0 errors**.
- **806 managed/TCP checks PASS**: gồm inventory-only presence, F2 request không cần map/co-op readiness, filter qua socket đã xác thực, public IPv4 policy, custody/replay/save protocols và Assist/profiler regression.
- **20 compiled audit checks PASS**: Melon PatchAll opt-out, policy false không có config override, hook allowlist, control-flow OnUpdate/OnLateUpdate, presence không gọi capture nặng, không còn Disintegrate/Arcane overrides. 114 lớp patch compiled, 47 chọn và 67 bỏ.
- **15 updater fixture checks / 17 live GitHub updater checks PASS**: tải lại đúng SHA256 của cả hai DLL từ commit đã push vào thư mục giả lập không chạy game. Bằng chứng tại `validation/release-dev29-*`.
- Chưa native-test phiên bản này trong game hoặc nghiệm thu hai máy Internet; không tự đóng game, không khởi chạy engine QA thứ hai, không cài vào Steam trong lượt build/upload này.

## Cập nhật

Thoát game khi thuận tiện, chạy `update.bat` ở thư mục game trên **cả Host và Client**. Updater xác minh SHA256 của cả `Mods/LastEpochLAN.dll` và `UserLibs/LastEpochLAN.Core.dll`, giữ backup và từ chối thay file khi game chạy. Dùng Play Offline rồi F4 Host Internet / Join IP; F2 mở hòm chung.

Checkpoint Encounter được giữ riêng, không đóng gói vào release này. Xem `PERFORMANCE-REVIEW-2026-10-21-VI.md` cho số đo lịch sử dev27; đó không phải benchmark FPS dev29.
