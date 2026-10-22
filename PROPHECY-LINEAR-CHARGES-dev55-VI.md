# Prophecy dùng hết charge — dev.55

Mỗi lần kích hoạt hợp lệ tiêu thụ toàn bộ completed charges đang tích của ô Prophecy. Tổng thưởng bằng số charge đã tiêu thụ nhân số lượng cơ sở của chính reward đang chọn, cộng các bonus native còn áp dụng. Không cố định20 món cho mọi loại.

Ví dụ chưa tính double reward:10 charge × cơ sở20 =200 món;10 charge × cơ sở4 =40;10 charge × cơ sở1 =10. Bonus double reward native vẫn hoạt động theo game. Số lượng cơ sở của từng reward, lens, mục tiêu, chất lượng item và điều kiện mở khóa do game quyết định.

GetChargesToUseWhenTriggered và số charge trong câu UI cập nhật theo toàn bộ charge. Giữ câu bản địa hóa và rich-text tags; chỉ thay con số hiển thị. Reward tooltip cơ sở vẫn mô tả một charge. Giới hạn tích trữ native99 charge không đổi.

TryTriggerReward có các ngưỡng cũ inline trong native code; chỉ patch helper UI sẽ không sửa loot thật. Mod gọi transaction native có kiểm tra mục tiêu lặp cho tới khi tiêu thụ hết snapshot charge ban đầu. Từng transaction tự trừ charge, phát loot, áp lens/bonus và gửi event gốc. Tổng chargesConsumed được cộng lại cho caller. Không ghi thẳng số dư charge, không sửa itemsDropped để giả số lượng.

Chỉ cho nhân vật local ở Play Offline. Guard theo slot/thread cho phép gọi native gốc mà không lặp prefix vô hạn; khác slot giữ scope riêng. Native reject/no progress/dữ liệu charge bị thay đổi dừng vòng lặp. Exception không hoàn lại charge đã dùng và không tự retry phát thưởng; guard luôn được xóa. Kích hoạt lần nữa khi đã0 charge không tạo loot.

Kiểm chứng: Release build0 warnings/errors;195 actual-source fixture checks và176 compiled/native audit checks pass. Fixture bao phủ cơ sở1/2/4/7/20 ở các mức0/1/2/3/4/10/11/19/20/29/30/99 charge, double reward, target sai, reject, no-progress, exception, nested slots, localized tags, online và remote actor. Native inspection xác nhận tier inline và vòng SpawnRewardForPlayer theo charge tiêu thụ; chưa xác nhận phát loot/UI trong game với DLL mới.

Giữ1.1.0-dev.55/protocol44, Auto Pickup native-trigger và chấm minimap xanh dương scale0.90 (gấp đôi bản xanh đầu tiên). Đóng game rồi chạy update.bat để cập nhật cả hai DLL.

Thông báo mở thưởng: giữ Notifications.ProphecyFulfilled gốc để lấy tên/màu/biểu tượng/bản địa hóa và số món của đúng reward. Trong lượt dùng hết charge, chỉ gom thông báo trình bày; mọi event gameplay/faction native vẫn chạy từng transaction. Kết thúc scope gọi một thông báo gốc với tổng charge đã tiêu thụ thành công (ví dụ10 với cơ sở4 →40 món). Không giới hạn3 hoặc10; fixture kiểm tra tới99 và base1/2/4/7/20. Không có charge thì không báo; mỗi ô có tổng riêng. Lỗi UI thông báo không được làm phát lại/hoàn charge hoặc ngắt các transaction đã xong. Bonus phát thêm vẫn theo native; thông báo giữ cách tính số món của formatter gốc.
