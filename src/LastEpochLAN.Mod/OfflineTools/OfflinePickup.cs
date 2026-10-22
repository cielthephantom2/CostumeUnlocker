using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using NVec=System.Numerics.Vector3;

namespace LastEpochLAN.Mod.OfflineTools;

// Use the game's pooled pickup trigger, condition handler and update callback.
// Only the item-storage dispatch differs from ordinary native gold pickup.
internal static class OfflinePickup
{
    private sealed record Bridge(GroundItemVisuals Visuals,GoldPickupInteraction Trigger,AutoPool.Subscriber<GoldPickupInteraction> Pool);
    private static readonly Dictionary<IntPtr,Bridge> bridges=new();
    private static readonly Dictionary<IntPtr,Bridge> byVisuals=new();
    private static int lastItemFrame=-1;
    private static float nextItemRetry;
    internal static bool Local(Actor actor)=>Owned(actor) && actor.itemContainersManager!=null;
    private static bool Owned(Actor actor)=>OfflineToolkit.Enabled && actor!=null && actor.Pointer==PlayerFinder.getPlayerActor()?.Pointer;
    internal static bool Special(ItemData item)=>EligibleType(item) &&
        (OfflineToolkit.Settings.Keys && Item.isKey(item.itemType) ||
         OfflineToolkit.Settings.Materials && ItemList.isCraftingItem(item.itemType) ||
         OfflineToolkit.Settings.WovenEchoes && Item.isWovenEcho(item.itemType));
    private static bool EligibleType(ItemData item)=>item!=null && !item.isTutorialItem() &&
        (Item.isKey(item.itemType) || ItemList.isCraftingItem(item.itemType) || Item.isWovenEcho(item.itemType));
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
    internal static void Configure(PickupGroundObjectInteraction interaction)
    {
        if(interaction==null)return;
        var manager=GroundItemManager.instance;if(manager==null)return;
        PickupGroundObjectInteraction prefab=null;
        if(interaction.TryCast<GoldPickupInteraction>()!=null)prefab=manager.goldPileSubscriber?.Pool?.prefabT;
        else if(interaction.TryCast<PotionPickupInteraction>()!=null)prefab=manager.potionSubscriber?.Pool?.prefabT;
        else if(interaction.TryCast<PickupAncientBonesInteraction>()!=null)prefab=manager.ancientBoneSubscriber?.Pool?.prefabT;
        if(prefab==null)return;
        var originals=prefab.GetComponentsInChildren<Collider>(true);
        var current=interaction.GetComponentsInChildren<Collider>(true);
        bool enhanced=Owned(interaction.player) && (bridges.ContainsKey(interaction.Pointer) || Managed(interaction));
        for(int i=0;i<Math.Min(originals.Length,current.Length);i++)
        {
            var source=originals[i];var target=current[i];if(source==null || target==null || !source.isTrigger)continue;
            // Prefabs currently use spheres. Keep other collider shapes unchanged.
            // The exact XZ distance is checked again before pickup, accounting for
            // collider overlap and character collision radius at the trigger edge.
            if(source.TryCast<SphereCollider>() is {} sphere && target.TryCast<SphereCollider>() is {} result)
                result.radius=enhanced?Math.Max(sphere.radius,PickupRules.Radius):sphere.radius;
        }
    }
    internal static void Attach(GroundItemVisuals visuals)
    {
        if(visuals==null)return;
        Detach(visuals);
        var actor=PlayerFinder.getPlayerActor();
        if(!Owned(actor) || !EligibleType(visuals.itemData))return;
        var pool=GroundItemManager.instance?.goldPileSubscriber;
        if(pool?.Pool?.prefabT==null)return;
        // Active and unparented first: native Awake binds ConditionHandler before
        // initialise. Item visuals may still be inactive during their spawn delay.
        var trigger=pool.instantiate(visuals.transform.position,Quaternion.identity,null,false);
        if(trigger==null)return;
        try
        {
            trigger.gameObject.layer=actor.gameObject.layer;
            var bridge=new Bridge(visuals,trigger,pool);
            bridges.Add(trigger.Pointer,bridge);byVisuals.Add(visuals.Pointer,bridge);
            trigger.initialise(actor,visuals.id);Configure(trigger);
            // Keep gameplay triggers independent of loot-filtered/inactive
            // visuals, as native gold creation does. Ground drops are stationary;
            // pooling hooks release this trigger together with its item visuals.
        }
        catch{bridges.Remove(trigger.Pointer);byVisuals.Remove(visuals.Pointer);pool.Release(trigger);throw;}
    }
    internal static bool PickUp(GoldPickupInteraction interaction)
    {
        if(!bridges.TryGetValue(interaction.Pointer,out var bridge))return true;
        if(lastItemFrame==Time.frameCount || Time.unscaledTime<nextItemRetry || !Special(bridge.Visuals.itemData) ||
            !CanAutoPick(interaction.player,bridge.Visuals.transform.position))return false;
        var manager=GroundItemManager.instance;var list=manager?.offlineItems;
        if(list?.items==null)return false;
        var p=interaction.player.position();var position=new NVec(p.x,p.y,p.z);
        var batch=new List<GroundItemManager.GroundItem>();
        // A native proximity event starts the batch. No repeated world-list
        // polling and no five-entry cursor delaying a nearby drop behind far loot.
        foreach(var item in list.items)
            if(item!=null && PickupRules.Within(position,new NVec(item.location.x,item.location.y,item.location.z)) && Special(item.itemData))batch.Add(item);
        using(var scope=new AutomaticPickupScope(batch,list))
        {
            if(scope.Primary==null){nextItemRetry=Time.unscaledTime+.25f;return false;}
            lastItemFrame=Time.frameCount;
            // All -> native vacuum -> native storage includes the primary item.
            // Keep the working scope filter; never suppress the vacuum itself.
            int before=list.items.Count;
            try{manager.pickupItem(interaction.player,scope.Primary.id,StackableItemFlags.All);}
            catch{nextItemRetry=Time.unscaledTime+.5f;throw;}
            if(list.items.Count>=before)nextItemRetry=Time.unscaledTime+.25f;
        }
        return false; // This borrowed trigger must never grant native gold.
    }
    internal static void Detach(GroundItemVisuals visuals)
    {if(visuals!=null && byVisuals.TryGetValue(visuals.Pointer,out var bridge))Release(bridge);}
    private static void Release(Bridge bridge)
    {
        bridges.Remove(bridge.Trigger.Pointer);byVisuals.Remove(bridge.Visuals.Pointer);
        if(bridge.Trigger!=null)bridge.Pool.Release(bridge.Trigger);
    }
    internal static void Refresh()
    {
        var manager=GroundItemManager.instance;if(manager==null)return;
        if(manager.activeGoldPiles!=null)foreach(var x in manager.activeGoldPiles)Configure(x);
        if(manager.activePotions!=null)foreach(var x in manager.activePotions)Configure(x);
        if(manager.activeAncientBones!=null)foreach(var x in manager.activeAncientBones)Configure(x);
        foreach(var bridge in bridges.Values)Configure(bridge.Trigger);
    }
    internal static void Clear()
    {
        foreach(var bridge in bridges.Values.ToArray())Release(bridge);
        lastItemFrame=-1;nextItemRetry=0;
    }
}
[HarmonyPatch(typeof(PickupGroundObjectInteraction),nameof(PickupGroundObjectInteraction.initialise))]
internal static class OfflineNativePickupRadiusPatch
{private static void Postfix(PickupGroundObjectInteraction __instance)=>OfflinePickup.Configure(__instance);}
[HarmonyPatch(typeof(GroundItemVisuals),nameof(GroundItemVisuals.initialise))]
internal static class OfflineNativeItemTriggerPatch
{private static void Postfix(GroundItemVisuals __instance)=>OfflinePickup.Attach(__instance);}
[HarmonyPatch(typeof(GroundItemVisuals),nameof(GroundItemVisuals.beforePooling))]
internal static class OfflineNativeItemPoolPatch
{private static void Prefix(GroundItemVisuals __instance)=>OfflinePickup.Detach(__instance);}
[HarmonyPatch(typeof(GroundItemVisuals),nameof(GroundItemVisuals.OnDestroy))]
internal static class OfflineNativeItemDestroyPatch
{private static void Prefix(GroundItemVisuals __instance)=>OfflinePickup.Detach(__instance);}
[HarmonyPatch(typeof(GoldPickupInteraction),nameof(GoldPickupInteraction.PickUp))]
internal static class OfflineNativeItemPickupPatch
{
    private static bool Prefix(GoldPickupInteraction __instance)
    {
        try{return OfflinePickup.PickUp(__instance);}
        catch(Exception ex)
        {
            string error="Auto Pickup: "+ex.GetBaseException().Message;
            if(OfflineToolkit.Error!=error)MelonLoader.MelonLogger.Warning(error);
            OfflineToolkit.Error=error;return false;
        }
    }
}
[HarmonyPatch(typeof(GroundItemManager),nameof(GroundItemManager.BeforeSceneUnloaded))]
internal static class OfflineNativePickupScenePatch
{private static void Prefix()=>OfflinePickup.Clear();}
[HarmonyPatch(typeof(GroundItemManager),nameof(GroundItemManager.ChangeZone))]
internal static class OfflineNativePickupZonePatch
{private static void Prefix()=>OfflinePickup.Clear();}
[HarmonyPatch(typeof(GroundItemManager),nameof(GroundItemManager.OnDestroy))]
internal static class OfflineNativePickupDisposePatch
{private static void Prefix()=>OfflinePickup.Clear();}
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
[HarmonyPatch]
internal static class OfflineNativeAutoPickupGatePatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        foreach(var type in new[]{typeof(GoldPickupInteraction),typeof(PotionPickupInteraction),typeof(PickupAncientBonesInteraction)})
            yield return AccessTools.Method(type,"PickUp");
    }
    // Gate the final action rather than the native callback, so its spawn-delay
    // timer and collision state continue progressing just as ordinary gold does.
    [HarmonyPriority(Priority.First)]
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
