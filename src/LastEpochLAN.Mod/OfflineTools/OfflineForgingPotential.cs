using HarmonyLib;
using Il2Cpp;
using System.Globalization;

namespace LastEpochLAN.Mod.OfflineTools;

[HarmonyPatch(typeof(ItemData),nameof(ItemData.applyForgingPotentialCost))]
internal static class OfflineForgingPotentialCostPatch
{
    private static void Prefix(ref int __0,ref int __1)
    {
        if(!OfflineToolkit.Enabled)return;
        __0=ForgingPotentialRules.Half(__0);
        __1=ForgingPotentialRules.Half(__1);
    }
}

[HarmonyPatch(typeof(CraftingSlotManager),nameof(CraftingSlotManager.UpdateForgingPotentialCostDisplay))]
internal static class OfflineForgingPotentialDisplayPatch
{
    private static void Postfix(CraftingSlotManager __instance)
    {
        if(!OfflineToolkit.Enabled || __instance==null || __instance.maxPotentialCost<=0)return;
        var item=__instance.main?.getItem();
        var rune=__instance.modifier?.getItem();
        if(item==null)return;
        // Rune of Weaving uses these same UI fields for Weaver's Will/Touch,
        // which is not Forging Potential and must keep its native cost.
        if(rune?.itemType==102 && rune.subType==8)return;
        __instance.minPotentialCost=ForgingPotentialRules.Half(__instance.minPotentialCost);
        __instance.maxPotentialCost=ForgingPotentialRules.Half(__instance.maxPotentialCost);
        if(__instance.minForgingPotentialText!=null)
            __instance.minForgingPotentialText.text="-"+__instance.minPotentialCost.ToString(CultureInfo.InvariantCulture);
        if(__instance.maxForgingPotentialText!=null)
            __instance.maxForgingPotentialText.text="-"+__instance.maxPotentialCost.ToString(CultureInfo.InvariantCulture);
    }
}
