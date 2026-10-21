# dev.32 — Combat Assist phục hồi và chẩn đoán

User báo thỉnh thoảng Assist ngừng, disconnect/host lại thì chạy. Không nhớ dòng trạng thái. Snapshot local trong lượt kiểm tra vẫn Armed=true, không lỗi, request/confirmed counters còn tăng; travelBusy=false. Chưa tái hiện chính xác lần kẹt, không khẳng định đã xác định duy nhất nguyên nhân từ mạng.

## Nguyên nhân sau khi user khoanh vùng bật/tắt DPS

User bổ sung lỗi xảy ra khi đang Assist và bật/tắt DPS Tracker. Code xác nhận chuỗi: mở F4 -> Mod Settings đặt OfflineToolkit.PanelOpen=true -> bật/tắt DPS -> đóng F4 đặt panel=false. DrawCoopUi không vẽ Settings vì parent đã ẩn, nhưng PanelOpen vẫntrue để nhớ trang con. OnUpdate cũ truyền `panel || sharedChest.Open || OfflineToolkit.PanelOpen` vào Assist, nên child flag vô hình tiếp tục pause vô hạn. Điều hướng Back to Co-op có thể xóa flag, giải thích việc thao tác room làm nó chạy lại.

Sửa chính xác: điều kiện pause UI chỉ là rootF4visible hoặc F2open. Settings nằm trong root nên không cần chặn riêng. Giữ trang Settings khi mở lại F4; không sửa Master switch, không tự bật/tắt DPS để chữa cháy. Runtime report thêm modUi.f4Visible/settingsPageSelected/settingsVisible/stashVisible/dpsVisible để phân biệt selected và visible. Compiled audit bảo đảm OnUpdate không còn đọc child PanelOpen khi gọi Assist.

## Sửa các đường dừng đã tìm thấy

- Actor null/identity trống tạm thời khi loading chỉ pause, không tự gạt Master OFF. Khi actor/identity trở lại thì đọc tiếp. Profile đổi nhân vật vẫn disarm và cần Start; không tự bật khi user đã Stop hoặc chuyển online.
- Lỗi đọc native tạm thời: xóa cache target/các deadline đọc, chờ250ms rồi retry, tối đa3lần. Đọc thành công reset bộ đếm và lỗi. Lỗi kéo dài dừng và chỉ rõ F1 để restart. Không tự retry lỗi trong lúc thực hiện native cast/potion/channel action.
- Lỗi render panel chỉ ghi Error/log, không tự xóa Master switch. F1 đang mở vẫn chặn auto-action; người dùng đóng bằng F1 được.
- Trạng thái Busy của travel/Monolith co-op chỉ được truyền vào Assist khi shared-world policy bật. Inventory-only room không cần phụ thuộc các state machine đã bị tắt.
- Đổi Repeat Interval, đặc biệt120s->0Hold, xóa deadline cũ của skill ở lần Select kế tiếp; giữ25msdispatchguard và khoảng chờ xác nhận cho cấu hình không đổi.
- F1 footer hiện LastRunStatus trước khi mở F1. Diagnostics ghi lý do pause cụ thể, số read failures/retryAt và thời gian repeat còn lại của từng slot; slot ready đang chờ timer ghi Repeat interval thay vì Ready chung chung. Explicit Start xóa recovery/error cũ.

## Kiểm chứng / phạm vi

- Release build0warnings/errors, **892managed checksPASS**, gồm interval-edit, native-confirmation interval, bounded read retry/reset và action-error no-replay; **38compiled audit checksPASS**, gồm regression hidden Settings sau toggleDPS. Không engine/nativegameplay/UI/FPS acceptance, không mở/đóng game, không thay Steam.
- Localdev32 gồm cả Bloodrage UI10/10 của lượt trước. Core có thay đổi AutoPlaySchedule/AutoPlayRecovery, phải cập nhật cặp DLL cùng bản khi phát hành; Core không còn identicaldev31.
- Cặp phát hành: Mods/LastEpochLAN.dll và UserLibs/LastEpochLAN.Core.dll theo latest.json. Báo cáo tổng hợp ở RELEASE-1.1.0-dev32-VI.md.
- Evidence: validation/managed-dev32-assist-recovery.json, validation/audit-dev32-assist-recovery.json. Báo cáo runtime thực tế vẫn ở UserData/LastEpochLAN.runtime-report.json trên máy chơi; không đưa dữ liệu raw nhân vật lên GitHub.
