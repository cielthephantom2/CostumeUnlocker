using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace LastEpochLAN.Mod.OfflineTools;

// Reuse the game's pooled gold trigger for stackable items. No managed Update,
// movement sampler, ground-list sweep, direct inventory grant or loot relocation.
internal static class OfflinePickup
{
    internal const float RangeMultiplier=3;
    private sealed record Bridge(GroundItemVisuals Visuals,GoldPickupInteraction Trigger,AutoPool.Subscriber<GoldPickupInteraction> Pool);
    private static readonly Dictionary<IntPtr,Bridge> bridges=new();
    private static readonly Dictionary<IntPtr,Bridge> byVisuals=new();
    [ThreadStatic] internal static int ItemPickupDepth;
    internal static bool Local(Actor actor)=>OfflineToolkit.Ready && actor!=null && actor.Pointer==PlayerFinder.getPlayerActor()?.Pointer;
    private static bool Owned(Actor actor)=>OfflineToolkit.Enabled && actor!=null && actor.Pointer==PlayerFinder.getPlayerActor()?.Pointer;
    internal static bool Special(ItemData item)=>item!=null && !item.isTutorialItem() &&
        (OfflineToolkit.Settings.Keys && Item.isKey(item.itemType) || OfflineToolkit.Settings.Materials && ItemList.isCraftingItem(item.itemType));
    internal static void Configure(PickupGroundObjectInteraction interaction)
    {
        if(interaction==null)return;
        bool owned=Owned(interaction.player);
        var manager=GroundItemManager.instance;
        if(manager==null)return;
        PickupGroundObjectInteraction prefab=null;bool enabled=false;
        if(interaction.TryCast<GoldPickupInteraction>()!=null)
        {
            prefab=manager.goldPileSubscriber?.Pool?.prefabT;
            enabled=owned && (bridges.TryGetValue(interaction.Pointer,out var bridge)?Special(bridge.Visuals.itemData):OfflineToolkit.Settings.Gold);
        }
        else if(interaction.TryCast<PotionPickupInteraction>()!=null)
        {prefab=manager.potionSubscriber?.Pool?.prefabT;enabled=owned && OfflineToolkit.Settings.Potions;}
        else if(interaction.TryCast<PickupAncientBonesInteraction>()!=null)
        {prefab=manager.ancientBoneSubscriber?.Pool?.prefabT;enabled=owned && OfflineToolkit.Settings.Bones;}
        if(prefab==null)return;
        var original=prefab.GetComponentsInChildren<Collider>(true);
        var current=interaction.GetComponentsInChildren<Collider>(true);
        for(int i=0;i<Math.Min(original.Length,current.Length);i++)
        {
            var a=original[i];var b=current[i];if(a==null || b==null || !a.isTrigger)continue;
            float scale=enabled?RangeMultiplier:1;
            if(a.TryCast<SphereCollider>() is {} sphere && b.TryCast<SphereCollider>() is {} targetSphere)targetSphere.radius=sphere.radius*scale;
            else if(a.TryCast<CapsuleCollider>() is {} capsule && b.TryCast<CapsuleCollider>() is {} targetCapsule)
            {targetCapsule.radius=capsule.radius*scale;targetCapsule.height=capsule.height*scale;}
            else if(a.TryCast<BoxCollider>() is {} box && b.TryCast<BoxCollider>() is {} targetBox)targetBox.size=box.size*scale;
            // Special-item triggers are optional. Gold/potion/bones retain native
            // pickup at their original radius when the enhancement is disabled.
            b.enabled=!bridges.ContainsKey(interaction.Pointer) || enabled;
        }
    }
    internal static void Attach(GroundItemVisuals visuals)
    {
        Detach(visuals);
        var actor=PlayerFinder.getPlayerActor();
        if(!Owned(actor) || visuals.itemData==null || visuals.itemData.isTutorialItem() ||
           !(Item.isKey(visuals.itemData.itemType) || ItemList.isCraftingItem(visuals.itemData.itemType)))return;
        var pool=GroundItemManager.instance?.goldPileSubscriber;
        if(pool?.Pool?.prefabT==null)return;
        var trigger=pool.instantiate(visuals.transform.position,Quaternion.identity,null,true);
        if(trigger==null)return;
        try
        {
            trigger.gameObject.layer=actor.gameObject.layer;
            trigger.transform.SetParent(visuals.transform,true);
            var bridge=new Bridge(visuals,trigger,pool);
            bridges.Add(trigger.Pointer,bridge);byVisuals.Add(visuals.Pointer,bridge);
            trigger.initialise(actor,visuals.id);
            Configure(trigger);
            trigger.gameObject.SetActive(true);
        }
        catch {bridges.Remove(trigger.Pointer);byVisuals.Remove(visuals.Pointer);pool.Release(trigger);throw;}
    }
    internal static bool PickUp(GoldPickupInteraction interaction)
    {
        if(!bridges.TryGetValue(interaction.Pointer,out var bridge))return true;
        if(!Owned(interaction.player) || !Special(bridge.Visuals.itemData))return false;
        // Each item has its own native trigger. Avoid the native secondary
        // vacuum reaching beyond those triggers or collecting disabled types.
        ItemPickupDepth++;
        try {GroundItemManager.instance.pickupItem(interaction.player,bridge.Visuals.id,StackableItemFlags.All);}
        finally {ItemPickupDepth--;}
        return false;
    }
    internal static void Detach(GroundItemVisuals visuals)
    {
        if(byVisuals.TryGetValue(visuals.Pointer,out var bridge))Release(bridge);
    }
    private static void Release(Bridge bridge)
    {bridges.Remove(bridge.Trigger.Pointer);byVisuals.Remove(bridge.Visuals.Pointer);if(bridge.Trigger!=null)bridge.Pool.Release(bridge.Trigger);}
    internal static void Clear()
    {foreach(var bridge in bridges.Values.ToArray())Release(bridge);}
    internal static void Refresh()
    {
        var manager=GroundItemManager.instance;if(manager==null || !OfflineToolkit.Ready)return;
        if(manager.activeGoldPiles!=null)foreach(var x in manager.activeGoldPiles)Configure(x);
        if(manager.activePotions!=null)foreach(var x in manager.activePotions)Configure(x);
        if(manager.activeAncientBones!=null)foreach(var x in manager.activeAncientBones)Configure(x);
        foreach(var b in bridges.Values)Configure(b.Trigger);
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
{private static bool Prefix(GoldPickupInteraction __instance)=>OfflinePickup.PickUp(__instance);}
[HarmonyPatch(typeof(GroundItemManager),nameof(GroundItemManager.vacuumNearbyStackableItems))]
internal static class OfflineNativeItemVacuumPatch
{private static bool Prefix()=>OfflinePickup.ItemPickupDepth==0;}

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

// Explicit click pickup retains its existing behavior.
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
