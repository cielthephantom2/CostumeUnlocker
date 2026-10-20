# Bản 1.1.0-dev.16 — Disintegrate di chuyển khi channel

Đóng game trên Host và mọi Client rồi chạy `update.bat`. Protocol 35; Core giữ nguyên dev.14.

- Thêm hook movement channel native cho Disintegrate của nhân vật local trong môi trường offline, nhằm cho phép di chuyển khi channel.
- Dùng AbilityMovement.Spin, cho đổi hướng khi di chuyển và dừng khi nhả phím. Movement speed ảnh hưởng di chuyển; cast/use speed không ảnh hưởng di chuyển.
- Không đổi asset chung hoặc skill của peer/NPC; giữ nguyên animation/channel/damage/mana/cooldown/beam scripts. Không thêm Tick, actor scan, Transform.Move hay damage replay.
- Bao gồm dev.15: bỏ party HP scaling, tối ưu xử lý nền khi solo; giữ mật độ quái và các sửa F1/F2 dev.13/14.

Build Release candidate đạt 0 warning/error và diff check đạt. Đã cài local khi game đóng và xác minh hash, không mở game. Chưa kiểm chứng native cast/movement/steering/release/mana-stop/collision/animation/skill-tree hoặc hai máy; chưa xác nhận hành vi gameplay. Các giới hạn dev.15/dev.14 vẫn áp dụng.

SHA256 Mod: `9A8A78FECB50A2DD6177EA19029F153CE890211F6591B3A55A06E77DABE845D6`

SHA256 Core: `4AE855E63CCD75E91099119387B3E295DBFC7E6C8AD7711F65502DCDD4631F6E`
