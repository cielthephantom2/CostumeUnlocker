using HarmonyLib;
using Il2Cpp;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class WovenEnchanter
{
    internal static void Apply(ZoneRestrictedCraftingManager manager)
    {
        if(OfflineToolkit.Enabled && manager!=null &&
            manager.craftingType==ZoneRestrictedCraftingManager.CraftingType.IdolEnchantment)
            manager.limitedUses=false;
    }
    internal static void PrepareUI(ZoneRestrictedCraftingManager.CraftingType craftingType)
    {
        if(!OfflineToolkit.Enabled || craftingType!=ZoneRestrictedCraftingManager.CraftingType.IdolEnchantment)return;
        var managers=ZoneRestrictedCraftingManager.all;
        if(managers!=null && managers.TryGetValue(craftingType,out var manager))Apply(manager);
    }
}

// Use the native unlimited mode for idol altars only. Keep native unlocks,
// echo completion, item validation and Memory Amber costs.
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

// Prepare before the native limitedUses branch, including when crafting is
// queried before the panel is opened or after this altar was exhausted.
[HarmonyPatch(typeof(ZoneRestrictedCraftingManager),nameof(ZoneRestrictedCraftingManager.CanCraft))]
internal static class OfflineWovenEnchanterEligibilityPatch
{private static void Prefix(ZoneRestrictedCraftingManager.CraftingType craftingType)=>WovenEnchanter.PrepareUI(craftingType);}
