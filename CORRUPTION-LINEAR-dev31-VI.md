# dev.31 — Bỏ diminishing returns của lợi tức Corruption

Bản dev31, protocol40; phòng public-IP chỉ F2 + Loot Filters, Encounter co-op đang tạm dừng. Báo cáo phát hành tổng hợp ở RELEASE-1.1.0-dev31-VI.md. Không mở/đóng game để kiểm thử hoặc tự cài Steam.

## Bloodrage charges bổ sung theo yêu cầu

- Rage of Morditas: năm nấc native1/2/3/4/5 nhận tổng2/4/6/8/10 charge. Giữ nguyên ngưỡng kill và timer; không nhân đôi `_bloodrageChargeCount` nội bộ vì nó là chỉ số nấc tiếp theo.
- Ba hook offline: `SendMorditasChargeGainedMessage` chuyển tổng charge thông báo x2; `GetBloodrageChargeCount` chuyển kết quả tính lại từ kills x2; `RageOfMorditasRewardLogic.Initialize` chuyển initial reward count x2 cho non-standalone encounters. Native khởi tạo balance thật cho mỗi người, rồi tự quản lý việc tiêu từng charge.
- Không nhân lại balance khi mở UI/refresh/tiêu charge. Standalone reward encounters và debug override giữ quy tắc native. Không bật lại đồng bộ Encounter giữa host/client, không thay threshold asset hoặc đưa thêm callback replay.
- Native đã xác nhận FixedUpdate_Hunt nhúng trực tiếp GrantBloodrageCharge, gọi Send...; HuntVisual timer refresh gọi GetBloodrageChargeCount; CreateRewardLogic truyền bộ đếm gốc cho reward Create, async Create gọi Initialize. Managed tests kiểm tra năm nấc, threshold index, repeat refresh, zero/sentinel, standalone, spending10 lần và overflow; compiled audit kiểm tra ba ranh giới hook, offline guard, không mutate native counter/callback. Chưa native gameplay test.

## Công thức đã chọn sau phản hồi không được giảm lợi tức

- EXP tăng thêm = `0,01 × C` (tức +1% mỗi Corruption). Thay cả helper public và phần tính nhúng trực tiếp trong `MonolithRun.calculateIncreasedRarityAndExperienceFromMods`; giữ các nguồn EXP từ monster modifiers.
- Vàng gốc trong `ItemDrop.SpawnGoldForActor`: hệ số `1 + 0,0012 × C`, bỏ các đoạn giảm trọng số sau 200/500/1.000. Giữ nguyên bonus mod `max(1,C/40)` ở pickup. Vì nhân hai hệ số nên **tổng vàng không tuyến tính tuyệt đối**; ưu tiên yêu cầu mới không giảm thưởng đang có. Không áp dụng phép chuẩn hóa tổng vàng ×19 ở 1.000 đã đề xuất trước đó. Nguồn vàng ngoài đường SpawnGoldForActor giữ cách tính riêng.
- Memory Amber standard drop: áp dụng cùng một hệ số Corruption trước/sau500, dùng hệ số lớn hơn của hai trường asset để không nerf biến thể. Giữ baseline, quantity modifier, faction modifiers và native stochastic rounding/range. Defaults baseline6, hệ số0,01: maximum trước modifiers = `6 + 0,01 × C`. Không phải mọi lượt rơi đều nhận maximum.
- LP roll multiplier = `1 + 0,166 / ln(10) × C/100`. Bỏ logarithm nhưng giữ RNG, CoF attribution, minimum và cap4. RollLegendaryPotential inlines log nên đưa multiplier mới vào non-CoF input, đặt riêng Corruption input=0; không thay đổi web/save.
- WW promotion-roll multiplier = `1 + 0,2 / ln(10) × C/100`. Dùng lại decision tree native với RNG native, level gates, minimum, range0..7, bonus rolls và cap28. Chỉ đổi multiplier Corruption của promotion roll, không nhân số WW cuối cùng và không đặt RNG hook global.
- Echo Reward Rarity = `0,008965562624 × C` (tức +0,8965562624% mỗi C). Lấy bước C=1 nguyên làm hệ số, bỏ cả lũy thừa0,88 và nhánh log sau200. Giữ native difficulty gate ở consumer; static helper cũng cập nhật. Không đồng nghĩa tăng số đồ/Unique cùng tỷ lệ.

## Mốc dự kiến

| C | EXP tăng thêm | Vàng quái, gồm mod | LP roll bonus | WW promotion-roll bonus | Echo reward rarity |
|---:|---:|---:|---:|---:|---:|
|100|+100%|×2,8|+7,209%|+8,686%|+89,656%|
|300|+300%|×10,2|+21,628%|+26,058%|+268,967%|
|500|+500%|×20|+36,046%|+43,429%|+448,278%|
|700|+700%|×32,2|+50,465%|+60,801%|+627,589%|
|1.000|+1.000%|×55|+72,093%|+86,859%|+896,556%|

LP/WW bonus là phần tăng hệ số của đúng roll, không phải xác suất tuyệt đối hay giá trị LP/WW bảo đảm. Xác suất thực tế vẫn dừng ở100% và giới hạn vật phẩm gốc giữ nguyên. Các bonus khác có thể tiếp tục nhân/cộng theo game. Vàng nguyên có thể chênh do rounding. Không thay đổi Stability, similar-item piles, HP/damage hay density.

## Kiểm tra

- Build Release sạch, 0 warnings/errors.
- Managed tests gồm bất biến tuyến tính, các ngưỡng cũ, gold no-nerf + giữ bonus /40, actual-asset Amber, WW RNG decision fixtures/cap và so sánh từng C nguyên1..10.000 với công thức cũ. Kết quả cuối xem `validation/managed-dev31-linear-corruption.json`.
- Compiled audit: giữ disabled-world policy, không native RNG/combat hooks mới; bao phủ các consumer có inlining và chỉ đọc cấu hình Amber. Kết quả cuối xem `validation/audit-dev31-linear-corruption.json`.
- Native disassembly xác nhận `RollLegendaryProperties` gọi `RollLegendaryPotential`/`RollWeaversWill` và standard Amber gọi unrounded helper. Các công thức phụ thuộc cấu trúc binary game đã đọc. Chưa nghiệm thu native gameplay, lượng loot thực tế hay UI trong game; không báo managed/audit pass là native pass.
- Gói phát hành dùng root `Mods/LastEpochLAN.dll`, `UserLibs/LastEpochLAN.Core.dll` và `latest.json`. Không thay riêng một DLL từ bản cũ.
