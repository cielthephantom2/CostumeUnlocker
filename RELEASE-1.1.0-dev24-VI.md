# TM Co-op 1.1.0-dev.24 — Combat Assist / minimap / DPS

Protocol36. Cập nhật cả Host và Client, đóng game trước khi chạy update.bat. Bản dev.23 được dành cho source Encounter đang tạm dừng, không phát hành trong đợt này.

- F1 Repeat interval có 0 s / Hold key: skill thường lặp khi native state/cooldown cho phép; channel automation duy trì khi đủ điều kiện, dừng khi mất trigger/MP hoặc mở panel, loading, đổi skill, mất focus hay Stop. Không giả lập giữ phím OS; manual casting ưu tiên.
- Low ward / Ward threshold dùng số Ward tuyệt đối và hỗ trợ ANY/ALL. MP before cast là phần trăm MP hiện tại tối thiểu TRƯỚC cast, không phải số MP còn lại sau trừ chi phí.
- Detection radius hiển thị vòng quanh nhân vật khi hover/kéo slider và ngắn sau chỉnh; đóng F1 thì ẩn.
- Chấm quái minimap nhỏ còn30% kích thước cũ; Tab overlay giữ kích thước gốc.
- DPS không idle-hide; khung ngoài trong suốt, bars xanh lá, bỏ DMG/title/timer/SKILLS. DPS/5s Total/Total/Biggest/Hits xếp dọc; giữ kéo/resize/tooltips.

Bản này lấy nền dev.22/protocol36 và chỉ chuyển các sửa Combat Assist/minimap/DPS cùng tests liên quan. Không đưa nhánh native Encounter đang dở hoặc GuestProfile QA helper vào bản phát hành. Không khôi phục chỉnh Disintegrate/Arcane Ascendance, không đổi mật độ quái/HP scaling.

Build Release 0 warning/error; compiled Encounter exclusion audit9 PASS. Managed selftest và updater proof nằm trong validation/release-dev24-*. Chưa kiểm chứng trực tiếp native/gameplay/visual cho các sửa này; không coi build/tests managed là nghiệm thu gameplay hay FPS.