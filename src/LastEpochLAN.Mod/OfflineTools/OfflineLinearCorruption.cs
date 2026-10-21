using HarmonyLib;
using Il2Cpp;
using LastEpochLAN.Core;

namespace LastEpochLAN.Mod.OfflineTools;

[HarmonyPatch(typeof(MonolithRun), nameof(MonolithRun.IncreasedExperienceFromCorruption))]
internal static class LinearCorruptionExperiencePatch
{
    private static bool Prefix(int __0, ref float __result)
    {
        if (!OfflineToolkit.Enabled) return true;
        __result = LinearCorruptionRules.Experience(__0); return false;
    }
}

// Native calculate... inlines the old EXP formula; patching the public helper
// alone changes previews but leaves real XP unchanged on this game build.
[HarmonyPatch(typeof(MonolithRun), nameof(MonolithRun.calculateIncreasedRarityAndExperienceFromMods))]
internal static class LinearCorruptionRunExperiencePatch
{
    private static void Postfix(MonolithRun __instance)
    {
        if (!OfflineToolkit.Enabled || __instance.web == null) return;
        int c = __instance.web.corruption;
        __instance.increasedExperience += LinearCorruptionRules.Experience(c) - LinearCorruptionRules.NativeExperience(c);
    }
}

[HarmonyPatch(typeof(ItemData), nameof(ItemData.GetLPChanceMultiplierFromCorruption))]
internal static class LinearCorruptionPotentialPreviewPatch
{
    private static bool Prefix(float __0, ref float __result)
    {
        if (!OfflineToolkit.Enabled) return true;
        __result = LinearCorruptionRules.PotentialMultiplier(__0); return false;
    }
}

// RollLegendaryPotential also inlines the log. Move its Corruption multiplier
// into the existing non-CoF multiplier so native rolls and CoF attribution stay intact.
[HarmonyPatch(typeof(ItemData), nameof(ItemData.RollLegendaryPotential))]
internal static class LinearCorruptionPotentialRollPatch
{
    private static void Prefix(ref float __3, ref float __6)
    {
        if (!OfflineToolkit.Enabled) return;
        __6 *= LinearCorruptionRules.PotentialMultiplier(__3); __3 = 0;
    }
}

[HarmonyPatch(typeof(ItemData), nameof(ItemData.RollWeaversWill))]
internal static class LinearCorruptionWillRollPatch
{
    private static bool Prefix(ItemData __instance, UniqueList.Entry __0, int __1, int __2, float __3, ref int __result)
    {
        if (!OfflineToolkit.Enabled || __0 == null || ItemData.rng == null) return true;
        int effective = __0.getEffectiveLevelForLegendaryPotential((byte)Math.Min(__instance.subType, (ushort)255));
        var rng = ItemData.rng;
        __result = LinearCorruptionRules.RollWill(__2, effective, __1, __3,
            chance => rng.Roll(chance), (min, max) => rng.Range(min, max));
        return false;
    }
}

[HarmonyPatch(typeof(EchoWeb), nameof(EchoWeb.GetIncreasedEchoRewardRarityFromCorruption))]
internal static class LinearCorruptionEchoRarityPreviewPatch
{
    private static bool Prefix(float __0, ref float __result)
    {
        if (!OfflineToolkit.Enabled) return true;
        __result = LinearCorruptionRules.EchoRarity(__0); return false;
    }
}

// The consumer inlines the calculation and also gates it to the proper
// timeline difficulty. Keep that native gate rather than enabling normal timelines.
[HarmonyPatch(typeof(EchoWeb), nameof(EchoWeb.getIncreasedEchoRewardRarity))]
internal static class LinearCorruptionEchoRarityPatch
{
    private static void Postfix(EchoWeb __instance, ref float __result)
    {
        if (OfflineToolkit.Enabled && __result > 0) __result = LinearCorruptionRules.EchoRarity(__instance.corruption);
    }
}

[HarmonyPatch(typeof(SilkenCocoonData), nameof(SilkenCocoonData.GetUnroundedMaximumMemoryAmberForStandardDrop))]
internal static class LinearCorruptionAmberPatch
{
    private static void Postfix(int __0, ref float __result)
    {
        if (!OfflineToolkit.Enabled) return;
        var data = RandomEncounterData.get()?.silkenCocoonData;
        if (data == null) return;
        __result *= LinearCorruptionRules.AmberCorrection(__0, data.maximumMemoryAmberFromSoul,
            data.maximumMemoryAmberFromSoulPerCorruptionUntil500, data.maximumMemoryAmberFromSoulPerCorruptionAfter500);
    }
}

[HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.SpawnGoldForActor))]
internal static class LinearCorruptionGoldPatch
{
    private static void Prefix(Actor __1, ref float __5, ref int __6)
    {
        if (!OfflinePickup.Local(__1)) return;
        var run = Il2CppLE.Gameplay.Monolith.MonolithGameplayManager.ActiveRun;
        var zone = MonolithZoneManager.instance;
        if (run?.web == null || zone == null || !zone.gameObject.scene.isLoaded) return;
        // Resolve native -1/current-map sentinel without changing the saved web.
        int c = __6 >= 0 ? __6 : run.web.corruption;
        __6 = c;
        // Undo only native diminishing. Preserve the existing /40 pickup boost:
        // normalizing their product to a C=100 anchor would nerf higher rewards.
        __5 *= LinearCorruptionRules.GoldMultiplier(c) /
            LinearCorruptionRules.NativeGoldMultiplier(c);
    }
}
