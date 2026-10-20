using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace LastEpochLAN.Mod.OfflineTools;

// Use the game's channelled movement path. No asset writes, Transform updates,
// additional damage ticks, mana overrides or per-frame scene/actor scans.
internal static class OfflineDisintegrateMovement
{
    [ThreadStatic] internal static int SkillCommandDepth;
    [ThreadStatic] internal static int AimUpdateDepth;
    internal static long InitialDestinations,SuppressedSkillMoves,ExplicitMoves;
    internal static long SuppressedAimDestinations;
    internal static long KeyboardMoves,KeyboardStops;
    internal static object Diagnostics=>new {InitialDestinations,SuppressedSkillMoves,ExplicitMoves,SuppressedAimDestinations,KeyboardMoves,KeyboardStops};
    internal static bool Applies(AbilityMutator mutator)
    {
        var disintegrate=mutator?.TryCast<DisintegrateMutator>();
        if(disintegrate==null || !OfflineToolkit.Enabled)return false;
        var actor=disintegrate.actor;
        return actor!=null && actor.Pointer==PlayerFinder.getPlayerActor()?.Pointer;
    }
    internal static bool IsLocalDisintegrate(Actor actor,Ability ability)
    {
        if(actor==null || ability==null || !OfflineToolkit.Enabled || actor.Pointer!=PlayerFinder.getPlayerActor()?.Pointer)return false;
        // Mutators can live on subobjects. Resolve through the native registry,
        // as UsingAbility does, rather than assuming a component on the actor.
        var mutator=actor.stateController?.usingAbility?.mutatorManager?.getMutator(ability);
        return Applies(mutator);
    }
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
        if((!Application.isFocused && !NativeDisintegrateProbe.Running) || Time.timeScale<=0 || Il2CppLE.UI.LoadingScreen.IsVisible ||
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
        var movement=OfflineDisintegrateMovement.ChannelMovement(__instance.usingAbilityPlayer);
        __state=movement!=null && (value.sqrMagnitude>0.0001f || __instance.input.sqrMagnitude>0.0001f);
        // Let native SetInput calculate the camera-relative WorldMovementDir,
        // but keep UsingAbility as the current state throughout the channel.
        if(movement!=null)shouldEnterState=false;
    }
    private static void Postfix(MovingPlayerThumbstick __instance,Vector2 value,bool __state)
    {
        if(!__state)return;
        var use=__instance.usingAbilityPlayer;
        var movement=OfflineDisintegrateMovement.ChannelMovement(use);
        if(movement==null)return;
        use.bufferedCommandHolder?.clearBufferedMovement();
        var actor=movement.actor;
        bool moving=value.sqrMagnitude>0.0001f && OfflineDisintegrateMovement.CanSteer(actor,false);
        var direction=moving?__instance.WorldMovementDir:Vector3.zero;
        if(!float.IsFinite(direction.x) || !float.IsFinite(direction.y) || !float.IsFinite(direction.z))return;
        // A short native NavMesh destination is renewed while a direction is
        // held; releasing the final movement key stops locomotion, not the beam.
        movement.UpdateDestination(actor.position()+direction*2f);
        if(moving)OfflineDisintegrateMovement.KeyboardMoves++;
        else OfflineDisintegrateMovement.KeyboardStops++;
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
            OfflineDisintegrateMovement.IsLocalDisintegrate(actor,__instance.ability);
        if(__state)OfflineDisintegrateMovement.AimUpdateDepth++;
    }
    private static Exception Finalizer(bool __state,Exception __exception)
    {
        if(__state)OfflineDisintegrateMovement.AimUpdateDepth--;
        return __exception;
    }
}

[HarmonyPatch(typeof(MovementFromAbility),nameof(MovementFromAbility.UpdateDestination))]
internal static class DisintegrateAimIsNotMovementPatch
{
    private static bool Prefix(MovementFromAbility __instance)
    {
        if(NativeDisintegrateProbe.Running && NativeDisintegrateProbe.IgnoreAimFilter)return true;
        if(OfflineDisintegrateMovement.AimUpdateDepth==0)return true;
        var use=PlayerFinder.getPlayerActor()?.stateController?.usingAbility;
        if(use?.movementFromAbility?.Pointer!=__instance.Pointer)return true;
        OfflineDisintegrateMovement.SuppressedAimDestinations++;
        return false;
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
        if(OfflineDisintegrateMovement.Applies(__instance))__result=false;
    }
}

[HarmonyPatch(typeof(AbilityMutator),nameof(AbilityMutator.DontStopAtDestination))]
internal static class DisintegrateChannelAtDestinationPatch
{
    private static void Postfix(AbilityMutator __instance,ref bool __result)
    {
        // Reaching a movement destination must not terminate the held channel.
        if(OfflineDisintegrateMovement.Applies(__instance))__result=true;
    }
}

[HarmonyPatch(typeof(MovementFromAbility),nameof(MovementFromAbility.startMoving))]
internal static class DisintegrateChannelInitialDestinationPatch
{
    private static void Prefix(MovementFromAbility __instance,ref Vector3 _destination,Vector3 _startPosition,
        Ability _ability,AbilityMovement _movementType,ref bool _changeDestinationWithMousePosition,
        ref bool shouldUsePlayerDestination,ref bool _dontStopAtDestination)
    {
        if(_movementType!=AbilityMovement.Spin ||
            !OfflineDisintegrateMovement.IsLocalDisintegrate(__instance.actor,_ability))return;
        // The cast target aims the beam. It is not an implicit move command.
        _destination=_startPosition;
        _changeDestinationWithMousePosition=false;
        shouldUsePlayerDestination=false;
        _dontStopAtDestination=true;
    }
    private static void Postfix(MovementFromAbility __instance,Ability _ability,AbilityMovement _movementType)
    {
        if(_movementType!=AbilityMovement.Spin ||
            !OfflineDisintegrateMovement.IsLocalDisintegrate(__instance.actor,_ability))return;
        __instance.changeDestinationWithMousePosition=false;
        __instance.dontStopAtDestination=true;
        __instance.UpdateDestination(__instance.actor.position());
        OfflineDisintegrateMovement.InitialDestinations++;
    }
}

[HarmonyPatch(typeof(UsingAbilityPlayer),nameof(UsingAbilityPlayer.UseAbilityCommand))]
internal static class DisintegrateSkillCommandScopePatch
{
    private static void Prefix(UsingAbilityPlayer __instance,Ability _ability,out bool __state)
    {
        var actor=PlayerFinder.getPlayerActor();
        __state=actor?.stateController?.usingAbility?.Pointer==__instance.Pointer &&
            OfflineDisintegrateMovement.IsLocalDisintegrate(actor,_ability);
        if(__state)OfflineDisintegrateMovement.SkillCommandDepth++;
    }
    private static Exception Finalizer(bool __state,Exception __exception)
    {
        if(__state)OfflineDisintegrateMovement.SkillCommandDepth--;
        return __exception;
    }
}

[HarmonyPatch(typeof(MovingPlayer),nameof(MovingPlayer.MoveToPoint))]
internal static class DisintegrateChannelExplicitMovePatch
{
    private static bool Prefix(MovingPlayer __instance,Vector3 point,bool dontMoveIfMouseOverUI,ref bool __result)
    {
        var movement=OfflineDisintegrateMovement.ChannelMovement(__instance);
        if(movement==null)return true;
        __result=false;
        // The native skill command also invokes MoveToPoint while RMB is held.
        // Only a separate movement command may steer the channel.
        if(OfflineDisintegrateMovement.SkillCommandDepth>0)
        {
            OfflineDisintegrateMovement.SuppressedSkillMoves++;
            return false;
        }
        var actor=movement.actor;
        if(!OfflineDisintegrateMovement.CanSteer(actor,dontMoveIfMouseOverUI))return false;
        if(!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z))return false;
        // Native destination handling keeps NavMesh/collision checks. Do not
        // switch to Moving state, which would end Disintegrate's channel.
        movement.UpdateDestination(point);
        OfflineDisintegrateMovement.ExplicitMoves++;
        __result=true;
        return false;
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
