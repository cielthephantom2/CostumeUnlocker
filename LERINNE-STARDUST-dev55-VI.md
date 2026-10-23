# Lerinne và Meteor Stardust — dev.55

Xác minh mới:79 kiểm tra engine IL2CPP cho Spirit Xylem/Stardust đã đạt, gồm tooltip parser, equip/stat/skill max points, All Attributes, không stack và more-damage native. [Báo cáo chi tiết](MOD-FEATURES-AND-CODE-AUDIT-dev55-VI.md). Chưa đo hit lên monster hoặc chụp UI render.

Giữ version 1.1.0-dev.55 / protocol 44.

- Respecialize Class tại Lerinne: nhận diện NPC ở cả speaker/listener, hoặc đúng hai dòng mastery gốc khi game dùng tên nội bộ khác. Hỗ trợ cả TextMeshProDialogueUI và UnityUIDialogueUI. Giữ trạng thái menu khi ShowResponses gọi HideResponses bên trong. Các nút thêm dùng vị trí tự động của game.
- Chọn Mage/Rogue/Primalist/Sentinel/Acolyte → xác nhận → về character selection. Giữ level, XP, đồ, ID và tiến trình; reset mastery, passive, specialization và thanh skill. Điểm passive được hoàn. Giữ nguyên cơ chế sao lưu Saves vào UserData/LastEpochLAN/backups/class-respec-* trước khi thay dữ liệu và đợi native save hoàn tất.
- Xóa hoàn toàn setting Ignore Vilatria Meteor visuals cùng ba hook VFX. Không còn ghi đè VFX của món đồ. JSON cũ có khóa này sẽ được bỏ qua khi đọc.
- Stardust (Meteor me27, node10, internal Meteor Non Fire Damage): tối đa 1 điểm. Bỏ cặp native modifier tăng damage chung và triệt tiêu phần fire; thay bằng một stat MORE Damage mọi hệ, giá trị Maximum Mana × 0.001. 500 max mana → 50% more (x1.5); 1000 → 100% more (x2); 2000 → 200% more (x3). Nhân độc lập với các modifier khác. Mana hiện tại không tham gia; không có ngưỡng tối thiểu hoặc làm tròn từng nhóm10.
- Đọc max mana hiện hành khi native yêu cầu temp stats. Không ghi thêm vào cache stat dùng lại của game, không cộng dồn qua lần cast. Cây native bỏ qua Stardust tạm khi tạo modifier cũ rồi khôi phục list, kể cả lỗi. Nếu cây đã tải trước khi local actor sẵn sàng, lần đọc temp stats đầu tiên sẽ dựng lại đúng cây.
- Phân bổ cũ 2–4 điểm được hạ xuống1; số điểm chưa dùng của skill được game tính từ tổng phân bổ nên điểm dư được hoàn. Cả global maxPoints, maxPoints UI, mô tả, alt text và dòng stat được đồng bộ.

## Kiểm tra

Build Release: 0 warnings / 0 errors. 47 actual-source class/regen checks, 106 Meteor/Arcane checks, 27 Spirit Xylem checks, 186 compiled/native audit checks (211 patch classes). Các fixture mô phỏng lớp game; chưa xác nhận gameplay/render trực tiếp.

Native evidence: research/dev55-stardust-dialogue-native.txt. Localized Stardust maps to Skill_me27_10_Name. Native Meteor Non Fire Damage hash 0x89DADE83 creates an untagged Damage MORE and reciprocal Fire MORE; both are excluded by the tree scope.

## Vào game kiểm tra

1. Nói chuyện Lerinne: phải thấy Respecialize Class cạnh các lựa chọn mastery. Mở đủ5 class; Back phải trả về menu cũ. Chọn class khác rồi xác nhận, vào lại nhân vật và kiểm tra level/đồ/quest/Monolith, mastery/passive/skill reset và thư mục backup.
2. F4 không còn Ignore Vilatria Meteor visuals. Hiệu ứng Vilatria do game gốc xử lý.
3. Mở cây Meteor: Stardust max1, text1% more mỗi10 max mana, không còn bonus chỉ ngoài hệ lửa. Cây cũ4 điểm còn1 và hoàn3 điểm.
4. So sánh damage trước/sau Stardust ở cùng build: 1000 max mana kỳ vọng x2; thay đổi mana hiện còn không đổi hệ số. Thay max mana thì hệ số cập nhật. Bỏ điểm Stardust phải mất bonus; cast nhiều lần không tăng dần.
