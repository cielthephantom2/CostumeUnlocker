# Shared Stash dev43 — tên tab bị cắt

Sửa trường hợp Unique chỉ hiện Un, Set hiện Se, Exalted hiện Ex và Shared hiện Sh.
SimpleLayoutGroup gốc tính chiều rộng tab từ RectTransform của nhãn. Sau khi bỏ controller của hòm cá nhân, nhãn trong bản sao còn chiều rộng của mẫu, dù chuỗi tên đã đổi.

Bản này đo tên bằng TMP.GetPreferredValues với font hiện tại, cập nhật chiều rộng nhãn trước khi bố trí tab/category bằng layout gốc. Giữ nguyên chiều cao, icon, padding, khoảng cách, màu và khung gốc; không đổi tên đã lưu hay dữ liệu hòm.

Mod 1.1.0-dev.43; protocol43. Core giữ nguyên bản dev39. Có tất cả sửa đổi dev42 trở về trước. Không thay interop.

Kiểm tra: build 0 warning/error; 18 kiểm tra layout dùng mã nguồn thực với Unity stub (gồm tên ngắn/dài, đổi tên, đổi font metrics và bảo toàn nguồn); 100 kiểm tra DLL/native. Các kiểm tra này chưa xác nhận render trong game. Cần mở F2 và kiểm tra tên, icon, category, đổi tên ngắn/dài và cuộn tab.

Bản cài Steam được sao lưu và đối chiếu SHA256; chi tiết validation/dev43-steam-install.json. Không tự mở game, chưa đăng GitHub.
