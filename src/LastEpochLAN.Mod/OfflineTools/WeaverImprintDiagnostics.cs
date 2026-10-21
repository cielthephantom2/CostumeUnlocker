using Il2Cpp;
using Il2CppLE.Factions;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class WeaverImprintDiagnostics
{
    internal static object Snapshot()
    {
        try
        {
            var actor=PlayerFinder.getPlayerActor();
            if(!OfflineToolkit.Enabled || actor==null || !WeaverTreeNodesExtensions.IsMemberOfTheWeaver(actor,out var faction) || faction==null)
                return new {ready=false};
            return new {ready=true,cacheChance=faction.OnCacheOpenedChanceForSimilarItems,
                cacheRollChance=MonolithZoneManager.instanceExistsAndInitialised && MonolithZoneManager.instance!=null?
                    (float?)MonolithZoneManager.instance.chanceForOneShotCachesToDropPileOfSimilarItems:null,
                championChance=faction.ImprintContainerChampionsChanceToDropMoreItemsOfType,
                cacheUpdates=OfflineWeaverImprintPointRatesPatch.CacheUpdates,
                championUpdates=OfflineWeaverImprintPointRatesPatch.ChampionUpdates,
                modernTooltipValues=OfflineWeaverImprintTooltipStatPatch.Calls,
                legacyTooltipValues=OfflineWeaverImprintLegacyValuePatch.Calls,
                legacyTooltipDescriptions=OfflineWeaverImprintLegacyInfoPatch.Calls};
        }
        catch(Exception ex){return new {ready=false,error=ex.GetBaseException().Message};}
    }
}
