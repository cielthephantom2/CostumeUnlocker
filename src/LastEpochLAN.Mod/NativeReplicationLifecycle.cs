using System.Collections.Concurrent;
using HarmonyLib;
using Il2CppLE.Networking.Replication;

namespace LastEpochLAN.Mod;

internal static class NativeReplicationLifecycle
{
    private static readonly ConcurrentDictionary<IntPtr, byte> managers = new();
    internal static void Track(ReplicationSystem system) => managers[system.Handles.Pointer] = 0;
    internal static void Untrack(ReplicationSystem system) => managers.TryRemove(system.Handles.Pointer, out _);
    internal static bool Owns(ReplicationHandleManager manager) => managers.ContainsKey(manager.Pointer);
}

// In this build ReleaseHandleIndex clears the sparse array but retains usedHandleKeys.
// An in-flight fragment then finds the stale key and calls GetHandle on an empty slot.
// Repair only the private session's mapping, preserving the native fragment parser,
// acknowledgement handling and all ordinary game services.
[HarmonyPatch(typeof(ReplicationHandleManager), nameof(ReplicationHandleManager.ReleaseHandleIndex))]
internal static class NativeReplicationReleasePatch
{
    private static void Prefix(ReplicationHandleManager __instance, int index, out ReplicationHandle __state)
    {
        __state = null;
        if (!NativeReplicationLifecycle.Owns(__instance) || index < 0 || index >= __instance.handles.Length) return;
        __state = __instance.handles[index];
    }

    private static void Postfix(ReplicationHandleManager __instance, int index, ReplicationHandle __state)
    {
        if (__state == null || !__state.Released) return;
        if (__instance.usedHandleKeys.TryGetValue(__state.NetObjectKey, out int mapped) && mapped == index)
            __instance.usedHandleKeys.Remove(__state.NetObjectKey);
    }
}
