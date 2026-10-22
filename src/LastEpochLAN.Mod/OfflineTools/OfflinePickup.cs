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
    private static float nextTrace;
    private static string lastState;
    internal static string Status="Waiting for local inventory";
    internal static int ItemAttempts,ItemCollections;
    // Pickup needs gameplay storage, not character model/network visual readiness.
    internal static bool Local(Actor actor)=>OfflineToolkit.Enabled && actor!=null &&
        actor.Pointer==PlayerFinder.getPlayerActor()?.Pointer && actor.itemContainersManager!=null;
    private static void State(string state)
    {
        Status=state;
        if(state==lastState)return;
        lastState=state;MelonLoader.MelonLogger.Msg("Auto Pickup: "+state);
    }
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
        if(!OfflineToolkit.Enabled){State("waiting: offline play required");return;}
        if(actor==null){State("waiting: local actor unavailable");return;}
        if(actor.itemContainersManager==null){State("waiting: local inventory unavailable");return;}
        if(Time.unscaledTime<nextSweep)return;
        nextSweep=Time.unscaledTime+.05f;
        var manager=GroundItemManager.instance;
        if(manager==null){State("waiting: ground item manager unavailable");return;}
        // Offline-only operation: use the same list as native offline pickup.
        // Avoid the network-dependent list selector and its out-object marshalling.
        var list=manager.offlineItems;
        if(list==null){State("waiting: offline ground item list unavailable");return;}
        State("active: radius 5, stationary pickup enabled");
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
        int count=Math.Min(64,items.Count),specialSeen=0;
        float nearestSquared=float.PositiveInfinity;
        for(int i=0;i<count && items.Count>0 && remaining>0;i++)
        {
            if(itemCursor>=items.Count)itemCursor=0;
            var item=items[itemCursor++];
            if(item==null || !Special(item.itemData))continue;
            specialSeen++;
            var position=actor.position();float dx=position.x-item.location.x,dz=position.z-item.location.z;
            nearestSquared=Math.Min(nearestSquared,dx*dx+dz*dz);
            if(!CanAutoPick(actor,item.location))continue;
            // Native All flags collect the primary item THROUGH vacuum.
            // Keep that native path, restricting its stackable candidates to
            // this selected ground item instead of blocking vacuum itself.
            int before=items.Count;ItemAttempts++;
            using(var scope=new AutomaticPickupScope(item,list))manager.pickupItem(actor,item.id,StackableItemFlags.All);
            if(items.Count<before){ItemCollections++;itemCursor=Math.Max(0,itemCursor-1);}
            remaining--;
        }
        if(items.Count>0 && Time.unscaledTime>=nextTrace)
        {
            nextTrace=Time.unscaledTime+5;
            MelonLoader.MelonLogger.Msg($"Auto Pickup: ground={items.Count}, specialSeen={specialSeen}, nearestSquared={nearestSquared}, attempts={ItemAttempts}, collected={ItemCollections}, keys={OfflineToolkit.Settings.Keys}, materials={OfflineToolkit.Settings.Materials}");
        }
    }

    internal static void Clear()
    {nextSweep=nextTrace=0;goldCursor=potionCursor=boneCursor=itemCursor=0;lastState=null;Status="Waiting for local inventory";ItemAttempts=ItemCollections=0;}
}
internal sealed class AutomaticPickupScope:IDisposable
{
    [ThreadStatic] internal static int Depth;
    [ThreadStatic] private static GroundItemManager.GroundItem selected;
    [ThreadStatic] private static GroundItemManager.GroundItemList source;
    private readonly GroundItemManager.GroundItem previous;
    private readonly GroundItemManager.GroundItemList previousSource;
    internal AutomaticPickupScope(GroundItemManager.GroundItem item=null,GroundItemManager.GroundItemList list=null)
    {previous=selected;previousSource=source;selected=item;source=list;Depth++;}
    internal static bool Allows(ItemData item)
    {
        if(Depth==0)return true;
        if(item==null || selected?.itemData==null || item.Pointer!=selected.itemData.Pointer)return false;
        // Native filtering receives ItemData rather than a ground id. If two
        // drops share that object, do not accidentally collect the other id.
        if(source?.items!=null)
            foreach(var entry in source.items)
                if(entry?.itemData?.Pointer==item.Pointer && entry.id!=selected.id)return false;
        return true;
    }
    public void Dispose(){selected=previous;source=previousSource;Depth--;}
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
