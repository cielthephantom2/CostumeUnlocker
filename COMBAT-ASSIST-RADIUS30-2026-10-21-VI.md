# Combat Assist — tối ưu radius tối đa 30

Đã được đưa vào release dev30 theo yêu cầu build/upload sau khi bổ sung glyph. Phần candidate cuối tài liệu là checkpoint trước khi xuất bản; xem `RELEASE-1.1.0-dev30-VI.md` và `validation/release-dev30-*` cho bản đóng gói cuối.

Theo yêu cầu sau dev29: bán kính tìm mục tiêu = radius lớn nhất của các skill **đang bật và cần mục tiêu/điều kiện Enemy/Rare/Boss**, tối đa 30. Skill bị tắt hoặc buff bản thân không có điều kiện quái không làm tăng vùng quét; potion-only không quét.

- Profile cũ có radius 31–60 được clamp về 30 khi load/normalize. Slider, ô nhập số, vòng preview, trigger và cast guard dùng chung giới hạn 30.
- Target pass đọc khoảng cách bình phương trước; ngoài vùng thì bỏ qua native health/dead/corpse/alignment/scene/rarity. Chỉ lấy căn bậc hai cho hai kết quả gần nhất cuối lượt. Bỏ đọc rarity khi ứng viên không gần hơn rare/boss tốt nhất đã tìm được.
- Giữ cách đo khoảng cách 3D như trước, cùng đơn vị world với vòng preview. Từng skill vẫn áp dụng radius riêng; scan chung 20 không cho skill radius10 cast vào quái cách15.
- Giữ registry refresh150ms, target pass50ms và cast dispatch25ms/Hold0. Danh sách registry chứa cả đối tượng xa để chúng được xét ngay lượt target pass tiếp theo khi đi vào vùng. Vẫn phải duyệt/đọc vị trí các ứng viên; chưa có spatial index, không tuyên bố loại bỏ hoàn toàn scan toàn map.
- Runtime diagnostics thêm `targetScanRadius`, `targetCandidates`, `targetInRange`; inRange là số visual qua lọc khoảng cách trước lọc sống/chết/phe/scene.

Kiểm chứng: Release build0warning/error, **815 managed/TCP checks PASS**, **20 compiled audit checks PASS**. Chín kiểm tra mới gồm radius tổng hợp, buff Rare/Boss, profile cũ60, biên khoảng cách, dữ liệu sai, giới hạn riêng mỗi skill và potion-only. Bằng chứng `validation/assist-radius30-*`.

Đây là **candidate local chưa commit/push/release/cài Steam**, source public dựa trên dev29/protocol40; metadata version vẫn29, DLL candidate phải phân biệt bằng SHA. `Mods/`, `UserLibs/` và `latest.json` của GitHub release29 giữ nguyên. Build riêng ở `local-releases/CombatAssist-radius30-CANDIDATE/` cùng manifest SHA của cặp DLL. Không đóng/mở game hoặc chạy native QA, chưa đo mức FPS cải thiện. Phòng public IP chỉ F2 + Loot Filter và checkpoint Encounter paused giữ nguyên.
