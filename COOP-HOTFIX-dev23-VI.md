# Co-op hotfix 1.0.9-dev.23 — protocol 30

## Crash khi bấm Host/Join

Log được gửi ghi bản đang chạy là **dev.21**. Crash là `AccessViolationException` ở `NativeCosmeticReplica.Capture → Dictionary.TryGetValue`, được gọi từ `PublishPlayer`. Đây là lỗi truy cập bộ nhớ native, không phải thông báo protocol mismatch và không thể xử lý bằng catch thông thường.

Bản này bỏ lời gọi generic `TryGetValue(..., out ...)` trong đường đọc footprint/aura, chuyển sang `ContainsKey` và indexer trên Unity thread. Bổ sung kiểm tra service/pool/visual chưa sẵn sàng. Đường đọc dictionary giáp ngoại hình cũng chuyển tương tự. Vẫn thu thập cosmetic đã trang bị.

Đã thêm smoke test ABI bằng dictionary native biệt lập cho lần chạy QA khi game đóng. Test native mới này **chưa chạy**, nên việc crash đã hết trên máy Client vẫn cần xác nhận.

## Omen Windows không rơi đồ Corrupted ở Client

Thưởng kết thúc Omen đi qua `ObserverWindowLogic.DropItemRewards`, tách khỏi loot chết thông thường của boss. Đường co-op trước đó chưa chuyển lời gọi thưởng này sang Client.

- Sau khi lời gọi thưởng native của Host trả về thành công, lưu một descriptor gồm loại Omen, tier, hai vị trí rơi đồ và danh sách nhân vật đủ điều kiện tại thời điểm đó. Descriptor nằm trong world snapshot/delta của Host, giữ tới khi đổi instance/map.
- Client chờ nhân vật đã spawn và catalog Omen sẵn sàng, rồi gọi chính phương thức thưởng native trên một carrier luôn bất hoạt, được hủy sau lời gọi. Không khởi tạo encounter/spawner thứ hai. Thuật toán roll Corrupted của game và dữ liệu nhân vật Client vẫn được sử dụng.
- Có receipt ghi trước lời gọi để chống nhận lại khi gói tin lặp, reconnect hoặc bộ nhớ dedup bị reset. Giao dịch lỗi một phần được giữ lại, không tự cấp lần hai. Nhân vật mới đến sau thời điểm thưởng hoặc không nằm trong danh sách không nhận thưởng hồi tố.
- Không sao chép pool Champion loot đã tích của Host; đường thưởng quái chết của Client vẫn xử lý độc lập. Phạm vi sửa là thưởng item/Corrupted qua `DropItemRewards`; không công bố đã hoàn tất mọi dạng Omen reward, Greater choices, altar, chain hay Woven Echo.
- Báo cáo runtime có thêm `omenError` và `omenItemGrants`; Changelog tiếng Việt trong F4 được bổ sung.

## Kiểm nghiệm

- Release build: 0 warning, 0 error.
- 499 kiểm tra managed/network hồi quy: đạt.
- 11 kiểm tra capture cosmetic với recording stubs: đạt, gồm service chưa khởi tạo, thiếu pool, visual bị giải phóng và giữ đúng asset footprint/aura.
- 21 kiểm tra adapter Omen với recording stubs và TCP loopback thật: đạt, gồm đúng type/tier/vị trí, chờ asset/spawn, chống trùng bền vững, sai map/nhân vật, lỗi một phần, giới hạn dữ liệu và chế độ online.
- Đã đối chiếu disassembly native local của Omen để xác định đường sinh Corrupted và các field cần thiết cho carrier.
- Chưa chạy QA native mới do game đang mở; không dừng game hay ghi đè DLL đang chạy. **Chưa xác nhận hết crash Host/Join hoặc loot Corrupted thực tế qua hai máy.**

## Cập nhật

Thoát game trên cả hai máy, cập nhật **đủ cặp DLL dev.23** rồi mở lại và tạo room mới:

```
Mods/LastEpochLAN.dll
UserLibs/LastEpochLAN.Core.dll
```

Protocol giữ ở 30; cả hai máy nên dùng dev.23 để có cùng đường thưởng mới. Bản này bao gồm sửa projectile, Nemesis và codec Echo của dev.22. Log khởi động sau cập nhật phải hiện `v1.0.9-dev.23`.
