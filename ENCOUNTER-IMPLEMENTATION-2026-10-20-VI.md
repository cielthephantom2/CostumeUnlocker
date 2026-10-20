# Triển khai Encounter co-op — đợt source 1, 20/10/2026

## Phạm vi và điều kiện làm việc

Theo `ENCOUNTER-COOP-REQUIREMENTS-VI.md`: Nemesis có lựa chọn/lượt/item riêng;
Rift Beast chung map/boss/NPC; Omen Window chung kích hoạt/boss/NPC;
Blood Crystal chung kích hoạt/boss/NPC/vùng kích hoạt; tất cả thưởng riêng.

Người dùng đang mở game và yêu cầu chỉ test sau khi bố cục hoàn thiện. Đợt này
chỉ sửa source và đọc diff; không build, không chạy managed/native test, không
launch sandbox và không cài DLL. Không có kết quả kiểm thử mới để báo đạt.

## Đã viết trong source, chưa xác minh bằng build/test

### Transport và phạm vi người nhận

- `Protocol.cs`: protocol source tăng 33 -> 34, thêm command, reply và delivery
  riêng cho encounter. Snapshot encounter đi trong frame `encounters` riêng,
  không dùng skill event hoặc nhét vào world chunk/NPC state.
- `LanSession.Encounters.cs` và `LanSession.cs`: nối command Client -> Host,
  xác thực sender bằng ID kết nối; reply Host -> đúng requester; gửi snapshot
  Host -> Client. Tối đa 8 command/reply mỗi đợt, hàng đợi 128.
- Snapshot/command/reply có kiểm cấu trúc, chiều truyền, instance/epoch và
  revision. Frame encounter không được chen giữa một transfer world chunk.
- Theo dõi thay đổi participation/map/run/nhân vật để gửi baseline hoặc clear
  ngay cả khi encounter không đổi. Client party-only không nhận encounter.
- Delivery lọc lượt Nemesis và danh sách người nhận thưởng về đúng nhân vật
  nhận; không gửi item, trứng, inventory hay lượt riêng của Host cho Client.
- Clear context vẫn giữ watermark instance/revision trong phiên để không cho
  tái xuất bản snapshot cũ. Disconnect xóa state và hàng đợi transport.

### Authority và lựa chọn Nemesis

- `EncounterHost.cs`: yêu cầu participation thật trong accepted player state;
  xác minh lại peer/map/run/nhân vật/khoảng cách trước khi cho dispatch native.
- Omen Window đổi từ action rỗng/tự bắt đầu sang `Activate` có reservation.
- `EncounterHost.Nemesis.cs`: lượt riêng theo encounter + identity/character,
  revision riêng, lựa chọn riêng, reservation và token callback native riêng.
- Lựa chọn của một người không làm stale revision lượt của người khác.
  Mỗi lần dispatch vẫn có reservation để adapter kiểm soát việc thực thi native.
- `NativeCompleted` chung không được hoàn tất Nemesis cho mọi người. Callback
  riêng cần đúng execution token, trả cùng outcome ID khi gọi lặp. Banish hoặc
  Empower hoàn tất không tự cấp loot.
- Đây mới là lượt managed trong instance hiện tại, chưa phải persisted native
  Nemesis progression, journal item/trứng hoặc đủ chuỗi empowerment qua các map.

### Bộ điều phối adapter

- `EncounterHostDriver.cs`: pump Host xử lý tối đa 8 request mỗi lần, nối
  reservation -> adapter family -> native dispatch result -> reply -> publish.
- Chỉ nhận adapter cho bốn family đã yêu cầu. Không có adapter thì không đăng
  ký encounter; không tạo controller giả để báo thành công.
- Không dispatch khi reservation không còn hợp lệ hoặc không thể enqueue
  phản hồi reservation. Native từ chối trước thực thi trả lại trạng thái sẵn sàng.
- Exception/dispatch không rõ kết quả chuyển encounter sang Faulted, giữ lỗi
  trong `LastDispatchError`; không tự retry hoặc cấp thưởng.
- Đây là interface/pump thuần managed, **chưa được instantiate trong `LanMod`**
  và chưa có family adapter native cụ thể. Chưa có encounter gameplay mới hoạt động.

### Các ca kiểm tra đã soạn, chưa chạy

`EncounterRewriteTests.cs` đã cập nhật cho action Omen mới, participation,
hai lựa chọn Nemesis khác nhau, callback token không thể chuyển sang nhân vật
khác và outcome không lặp. Bổ sung ca TCP riêng cho command/reply/snapshot,
lọc dữ liệu cá nhân, rời participation và clear context. Viết ca kiểm tra không
đồng nghĩa các ca này đã biên dịch hoặc đạt; tất cả chờ sau khi bố cục hoàn thiện.

## Còn phải triển khai

1. Native adapter từng family: discovery/lifetime/ID theo map/run, tương tác
   Host và Client, quan sát phase/completion, không can thiệp solo.
2. Điều phối native Nemesis khi hai người chọn khác nhau; journal trứng/item
   và tiến trình riêng bền vững qua map/reconnect. Phân biệt kết quả lượt với
   quyền thưởng native; không coi Empower/Banish như Challenge giết boss.
3. Client presentation: vị trí/tương tác, boss/NPC ownership; map travel của
   Rift Beast; vùng kích hoạt Blood Crystal và trạng thái kích hoạt Omen.
4. Private reward executor + durable entitlement/receipt/save/ACK theo family.
   Chưa có mã mới cấp thưởng riêng; outcome ID managed không thay thế journal.
5. Gắn driver/adapter/presentation vào vòng đời `LanMod` sau khi các phần trên
   có contract đầy đủ. Xử lý completion quan sát đồng bộ trong native Execute
   bằng hàng đợi, chỉ xử lý sau bước NativeAccepted.
6. Sau khi bố cục source hoàn thiện mới build và chạy managed/transport tests.
   Native sandbox/cài DLL phải đợi kiểm tra lại game đã đóng. Sau đó nghiệm thu
   theo family và hai máy; giữ nguyên raw full campaign QA failures nếu còn.

## Phiên bản và cài đặt

Source đang phát triển mang `1.1.0-dev.9 / protocol 34` để phân biệt với binary
dev.8/protocol33. Đây không phải một bản phát hành hoặc bản đã cài. Không sửa
manifest/latest.json, package hoặc binary Steam/GitHub trong đợt này. Chưa commit
hoặc push. Các LOCAL/validation untracked có sẵn được giữ nguyên.
