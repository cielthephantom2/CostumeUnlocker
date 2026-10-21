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
        "• Dev.32: sửa Assist bị pause sau bật/tắt DPS rồi đóng F4; Bloodrage UI hiển thị2/10 đến10/10.\n"+
        "• Bloodrage (Rage of Morditas): mỗi nấc nhận2 charge, tổng2/4/6/8/10; giữ mốc kill và tiêu hao native.\n"+
        "• Glyph of Hope: 33% không tốn Forging Potential.\n"+
        "• Glyph of Despair: T1 100%, T2 70%, T3 50%, T4 33%; cùng tỷ lệ cho Rare và Exalted có T6/T7. Không mở seal T5 trở lên.\n"+
        $"• Tốc độ di chuyển +{OfflineBonusRules.MoveSpeed*100:0}%.\n"+
        $"• Diện tích ảnh hưởng của kỹ năng (AoE) +{OfflineBonusRules.Area*100:0}%.\n"+
        $"• Hồi mana +{OfflineBonusRules.ManaRegen*100:0}%; hồi máu +{OfflineBonusRules.HealthRegen*100:0}%.\n"+
        "• Unique có thể roll Legendary Potential luôn đạt tối thiểu 1 LP; giữ các kết quả LP cao hơn.\n"+
        "• Bỏ diminishing returns: EXP +1%/Corruption; LP roll bonus +0,07209%/Corruption; WW promotion roll bonus +0,08686%/Corruption; Echo Reward Rarity +0,89656%/Corruption.\n"+
        "• Vàng gốc tăng tuyến tính +0,12%/Corruption và giữ nguyên bonus mod corruption/40 (tối thiểu x1). 1.000 Corruption: x2,2 × x25 = x55 vàng quái. Tổng hai hệ số nhân chồng không phải đường thẳng.\n"+
        "• Amber standard drop: giữ hệ số trước 500 cho cả phần Corruption trên 500. LP tối đa4, WW tối đa28; xác suất roll không vượt100%.\n"+
        "• Echo of a World (Shade of Orobyss): thưởng gốc và Gaze x5. Catch-up chỉ bắt kịp timeline cao nhất, không nhân x5 để tránh Corruption tăng vọt. Giữ giới hạn timeline và lượt giảm Corruption.\n\n"+
        "<b>WOVEN POTENTIAL — REROLL LP</b>\n"+
        "• Từ 1 LP: 50% lên 2 LP; 50% xuống 0 LP.\n"+
        "• Từ 2 LP: 50% lên 3 LP; 50% xuống 1 LP.\n"+
        "• Từ 3 LP: 25% lên 4 LP; 75% về 1 LP.\n"+
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
        "• Rank 7: Exiled Mage rơi gấp 3 số vật phẩm Experimental.\n"+
        $"• Rank 8: hệ số xác suất roll LP gốc của Unique là x{FortuneRules.LpMultiplier:0}; vẫn giữ tối thiểu 1 LP như trên.\n"+
        "• Cần là thành viên CoF và mở rank tương ứng. Các rank khác giữ nguyên.\n\n"+
        "<b>PROPHECY</b>\n"+
        $"• Toàn bộ số lượng thưởng cơ sở x2: Rare x10 → x{FortuneRules.ProphecyCount(10)}, Unique x2 → x{FortuneRules.ProphecyCount(2)}…\n"+
        "• Số lượng trong ô chọn thưởng và tooltip khớp với số lượng cơ sở mới.\n"+
        "• Bonus từ charge, lens và các rank khác vẫn được tính thêm theo game.\n\n"+
        "<b>TIỆN ÍCH TRONG MOD SETTINGS</b>\n"+
        "• Nhân vật mới: tự hoàn thành campaign, lên ít nhất cấp 58 và đến End of Time khi bật tùy chọn. Nhân vật cũ được giữ nguyên.\n"+
        "• Đã bỏ nút và chức năng Retry character setup.\n"+
        "• Có thể bật/tắt: mở khóa lựa chọn cosmetic; tự nhặt vàng, key, potion, nguyên liệu và Ancient Bones.\n"+
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
        "• Dev.32 giữ protocol40; phòng Internet vẫn chỉ F2 + Loot Filters. Bản dev.27/protocol36 không kết nối chung.";

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
