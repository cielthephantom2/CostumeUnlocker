# TM Last Epoch Co-op Mod 1.1.0-dev.73

Protocol44. Cập nhật cả hai DLL khi game đã đóng.

## Thay đổi

- Werebear / Rampage: giữ skill để di chuyển và đổi hướng theo vị trí ngắm chuột. Bổ sung cập nhật đích Dash vì asset Rampage gốc không đánh dấu countsAsMovementAbility; chỉ bật cờ steer là chưa đủ.
- Relentless Thunder: bỏ thời hạn6 giây. Cả chuyển100% base damage Swipe sang Lightning và more Lightning Damage tồn tại suốt dạng Werebear, được gỡ khi thoát dạng. Lấy đúng actor từ mutatorManager của object con.
- Gathering Storm / Excited Bolts: tăng cap damage bonus từ300% lên1000%. Giữ công thức theo mana trước khi tiêu thụ và giới hạn chi phí10 mana mỗi Storm Bolt.
- Rule of the Simoon: proc Tornado thường thay vì ép stationary fire Tornado; tối đa2 lần mỗi1 giây. Fire Shred của Tornado đổi thành Lightning Shred, increased Fire Damage đổi thành increased Lightning Damage, Fire Resistance đổi thành Lightning Resistance. Giữ giá trị roll, rollID và scaling item; Tornado vẫn tuân theo cây skill người chơi.
- Grip of Chaos: “When you cast Maelstrom”, bỏ yêu cầu direct cast riêng cho proc Tornado. Giữ xác suất và chi phí50% mana Tornado; không biến các cơ chế khác của Maelstrom thành direct cast. Có chặn proc đệ quy đồng bộ.
- Đồng bộ mô tả node và tooltip item với các hiệu ứng mới.

## Đã kiểm tra

- Release build:0 warning/error;212 compiled audits đạt, bao gồm không có debug/probe/smoke hoặc bộ đo tạm trong DLL phát hành.
-48 kiểm tra native IL2CPP đạt trên game Steam, chạy trong bản QA/profile riêng. Không dùng save nhân vật thật.
- Trong map MonolithHub với nhân vật Primalist thật của engine: lệnh Rampage được chấp nhận, nhân vật thực sự di chuyển, đích Dash và hướng di chuyển đổi theo vị trí ngắm mới. Relentless vẫn áp dụng cả hai stat sau hơn6 giây, rồi gỡ cả hai khi trở về dạng người.
- Grip: dựng Maelstrom bằng Direct, Indirect và DelayedCast; mỗi lần tạo đúng1 Tornado thật, mỗi lần tiêu9 mana theo chi phí của cấu hình thử, xác suất được khôi phục sau cast.
- Simoon: formatter tooltip native hiển thị đúng Lightning và2 proc/giây; registry giữ đúng roll; consumer stat đọc Lightning Shred riêng cho Tornado; applier native nhận đúng ailment/radius/chance; tracker native cho2 proc và chặn lần3 tức thì.
- Excited Bolts: các mức mana0/100/500/1000/2000 cho bonus0/100/500/1000/1000%; consumer damage native xác nhận các hệ số tương ứng.

Phạm vi: test Rampage cấp tọa độ ngắm bằng API input native, không thao tác chuột thủ công. Simoon/Excited được kiểm tra bằng registry và consumer native trên object thử; chưa đo damage lên quái hoặc chạy một trận combat dài với item trang bị.48 kiểm tra này không phải bảo đảm mọi tương tác build hoặc multiplayer đều đúng.

ZIP chỉ chứa cặp DLL, latest.json, update.bat, README và release notes. Helper QA, debug, log và profile không được đóng gói. Updater và ZIP public được xác minh SHA256 sau khi phát hành.
