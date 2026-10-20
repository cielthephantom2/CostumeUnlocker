# Woven Potential — 1.1.0-dev.3, 20/10/2026

Sửa riêng mức 1 LP theo yêu cầu mới:

| LP ban đầu | Thành công | Thất bại |
| --- | --- | --- |
| 1 LP | 50% lên 2 LP | 50% xuống 0 LP |
| 2 LP | 50% lên 3 LP | 50% xuống 1 LP |
| 3 LP | 25% lên 4 LP | 75% xuống 1 LP |

Changelog trong game và kiểm thử đã đổi theo xác suất mới. Giữ cách xử lý Woven của dev.2: một roll nguyên 0–99 cho mỗi vật phẩm, không chặn kết quả 0 LP bằng bonus đồ rơi tối thiểu 1 LP. Đầu vào 0/4 LP giữ hành vi native; tiêu hao lượt và điều kiện dùng vẫn do hàm gọi native xử lý. Protocol vẫn 32.

Build Release sạch, 515 kiểm tra managed và 7 compiled audit checks đạt. Quan sát native cho cả sáu nhánh qua detour thật đạt, serialize/đọc lại đúng LP và giữ dữ liệu vật phẩm còn lại. Tuy nhiên native QA tổng thể FAIL do SteamApi_Init, cả khi mở Steam và chạy lại; giữ nguyên trạng thái FAIL, không coi quan sát riêng là kết quả QA tổng thể đạt. Riêng mức 1 LP dùng hai biên 49 → 2 LP, 50 → 0 LP. Updater fixture kiểm tra cặp DLL và lỗi tải đạt 15 bài trước phát hành.

Chưa nghiệm thu trọn luồng panel, tiêu hao lượt hoặc lưu inventory trong gameplay thật; encounter co-op mới vẫn chưa hoạt động. Không sửa save hoặc DLL đang cài của người dùng để kiểm thử.

Đóng game và chạy `update.bat` trên cả hai máy để tải bản mới.

Cặp DLL: Mod SHA-256 `6E9BDE5F6E2EED0734917EEC08E91B5A606A5DBC7EF695F84E7795A0528C814F` (734208 byte); Core `AAF10B56E3E719608047CE9E8F55B829A3C5996FDB4CE03D61E083100E3AAE7C` (282112 byte). Lượt QA đầu báo lỗi Steam chưa chạy được giữ nguyên trong bằng chứng kiểm thử.
