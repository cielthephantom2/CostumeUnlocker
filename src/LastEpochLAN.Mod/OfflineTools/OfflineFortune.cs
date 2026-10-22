using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Factions;
using Il2CppLE.Core;
using LastEpochLAN.Core;
using UnityEngine;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class OfflineFortune
{
    private sealed record RewardEdit(ProphecySlotReward Reward,int Original,int Applied);
    private static readonly Dictionary<IntPtr,RewardEdit> rewards=new();
    private static float next;
    internal static bool EnabledAt(CircleOfFortune faction,int rank)=>OfflineToolkit.Enabled && faction?.IsMember==true &&
        faction.Rank>=rank && (faction.rankToggles==null || !faction.rankToggles.TryGetValue(rank,out var enabled) || enabled);
    internal static CircleOfFortune Fortune(IFactionInfoProvider provider)=>provider!=null && provider.TryGetFaction(FactionID.CircleOfFortune,out var faction)
        ? faction?.TryCast<CircleOfFortune>() : null;

    internal static void ApplyRank(CircleOfFortune faction,int rank,bool enabled)
    {
        if(!enabled || !EnabledAt(faction,rank) || faction.CachedPlayerActor==null || faction.CachedPlayerActor.Pointer!=PlayerFinder.getPlayerActor()?.Pointer) return;
        switch(rank)
        {
            case 1: faction.ChanceForDoubleEnemyRewards0To1=FortuneRules.EnemyDoubleChance;break;
            case 3:
                var generator=faction.CachedPlayerActor?.generateItems;
                if(generator!=null){generator.ChanceForAdditionalIdols0To1=FortuneRules.IdolExtraChance;generator.AdditionalIdols=FortuneRules.AdditionalIdols;}
                break;
            case 5: faction.ChanceForDoubleEchoRewards0To1=FortuneRules.EchoDoubleChance;break;
            case 6: faction.ChanceToForceExaltedMultiplier=FortuneRules.ExaltedMultiplier;break;
            case 8: faction.UniquesLPRollMultiplier=FortuneRules.LpMultiplier;break;
            case 10: faction.IncreasedChanceToDropBossSpecificLoot=FortuneRules.BossSpecificLootIncrease;break;
            case 11:
                if(faction.CachedPlayerActor.generateItems!=null)faction.CachedPlayerActor.generateItems.ChanceForRareItemsToBecomeExalted0To1=FortuneRules.RareToExaltedChance;
                break;
        }
    }
    internal static void ScaleRewards(ProphecyData data)
    {
        if(!OfflineToolkit.Enabled || data?.rewards==null) return;
        var visited=new HashSet<IntPtr>();
        foreach(var reward in data.rewards)
        {
            for(var tier=reward;tier!=null && visited.Add(tier.Pointer);tier=tier.upgradedReward)
            {
                if(rewards.TryGetValue(tier.Pointer,out var edit) && edit.Reward!=null && tier.itemsDropped==edit.Applied) continue;
                int original=tier.itemsDropped,scaled=FortuneRules.ProphecyCount(original);
                if(scaled==original) continue;
                tier.itemsDropped=scaled;
                rewards[tier.Pointer]=new(tier,original,scaled);
            }
        }
    }
    internal static void RestoreRewards()
    {
        foreach(var edit in rewards.Values)
            if(edit.Reward!=null && edit.Reward.itemsDropped==edit.Applied) edit.Reward.itemsDropped=edit.Original;
        rewards.Clear();
    }
    internal static void Tick()
    {
        if(!OfflineToolkit.Enabled){RestoreRewards();return;}
        if(!OfflineToolkit.Ready || Time.unscaledTime<next) return;
        next=Time.unscaledTime+1;
        ScaleRewards(ProphecyData.Data);
        var faction=Fortune(PlayerFinder.getPlayerActor()?.FactionInfo);
        if(faction==null) return;
        foreach(int rank in new[]{1,3,5,6,8,10,11}) if(EnabledAt(faction,rank)) ApplyRank(faction,rank,true);
    }
    internal static object NativeSmoke()
    {
        var reward=ScriptableObject.CreateInstance<ProphecySlotReward>();
        var data=ScriptableObject.CreateInstance<ProphecyData>();
        try
        {
            reward.itemsDropped=10;
            data.rewards=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<ProphecySlotReward>(new[]{reward});
            ScaleRewards(data);ScaleRewards(data);
            if(reward.ItemsDropped!=20 || reward.GetItemsDropped(3)!=60) throw new InvalidOperationException("Native Prophecy count/display mismatch or repeated multiplication.");
            var rng=RunePrisonItemDrops.rng;
            int boosted,unchanged;
            using(var scope=new MageLootScope(rng.Pointer) {Pending=true,Eligible=true})
            {boosted=rng.StochasticRound(2);unchanged=rng.StochasticRound(2);}
            if(boosted!=15 || unchanged!=2 || MageLootScope.Current!=null) throw new InvalidOperationException("Native scoped mage quantity did not replace x2 with x15 exactly once.");
            return new {nativeProphecyItems=20,nativeThreeChargeItems=60,nativeMageQuantity=boosted,nextNativeRound=unchanged,
                nativeLootGameplayValidated=false,nativeUiRendered=false};
        }
        finally
        {
            if(reward!=null) rewards.Remove(reward.Pointer);
            UnityEngine.Object.Destroy(reward);UnityEngine.Object.Destroy(data);
        }
    }
}

[HarmonyPatch(typeof(CircleOfFortune),nameof(CircleOfFortune.ToggleRank))]
internal static class FortuneRankPatch
{
    private static void Postfix(CircleOfFortune __instance,int rank,bool enable)=>OfflineFortune.ApplyRank(__instance,rank,enable);
}

[HarmonyPatch(typeof(ProphecyData),nameof(ProphecyData.InitializeInstance))]
internal static class FortuneProphecyCatalogPatch
{
    private static void Postfix(ProphecyData __instance)=>OfflineFortune.ScaleRewards(__instance);
}

[HarmonyPatch(typeof(FactionData.RankData),nameof(FactionData.RankData.GetRankDescription))]
internal static class FortuneRankDescriptionPatch
{
    private static void Postfix(FactionData.RankData __instance,int rank,FactionID id,ref string __result)
    {
        if(!OfflineToolkit.Enabled || id!=FactionID.CircleOfFortune) return;
        var replacement=FortuneRules.RankDescription(rank);if(replacement=="") return;
        var lines=__instance.rankDescriptionLines;
        // Native appends slot/lens unlock descriptions after the main reward.
        // Replace just the exact first entry, retaining formatting and unlocks.
        if(lines==null || lines.Count==0) return;
        string original=lines[0];
        if(string.IsNullOrEmpty(original) || __result==null) return;
        int index=__result.IndexOf(original,StringComparison.Ordinal);
        if(index<0) return;
        __result=__result.Substring(0,index)+replacement+__result.Substring(index+original.Length);
        lines[0]=replacement;
    }
}

// Only this one native quantity roll inside Exiled Mage loot is adjusted. No
// item-generation detour with Nullable lens/affix arguments is introduced.
internal sealed class MageLootScope : IDisposable
{
    [ThreadStatic] internal static MageLootScope Current;
    private readonly MageLootScope previous;
    internal bool Pending,Eligible;
    internal readonly IntPtr Rng;
    internal MageLootScope(IntPtr rng){previous=Current;Rng=rng;Current=this;}
    public void Dispose()=>Current=previous;
    internal void Observe(IFactionInfoProvider provider)
    {
        var faction=OfflineFortune.Fortune(provider);
        Eligible=OfflineFortune.EnabledAt(faction,7) && faction.ExiledMagesDropExperimentalItemsTwice;
        Pending=true;
    }
    internal void Adjust(IntPtr rng,ref float value)
    {
        if(!Pending || rng!=Rng) return;
        Pending=false;value=FortuneRules.MageCount(value,Eligible);
    }
}

[HarmonyPatch(typeof(RunePrisonItemDrops),nameof(RunePrisonItemDrops.SpawnExperimentalItem))]
internal static class FortuneMageLootPatch
{
    private static void Prefix(out MageLootScope __state)=>__state=OfflineToolkit.Enabled ? new MageLootScope(RunePrisonItemDrops.rng.Pointer) : null;
    private static void Finalizer(MageLootScope __state)=>__state?.Dispose();
}

[HarmonyPatch(typeof(Actor),"get_FactionInfo")]
internal static class FortuneMageRecipientPatch
{
    private static void Postfix(Actor __instance,IFactionInfoProvider __result)
    {
        if(MageLootScope.Current!=null && __instance.isPlayerActor) MageLootScope.Current.Observe(__result);
    }
}

[HarmonyPatch(typeof(RngElement),nameof(RngElement.StochasticRound),new[]{typeof(float)})]
internal static class FortuneMageQuantityPatch
{
    private static void Prefix(RngElement __instance,ref float __0)=>MageLootScope.Current?.Adjust(__instance.Pointer,ref __0);
}
