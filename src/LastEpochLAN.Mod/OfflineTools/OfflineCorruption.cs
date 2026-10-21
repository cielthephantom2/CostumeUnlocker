using HarmonyLib;
using Il2Cpp;
using LastEpochLAN.Core;

namespace LastEpochLAN.Mod.OfflineTools;

// Native preview and native Shade completion both call this calculator.
// One postfix avoids multiplying twice or writing an active/saved web directly.
[HarmonyPatch(typeof(EchoWeb), nameof(EchoWeb.NewCorruptionForShadeEchoOfTier))]
internal static class OfflineShadeCorruptionPatch
{
    private static void Postfix(TimelineID __0, int __1, int __3, int __5,
        ref int __7, ref int __8, ref int __result)
    {
        if (!OfflineToolkit.Enabled || __result <= __3) return;
        var difficulty = TimelineList.getTimelineDifficulty(__0, __1);
        if (difficulty == null) return;
        var reward = OfflineBonusRules.ShadeCorruption(__3, __result, __7, __8,
            difficulty.hasMaxCorruption ? difficulty.maximumCorruption : int.MaxValue, __5);
        __result = reward.Total;
        __7 = reward.Catchup;
        __8 = reward.Gaze;
    }
}

// Completion-only trace: no per-frame or preview logging and no save mutation.
[HarmonyPatch(typeof(EchoWeb), nameof(EchoWeb.GainCorruptionAfterCompletingShadeEcho))]
internal static class OfflineShadeCorruptionCompletionTracePatch
{
    private static void Prefix(EchoWeb __instance, SharedMonolithRunsData __1,
        out (int Current, int Gaze, int Highest, int StashHighest) __state)
        => __state = (__instance.corruption, __instance.gazeOfOrobyss,
            __1?.highestCorruptionOfAnyRun ?? 0, __1?.highestCorruptionOfAnyRunInStash ?? 0);

    private static void Postfix(EchoWeb __instance, (int Current, int Gaze, int Highest, int StashHighest) __state)
    {
        if (OfflineToolkit.Enabled)
            MelonLoader.MelonLogger.Msg($"Shade completion: timeline={__instance.timelineID}, corruption={__state.Current}->{__instance.corruption}, gained={(long)__instance.corruption-__state.Current}, gazeBefore={__state.Gaze}, highestCharacter={__state.Highest}, highestInStash={__state.StashHighest}; catchup is not multiplied x5.");
    }
}
