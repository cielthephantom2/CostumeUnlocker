using HarmonyLib;
using Il2Cpp;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class WovenEnchanter
{
    internal const int MaximumUses=10;
    internal static void Apply(ZoneRestrictedCraftingManager manager)
    {
        if(OfflineToolkit.Enabled && manager!=null &&
            manager.craftingType==ZoneRestrictedCraftingManager.CraftingType.IdolEnchantment)
            manager.maxUses=MaximumUses;
    }
    internal static void PrepareUI(ZoneRestrictedCraftingManager.CraftingType craftingType)
    {
        if(!OfflineToolkit.Enabled || craftingType!=ZoneRestrictedCraftingManager.CraftingType.IdolEnchantment)return;
        var managers=ZoneRestrictedCraftingManager.all;
        if(managers!=null && managers.TryGetValue(craftingType,out var manager))Apply(manager);
    }
}

// Native calculates a different limit for each idol altar tier. Replace that
// limit, keeping native use consumption, unlocks and echo completion gates.
[HarmonyPatch(typeof(ZoneRestrictedCraftingManager),nameof(ZoneRestrictedCraftingManager.CalculateMaxUses))]
internal static class OfflineWovenEnchanterLimitPatch
{private static void Postfix(ZoneRestrictedCraftingManager __instance)=>WovenEnchanter.Apply(__instance);}

// Also covers an altar already loaded before its crafting panel is opened.
// Both CanCraft and OnUse consume this native remaining-use calculation.
[HarmonyPatch(typeof(ZoneRestrictedCraftingManager),nameof(ZoneRestrictedCraftingManager.UsesRemaining))]
internal static class OfflineWovenEnchanterRemainingPatch
{private static void Prefix(ZoneRestrictedCraftingManager __instance)=>WovenEnchanter.Apply(__instance);}

[HarmonyPatch(typeof(ZoneRestrictedCraftingManager),nameof(ZoneRestrictedCraftingManager.GetUsesValuesForUI))]
internal static class OfflineWovenEnchanterUiPatch
{private static void Prefix(ZoneRestrictedCraftingManager.CraftingType craftingType)=>WovenEnchanter.PrepareUI(craftingType);}
