using HarmonyLib;
using Il2Cpp;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class DisintegrateMovement
{
    internal const float SpeedMultiplier=.8f;
    internal static bool Local(Actor actor)=>OfflineToolkit.Enabled && actor!=null && actor.Pointer==PlayerFinder.getPlayerActor()?.Pointer;
    internal static bool Applies(AbilityMutator mutator)=>Local(mutator?.TryCast<DisintegrateMutator>()?.actor);
    internal static bool IsAbility(Ability ability)=>ability!=null && ability.Pointer==Ability.getAbility(AbilityID.disintegrate)?.Pointer;
    internal static bool Channel(Actor actor)
    {
        if(!Local(actor))return false;
        var use=actor.GetComponent<UsingAbility>();
        return use!=null && use.channelling && IsAbility(use.ability);
    }
}

// Use the native mobile-channel path (also used by mobile Focus). Keep native
// channel costs, damage, targeting and root checks intact.
[HarmonyPatch(typeof(AbilityMutator),nameof(AbilityMutator.getMovementDuringUse))]
internal static class OfflineDisintegrateMovementPatch
{private static void Postfix(AbilityMutator __instance,ref AbilityMovement __result){if(DisintegrateMovement.Applies(__instance))__result=AbilityMovement.Spin;}}
[HarmonyPatch]
internal static class OfflineDisintegrateMobileFlagsPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        foreach(string name in new[]{nameof(AbilityMutator.getStopMovementOnKeyRelease),nameof(AbilityMutator.DontStopAtDestination),nameof(AbilityMutator.getCanUpdateTargetWhileChannelling),nameof(AbilityMutator.getIncreasedMovementSpeedAffectsMovement)})
            yield return AccessTools.Method(typeof(AbilityMutator),name);
    }
    private static void Postfix(AbilityMutator __instance,ref bool __result){if(DisintegrateMovement.Applies(__instance))__result=true;}
}
[HarmonyPatch(typeof(AbilityMutator),nameof(AbilityMutator.getChangeDestinationWhileMoving))]
internal static class OfflineDisintegrateMouseSteeringPatch
{private static void Postfix(AbilityMutator __instance,ref bool __result){if(DisintegrateMovement.Applies(__instance))__result=false;}}
[HarmonyPatch(typeof(AbilityMutator),nameof(AbilityMutator.getUseSpeedAffectsMovement))]
internal static class OfflineDisintegrateCastSpeedPatch
{private static void Postfix(AbilityMutator __instance,ref bool __result){if(DisintegrateMovement.Applies(__instance))__result=false;}}
[HarmonyPatch(typeof(AbilityMutator),nameof(AbilityMutator.getBaseMovementDuration))]
internal static class OfflineDisintegrateDurationPatch
{private static void Postfix(AbilityMutator __instance,ref float __result){if(DisintegrateMovement.Applies(__instance))__result=.3f;}}
[HarmonyPatch(typeof(AbilityMutator),nameof(AbilityMutator.getMovementDurationPerUnitDistance))]
internal static class OfflineDisintegrateDistancePatch
{private static void Postfix(AbilityMutator __instance,ref float __result){if(DisintegrateMovement.Applies(__instance))__result=.05f;}}

[HarmonyPatch(typeof(SpeedManager),nameof(SpeedManager.updateSpeed))]
internal static class OfflineDisintegrateSpeedPatch
{
    // Original updateSpeed restores the normal stat-derived speed first, so
    // repeated updates cannot multiply the penalty more than once.
    private static void Postfix(SpeedManager __instance)
    {if(DisintegrateMovement.Channel(__instance.actor) && __instance.actor.navMeshAgent!=null)__instance.actor.navMeshAgent.speed*=DisintegrateMovement.SpeedMultiplier;}
}
[HarmonyPatch(typeof(MovementFromAbility),nameof(MovementFromAbility.startMoving))]
internal static class OfflineDisintegrateStartPatch
{
    private static void Prefix(MovementFromAbility __instance,ref UnityEngine.Vector3 _destination,UnityEngine.Vector3 _startPosition,
        Ability _ability,AbilityMovement _movementType,ref bool _changeDestinationWithMousePosition,
        ref bool shouldUsePlayerDestination,ref bool _dontStopAtDestination)
    {
        if(_movementType!=AbilityMovement.Spin || !DisintegrateSteering.IsLocalDisintegrate(__instance.actor,_ability))return;
        _destination=_startPosition;
        _changeDestinationWithMousePosition=false;shouldUsePlayerDestination=false;_dontStopAtDestination=true;
    }
    private static void Postfix(MovementFromAbility __instance,Ability _ability,AbilityMovement _movementType)
    {
        if(_movementType!=AbilityMovement.Spin || !DisintegrateSteering.IsLocalDisintegrate(__instance.actor,_ability))return;
        __instance.changeDestinationWithMousePosition=false;__instance.dontStopAtDestination=true;
        __instance.UpdateDestination(__instance.actor.position());
        __instance.actor.GetComponent<SpeedManager>()?.updateSpeed();
    }
}
[HarmonyPatch(typeof(UsingAbility),nameof(UsingAbility.stopUsingAbility))]
internal static class OfflineDisintegrateStopPatch
{
    private static void Prefix(UsingAbility __instance,out bool __state)=>__state=DisintegrateMovement.Local(__instance.GetComponent<Actor>()) && DisintegrateMovement.IsAbility(__instance.ability);
    private static void Finalizer(UsingAbility __instance,bool __state)
    {if(__state)__instance.GetComponent<SpeedManager>()?.updateSpeed();}
}
[HarmonyPatch(typeof(Il2CppLocalizationSystem.LocalizationAbilities),nameof(Il2CppLocalizationSystem.LocalizationAbilities.GetDescription))]
internal static class OfflineDisintegrateDescriptionPatch
{
    private static void Postfix(Ability ability,ref string __result)
    {if(OfflineToolkit.Enabled && DisintegrateMovement.IsAbility(ability))__result+="\nAim with the mouse and move with your movement controls while channelling at 80% of your normal movement speed.";}
}
