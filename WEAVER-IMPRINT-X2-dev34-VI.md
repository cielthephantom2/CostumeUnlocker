# dev.34 candidate — Weaver Imprint x2

**Cập nhật: dev.34 đã được bổ sung Shared Stash nhiều ngăn, Sort/Configure/+ và nâng protocol lên41. Gói Weaver-only/protocol40 bên dưới là bản ứng viên cũ; dùng gói shared-tabs mới. Xem [báo cáo kết hợp](SHARED-STASH-TABS-dev34-VI.md).**

Bản kết hợp dev.34/protocol41 được chuẩn bị phát hành theo yêu cầu người dùng; root DLL pair/latest.json đã cập nhật. Xem RELEASE-1.1.0-dev34-VI.md và validation/release-dev34-*. Gói Weaver-only bên dưới là lịch sử, không dùng để cập nhật. Chưa cài Steam, không khởi chạy hoặc đóng game.

| Node | Thay đổi |
| --- | --- |
| Commoner's Riches | Xác suất kích hoạt từ công thức native ×2; giữ tỷ lệ spawner/quái và đường cong Corruption |
| Sealed Riches | 2,9% → 5,8% mỗi điểm |
| Echoing Riches | Xác suất từ enemyScore của thưởng Echo và hệ số Corruption ×2 |
| Warlord's Riches | 3% → 6% mỗi điểm |
| Woven Riches | Trọng số tự xuất hiện của Woven Echo đã Imprint ×2 |
| Loomed Riches | Trọng số tự xuất hiện của Woven Echo đã Imprint ×2 |

Đối với Woven/Loomed, đây là **trọng số native**, không phải cam kết số Echo thực nhận tăng đúng gấp đôi: các ô cùng tham gia lựa chọn có chuẩn hóa và giới hạn native. Đã bao phủ bước IncreaseChanceForSpecificEchoTypes tính trọng số đảo ngoài cùng, không chỉ nhân hai trọng số trong lần chọn cuối vốn sẽ triệt tiêu khi chuẩn hóa.

Giữ điều kiện đã mở node, ô đã có vật phẩm, loại vật phẩm và Corruption tối thiểu. Không đổi số món trong cụm, affix/Unique roll, số điểm Weaver, tỷ lệ LP của máy Woven Potential hoặc điều kiện đồ 0 LP. Xác suất cuối không vượt 100%; hệ số trung gian không bị cắt về1 trước khi nhân các thành phần còn lại.

## Cách áp dụng vào code native

- Sealed/Warlord: postfix UpdateWeaverTreeNode chỉ nhân hai field xác suất sau mỗi lần native tính lại từ điểm. Các đường drop native đọc field trực tiếp nên hook getter đơn lẻ không đủ. Mỗi lần refresh native ghi giá trị gốc rồi mới nhân2, tránh cộng dồn.
- Commoner: native DropBonusLootForAnyActor nhúng toàn bộ công thức; chỉ hook ChanceForEnemiesToDropPileOfSimilarItems sẽ không đổi drop thực. Trong lời gọi đồng bộ này tạm nhân2 totalSpawners, là tử số chỉ được đọc một lần tại nhánh Commoner, rồi finalizer khôi phục chính xác số gốc kể cả khi có exception. Guard theo zone/thread chống nhân lặp khi gọi lồng; không đổi mật độ spawn thực. Helper trực tiếp có hook riêng và tránh nhân lần hai khi đang trong scope.
- Echoing: scope theo thread bao quanh TryDropSimilarItemsToImprintedWeaverTreeItem; chỉ nhân hệ số SimilarItemDropMultiplierFromCorruption trong scope này. Commoner có bản exponential decay nhúng riêng, không bị hook này nhân tiếp.
- Woven/Loomed: scope bốn consumer OverrideEchoTypes, CanSpawnSpecialEchoesNaturally, IncreaseChanceForSpecificEchoTypes, SetNaturalSpawnedWovenEchoIslandData. GetTotalNaturalSpawnWeighting chỉ được nhân2 trong các scope đó; giữ các gate native. Scope lồng không nhân nhiều lần, finalizer khôi phục context.
- Không hook RNG toàn cục, không quét map mỗi frame, không chạy thêm lần drop/reward, không sửa shared asset. Chỉ hoạt động trong offline game (bao gồm phòng Internet của mod xây trên offline). Không thay official online.

## Kiểm chứng

- Build Release **0 warnings/errors**.
- **930 managed/TCP checks PASS**, gồm công thức x2, các mức điểm, cap xác suất, hệ số Corruption lớn hơn1 và trọng số0.
- **20 patch fixture checks PASS**: link nguyên source patch vào môi trường stub .NET; thử native point refresh lặp, bỏ điểm, Commoner lồng cùng/khác zone, khôi phục khi có lỗi, scope Echo/Woven, và chặn official online.
- **46 compiled audit checks PASS**: 139 lớp patch, 72 chọn và67 bỏ; kiểm target thật trong DLL và giữ world/Encounter co-op disabled.
- Đối chiếu native GameAssembly SHA256 D5F9FB458F0E9878E697A92B590A586A52FD77E645F8BF87BB4F2B74C6C56567; các kết luận inlining áp dụng cho build này.
- **Chưa chạy native gameplay/drop statistics/UI/FPS**. Fixture không thay thế kiểm thử detour và vòng đời game thực.

Bằng chứng: validation/dev34-weaver-*. Bản build cặp DLL và ZIP ứng viên trong local-releases/dev34-weaver-candidate. Cần thay cả Mod DLL và Core DLL khi cài sau này.
