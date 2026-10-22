# Faction và auto pickup — dev.55

Giữ version1.1.0-dev.55/protocol44, CoF Rank8 LP x20; World Assist đã gỡ theo yêu cầu mới. Cập nhật cả mod và Core theo manifest mới. Đóng gói cùng release dev.55 trên GitHub.

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

## Auto pickup hiện tại

Cơ chế quét đồ và điều kiện di chuyển của bản trước đã được thay thế. Xem [báo cáo hiện tại](NATIVE-PICKUP-dev55-VI.md): dùng trigger native, phạm vi gốc x3, thêm Keys/Materials qua tương tác nhặt gốc. World Assist đã được gỡ toàn bộ.

Đóng game rồi chạy `update.bat` để nhận cặp DLL của release dev.55.
