# Meteor dev46

- Extinction: Large Meteor chance10%→33% mỗi điểm. 1/2/3 điểm tương ứng33%/66%/99%, xác suất tối đa100%. Giữ bonus crit/damage khác của node.
- Heat Flux:1→2 Frost Claw mỗi điểm. Mỗi Frost Claw dùng100% chi phí mana bình thường sau các modifier gốc, thay120%. Tổng mana còn phụ thuộc số lần cast; giữ yêu cầu mục tiêu và khả năng chi trả của game.
- Sửa cả tooltip cũ và V2 trong UI gốc, không ghi lại asset hoặc điểm kỹ năng. Tooltip lấy đúng nội bộ Meteor tree/node, không dựa vào tên hiển thị theo ngôn ngữ.

## Triển khai

Sau `MeteorSkillTree.updateMutator`, chỉ chỉnh `MeteorMutator.chanceFor100increasedArea` và `frostClawCastsOnImpact` của actor local offline. Game đã tính lại từ điểm trước đó nên không nhân chồng qua các lần load/respec. Native dùng khóa `Meteor Crit Chance` cho Extinction và `Meteor Frost Claw on Impact` cho Heat Flux; đối chiếu GlobalTreeData theo treeID/nodeID.

Native Meteor dùng trường0x178 cho RNG meteor lớn, sao chép số Frost Claw từ0x150 sang impact0x138. Impact gọi `BaseMana.getManaCost(AbilityID.frostClaw=708,...)` rồi nhân hằng số1.2, dùng cùng kết quả để kiểm tra mana và tiêu hao. Một scope thread-local giới hạn vào impact local có Heat Flux; chỉ điều chỉnh kết quả overload AbilityID của đúng mana component bằng chia1.2. Finalizer trả scope khi kết thúc/khi có lỗi. Không hook RNG chung, không sửa hằng số/asset, không hoàn mana sau khi cast, không sửa Frost Claw dùng trực tiếp.

Build0warning/error;34 fixture checks dùng nguồn patch thực với stub,116 compiled/native audit checks. Native audit kiểm tra target không phải shared no-op, đúng overload mana, đường RNG/copy count/mana consumer. Core giữ nguyên dev45, protocol43. Chưa nghiệm thu thi triển/render thực trong engine. Runtime report thêm `meteorNodes.TreeUpdates`, `ManaAdjustments`, `LastLargeMeteorChance`, `LastFrostClawCount`.

Khởi động lại game sau khi cập nhật cả cặp DLL. Dev45 đã được public; dev46 là bản local đang kiểm tra, chưa upload GitHub theo lượt sửa Meteor này. Không tự mở/đóng game hoặc thay trạng thái MelonLoader.
