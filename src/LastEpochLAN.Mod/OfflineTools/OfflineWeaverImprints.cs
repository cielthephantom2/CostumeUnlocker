using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Factions;
using Il2CppLE.Gameplay.Monolith;
using LastEpochLAN.Core;

namespace LastEpochLAN.Mod.OfflineTools;

// Native UpdateWeaverTreeNode overwrites these fields from allocated points.
// Scale only these two effects after that recomputation, never an asset constant
// or a generic getter (the real drop consumers inline those getters).
[HarmonyPatch(typeof(TheWeaver), nameof(TheWeaver.UpdateWeaverTreeNode))]
internal static class OfflineWeaverImprintPointRatesPatch
{
    private static void Postfix(TheWeaver __instance, WeaverTreeNodeEffect __0)
    {
        if (!OfflineToolkit.Enabled) return;
        if (__0 == WeaverTreeNodeEffect.ImprintContainerCachesDropMoreItemsOfType)
            __instance.OnCacheOpenedChanceForSimilarItems = WeaverImprintRules.DoubleChance(__instance.OnCacheOpenedChanceForSimilarItems);
        else if (__0 == WeaverTreeNodeEffect.ImprintContainerChampionsDropMoreItemsOfType)
            __instance.ImprintContainerChampionsChanceToDropMoreItemsOfType = WeaverImprintRules.DoubleChance(__instance.ImprintContainerChampionsChanceToDropMoreItemsOfType);
    }
}

// The pinned native DropBonusLootForAnyActor inlines the entire Commoner
// probability, including totalSpawners/totalEnemies; a helper-only hook is inert.
// That numerator is read once, only by the Commoner branch. Temporarily double
// it during this synchronous call and restore exactly even on native exceptions.
[HarmonyPatch(typeof(MonolithZoneManager), nameof(MonolithZoneManager.DropBonusLootForAnyActor))]
internal static class OfflineWeaverCommonerDropPatch
{
    [ThreadStatic] private static HashSet<IntPtr> activeZones;
    internal readonly record struct State(bool Changed, float Original, IntPtr Zone);
    internal static bool IsScaled(MonolithZoneManager zone) => activeZones?.Contains(zone.Pointer) == true;
    private static void Prefix(MonolithZoneManager __instance, ref State __state)
    {
        if (!OfflineToolkit.Enabled || !__instance.enemiesCanDropPileOfSimilarItems) return;
        activeZones ??= new();
        if (!activeZones.Add(__instance.Pointer)) return;
        // Record original before any write, including any getter failure.
        try { __state = new(true, __instance.totalSpawners, __instance.Pointer); }
        catch { activeZones.Remove(__instance.Pointer); throw; }
        __instance.totalSpawners = WeaverImprintRules.DoubleWeight(__state.Original);
    }
    private static Exception Finalizer(MonolithZoneManager __instance, State __state, Exception __exception)
    {
        if (!__state.Changed) return __exception;
        try { __instance.totalSpawners = __state.Original; }
        finally { activeZones.Remove(__state.Zone); }
        return __exception;
    }
}

// Keep direct/preview calls consistent; avoid double scaling if a future caller
// invokes the helper while inside the already-scaled native drop scope.
[HarmonyPatch(typeof(MonolithZoneManager), nameof(MonolithZoneManager.ChanceForEnemiesToDropPileOfSimilarItems))]
internal static class OfflineWeaverCommonerChancePatch
{
    private static void Postfix(MonolithZoneManager __instance, ref float __result)
    {
        if (OfflineToolkit.Enabled && !OfflineWeaverCommonerDropPatch.IsScaled(__instance))
            __result = WeaverImprintRules.DoubleChance(__result);
    }
}

// Thread-local scopes restrict shared native helpers to the actual Imprint
// consumers. No RNG hooks, extra rolls, reward replay or shared asset mutation.
internal sealed class WeaverImprintCallScope : IDisposable
{
    [ThreadStatic] internal static WeaverImprintCallScope Current;
    private readonly WeaverImprintCallScope previous;
    internal readonly bool Echo, Woven;
    internal WeaverImprintCallScope(bool echo, bool woven)
    {
        previous = Current; Echo = echo || previous?.Echo == true;
        Woven = woven || previous?.Woven == true; Current = this;
    }
    public void Dispose() => Current = previous;
}

[HarmonyPatch(typeof(MonolithItemManager), nameof(MonolithItemManager.TryDropSimilarItemsToImprintedWeaverTreeItem))]
internal static class OfflineWeaverEchoImprintScopePatch
{
    private static void Prefix(out WeaverImprintCallScope __state) =>
        __state = OfflineToolkit.Enabled ? new(true, false) : null;
    private static void Finalizer(WeaverImprintCallScope __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(MonolithZoneManager), nameof(MonolithZoneManager.SimilarItemDropMultiplierFromCorruption))]
internal static class OfflineWeaverEchoImprintChancePatch
{
    private static void Postfix(ref float __result)
    {
        if (OfflineToolkit.Enabled && WeaverImprintCallScope.Current?.Echo == true)
            __result = WeaverImprintRules.DoubleWeight(__result);
    }
}

[HarmonyPatch]
internal static class OfflineWeaverWovenImprintScopePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        // Include the actual outer island weighting consumer: multiplying only
        // the final selection weights of both slots cancels after normalization.
        foreach (string name in new[] { nameof(WeaverTreeNodesExtensions.OverrideEchoTypes),
            nameof(WeaverTreeNodesExtensions.CanSpawnSpecialEchoesNaturally),
            nameof(WeaverTreeNodesExtensions.IncreaseChanceForSpecificEchoTypes),
            nameof(WeaverTreeNodesExtensions.SetNaturalSpawnedWovenEchoIslandData) })
            yield return AccessTools.Method(typeof(WeaverTreeNodesExtensions), name) ??
                throw new MissingMethodException(typeof(WeaverTreeNodesExtensions).FullName, name);
    }
    private static void Prefix(out WeaverImprintCallScope __state) =>
        __state = OfflineToolkit.Enabled ? new(false, true) : null;
    private static void Finalizer(WeaverImprintCallScope __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(WovenEchoData), nameof(WovenEchoData.GetTotalNaturalSpawnWeighting))]
internal static class OfflineWeaverWovenImprintWeightPatch
{
    private static void Postfix(ref float __result)
    {
        if (OfflineToolkit.Enabled && WeaverImprintCallScope.Current?.Woven == true)
            __result = WeaverImprintRules.DoubleWeight(__result);
    }
}
