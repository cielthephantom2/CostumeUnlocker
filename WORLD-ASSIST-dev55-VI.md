# World Assist — dev.55

Giữ version1.1.0-dev.55 và protocol44. Bao gồm CoF Rank8 x20, text và các sửa trước đó. Đóng gói trong release dev.55 trên GitHub; DLL đang được game sử dụng không được ghi đè khi game còn mở.

Tham khảo file `D:\Downloads\Le_helper.dll` (native x64 C++, SHA256 `2AD11DBC09622196F36E9818E9B2CBAD170F4236B3F272515C4B46B08937F465`). DLL không có metadata .NET để lấy source C# bằng ILSpy. Đã đọc cấu hình, chuỗi API và mô tả hành vi trong binary; triển khai lại bằng C#/Harmony/Unity cho mod hiện tại. Không nạp hoặc chạy DLL tham chiếu, không đưa DLL đó vào gói phát hành.

## Tính năng

- Danger areas: vòng tròn, hình nón và hành lang dự báo đường đạn cho AbilitySync của địch trong60m. Xác định chủ thể qua CreationReferences, theo chuỗi chủ thể tối đa8 cấp, lọc người chơi local, người chơi khác và đồng minh; không vẽ khi chưa xác định được địch. Ưu tiên tối đa64 vùng gần nhất mỗi frame.
- Bảng riêng theo tên skill, không phân biệt hoa/thường. Giữ mặc định từ DLL tham chiếu `Flaming Ground=4`, bán kính dự phòng đạn0.75m/vùng tĩnh2m. Cấu hình được ở UI: `Tên skill=4; Tên khác=cone:60:8; Arrow=projectile:0.75:12`. Cũng đọc cú pháp `kegel` của DLL tham chiếu. Đây là vùng ước lượng theo bảng, không phải xác nhận hitbox cho mọi skill trong game.
-14 loại mục tiêu: Shrine, Blood Crystal, Nemesis/Rage of Morditas, Rift Beast Nest, Rune Prison, Omen Window, Container, Chest, Cache, Tomb Entrance, Time Rift, Quest Target, Transition và Other. Mỗi loại có màu, một biểu tượng riêng, công tắc world/edge arrow/Tab/minimap/route. Mũi tên mặc định cho Blood Crystal, Nemesis và Quest để giảm chồng chữ.
- Đọc registry click listeners, encounter và minimap icons của game; bổ sung bootstrap theo type mỗi4giây để tìm đối tượng đã được tạo trước khi hook chạy. Đọc trạng thái shrine/chest/rune prison/rift/nest/cache/Omen/Nemesis để ẩn mục tiêu đã dùng khi có dữ liệu trạng thái. Nhận diện theo tên prefab/key/sprite và màu quest gốc, không chỉ dựa vào text English.
- Đường đi dùng NavMesh.SamplePosition + NavMesh.CalculatePath của game, tối đa một lượt tính mỗi0.4giây. Dùng điểm pathfinding riêng của interactable nếu có. Mặc định chọn mục tiêu gần nhất; có thể chọn bất kỳ mục tiêu đã tải trong tab Choose target. Chọn mục tiêu sẽ bật quyền route của loại đó. Không tự di chuyển/tương tác.
- Vẽ đường ở world, Tab map hoặc minimap độc lập. Partial path vẽ mờ; không vẽ đường thẳng thay thế khi không tìm được đường. Kiểm tra đầu đường bằng NavMesh.Raycast sau khi nhân vật di chuyển; bỏ các góc đã đi qua và không kéo đường cũ qua tường.
- Phép chiếu đường map lấy từ ba icon native trong suốt trên đúng frame, theo zoom/pan/UI scale hiện tại. Đường bị cắt ở biên vùng map; không dùng phép chiếu của frame cũ.
- Vị trí lấy từ transform visual ở late update, dự đoán tới thời điểm vẽ với giới hạn40ms. Teleport, snapshot cũ, tọa độ lỗi và đổi scene không tiếp tục extrapolate. Dọn toàn bộ marker/probe/path khi đổi scene, đổi nhân vật, tắt tính năng hoặc rời offline.

## Dùng và cài

1. Đóng Last Epoch, giải nén toàn bộ gói release vào một thư mục riêng.
2. Chạy `powershell -ExecutionPolicy Bypass -File .\Install-WorldAssist-Candidate.ps1`. Có thể truyền `-GameRoot 'đường dẫn game'`. Script kiểm tra SHA256 của cả hai DLL, từ chối khi game còn mở, backup rồi cài đúng cặp mod/Core. Không tải file từ mạng.
3. Vào Play Offline, mở **F4 → Mod Settings → World Assist**. Bật/tắt từng loại trong Display / types; chọn đường trong Choose target. Cấu hình lưu cùng file `UserData/LastEpochLAN.tools.json`.

Gói release có `update.bat` để tải cặp DLL đã publish từ GitHub; script cài trực tiếp cũng được giữ trong gói. Không ghi đè config/save của người chơi.

## Kiểm chứng

Build Release0warnings/errors;65 kiểm tra World Assist,141 compiled/native audit và13 kiểm tra script cài đều đạt. Fixture và compiled/native audit có kết quả trong `validation/dev55-world-assist-fixture.json` và `validation/dev55-world-assist-audit.json`.

Chưa kiểm chứng render, mức FPS hoặc độ khớp hình vẽ trong game: game đang chạy DLL cũ nên chưa nạp bản mới. Cần kiểm tra trong game: vị trí icon/đường khi zoom/pan Tab và minimap; cone/projectile của một địch có tên trong bảng; skill của bản thân/pet không có vùng đỏ; marker biến mất sau tương tác; đổi zone không giữ marker cũ. Bản đồ chỉ có mục tiêu mà game đã tải; đối tượng có tên/key lạ có thể vào nhóm Other.

Runtime report có trường `worldAssist` với số mục tiêu theo14 loại, số ability địch, bảng radius, trạng thái/corners của route và frame phép chiếu để đối chiếu sau khi thử.
