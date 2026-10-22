# Faction và auto pickup — dev.55

Giữ version1.1.0-dev.55/protocol44, toàn bộ World Assist và CoF Rank8 LP x20. Cập nhật cả mod và Core theo manifest mới. Đóng gói cùng release dev.55 trên GitHub.

## Bonus và text

| Faction | Rank | Kết quả mới |
|---|---:|---|
| Circle of Fortune |7|Exiled Mages rơi gấp15 vật phẩm Experimental thay gấp3|
| Circle of Fortune |10|Bosses have300% increased drop rate for any specific rewards they have|
| Circle of Fortune |11|Đồ lẽ ra rơi Rare có50% cơ hội thành Exalted|
| The Woven / TheWeaver |7|Reforging Weaver idols is30% more likely to result in2Weaver affixes|
| The Woven / TheWeaver |9|50% more Memory Amber drops in your echoes|

Rank8 CoF giữ x20 LP. Rank10 của The Woven trong ảnh giữ nguyên +3Weaver Tree points và mở3Woven Echoes. Mô tả Rank7/9 chỉ đổi phần trăm; không đổi số affix, điểm cây hoặc Echo mở từ Masque.

Gán giá trị tuyệt đối vào field native và refresh khi bật rank/đọc lại faction; chỉ áp cho local actor offline, thành viên đã đạt rank và không tắt bonus đó. Rank7 CoF dùng scope của lượt loot Exiled Mage: native đã x2, correction x7.5 trước stochastic rounding cho tổng x15; không nhân lại những lần rounding khác.

Đã đối chiếu jump table và getter native cho boss loot, Rare→Exalted, idol reforge và Memory Amber. Riêng The Woven: text English trong game/ảnh là10%, nhưng native constants của build đang cài là31%/30%. Bản sửa đặt chính xác bonus thực tế30%/50% và text đồng bộ với yêu cầu, không lấy text cũ làm giá trị runtime. Chi tiết trong `validation/dev55-factions-pickup-native-evidence.json`.

## Auto pickup

- Vàng, key, potion, crafting materials và Ancient Bones chỉ tự nhặt trong vòng tròn bán kính3world units quanh nhân vật, đo trên mặt phẳng XZ giống vòng Combat Assist. Không quy đổi từ pixel; không phụ thuộc radius đang chọn cho skill trong Combat Assist.
- Phải có dịch chuyển ngang thực tế giữa các frame. Đứng yên, giữ lệnh chạy khi bị chặn, thay đổi độ cao đứng tại chỗ, frame cũ hoặc teleport không kích hoạt nhặt. Dọn mẫu chuyển động khi đổi scene/nhân vật.
- Không nhận đồ ngay ở hook tạo loot, không kéo vật phẩm về chân nhân vật. Loot ngoài phạm vi nằm lại trên đất để nhặt sau khi đến gần.
- Vàng vẫn được scale đúng một lần ở lúc drop theo bonus hiện tại. Nhặt dùng native API theo id; giữ pooling, âm thanh, giới hạn charges, storage và các callback native. Potion đầy được để lại; equipment và tutorial item không tự nhặt.
- Khóa đường native auto trigger cho các loại đã bật nếu chưa đáp ứng movement/radius. Với auto key/material, chặn riêng lượt vacuum diện rộng phát sinh từ thao tác đó để không hút theo đồ ngoài vòng3; scope được khôi phục kể cả exception.
- Quét tối đa mỗi0.05giây, giới hạn số lượt duyệt và8 thao tác trong một lượt. Cài đặt Gold/Keys/Potions/Materials/Bones hiện có vẫn điều khiển từng loại. Text trong Mod Settings và changelog đã cập nhật.
- Nhặt bằng click và Range Pickup là thao tác chủ động, giữ hành vi hiện tại. XP/Favor/Memory Amber không được bổ sung vào nhóm auto pickup của mod trong thay đổi này.

## Kiểm chứng và cài

Build Release0warnings/errors.40 kiểm tra actual-source faction,32 kiểm tra actual-source pickup,1.038 self-tests,65 World Assist và144 compiled/native audit đã pass. Fixture dùng recording substitutes; gameplay và hình vẽ trong game chưa được kiểm chứng với DLL mới.

Gói `TM-Coop-1.1.0-dev.55.zip` chứa đúng cặp DLL,manifest, tài liệu và updater. Đóng game rồi chạy `update.bat` để tải cặp DLL đã publish; hoặc giải nén toàn bộ gói và chạy `Install-WorldAssist-Candidate.ps1` để cài trực tiếp, có kiểm tra checksum và backup. Script cài trực tiếp mặc định dùng `D:\SteamLibrary\steamapps\common\Last Epoch`; có thể truyền `-GameRoot` để chọn thư mục khác.
