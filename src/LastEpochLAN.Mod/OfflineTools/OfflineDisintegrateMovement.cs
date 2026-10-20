using HarmonyLib;
using Il2Cpp;

namespace LastEpochLAN.Mod.OfflineTools;

// Use the game's channelled movement path. No asset writes, Transform updates,
// additional damage ticks, mana overrides or per-frame scene/actor scans.
internal static class OfflineDisintegrateMovement
{
    internal static bool Applies(AbilityMutator mutator)
    {
        var disintegrate=mutator?.TryCast<DisintegrateMutator>();
        if(disintegrate==null || !OfflineToolkit.Enabled)return false;
        var actor=disintegrate.actor;
        return actor!=null && actor.Pointer==PlayerFinder.getPlayerActor()?.Pointer;
    }
}

[HarmonyPatch(typeof(AbilityMutator),nameof(AbilityMutator.getMovementDuringUse))]
internal static class DisintegrateChannelMovementPatch
{
    private static void Postfix(AbilityMutator __instance,ref AbilityMovement __result)
    {
        if(OfflineDisintegrateMovement.Applies(__instance))__result=AbilityMovement.Spin;
    }
}

[HarmonyPatch(typeof(AbilityMutator),nameof(AbilityMutator.getChangeDestinationWhileMoving))]
internal static class DisintegrateChannelSteeringPatch
{
    private static void Postfix(AbilityMutator __instance,ref bool __result)
    {
        if(OfflineDisintegrateMovement.Applies(__instance))__result=true;
    }
}

[HarmonyPatch(typeof(AbilityMutator),nameof(AbilityMutator.getStopMovementOnKeyRelease))]
internal static class DisintegrateChannelReleasePatch
{
    private static void Postfix(AbilityMutator __instance,ref bool __result)
    {
        if(OfflineDisintegrateMovement.Applies(__instance))__result=true;
    }
}

[HarmonyPatch(typeof(AbilityMutator),nameof(AbilityMutator.getIncreasedMovementSpeedAffectsMovement))]
internal static class DisintegrateChannelMoveSpeedPatch
{
    private static void Postfix(AbilityMutator __instance,ref bool __result)
    {
        if(OfflineDisintegrateMovement.Applies(__instance))__result=true;
    }
}

[HarmonyPatch(typeof(AbilityMutator),nameof(AbilityMutator.getUseSpeedAffectsMovement))]
internal static class DisintegrateChannelCastSpeedPatch
{
    private static void Postfix(AbilityMutator __instance,ref bool __result)
    {
        if(OfflineDisintegrateMovement.Applies(__instance))__result=false;
    }
}
