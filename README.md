# Costume Unlocker

Mod MelonLoader cho Last Epoch. Chọn cosmetic bằng Appearance UI gốc; **F4** mở bảng kết nối Internet và các tùy chọn. Hai người vẫn cần chơi online cùng party/map để chia sẻ ngoại hình.

## Cài lần đầu

1. Cài MelonLoader **0.7.3 x64** vào game.
2. Tải [Update-CostumeUnlocker.bat](https://github.com/cielthephantom2/CostumeUnlocker/raw/refs/heads/main/Update-CostumeUnlocker.bat), đặt cạnh `Last Epoch.exe`.
3. Thoát game bằng menu, chạy batch. Nó tự tải bản mới nhất vào `Mods/CostumeUnlocker-<version>.dll`.

Nếu batch nằm ngoài thư mục game, nó sẽ hỏi đường dẫn game. Cũng có thể chạy `Update-CostumeUnlocker.bat "D:\SteamLibrary\steamapps\common\Last Epoch"`.

## Cập nhật về sau

Chạy lại chính file batch đó. Nó đọc `latest.json`, kiểm tra SHA-256 DLL đang cài, chỉ tải/cài khi cần. DLL tải về được kiểm tra kích thước, SHA-256, tên assembly và phiên bản trước khi thay file. DLL cũ được chuyển sang `UserData/CostumeUnlocker/backups`, tránh nạp trùng. Nếu lỗi cài đặt, updater khôi phục bản cũ.

Không cần app ngoài, không cần GitHub CLI hay đăng nhập GitHub để cập nhật. Batch không tự đóng hoặc khởi chạy game. Nếu cần cập nhật lúc game đang mở, nó báo bạn thoát game rồi chạy lại. Preset, IP/port, mật khẩu và chứng chỉ host được giữ nguyên.

## Kết nối cosmetic qua Internet

Host chuyển tiếp **TCP 7777** trên router tới PC host, rồi bấm **Host** trên F4. Guest nhập public IPv4 của host, cổng, cùng mật khẩu phòng (16–128 ký tự) và fingerprint do host gửi, rồi **Join**. Bấm **Save settings** để lưu. Kết nối dùng TLS, xác thực chứng chỉ host bằng fingerprint và mật khẩu phòng.

## Bản hiện tại: 0.4.2

Bản 0.4.2 bổ sung đường render VFX trực tiếp cho skill, bao gồm các đường hiệu ứng của Smite. Kiểm thử riêng Red/Cosmic Smite: 28 kiểm tra renderer và 48 kiểm tra prefab. Đã qua kiểm tra hồi quy native Fireball/Lightning Blast/Glacier và 50 kiểm tra transport/QOL. Combat online thực tế vẫn cần xác nhận khi chơi.

Repo chỉ chứa DLL mod, manifest và updater. Không chứa game, MelonLoader runtime, save, cấu hình hay khóa riêng của host. Khi copy DLL thủ công, chỉ giữ **một phiên bản Costume Unlocker** trong `Mods`.

