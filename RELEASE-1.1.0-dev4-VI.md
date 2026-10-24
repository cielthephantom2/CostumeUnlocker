# Bản 1.1.0-dev.4 — Party riêng và hòm chung F2

Phát hành lên main theo yêu cầu ngày 20/10/2026. Protocol 33: Host và Client cùng cập nhật bằng update.bat. Đây là đúng cặp DLL đã cài local trước đó, không rebuild hoặc sửa thêm gameplay.

## Cách dùng

- Host/Join chỉ tạo party. Client giữ nguyên map, NPC và tiến trình solo. Chưa bấm Teleport thì không nhận quyền điều khiển world/NPC của Host, không bị kéo vào Echo và không làm tăng group scaling của Host.
- Bấm Teleport ở danh sách party F4, hoặc portal trên native party UI, để tải map/nhập run của Host và đặt nhân vật gần Host. Echo kế tiếp cần yêu cầu Teleport mới. Dọn context borrowed Echo và trở về Rest vẫn chạy khi run cũ kết thúc/đứt kết nối.
- F2 mở/đóng **hòm riêng**, không dùng stash cá nhân. Người trong cùng party/chế độ nhân vật dùng được khi khác map. Hòm lưu trên máy Host theo danh tính Host và cycle/mode; không cần Host đang mở bảng.
- Chọn trang bị/Idol trong cột inventory, bấm Gửi; hoặc kéo từ danh sách sang hòm. Bấm món trong hòm để Lấy, hoặc kéo sang cột inventory. Có nhận từ native cursor khi thả vào vùng hòm nếu món đủ điều kiện.
- Hiện hỗ trợ trang bị/Idol hợp lệ cho Gift, mỗi món x1, có native individual ID; không nhận vật phẩm bị native cấm Gift, nguyên liệu stack hoặc nhân vật Solo Challenge. Hòm có 96 vị trí logic, chưa có tab mua/mở rộng.
- Giao diện dùng artwork/icon native với projection riêng; không clone controller, event handler hay dữ liệu save của stash cá nhân. Đây là bảng riêng gồm inventory và lưới hòm, không phải bản sao đầy đủ mọi tính năng native Stash.

## Cơ chế giữ đồ

1. Gửi: ghi intent local → khóa và lấy món khỏi inventory → chờ native save thành công → gửi cùng mã giao dịch tới Host → Host ghi file durable → client ghi receipt hoàn tất.
2. Lấy: Host ghi reservation và bỏ món khỏi danh sách chung → chỉ người giữ reservation nhận payload → client ghi intent trước khi thêm native inventory → lưu nhân vật → ACK → Host ghi hoàn tất → client mở khóa món.
3. Timeout/disconnect không tự mở reservation hay cấp/hoàn lại món. Reconnect đúng Host/nhân vật tiếp tục cùng mã. Sau crash, so count native individual ID trong nhân vật đã tải với baseline; chỉ tiếp tục khi chênh lệch đúng, trường hợp khác giữ `Held`.
4. Mã giao dịch gắn chủ sở hữu và payload, tombstone không bị mở lại. Native ID ngăn cùng món mang dữ liệu đã sửa lọt vào hai vị trí. Lượt gửi lại món từng lấy cần custody ticket từ receipt lấy gần nhất: packet cũ đổi mã giao dịch cũng không thể tái tạo món.
5. Host ghi file tạm, flush và rename trước khi ACK; khóa file và đọc lại ledger bảo vệ nhiều instance. Lỗi ghi/corrupt file giữ giao dịch, không tự tạo hòm trống hoặc tự refund. Receipt/intent client cũng ghi durable trước khi đổi custody.
6. Inventory đầy giữ món trong reservation trên Host để thử lại. Hòm đầy giữ món đã gửi trong escrow client, không bỏ payload; vẫn cho phép thao tác khác để giải phóng chỗ khi không có native save đang chạy. Không đổi map bằng Teleport trong lúc còn giao dịch chờ.

Dữ liệu: `Steam game/UserData/LastEpochLAN/SharedChest/Host` và `Client`. Không xóa/đổi các thư mục này để “reset” lỗi khi còn giao dịch pending; phần đó có thể đang giữ quyền sở hữu món. Trường hợp `Held` cần kiểm tra nhật ký, không tự cấp lại.

## Kiểm nghiệm và giới hạn

- Build Release sạch, 696 kiểm tra managed đạt, gồm TCP thật khác map và 150 kịch bản disconnect/restart/crash/replay; hai người tranh một món, ACK mất, packet cũ, payload đổi, sai người/sai Host, inventory/Host đầy, corrupt file và lỗi ghi đĩa.
- 7 compiled audit checks đạt: không đưa lại encounter replay cũ. Native smoke cơ bản được ghi riêng trong validation.
- Luồng native sandbox cuối quan sát đạt: remove/save → Host custody → add/save, cuối cùng đúng một bản và giữ serialized bytes; query hòm qua loopback khác map; native Monolith controller không chuyển map khi chỉ Join, dù đã nhận Echo context.
- Log full native campaign giữ nguyên trạng thái FAIL vì 5 lần báo lỗi bootstrap sandbox (Actor null, hai lỗi PlayFab chưa login, UserIdentity và EncounterPlacementData). Người dùng xác nhận đây là baseline bình thường trong môi trường QA; phân loại riêng với lỗi tính năng, không sửa hoặc xóa log gốc. Các quan sát gửi/lấy riêng đạt.
- Kiểm tra giao diện có cửa sổ riêng không hoàn tất vì sandbox startup/CharacterSelect transition; chưa nghiệm thu ảnh F2, thao tác kéo/thả thật, save-reload sau crash thực, hoặc hai inventory native trên hai máy vật lý. Các mô phỏng không phải cam kết không thể dupe trong mọi điều kiện, kể cả sửa file/save ngoài mod.
- Không sửa toàn bộ hệ thống encounter trong đợt này; encounter co-op mới vẫn chưa tích hợp.

Gói cập nhật gồm Mods/LastEpochLAN.dll và UserLibs/LastEpochLAN.Core.dll, ghim SHA-256/size trong latest.json. Đóng game trên cả hai máy rồi chạy update.bat; updater sao lưu cặp cũ trước khi thay. Không khởi chạy game hoặc sửa save thật trong kiểm thử phát hành.

Kiểm tra updater sau phát hành: 17 bài fixture/live đạt, gồm tải thật cả hai DLL từ main và kiểm SHA-256. Không khởi chạy game thật.
