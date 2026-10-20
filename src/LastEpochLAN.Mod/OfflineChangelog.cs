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
        $"• Tốc độ di chuyển +{OfflineBonusRules.MoveSpeed*100:0}%.\n"+
        $"• Diện tích ảnh hưởng của kỹ năng (AoE) +{OfflineBonusRules.Area*100:0}%.\n"+
        $"• Hồi mana +{OfflineBonusRules.ManaRegen*100:0}%; hồi máu +{OfflineBonusRules.HealthRegen*100:0}%.\n"+
        "• Unique có thể roll Legendary Potential luôn đạt tối thiểu 1 LP; giữ các kết quả LP cao hơn.\n"+
        "• Vàng trong Monolith: hệ số = corruption / 40, tối thiểu x1. Ví dụ: 1.000 corruption → x25 vàng.\n"+
        "• Echo of a World (Shade of Orobyss): Corruption nhận thêm x5, gồm Gaze và bonus bắt kịp. Ví dụ +10 → +50; giữ giới hạn timeline và lượt giảm Corruption.\n\n"+
        "<b>WOVEN POTENTIAL — REROLL LP</b>\n"+
        "• Từ 1 LP: 50% lên 2 LP; 50% xuống 0 LP.\n"+
        "• Từ 2 LP: 50% lên 3 LP; 50% xuống 1 LP.\n"+
        "• Từ 3 LP: 25% lên 4 LP; 75% về 1 LP.\n"+
        "• Giữ tiêu hao lượt và điều kiện dùng native. Mức tối thiểu 1 LP cho đồ rơi không chặn kết quả Woven về 0 LP.\n\n"+
        "<b>MẬT ĐỘ QUÁI</b>\n"+
        $"• Ngoài Monolith: mật độ sinh quái x{DensityRules.Multiplier(false,0):0}.\n"+
        "• Trong Monolith: hệ số = corruption / 100, tối thiểu x1. Ví dụ: 1.000 corruption → x10.\n"+
        "• Áp dụng khi tạo map mới. Trong co-op, Host quản lý quái của cả phòng.\n\n"+
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
        "• Mặc định ở góc trên trái, chỉ hiện khi ghi nhận sát thương; tự ẩn sau 5 giây không có sát thương mới.\n"+
        "• Hiển thị tối đa 8 kỹ năng; DPS, tổng 5 giây, tổng sát thương, đòn lớn nhất và số hit nằm cùng hàng.\n"+
        "• Có nút ẩn/hiện, chỉnh kích thước và đặt lại vị trí.\n"+
        "• F4: có bản cập nhật mới hiện màu đỏ; đang ở bản mới nhất hiện màu xanh lá.\n\n"+
        "<b>CO-OP — THAY ĐỔI GAMEPLAY HIỆN CÓ</b>\n"+
        "• Join chỉ vào party: giữ map và gameplay solo của Client; chỉ nút Teleport mới yêu cầu load sang gần Host. Mỗi Echo mới cần yêu cầu Teleport, không tự đi theo.\n"+
        "• F2: hòm chung riêng, dữ liệu lưu trên Host, dùng được khi khác map. Gửi/lấy trang bị hoặc Idol x1 có ID riêng; khóa món đang lưu để tránh thao tác trùng.\n"+
        "• Giao dịch gửi/lấy có nhật ký; mất mạng hoặc lỗi lưu giữ món trong escrow chờ xác minh, không tự cấp lại/hoàn lại. Hòm không dùng stash cá nhân.\n"+
        "• Đã thêm hiển thị minion/companion của đồng đội: model, di chuyển và animation theo máy chủ nhân, tối đa 64 triệu hồi/người. Cần kiểm nghiệm hai máy.\n"+
        "• Đã gỡ hệ thống co-op encounter cũ. Tương tác/thưởng encounter và tương tác map chung chưa được hỗ trợ trong bản thử nghiệm viết lại.\n"+
        "• Solo dùng encounter và nhận thưởng native; chưa xác nhận toàn bộ encounter trong game thật.\n"+
        "• NPC Client dùng đồng bộ animation và bộ đệm chuyển động theo Host; độ mượt vẫn đang được kiểm nghiệm.\n"+
        "• Teleport đến đồng đội ở map thường, thị trấn và hub.\n"+
        "• Monolith: Host chọn Echo thường, Client bấm Teleport để theo đúng lượt; đồng bộ mục tiêu, hoàn thành, đường nhận thưởng và trở về Rest.\n"+
        "• Quest Echo/boss quest, Woven/Tombs và lượt Arena/Dungeon/Pinnacle chưa được hỗ trợ trong luồng đi chung mới.\n"+
        "• Host nên đợi đồng đội nhận thưởng ở Rest trước khi mở Echo tiếp. Co-op Monolith hai máy vẫn cần kiểm nghiệm thực tế.";

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
