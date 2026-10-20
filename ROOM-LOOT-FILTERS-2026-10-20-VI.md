# Loot filter của room — 20/10/2026

Tính năng dev.9 / protocol 35. Các mục review/source-only bên dưới là lịch sử;
kết quả chốt cặp phát hành nằm ở mục cuối và `RELEASE-1.1.0-dev9-VI.md`.

- Mỗi máy đọc filter đang được chọn qua ItemFilterManager, serialize bản clone; không đọc toàn bộ thư mục filter và không ghi đè filter/binding cá nhân.
- Host nhận filter theo kết nối thật, relay cho mọi thành viên; không phụ thuộc map hay participation. Người vào sau nhận roster hiện tại; rời room gỡ filter đó. Thay đổi filter được kiểm tra mỗi giây.
- Hợp kết quả: giữ kết quả và style của filter cá nhân nếu cho hiện. Nếu cá nhân ẩn, chọn filter remote đầu tiên cho hiện theo thứ tự ID ổn định. Chỉ ẩn khi mọi filter được chọn đều ẩn. Không chọn filter cá nhân thì giữ hành vi native hiện tất cả. Không có filter ở máy remote không tạo quy tắc hiện tất cả cho room.
- Nhãn đồ rơi thêm [Tên người chơi] hoặc [Tên A, Tên B] cho mọi filter cùng cho hiện. Tên được loại control/rich-text delimiters, giới hạn 24 ký tự. Một người chỉ chia sẻ filter hiện đang chọn. Không đổi tên item trong inventory/save. Pooling xóa dấu cũ.
- Đánh giá từng filter riêng bằng native ItemFilter.Match; không nối rule list vì rule Hide sẽ che mất filter khác. Sau review, hook Harmony trên Match đã được thay bằng native detour dùng raw pointers; chi tiết và giới hạn kiểm chứng ở phần cập nhật bên dưới.
- ManualFilterDisable giữ hành vi native và tạm bỏ nhãn attribution. Filter vẫn được chia sẻ, vì phím tạm xem toàn bộ đồ không thay filter đã chọn.
- Filter nhận chỉ tồn tại trong bộ nhớ. XML cấm DTD, giới hạn 262144 ký tự/4096 nodes/depth32, nén tối đa131072 bytes/filter. Dedicated frame dưới giới hạn transport cho room4. F4 có trạng thái sync/lỗi.

## Kiểm chứng trước bản sửa review

- Build Release: 0 warning / 0 error.
- 18 managed checks đạt: XML validation, nén/giải nén, TCP host + 2 client, party-only, late join, thay/clear filter, filter lớn, leave/disconnect. Không chạy game hoặc sửa save.
- Chưa native smoke cho Harmony Match có ref Nullable, clone/serialize/deserialize native, màu/emphasis/sound/beam, label suffix/width/pooling, refresh đồ đã rơi, character-level-dependent rules hoặc hai máy thật. Không đồng nhất managed pass với native gameplay pass.
- Game Last Epoch đang mở khi triển khai; không cài DLL, không đóng game, không native sandbox, không push GitHub.

Lệnh managed riêng: dotnet run --project tests/LastEpochLAN.SelfTest -c Release -- --loot-filter-tests

## Bản sửa theo review — chỉ source, chưa build/test

Theo yêu cầu fix sau review, đã sửa hai vấn đề:

1. Bỏ lớp Harmony `ApplyRoomFilters` nhắm `ItemFilter.Match`. Thêm
   `NativeLootFilterMatch.cs` với callback ABI native dùng con trỏ cho từng out
   value; không chuyển `Nullable<int/bool>` qua managed by-ref object bridge.
   Kiểm size và offset `hasValue/value` bằng metadata IL2CPP trước khi cho phép
   cài hook: Nullable<int> 8 bytes, Nullable<bool> 2 bytes. Layout không khớp thì
   không bật hợp filter remote. Chỉ ghi style/output sau khi có đủ verdict;
   không ghi pointer vào bộ nhớ Nullable. Chỉ áp kết quả remote trên Unity thread.
2. Tách publish local khỏi ingest remote. Deserialize, sanitize và đánh giá rule
   được bắt lỗi theo owner; bỏ riêng filter lỗi, giữ filter hợp lệ của người khác.
   XML lỗi được ghi nhớ theo owner/content, chỉ thử lại khi XML đổi hoặc phiên
   room được reset. Lỗi remote không publish local XML rỗng. Lỗi serialize LOCAL
   vẫn có thể clear phần chia sẻ LOCAL để tránh quảng bá filter của nhân vật cũ.

Đường gọi native gốc dùng bản sao MethodInfo trỏ vào original trampoline và
`il2cpp_runtime_invoke`, giữ cơ chế bắt exception native. Đánh giá filter detached
cũng dùng metadata gốc này để không đi lại qua room hook. Exception khi đánh giá
remote trả về đúng đường xử lý lỗi owner. Nếu native Match gốc lỗi ở caller bình
thường, callback khởi tạo lại các out và cho SHOW để lỗi filter không làm đồ
không thể thấy; `LastError` và trạng thái F4 ghi nhận lỗi. Không đổi drop/inventory.

Hook cài trễ khi có filter remote hợp lệ. Sau lần cài thành công, detour/delegate
và MethodInfo copy được giữ tới khi process kết thúc; ngoài room/không có remote
thì không áp kết quả remote. Không tháo trampoline lúc có native caller đang chạy.
Provider được đối chiếu từ DLL local: Create tự attach; callback chờ hoàn tất gán
original trampoline trước khi gọi gốc. Chưa quan sát đường này trong native game.

**Bản sửa này chưa build, chưa chạy managed/native test và chưa cài DLL**, theo
yêu cầu chưa test khi bố cục chưa hoàn thiện/game đang mở. Các kết quả 0 warning/
18 checks ở phần trên thuộc source trước bản sửa này, không xác nhận bản sửa.
Đợt này chỉ đọc source, metadata DLL local và diff; không khởi động sandbox, sửa
save, push hoặc phát hành. Encounter tiếp tục tạm gác theo yêu cầu người dùng.

Các ca cần chạy sau: nullable có/không giá trị với canary quanh từng buffer;
verdict/style native khi không ở room; union khi local HIDE và remote SHOW;
filter A lỗi/B hợp lệ/local vẫn được publish; XML lỗi được sửa rồi tái nhận;
remote rule lỗi không chặn owner tiếp theo; pooling/refresh/manual-disable;
clone/serialization và hai máy thật. Managed transport pass không đủ để nghiệm
thu callback ABI native mới.

## Kết quả chốt cặp dev.9 sau yêu cầu build/cài/upload

- Build 0 warning/error. 713 managed checks và 18 LootFilter/TCP checks đạt;
  7 compiled audit checks đạt. Smoke native cơ bản đạt.
- Native QA phát hiện phép suy layout từ class của object đã box không đúng:
  wrapper Nullable có thể trả boxed T. Đã sửa để lấy metadata từ kiểu generic
  khai báo, normalize/kiểm offset và dùng chính offset đó khi đọc/ghi buffers.
- Fixture màu/emphasis đặt isEnabled/recolor/emphasized tường minh; không dựa
  vào mặc định của constructor native. Canary bao quanh 8-byte int Nullable và
  2-byte bool Nullable xác minh không bị ghi tràn, cả trước và sau detour.
- 10 quan sát native cuối đạt, gồm union/style, sửa filter giữa phiên, lỗi XML
  một owner không làm mất filter người khác/local publish, XML được sửa tái nhận,
  manual-disable và rời room. Native detour không ghi nhận dispatch exception.
- Full rendered campaign gate **FAIL với 76 log errors**; giữ raw failure riêng
  với `observationsPassed=true`. Không đổi gate thành PASS. Chưa nghiệm thu
  visual labels/hover/pooling hoặc hai máy vật lý.
- Cặp DLL/SHA chốt ở manifest và release report. Không rebuild giữa native QA,
  cài local và upload. Các kết quả trước sửa không thay thế kiểm tra cặp cuối.
