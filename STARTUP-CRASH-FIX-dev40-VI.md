# dev.40 — sửa crash khởi tạo dev38/dev39

Melon Latest.log dừng01:53:47 sau Support Module Loaded, chưa có dòng patches installed. Windows APPCRASH ghi0xc0000005/coreclr.dll+0x1d4089. Đọc dump `C:/Users/cielt/AppData/Local/CrashDumps/Last Epoch.exe.18856.dmp` bằng dotnet-dump cho thấy RoomLadderSeasonPatch.Prefix→OfflineToolkit.Enabled→GameplayEnvironment.get_IsOnlinePlay/.cctor, lồng trong CosmeticsManager.cctor khi RoomPatches.Install còn đang cài hook.

Nguyên nhân: `LadderUIManager.HandleSeasonEventUI` trên GameAssembly Steam26087601 là `ret0` tại RVA0x3D2F40, dùng chung native body với nhiều hàm rỗng khác. Detour hàm này tác động vào khởi tạo loại không liên quan và gọi ngược GameplayEnvironment chưa khởi tạo xong. Đây là lỗi của hook Ladder mới, không phải lỗi CoreModule hay Shared Stash.

Dev40 xóa hoàn toàn class hook và allowlist của HandleSeasonEventUI. Chỉ điều chỉnh các GameObject mùa/sự kiện trên đúng instance khi mở Ladder. Không dùng guard để giữ lại detour của body chung. Thêm log tên hook trước khi cài để khoanh vùng nếu còn crash khác.

Build0warnings/errors;18actual-source Ladder stub checks;91compiled/native audit checks. Audit kiểm raw native body của9hook Ladder/minion/tooltip mới, loại ret/ret0/int3, đồng thời xác nhận hook HandleSeasonEventUI đã biến mất. Kiểm này bổ sung cho các test logic; không thay thế chạy engine.

Mod1.1.0-dev.40/protocol43, Core giữ nguyên dev39. Gói cả hai DLL; giữ sửa stash/tooltip dev39 và minion/Ladder dev38. Chưa tự khởi chạy game và chưa xác nhận boot mới thành công. Cài vào Steam sau khi kiểm game đóng, backup và hash verify; xem validation/dev40-steam-install.json. Không tự phát hành GitHub hoặc sửa interop/save.

Chẩn đoán dump ở ignored research/dev40-crash-managed.txt. Bằng chứng native no-op đã lưu validation/dev40-crash-analysis.json. Các gói dev38/dev39 là lịch sử, không dùng thay cho dev40.
