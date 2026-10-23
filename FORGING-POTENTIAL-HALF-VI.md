# Giảm chi phí Forging Potential 50%

Phát hành trong 1.1.0-dev.60 / protocol 44.

- Giảm hai đầu khoảng chi phí FP trước native RNG/lucky roll: số dương chia 2, làm tròn xuống, tối thiểu 1. Ví dụ Rune of Shaping 1–25 thành 1–12; 4–8 thành 2–4.
- Forge hiển thị khoảng đã giảm. Không sửa getter chung vì một số công thức bị native inline và getter còn dùng cho Weaver's Will/Touch.
- Native vẫn quyết định Hope, critical/free craft, giới hạn FP còn lại và kết quả craft. Không hoàn lại FP sau craft.
- Rune of Creation vẫn xóa toàn bộ FP của cả hai món, đúng yêu cầu. Không patch DuplicateItem hoặc CreateGameplayDuplicate.
- Không giảm Weaver's Will/Touch. Chỉ áp dụng offline.

Release build 0 warnings/errors. 278 kiểm tra actual-source, 185 compiled/native audit và 26 kiểm tra IL2CPP đều PASS. Native kiểm tra các đường Rune và affix/Glyph, RNG/lucky roll, miễn phí và giới hạn FP; xác nhận DuplicateItem không có Harmony patch. Test trên item QA tạm, không dùng inventory nhân vật thật. Phần hiển thị kiểm tra qua fixture; chưa chụp Forge render.

Đã backup và copy đúng cặp DLL đã chạy native vào Steam khi game đóng, SHA256 khớp. Đường dẫn bản dự phòng, candidate và hash nằm trong `validation/forging-half-steam.json`.
