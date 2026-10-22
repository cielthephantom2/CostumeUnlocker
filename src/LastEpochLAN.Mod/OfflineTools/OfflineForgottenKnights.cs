using System.Text.RegularExpressions;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Factions;
using UnityEngine;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class ForgottenKnightsRules
{
    internal static float Chance(int rank)=>rank switch{1=>1f,2=>1f,5=>.5f,_=>0f};
    internal static string Description(int rank,string original)
    {
        string baseline=rank switch{1=>"40",2=>"10",5=>"20",_=>null};
        if(baseline==null || original==null)return original;
        string replacement=rank==5?"50":"100";
        // Change only this rank's percentage, retaining +, markup, line breaks
        // and localized wording, including any other numbers in the text.
        return Regex.Replace(original,@"(?<![\d.,])"+baseline+@"(\s*)%",m=>replacement+m.Groups[1].Value+"%");
    }
}
internal static class OfflineForgottenKnights
{
    private static float next;
    internal static bool Local(ForgottenKnights faction)=>OfflineToolkit.Enabled && faction?.IsMember==true &&
        faction.CachedPlayerActor!=null && faction.CachedPlayerActor.Pointer==PlayerFinder.getPlayerActor()?.Pointer;
    internal static bool RankEnabled(ForgottenKnights faction,int rank)=>faction.Rank>=rank &&
        (faction.rankToggles==null || !faction.rankToggles.ContainsKey(rank) || faction.rankToggles[rank]);
    internal static void ApplyRank(ForgottenKnights faction,int rank,bool enable)
    {
        if(!enable || !Local(faction) || !RankEnabled(faction,rank))return;
        switch(rank)
        {
            case 1:faction.AdditionalChanceForSwappableNemesisUnique=ForgottenKnightsRules.Chance(rank);break;
            case 2:faction.DoubleStabilityGainChance=ForgottenKnightsRules.Chance(rank);break;
            case 5:faction.AdditionalChanceForHarbingersToDropTemporalKeystone=ForgottenKnightsRules.Chance(rank);break;
        }
    }
    internal static void Tick()
    {
        if(!OfflineToolkit.Ready || Time.unscaledTime<next)return;
        next=Time.unscaledTime+1;
        var provider=PlayerFinder.getPlayerActor()?.FactionInfo;if(provider==null)return;
        var faction=FactionInfoExtensions.FK(provider);
        // Also repairs initialization that happened before the local actor was
        // discoverable. Absolute assignments cannot stack on repeated refresh.
        foreach(int rank in new[]{1,2,5})ApplyRank(faction,rank,true);
    }
}
[HarmonyPatch(typeof(ForgottenKnights),nameof(ForgottenKnights.ToggleRank))]
internal static class OfflineForgottenKnightsRankPatch
{
    private static void Postfix(ForgottenKnights __instance,int rank,bool enable)=>OfflineForgottenKnights.ApplyRank(__instance,rank,enable);
}
[HarmonyPatch(typeof(FactionData.RankData),nameof(FactionData.RankData.GetRankDescription))]
internal static class OfflineForgottenKnightsDescriptionPatch
{
    private static void Postfix(int rank,FactionID id,ref string __result)
    {if(OfflineToolkit.Enabled && id==FactionID.ForgottenKnights)__result=ForgottenKnightsRules.Description(rank,__result);}
}
