using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using NativeMods=Il2CppSystem.Collections.Generic.List<Il2Cpp.UniqueItemMod>;
using NativeDescriptions=Il2CppSystem.Collections.Generic.List<Il2Cpp.ItemTooltipDescription>;
using NativeEntries=Il2CppSystem.Collections.Generic.List<Il2Cpp.UniqueModDisplayListEntry>;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class SpiritXylem
{
    private sealed record Registry(UniqueList.Entry Entry,NativeMods Mods,NativeDescriptions Descriptions,NativeEntries Entries);
    private static Registry registry;
    private static CharacterStats current;
    private static readonly List<Stats.Stat> injected=new();
    private static readonly UniqueItemMod skills=new(){property=SP.LevelOfSkills,value=3,maxValue=4,canRoll=true,rollID=5};
    private static readonly UniqueItemMod attribute=new(){property=SP.Attunement,value=10,maxValue=20,canRoll=true,rollID=6};
    private static readonly UniqueItemMod healthMana=new(){property=SP.PlayerProperty,tags=(AT)596,value=3,maxValue=6,canRoll=true,rollID=2};
    private static string lastError;
    internal static void Report(Exception ex)
    {var message="Spirit Xylem: "+ex.GetBaseException().Message;if(message!=lastError){MelonLoader.MelonLogger.Warning(message);lastError=message;}}
    internal static void Configure(UniqueList.Entry entry)
    {
        if(entry==null || entry.uniqueID!=SpiritXylemRules.Id)return;
        if(!OfflineToolkit.Enabled){RestoreRegistry();return;}
        if(registry?.Entry.Pointer==entry.Pointer)return;
        if(entry.mods==null || entry.mods.Count!=8)throw new InvalidOperationException("Unexpected Spirit Xylem definition");
        // Keep the shipped common spell-health-cost and native mana-threshold
        // damage/defense mods. All rolled conditional bonuses are supplied once
        // by the equipped source below, never as unconditional registry stats.
        var common=new NativeMods();common.Add(entry.mods[1]);common.Add(entry.mods[5]);common.Add(entry.mods[4]);
        if(common[0].property!=SP.AbilityProperty || common[0].tags!=(AT)253 || common[0].specialTag!=2 ||
            common[1].property!=SP.GlobalConditionalDamage || common[1].specialTag!=40 ||
            common[2].property!=SP.PlayerProperty || common[2].tags!=(AT)598)
            throw new InvalidOperationException("Spirit Xylem native modifier identity mismatch");
        var descriptions=new NativeDescriptions();
        descriptions.Add(new(){description=SpiritXylemRules.AttunementText,altText=SpiritXylemRules.ComparisonText});
        descriptions.Add(new(){description=SpiritXylemRules.IntelligenceText,altText=SpiritXylemRules.ComparisonText});
        var entries=new NativeEntries();
        foreach(byte index in new byte[]{0,1,2,128,129})entries.Add(new UniqueModDisplayListEntry(index));
        registry=new(entry,entry.mods,entry.tooltipDescriptions,entry.tooltipEntries);
        entry.mods=common;entry.tooltipDescriptions=descriptions;entry.tooltipEntries=entries;
    }
    private static void Ensure()
    {
        if(registry!=null)return;
        var unique=UniqueList.instance;
        if(unique?.uniques==null)return;
        foreach(var entry in unique.uniques)if(entry?.uniqueID==SpiritXylemRules.Id){Configure(entry);return;}
    }
    private static void RestoreRegistry()
    {
        if(registry==null)return;
        registry.Entry.mods=registry.Mods;registry.Entry.tooltipDescriptions=registry.Descriptions;registry.Entry.tooltipEntries=registry.Entries;registry=null;
    }
    internal static void Remove(CharacterStats stats)
    {
        if(current==null || stats==null || current.Pointer!=stats.Pointer)return;
        if(current.stats!=null)
            for(int i=current.stats.Count-1;i>=0;i--)
                if(current.stats[i]!=null && injected.Any(stat=>stat.Pointer==current.stats[i].Pointer))current.stats.RemoveAt(i);
        injected.Clear();current=null;
    }
    private static void Add(CharacterStats stats,SP property,float value,byte special=0)
    {
        if(value==0 || !float.IsFinite(value))return;
        var stat=new Stats.Stat(property,AT.None,value,0,0,special,0){dontCollapse=true};
        injected.Add(stat);stats.stats.Add(stat);
    }
    private static float Roll(ItemData item,UniqueItemMod mod)
    {
        if(item.uniqueRolls==null || item.uniqueRolls.Length<=mod.rollID)throw new InvalidOperationException("Spirit Xylem unique rolls unavailable");
        return mod.getValue(item.uniqueRolls[mod.rollID]);
    }
    internal static void Apply(CharacterStats stats)
    {
        if(current!=null && (stats==null || current.Pointer!=stats.Pointer))
        {var old=current;Remove(old);if(old!=null)old.UpdateStatsNextFrame();}
        Remove(stats);
        if(!OfflineToolkit.Enabled || stats?.actor==null || stats.actor.Pointer!=PlayerFinder.getPlayerActor()?.Pointer || stats.stats==null)return;
        Ensure();
        var equip=stats.actor.itemContainersManager?.itemEquipManager;
        if(equip?.equippedItems==null || registry==null)return;
        var items=new List<(ItemData Item,float Effect)>();
        foreach(var equipped in equip.equippedItems)
            if(equipped?.StatsApplied==true && equipped.Item?.uniqueID==SpiritXylemRules.Id && float.IsFinite(equipped.globalStatModifier))
                items.Add((equipped.Item,Math.Max(0,1+equipped.globalStatModifier)));
        if(items.Count==0)return;
        float att=0,intel=0;
        // This is the same raw sum consumed by native ApplyCoreAttributeModifiers.
        // The prior source was removed before counting. Item implicit/legendary
        // affixes and other equipment/passive/buff attributes remain included.
        foreach(var stat in stats.stats)
        {
            if(stat==null)continue;
            if(stat.property==SP.AllAttributes){att+=stat.addedValue;intel+=stat.addedValue;}
            else if(stat.property==SP.Attunement)att+=stat.addedValue;
            else if(stat.property==SP.Intelligence)intel+=stat.addedValue;
        }
        var branch=SpiritXylemRules.Choose(att,intel);
        if(branch==SpiritXylemRules.Branch.None)return;
        bool attBranch=branch==SpiritXylemRules.Branch.Attunement;
        float addedAttribute=0,levels=0,perAttunement=0,spellPerIntelligence=0;
        foreach(var (item,effect) in items)
        {
            addedAttribute+=Roll(item,attribute)*effect;
            levels+=Roll(item,skills)*effect;
            if(attBranch)perAttunement+=Roll(item,healthMana)*effect;
            else spellPerIntelligence+=.5f*effect;
        }
        current=stats;
        // Publish physical stats before native attribute/HP/MP/skill calculations;
        // native events update max skill points and all derived attribute effects.
        Add(stats,attBranch?SP.Attunement:SP.Intelligence,addedAttribute);
        Add(stats,SP.LevelOfSkills,levels,(byte)(attBranch?Tags.SpecificityType.TagsAttunement:Tags.SpecificityType.TagsIntelligence));
        if(attBranch)
        {
            float bonus=perAttunement*SpiritXylemRules.Attribute(att+addedAttribute);
            Add(stats,SP.Health,bonus);Add(stats,SP.Mana,bonus);
        }
        else Add(stats,SP.AdaptiveSpellDamage,spellPerIntelligence*SpiritXylemRules.Attribute(intel+addedAttribute));
    }
    internal static void Tick()
    {
        if(!OfflineToolkit.Enabled){Clear();return;}
        if(current!=null && current.actor?.Pointer!=PlayerFinder.getPlayerActor()?.Pointer)
        {var old=current;Remove(old);if(old!=null)old.UpdateStatsNextFrame();}
    }
    internal static void Clear()
    {
        var old=current;Remove(old);if(old!=null)old.UpdateStatsNextFrame();RestoreRegistry();
    }
}
[HarmonyPatch(typeof(UniqueList),nameof(UniqueList.getUnique))]
internal static class OfflineSpiritXylemDefinitionPatch
{private static void Postfix(UniqueList.Entry __result){try{SpiritXylem.Configure(__result);}catch(Exception ex){SpiritXylem.Report(ex);}}}
[HarmonyPatch(typeof(UniqueList),nameof(UniqueList.getMods))]
internal static class OfflineSpiritXylemModsPatch
{
    private static void Postfix(ushort uniqueId,ref NativeMods __result)
    {
        if(uniqueId!=SpiritXylemRules.Id)return;
        try{var entry=UniqueList.getUnique(uniqueId);SpiritXylem.Configure(entry);if(entry!=null)__result=entry.mods;}
        catch(Exception ex){SpiritXylem.Report(ex);}
    }
}
[HarmonyPatch(typeof(BaseStats),nameof(BaseStats.UpdateStatsInternal))]
internal static class OfflineSpiritXylemResetPatch
{private static void Prefix(BaseStats __instance)=>SpiritXylem.Remove(__instance.TryCast<CharacterStats>());}
[HarmonyPatch(typeof(CharacterStats),nameof(CharacterStats.ApplyCoreAttributeModifiers))]
internal static class OfflineSpiritXylemConditionPatch
{
    private static void Prefix(CharacterStats __instance)
    {try{SpiritXylem.Apply(__instance);}catch(Exception ex){SpiritXylem.Remove(__instance);SpiritXylem.Report(ex);}}
    private static Exception Finalizer(CharacterStats __instance,Exception __exception)
    {if(__exception!=null)SpiritXylem.Remove(__instance);return __exception;}
}

// Native tooltip builders localize Unique_Tooltip_0_445 before parsing rolls.
// That shipped key contains the OLD HP/MP-only description and overrides our
// replacement asset string. Override these exact keys in both tooltip renderers.
[HarmonyPatch(typeof(Localization),nameof(Localization.GetText),new[]{typeof(string),typeof(string)})]
internal static class OfflineSpiritXylemLocalizationPatch
{
    private static bool Prefix(string key,ref string __result)
    {
        if(!OfflineToolkit.Enabled)return true;
        string text=key switch
        {
            "Unique_Tooltip_0_445"=>SpiritXylemRules.AttunementText,
            "Unique_Tooltip_1_445"=>SpiritXylemRules.IntelligenceText,
            "Unique_Tooltip_0_445_Alt" or "Unique_Tooltip_1_445_Alt"=>SpiritXylemRules.ComparisonText,
            _=>null
        };
        if(text==null)return true;__result=text;return false;
    }
}
