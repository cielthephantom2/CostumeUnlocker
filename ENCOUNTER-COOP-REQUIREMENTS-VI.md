# Yêu cầu Encounter co-op — 20/10/2026

Đây là phạm vi người dùng vừa xác nhận, dùng cho công việc tiếp theo trên nhánh
`encounter-rewrite`. Đây là đặc tả đích, không phải báo cáo tính năng đã hoạt động.
Trạng thái triển khai nền và các giới hạn QA xem `ENCOUNTER-REWRITE-VI.md` và
`D:\New folder\LastEpochLAN\HANDOVER-COOP-CURRENT.md`.

## Phạm vi đã chốt

| Encounter | Trạng thái chung Host/Client | Trạng thái và thưởng riêng |
|---|---|---|
| Nemesis | Vị trí, hiện diện và tương tác; chiến đấu dùng thế giới do Host quản lý | Mỗi người chọn Challenge/Empower/Banish riêng, xử lý trứng/item của mình và nhận thưởng riêng |
| Rift Beast | Vị trí, tương tác và map, boss, NPC | Mỗi người nhận thưởng riêng; không mở rộng sang phản chiếu toàn bộ trạng thái nhân vật hoặc hệ thống riêng ngoài phạm vi này |
| Omen Window | Vị trí, tương tác kích hoạt, boss và NPC | Mỗi người nhận thưởng riêng |
| Rage of Morditas Blood Crystal | Vị trí, tương tác kích hoạt, boss, NPC và vùng kích hoạt | Mỗi người nhận thưởng riêng |

Người dùng đã xác nhận riêng: **Nemesis mỗi người chọn riêng**, không dùng một
lựa chọn Challenge/Empower/Banish chung cho cả party. Lựa chọn và item của một
nhân vật không được tự sửa tiến trình, tiêu thụ trứng/item hoặc quyết định thưởng
của nhân vật khác. Cách điều phối các lựa chọn khác nhau vào trận đánh chung phải
được triển khai và kiểm chứng; lõi một phase/một reservation hiện tại chưa biểu
diễn đủ quy tắc này. Không được tự chuyển yêu cầu thành cả nhóm chọn chung.

## Ràng buộc triển khai

- Host sở hữu encounter trong map/run của Host, xác nhận thao tác, quản lý phase,
  sinh boss/NPC và quyết định hoàn thành. Client gửi yêu cầu tương tác và nhận
  presentation/trạng thái chung; không tự chạy thêm encounter gameplay độc lập.
- Chỉ người thực sự tham gia run của Host được tham gia encounter. Join party
  khi đang ở map riêng không tự nhận encounter hoặc phần thưởng của Host.
- Dùng encounter ID gắn với instance/epoch/map/run và ID ổn định cho boss/NPC.
  Vị trí và hành động tham chiếu cùng một encounter, không ghép theo khoảng cách
  hoặc thứ tự spawn một cách mơ hồ.
- Rift Beast chỉ đồng bộ map, boss, NPC và phần vị trí/tương tác đã yêu cầu;
  Omen Window có thao tác kích hoạt; Blood Crystal cần trạng thái và hình học
  vùng kích hoạt từ Host để Client thấy đúng vùng và không kích hoạt hai lần.
- Nemesis cần trạng thái lượt/hành động và dữ liệu item riêng theo nhân vật,
  bên cạnh trạng thái encounter/chiến đấu chung. Không sao chép inventory hoặc
  danh sách item Nemesis của Host cho Client.
- Mỗi người có quyền nhận thưởng riêng theo kết quả và điều kiện của mình;
  không chia một lần drop chung, không sao chép nguyên loot Host cho cả nhóm.
  Mỗi quyền thưởng cần receipt bền vững, xác minh native hoàn tất và lưu trước
  khi ACK. Callback hoặc packet lặp không cấp thêm lượt thưởng.
- Không khôi phục generic callback replay, mirror field tùy ý, hook tương tác
  global hoặc nhét reward ACK vào kênh skill. Adapter phải theo từng family,
  vòng đời phòng và instance sở hữu; solo không bị encounter LAN can thiệp.
- Chưa chốt quy tắc thưởng khi chết, vào giữa trận hoặc mất kết nối ở thời điểm
  hoàn thành. Phải đối chiếu native từng family trước khi đóng quy tắc; bộ lọc
  `!Dead` tại thời điểm hoàn thành trong lõi hiện tại chưa phải yêu cầu đã chốt.

## Khoảng cách với source hiện tại

- `EncounterHost` mới là state machine managed, chưa nối transport, native
  adapters, Client presentation hoặc private reward executor vào gameplay.
- Omen Window hiện có action rỗng và bài test tự kích hoạt qua `NativeStarted`;
  cần hỗ trợ đường tương tác kích hoạt được Host xác nhận theo yêu cầu mới.
- Một reservation/phase chung của Nemesis chưa đủ cho lựa chọn riêng từng người.
- Core hiện chưa mang tham chiếu boss/NPC, dữ liệu map Rift Beast hay vùng
  kích hoạt Blood Crystal trong snapshot encounter.
- Kiểm thử gọi riêng hàm native thưởng Omen không chứng minh luồng encounter
  hoặc thưởng riêng đã hoạt động trên hai máy.

## Tiêu chí nghiệm thu

1. Solo: mỗi family hoạt động đúng trước và sau khi Host/Join/Leave; không có
   controller hoặc callback LAN can thiệp encounter solo.
2. Trên hai máy: cùng vị trí/instance, cả Host và Client thao tác được; thao tác
   đồng thời chỉ tạo đúng kết quả hợp lệ, không nhân boss/NPC hoặc chạy hai trận.
3. Nemesis: Host và Client chọn khác nhau, có thể có tiến trình và trứng/item
   khác nhau; kiểm cả Challenge, Empower và Banish; không thay đổi item/lượt
   của người khác. Kiểm cách điều phối trận chung khi lựa chọn khác nhau.
4. Rift Beast: đúng map của Host và cùng boss/NPC; Client không tự sinh một
   encounter riêng. Kiểm travel vào/ra map đặc biệt của family.
5. Omen Window: Client kích hoạt được qua Host; boss/NPC và phase thống nhất;
   mỗi người nhận đúng phần thưởng riêng, giữ các thuộc tính native của loot.
6. Blood Crystal: kiểm cả thao tác và vùng kích hoạt ở hai máy, boss/NPC sinh
   một lần, Client thấy vùng đúng với Host và mỗi người nhận thưởng riêng.
7. Thử late join, reconnect, đổi map, packet cũ/lặp và callback hoàn thành lặp;
   không tái kích hoạt instance cũ hoặc phát lại thưởng. Crash khi đang thưởng
   cần giữ trạng thái chưa rõ để đối soát, không tự cấp lại.
8. Báo cáo tách managed/transport tests, native theo từng family và nghiệm thu
   hai máy thật. Chỉ đánh dấu hoạt động khi có bằng chứng tương ứng.

## Trạng thái thay đổi này

Đã bắt đầu source đợt 1: transport riêng, private Nemesis turns và Host adapter
driver; xem `ENCOUNTER-IMPLEMENTATION-2026-10-20-VI.md`. Chưa nối native gameplay,
chưa build/test, cài DLL hoặc phát hành. Người dùng yêu cầu test sau khi bố cục
hoàn thiện vì game đang mở.
Không có quyền push/phát hành bản tiếp theo từ yêu cầu này.
