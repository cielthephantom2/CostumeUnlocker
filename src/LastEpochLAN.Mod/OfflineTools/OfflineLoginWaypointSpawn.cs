using HarmonyLib;
using Il2CppLE.Services.Models;

namespace LastEpochLAN.Mod.OfflineTools;

// M_Rest has separate default-login and waypoint spawn markers. Match the same
// waypoint gate as native teleport, letting the native placer use its live
// transform/navmesh/respawn logic instead of hard-coding world coordinates.
[HarmonyPatch(typeof(EnterGameTransitionInfo),nameof(EnterGameTransitionInfo.CompatibleWithSpawn))]
internal static class OfflineLoginWaypointSpawnPatch
{
    internal const byte WaypointGate=0;
    private static void Postfix(EnterGameTransitionInfo __instance,byte gateId,TransitionType transitionType,ref bool __result)
    {
        if(OfflineToolkit.Enabled && __instance?.TargetScene==OfflineLoginDestinationPatch.Destination)
            __result=transitionType==TransitionType.Waypoint && gateId==WaypointGate;
    }
}
