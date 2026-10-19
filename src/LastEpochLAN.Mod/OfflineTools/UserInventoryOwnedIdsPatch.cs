using HarmonyLib;
using Il2CppEHG.Multiplayer;
using Il2CppLE.MicrotransactionSystem;

namespace LastEpochLAN.Mod.OfflineTools;

/// <summary>Offline: replaces the owned cosmetic ids with the registry catalog.</summary>
[HarmonyPatch(typeof(UserInventory), nameof(UserInventory.GetOwnedCosmeticIds))]
public class UserInventoryOwnedIdsPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref Il2CppSystem.Collections.Generic.List<string> __result)
    {
        if (GameplayEnvironment.IsOnlinePlay || !OfflineToolkit.Settings.UnlockCosmetics)
        {
            return;
        }

        Il2CppSystem.Collections.Generic.List<string> ids = OfflineCosmeticCatalog.OwnedIds();
        if (ids != null)
        {
            __result = ids;
        }
    }
}
