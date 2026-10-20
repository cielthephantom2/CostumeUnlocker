# Woven Potential — 1.1.0-dev.2, 20/10/2026

Protocol 32. Cập nhật qua `update.bat` khi game đã đóng, trên cả Host và Client.

| LP trước khi dùng | Thành công | Thất bại |
| --- | --- | --- |
| 1 LP | 75% lên 2 LP | 25% xuống 0 LP |
| 2 LP | 50% lên 3 LP | 50% xuống 1 LP |
| 3 LP | 25% lên 4 LP | 75% xuống 1 LP |

Chỉ thay phép reroll riêng `ItemData.RandomizeLegendaryPotential` khi chơi offline. Mỗi vật phẩm roll một lần bằng số nguyên ngẫu nhiên 0–99. Giữ nguyên hàm gọi native kiểm tra điều kiện, tiêu hao lượt, cập nhật container và xử lý UI. Dữ liệu vật phẩm được rebuild/đọc lại bằng serialization native; giữ ID cá nhân, affix, implicit, unique rolls và điều kiện faction. Đầu vào 0/4 LP giữ hành vi native.

Kết quả 1 → 0 LP được giữ đúng, không bị bonus tối thiểu 1 LP của đồ rơi sửa lại. Không áp hệ số CoF hoặc lượt roll LP của đồ rơi lên bảng Woven mới. Changelog trong game ghi đủ ba mức xác suất. Giữ bonus Corruption x5 và các thay đổi của dev.1; encounter co-op mới vẫn chưa được nối vào gameplay.

## Kiểm nghiệm

- Release build: 0 warning, 0 error.
- 515 kiểm tra managed đạt. Các bài Woven duyệt toàn bộ 100 giá trị roll cho mỗi mức LP và kiểm tra hai phía của ngưỡng 75/50/25, đầu vào ngoài phạm vi và giá trị roll không hợp lệ.
- 7 compiled audit checks đạt, khảo sát 105 lớp Harmony; không đưa lại cơ chế encounter replay cũ.
- Native asset/adapter QA đạt trên đúng cặp DLL phát hành. Sáu trường hợp biên đi qua detour native thật: 1→2, 1→0, 2→3, 2→1, 3→4, 3→1. Đọc lại serialization cho đúng LP; các byte vật phẩm còn lại khớp bản đối chiếu chỉ đổi LP, ID cá nhân giữ nguyên.
- Lượt native đầu tiên vẫn lưu kết quả FAIL do Steam không chạy. Sau khi khởi động Steam, lượt chạy lại đạt toàn bộ QA này. Không lọc bỏ lỗi từ lượt đầu.
- 15 kiểm tra updater fixture đạt trước khi phát hành. Cặp DLL được ghim SHA-256 và size trong `latest.json`. Sau khi phát hành, kiểm thử tải thật từ GitHub và kiểm SHA cả hai DLL đạt: tổng 17 bài updater fixture/live.

Chưa kiểm thử trọn luồng bảng Woven trong map thật, tiêu hao lượt hay lưu inventory của nhân vật thật. Không thay DLL đang cài hoặc save thật để kiểm thử. Các kiểm thử trên không chứng minh encounter co-op đã hoạt động; hạn chế encounter và full campaign QA từ bản trước vẫn còn.

## SHA-256 của cặp DLL

- Mod: `A668B58533C47AE563324F8C1973A885E77D0730A734D15498EE5356E28D4697` — 734208 byte.
- Core: `6F18A2C0428503286790A96A6C6A3A2F2CC880E6587CB4F8C5048829BBC8E6F3` — 282112 byte.
