# Co-op hotfix 1.0.9-dev.25 — protocol 31

## Client vào Echo rồi bị đưa ra ngoài

Đã sửa đường lỗi trong ảnh: `ReplaceObjectiveDescriptionsInternal → QuestTrackerUI → GetFormattedMonolithMessage`.

Client trước đây dựng mục tiêu từ lớp chung `DynamicObjectiveDescription`. Native formatter kiểm tra kiểu đối tượng và ép sang `MonolithObjectiveDescription`; đối tượng lớp chung khiến kết quả ép kiểu là null dù các thông số mục tiêu đúng. Bản này dùng constructor native `MonolithObjectiveDescription`, giữ nguyên loại mục tiêu, message, data, count, category và trạng thái hoàn thành từ Host. Đã đối chiếu đường ép kiểu với mã native, không chỉ suy đoán từ log.

Lỗi bảng mục tiêu trước đây truyền vào bộ xử lý lỗi travel và khiến Client rời/unload Echo vừa tải. Bản này tách cập nhật bảng mục tiêu khỏi cập nhật gameplay: lỗi riêng ở bảng mục tiêu không kích hoạt thoát map; tự thử hiển thị lại tối đa một lần mỗi giây, kể cả khi Host chưa gửi revision mới. Chỉ ghi warning khi nội dung lỗi thay đổi. Hoàn thành Echo và phần thưởng không chạy lại khi bảng phục hồi; lỗi travel/gameplay thật vẫn đi qua cơ chế xử lý cũ.

Runtime report thêm `monolithParty.ObjectiveError` để phân biệt lỗi hiển thị mục tiêu với lỗi tải/chuyển map. Trạng thái retry được xóa khi rời run tạm. Các sửa faction/Weaver và hiển thị minion của dev.24 vẫn được giữ.

## Kiểm nghiệm

- Release build: 0 warning, 0 error.
- 521 kiểm tra managed/network: đạt.
- 46 kiểm tra adapter Monolith với recording stubs qua TCP thật: đạt. Có kiểm tra kiểu lớp mục tiêu, lỗi HUD ngay khi spawn, giữ Client trong Echo, giới hạn retry, phục hồi không cần revision mới, không lặp completion/reward, chuyển Echo và khôi phục run gốc khi disconnect.
- Các stubs không thay thế Unity/IL2CPP thật. Native QA đã từ chối chạy vì game đang mở; chưa xác nhận đi Echo hai máy với dev.25. Không dừng game hay ghi đè DLL đang dùng.

## Cập nhật

Thoát game, cập nhật đủ hai DLL dev.25 trên cả Host và Client, mở lại rồi tạo phòng mới:

```
Mods/LastEpochLAN.dll
UserLibs/LastEpochLAN.Core.dll
```

Protocol giữ nguyên 31. Từ Traveler's Rest, Host mở Echo thường và Client theo vào; kiểm tra Client ở lại map, bảng mục tiêu cập nhật, hoàn thành và về Rest. Quest Echo/boss timeline, Woven/Tombs và các hoạt động ngoài phạm vi Echo thường vẫn chưa được hỗ trợ đầy đủ.
