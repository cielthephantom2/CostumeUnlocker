using HarmonyLib;
using Il2Cpp;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class AggressiveMinionRange
{
    internal static long ExpandedAcquisitions, ExpandedTargetSearches;
    internal static bool Applies(Actor actor)
    {
        if (!OfflineToolkit.Enabled || actor == null) return false;
        var player = PlayerFinder.getPlayerActor();
        if (player == null || actor.Pointer == player.Pointer) return false;
        bool aggressive = false;
        // Include sub-minions, but never enemies or another player's summons.
        for (int depth = 0; depth < 8 && actor != null; depth++)
        {
            if (actor.Pointer == player.Pointer) return aggressive;
            aggressive |= actor.GetPlayerMinionStance() == PlayerMinionStance.Assassinate;
            var creator = actor.GetCreationReferences()?.creator;
            if (creator == null || creator.Pointer == actor.Pointer) return false;
            actor = creator;
        }
        return false;
    }
    internal static float DoubleRange(float value) => float.IsFinite(value) && value > 0 && value <= float.MaxValue / 2 ? value * 2 : value;
}

// Change only this acquisition call, restoring even if native invocation throws.
// Do not edit shared ability assets or the game's global aggro constants.
[HarmonyPatch(typeof(UsingAbility), nameof(UsingAbility.activateIfEnemyInRange))]
internal static class AggressiveMinionAcquirePatch
{
    internal sealed record Scope(StateController Controller, float Original);
    private static void Prefix(UsingAbility __instance, out Scope __state)
    {
        __state = null;
        var actor = __instance.getActor();
        if (!AggressiveMinionRange.Applies(actor) || actor.stateController == null) return;
        var controller = actor.stateController;
        float old = controller.aggroRangeModifier;
        if (!float.IsFinite(old) || old <= -1 || old > 1000000) return;
        __state = new(controller, old);
        controller.aggroRangeModifier = (1 + old) * 2 - 1;
        AggressiveMinionRange.ExpandedAcquisitions++;
    }
    private static Exception Finalizer(Exception __exception, Scope __state)
    {
        if (__state?.Controller != null) __state.Controller.aggroRangeModifier = __state.Original;
        return __exception;
    }
}

[HarmonyPatch(typeof(TargetFinder), nameof(TargetFinder.updateTarget))]
internal static class AggressiveMinionTargetPatch
{
    internal sealed record Scope(TargetFinder Finder, Il2CppSystem.Nullable<float> Original);
    private static void Prefix(TargetFinder __instance, bool targetAlly, bool onlyTargetSelf, out Scope __state)
    {
        __state = null;
        if (targetAlly || onlyTargetSelf || !AggressiveMinionRange.Applies(__instance.thisActor)) return;
        var distance = __instance.MaxDistanceFromSummoner;
        if (!distance.HasValue) return;
        float expanded = AggressiveMinionRange.DoubleRange(distance.Value);
        if (expanded == distance.Value) return;
        __state = new(__instance, distance);
        __instance.MaxDistanceFromSummoner = new Il2CppSystem.Nullable<float>(expanded);
        AggressiveMinionRange.ExpandedTargetSearches++;
    }
    private static Exception Finalizer(Exception __exception, Scope __state)
    {
        if (__state?.Finder != null) __state.Finder.MaxDistanceFromSummoner = __state.Original;
        return __exception;
    }
}
