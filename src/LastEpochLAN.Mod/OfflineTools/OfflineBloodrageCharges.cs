using HarmonyLib;
using Il2Cpp;
using LastEpochLAN.Core;

namespace LastEpochLAN.Mod.OfflineTools;

// Keep the native hunt counter at 1..5: FixedUpdate_Hunt uses it as the next
// kill-threshold index and inlines GrantBloodrageCharge. Only convert earned
// charges at their presentation/currency boundaries, never mutate that index.
[HarmonyPatch(typeof(RageOfMorditasSync), nameof(RageOfMorditasSync.SendMorditasChargeGainedMessage))]
internal static class OfflineBloodrageChargeAnnouncementPatch
{
    private static void Prefix(ref int __0)
    {
        if (OfflineToolkit.Enabled) __0 = BloodrageChargeRules.Earned(__0);
    }
}

// Timer/kill-count refresh recomputes the displayed charge count from kills,
// independently of the gained message. Keep both UI paths consistent.
[HarmonyPatch(typeof(RageOfMorditasSync), nameof(RageOfMorditasSync.GetBloodrageChargeCount))]
internal static class OfflineBloodrageChargeCountPatch
{
    private static void Postfix(ref int __result)
    {
        if (OfflineToolkit.Enabled) __result = BloodrageChargeRules.Earned(__result);
    }
}

// Actual native per-player reward balances are initialized from this argument.
// Do not double remaining balances on sync, reopen or ConsumeReward. Standalone
// reward encounters and explicit native debug overrides retain their own rules.
[HarmonyPatch(typeof(RageOfMorditasRewardLogic), nameof(RageOfMorditasRewardLogic.Initialize))]
internal static class OfflineBloodrageInitialRewardsPatch
{
    private static void Prefix(ref int __2, bool __3)
    {
        if (OfflineToolkit.Enabled) __2 = BloodrageChargeRules.InitialRewards(__2, __3);
    }
}

internal static class OfflineBloodrageProgressText
{
    internal static void Refresh(Il2CppTMPro.TMP_Text label, int earnedCharges)
    {
        if (!OfflineToolkit.Enabled || label == null) return;
        var thresholds = RageOfMorditasData.Instance?.HuntKillThresholdsForBloodrageCharges;
        if (thresholds == null) return;
        // The numerator is already converted to earned currency by dev31.
        // Only convert the native tier-count denominator; never change the asset.
        label.text = $"{earnedCharges}/{BloodrageChargeRules.Earned(thresholds.Count)}";
    }
}

[HarmonyPatch(typeof(RageOfMorditasHuntProgress), nameof(RageOfMorditasHuntProgress.SetBloodrageCharges))]
internal static class OfflineBloodrageHuntProgressTextPatch
{
    private static void Postfix(RageOfMorditasHuntProgress __instance, int __0)
        => OfflineBloodrageProgressText.Refresh(__instance.chargesText, __0);
}

[HarmonyPatch(typeof(RageOfMorditasQuestTrackerUI), nameof(RageOfMorditasQuestTrackerUI.UpdatePhaseVisuals))]
internal static class OfflineBloodrageQuestProgressTextPatch
{
    private static void Postfix(RageOfMorditasQuestTrackerUI __instance)
        => OfflineBloodrageProgressText.Refresh(__instance.bloodrageChargesText, __instance._currentBloodChargeCount);
}
