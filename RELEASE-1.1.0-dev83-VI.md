# TM Last Epoch Co-op Mod 1.1.0-dev.83

Tăng gấp đôi chỉ số hai passive Sorcerer:

| Node | Trước, mỗi điểm | Sau, mỗi điểm |
|---|---|---|
| Mana Bulwark | +4 mana; 2% sát thương chuyển sang mana trước máu | **+8 mana; 4% sát thương chuyển sang mana trước máu** |
| Dragon Breath | +7% increased Elemental Damage; +4% diện tích skill | **+14% increased Elemental Damage; +8% diện tích skill** |

Giữ nguyên giới hạn 10 điểm, điều kiện mở node và số điểm đã phân bổ/lưu nhân vật. Cập nhật chỉ số tooltip ở cả hai đường giao diện native. Các node khác giữ nguyên.

40 kiểm tra native đạt: từng hiệu ứng ở 1/10 điểm, kết hợp cả hai node, tính lại nhiều lần không cộng dồn, respec về 0, giữ điểm gốc và hai đường tooltip. Production Release build sạch; 227 compiled audit đạt. Kiểm tra trực tiếp các stat row native, không phải benchmark damage thực chiến.

Giữ các thay đổi trước, gồm bước native hoàn tất hàng đợi spawn khi tải map, mật độ tối đa ×5 và bonus Amber/Bones. Protocol 44. Đóng game trước khi chạy `update.bat` hoặc thay đủ hai DLL; các máy cùng phòng nên dùng cùng phiên bản. Gói không chứa mã nguồn, profiler hoặc công cụ QA.
