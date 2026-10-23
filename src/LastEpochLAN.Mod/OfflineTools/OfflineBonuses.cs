using HarmonyLib;
using Il2Cpp;
using LastEpochLAN.Core;
using UnityEngine;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class OfflineEconomy
{
    internal static float GoldMultiplier()
    {
        var zone = MonolithZoneManager.instance;
        if (zone == null || !zone.gameObject.scene.isLoaded) return 1;
        var run = Il2CppLE.Gameplay.Monolith.MonolithGameplayManager.ActiveRun;
        return run?.web == null ? 1 : OfflineBonusRules.GoldMultiplier(true, run.web.corruption);
    }
}

// Clamp the native result instead of replacing its rarity distribution with a
// uniform roll. The caller still handles LP storage, CoF flags and rebuilding ID.
[HarmonyPatch(typeof(ItemData), nameof(ItemData.RollLegendaryPotential))]
internal static class OfflineMinimumPotentialPatch
{
    private static void Postfix(UniqueList.Entry __0, ref int __result)
    {
        if (OfflineToolkit.Enabled && __0 != null && !__0.isSetItem &&
            __0.legendaryType == UniqueList.LegendaryType.LegendaryPotential)
            __result = OfflineBonusRules.MinimumPotential(__result);
    }
}

internal static class OfflineBuffs
{
    // Native source uses increasedValue for speed and addedValue for this
    // area property. One untagged area entry avoids duplicating +50% for melee.
    internal readonly record struct Spec(string Name, SP Property, float Added, float Increased);
    internal static readonly Spec[] Specs =
    {
        new("TMCoop.Fixed.MoveSpeed", SP.Movespeed, 0, OfflineBonusRules.MoveSpeed),
        new("TMCoop.Fixed.Area", SP.IncreasedAreaForAreaSkills, OfflineBonusRules.Area, 0)
    };
    private static StatBuffs current;
    private static float nextCheck;

    internal static void Tick()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 2;
        if (!OfflineToolkit.Enabled) { Restore(); return; }
        if (!OfflineToolkit.Ready) return;
        var buffs = PlayerFinder.getPlayerActor()?.statBuffs;
        if (buffs == null || buffs.activeBuffNames == null) return;
        if (current != null && current.Pointer != buffs.Pointer) Restore();
        current = buffs;
        Ensure(buffs);
    }

    internal static void Ensure(StatBuffs buffs)
    {
        // Remove the retired increased-regen buffs, including live migration.
        if(buffs.activeBuffNames.ContainsKey("TMCoop.Fixed.ManaRegen")) buffs.removeBuffsWithName("TMCoop.Fixed.ManaRegen");
        if(buffs.activeBuffNames.ContainsKey("TMCoop.Fixed.HealthRegen")) buffs.removeBuffsWithName("TMCoop.Fixed.HealthRegen");
        foreach (var spec in Specs)
        {
            if (buffs.activeBuffNames.TryGetValue(spec.Name, out var buff) && buff != null)
            {
                // Extend the same native buff; no remove/add/recalculation on
                // every check and no stacking on reconnect or scene changes.
                if (buff.remainingDuration < 15) buff.remainingDuration = 255;
                continue;
            }
            buffs.addBuff(255, spec.Property, spec.Added, spec.Increased, null, AT.None, 0, 0, spec.Name);
        }
    }

    internal static void Restore()
    {
        if (current != null)
            foreach (var spec in Specs) current.removeBuffsWithName(spec.Name);
        current = null;
    }
}
