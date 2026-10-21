# dev.33 — Tỷ lệ Woven Potential

Protocol **40**. Cập nhật cả hai DLL trên mỗi máy.

| LP ban đầu | Kết quả mỗi lượt |
| --- | --- |
| 0 | Không được phép đưa vào máy |
| 1 | 50% lên 2 LP; 50% về 0 LP |
| 2 | 25% lên 3 LP; 75% về 1 LP |
| 3 | 25% lên 4 LP; 75% về 2 LP |
| 4 | 25% giữ 4 LP; 75% về 3 LP |

- Theo yêu cầu sửa lại, không mở điều kiện cho đồ 0 LP. Nếu 1 LP roll thất bại về 0, món đó không thể tiếp tục dùng máy. Bonus tối thiểu 1 LP cho đồ rơi không sửa kết quả này.
- LP tối đa 4. Không thêm LP thứ 5. Giữ nguyên điều kiện sử dụng, tiêu hao lượt và quy trình cập nhật vật phẩm của game.
- Tỷ lệ cố định theo LP đầu vào; không nhân Corruption hoặc Circle of Fortune vào xác suất Woven.
- Các nhánh còn lại được điền như bảng: 2 LP thất bại về 1; 4 LP có 25% giữ nguyên. Bảng này được hiển thị trong changelog của mod.

## Kiểm chứng

Build Release 0 warnings/errors. **894 managed/TCP checks PASS**, gồm kiểm đủ 100 mốc RNG cho mỗi mức 1–4 LP và các điểm biên; **41 compiled audit checks PASS**. 132 lớp Harmony: 65 chọn, 67 bỏ. Updater fixture 15 checks PASS. Bằng chứng và SHA256 lưu trong `validation/release-dev33-*`.

Đã mở rộng bộ kiểm thử native cô lập sang 4 LP nhưng chưa chạy gameplay native trong lượt này. Không mở/đóng game, không cài Steam. Phòng Internet vẫn giữ F2 Shared Stash + Loot Filters; giữ các sửa Assist/DPS và Corruption trước đó. Encounter tiếp tục tạm dừng.

Thoát game khi thuận tiện rồi chạy `update.bat`, hoặc chép cả `Mods/LastEpochLAN.dll` và `UserLibs/LastEpochLAN.Core.dll` từ ZIP vào thư mục game.
