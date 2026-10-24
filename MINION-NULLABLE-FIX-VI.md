# Sửa lỗi đỏ khi minion tìm mục tiêu

Chỉ sửa minion, giữ hệ số x5. Không thay đổi host/join hoặc retry kết nối.

Nguyên nhân đã tái hiện bằng engine: `TargetFinder.MaxDistanceFromSummoner` trả về nullable native. IL2CPP box giá trị rỗng thành null, làm getter được sinh tự động ném NullReferenceException; giá trị có dữ liệu được box thành Single nên đọc qua wrapper Nullable<float> cũng sai layout. Fixture cũ dùng struct giả lập nên không phát hiện lỗi này.

Bản sửa giữ hook updateTarget và đọc backing field qua NativeNullable.ReadField<float> đã có trong mod. Scope lưu float gốc, dựng nullable hợp lệ để khôi phục sau mỗi lần tìm mục tiêu. Các điều kiện offline, ownership, stance Aggressive và loại trừ ally/self giữ nguyên.

## Kiểm tra

- Build Release: 0 warning/error;20 actual-source fixture checks và206 compiled audits đạt.
- Bản dev.71 đã phát hành:100 lượt tìm mục tiêu có nullable rỗng tạo100 lỗi trampoline đúng dạng log client. Tìm mục tiêu với khoảng cách hữu hạn không áp dụng scope đúng.
- Bản sửa:200 lượt native gồm100 nullable rỗng và100 khoảng cách30;0 lỗi trampoline. Observer trong hàm tìm mục tiêu xác nhận100 lượt hữu hạn thực sự thấy150, sau mỗi lượt trả về30.9 kiểm tra native đạt.
- QA dùng object tạm trong scene menu, cưỡng chế eligibility riêng cho object này để cô lập lỗi nullable. Không phải thử minion đuổi và đánh quái trong gameplay.
- Helper QA nằm riêng trong tools và bản engine QA; không có trong DLL phát hành.

Bản sửa đã được đóng gói trong dev.72 theo yêu cầu upload GitHub. Candidate trước khi nâng version lưu tại `local-candidates/minion-nullable-fix-dev71`. Game Steam vẫn chạy tại thời điểm kiểm tra nên chưa copy vào Steam.

Kết quả: `validation/minion-nullable-fixed-native.json`, `validation/minion-nullable-fix-audit.json`, `validation/minion-nullable-fix-candidate.json`.
