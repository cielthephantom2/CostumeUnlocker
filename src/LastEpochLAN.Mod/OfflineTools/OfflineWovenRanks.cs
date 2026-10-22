using System.Text.RegularExpressions;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Factions;
using UnityEngine;

namespace LastEpochLAN.Mod.OfflineTools;
internal static class WovenRankRules
{
    internal static float Value(int rank)=>rank switch{7=>.3f,9=>.5f,_=>0};
    internal static string Description(int rank,string original)=>rank is 7 or 9 && original!=null
        ? Regex.Replace(original,@"(?<![\d.,])10(\s*)%",m=>(rank==7?"30":"50")+m.Groups[1].Value+"%") : original;
}
internal static class OfflineWovenRanks
{
    private static float next;
    internal static void ApplyRank(TheWeaver faction,int rank,bool enabled)
    {
        if(!enabled || !OfflineToolkit.Enabled || faction?.IsMember!=true || faction.Rank<rank ||
            faction.CachedPlayerActor==null || faction.CachedPlayerActor.Pointer!=PlayerFinder.getPlayerActor()?.Pointer ||
            faction.rankToggles!=null && faction.rankToggles.TryGetValue(rank,out var on) && !on)return;
        if(rank==7)faction.IncreasedChanceForSecondAffixToBeWeaverWhenReforgingIdols=WovenRankRules.Value(rank);
        else if(rank==9)faction.MoreMemoryAmberDropRate=WovenRankRules.Value(rank);
    }
    internal static void Tick()
    {
        if(!OfflineToolkit.Ready || Time.unscaledTime<next)return;
        next=Time.unscaledTime+1;
        var provider=PlayerFinder.getPlayerActor()?.FactionInfo;
        if(provider==null || !provider.TryGetFaction(FactionID.TheWeaver,out var value))return;
        var faction=value?.TryCast<TheWeaver>();
        ApplyRank(faction,7,true);ApplyRank(faction,9,true);
    }
}
[HarmonyPatch(typeof(TheWeaver),nameof(TheWeaver.ToggleRank))]
internal static class OfflineWovenRankPatch
{private static void Postfix(TheWeaver __instance,int rank,bool enable)=>OfflineWovenRanks.ApplyRank(__instance,rank,enable);}
[HarmonyPatch(typeof(FactionData.RankData),nameof(FactionData.RankData.GetRankDescription))]
internal static class OfflineWovenRankDescriptionPatch
{
    private static void Postfix(int rank,FactionID id,ref string __result)
    {if(OfflineToolkit.Enabled && id==FactionID.TheWeaver)__result=WovenRankRules.Description(rank,__result);}
}
