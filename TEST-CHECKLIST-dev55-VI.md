# Checklist kiểm tra trong game — dev.55 / protocol44

Bản release vừa đăng: https://github.com/cielthephantom2/CostumeUnlocker/releases/tag/v1.1.0-dev.55

Đóng game, chạy update.bat để cập nhật cả mod và Core, rồi vào Play Offline. Version vẫn là1.1.0-dev.55/protocol44. Đây là danh sách hành vi cần kiểm tra, chưa phải kết quả xác nhận gameplay trong game.

## 1. Circle of Fortune — thay đổi mới

- [ ] Rank7: text Exiled Mages drop15times as many Experimental items; kiểm tra loot Experimental từ Rune Prison tăng theo hệ số mới, thay x3 trước đây.
- [ ] Rank8: text Unique items have20times the native chance to roll Legendary Potential; LP tối thiểu1 vẫn giữ. Đây là hệ số xác suất, không phải đảm bảo2–4LP.
- [ ] Rank10: boss-specific rewards có300% increased drop rate, thay150%; kiểm tra cả dòng mô tả.
- [ ] Rank11: đồ lẽ ra rơi Rare có50% cơ hội thành Exalted, thay25%; kiểm tra cả dòng mô tả.
- [ ] Các bonus chỉ áp khi là thành viên, đã mở rank và bonus đó đang bật. Tắt/bật lại rank không được nhân chồng hệ số.

## 2. The Woven — thay đổi mới

- [ ] Rank7: Reforging Weaver idols is30% more likely to result in2Weaver affixes; thay10% trong ảnh.
- [ ] Rank7 vẫn +1Weaver Tree point và mở5Woven Echoes từ Masque.
- [ ] Rank9:50% more Memory Amber drops in your echoes; thay10% trong ảnh.
- [ ] Rank9 vẫn +2Weaver Tree points và mở2Woven Echoes.
- [ ] Rank10 The Woven giữ nguyên +3Weaver Tree points và mở3Woven Echoes. Mức300% ở trên thuộc CoF Rank10.

## 3. Auto Pickup hiện tại — cách cũ +0,5giây

- [ ] F4 bật các loại Gold/Keys/Potions/Materials/Ancient Bones/Woven Echoes, text all map /0,5s.
- [ ] Đồ mới rơi từ nguồn xa vẫn tự nhận sau khoảng0,5s; không cần tới gần hoặc di chuyển.
- [ ] Hai nhóm rơi cách nhau0,2s phải có deadline riêng; nhóm sau không bị vacuum sớm.
- [ ] Gold/bones không cộng trùng; potion đầy để lại; inventory/storage tăng đúng.
- [ ] Equipment/tutorial và loại bị tắt không bị auto pickup. Click Range Pickup vẫn ngay lập tức.
- [ ] Thử đi xa sau khi drop, nhặt tay trước deadline, đổi map/nhân vật trong0,5s và túi đầy.
- [ ] Quan sát FPS khi loot nhiều; chưa có số đo FPS cho bản này.

## 4. World Assist đã gỡ

- [ ] F4 không còn World Assist, danger rings, marker hoặc đường đi của tính năng đó.

Xem [pickup hiện tại](NATIVE-PICKUP-dev55-VI.md). Các checklist pickup cũ phía dưới chỉ là lịch sử; bản hiện tại dùng toàn map +0,5s.

## 5. Các sửa gần đây đã nằm trong bản này — kiểm tra lại

- [ ] Forgotten Knights Rank1: Egg of the Forgotten100%, thay40%; text đồng bộ.
- [ ] Forgotten Knights Rank2:25% cơ hội nhận gấp đôi stability khi hoàn thành Echo, thay10%; text đồng bộ.
- [ ] Forgotten Knights Rank5:50% bonus cơ hội Temporal Keystone từ Harbingers, gồm Aberroth, thay20%; text đồng bộ.
- [ ] Echo Rewards mọi loại: x2 cả Timeline Stability cơ bản và Bonus Timeline Stability. Ví dụ24 / tối đa50 thành48 / tối đa100. Khi FK Rank2 proc, phần base có thể được nhân đôi thêm.
- [ ] Temporal Keystone: nhận x2 stability, vẫn chỉ tiêu hao1key; số hiển thị phải khớp phần được cộng, vẫn chịu cap timeline.
- [ ] Meteor Extinction:33% mỗi điểm thực tế tạo meteor lớn; không chỉ đổi text.
- [ ] Meteor Heat Flux:2Frost Claws mỗi điểm; dùng100% mana cost Frost Claw thay120%; tooltip đồng bộ.
- [ ] Arcane Ascendance: di chuyển tự do khi bật, không bị prevent movement.
- [ ] Tranquility: không còn chặn hồi mana khi bật Arcane Ascendance; vẫn giữ giảm mana cost20% mỗi điểm và bỏ text Cannot Regenerate Mana.
- [ ] Archmage, khi có đủ5điểm: dưới300max mana → +5spell damage,35% hoàn20%;300–999 → +10,70% hoàn40%; từ1000 → +15,100% hoàn60%. Kiểm tra cả số và text khi đổi max mana qua các ngưỡng.
- [ ] Chuyển Empowered Monolith → Normal Monolith: kiểm tra lỗi MelonLoader từng báo có còn xuất hiện không.

## 6. Các mục đã gỡ / tạm hoãn

- [ ] UI Shortcut đã gỡ; F4 Mod Settings và F1 Combat Assist vẫn là giao diện đang dùng.
- [ ] Default Meteor đã rollback: không có tùy chọn ép hiệu ứng Meteor mặc định; hiệu ứng từ Vilatria's Downfall giữ hành vi game.
- [ ] Change Class đang tạm hoãn, chưa có trong bản release này; không đổi/reset class nhân vật.

Với các tỷ lệ ngẫu nhiên, vài lần rơi chưa đủ kết luận xác suất đúng/sai. Khi thấy lỗi, ghi mục checklist, scene/loại Echo, số điểm node/rank, kết quả mong đợi và kết quả thực tế; ảnh hoặc đoạn log MelonLoader giúp đối chiếu.

## Combat Assist sau disconnect

- [ ] Assist đang Start → Join → Disconnect: F4 đóng và Assist tiếp tục trên cùng nhân vật.
- [ ] Host tắt room/mất mạng, F2 đang mở: cleanup không giữ pause vì room.
- [ ] Assist đang Stop vẫn Stop; F1 đang mở vẫn mở và Assist đã Start tiếp tục chạy.
- [ ] Lặp Join/Disconnect; kiểm tra runtime report `DisconnectRefreshes`, `Armed`, `pauseReason` và log.

## Combat Assist phản ứng theo frame

- [ ] Potion tại ngưỡng HP vẫn ưu tiên khi đang channel; giữ reserve và interval.
- [ ] Slot interval>0 đã bật/đến lượt làm channel nhường, cast khi game cho phép; sau đó channel có thể tiếp tục.
- [ ] Hai slotHold0 không liên tục cắt channel của nhau; cooldown/MP/điều kiện vẫn chặn đúng.
- [ ] F1/F4/inventory/stash/passive/skill/menu/chat mở: Assist vẫn chạy, potion vẫn được kiểm tra và channel không tự nhả vì UI. Mất focus/chết/loading/cinematic/game time pause vẫn dừng; Join→Disconnect giữ Start/Stop.
- [ ] Kiểm tra FPS và diagnostics ChannelYields/prioritySlot; bản này chưa có xác nhận native gameplay.

## Auto Pickup giảm tải + Woven Echoes

- [ ] Tick tối đa5lượt/giây; mỗi lượt tối đa5ứng viên và1lệnh native, có thể lấy nhóm món đã duyệt.
- [ ] Woven Echoes được nhặt/lưu vào tab gốc trong radius5; tắt riêng Woven thì để lại.
- [ ] Bên ngoài5, equipment/tutorial và loại bị tắt không bị hút theo.
- [ ] F4 không còn debug pickup hoặc chữ including while standing; log không còn Auto Pickup diagnostic.
- [ ] Kiểm tra potion đầy, Range Pickup, đổi zone và map nhiều loot/FPS.

## Combat Assist khi mở UI

- [ ] Start ngay khi F1 mở: tự cast khi đủ điều kiện, potion dùng đúng ngưỡng HP.
- [ ] Mở F4, Shared Stash, inventory, bảng skill/passive, menu/chat: Assist tiếp tục; đóng UI không phải bấm Start lại.
- [ ] Giữ channel rồi mở từng UI: không bị nhả chỉ vì UI; kỹ năng interval vẫn được ưu tiên khi đủ điều kiện.
- [ ] Stop khi F1 mở: dừng ngay và không tự Start khi đóng UI.
- [ ] Mất focus, chết/loading, cinematic và game time pause: không cast; trở lại trạng thái hợp lệ thì tiếp tục nếu còn Start.

## Icon người chơi trên minimap

- [ ] Icon vàng cũ trên minimap góc màn hình được thay bằng1 chấm xanh dương có viền, đường kính gấp3 chấm NPC (gấp đôi chấm xanh bản trước).
- [ ] Di chuyển, đổi hướng, đổi class/model, +/- zoom: chấm theo đúng vị trí, giữ kích thước tương đối; không đổi kích thước nhân vật.
- [ ] Tab mở: icon gốc; Tab đóng: chấm xanh dương; lặp lại không tạo icon trùng.
- [ ] Tắt Enemy Map Dots: chấm người chơi vẫn còn; bật lại: màu và kích thước quái giữ nguyên.
- [ ] Đổi map/Monolith, về menu rồi vào nhân vật khác: không còn marker nhân vật cũ, nhân vật mới hiện1 chấm.

## Auto Pickup native trigger — bản hiện tại thay thế giới hạn5Hz/5món

- [ ] Đi ngang key/material/Woven Echoes trong radius5: nhặt theo callback sau delay rơi của game, không cần đứng chờ cursor.
- [ ] Lặp ở map nhiều gear xa và một nhóm20+ vật liệu gần; so với gold/potion thật trong cùng map.
- [ ] Tắt nhãn/ẩn loot bằng filter: tự nhặt loại đã bật vẫn hoạt động.
- [ ] Potion chưa đầy trong radius5 tự nhặt; potion đầy vẫn để lại. Gold/bone không bị cộng trùng.
- [ ] Đồ ngoài5, trang bị, tutorial và loại bị tắt không bị auto vacuum. Đứng yên vẫn nhặt.
- [ ] Túi đầy không spam xử lý; mở chỗ trống rồi nhặt lại. Click Range Pickup vẫn hoạt động.
- [ ] Đổi map/Monolith, nhận loot đã lưu, scene UI phụ và Join/Disconnect: trigger không mất hoặc sót sang map khác.
- [ ] Không có log pickup định kỳ; nếu có lỗi native thì kiểm tra dòng lỗi cụ thể.

## Prophecy giữ cơ chế charge gốc

- [ ] Có9 charge: một lần kích hoạt hợp lệ dùng3, còn6. Không tự xả hết charge.
- [ ] Số charge tiêu thụ trên UI và thông báo vẫn theo game gốc.
- [ ] Minimap chấm xanh scale0.90, Auto Pickup native-trigger và Combat Assist vẫn hoạt động.

## Auto Pickup toàn map +0,5s — kiểm tra cuối

- [ ] Không còn radius5 hoặc trigger bridge mới. Mỗi món chờ0,5s riêng; không thêm giới hạn chia lượt.
- [ ] Kiểm tra Keys/Materials/Woven Echoes với loot filter ẩn nhãn, nguồn rơi xa và nhiều món đồng thời.
- [ ] Prophecy giữ charge gốc; Combat Assist/minimap vẫn hoạt động.
