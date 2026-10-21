using HarmonyLib;

namespace LastEpochLAN.Mod;

internal static class RoomPatches
{
    internal static int Installed { get; private set; }
    internal static int Skipped { get; private set; }
    internal static void Install(HarmonyLib.Harmony harmony)
    {
        foreach (var type in typeof(LanMod).Assembly.GetTypes().Where(type => type.IsDefined(typeof(HarmonyPatch), false)))
        {
            if (!RoomFeaturePolicy.AllowsPatch(type.Namespace ?? "", type.Name, type.DeclaringType?.Name ?? ""))
            { Skipped++; continue; }
            MelonLoader.MelonLogger.Msg("Installing hook: "+type.FullName);
            harmony.CreateClassProcessor(type).Patch(); Installed++;
        }
    }
}

// Local automation confirmation is separate from disabled network skill capture.
[HarmonyPatch(typeof(Il2Cpp.UsingAbility), nameof(Il2Cpp.UsingAbility.UseAbility))]
internal static class AutoPlayCastObservationPatch
{
    private static void Postfix(Il2Cpp.UsingAbility __instance, Il2Cpp.Ability _ability) => AutoPlayRuntime.CastObserved(__instance, _ability);
}
