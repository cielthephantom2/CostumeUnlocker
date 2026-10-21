# dev.30 — Glyph crafting và Combat Assist radius30

Protocol **40**, giữ phòng Host/Join **public IPv4 + port qua Internet**, chỉ F2 Shared Stash + Loot Filter. Không đưa nhánh Encounter đang tạm dừng vào release. Nên cập nhật cả Host và Client để mỗi máy có cùng tùy chỉnh cá nhân.

## Glyph

- **Glyph of Hope: 33%** cơ hội không mất Forging Potential từ hiệu ứng Hope, thay25%. Chỉ sửa một lần roll của glyph Hope thật trong thao tác Forge của nhân vật local, trên đúng RNG crafting. Hook RNG chỉ gắn trong thời gian Forge và gỡ ở finalizer; không giữ hook Roll trên đường chơi map. Không thay RNG của loot/combat/critical hoặc tự cho miễn FP khi không đặt Hope.
- **Glyph of Despair:**

| Tier affix đang seal | Tỷ lệ roll mới |
|---|---:|
| T1 | 100% |
| T2 | 70% |
| T3 | 50% |
| T4 | 33% |

Bảng giống nhau cho Rare và Exalted có T6/T7, không phụ thuộc rarity/số affix như công thức native. Không mở seal affix T5–T7. Native crafting vẫn kiểm tra eligibility/affix đã seal, xử lý critical craft, tiêu hao glyph/FP, thay đổi item và lưu nhân vật; không dùng đường craft riêng. Các phần trăm là tỷ lệ roll tương ứng, không cộng gộp mọi cơ chế miễn FP hay thay toàn bộ kết quả Forge.

- Native glyph description builder hiển thị tỷ lệ custom cho Hope/Despair; các glyph khác giữ mô tả cũ. Changelog F4 bổ sung tỷ lệ. Không thêm vật phẩm "Greater Hope"; hằng số GreaterHope40% trong baseline chưa xác minh là vật phẩm dùng được.
- Chỉ áp dụng Play Offline (kể cả phòng Internet của mod). File GameAssembly và save item format không bị chỉnh sửa.

## Combat Assist

- Radius tìm mục tiêu = lớn nhất trong các skill bật cần target/Enemy/Rare/Boss, tối đa **30**. Potion hoặc self-buff không cần quái bỏ quét.
- Slider, ô nhập số, vòng preview, profile normalization và cast guard cùng cap30; profile cũ60 được hạ xuống30 khi load.
- Lọc khoảng cách bình phương trước health/alignment/scene/rarity; chỉ sqrt hai kết quả cuối. Mỗi skill giữ range riêng. Target50ms/cast25ms/Hold0 không bị giảm nhịp.
- Vẫn đọc vị trí danh sách ứng viên để biết ai trong vòng; không tuyên bố có spatial index hoặc benchmark FPS native.

## Kiểm chứng/phạm vi

- Release build **0 warnings / 0 errors**.
- **831 managed/TCP checks PASS**: tier mapping, unsupported-tier guard, glyph descriptions, Hope33%, đúng RNG, một lần/Forge, nested/exception/thread isolation, radius30, F2 custody/filter transport và các regression trước.
- **26 compiled audit checks PASS**, 117 lớp Harmony tự chọn: **50 chọn / 67 bỏ**. Xác minh glyph hooks được chọn, Forge local/offline + finalizer, RNG chỉ sửa float probability và gỡ hook sau lease cuối, Despair chỉ sửa getter thay vì tự seal/upgrade, scope thread-local; shared world/combat/VFX/map/Encounter vẫn bị chặn, Disintegrate/Arcane overrides vẫn vắng mặt.
- Updater xác minh SHA256 cặp DLL, backup và từ chối ghi khi game chạy. Proof release tại `validation/release-dev30-*`.
- Chưa chạy native gameplay/UI hoặc đo FPS cho bản này; managed/audit không thay thế nghiệm thu craft trong game. Không khởi chạy engine QA thứ hai, không đóng game hoặc cài Steam trong lượt build/upload.

Để cập nhật, thoát game khi thuận tiện rồi chạy `update.bat` trong thư mục game. Cặp bắt buộc: `Mods/LastEpochLAN.dll` và `UserLibs/LastEpochLAN.Core.dll` từ cùng manifest.
