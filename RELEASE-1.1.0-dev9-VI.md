# Bản 1.1.0-dev.9 — Loot Filter trong room

Protocol **35**. Cả Host và Client cần cùng bản. Đóng game trước khi chạy
`update.bat`; updater xác minh cả cặp DLL và giữ backup.

- Mỗi người chia sẻ filter đang chọn, chỉ trong bộ nhớ; không ghi đè file filter
  hoặc binding nhân vật của người khác. Không cần cùng map hay Teleport.
- Sửa/chọn/tắt filter được phát hiện mỗi giây, chuyển qua Host tới các thành
  viên. Người vào sau nhận roster hiện tại; người rời room được gỡ khỏi roster.
- Giữ kết quả/style local nếu local cho hiện. Nếu local ẩn, filter remote có thể
  cho hiện; thứ tự chọn style remote theo owner ID ổn định. Không có filter local
  thì giữ hành vi native hiện tất cả. Remote không chọn filter không có nghĩa là
  tự cho hiện tất cả cho nhóm.
- Nhãn đồ rơi bổ sung tên những người có filter cho hiện món đó. Khi nhận filter
  mới, mod yêu cầu native refresh nhãn hiện có. Chưa nghiệm thu đầy đủ layout,
  width, pooling, hover hoặc hình ảnh trên hai máy vật lý.
- Bỏ Harmony hook `ItemFilter.Match` có by-ref Nullable không phù hợp bridge.
  Native detour dùng raw output pointers, đọc size/offset từ metadata kiểu generic
  Nullable thật; không dùng class của giá trị đã box để suy layout. Chỉ ghi đủ
  byte value-type, không ghi object pointer vào output native.
- Đánh giá filter detached qua original native metadata/trampoline và
  runtime_invoke; lỗi một filter không chặn filter khác. XML lỗi bị cô lập theo
  owner/content; sửa XML thì thử lại. Lỗi remote không clear publish local.
- Giữ Shared Stash, custody/save ACK, party-only Join và các bonus offline.
  Encounter mới vẫn chưa nối native gameplay; công việc Encounter đang tạm gác.

## Kiểm chứng

- Release build: 0 warning, 0 error.
- 713 managed checks đạt và 18 kiểm tra LootFilter/XML/nén/TCP riêng đạt.
- Audit compiled binary: 7 kiểm tra đạt; không đưa lại encounter replay cũ.
- Smoke native cơ bản đạt trên đúng cặp DLL phát hành.
- 10 quan sát LootFilter native đạt: guard bytes quanh Nullable buffers, giữ
  verdict/style khi ngoài room, local HIDE + remote SHOW, manual-disable, sửa
  filter SHOW -> HIDE giữa phiên, cô lập XML lỗi và nhận lại XML đã sửa, rời room.
- **Full rendered campaign gate vẫn FAIL: 76 log errors**, gồm khởi tạo service
  lặp và các lỗi bootstrap sandbox. Giữ `passed=false`, `observationsPassed=true`.
  Chi tiết phạm vi và hash tại `validation/native-loot-filter-dev9.json` và
  `validation/native-basic-dev9.json`; không lọc lỗi để gọi full gate đạt.
- Updater fixture: 15 kiểm tra đạt trước push; kiểm tra tải thật sau push được
  ghi riêng trong `validation/public-updater-dev9.json`.

Các kết quả Shared Stash/Woven chi tiết của dev.8 thuộc cặp DLL dev.8; không coi
đó là kiểm thử lại đầy đủ mọi tính năng trong dev.9. Chưa nghiệm thu hai máy vật
lý, visual label đầy đủ hoặc toàn bộ rule phụ thuộc level/class khác nhau.

Không phát hành runtime/game assets, save/log thật, UserData hoặc local research.
Binary pair phải được chốt sau kiểm tra, cài local và phát hành đúng cặp đó;
không rebuild giữa cài và upload. Xem `ROOM-LOOT-FILTERS-2026-10-20-VI.md` để biết
chi tiết bản sửa và phạm vi probe native.

| File | Bytes | SHA-256 |
|---|---:|---|
| Mods/LastEpochLAN.dll | 867840 | 5C093CF3D6F8829598E1265B385D39A90E0C56AED90194B278F2E6D9424B85F6 |
| UserLibs/LastEpochLAN.Core.dll | 346624 | 4C9C7B185A54556279B51E0516F3B00D7D67843BE84BF8499A23E2B89378B3E4 |
