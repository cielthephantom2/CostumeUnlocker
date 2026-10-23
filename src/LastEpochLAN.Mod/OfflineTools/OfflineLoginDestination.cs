using HarmonyLib;
using Il2CppLE.Data;

namespace LastEpochLAN.Mod.OfflineTools;

// Select the login map before native scene validation/loading, rather than
// entering the saved town and performing a second transition afterwards.
[HarmonyPatch(typeof(CharacterDataExtensions),nameof(CharacterDataExtensions.GetInitialScene))]
internal static class OfflineLoginDestinationPatch
{
    internal const string Destination="M_Rest";
    private static void Postfix(CharacterData characterData,ref string __result)
    {
        // Keep the native introduction for a fresh character. The separate
        // new-character setup still owns its campaign-completion transition.
        if(OfflineToolkit.Enabled && characterData?.IsOffline==true && characterData.ReachedTown && !string.IsNullOrEmpty(__result))
            __result=Destination;
    }
}
