# Combat Assist tiếp tục khi mở UI — dev.55

Khi đã Start, Assist tiếp tục kiểm tra potion và kỹ năng mỗi Unity frame dù F1/F4, Shared Stash, inventory, skill/passive, menu hoặc chat đang mở. Bỏ UI visibility và các cờ khóa input của UI khỏi cả điều kiện pause và điều kiện nhả channel. F1 không tự bật forceDisableInput toàn game. Chỉ F1 đang Stop mới dùng refresh100ms để xem cấu hình.

Đổi help/trạng thái Start cho đúng hành vi. Disconnect vẫn giữ Start/Stop và F1 Open; F1 mở không còn báo Paused. Nhập chat vẫn ngăn hotkey F1 bật/tắt nhầm; thao tác kỹ năng dùng native command trực tiếp.

Giữ kiểm tra offline, nhân vật sống/sẵn sàng, loading/transition, mất focus, game time pause, cinematic, cooldown/mana/charges, điều kiện từng skill và quyền ưu tiên thao tác tay. Game vẫn có thể từ chối cast theo trạng thái native. Không ép bật input của game.

Release build 0 warnings/errors; 1.053 managed/TCP self-tests, 13 actual-source disconnect checks và 165 compiled/native audit checks pass. Chưa xác nhận hành vi mở từng UI trong game với DLL mới. Game đang chạy nên chưa thay DLL trong Steam.

Giữ Auto Pickup 5 lượt/giây, tối đa 5 ứng viên, 1 thao tác, radius5, Woven Echoes và bỏ log pickup. Version 1.1.0-dev.55 / protocol44. Cập nhật cả Mod và Core cùng bản.
