# dev.38 — Ladder phòng và tìm địch của minion/companion x2

## Ladder

- Giữ bảng, hàng nhân vật, icon, bộ lọc và tìm kiếm của game. Gỡ trạng thái khóa offline tại đúng màn hình Ladder; không giả lập đăng nhập online và không tạo overlay thay thế.
- Bản tin thành viên phòng mang thêm một hồ sơ nhân vật có giới hạn: tên, class/mastery, level/XP, best Arena wave, max Corruption, Omen kills/windows, hardcore/deaths, cycle và tối đa5 skill chuyên hóa.
- Dữ liệu được đọc từ nhân vật offline đang tải trên từng máy. Host xác định ID/tên tài khoản của người gửi từ kết nối. Đây là thành tích do máy của người chơi cung cấp, không phải bảng xếp hạng được máy chủ game xác nhận.
- Hiển thị thành viên **đang kết nối**, gồm Host. Đổi nhân vật thay dòng cũ; rời phòng bỏ dòng đó. Không lưu lịch sử thành viên đã rời phòng. Ngoài phòng hiển thị nhân vật hiện tại với tiêu đề LOCAL LADDER; trong phòng là ROOM LADDER.
- Các bảng Arena, Level, Corruption và Omen dùng đúng loại record native. Xếp level trước rồi XP; Arena theo best wave; Corruption theo max đã lưu; Omen theo kills rồi completed windows. Đồng điểm có thứ tự ổn định. Bộ lọc class/hardcore/cycle giữ tách dữ liệu; cycle lấy từ nhân vật offline thay vì dịch vụ online.
- Chỉ có dữ liệu thành tích cá nhân. Mục Arena đội2/3/4 để trống vì không có thành tích đội được ghi nhận; không cộng/ghép điểm cá nhân thành kết quả đội. Mốc thời gian của record là thời điểm tạo bản chụp hiển thị, không phải thời điểm đạt thành tích.
- Cập nhật UI tối đa mỗi giây khi dữ liệu thay đổi, giữ vị trí cuộn. Các listener của mod được gỡ khi đóng. Thanh trạng thái/cờ đồng bộ thế giới và chiến đấu không được bật bởi Ladder.

## Minion/companion

- Chỉ áp dụng offline cho summon thuộc người chơi cục bộ ở stance tấn công `Assassinate`; bao gồm sub-minion có chuỗi chủ sở hữu hợp lệ. Bỏ qua player, enemy, summon không thuộc local và stance bảo vệ.
- Trong `UsingAbility.activateIfEnemyInRange`, hệ số phát hiện hiệu dụng `(1+aggroRangeModifier)` được nhân2. Ví dụ modifier+50% trở thành3x cơ bản, tức2x phạm vi hiệu dụng trước đó.
- Trong tìm mục tiêu địch, giới hạn hữu hạn `MaxDistanceFromSummoner` được nhân2. Tìm đồng minh/bản thân không thay đổi. Giới hạn vô hạn vẫn giữ vô hạn.
- Chỉ đổi trường trên instance trong thời gian lời gọi native, rồi khôi phục qua Harmony finalizer kể cả khi lỗi. Không đổi asset chung/global aggro constants, không nhân chồng qua các frame, không tăng tầm đánh hay tạo thêm lần đánh.
- Runtime report có `roomLadder.Error`, `aggressiveMinions.ExpandedAcquisitions` và `ExpandedTargetSearches` để xác nhận hook thực sự chạy trong game.

## Build, kiểm tra và cài đặt

- Mod **1.1.0-dev.38**, protocol **43**. Host và mọi Client cần thay **cả hai** `Mods/LastEpochLAN.dll` và `UserLibs/LastEpochLAN.Core.dll`, rồi khởi động lại game. Bao gồm thay đổi Shared Stash dev37.
- Release build:0warnings/0errors;1.009 managed/TCP checks;78 compiled audit checks;17 kiểm tra actual-source Ladder bằng stub;16 kiểm tra actual-source minion bằng stub.
- TCP test có Host+2Clients: dữ liệu Ladder chạy khi shared world không Available, chống giả ID/tên account, đổi nhân vật và rời phòng. Fixture UI kiểm cả4 loại native record, cycle, marker chết Hardcore, mở/đóng/lặp listener, score refresh và giữ cuộn. Fixture AI kiểm x2 trên modifier có bonus, bảo vệ/enemy/online không đổi, chuỗi chủ sở hữu và khôi phục khi exception/100lần tìm kiếm.
- Chưa nghiệm thu Unity rendering hoặc gameplay thực tế. Build/fixture không chứng minh rằng bảng đã hiển thị đúng trong game hoặc AI đã bắt mục tiêu ở khoảng cách mong muốn.
- Bằng chứng build/test/cài: `validation/dev38-*.json` và `.txt`. Ứng viên cục bộ trong `local-releases/dev38-room-ladder-minion-candidate` cùng ZIP. Không tự upload GitHub; root/latest/public release giữ dev35.
- Không sửa interop hoặc save nhân vật; Corruption gain issue đã hoãn trước đó không thay đổi. Trước cài kiểm tra game đã đóng, sao lưu hai DLL và SharedChest, kiểm SHA256 sau cài. Không tự mở/đóng game.

Kiểm tra trong game: Host và Client cùngdev38/protocol43, mở Ladder bằng nút gốc; thử Arena/Level/Corruption/Omen, class và Hardcore, thay nhân vật/rời phòng. Với summon, đổi qua lại tấn công/bảo vệ rồi so khoảng phát hiện; xem counters runtime report. Ghi log nếu có lỗi native UI.
