using HarmonyLib;
using Il2Cpp;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class EchoEnemyAggro
{
    internal const float Multiplier=3f;
    [ThreadStatic] internal static bool Searching;
    [ThreadStatic] internal static IntPtr CurrentController;
    internal static long AcquisitionCalls,ExpandedAcquisitions,ScaledBoxChecks;
    internal static long ActivationAttempts,NewActivations;
    internal static long OutsideEcho,NonEnemy,MissingController,DeadActor,MissingPlayer;
    internal static bool Applies(Actor actor)
    {
        if(!OfflineToolkit.Enabled)return false;
        if(actor==null || actor.stateController==null){MissingController++;return false;}
        if(actor.isPlayerActor){NonEnemy++;return false;}
        if(actor.isDying(false)){DeadActor++;return false;}
        var zone=MonolithZoneManager.instance;
        if(zone==null || !zone.gameObject.scene.isLoaded){OutsideEcho++;return false;}
        var player=PlayerFinder.getPlayerActor();
        if(player==null){MissingPlayer++;return false;}
        if(actor.Pointer==player.Pointer){NonEnemy++;return false;}
        // Gameplay actors may live in separate pooled/persistent scenes.
        // Their native alignment and the live echo manager determine scope.
        var local=player.GetAlignment();
        if(local==null || actor.GetAlignment()?.IsFoe(local)!=true){NonEnemy++;return false;}
        // Do not reject autoCombatDisabled here: native acquisition/activation
        // still owns that gate. We only change range if native calls this path.
        return true;
    }
    internal static object Read()=>new {multiplier=Multiplier,AcquisitionCalls,ExpandedAcquisitions,ScaledBoxChecks,ActivationAttempts,NewActivations,
        rejected=new {OutsideEcho,NonEnemy,MissingController,DeadActor,MissingPlayer},
        scope="offline hostile native actors in a loaded echo; no scene-equality/minimap/autoCombatDisabled prefilter"};

}

[HarmonyPatch(typeof(UsingAbility),nameof(UsingAbility.Activate))]
internal static class OfflineEchoEnemyAggroActivationTracePatch
{
    private static void Prefix(UsingAbility __instance,out bool __state)
    {
        __state=EchoEnemyAggro.Searching && __instance?.controller?.Pointer==EchoEnemyAggro.CurrentController && !__instance.activated;
        if(__state)EchoEnemyAggro.ActivationAttempts++;
    }
    private static void Postfix(UsingAbility __instance,bool __state)
    {if(__state && __instance.activated)EchoEnemyAggro.NewActivations++;}
}

[HarmonyPatch(typeof(UsingAbility),nameof(UsingAbility.activateIfEnemyInRange))]
internal static class OfflineEchoEnemyAggroScopePatch
{
    internal readonly record struct Scope(bool Previous,IntPtr PreviousController,StateController Controller,float Original);
    private static void Prefix(UsingAbility __instance,out Scope __state)
    {
        __state=new(EchoEnemyAggro.Searching,EchoEnemyAggro.CurrentController,null,0);
        EchoEnemyAggro.Searching=false;EchoEnemyAggro.CurrentController=IntPtr.Zero;EchoEnemyAggro.AcquisitionCalls++;
        var actor=__instance?.getActor();
        if(!EchoEnemyAggro.Applies(actor))return;
        var controller=actor.stateController;float old=controller.aggroRangeModifier;
        if(__state.Previous && __state.PreviousController==controller.Pointer)
        {EchoEnemyAggro.Searching=true;EchoEnemyAggro.CurrentController=controller.Pointer;return;}
        if(!float.IsFinite(old) || old<=-1 || old>(float.MaxValue/EchoEnemyAggro.Multiplier)-1)return;
        __state=new(__state.Previous,__state.PreviousController,controller,old);
        // Scale the caller's range too: its separate vertical-distance formula
        // uses this value after the horizontal box returns.
        controller.aggroRangeModifier=(1+old)*EchoEnemyAggro.Multiplier-1;
        EchoEnemyAggro.Searching=true;EchoEnemyAggro.CurrentController=controller.Pointer;EchoEnemyAggro.ExpandedAcquisitions++;
    }
    private static Exception Finalizer(Scope __state,Exception __exception)
    {
        try{if(__state.Controller!=null)__state.Controller.aggroRangeModifier=__state.Original;}
        finally{EchoEnemyAggro.Searching=__state.Previous;EchoEnemyAggro.CurrentController=__state.PreviousController;}
        return __exception;
    }
}

[HarmonyPatch(typeof(UsingAbility),nameof(UsingAbility.DisplacementWithinAggroBox))]
internal static class OfflineEchoEnemyAggroBoxPatch
{
    private static void Prefix(ref float __0,ref float __1,ref float __2)
    {
        if(!EchoEnemyAggro.Searching || !float.IsFinite(__0) || !float.IsFinite(__1) || !float.IsFinite(__2))return;
        // Native has one unscaled directional edge. Normalize all coordinates
        // so every horizontal edge expands x3. Undo the caller's multiplier
        // here to avoid x9 on the other edges; preserve its native modifiers.
        __0/=EchoEnemyAggro.Multiplier;__1/=EchoEnemyAggro.Multiplier;__2/=EchoEnemyAggro.Multiplier;
        EchoEnemyAggro.ScaledBoxChecks++;
    }
}
