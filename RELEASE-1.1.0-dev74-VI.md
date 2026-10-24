# TM Last Epoch Co-op Mod 1.1.0-dev.74

Protocol44. Đóng Last Epoch và cập nhật cả hai DLL trước khi chơi; hai máy trong phòng cùng cập nhật.

## Grip of Chaos

- Tăng xác suất từ10% lên20% mỗi điểm. Tính lại từ số điểm đã cộng khi native Maelstrom tree cập nhật, không nhân dồn sau mỗi lần refresh.
- Giữ proc khi cast Maelstrom, bao gồm cast được kích hoạt từ skill khác. Tornado do Grip vẫn là Indirect và tiêu50% chi phí mana Tornado.
- Bỏ “directly cast” trong mô tả. Ghi đúng20% mỗi điểm/50% mana cost; sửa cả description/nodeDescription trên node, mô tả localization và Alt Text để giao diện không lấy lại câu cũ.
- Không thay giới hạn hoặc UI specialize.

## Các bản sửa local được đưa lên cùng đợt

- Rampage đổi hướng theo chuột và giữ tốc độ Dash native khi retarget; đặt lại segment mỗi bước mô phỏng từ vị trí authoritative, tránh tăng tốc/nhảy vị trí khi đổi đích.
- Excited Bolts: cap600% more damage, dùng current mana trước khi tiêu hao; chi phí1% current mana, cap20 thay vì10. Đồng bộ tooltip. +600% more là tổng damage x7.
- Rule of the Simoon: giữ stationary cho Tornado do item proc, bỏ forced Fire; giữ Lightning Shred, increased Lightning Damage, Lightning Resistance và2 proc/giây.
- Primal Shifter/Shapeshifter: cap1 điểm, buff không hết hạn theo thời gian; hoàn điểm đã cộng dư. Tiger Spirit yêu cầu1 điểm; nhánh hồi máu cũng hạ yêu cầu xuống1 để không bị khóa. Native buff vẫn còn khi về dạng người; respec node gỡ buff. Icon không đếm ngược.
- Intimidating Cry: bỏ Roar sau Rampage. Khi Rampage đang chạy, tự cast Earthquake hiện tại bằng Indirect mỗi1 giây; tiêu100% chi phí mana sau modifier dưới dạng Rage, thiếu tài nguyên thì bỏ qua. Dừng chạy/bỏ node thì ngừng proc. Hai dòng tooltip dùng icon Earthquake.

## Kiểm tra và phạm vi

- Bản Release dev.74 build0 warning/error,217 compiled audits đạt. Audit kiểm tra hook chance20%, wording và raw node description, đồng thời xác nhận không có bộ đo/helper/probe/smoke tạm trong DLL phát hành.
- Các thay đổi gameplay local trước khi nâng version đã qua74 kiểm tra native về Rampage/Excited/Simoon và26 kiểm tra native Druid trên profile QA riêng.
- Storm Bolt thật: damage trước phòng thủ từ24,15 lên96,6 ở1000 mana (x4),169,05 ở2000/3000 mana (x7); chi phí thực10/20/20.
- Druid: allocation8→1 hoàn7 điểm; buff còn sau native update120 giây, vẫn còn khi về người và mất khi respec. Rampage liên tục3,5 giây tạo3 Earthquake Indirect, mỗi proc tiêu67 trong cấu hình thử50+17;67 không phải chi phí hard-code. Không có Roar khi dừng; thiếu Rage/idle/không cộng node không cast.
- QA giữ cờ nhả phím và tắt kết thúc Dash ở biên NavMesh chỉ trong helper riêng để duy trì Dash trong MonolithHub. Code sản phẩm giữ collision/điều hướng native. Không thử combat dài bằng chuột thủ công.
- Lần đổi riêng Grip20%/sửa câu chữ và icon Earthquake được build/audit; chưa chạy lại native/hover giao diện do game Steam đang mở. Không khẳng định đã kiểm tra hình ảnh tooltip của dev.74 trong phiên đang chơi.

Gói ZIP chỉ có cặp DLL, manifest, updater, README và release notes. Không kèm helper QA, bộ đo, log hoặc profile. Gói public và updater được tải về kiểm tra SHA256 sau khi phát hành.
