using System.Collections.Concurrent;
using HarmonyLib;
using LastEpochLAN.Core;
using UnityEngine;

namespace LastEpochLAN.Mod;

/// <summary>Observe native async drop completion without replacing the loot algorithm or retaining state-machine pointers.</summary>
internal static class NativeRewardCompletion
{
    internal sealed class Pending
    {
        internal int Started, Finished, Failed, DispatchReturned;
        internal readonly IntPtr Owner;
        internal readonly float Deadline;
        internal Pending(IntPtr owner) { Owner = owner; Deadline = Time.unscaledTime + 30; }
        internal DeathReceiptOutcome? Outcome => Volatile.Read(ref Failed) != 0 ? DeathReceiptOutcome.Failed :
            Volatile.Read(ref DispatchReturned) != 0 && (Volatile.Read(ref Started) == 0 || Volatile.Read(ref Finished) != 0)
                ? DeathReceiptOutcome.Completed : null;
    }
    private static readonly ConcurrentDictionary<IntPtr, Pending> pending = new();
    [ThreadStatic] internal static Pending Current;

    internal static Pending Begin(Il2Cpp.ItemDrop drop)
    {
        var result = new Pending(drop?.Pointer ?? IntPtr.Zero);
        if (result.Owner != IntPtr.Zero) pending[result.Owner] = result;
        return result;
    }
    internal static Pending Enter(Il2Cpp.ItemDrop owner)
    {
        var previous = Current;
        Current = owner != null && pending.TryGetValue(owner.Pointer,out var found) ? found : null;
        if (Current != null) Volatile.Write(ref Current.Started, 1);
        return previous;
    }
    internal static void Exit(Pending previous, bool finished, bool failed)
    {
        var tracked = Current;
        if (tracked != null)
        {
            if (failed) Volatile.Write(ref tracked.Failed, 1);
            if (finished) Volatile.Write(ref tracked.Finished, 1);
        }
        Current = previous;
    }
    internal static void Release(Pending handle)
    {
        if (handle.Owner != IntPtr.Zero && pending.TryGetValue(handle.Owner,out var current) && ReferenceEquals(current,handle)) pending.TryRemove(handle.Owner,out _);
    }
    internal static void Reset() { pending.Clear(); Current = null; }

    internal static void NativeSmoke()
    {
        var root = new GameObject("LAN async reward completion smoke"); root.SetActive(false);
        var drop = root.AddComponent<Il2Cpp.ItemDrop>();
        var tracked = Begin(drop);
        try
        {
            // A zero-loot native state completes before scene/player queries. This exercises the actual IL2CPP value-type detour.
            var state = new Il2Cpp.ItemDrop._DropItem_d__102 { __1__state = -1, __4__this = drop,
                lootToAttemptToDrop = 0, skipLootTypesCheck = false };
            state.MoveNext();
            Volatile.Write(ref tracked.DispatchReturned, 1);
            if (state.__1__state != -2 || tracked.Started != 1 || tracked.Finished != 1 || tracked.Outcome != DeathReceiptOutcome.Completed || Current != null)
                throw new InvalidOperationException("Native async drop completion hook failed.");
        }
        finally { Release(tracked); UnityEngine.Object.Destroy(root); }
    }
}

[HarmonyPatch(typeof(Il2Cpp.ItemDrop._DropItem_d__102), nameof(Il2Cpp.ItemDrop._DropItem_d__102.MoveNext))]
internal static class NativeDropMoveNextPatch
{
    private static void Prefix(Il2Cpp.ItemDrop._DropItem_d__102 __instance, out NativeRewardCompletion.Pending __state)
        => __state = NativeRewardCompletion.Enter(__instance.__4__this);
    private static void Postfix(Il2Cpp.ItemDrop._DropItem_d__102 __instance, NativeRewardCompletion.Pending __state)
        => NativeRewardCompletion.Exit(__state, __instance.__1__state == -2, false);
    private static Exception Finalizer(Exception __exception, NativeRewardCompletion.Pending __state)
    {
        if (__exception != null) NativeRewardCompletion.Exit(__state, true, true);
        return __exception;
    }
}

[HarmonyPatch(typeof(Il2CppCysharp.Threading.Tasks.UniTaskScheduler), nameof(Il2CppCysharp.Threading.Tasks.UniTaskScheduler.PublishUnobservedTaskException))]
internal static class NativeRewardAsyncFailurePatch
{
    private static void Prefix()
    {
        // Preserve the game's exception reporting. Mark only the currently tracked native drop.
        if (NativeRewardCompletion.Current is { } tracked) Volatile.Write(ref tracked.Failed,1);
    }
}
