# Dev.52 — Archmage, UI Shortcut và sửa Meteor/Arcane Ascendance

Đã sửa gameplay và text của **Archmage Sorcerer**, tree `mg-1`, node30 (`Sorc Base Spell Damage`). Giữ toàn bộ thay đổi dev51: Meteor, Arcane Ascendance và UI Shortcut.

Khi phân bổ đủ5điểm:

| Mana tối đa | Spell damage | Xác suất proc | Phần mana cost được hoàn khi proc |
|---|---:|---:|---:|
| Dưới300 | +5 | 35% | 20% |
| 300–999 | +10 | 70% | 40% |
| Từ1000 | +15 | 100% | 60% |

Điểm chưa đầy vẫn theo tỉ lệ gốc: +1/+2/+3 spell damage và7%/14%/21% xác suất mỗi điểm, cap100%. Lượng hoàn20%/40%/60% dựa trên mana tối đa khi cast, không dựa trên mana còn lại. Chỉ có proc khi đã đầu tư vào hiệu ứng Archmage và thực trả chi phí mana.

## Gameplay

Native MageTree đã cấp +1spell damage và7% proc mỗi điểm. CharacterMutator đã thêm spell damage theo ngưỡng300/1000 và nhân xác suất2/3, nhưng hàm OnAbilityUse luôn yêu cầu hoàn `manaCost*0.2`. Vì vậy giữ đường damage gốc, giữ native RNG và thời điểm proc, giới hạn xác suất tối đa100%, rồi sửa đối số hoàn thành20/40/60% trong đúng scope cast.

Scope khớp mana object, cast ID, giá trị20% native và chỉ dùng một lần; có finalizer khôi phục khi lỗi hoặc cast lồng nhau. Không hoàn mana bằng một lần gainMana riêng. Hàm RefundManaFromMostRecentCast gốc vẫn giới hạn theo AvailableManaToRefund, mana tối đa và phát event gốc. Meteor/manual refund ngoài scope, nhân vật khác và online giữ nguyên.

## Text

- Tooltip cũ và V2 hiển thị spell damage/xác suất theo mana tối đa hiện tại; cap100% và giữ suffix “per point”/TMP tags.
- Dòng refund hiển thị đúng20%/40%/60%.
- Description ghi rõ cả3mức với tổng tại5điểm, điều kiện max mana và cách cộng theo điểm.
- Chỉ sửa node30 trong treeMage; không sửa Archmage của Skeletal Mage hoặc node lân cận. Text tùy chỉnh tiếng Anh, phù hợp UI gốc đang dùng.

## Kiểm tra và cài

Kết quả trong `validation/dev52-*`: fixture source thật kiểm ngưỡng299/300/999/1000, điểm1–5, cast lồng, ID thay đổi, exception cleanup, refund ngoài scope, online và tooltip. Đối chiếu native GameAssembly SHA256 `323E9183F3BEE600B6C9E1C469218B9DE926E9044F2CA3D44FAE8A0EC9A91464`, hằng số7%/20% và multiplier2/3; xác nhận cả hai renderer gọi hàm localization được hook.

Gói phát hành `TM-Coop-1.1.0-dev.52.zip`; Core dùng lại đúng binary47/protocol44. Thoát game rồi chạy `update.bat` từ thư mục game; updater kiểm checksum cặpDLL và giữ backup. Cập nhật cả haiDLL trên Host và mọi Client. Không chứa game/runtime/interop, dữ liệu nhân vật hoặc Shared Stash. Bản dev52 đã được cài vào Steam khi game đóng và kiểm checksum; không tự mở game để nghiệm thu. Build/fixture/native audit không thay thế nghiệm thu chiến đấu/render trong engine; runtime report có `archmage.Casts`, `Refunds`, `LastMaximumMana`, `LastRequestedRefund` để kiểm tra lần chơi tiếp theo.

## UI Shortcut và sửa lỗi dev51 đi kèm

- F4 → UI Shortcut: Mesembria Telescope; Respecialize Mastery; Respecialize Mastery Points; TP Champions' Gate; Personal Stash gốc. Mỗi nút có hotkey riêng, mặc định trống. Ctrl/Alt/Shift được hỗ trợ; Esc hủy bind, Backspace/Delete xóa; F1/F2/F4 dành cho UI hiện có. Panel, xác nhận và phí vẫn do game xử lý.
- Extinction: áp dụng33% Meteor lớn mỗi điểm ngay trước khi tạo Meteor; Heat Flux:2 Frost Claw mỗi điểm,100% chi phí mana Frost Claw. Lấy trực tiếp điểm đã phân bổ, cast lặp không nhân tiếp; respec về0 xóa hiệu ứng ở cast tiếp theo.
- Arcane Ascendance dùng bản sao buff ID57 riêng cho nhân vật local với roots=false; giữ mana drain và các hiệu ứng khác. Không xóa bộ đếm root chung; tooltip tiếng Anh bỏ giới hạn di chuyển.
- Giữ việc gỡ Delete Tab của dev50. Không khẳng định lỗi cắt chữ Shared Stash đã hết; báo cáo Corruption100+60→400 vẫn hoãn.
