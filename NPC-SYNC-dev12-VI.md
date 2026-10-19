# NPC Client — vòng sửa đầu tiên, 1.0.9-dev.12 / protocol 28

## Phạm vi và kết luận

Đã thay cách áp phần trình bày NPC trên Guest. Đây là candidate cần kiểm thử
gameplay hai máy, chưa phải bằng chứng đã hết khựng hoặc đạt độ mượt như solo.
Không thay quyền quyết định damage, native AI, quest, loot hay encounter.

## Các thay đổi

- Pose đã có nội suy trước đây; lỗi không được quy chung thành thiếu Lerp.
  Animation/motion, control VFX, ailment VFX và projectile snapshot presentation
  nay được lấy theo cùng host-time cursor với pose, thay vì áp ngay tại lúc nhận
  packet trong khi pose đang trễ 100 ms. Baseline của replica mới được trình bày
  ngay để tránh chờ trong trạng thái animation chưa thiết lập.
- Buffer riêng từng NPC giới hạn tám mẫu. Snapshot cũ/duplicate bị từ chối.
  Sau render hitch, phần hình ảnh cũ được coalesce về trạng thái tới hạn mới
  nhất; không chạy dồn nhiều attack đã cũ trong một frame. Đây chỉ là visual
  state, không bỏ damage/reward hay lệnh gameplay đáng tin cậy.
- Disabled ComplexAnimationManager được tick từ LateUpdate mỗi frame, tối đa
  một lần/frame. Trước đây tick bù ở Apply nhận snapshot dùng Time.deltaTime
  của một frame, nên timer native có thể tiến chậm hơn thời gian thực. Driver
  đang enabled vẫn do game tick, không được tick bù lần hai. Native method đã
  được đọc disassembly để xác nhận nó cập nhật timer temporary-idle; chưa có
  số đo tỷ lệ NPC thực tế dùng nhánh disabled trong phiên của người dùng.
- Replay phân biệt InstantAttacking với Attacking. Snapshot tương ứng không
  gọi lại cùng attack; vẫn đặt move-speed và temporary-idle parameters. Animator
  được bật khi controller sẵn sàng và không dùng root motion cạnh tranh pose.
- Với budget hai native spawn/Apply hiện có, ưu tiên NPC gần vị trí Guest.
  Boss ưu tiên khi cùng khoảng cách; ID ổn định phân định tie. Không cắt world,
  không loại quái xa khỏi authority và không thay eligibility nhận reward.
- HP, ward, modifier gameplay, native death/reward vẫn áp qua đường cũ khi có
  xác nhận; không đợi visual buffer. Buffer/driver được dọn khi death/despawn,
  reset/đổi scene. Thêm enemyPresentation trong runtime-report để kiểm tra.

## Kiểm nghiệm đã thực hiện

- Release Mod/Core: 0 cảnh báo, 0 lỗi.
- 437 managed/network checks qua, gồm 12 check mới: timeline, duplicate/stale,
  buffer hữu hạn/catch-up, chuyển scene, ưu tiên admission của 2.400 identity,
  đổi vị trí Guest và baseline/delta TCP thật.
- 9 check source-linked animation adapter qua với recording API stubs: cadence
  60 frame/giây, tránh double tick, tránh double attack và thoát về movement.
  Các stubs không thay thế Unity/IL2CPP và không xác nhận chất lượng hình ảnh.
- Chứng cứ local: research/selftest-npc-presentation-dev12.json,
  research/npc-animation-driver-dev12.json, research/npc-animation-update-dev12.txt.
- Không chạy native QA vì game người dùng đang mở và script QA yêu cầu đóng
  game trước. Không đóng/mở game, không cài DLL vào game hay sửa save/settings.

## Những phần chưa được nghiệm thu

- Chưa có gameplay hai PC, frame-time/FPS hay video trước/sau của đúng build này.
- Không có đường reliable NPC action event mới; attack ngắn hoàn toàn nằm giữa
  hai lần Host capture vẫn có thể thiếu. Render hitch có thể bỏ qua visual state
  cũ. Cần trace trước khi mở rộng action transport.
- Chưa thêm dự đoán hit flash/âm thanh hay combat-result events. Phản hồi trúng
  đòn local và số damage đầy đủ vẫn cần kiểm tra/sửa tiếp; không tạo stun/HP giả.
- DPS Guest, Lightning Blast chain, Host thực thi build/skill đầy đủ cho Guest,
  shared encounter/reward parity còn mở. Không tuyên bố hoàn tất bước combat.
- Host vẫn capture toàn bộ quái ở 10 Hz; spawn budget không tăng. Density x10
  và chi phí AI/render game gốc vẫn có thể gây frame hitch.

## Kiểm thử gameplay tiếp theo

Cập nhật đủ hai DLL trên cả hai máy bằng update.bat khi đã thoát game. So sánh
solo, Host và Guest ở cùng map/mật độ: đứng yên, đi/quay liên tục, kéo một nhóm
quái rồi quan sát attack, trúng đòn, stun/freeze và death; tiếp theo map đông,
đổi map và reconnect. Cần runtime-report của cả hai máy, đặc biệt frame timing,
enemyPresentation, enemyAnimationError và MissingAlive/SpawnFailures. Chỉ chốt
độ mượt khi quan sát/đo gameplay thật, không dựa trên số check đã qua.
