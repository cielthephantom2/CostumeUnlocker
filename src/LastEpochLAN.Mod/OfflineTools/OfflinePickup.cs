using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using NVec=System.Numerics.Vector3;

namespace LastEpochLAN.Mod.OfflineTools;

// Read actual ground drops after placement. Never grant at spawn or move a drop
// to the player. Native pickup handles storage, pooling, charges and objectives.
internal static class OfflinePickup
{
    private static int goldCursor,potionCursor,boneCursor,itemCursor;
    private static float nextSweep;
    internal static bool Local(Actor actor)=>OfflineToolkit.Ready && actor!=null && actor.Pointer==PlayerFinder.getPlayerActor()?.Pointer;
    internal static bool Special(ItemData item)=>item!=null && !item.isTutorialItem() &&
        (OfflineToolkit.Settings.Keys && Item.isKey(item.itemType) || OfflineToolkit.Settings.Materials && ItemList.isCraftingItem(item.itemType));
    internal static bool CanAutoPick(Actor actor,Vector3 location)
    {
        if(!Local(actor))return false;
        var p=actor.position();
        return PickupRules.Within(new NVec(p.x,p.y,p.z),new NVec(location.x,location.y,location.z));
    }
    internal static bool Managed(PickupGroundObjectInteraction pickup)=>pickup!=null &&
        (OfflineToolkit.Settings.Gold && pickup.TryCast<GoldPickupInteraction>()!=null ||
        OfflineToolkit.Settings.Potions && pickup.TryCast<PotionPickupInteraction>()!=null ||
        OfflineToolkit.Settings.Bones && pickup.TryCast<PickupAncientBonesInteraction>()!=null);
    internal static void Tick()
    {
        var actor=PlayerFinder.getPlayerActor();
        if(!Local(actor) || Time.unscaledTime<nextSweep)return;
        nextSweep=Time.unscaledTime+.05f;
        var manager=GroundItemManager.instance;
        if(manager==null || !manager.TryGetGroundItemList(actor,out var list) || list==null)return;
        int remaining=8;
        void Visit<T>(Il2CppSystem.Collections.Generic.List<T> entries,ref int cursor,Action<T> pickup) where T:PickupGroundObjectInteraction
        {
            if(entries==null)return;
            int visits=Math.Min(32,entries.Count);
            for(int i=0;i<visits && entries.Count>0 && remaining>0;i++)
            {
                if(cursor>=entries.Count)cursor=0;
                var entry=entries[cursor++];
                if(entry==null || !entry.isActiveAndEnabled || !Local(entry.player) || entry.index<entry.delay || !CanAutoPick(actor,entry.transform.position))continue;
                pickup(entry);remaining--;
            }
        }
        if(OfflineToolkit.Settings.Gold)Visit(manager.activeGoldPiles,ref goldCursor,p=>manager.pickupGold(actor,p.id,p));
        if(OfflineToolkit.Settings.Potions)
        {
            var potion=actor.GetComponent<HealthPotion>();
            if(potion!=null && potion.currentCharges<potion.maxCharges)
                Visit(manager.activePotions,ref potionCursor,p=>{if(potion.currentCharges<potion.maxCharges)manager.pickupPotion(actor,p.id,p);});
        }
        if(OfflineToolkit.Settings.Bones)Visit(manager.activeAncientBones,ref boneCursor,p=>manager.pickupAncientBone(actor,p.id,p));
        var items=list.items;
        if(items==null)return;
        int count=Math.Min(64,items.Count);
        for(int i=0;i<count && items.Count>0 && remaining>0;i++)
        {
            if(itemCursor>=items.Count)itemCursor=0;
            var item=items[itemCursor++];
            if(item==null || !Special(item.itemData) || !CanAutoPick(actor,item.location))continue;
            // Native pickup normally vacuums other stackables in a wider radius.
            // Disable that secondary sweep only for this automatic operation;
            // each eligible ground item is checked against the player's 5-unit circle.
            using(var scope=new AutomaticPickupScope())manager.pickupItem(actor,item.id,StackableItemFlags.All);
            remaining--;
        }
    }
    internal static void Clear()
    {nextSweep=0;goldCursor=potionCursor=boneCursor=itemCursor=0;}
}
internal sealed class AutomaticPickupScope:IDisposable
{
    [ThreadStatic] internal static int Depth;
    internal AutomaticPickupScope()=>Depth++;
    public void Dispose()=>Depth--;
}
[HarmonyPatch(typeof(GroundItemManager),nameof(GroundItemManager.dropGoldForPlayer))]
internal static class OfflineGoldPickupPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(Actor __0,ref int __1)
    {
        if(OfflinePickup.Local(__0) && __1>0)
            __1=LastEpochLAN.Core.OfflineBonusRules.GoldAmount(__1,OfflineEconomy.GoldMultiplier());
    }
}
[HarmonyPatch(typeof(PickupGroundObjectInteraction),nameof(PickupGroundObjectInteraction.OnUpdateTick))]
internal static class OfflineNativeAutoPickupGatePatch
{
    private static bool Prefix(PickupGroundObjectInteraction __instance)=>
        !OfflinePickup.Local(__instance.player) || !OfflinePickup.Managed(__instance) ||
        OfflinePickup.CanAutoPick(__instance.player,__instance.transform.position);
}
[HarmonyPatch(typeof(GroundItemManager),nameof(GroundItemManager.vacuumNearbyStackableItems))]
internal static class OfflineAutoPickupVacuumGatePatch
{private static bool Prefix()=>AutomaticPickupScope.Depth==0;}

// Explicit clicks retain their existing behavior. Automatic pickup has a
// fixed 5-unit radius and works while standing still.
[HarmonyPatch(typeof(GroundItemLabel),nameof(GroundItemLabel.ClickedItem))]
internal static class OfflineRangePickupPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(GroundItemLabel __instance)
    {
        if(!OfflineToolkit.Ready || !OfflineToolkit.Settings.RangePickup) return true;
        var manager=GroundItemManager.instance;var actor=PlayerFinder.getPlayerActor();var data=__instance.getItemData();
        if(data==null || manager?.offlineItems?.items==null) return true;
        foreach(var item in manager.offlineItems.items)
        {
            if(item?.itemData==null || item.itemData.Pointer!=data.Pointer) continue;
            item.location=actor.position();manager.pickupItem(actor,item.id,StackableItemFlags.All);return false;
        }
        return true;
    }
}
