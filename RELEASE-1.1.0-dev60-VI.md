# TM Last Epoch Co-op Mod 1.1.0-dev.60

Protocol 44. Cập nhật cả cặp DLL khi game đã đóng.

- Chi phí Forging Potential của Rune và nâng affix/Glyph giảm 50% trước native RNG/lucky roll. Chia số nguyên, làm tròn xuống, tối thiểu 1 FP khi chi phí gốc dương. Rune of Shaping: 1–25 → 1–12 FP; 4–8 → 2–4 FP.
- Forge hiển thị khoảng chi phí đã giảm. Craft miễn phí, Hope và critical preservation vẫn theo native.
- Rune of Creation vẫn xóa toàn bộ FP của cả hai món. Không thay đổi cơ chế nhân bản.
- Weaver’s Will/Touch giữ nguyên. Chỉ áp dụng offline.
- Giữ bản sửa Spirit Xylem: mỗi Range nằm ngay dưới hiệu ứng tương ứng.

## Kiểm tra

Release build 0 warnings/errors; 278 actual-source checks, 185 compiled/native audit. Native IL2CPP QA kiểm tra 26 assertions trên các đường Rune/affix/Glyph, RNG/lucky roll, miễn phí, giới hạn FP và xác nhận DuplicateItem không bị patch. Test trên item tạm trong runtime/profile QA riêng. Phần hiển thị kiểm tra qua fixture; chưa chụp Forge render.

## Cài đặt

Thoát game, chạy update.bat hoặc copy Mods/LastEpochLAN.dll và UserLibs/LastEpochLAN.Core.dll từ ZIP. Updater kiểm tra SHA256 và tạo backup. ZIP không kèm helper QA. Cập nhật cả hai máy trước khi kết nối.
