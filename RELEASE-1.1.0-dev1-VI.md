# Bản 1.1.0-dev.1 — 20/10/2026

Phát hành lên `main` theo yêu cầu để Host và Client cập nhật qua `update.bat`. Protocol 32: cả hai máy phải cập nhật cùng phiên bản, không kết nối với dev.27/protocol31.

## Thay đổi

- Gỡ cơ chế encounter co-op cũ và đường phát lại callback/map graph/object. Chưa nối hệ thống encounter mới vào gameplay: tương tác và thưởng encounter/map chung chưa đồng bộ. Nemesis, Omen Window, Blood Crystal, Exiled Mage và Rift Beast chưa hỗ trợ chơi chung trong bản này.
- Giữ phần NPC movement/animation/VFX, minion, combat và party/Monolith travel. Những phần này vẫn cần thử lại trên hai máy sau thay đổi.
- Echo of a World (Shade of Orobyss): nhân x5 phần Corruption tăng thêm, gồm Gaze và bonus bắt kịp. Ví dụ từ 100, thưởng gốc +10 thì kết quả 150. Giữ giới hạn timeline; không nhân Corruption đã tích lũy hoặc lượt giảm Corruption.
- Dùng một postfix ở hàm native `EchoWeb.NewCorruptionForShadeEchoOfTier`: cả xem trước và hoàn thành native đều dùng hàm này. Không tự ghi web/save hoặc chạy thêm lệnh hoàn thành. Các thành phần thưởng hiển thị được điều chỉnh cùng kết quả, có chặn tràn số.
- Changelog trong game bổ sung bonus Corruption và ghi rõ tình trạng encounter.

## Kiểm nghiệm

- Build Release: 0 warning, 0 error.
- 505 kiểm tra managed đạt; 5 bài mới kiểm tra phần tăng thêm, Gaze/catch-up, giảm/không tăng, giới hạn timeline và tràn số.
- 7 kiểm tra compiled audit đạt, khảo sát 104 lớp Harmony. Không có lại adapter/hook phát lại encounter cũ. Bonus CoF Rank 7 offline giữ riêng.
- Native assets/adapter QA đạt trên đúng cặp DLL phát hành trong sandbox riêng. Hàm Corruption native đã patch trả 100 → 130 khi thưởng gốc +6; với 2 Gaze trả 100 → 250 (Gaze +120); gọi xem trước lặp vẫn 130. Timeline có trần 50 trả 49 → 50.
- 15 bài updater fixture đạt: cài đúng cặp DLL, kiểm SHA/size, pin một commit, không dùng REST quota, chặn file hỏng và game đang mở. Không khởi chạy game thật. Sau khi đẩy main, tải trực tiếp qua updater từ GitHub và kiểm SHA của cả hai DLL cũng đạt: tổng 17 bài fixture/live.

Chưa kiểm nghiệm giết Shade và lưu tiến trình trong gameplay thực tế, toàn bộ encounter solo hoặc co-op hai máy. Full native campaign QA ở mốc trước vẫn chưa đạt do 5 lỗi khởi tạo sandbox; không coi native calculator/adapter smoke là bằng chứng toàn bộ gameplay đã ổn.

## Gói cập nhật

- Mod SHA-256: `0BFD05598AB2CA340931B13080BE02B10621B3910B6E8432CC3EB9FFA1BB7F5F` (729088 byte).
- Core SHA-256: `9DAEC339C53D8DEB5D54598ECA0ED59D327680788E018547F51F144AC954FADF` (275456 byte).

`latest.json` ghim cặp DLL này. Chạy `update.bat` khi game đã đóng trên cả hai máy. Không thay DLL đang cài hoặc save thật của người dùng trong quá trình kiểm thử.
