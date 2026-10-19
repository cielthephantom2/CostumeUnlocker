using HarmonyLib;
using Il2Cpp;
using Il2CppCysharp.Threading.Tasks;
using Il2CppEHG.Multiplayer;
using Il2CppLE.Networking.Cosmetics;

namespace LastEpochLAN.Mod.OfflineTools;

/// <summary>Offline: answers the owned-cosmetics query from the registry catalog.</summary>
[HarmonyPatch(typeof(CosmeticsManager), nameof(CosmeticsManager.GetOwnedCosmetics))]
public class CosmeticsManagerOwnedCosmeticsPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        ref CosmeticsManager __instance,
        ref UniTask<Il2CppSystem.Collections.Generic.List<string>> __result
    )
    {
        if (GameplayEnvironment.IsOnlinePlay || !OfflineToolkit.Settings.UnlockCosmetics)
        {
            return;
        }

        if (Il2Cpp.PlayerFinder.getPlayerActor() != null)
        {
            __instance.player = Il2Cpp.PlayerFinder.getPlayerActor().gameObject;
        }

        UniTask<Il2CppSystem.Collections.Generic.List<string>> task =
            OfflineCosmeticCatalog.OwnedIdsTask();
        if (task != null)
        {
            __result = task;
        }
    }
}
