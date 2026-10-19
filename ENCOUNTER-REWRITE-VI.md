# Viết lại co-op encounter — mốc nền 1, ngày 20/10/2026

Theo yêu cầu bỏ hệ thống encounter co-op hiện tại, công việc chuyển sang nhánh `encounter-rewrite`. Dừng bản vá Omen đang thử; không đưa nhánh mở rộng encounter cũ vào bản cập nhật.

## Đã thực hiện

- Gỡ 15 file adapter/logic encounter native cũ, gồm journal phản chiếu field, replica, Nemesis bridge/lifetime, shrine registry, Omen reward replay và các probe phụ thuộc.
- Gỡ thêm 5 file cơ chế map chung: phát lại `ConditionHandler.OnInteract`, áp trạng thái scene graph/object và index hook phụ thuộc. Đây là đường có thể chạy lại hành động native của client dù máy đó không sở hữu encounter.
- Xóa schema encounter cũ, RPC tương tác nhét trong kênh skill, ngoại lệ receipt Nemesis cho nhân vật chết và phần encounter trong world chunk/delta. Protocol thử nghiệm tăng thành 32; không ghép với client dev.27/protocol31.
- Giữ mã NPC transform/HP/animation/VFX, minion/companion, party status và Monolith travel. Việc còn trong binary và các bài managed đạt không phải xác nhận mọi luồng này đã ổn trên hai máy sau thay đổi.
- Giữ các bonus offline đã yêu cầu, gồm bonus CoF Rank 7 x3 Experimental. Đây là chỉnh thưởng offline riêng, không phải hệ thống đồng bộ encounter.
- F4/Changelog ghi rõ encounter co-op chưa được hỗ trợ trong bản thử nghiệm này. Đồng bộ tương tác map chung cũng đang tắt; cửa/quest/ambush phải được kiểm tra lại trước khi phát hành.

## Lõi mới đã viết

`src/LastEpochLAN.Core/Encounters/EncounterHost.cs` là state machine quản lý riêng cho một phòng Host đang kết nối. Nó không tham chiếu Unity/Harmony, không gọi native, không cấp loot và chưa được nối vào gameplay.

Host cấp ID encounter theo instance/epoch/map/run. Lệnh chỉ có loại hành động cụ thể; vị trí và danh tính người gửi lấy từ trạng thái peer mà phòng đã nhận. Kiểm tra revision, sequence, khoảng cách, vai trò và token reservation trước khi adapter tương lai được phép thực thi. Callback native thành công mới chuyển trạng thái; lỗi một phần bị giữ ở `Faulted`, không tự chạy thưởng lại. Hoàn thành tạo một ID ổn định với danh sách người chơi có mặt lúc đó; callback hoàn thành lặp trả lại cùng ID. Đổi map và ngắt phòng làm các token cũ mất hiệu lực.

Lõi này là nền để tích hợp tiếp, **chưa phải hệ thống encounter co-op hoạt động**. Lệnh mới chưa có transport riêng; chưa có adapter native, client presentation, reward executor hoặc quy tắc lượt riêng của Nemesis.

## Kiểm nghiệm

- Release build: 0 warning, 0 error.
- 500 kiểm tra managed đạt: 477 kiểm tra phần giữ lại và 23 kiểm tra lõi mới. Bài lõi mới dùng phòng TCP thật để lấy trạng thái peer; bản thân lệnh encounter đang gọi API trực tiếp, chưa đi qua transport encounter mới.
- Audit compiled metadata/IL: 7 kiểm tra đạt, khảo sát 103 lớp Harmony. Không còn adapter/schema/RPC cũ; không có hook còn lại nhắm vào encounter hoặc callback tương tác chung. Ngoại lệ được ghi rõ là bonus CoF Rank 7 offline. NPC animation, VFX và enemy replica còn trong binary.
- Trong sandbox riêng với nhân vật tạm: gọi hàm thưởng Omen native trực tiếp, không hook encounter, tạo 9 vật phẩm trên đất, trong đó 4 Corrupted; gọi đường thưởng đầy đủ tạo 10 vật phẩm, trong đó 4 Corrupted. Đây là kiểm tra hai hàm thưởng, chưa phải chơi trọn Omen với sóng quái/boss.
- Native smoke cơ bản đạt trên đúng cặp DLL đã build; bài này kiểm tra pose/HP và damage detour, không xác nhận encounter hoặc co-op hai máy.
- **Full native campaign QA vẫn chưa đạt**, do 5 lỗi khởi tạo sandbox: Actor null, hai lỗi PlayFab chưa đăng nhập, UserIdentity chưa sẵn sàng và EncounterPlacementData null. Báo cáo giữ `passed=false`, `observationsPassed=true`. Không lọc lỗi để biến thành kết quả đạt.
- Chưa xác nhận toàn bộ encounter solo hoặc co-op hai máy. Chưa kết luận chắc một hook cụ thể là nguyên nhân duy nhất của lỗi Omen người dùng gặp. Rà source không thấy chỉnh quy tắc Loot Filter; trang bị không thuộc nhóm tự nhặt nguyên liệu/chìa khóa.

Các báo cáo ở `validation/`. Không đưa log/save thật của người dùng lên GitHub. Kiểm thử không thay DLL cài đặt hoặc save thật.

## Các mốc còn phải làm trước phát hành

1. Kiểm chứng solo đầy đủ: Nemesis (Challenge/Empower/Banish), Omen, Blood Crystal, Exiled Mage, Rift Beast, shrine/cache. Không Host/Join phải không có handler encounter LAN nào được cài.
2. Tạo transport riêng cho encounter: Host snapshot/command/ack có giới hạn; client không được gửi trạng thái hoàn thành hoặc tự công bố quyền nhận thưởng. Kiểm tra join muộn, reconnect, đổi map và thứ tự packet.
3. Adapter riêng cho từng loại: giữ controller gameplay native trên Host; client chỉ dựng presentation và gửi thao tác. Đăng ký hook theo vòng đời phòng và instance sở hữu, không hook global lúc khởi động, không phát lại callback hoặc phản chiếu field tùy ý. Không dùng shared no-op IL2CPP làm điểm detour.
4. Thưởng riêng theo nhân vật: xác minh điều kiện trước khi claim; ghi receipt bền vững; quan sát native hoàn tất và save/ack. Lỗi một phần không được tự cấp lại. Không sửa điều kiện thưởng toàn cục, không giả account/identity và không nhân bản inventory Host sang client.
5. Nghiệm thu từng loại trên hai máy: vị trí/phase/boss chung, tương tác hai phía, mỗi người nhận đúng một lượt thưởng, chuyển map/reconnect không tạo trùng hoặc mất thưởng. Sau đó mới mở rộng sang các hoạt động endgame đặc biệt.

Nhánh này chỉ phát hành mã nguồn và bằng chứng kiểm tra. Chưa có DLL/updater mới để chơi; `main` và `update.bat` vẫn đang ở dev.27. Không công bố encounter đã sửa xong.
