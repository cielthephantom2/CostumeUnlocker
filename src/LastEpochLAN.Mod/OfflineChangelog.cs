using LastEpochLAN.Core;
using UnityEngine;

namespace LastEpochLAN.Mod;

internal static class OfflineChangelog
{
    internal static bool Open;
    private static Vector2 scroll;
    private static GUIStyle bodyStyle;
    private static readonly string content=
        "<b>BONUS OFFLINE</b>\n"+
        "• Dev.69: bỏ giảm20% tốc độ Disintegrate; channel di chuyển ở tốc độ bình thường, giữ cách điều khiển cũ.\n"+
        "• Dev.68: Disintegrate dùng cách điều khiển cũ: chuột chỉ ngắm, WASD/lệnh đi riêng điều khiển di chuyển; giữ80% tốc độ và root native.\n"+
        "• Dev.67: sửa tọa độ login Traveler’s Rest bằng đúng Waypoint gate0 của teleport; dùng spawn marker native.\n"+
        "• Dev.66: Woven Enchanter vô hạn lượt, giữ phí Memory Amber; nhân vật offline đã qua phần mở đầu đăng nhập ở Traveler’s Rest.\n"+
        "• Dev.65: ẩn banner sent to the Forge/Storage khi nhặt shard, rune/glyph, vật liệu, key và Woven Echo; giữ Woven Enchanter10 lượt.\n"+
        "• Spirit Xylem: +3–4 All Skills, +10–16 Attunement, +3–6 Health/Mana mỗi ATT, +1 Spell Damage mỗi25 Maximum Mana; giữ hai hiệu ứng boss/rare. Disintegrate di chuyển khi channel với80% tốc độ; Arcane Ascendance khóa di chuyển gốc.\n"+
        "• Dev.56: phát hành Spirit Xylem/Stardust, sửa Lerinne/gold, x2 base regen; gỡ Default Meteor/Vilatria override và helper thừa. Protocol44.\n"+
        "• Dev.55: sửa mất đoạn Attunement Spirit Xylem do localization gốc ghi đè; gỡ Default Meteor save repair gây spam log.\n"+
        "• Dev.55: sửa menu đổi class Lerinne; gỡ tùy chọn Vilatria VFX; Stardust tối đa1 điểm, mỗi10 Maximum Mana cho1% more damage tất cả hệ.\n"+
        "• Dev.55 faction/pickup: CoF Rank7 x15, Rank10 boss loot +300%, Rank11 Rare→Exalted50%; The Woven Rank7 reforge30%, Rank9 Amber50%. Đã gỡ World Assist. Auto pickup bán kính5, thêm Woven Echoes;5 lượt/giây, tối đa5 món xét và1 thao tác/lượt.\n"+
        "• Dev.55 Stability: x2 cả Timeline Stability và Bonus Stability trong Echo Rewards mọi loại; Temporal Keystone x2, đồng bộ số hiển thị.\n"+
        "• Dev.55 bổ sung: Forgotten Knights Rank1 Egg of the Forgotten100%, Rank2 x2 Stability25%, Rank5 Temporal Keystone50%; sửa cả mô tả.\n"+
        "• Dev.55: Spirit Xylem có hai nhánh Attunement/Intelligence; chỉ nhánh thuộc tính cao hơn được hưởng skill level, thuộc tính và HP/MP hoặc spell damage. Đồng bộ tooltip.\n"+
        "• Dev.55: sửa nhặt gold toàn map sau0.5s; x2 base hồi mana/máu; Lerinne có Respecialize Class (giữ level/đồ/tiến trình, reset mastery/passive/skill).\n"+
        "• Dev.55: Tranquility của Arcane Ascendance không còn chặn hồi mana; giữ giảm mana cost và sửa cả tooltip.\n"+
        "• Dev.54: gỡ toàn bộ UI Shortcut, hotkey và xử lý mở bảng liên quan.\n"+
        "• Dev.53: chuyển hook Woven Imprint ra hàm chọn Echo bên ngoài để tránh lỗi native→managed khi chuyển Empowered sang Normal Monolith; giữ trọng số x2 và luồng chọn Echo native.\n"+
        "• Dev.52: Archmage (5 điểm): dưới300 max mana +5 spell damage,35% hoàn20%; từ300 +10,70% hoàn40%; từ1000 +15,100% hoàn60%. Sửa cả text và số tooltip theo max mana hiện tại.\n"+
        "• Dev.51: áp dụng Extinction33%/điểm và Heat Flux2 Frost Claw/điểm ngay khi cast Meteor; Arcane Ascendance cho phép di chuyển.\n"+
        "• Dev.50: gỡ chức năng DELETE TAB cho tab con Shared Stash. Giữ Delete Category gốc và dữ liệu hòm. Tooltip Meteor sửa trực tiếp số ở dòng gốc. Protocol44.\n"+
        "• Dev.46: Meteor Extinction33% meteor lớn mỗi điểm; Heat Flux2 Frost Claw mỗi điểm, mỗi lần tiêu hao100% mana Frost Claw thay120%.\n"+
        "• Dev.45: Sealed Riches 1 điểm =12% theo tooltip gốc6%; thay mức5.8% cũ. Warlord6%; Echoing hiển thị tối đa100%, vẫn phụ thuộc điểm thưởng/Corruption. Sealed áp dụng khi vào Echo mới.\n"+
        "• Dev.43: đo lại chiều rộng tên tab/category bằng font gốc trước khi căn layout Shared Stash; sửa chữ Unique/Set/Shared bị cắt.\n"+
        "• Dev.42: sửa tooltip Imprint ở giao diện cây cũ NodeTooltipManager: Warlord6%, Sealed5.8% mỗi điểm; bản trước chỉ sửa tooltip V2.\n"+
        "• Dev.41: bỏ Configure Category thừa ở chân Shared Stash; giữ chuột phải category. Khôi phục layout native cho tên/icon/nút+, giữ bề mặt bắt click trong suốt.\n"+
        "• Dev.39: sửa Shared Stash gọi hàm Unity bị strip khi cuộn tab. Tooltip Imprint khớp bonus x2: Sealed5.8%, Warlord6% mỗi điểm; node0/1 vẫn chưa kích hoạt. Protocol43.\n"+
        "• Dev.38: Ladder gốc hiển thị người đang trong phòng: level/XP, Arena, Corruption và Omen; cập nhật khi đổi nhân vật/rời phòng. Protocol43, cập nhật cả hai DLL trên mọi máy.\n"+
        "• Minion/companion ở stance tấn công tìm địch trong phạm vi x2; giữ tầm đánh gốc và stance bảo vệ.\n"+
        "• Dev.37: Shared Stash dùng Configure gốc đầy đủ: tên/icon/màu, Category và Priority theo Unique/rarity/type/class/level/faction/Corruption/LP/FP/WW/Touch. Duplicate chỉ sao chép cấu hình sang ngăn rỗng.\n"+
        "• Chuột phải tab hoặc category để cấu hình; + ở hàng category tạo nhóm. F2 tái sử dụng UI, nguồn hòm cá nhân được chuẩn bị ẩn. Protocol42, Host và mọi Client phải cập nhật cả hai DLL.\n"+
        "• Dev.34: F2 có6 ngăn chung, mỗi ngăn96 món; nút + thêm tối đa24 ngăn; Sort và Configure Tab (tên/màu/icon). Protocol41, cần cập nhật cả Host và Client.\n"+
        "• Dev.34: sáu node Imprint x2: Commoner/Sealed/Echoing/Warlord và trọng số Woven/Loomed.\n"+
        "• Dev.33: cập nhật tỷ lệ Woven Potential cho1–4 LP; đồ0 LP không được phép đưa vào máy.\n"+
        "• Bloodrage (Rage of Morditas): mỗi nấc nhận2 charge, tổng2/4/6/8/10; giữ mốc kill và tiêu hao native.\n"+
        "• Forge: chi phí Forging Potential giảm 50%, làm tròn xuống, tối thiểu 1 FP; craft miễn phí giữ 0. Rune of Shaping: 1–25 → 1–12 FP. Rune of Creation vẫn xóa toàn bộ FP; Weaver’s Will/Touch giữ nguyên.\n"+
        "• Glyph of Hope: 33% không tốn Forging Potential.\n"+
        "• Glyph of Despair: T1 100%, T2 70%, T3 50%, T4 33%; cùng tỷ lệ cho Rare và Exalted có T6/T7. Không mở seal T5 trở lên.\n"+
        $"• Tốc độ di chuyển +{OfflineBonusRules.MoveSpeed*100:0}%.\n"+
        $"• Diện tích ảnh hưởng của kỹ năng (AoE) +{OfflineBonusRules.Area*100:0}%.\n"+
        "• Hồi mana/máu: x2 giá trị cơ bản; giữ modifier từ đồ và passive.\n"+
        "• Unique có thể roll Legendary Potential luôn đạt tối thiểu 1 LP; giữ các kết quả LP cao hơn.\n"+
        "• Bỏ diminishing returns: EXP +1%/Corruption; LP roll bonus +0,07209%/Corruption; WW promotion roll bonus +0,08686%/Corruption; Echo Reward Rarity +0,89656%/Corruption.\n"+
        "• Vàng gốc tăng tuyến tính +0,12%/Corruption và giữ nguyên bonus mod corruption/40 (tối thiểu x1). 1.000 Corruption: x2,2 × x25 = x55 vàng quái. Tổng hai hệ số nhân chồng không phải đường thẳng.\n"+
        "• Amber standard drop: giữ hệ số trước 500 cho cả phần Corruption trên 500. LP tối đa4, WW tối đa28; xác suất roll không vượt100%.\n"+
        "• Echo of a World (Shade of Orobyss): thưởng gốc và Gaze x5. Catch-up chỉ bắt kịp timeline cao nhất, không nhân x5 để tránh Corruption tăng vọt. Giữ giới hạn timeline và lượt giảm Corruption.\n\n"+
        "<b>WOVEN POTENTIAL — REROLL LP</b>\n"+
        "• Từ 1 LP: 50% lên 2 LP; 50% xuống 0 LP.\n"+
        "• Từ 2 LP: 25% lên 3 LP; 75% xuống 1 LP.\n"+
        "• Từ 3 LP: 25% lên 4 LP; 75% về 2 LP.\n"+
        "• Từ 4 LP: 25% giữ 4 LP; 75% xuống 3 LP. Đồ 0 LP không được nhận.\n"+
        "• Giữ tiêu hao lượt và điều kiện dùng native. Mức tối thiểu 1 LP cho đồ rơi không chặn kết quả Woven về 0 LP.\n\n"+
        "<b>MẬT ĐỘ QUÁI</b>\n"+
        $"• Ngoài Monolith: mật độ sinh quái x{DensityRules.Multiplier(false,0):0}.\n"+
        "• Trong Monolith: hệ số = corruption / 100, tối thiểu x1. Ví dụ: 1.000 corruption → x10.\n"+
        "• Áp dụng khi tạo map mới. Mỗi máy giữ gameplay/quái riêng trong bản hiện tại.\n\n"+
        "<b>CIRCLE OF FORTUNE — THƯỞNG THEO RANK</b>\n"+
        $"• Rank 1: quái có {FortuneRules.EnemyDoubleChance*100:0}% xác suất rơi gấp đôi số vật phẩm.\n"+
        $"• Rank 3: khi Idol rơi, có {FortuneRules.IdolExtraChance*100:0}% xác suất rơi thêm {FortuneRules.AdditionalIdols} Idol.\n"+
        $"• Rank 5: {FortuneRules.EchoDoubleChance*100:0}% xác suất nhận gấp đôi phần thưởng vật phẩm Monolith Echo.\n"+
        $"• Rank 6: tăng {(FortuneRules.ExaltedMultiplier-1)*100:0}% xác suất item trở thành Exalted.\n"+
        "• Rank 7: Exiled Mage rơi gấp 15 số vật phẩm Experimental.\n"+
        $"• Rank 8: hệ số xác suất roll LP gốc của Unique là x{FortuneRules.LpMultiplier:0}; vẫn giữ tối thiểu 1 LP như trên.\n"+
        $"• Rank 10: boss tăng {FortuneRules.BossSpecificLootIncrease*100:0}% tỷ lệ rơi phần thưởng riêng.\n"+
        $"• Rank 11: đồ Rare có {FortuneRules.RareToExaltedChance*100:0}% cơ hội thành Exalted.\n"+
        "• Cần là thành viên CoF và mở rank tương ứng. Các rank khác giữ nguyên.\n\n"+
        "<b>PROPHECY</b>\n"+
        $"• Toàn bộ số lượng thưởng cơ sở x2: Rare x10 → x{FortuneRules.ProphecyCount(10)}, Unique x2 → x{FortuneRules.ProphecyCount(2)}…\n"+
        "• Số lượng trong ô chọn thưởng và tooltip khớp với số lượng cơ sở mới.\n"+
        "• Bonus từ charge, lens và các rank khác vẫn được tính thêm theo game.\n\n"+
        "<b>TIỆN ÍCH TRONG MOD SETTINGS</b>\n"+
        "• Nhân vật mới: tự hoàn thành campaign, lên ít nhất cấp 58 và đến End of Time khi bật tùy chọn. Nhân vật cũ được giữ nguyên.\n"+
        "• Đã bỏ nút và chức năng Retry character setup.\n"+
        "• Có thể bật/tắt: mở khóa lựa chọn cosmetic; tự nhặt vàng, key, potion, nguyên liệu và Ancient Bones.\n"+
        "• Auto pickup vàng/potion/Ancient Bones/key/material/Woven Echoes mới rơi trên toàn map sau0,5 giây; không yêu cầu di chuyển. Potion đầy vẫn để lại.\n"+
        "• Nhặt đồ từ xa bằng cách bấm vào loot; hiện mục tiêu, chấm quái trên minimap, mở sương mù và mở rộng zoom minimap.\n"+
        "• Zoom camera: phím dấu phẩy / dấu chấm, phạm vi 0,5–5x.\n\n"+
        "<b>DPS TRACKER & GIAO DIỆN</b>\n"+
        "• Mặc định ở góc trên trái, chỉ hiện khi ghi nhận sát thương; tự ẩn sau 15 giây không có sát thương mới.\n"+
        "• Hiển thị tối đa 8 kỹ năng; thống kê xếp dọc, nền trong suốt và thanh sát thương xanh lá.\n"+
        "• Có nút ẩn/hiện, chỉnh kích thước và đặt lại vị trí.\n"+
        "• F4: có bản cập nhật mới hiện màu đỏ; đang ở bản mới nhất hiện màu xanh lá.\n\n"+
        "<b>CO-OP — THAY ĐỔI GAMEPLAY HIỆN CÓ</b>\n"+
        "• Phòng Host/Join qua public IP + port trên Internet vẫn hoạt động. Mỗi máy chơi map riêng.\n"+
        "• F2: hòm chung riêng, dữ liệu lưu trên Host, dùng được khi khác map. Gửi/lấy trang bị hoặc Idol x1 có ID riêng; khóa món đang lưu để tránh thao tác trùng.\n"+
        "• Giao dịch gửi/lấy có nhật ký; mất mạng hoặc lỗi lưu giữ món trong escrow chờ xác minh, không tự cấp lại/hoàn lại. Hòm không dùng stash cá nhân.\n"+
        "• Loot Filter vẫn chia sẻ trong phòng; chỉnh/switch/tắt filter được cập nhật cho các máy khác.\n"+
        "• Tạm tắt đồng bộ quái/combat/HP/status/thưởng, nhân vật/pet/VFX, Teleport/Monolith và Encounter để giảm tải.\n"+
        "• Combat Assist, DPS và toàn bộ tiện ích cá nhân trong Mod Settings tiếp tục hoạt động.\n"+
        "• Dev.37 dùng protocol42; phòng Internet vẫn chỉ F2 + Loot Filters. Các bản protocol41 trở xuống không kết nối chung.";

    internal static void Show(){scroll=Vector2.zero;Open=true;}
    internal static void Draw(string version)
    {
        float width=Mathf.Max(1,Mathf.Min(700,Screen.width-32));
        float height=Mathf.Max(1,Mathf.Min(720,Screen.height-32));
        float x=(Screen.width-width)/2,y=(Screen.height-height)/2;
        GUI.Box(new Rect(x,y,width,height),"Changelog — "+version);
        GUI.Label(new Rect(x+18,y+30,width-36,24),"Bonus offline và các thay đổi gameplay hiện có");
        bodyStyle??=new GUIStyle(GUI.skin.label) {wordWrap=true,richText=true,fontSize=14};
        float textWidth=Mathf.Max(1,width-64);
        float textHeight=bodyStyle.CalcHeight(new GUIContent(content),textWidth);
        scroll=GUI.BeginScrollView(new Rect(x+18,y+62,width-36,Mathf.Max(1,height-118)),scroll,new Rect(0,0,textWidth,textHeight+12));
        GUI.Label(new Rect(0,0,textWidth,textHeight),content,bodyStyle);
        GUI.EndScrollView();
        if(GUI.Button(new Rect(x+18,y+height-42,width-36,27),"Quay lại Co-op")) Open=false;
    }
}
