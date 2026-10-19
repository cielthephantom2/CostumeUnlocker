# Nemesis 1.0.9-dev.27 — protocol 31

## Thay đổi

Sau khi hoàn tất Empower/Challenge, hình Nemesis đứng chờ có thể xuất hiện lại tại vị trí vừa dùng. Bản này thêm kiểm soát vòng đời cho offline solo và nhân vật Host, đồng thời sửa việc client dựng lại hình đứng chờ khi lượt đang chờ lưu hoặc đã được dùng.

- Ghi nhận hoàn tất bằng controller, nhân vật kích hoạt và boss thực tế của encounter. Chỉ ghi nhận khi callback chết khớp boss; không dựa riêng vào account ID offline.
- Chặn callback chết lặp của cùng boss để không chạy nhận thưởng lần hai. Luồng nhận thưởng, Empower và lưu nhân vật vẫn dùng hàm native.
- Đánh dấu nhân vật đã hoàn tất khi native kiểm tra quyền dùng lại; chặn reset/spawn hình đứng chờ cho vị trí đã dùng. Client không tự sinh hình Nemesis native ngoài bản sao của Host.
- Client tiếp tục xử lý giao dịch thưởng/lưu trước khi dọn hình đứng chờ. Các boss của lượt người chơi khác vẫn đi qua hệ thống đồng bộ NPC.
- Controller mới ở map khác có lượt mới. Các chặn vòng đời này chỉ áp dụng offline.

Đây là bản sửa Nemesis tách từ dev.26. Đợt mở rộng đồng bộ Blood Crystal, Rift Beast và nhận thưởng encounter vẫn đang phát triển, chưa đưa vào bản này vì kiểm thử thưởng Rift Beast chưa đạt. Không công bố toàn bộ encounter đã đồng bộ hoàn chỉnh.

## Kết quả kiểm tra

- Release: 0 warning, 0 error.
- 521 kiểm tra managed/network, 13 kiểm tra vòng đời Nemesis với recording native objects, 6 kiểm tra loot người hỗ trợ, 22 kiểm tra vòng đời VFX, 46 kiểm tra Monolith và 16 kiểm tra animation: đạt.
- Native bootstrap/inactive-component smoke trong sandbox riêng: đạt, không có lỗi runtime trong bài này.
- Trong bài native encounter dùng nhân vật tạm và prefab thật, các bước Nemesis đã chạy tới cuối: nhận thưởng/Empower, chết boss qua callback native, kiểm tra đã dùng lượt, gửi lại callback chết và thử reset visual. Kiểm tra fingerprint xác nhận callback lặp không đổi pool lần nữa; reset không hiện lại hình đứng chờ. Luồng panel Guest/TCP/lưu Banish và boss Host cho Guest cũng hoàn tất các bước quan sát.
- **Toàn bộ native campaign QA chưa đạt:** sandbox phát sinh 5 lỗi khởi tạo: Actor null, hai lỗi PlayFab chưa đăng nhập, UserIdentity chưa sẵn sàng và EncounterPlacementData null. Báo cáo giữ `passed=false`, `observationsPassed=true`; không đổi các lỗi này thành kết quả đạt.
- Chưa tái hiện toàn bộ tình huống người dùng trên hai máy thật. Vì vậy đây là bản sửa để kiểm tra lại, không phải xác nhận hết mọi nguyên nhân tạo trùng Nemesis.

## Đọc log hiện tại

Log được kiểm tra đang chạy dev.26. Player.log có hơn 93.000 exception tại `JBooth.MicroSplat.TraxManager.LateUpdate`, 265 tại `AttachRefreshableVFXOnHit.onHit` và 70 tại `TargetFinder.updateTarget`. MelonLoader có lỗi native tại `GlobalVFXManager.HandleSettingCreationReferences` và `UsingAbility.beginUsingAbility`. Các lỗi này chưa được sửa trong bản Nemesis này; số lượng log không chứng minh chúng gây nhân bản Nemesis. Log chưa có trace Empower/Challenge đủ để kết luận duy nhất một nguyên nhân.

## Kiểm tra sau cập nhật

Thoát game, chạy update.bat mới trên cả hai máy để nhận đủ hai DLL dev.27; mở lại game và tạo phòng mới. Protocol vẫn là 31.

Thử solo trước: Empower rồi giết boss; thử Challenge ở encounter mới. Hình Nemesis đứng chờ không được xuất hiện lại ở vị trí đã dùng và không nhận thưởng lặp. Sau đó thử tương tự khi Host/Client thay nhau kích hoạt lượt riêng. Người đã dùng lượt không thấy lại hình đứng chờ của mình; boss lượt đồng đội vẫn phải hiện như NPC được đồng bộ.

Không cập nhật DLL đang được game sử dụng. Bộ kiểm thử chỉ chạy trên sandbox riêng; không thay DLL cài đặt, không sửa save thật.
