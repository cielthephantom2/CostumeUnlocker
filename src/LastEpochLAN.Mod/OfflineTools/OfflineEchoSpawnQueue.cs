using HarmonyLib;
using Il2Cpp;
using LastEpochLAN.Core;
using UnityEngine.SceneManagement;

namespace LastEpochLAN.Mod.OfflineTools;

// Keep the native generators, counts, RNG and callbacks. Only route dense echo
// generation through the game's scene-owned queue instead of its instant queue.
internal static class OfflineEchoSpawnQueue
{
    internal struct State { internal bool Changed, Original; }
    private static bool warned;

    private static bool DenseEcho()
    {
        if (!OfflineToolkit.Enabled) return false;
        var zone = MonolithZoneManager.instance;
        if (zone == null || !zone.gameObject.scene.isLoaded) return false;
        var session = HudAuthority.Session;
        if (session?.Connected == true && !session.IsHost) return false;
        var web = Il2CppLE.Gameplay.Monolith.MonolithGameplayManager.ActiveRun?.web;
        return web != null && DensityRules.Multiplier(true, web.corruption) > 1;
    }

    private static bool EchoScene(Scene scene) => scene.IsValid() && scene.isLoaded && SceneList.IsSceneType(scene, SceneType.Monolith);

    internal static void BeforeEnqueue(Spawner spawner, ref State state)
    {
        try
        {
            if (spawner == null || !DenseEcho() || !EchoScene(spawner.gameObject.scene)) return;
            state = new State { Changed = true, Original = spawner.allowAmortizedSpawn };
            spawner.allowAmortizedSpawn = true;
        }
        catch (Exception ex) { Warn(ex); }
    }

    internal static void Restore(Spawner spawner, State state)
    {
        if (state.Changed && spawner != null) spawner.allowAmortizedSpawn = state.Original;
    }

    internal static bool BeforeDrain(BaseSteppedQueue queue)
    {
        try
        {
            if (!DenseEcho() || queue?.TryCast<AmortizedSteppedQueue>() == null || SpawnQueueManager.instances == null) return true;
            foreach (var pair in SpawnQueueManager.instances)
            {
                var native = pair.Value?.amortizedQueue;
                if (native == null || native.Pointer != queue.Pointer || !EchoScene(pair.Key)) continue;
                // A native post-load callback otherwise drains this queue in one
                // frame. Leave it registered with its own native update loop.
                if (!native.IsEmpty()) native.EnsureIsTicking();
                return false;
            }
        }
        catch (Exception ex) { Warn(ex); }
        return true;
    }

    private static void Warn(Exception ex)
    {
        if (warned) return;
        warned = true;
        MelonLoader.MelonLogger.Warning("Echo spawn queue: " + ex.GetBaseException().Message);
    }
}

[HarmonyPatch(typeof(Spawner), nameof(Spawner.EnqueueSpawnSteps))]
internal static class OfflineEchoSpawnEnqueuePatch
{
    [HarmonyPrefix]
    private static void Prefix(Spawner __instance, ref OfflineEchoSpawnQueue.State __state) => OfflineEchoSpawnQueue.BeforeEnqueue(__instance, ref __state);
    [HarmonyFinalizer]
    private static Exception Finalizer(Spawner __instance, OfflineEchoSpawnQueue.State __state, Exception __exception)
    {
        OfflineEchoSpawnQueue.Restore(__instance, __state);
        return __exception;
    }
}

[HarmonyPatch(typeof(BaseSteppedQueue), nameof(BaseSteppedQueue.ProcessEntireQueueImmediately))]
internal static class OfflineEchoSpawnDrainPatch
{
    [HarmonyPrefix]
    private static bool Prefix(BaseSteppedQueue __instance) => OfflineEchoSpawnQueue.BeforeDrain(__instance);
}
