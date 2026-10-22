using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using NVec=System.Numerics.Vector3;

namespace LastEpochLAN.Mod.OfflineTools;

// Read actual ground drops after placement. Never grant at spawn or move a drop
// to the player. Native pickup handles storage, pooling, charges and objectives.
internal static class OfflinePickup
{
    private static int goldCursor,potionCursor,boneCursor,itemCursor,nextKind;
    private static float nextSweep;
    // Pickup needs gameplay storage, not character model/network visual readiness.
    internal static bool Local(Actor actor)=>OfflineToolkit.Enabled && actor!=null &&
        actor.Pointer==PlayerFinder.getPlayerActor()?.Pointer && actor.itemContainersManager!=null;
    internal static bool Special(ItemData item)=>item!=null && !item.isTutorialItem() &&
        (OfflineToolkit.Settings.Keys && Item.isKey(item.itemType) ||
         OfflineToolkit.Settings.Materials && ItemList.isCraftingItem(item.itemType) ||
         OfflineToolkit.Settings.WovenEchoes && Item.isWovenEcho(item.itemType));
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
        if(Time.unscaledTime<nextSweep)return;
        nextSweep=Time.unscaledTime+PickupRules.SweepInterval;
        var actor=PlayerFinder.getPlayerActor();if(!Local(actor))return;
        var manager=GroundItemManager.instance;var list=manager?.offlineItems;if(list==null)return;
        int visits=PickupRules.MaximumVisits;
        bool Visit<T>(Il2CppSystem.Collections.Generic.List<T> entries,ref int cursor,Action<T> pickup) where T:PickupGroundObjectInteraction
        {
            if(entries==null)return false;
            int count=Math.Min(visits,entries.Count);
            for(int i=0;i<count && entries.Count>0;i++)
            {
                if(cursor>=entries.Count)cursor=0;
                var entry=entries[cursor++];visits--;
                if(entry==null || !entry.isActiveAndEnabled || !Local(entry.player) || entry.index<entry.delay || !CanAutoPick(actor,entry.transform.position))continue;
                pickup(entry);return true;
            }
            return false;
        }
        // Rotate the starting category so a long list of far gold cannot starve
        // keys/materials, or the reverse. One shared budget across all lists.
        int start=nextKind;
        for(int offset=0;offset<4 && visits>0;offset++)
        {
            int kind=(start+offset)%4;nextKind=(kind+1)%4;
            if(kind==0 && OfflineToolkit.Settings.Gold && Visit(manager.activeGoldPiles,ref goldCursor,p=>manager.pickupGold(actor,p.id,p)))return;
            if(kind==1 && OfflineToolkit.Settings.Potions)
            {
                var potion=actor.GetComponent<HealthPotion>();
                if(potion!=null && potion.currentCharges<potion.maxCharges && Visit(manager.activePotions,ref potionCursor,p=>manager.pickupPotion(actor,p.id,p)))return;
            }
            if(kind==2 && OfflineToolkit.Settings.Bones && Visit(manager.activeAncientBones,ref boneCursor,p=>manager.pickupAncientBone(actor,p.id,p)))return;
            if(kind!=3 || list.items==null)continue;
            var items=list.items;int count=Math.Min(visits,items.Count);
            var batch=new List<GroundItemManager.GroundItem>(PickupRules.MaximumVisits);
            for(int i=0;i<count && items.Count>0;i++)
            {
                if(itemCursor>=items.Count)itemCursor=0;
                var item=items[itemCursor++];visits--;
                if(item!=null && Special(item.itemData) && CanAutoPick(actor,item.location))batch.Add(item);
            }
            if(batch.Count==0)continue;
            // All flags store stackables THROUGH native vacuum. Permit the
            // nearby candidates already approved within this pass's budget,
            // so one native action can collect the surrounding batch.
            using(var scope=new AutomaticPickupScope(batch,list))
            {
                var primary=scope.Primary;if(primary==null)continue;
                int before=items.Count;
                manager.pickupItem(actor,primary.id,StackableItemFlags.All);
                itemCursor=Math.Max(0,itemCursor-(before-items.Count));
            }
            return;
        }
    }
    internal static void Clear()
    {nextSweep=0;goldCursor=potionCursor=boneCursor=itemCursor=nextKind=0;}
}
internal sealed class AutomaticPickupScope:IDisposable
{
    [ThreadStatic] internal static int Depth;
    [ThreadStatic] private static HashSet<IntPtr> allowed;
    private readonly HashSet<IntPtr> previous;
    internal readonly GroundItemManager.GroundItem Primary;
    internal AutomaticPickupScope(IReadOnlyList<GroundItemManager.GroundItem> batch=null,GroundItemManager.GroundItemList source=null)
    {
        var permitted=new HashSet<IntPtr>();
        if(batch!=null)
        {
            var identities=new Dictionary<IntPtr,HashSet<uint>>();
            foreach(var entry in batch)
            {
                if(entry?.itemData==null)continue;
                var pointer=entry.itemData.Pointer;
                if(!identities.TryGetValue(pointer,out var ids))identities.Add(pointer,ids=new());
                ids.Add(entry.id);permitted.Add(pointer);
            }
            // Native classification receives ItemData, not a ground id. One
            // identity pass prevents shared data from admitting unapproved ids.
            if(source?.items!=null)foreach(var entry in source.items)
                if(entry?.itemData!=null && identities.TryGetValue(entry.itemData.Pointer,out var ids) && !ids.Contains(entry.id))permitted.Remove(entry.itemData.Pointer);
            Primary=batch.FirstOrDefault(entry=>entry?.itemData!=null && permitted.Contains(entry.itemData.Pointer));
        }
        // Publish scope only after validation; constructor failure cannot leave
        // the native classifier stuck inside an automatic pickup scope.
        previous=allowed;allowed=permitted;Depth++;
    }

    internal static bool Allows(ItemData item)=>Depth==0 || item!=null && allowed?.Contains(item.Pointer)==true;
    public void Dispose(){allowed=previous;Depth--;}
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
[HarmonyPatch(typeof(ItemList),nameof(ItemList.IsStackableItem),new[]{typeof(ItemData)})]
internal static class OfflineAutoPickupItemFilterPatch
{
    private static void Postfix(ItemData __0,ref bool __result)
    {if(__result && AutomaticPickupScope.Depth>0)__result=AutomaticPickupScope.Allows(__0);}
}

// Explicit clicks retain their existing behavior. Automatic pickup has a
// fixed 5-unit radius and works while standing still.
[HarmonyPatch(typeof(GroundItemLabel),nameof(GroundItemLabel.ClickedItem))]
internal static class OfflineRangePickupPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(GroundItemLabel __instance)
    {
        if(!OfflinePickup.Local(PlayerFinder.getPlayerActor()) || !OfflineToolkit.Settings.RangePickup) return true;
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
