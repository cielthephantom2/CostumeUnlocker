# dev.36 local candidate — context click và Configure Tab native

User báo Shared Stash hoạt động nhưng không click đổi tên, Configure khác hòm riêng; ảnh có tab trống và category Shared Stash. Đã hỏi rõ người dùng đang nói tab, category hay ô nhập tên trong modal; chưa có câu trả lời ở thời điểm viết báo cáo.

Các lỗi code đã xác định và sửa:

- Dev35 chỉ gắn Button.onClick chọn tab, không nhận chuột phải; tab đang chọn còn bị disable. Dev36 gắn context click lên từng tab, cho phép click tab đang chọn và mở Configure sau khi snapshot Host của tab đích sẵn sàng. Không viết metadata hòm cá nhân.
- Dev35 luôn mở StashConfigureTabModalV2. Dev36 chọn StashConfigureTabModal cho hòm cũ và V2 cho hòm mới. Giữ modal native tên/màu/icon; folder/priority/delete/duplicate chưa có backend hòm chung vẫn ẩn, không cam kết clone mọi chức năng.
- Legacy dùng tab native đang hiển thị đã được khởi tạo làm mẫu trước raw prefab. Khôi phục label, áp màu lên background riêng và không hiển thị icon trắng khi sprite null. Chưa khẳng định đã giải quyết toàn bộ lỗi hình ảnh trong engine.
- Category Shared Stash là tên nhóm cố định hiện tại; bản này không thêm rename category hoặc folder protocol. Cần user xác nhận nếu vấn đề thực sự là category hoặc input tên modal.

Build Release0warning/error; **64 compiled audit PASS**. Không thay Core/protocol41/Corruption. Chưa chạy native UI/gameplay, chưa cài Steam, chưa commit/push/release; root/latest/GitHub và Steam vẫn dev35. Các kiểm tra metadata/IL không chứng minh thao tác chuột, input focus hoặc bố cục trong game.

Evidence validation/dev36-tab-config-*. Candidate ZIP local-releases/TM-Coop-1.1.0-dev.36-tab-config-candidate.zip. Giữ selftest-result.json và các file repair local untracked cũ.

User đã làm rõ: vấn đề là các ô tab xám trong ảnh và không Configure được, không phải category. Đã ưu tiên lấy tabUI từ localTabs native đang active, kiểm tra label/background/sprite khởi tạo; bật Graphic nhận raycast và thêm handler chuột phải. Bản36 vẫn local,64auditPASS, chưa cài/testengine.
