# dev.32 — Bloodrage UI 10/5 -> 10/10

- Sửa mẫu số trong RageOfMorditasHuntProgress.SetBloodrageCharges và RageOfMorditasQuestTrackerUI.UpdatePhaseVisuals. Đọc số mốc thực từ asset rồi chuyển thành maximum charge qua BloodrageChargeRules.Earned. Asset5mốc ->10charge.
- Tử số đã được dev31 chuyển sang earned charge nên giữ nguyên:2/10,4/10,6/10,8/10,10/10. Không nhân đôi charge lần nữa; không sửa threshold list, hunt index, timer, reward balance hoặc save.
- Native call chain đã đối chiếu: notification Show gọi SetBloodrageCharges; Quest Display gọi UpdatePhaseVisuals. Hai đường refresh đều có postfix; chỉ ghi TMP text khi OfflineToolkit.Enabled.
- Build Release0warnings/errors;35compiled audit checksPASS,132patchclasses65selected/67skipped. Audit mới kiểm tra cảhaiUIhook, offlineguard và helper chỉ ghi text. Không chạy native engine/UI; không mở/đóng game hoặc càiSteam.
- Core có thay đổi Assist cùng bản, xem COMBAT-ASSIST-RECOVERY-dev32-VI.md; cập nhật cả cặp DLL. Protocol40 giữ nguyên. Báo cáo tổng hợp ở RELEASE-1.1.0-dev32-VI.md.
- Evidence: validation/audit-dev32-bloodrage-ui.json.
