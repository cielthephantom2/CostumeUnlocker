# Glyph of Hope / Despair — baseline native trước dev30

Đây là bằng chứng tỷ lệ native trước khi user yêu cầu custom rates. Source dev30 đã thêm Hope33% và Despair100/70/50/33%; xem `RELEASE-1.1.0-dev30-VI.md`. Các nhận xét "chưa sửa/chưa có hook" bên dưới mô tả thời điểm kiểm tra baseline, không phải trạng thái source dev30.

Upload radius30 đã tạm dừng theo yêu cầu. GitHub vẫn dev29; chưa tăng version, chưa upload candidate radius30. Chưa sửa glyph hoặc file game.

GameAssembly SHA256 `D5F9FB458F0E9878E697A92B590A586A52FD77E645F8BF87BB4F2B74C6C56567`. Đọc metadata/dịch ngược native trên đĩa, không khởi chạy hay đóng game. Mods Steam chỉ có LastEpochLAN.dll. Source mod hiện không có hook thay Hope/Despair; phần Guaranteed trong source LE HUD nghiên cứu là dự án khác, không được đóng gói vào mod co-op hiện tại.

## Hope

Metadata `CraftingManager.glyphOfHopePotentialLossPreventionChance = 0.25f`: **25%** không tốn Forging Potential khi Hope proc. Có thêm constant nội bộ `glyphOfGreaterHopePotentialLossPreventionChance = 0.4f`, nhưng **chưa xác nhận có vật phẩm Greater Hope đang dùng/drop trong game**. Không được coi constant40% là bằng chứng tồn tại glyph có thể farm; phát biểu trước đó đã gộp hai việc này và cần đính chính. Đây là tỷ lệ riêng của Hope, không phải tổng xác suất miễn FP từ mọi cơ chế critical/FP khác.

Nguồn chính thức đối chiếu Hope25%: https://support.lastepoch.com/hc/en-us/articles/46361877750043-Runes-and-Glyphs . Số Greater Hope lấy từ metadata native local, không suy ra từ bài cũ.

## Despair

Native `ItemData.getChanceToSealAffix` RVA0x120E700:

`K = 0.6 + 0.15 * item.rarity + (hasExaltedAffix ? 0.3 : 0)`

Tỷ lệ roll seal = K nhân hệ số theo tier hiện tại: T1 **0.70**, T2 **0.46**, T3 **0.23**, T4 **0.10**. Tier nội bộ0–3 tương ứngT1–T4; native AddAffixTier từ chối seal affixTier>=4 (T5 trở lên). `RngElement.Roll` trả true ngay khi xác suất>=1, nên tỷ lệ hiệu dụng tối đa100%.

Với đồ thường/Magic/Rare/Exalted hợp lệ, `setRarityFromAffixesForNormalMagicOrRareItem` đặt rarity theo số affix chưa seal, clamp0–4; không dùng rarity7/8 của Unique/Set để tính bảng crafting này. `hasExaltedAffix` kiểm tra có affixTier>=5 (T6+) trong item.

| Tier muốn seal | Rare đủ4 affix, không T6/T7 | Exalted đủ4 affix, có T6/T7 |
|---|---:|---:|
| T1 | 84% | 100% (raw105%) |
| T2 | 55,2% | 69% |
| T3 | 27,6% | 34,5% |
| T4 | 12% | 15% |

Đây là tỷ lệ của phép roll seal khi đi vào nhánh Despair, không phải một mức cố định cho mọi món đồ hoặc cam kết kết quả toàn bộ thao tác Forge; native critical craft/eligibility và giới hạn affix đã seal vẫn do game xử lý. Không nhầm xác suất với drop rate của glyph.

## Bằng chứng đọc native

Files trong `research/` (ignored, chỉ local): `glyph-native-metadata.txt`, `glyph-seal-native.txt`, `glyph-formula-confirmation.txt`, `glyph-forge-native.txt`, `glyph-rarity-native.txt`, `glyph-rarity-check.txt`.

Constants float đọc bằng PE RVA mapping: 0x4722EB0=0.15; 0x4567DBC=0.3; 0x456826C=0.6; 0x4568270=0.7; 0x47230E8=0.46; 0x4722F8C=0.23; 0x4567DB8=0.1. FieldOffset ItemData.rarity=0x30, forgingPotential=0x18. Vì vậy Despair dùng rarity/số affix và trạng thái Exalted, không trực tiếp dùng lượng FP còn lại trong hàm chance này.
