# Mesembria Prophecy charge — native inspection

GameAssembly SHA256: 323E9183F3BEE600B6C9E1C469218B9DE926E9044F2CA3D44FAE8A0EC9A91464.

Mesembria Targets dùng charge của mỗi ProphecySlot. Giới hạn99 completed charges/slot:

- CanCompleteCharge (RVA0x20B1B70): cmp byte ptr [rcx+0x40],0x63; setb al. 0x63=99.
- AddCharges (RVA0x20B16C0): kiểm tra [rbx+0x40]<0x63 trước mỗi lần tăng; tới99 thì dừng.
- AddFavor (RVA0x20B1790): cùng điều kiện<99 trước khi hoàn tất charge.
- GetChargesToUseWhenTriggered (RVA0x20B1D10):1charge khi có1;2 khi có2;3 khi có3–10;4 khi có11–19;5 khi có20–29;6 khi có30–99.

Dòng Requires at least one charge trong ảnh là điều kiện kích hoạt, không phải giới hạn lưu. Mod không patch các hàm charge này; lần thay đổi này chỉ tăng kích thước chấm minimap, không sửa charge hoặc phần thưởng Prophecy.

Phần thử nghiệm dùng hết charge đã được gỡ theo yêu cầu. Bản hiện tại giữ cơ chế charge, helper/UI và thông báo gốc nêu trên. Các thay đổi mod có trước thử nghiệm charge vẫn giữ.
