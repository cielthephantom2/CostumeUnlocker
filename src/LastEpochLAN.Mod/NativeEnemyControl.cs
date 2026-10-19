using HarmonyLib;
using LastEpochLAN.Core;
namespace LastEpochLAN.Mod;

internal static class NativeEnemyControl
{
    private static readonly Dictionary<IntPtr,(int Freeze,bool Stun)> shown = new();
    internal static void Reset() => shown.Clear();
    internal static bool IsStunned(Il2Cpp.Actor actor) => actor.stateController?.stunned is { } stun &&
        actor.stateController.currentState?.Pointer == stun.Pointer;
    internal static int FreezeMaterial(Il2Cpp.Actor actor)
    {
        var stun = actor.stateController?.stunned;
        if (stun == null || !IsStunned(actor)) return 0;
        return stun.frozen ? 1 : stun.timeLocked ? 2 : stun.petrified ? 3 : 0;
    }
    internal static void ApplyRequest(Il2Cpp.Actor actor, HitRequest request)
    {
        var stun = actor.stateController?.stunned;
        if (stun == null) return;
        var immunity = Il2Cpp.Stunned.IgnoreImmunityType.None;
        switch (request.ControlType)
        {
            case 0: stun.stun(request.ControlDuration, immunity); break;
            case 1: stun.freeze(request.ControlDuration, request.ControlAngle, immunity); break;
            case 2: stun.timeLock(request.ControlDuration, immunity); break;
            case 3: stun.petrify(request.ControlDuration, immunity); break;
        }
    }
    internal static void Present(Il2Cpp.Actor actor, EnemyState state, EnemyState previous)
    {
        var sync = actor.GetActorSync();
        if (sync?.actorVisuals == null) return;
        shown.TryGetValue(actor.Pointer,out var old);
        if (old.Freeze != state.FreezeMaterial)
        {
            if (state.FreezeMaterial == 0) sync.ReceiveUnfreeze();
            else sync.ReceiveFreeze((Il2Cpp.RendererManager.FreezeMaterialType)state.FreezeMaterial, 30, 0);
        }
        bool stunVfx = state.Stunned && state.FreezeMaterial == 0 && !state.Dead;
        bool before = old.Stun;
        if (stunVfx != before) { if (stunVfx) sync.ReceiveStartStunVFX(); else sync.ReceiveStopStunVFX(); }
        shown[actor.Pointer]=(state.FreezeMaterial,stunVfx);
    }
}
[HarmonyPatch]
internal static class NativeEnemyControlPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods() => new[] {"stun","freeze","timeLock","petrify"}
        .Select(name => AccessTools.Method(typeof(Il2Cpp.Stunned), name));
    private static bool Prefix(Il2Cpp.Stunned __instance, float duration, System.Reflection.MethodBase __originalMethod, object[] __args)
    {
        var actor = __instance.getActor();
        int type = __originalMethod.Name switch { "stun"=>0, "freeze"=>1, "timeLock"=>2, _=>3 };
        return WorldSynchronizer.Active?.ForwardControl(actor, type, duration, type == 1 ? (float)__args[1] : 0) != true;
    }
}
