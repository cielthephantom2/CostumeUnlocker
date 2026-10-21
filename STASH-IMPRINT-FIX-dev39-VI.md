# dev.39 — Shared Stash unstripping và tooltip Imprint

## Shared Stash

Nguyên nhân xác định bằng IL của interop Steam: `UnityEngine.RectTransformUtility.CalculateRelativeRectTransformBounds(Transform, Transform)` có body ném `Method unstripping failed`. `ScrollSharedSelection` gọi đúng overload này ở dev37/dev38; lỗi làm dispose giao diện Shared Stash. Log trước đây chỉ ghi Message nên mất stack trace.

- Thay bằng đo4góc RectTransform qua native `TransformPoint` và `InverseTransformPoint`, vẫn dùng giao diện game gốc.
- Audit DLL đọc body các Unity/TMP callee trực tiếp trong view và helper mới, từ chối nếu còn stub unstripping. Không sửa DLL Unity/interop của game.
- Log ghi đầy đủ exception/stack. Đóng F2 sau lỗi reset trạng thái chuẩn bị để có thể thử lại; giữ dữ liệu stash và cơ chế transfer cũ.

## Imprint

Ảnh Warlord's Riches ghi0/1: node chưa phân điểm nên chưa có hiệu lực. Cần điểm và vật phẩm Imprint hợp lệ; mod không tự mở node hoặc cấp điểm.

Bonus gameplay và tooltip là hai đường khác nhau. Dev34 đã sửa đường gameplay nhưng chưa đồng bộ các stat tooltip lấy từ node/localization. Dev39 sửa kết quả tooltip native mới tạo, không sửa asset/node points:

- Warlord:3%→6% mỗi điểm. Sealed:2.9%→5.8% mỗi điểm; giá trị nhiều điểm và dấu thập phân được giữ đúng.
- Giữ mô tả gốc, bổ sung giải thích x2 cho cả6node: Commoner/Echoing là xác suất theo công thức native, Woven/Loomed là trọng số spawn chứ không cam kết xác suất cố định hay gấp đôi số Echo thực nhận.
- Không nhân số món/affix được mô tả, không đổi điều kiện unlocked/item/minCorruption hoặc dữ liệu nhân vật.
- Runtime report `UserData/LastEpochLAN.runtime-report.json` thêm `weaverImprints`: field xác suất thật trên TheWeaver và số lần hook cập nhật. Có thể phân biệt node chưa kích hoạt với tooltip chưa đồng bộ. Đây là chẩn đoán, không ép lại điểm hay xác suất mỗi frame.

Đã đối chiếu GameAssembly Steam26087601 SHA256 `323E9183F3BEE600B6C9E1C469218B9DE926E9044F2CA3D44FAE8A0EC9A91464`: callback OnWeaverTreeChanged đi qua OnWeaverTreeNodeChanged→UpdateWeaverTreeNode; base Cache.029 tại RVA472A05C và Champion.03 tại RVA45742F8 vẫn đúng. Các consumer Echo/Woven còn gọi các helper đã hook; RefreshModifiers gọi GetModifierInfo nên hook sửa số tooltip nằm trên đường thực. Phân tích native không thay thế thống kê drop/gameplay.

## Kiểm chứng/cài

- Mod1.1.0-dev.39, protocol43. Cần thay **cả hai DLL** vì Core thêm helper tooltip. Bao gồm Ladder/minion dev38 và Shared Stash dev37. Không tự upload GitHub; root/latest vẫn dev35.
- Build Release0warnings/errors;1.013managed/TCP checks;81compiled audit checks;34actual-source Weaver stub checks;5geometry checks;11lifecycle checks. Fixture kiểm tọa độ relative/rotation/scale/parent, retry và tooltip locale/nhiều điểm/online không đổi.
- Chưa render UI hoặc kiểm loot/địch thực tế trong Unity. Không tự mở/đóng game. Cài Steam có backup hai DLL và SharedChest, kiểm SHA sau copy; bằng chứng `validation/dev39-steam-install.json`.
- Các vấn đề Corruption gain đã hoãn và save nhân vật không thay đổi.

Kiểm tra: F2 mở được, chuyển tab/category không warning; Warlord tooltip6% tại0/1 là preview mỗi điểm, còn hiệu lực cần phân điểm và Imprint. Khi đã phân điểm, đối chiếu `weaverImprints.championChance` (.06mỗiđiểm) / `cacheChance` (.058mỗiđiểm). Gửi log mới nếu còn lỗi; log nay có stack cụ thể.
