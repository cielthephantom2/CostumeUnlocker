# dev.32 — Combat Assist/DPS và Bloodrage UI

Protocol **40**, phòng public-IP Internet giữ **F2 Shared Stash + Loot Filters**. Cập nhật cả hai DLL trên mỗi máy. Core có thay đổi so với dev31; không thay riêng Mod DLL.

## Combat Assist bị pause sau bật/tắt DPS

- Sửa chuỗi F4 -> Mod Settings -> bật/tắt DPS -> đóng F4: cờ trang Settings được nhớ nhưng UI đã ẩn; bản cũ vẫn coi cờ đó là panel mở và pause Assist vô hạn.
- Assist nay chỉ xét root F4 và F2 đang mở. Trang Settings ẩn không còn chặn; bật/tắt DPS không thay Master switch hoặc dữ liệu DPS để chữa lỗi.
- F1 hiển thị trạng thái lần chạy trước khi mở panel. Runtime report thêm visibility bits để phân biệt trang được chọn và bảng đang hiện; lý do pause và repeat timer từng slot rõ hơn.

## Khả năng phục hồi Assist

- Actor/identity thiếu tạm lúc chuyển map chỉ pause; tự đọc tiếp khi sẵn sàng. Đổi nhân vật vẫn dừng và cần Start, không tự bật lại sau Stop.
- Lỗi đọc native tạm thời retry sau250ms, tối đa3lần; lỗi kéo dài dừng và chỉ rõ F1 để restart. Lỗi trong native cast/potion/channel action **không tự replay**.
- Lỗi render panel không tự gạt Master OFF. F1 đang mở vẫn chặn action; đóng bằng F1 được.
- Trạng thái travel/Monolith đã tắt trong inventory-only room không còn ảnh hưởng Assist.
- Đổi Repeat Interval, nhất là120s->0Hold, xóa deadline cũ ở lần chọn skill kế tiếp. Giữ25ms dispatch guard và timer tính từ native cast confirmation cho cấu hình không đổi.

## Bloodrage UI

- Sửa **10/5 ->10/10** ở Hunt notification và Quest Tracker. Các mốc hiển thị **2/10,4/10,6/10,8/10,10/10**.
- Mẫu số được tính từ số mốc native thực; tử số đã là earned charge nên không nhân lần nữa. Không sửa threshold asset, hunt index, timer, reward balance hoặc save.

## Kiểm chứng/phạm vi

- Build Release **0 warnings/errors**; **892 managed/TCP checks PASS**; **38 compiled audit checks PASS**.132 lớp patch,65 chọn/67 bỏ.
- Kiểm tra interval-edit, cast-confirmation timing, bounded read recovery, không retry action, UI text-only và regression không đọc cờ Settings ẩn để chặn Assist.
- Updater fixture/live và SHA256 của gói/DLL lưu trong `validation/release-dev32-*`.
- Chưa chạy native gameplay/UI/FPS/two-PC acceptance; không mở/đóng game hoặc cài Steam trong lượt phát hành.
- Giữ thay đổi Corruption/Bloodrage/Shade catch-up của dev31. Không sửa tỷ lệ Weaver Imprint, không tự khôi phục Corruption đã lưu, Encounter co-op vẫn tạm dừng.

Thoát game khi thuận tiện rồi chạy `update.bat` trong thư mục game. Cặp bắt buộc cùng manifest: `Mods/LastEpochLAN.dll` + `UserLibs/LastEpochLAN.Core.dll`.
