using HarmonyLib;
using Il2Cpp;
using LastEpochLAN.Core;

namespace LastEpochLAN.Mod.OfflineTools;

// This is the Woven Potential item operation, not the general loot LP roll.
// The native crafting caller still validates access, consumes uses and updates
// its container. Preserve item identity, affixes, rolls and faction requirements.
[HarmonyPatch(typeof(ItemData), nameof(ItemData.RandomizeLegendaryPotential))]
internal static class OfflineWovenPotentialPatch
{
    private static bool Prefix(ItemData __instance)
    {
        if (!OfflineToolkit.Enabled || __instance == null || !WovenPotentialRules.AppliesTo(__instance.legendaryPotential)) return true;
        __instance.legendaryPotential = (byte)WovenPotentialRules.Roll(__instance.legendaryPotential,UnityEngine.Random.Range(0,100));
        __instance.RebuildID();
        __instance.setValuesFromSerialisation();
        return false;
    }
}
