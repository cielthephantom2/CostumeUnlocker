using HarmonyLib;
using Il2Cpp;
using Il2CppEHG.Multiplayer;
using Il2CppLE.Services.Cosmetics;

namespace LastEpochLAN.Mod.OfflineTools;

/// <summary>Offline: the cosmetic flyouts skip junk registry entries.</summary>
[HarmonyPatch(typeof(CosmeticItemSlot), nameof(CosmeticItemSlot.Matches))]
public class CosmeticItemSlotMatchesPatch
{
    [HarmonyPostfix]
    private static void Postfix(Cosmetic c, ref bool __result)
    {
        if(MeteorDefaultCosmetic.IsDefault(c))
        {__result=__result && MeteorDefaultCosmetic.Enabled;return;}
        if (GameplayEnvironment.IsOnlinePlay || !OfflineToolkit.Settings.UnlockCosmetics || !__result || c == null)
        {
            return;
        }

        __result = !OfflineCosmeticCatalog.IsJunk(c);
    }
}
