# Imprint dev45 — sửa sai cơ sở Sealed và tooltip Echoing

Người dùng kiểm tra khi tắt MelonLoader: Sealed Riches 1/1 hiển thị6%, Warlord3%, Echoing Maximum chance50%. Các bản trước lấy hằng số native .029 làm cơ sở Sealed và kết luận5.8% sau x2. Kết luận đó không khớp mức6% trên tooltip gốc; kiểm tra cũ chỉ chứng minh phép nhân .029×2, chưa chứng minh đúng mục tiêu người dùng.

| Node | Mục tiêu dev45 |
|---|---|
| Sealed Riches |12% với1 điểm; 0 điểm vẫn0. Gán từ số điểm, không nhân lặp lên kết quả cũ.|
| Warlord's Riches |6% mỗi điểm (gốc3%).|
| Echoing Riches |Nhân đôi công thức roll khi nhận thưởng Echo; tooltip Maximum chance50%→100%. Không đảm bảo mọi Echo đều rơi vì còn enemyScore, Corruption, unlock và đồ imprint.|
| Commoner's Riches |Nhân đôi tử số trong công thức theo spawner/enemy/Corruption; không tuyên bố một tỷ lệ cố định.|
| Woven Riches |Trọng số spawn tự nhiên của Woven Echo imprint x2 trong các consumer được chọn, vẫn giữ điều kiện đủ. Không đồng nghĩa xác suất cuối cùng x2 tuyệt đối sau chuẩn hóa.|
| Loomed Riches |Cơ chế trọng số như Woven cho slot thứ hai.|

## Bằng chứng và giới hạn

GameAssembly SHA256 `323E9183F3BEE600B6C9E1C469218B9DE926E9044F2CA3D44FAE8A0EC9A91464` vẫn chứa .029 ởRVA0x472A05C, effect42 trong UpdateWeaverTreeNode ghi points×.029 vào faction+0x120. Đây là quan sát native riêng biệt, không được tiếp tục gọi là tooltip gốc2.9%. Bản45 chủ động đặt mục tiêu12% từ cơ sở6% người dùng xác nhận, thay vì chỉ nhân đôi hằng số .029.

`MonolithZoneManager.ApplyHostWeaverModifiers` sao chép faction+0x120 sang `chanceForOneShotCachesToDropPileOfSimilarItems` tại zone+0x304. Coroutine `OneShotCacheGameplayObject/<OpenAfterDelay>d__26.MoveNext` đọc zone+0x304 truyền vào RNG.Roll, rồi kiểm tra faction/slot/item trước khi tạo đồ tương tự. Audit103 kiểm tra có đối chiếu byte native của cả hai bước. Khi mở cache, trường faction còn được dùng làm gate>0, không nhân xác suất thêm lần nữa.

Phải vào Echo mới để zone nhận giá trị mới. Runtime report thêm `weaverImprints.cacheRollChance` bên cạnh `cacheChance`: với1 điểm, ở Echo mới cần thấy cả hai khoảng0.12. Không thay unlock/điểm/asset hoặc tạo thêm lượt roll. Không re-enable MelonLoader tự động.

Build0warning/error; 43 kiểm tra fixture dùng nguồn patch thực với stub; full managed/TCP và compiled/native audit trong validation/dev45-*. Các kiểm tra này chưa xác nhận render tooltip và xác suất rơi bằng thực nghiệm trong game. Runtime trước sửa (dev42) .058 không phải bằng chứng chấp nhận dev45.

Upload GitHub và tắt máy vẫn tạm dừng theo yêu cầu “khoan”. Dev44 chỉ là bản rà soát chưa phát hành, gồm sửa thông báo category thành công bị báo rejected và mô tả phòng thiếu Ladder. Dev45 giữ các sửa đó cùng dev43 và các bản trước.
