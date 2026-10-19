using HarmonyLib;
using Il2Cpp;
using LastEpochLAN.Core;

namespace LastEpochLAN.Mod.OfflineTools;

// Native preview and native Shade completion both call this calculator.
// One postfix avoids multiplying twice or writing an active/saved web directly.
[HarmonyPatch(typeof(EchoWeb), nameof(EchoWeb.NewCorruptionForShadeEchoOfTier))]
internal static class OfflineShadeCorruptionPatch
{
    private static void Postfix(TimelineID __0, int __1, int __3,
        ref int __7, ref int __8, ref int __result)
    {
        if (!OfflineToolkit.Enabled || __result <= __3) return;
        var difficulty = TimelineList.getTimelineDifficulty(__0, __1);
        if (difficulty == null) return;
        var reward = OfflineBonusRules.ShadeCorruption(__3, __result, __7, __8,
            difficulty.hasMaxCorruption ? difficulty.maximumCorruption : int.MaxValue);
        __result = reward.Total;
        __7 = reward.Catchup;
        __8 = reward.Gaze;
    }
}
