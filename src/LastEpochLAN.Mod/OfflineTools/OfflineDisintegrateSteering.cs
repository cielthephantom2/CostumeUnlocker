using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace LastEpochLAN.Mod.OfflineTools;

// Restore dev20/21 separation of aim from movement. No diagnostic counters,
// probe bypasses, transform writes or native damage/mana changes.
internal static class DisintegrateSteering
{
    [ThreadStatic] internal static int SkillCommandDepth;
    [ThreadStatic] internal static int AimUpdateDepth;
    internal static bool IsLocalDisintegrate(Actor actor,Ability ability)
        =>DisintegrateMovement.Local(actor) && DisintegrateMovement.IsAbility(ability);
    internal static MovementFromAbility ChannelMovement(MovingPlayer moving)
        =>ChannelMovement(moving?.usingAbilityPlayer);
    internal static MovementFromAbility ChannelMovement(UsingAbilityPlayer use)
    {
        if(use==null || !use.channelling)return null;
        var actor=PlayerFinder.getPlayerActor();
        if(actor?.stateController?.currentState?.Pointer!=use.Pointer ||
            !IsLocalDisintegrate(actor,use.ability))return null;
        var movement=use.MovementFromAbility;
        return movement!=null && movement.moving && movement.movementType==AbilityMovement.Spin?movement:null;
    }
    internal static bool CanSteer(Actor actor,bool checkMouse)
    {
        var input=EpochInputManager.instance;
        if(!Application.isFocused || Time.timeScale<=0 || Il2CppLE.UI.LoadingScreen.IsVisible ||
            input==null || input.forceDisableInput || input.cinematicActive || input.IsInputFieldActive ||
            actor==null || actor.isDying(false) || actor.stateController.numberOfRoots>0 ||
            UIMouseListener.WorldActionsDisabled() || checkMouse && UIMouseListener.MouseOverUI())return false;
        return !(PlayerFinder.getItemContainersManager()?.cursor?.GetContent()?.Count>0);
    }
}

[HarmonyPatch(typeof(MovingPlayerThumbstick),nameof(MovingPlayerThumbstick.SetInput))]
internal static class DisintegrateKeyboardMovementPatch
{
    private static void Prefix(MovingPlayerThumbstick __instance,Vector2 value,ref bool shouldEnterState,out bool __state)
    {
        var movement=DisintegrateSteering.ChannelMovement(__instance.usingAbilityPlayer);
        __state=movement!=null && (value.sqrMagnitude>0.0001f || __instance.input.sqrMagnitude>0.0001f);
        // Let native SetInput calculate the camera-relative WorldMovementDir,
        // but keep UsingAbility as the current state throughout the channel.
        if(movement!=null)shouldEnterState=false;
    }
    private static void Postfix(MovingPlayerThumbstick __instance,Vector2 value,bool __state)
    {
        if(!__state)return;
        var use=__instance.usingAbilityPlayer;
        var movement=DisintegrateSteering.ChannelMovement(use);
        if(movement==null)return;
        use.bufferedCommandHolder?.clearBufferedMovement();
        var actor=movement.actor;
        bool moving=value.sqrMagnitude>0.0001f && DisintegrateSteering.CanSteer(actor,false);
        var direction=moving?__instance.WorldMovementDir:Vector3.zero;
        if(!float.IsFinite(direction.x) || !float.IsFinite(direction.y) || !float.IsFinite(direction.z))return;
        // A short native NavMesh destination is renewed while a direction is
        // held; releasing the final movement key stops locomotion, not the beam.
        movement.UpdateDestination(actor.position()+direction*2f);
    }
}

[HarmonyPatch]
internal static class DisintegrateAimUpdateScopePatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()=>new[]
    {
        AccessTools.Method(typeof(UsingAbility),nameof(UsingAbility.TryUpdateTargetLocationIfChannelling)),
        AccessTools.Method(typeof(UsingAbility),nameof(UsingAbility.UpdateTargetLocation))
    };
    private static void Prefix(UsingAbility __instance,out bool __state)
    {
        var actor=PlayerFinder.getPlayerActor();
        __state=actor?.stateController?.usingAbility?.Pointer==__instance.Pointer &&
            DisintegrateSteering.IsLocalDisintegrate(actor,__instance.ability);
        if(__state)DisintegrateSteering.AimUpdateDepth++;
    }
    private static Exception Finalizer(bool __state,Exception __exception)
    {
        if(__state)DisintegrateSteering.AimUpdateDepth--;
        return __exception;
    }
}

[HarmonyPatch(typeof(MovementFromAbility),nameof(MovementFromAbility.UpdateDestination))]
internal static class DisintegrateAimIsNotMovementPatch
{
    private static bool Prefix(MovementFromAbility __instance)
    {
        if(DisintegrateSteering.AimUpdateDepth==0)return true;
        var use=PlayerFinder.getPlayerActor()?.stateController?.usingAbility;
        if(use?.movementFromAbility?.Pointer!=__instance.Pointer)return true;
        return false;
    }
}

[HarmonyPatch(typeof(UsingAbilityPlayer),nameof(UsingAbilityPlayer.UseAbilityCommand))]
internal static class DisintegrateSkillCommandScopePatch
{
    private static void Prefix(UsingAbilityPlayer __instance,Ability _ability,out bool __state)
    {
        var actor=PlayerFinder.getPlayerActor();
        __state=actor?.stateController?.usingAbility?.Pointer==__instance.Pointer &&
            DisintegrateSteering.IsLocalDisintegrate(actor,_ability);
        if(__state)DisintegrateSteering.SkillCommandDepth++;
    }
    private static Exception Finalizer(bool __state,Exception __exception)
    {
        if(__state)DisintegrateSteering.SkillCommandDepth--;
        return __exception;
    }
}

[HarmonyPatch(typeof(MovingPlayer),nameof(MovingPlayer.MoveToPoint))]
internal static class DisintegrateChannelExplicitMovePatch
{
    private static bool Prefix(MovingPlayer __instance,Vector3 point,bool dontMoveIfMouseOverUI,ref bool __result)
    {
        var movement=DisintegrateSteering.ChannelMovement(__instance);
        if(movement==null)return true;
        __result=false;
        // The native skill command also invokes MoveToPoint while RMB is held.
        // Only a separate movement command may steer the channel.
        if(DisintegrateSteering.SkillCommandDepth>0)
        {
            return false;
        }
        var actor=movement.actor;
        if(!DisintegrateSteering.CanSteer(actor,dontMoveIfMouseOverUI))return false;
        if(!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z))return false;
        // Native destination handling keeps NavMesh/collision checks. Do not
        // switch to Moving state, which would end Disintegrate's channel.
        movement.UpdateDestination(point);
        __result=true;
        return false;
    }
}

[HarmonyPatch(typeof(MovementFromAbility),nameof(MovementFromAbility.MovementStep))]
internal static class DisintegrateRespectsRootsPatch
{
    private static void Prefix(MovementFromAbility __instance)
    {
        var actor=__instance.actor;
        if(actor?.stateController==null || actor.stateController.numberOfRoots<=0 || !__instance.moving ||
            __instance.movementType!=AbilityMovement.Spin || !DisintegrateSteering.IsLocalDisintegrate(actor,__instance.currentAbility))return;
        __instance.UpdateDestination(actor.position());
    }
}
