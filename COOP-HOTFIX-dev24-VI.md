# Co-op hotfix 1.0.9-dev.24 — protocol 31

## Client không theo Host vào Echo: lỗi faction/Weaver

Log mới chỉ đúng đường lỗi: `GatherNextLevel → EchoWebIsland.HasSecondBossIfBossEcho → IsMemberOfTheWeaver` nhận faction provider rỗng.

Codec network đọc lại EchoWeb nhưng không mang tham chiếu `ActorOnServerOffline` của tiến trình Host. Native `GatherNextLevel` đọc faction thông qua Actor này. Bản này gắn **nhân vật thật cục bộ của Client vào bản EchoWeb tạm** trước khi tạo map; chỉ bắt đầu khi faction provider và nhân vật đã sẵn sàng. Run lưu của nhân vật không bị sửa hoặc thay bằng run Host. Host vẫn chọn Echo, quản lý quái và kết quả hoàn thành.

Fixture Monolith hiện mô phỏng đúng việc codec bỏ tham chiếu Actor và sẽ ném lỗi nếu bước tạo map chưa nhận được faction context. Đã kiểm tra chờ faction, gắn vào web tạm và giữ nguyên web của run gốc. Điều này sửa đường khởi tạo gây exception trong log; chưa xác nhận Client thực sự vào Echo qua hai máy với bản mới.

## Hiển thị minion/companion của nhau

- Mỗi máy đọc minion và sub-minion từ SummonTracker của nhân vật mình, gửi danh sách theo chủ nhân. Host và Client chuyển danh sách theo cả hai chiều; người join sau nhận được danh sách hiện có.
- Dữ liệu gồm prefab visual native, vị trí, hướng, kích thước, trạng thái hiện/ẩn, animation và sự kiện đánh. Nhận diện riêng từng triệu hồi và từng instance/map, xóa model khi desummon, chuyển map hoặc rời phòng.
- Máy nhận dựng model visual với bộ đệm vị trí/animation và cập nhật mỗi khung hình. Không tạo thêm Actor gameplay, AI, collider gây va chạm hay bản sao gây damage/chiếm giới hạn triệu hồi. Đây là đường hiển thị; không công bố đã hoàn thiện mọi hành vi combat/aggro/health của minion từ xa.
- Chụp trạng thái 10Hz; tối đa 64 visual/người. Load asset trước, giới hạn hai model mới mỗi khung hình. Runtime report bổ sung số lượng minion đã chụp/hiển thị, số vượt giới hạn và lỗi capture/render.
- Đã cập nhật Changelog tiếng Việt trong F4. Bộ đệm NPC hiện tại được giữ nguyên.

## Kiểm nghiệm

- Release build: 0 warning, 0 error.
- 521 kiểm tra managed/network: đạt, gồm dữ liệu triệu hồi hai chiều qua TCP thật, late join, deep-copy animation, chống packet cũ, nội suy, teleport, desummon và giới hạn payload.
- 38 kiểm tra adapter Monolith với recording stubs: đạt, gồm lỗi thiếu Actor/faction và cleanup run tạm.
- 13 kiểm tra capture triệu hồi với recording stubs: đạt, gồm tracker/sub-minion, prefab fallback, ID ổn định, attack, pooled/desummon, instance cùng tên map và giới hạn 64.
- Hồi quy adapter animation 16, Omen 21 và cosmetic 11: đạt.
- Native QA mới chưa chạy vì game đang mở (không dừng game hoặc ghi đè DLL đang dùng). **Tải prefab/animation minion trong game và việc Client theo vào Echo thực tế vẫn cần xác nhận hai máy.** Không coi test stubs là xác nhận gameplay native.

## Cập nhật và đi Monolith chung

Protocol tăng lên **31** vì bổ sung dữ liệu triệu hồi vào player packet. Thoát game và cập nhật đủ cặp DLL **dev.24 trên cả hai máy**, mở lại game và tạo room mới:

```
Mods/LastEpochLAN.dll
UserLibs/LastEpochLAN.Core.dll
```

1. Host và Client join cùng phòng, ở Traveler's Rest.
2. Host chọn và mở **Echo thường**. Client theo tự động khi nhận đủ context và nhân vật sẵn sàng; nếu cần, bấm teleport tới Host trong party.
3. Hoàn thành, nhặt đồ rồi về Rest. Host chờ đồng đội trước khi mở Echo tiếp.

Quest Echo/boss timeline, Woven/Tombs và các lượt Arena/Dungeon/Pinnacle vẫn chưa được hỗ trợ đầy đủ. Nếu còn bị chặn, log khởi động cần hiện `dev.24`, protocol 31; xem thêm `monolithParty` và `summons` trong runtime report.
