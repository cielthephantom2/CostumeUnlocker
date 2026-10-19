using HarmonyLib;

namespace LastEpochLAN.Mod;

// Reuses the native right-click menu row, prefab, icon and click/close behavior.
[HarmonyPatch(typeof(Il2Cpp.RightClickMenu), nameof(Il2Cpp.RightClickMenu.SetButtons))]
internal static class NativeLanGiftMenuPatch
{
    internal static long Added;
    internal static string Error = "";
    private static bool Prefix(Il2Cpp.RightClickMenu __instance, Il2CppLE.Services.Models.UserIdentity userIdentity)
    {
        if (LanGift.Active?.FindRecipient(userIdentity) == null) return true;
        try
        {
            var icons = Il2Cpp.SocialMenuSpriteBank.get();
            if (icons == null) throw new InvalidOperationException("Native social menu icons unavailable.");
            // Native SetButtons queries online friend/block state even in an
            // offline campaign. LAN peers need the native Gift row directly.
            __instance.ClearButtons();
            __instance.AddButton(new Action(() => __instance.GiftItem()), "Gift Item", icons.SendGiftIconSoftRef, true);
            Added++; Error = "";
        }
        catch (Exception ex) { Error = ex.Message; MelonLoader.MelonLogger.Warning("LAN Gift menu: " + Error); }
        return false;
    }
}

[HarmonyPatch(typeof(Il2Cpp.RightClickMenu), nameof(Il2Cpp.RightClickMenu.GiftItem))]
internal static class NativeLanGiftMenuActionPatch
{
    private static bool Prefix(Il2Cpp.RightClickMenu __instance)
    {
        var player = LanGift.Active?.FindRecipient(__instance.UserIdentity);
        if (player == null) return true;
        LanGift.Active.SelectRecipient(player); return false;
    }
}

[HarmonyPatch(typeof(Il2Cpp.SocialManager), nameof(Il2Cpp.SocialManager.GiftItem))]
internal static class NativeLanGiftSocialActionPatch
{
    private static bool Prefix(Il2CppLE.Services.Models.UserIdentity userIdentity)
    {
        var player = LanGift.Active?.FindRecipient(userIdentity);
        if (player == null) return true;
        LanGift.Active.SelectRecipient(player); return false;
    }
}
