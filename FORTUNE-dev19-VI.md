# Circle of Fortune và Prophecy — dev.19 / protocol 29

Đã bỏ nút Retry character setup, method Retry và gợi ý retry thủ công. Luồng thiết lập nhân vật mới tự động vẫn giữ nguyên.

## CoF rank rewards

| Rank | Hiệu ứng mới |
| --- | --- |
| 1 | Quái có 100% xác suất rơi gấp đôi số vật phẩm. |
| 3 | Khi Idol rơi, có 50% xác suất rơi thêm 2 Idol. |
| 5 | 100% xác suất nhận gấp đôi phần thưởng vật phẩm Monolith Echo. |
| 6 | Tăng 100% xác suất item trở thành Exalted, hệ số native từ 1.5 lên 2. |
| 7 | Exiled Mage rơi x3 vật phẩm Experimental. |
| 8 | Hệ số xác suất roll LP native của Unique là x5. |

Giữ điều kiện thành viên/rank và bật/tắt rank của game. Các rank khác, yêu cầu reputation, favor và lens không được sửa. Chỉ áp dụng offline. Cập nhật backing fields native khi toggle rank và làm mới khi nhân vật đã sẵn sàng; không chỉ sửa getter vì native thường đọc field trực tiếp.

Rank 7: game vốn nhân x2 số lượng Experimental trước StochasticRound. Bản này đổi lượng kỳ vọng đó thành x3 bằng hệ số 1.5 trong đúng scope loot Exiled Mage, đúng recipient và đúng RNG. Không gọi loot lần thứ hai, không biến x2 thành x6, không sửa các roll bên ngoài scope. Native vẫn tự roll từng vật phẩm và xử lý các modifier số lượng khác.

Người dùng đã xác nhận **giữ Unique tối thiểu 1 LP**. Hệ số CoF x5 áp dụng vào lượt roll native trước khi chặn kết quả về tối thiểu 1. Vì vậy không quảng cáo rằng xác suất có LP khác 0 tăng thêm x5 so với mức tối thiểu 1 LP vốn đã có.

## Prophecy x2 và chữ trong game

Nhân đôi itemsDropped trong catalog runtime, gồm các tier nâng cấp: Rare x10 → x20, Unique x2 → x4, mọi reward khác cũng x2. Picker, socket, tooltip và luồng spawn native cùng đọc field này. Có guard chống nhân lặp khi catalog mở/khởi tạo lại hoặc asset xuất hiện nhiều lần. Chỉ sửa dữ liệu asset trong bộ nhớ, không đổi ID reward hoặc chép save; khôi phục khi dispose/chuyển khỏi offline nếu giá trị vẫn thuộc mod.

Native charge/lens và rank bonus khác được giữ: x2 ở đây là số lượng cơ sở so với bản trước, không vô hiệu hóa bonus nhân thêm mà nhân vật đã mở. Rank descriptions trong game dùng nội dung tiếng Anh tương ứng với số mới; giữ các dòng native mở khóa Prophecy slot/lens và các rank không được yêu cầu.

## Kiểm tra

- Release build: 0 warning, 0 error.
- 499 managed/network checks đạt, gồm 10 quy tắc Fortune mới.
- 22 kiểm tra adapter dùng source thật với API giả lập đạt: backing fields, điều kiện rank/member, catalog/upgrades/cycle/reinit, số UI và số theo charge, phục hồi, giữ unlock text và RNG scope.
- Kiểm tra source xác nhận không còn nút/method/lời nhắc Retry character setup.
- Native sandbox QA bị script chặn vì game người dùng đang chạy. **Chưa xác nhận render UI, loot thực tế hay hook native của dev.19.** Không lấy kết quả native dev.18 làm kết quả cho bản này.

Cập nhật cả hai DLL sau khi thoát game. Protocol giữ 29. Giữ các thay đổi co-op/DPS/density và trạng thái update đỏ/xanh từ bản trước. Không thay DLL trong thư mục game đang chạy bằng công cụ phát triển.
