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

## 3. Auto pickup — thay đổi mới

Mở F4 → Mod Settings và bật các loại muốn thử: Gold, Keys, Potions, Materials, Ancient Bones.

- [ ] Gold/potion/Ancient Bones dùng bán kính cố định5, không phụ thuộc chuyển động.
- [ ] Keys/Materials dùng bộ quét cũ với bán kính5; nhặt đúng một lần và lưu vào storage native.
- [ ] Potion đầy charges vẫn nằm lại; equipment/tutorial item không tự nhặt.
- [ ] Tắt từng loại để kiểm tra bộ quét bỏ qua loại đó; equipment/tutorial không được tự nhặt.
- [ ] Đổi zone/echo rồi quay lại loot còn trên đất; pickup vẫn hoạt động, không còn gọi Attach/initialise trigger bổ sung.
- [ ] Click thủ công / Range Pickup vẫn hoạt động.

Phạm vi sửa lần này là5nhóm trên. Không thêm auto pickup XP/Favor/Memory Amber.

## 4. World Assist đã gỡ / pickup radius5

- [ ] F4 không còn World Assist; không còn danger rings, marker hoặc đường đi của tính năng này.
- [ ] Gold/potion/Ancient Bones nhặt trong bán kính5; potion đầy nằm lại.
- [ ] Keys/Materials tự nhặt trong vòng5, kể cả đứng yên; storage tăng đúng, equipment không tự nhặt.
- [ ] Tắt từng loại và đổi zone/echo; kiểm tra FPS và log.
- [ ] Range Pickup vẫn hoạt động khi click thủ công.

Xem hướng dẫn mới: [native pickup](NATIVE-PICKUP-dev55-VI.md).

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
- [ ] Assist đang Stop vẫn Stop; F1 đang mở vẫn pause, không tự đóng F1.
- [ ] Lặp Join/Disconnect; kiểm tra runtime report `DisconnectRefreshes`, `Armed`, `pauseReason` và log.

## Combat Assist phản ứng theo frame

- [ ] Potion tại ngưỡng HP vẫn ưu tiên khi đang channel; giữ reserve và interval.
- [ ] Slot interval>0 đã bật/đến lượt làm channel nhường, cast khi game cho phép; sau đó channel có thể tiếp tục.
- [ ] Hai slotHold0 không liên tục cắt channel của nhau; cooldown/MP/điều kiện vẫn chặn đúng.
- [ ] F1/F4/menu/mất focus vẫn pause; Join→Disconnect giữ Start/Stop.
- [ ] Kiểm tra FPS và diagnostics ChannelYields/prioritySlot; bản này chưa có xác nhận native gameplay.
