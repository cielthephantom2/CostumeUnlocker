using HarmonyLib;
using Il2Cpp;
using LastEpochLAN.Core;

namespace LastEpochLAN.Mod.OfflineTools;

// Scope the native base-stat initialization. Gear/passive additions and all
// increased/more multipliers run afterwards, entirely outside this scope.
internal static class BaseRegeneration
{
    [ThreadStatic] internal static IntPtr Initializing;
}
[HarmonyPatch(typeof(CharacterStats),nameof(CharacterStats.SetInitialValues))]
internal static class OfflineBaseRegenerationScopePatch
{
    private static void Prefix(CharacterStats __instance,out IntPtr __state)
    {
        __state=BaseRegeneration.Initializing;
        BaseRegeneration.Initializing=OfflineToolkit.Enabled && __instance.actor!=null &&
            __instance.actor.Pointer==PlayerFinder.getPlayerActor()?.Pointer ? __instance.Pointer : IntPtr.Zero;
    }
    private static Exception Finalizer(Exception __exception,IntPtr __state)
    {BaseRegeneration.Initializing=__state;return __exception;}
}
[HarmonyPatch(typeof(CharacterStats),nameof(CharacterStats.ChangeStatModifier))]
internal static class OfflineBaseRegenerationValuePatch
{
    private static void Prefix(CharacterStats __instance,SP property,ref float changeValue,BaseStats.ModType modificationType)
    {
        if(BaseRegeneration.Initializing!=IntPtr.Zero && BaseRegeneration.Initializing==__instance.Pointer &&
            modificationType==BaseStats.ModType.ADDED && property is SP.ManaRegen or SP.HealthRegen)
            changeValue=OfflineBonusRules.BaseRegen(changeValue);
    }
}
