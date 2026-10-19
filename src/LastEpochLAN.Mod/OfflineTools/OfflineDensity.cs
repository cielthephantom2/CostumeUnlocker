using HarmonyLib;
using Il2Cpp;
using LastEpochLAN.Core;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class OfflineDensity
{
    internal struct RollState
    {
        internal bool Changed;
        internal float Modifier, Target, Multiplier;
    }

    internal static string Status = "Density: outside x5; Monolith = max(1, corruption / 100)";
    private static bool warned;

    // Only generation-time hooks: no actor scans, per-frame polling or packets.
    internal static bool BeforeRoll(SpawnerPlacementRoom room, ref RollState state)
    {
        if (!OfflineToolkit.Enabled) return true;
        var session = HudAuthority.Session;
        if (!DensityRules.CanGenerate(true, session?.Connected == true, session?.IsHost == true))
        {
            Status = "Density: Guest receives Host monsters";
            return false;
        }
        if (room == null || room.locked) return true;
        try
        {
            // A remembered currentRun can survive leaving Monolith. Classify by
            // the live zone manager, not by that remembered run alone.
            var zone = MonolithZoneManager.instance;
            bool monolith = zone != null && zone.gameObject.scene.isLoaded;
            int corruption = 0;
            if (monolith)
            {
                // This is the same authoritative run read by native zone
                // initialise before it calls RollSpawners. The player actor's
                // remembered currentRun may still refer to another timeline.
                var run = Il2CppLE.Gameplay.Monolith.MonolithGameplayManager.ActiveRun;
                if (run?.web == null)
                {
                    Warn("Monolith run not ready at placement; keeping native density for this room.");
                    return true;
                }
                corruption = run.web.corruption;
            }
            float multiplier = DensityRules.Multiplier(monolith, corruption);
            float modifier = room.spawnerDensityModifier;
            float target = room.intendedSpawnerDensity;
            if (!float.IsFinite(modifier) || !float.IsFinite(target)) return true;
            state = new RollState { Changed = true, Modifier = modifier, Target = target, Multiplier = multiplier };
            // Preserve native room/Monolith modifiers and native randomisation.
            // Hook only the config overload: the no-argument overload forwards
            // into it, so it must not apply the multiplier a second time.
            room.spawnerDensityModifier = DensityRules.PlacementModifier(modifier, multiplier);
            Status = monolith ? $"Density: Monolith x{multiplier:0.##} ({corruption} corruption)" : "Density: outside x5";
        }
        catch (Exception ex) { Restore(room, state, ex); state = default; Warn(ex.GetBaseException().Message); }
        return true;
    }

    internal static void Restore(SpawnerPlacementRoom room, RollState state, Exception error)
    {
        if (!state.Changed || room == null) return;
        try
        {
            room.spawnerDensityModifier = state.Modifier;
            room.intendedSpawnerDensity = error == null
                ? DensityRules.RestoreTarget(room.intendedSpawnerDensity, state.Multiplier)
                : state.Target;
        }
        catch (Exception ex) { Warn("Restoring placement fields: " + ex.GetBaseException().Message); }
    }

    private static void Warn(string message)
    {
        if (warned) return;
        warned = true;
        MelonLoader.MelonLogger.Warning("Offline density: " + message);
    }
}

[HarmonyPatch(typeof(SpawnerPlacementRoom), "rollSpawners", new[] { typeof(SpawnerPlacementRoom.SpawnerRuntimeConfig) })]
internal static class OfflineDensityPlacementPatch
{
    [HarmonyPrefix]
    private static bool Prefix(SpawnerPlacementRoom __instance, ref OfflineDensity.RollState __state) =>
        OfflineDensity.BeforeRoll(__instance, ref __state);

    [HarmonyFinalizer]
    private static Exception Finalizer(SpawnerPlacementRoom __instance, OfflineDensity.RollState __state, Exception __exception)
    {
        OfflineDensity.Restore(__instance, __state, __exception);
        return __exception;
    }
}
