# dev.31 — Corruption rewards, Bloodrage và sửa Shade catch-up

Protocol **40**, giữ phòng public-IP Internet chỉ **F2 Shared Stash + Loot Filters**. Tất cả tùy chỉnh gameplay dưới đây áp dụng Play Offline, kể cả khi ở phòng Internet của mod. Cập nhật cả cặp DLL trên Host và mỗi Client để mỗi máy nhận cùng bản sửa. Encounter co-op chưa hoàn thiện vẫn tạm dừng.

## Lợi tức Corruption

Bỏ diminishing returns ở sáu đường xử lý native đã kiểm tra. Sau phản hồi không được giảm thưởng hiện tại, hệ số mới kiểm tra không thấp hơn công thức cũ ở mọi C nguyên1..10.000:

| C | EXP tăng thêm | Vàng quái gồm mod | LP roll bonus | WW promotion-roll bonus | Echo Reward Rarity |
|---:|---:|---:|---:|---:|---:|
|100|+100%|×2,8|+7,209%|+8,686%|+89,656%|
|300|+300%|×10,2|+21,628%|+26,058%|+268,967%|
|500|+500%|×20|+36,046%|+43,429%|+448,278%|
|700|+700%|×32,2|+50,465%|+60,801%|+627,589%|
|1.000|+1.000%|×55|+72,093%|+86,859%|+896,556%|

- EXP: +1% mỗi C. Giữ phần EXP từ monster modifiers.
- Vàng gốc từ SpawnGoldForActor: `1 + 0,0012 × C`, giữ bonus mod `max(1,C/40)`. Tổng hai hệ số nhân chồng **không tuyến tính tuyệt đối**. Nguồn vàng khác có cách tính riêng; rounding vàng nguyên vẫn native. Không dùng phương án ×19 ở1.000 vì thấp hơn bản cũ.
- Memory Amber standard drop: dùng cùng slope cho phần trước/sau500, đọc asset thực và chọn slope lớn hơn để không nerf biến thể. Defaults maximum trước modifiers=`6+0,01×C`; không bảo đảm mỗi lượt nhận maximum. Giữ native RNG, quantity và faction modifiers.
- LP/WW: bonus hệ số roll tăng tuyến tính; không phải xác suất tuyệt đối hoặc số LP/WW cuối cùng. Giữ giới hạn native4LP/28WW và xác suất roll100%, CoF attribution, level gates và caller minimum. Không thêm hook RNG global.
- Echo Reward Rarity: bỏ lũy thừa/log trong phần Corruption; giữ difficulty gate. Không đồng nghĩa tăng số đồ/Unique đúng tỷ lệ trên.
- Sửa cả các consumer có công thức nhúng trực tiếp, không chỉ các helper/preview. Không thay đổi density, HP/damage, Stability hoặc đường cong similar-item Imprint.

## Bloodrage

- Năm nấc earned charge của Rage of Morditas: **2 → 4 → 6 → 8 → 10**.
- Đồng bộ ba đường local: thông báo nhận charge, UI tính lại từ kill count và initial spendable reward balance. Giữ counter nấc native1..5, kill thresholds và timer để không bỏ qua mốc.
- Native tiếp tục tiêu từng charge; không nhân lại remaining balance khi mở bảng/refresh. Standalone reward encounters và native debug override giữ cách tính riêng. Không bật lại Encounter co-op.

## Sửa Corruption tăng vọt sau Shade / Echo of a World

- Bản cũ nhân x5 cả catch-up nên có thể vượt xa timeline đang bắt kịp và gây feedback giữa các timeline. Tái hiện logic: current100/highest15000/native result14255 bị nhân thành **70875**. Đây là fixture, không phải số đọc từ save Client bị báo lỗi.
- Bản mới chỉ x5 thưởng base/Gaze; catch-up95% được tính lại trên khoảng cách còn lại tới highest-C gốc. Giữ cap timeline, loss/no-gain và integer safety.
- Thêm log một lần khi hoàn thành Shade: before/after, Gaze, highest character/stash. Không log mỗi preview/frame.
- **Không tự sửa Corruption đã lưu hoặc highest-in-stash đã bị đẩy cao.** Native vẫn có thể bắt kịp một highest vốn đã cao. Chưa xác minh chính xác sự cố Client100→~71k vì chưa có log/save máy đó; cần bản sao để khôi phục đúng mốc, không tự chia5.

## Báo cáo nghiên cứu, không thay đổi tỷ lệ

Đã liệt kê tỷ lệ Weaver Imprint trong `WEAVER-IMPRINT-RATES-CURRENT-VI.md`. Dev31 **không sửa tỷ lệ Imprint**.

## Kiểm chứng và cập nhật

- Release build: **0 warnings / 0 errors**.
- **886 managed/TCP checks PASS**; bao gồm regression Corruption, no-nerf1..10.000, Bloodrage và F2/filter.
- **34 compiled audit checks PASS**, 130 lớp patch, **63 chọn / 67 bỏ**; policy shared-world vẫn false, các hook reward local được kiểm tra riêng, không có Disintegrate/Arcane override.
- Updater fixture/live và package hashes được ghi ở `validation/release-dev31-*` sau đóng gói.
- Chưa chạy native gameplay/UI/FPS/two-PC acceptance. Không mở hoặc đóng game, không cài Steam trong lượt build/upload.

Thoát game khi thuận tiện rồi chạy `update.bat` trong thư mục game. Hai file bắt buộc cùng manifest: `Mods/LastEpochLAN.dll` và `UserLibs/LastEpochLAN.Core.dll`.
