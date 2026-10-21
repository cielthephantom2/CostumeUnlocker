# dev.42 — đường tooltip Weaver legacy bị bỏ sót

Ảnh người dùng tiếp tục hiện Warlord3% và không có dòng giải thích bonus. Các hook trước chỉ nhắm `LE.UI.Tooltips.NodeTooltipContentBuilder`, trong khi màn hình thực tế này dùng `NodeTooltipManager` legacy.

Đối chiếu native Steam26087601: `NodeTooltipManager.setInfoForNode` gọi `scaleValueText` rồi `BuildColoredValue` tại RVA17E63DE. `BuildColoredValue` là triển khai thật tại RVA17E3940, không phải shared no-op. Native setInfoForNode cũng dùng localization riêng, không qua bộ dựng V2.

Dev42 thêm hai hook offline cụ thể:

- Prefix BuildColoredValue đổi bản sao string `valueText`: Warlord3%→6%, Sealed2.9%→5.8%, cả preview0điểm và trị số đã scale theo số điểm. Sau đó game tự tô màu/ghép dòng. Không sửa NodeTooltipStat.value, asset hoặc điểm node.
- Postfix setInfoForNode giữ mô tả native, thêm giải thích cùng nội dung với V2 và đặt requireLayoutUpdate để game căn lại tooltip. Không nhân đôi ghi chú khi refresh.

V2 hooks được giữ. Runtime report bổ sung modernTooltipValues/legacyTooltipValues/legacyTooltipDescriptions để xác định renderer đã đi qua hook nào.

Runtime report thực tế trước sửa, moddev40: weaverImprints.ready=true, cacheChance=.058, championChance=0, cacheUpdates=1,championUpdates=0. Đây là bằng chứng Sealed đã áp bonus thật; Warlord chưa nhận điểm ở ảnh0/1 nên xác suất hiện tại0. Tooltip preview mỗi điểm vẫn phải6%;0/1 không giải thích việc tooltip3% và không phải lý do bỏ qua lỗi UI.

Build Release0warnings/errors;40actual-source Weaver stub checks;99compiled/native audit checks, gồm hai native targets legacy không phải ret/ret0. Chưa mở game/render tooltip sau cài. Không tự chạy/đóng game; không đổi điểm/save/interops/Core/protocol43. Bao gồm layout dev41 và fix startup dev40. Cặp DLL/ZIP trong local-releases/dev42-weaver-legacy-candidate và TM-Coop-1.1.0-dev.42-weaver-legacy-candidate.zip; cài Steam có hash và backup trong validation/dev42-steam-install.json. Không phát hành GitHub.
