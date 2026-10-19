using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace LastEpochLAN.Mod.OfflineTools;

// Adapted from HUD 5.1.0 AutoLoot: receive each private drop through native
// trackers/pickup. Equipment is never auto-looted or moved by these hooks.
internal static class OfflinePickup
{
    internal static bool Local(Actor actor)=>OfflineToolkit.Ready && actor!=null && actor.Pointer==PlayerFinder.getPlayerActor().Pointer;
    internal static bool Special(ItemData item)=>item!=null && !item.isTutorialItem() &&
        (OfflineToolkit.Settings.Keys && Item.isKey(item.itemType) || OfflineToolkit.Settings.Materials && ItemList.isCraftingItem(item.itemType));
}
[HarmonyPatch(typeof(GroundItemManager),nameof(GroundItemManager.dropGoldForPlayer))]
internal static class OfflineGoldPickupPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Actor __0,ref int __1)
    {
        if(!OfflinePickup.Local(__0) || __1<=0) return true;
        // Scale each private local drop exactly once, before either native
        // ground placement or our optional auto-pickup can consume it.
        __1=LastEpochLAN.Core.OfflineBonusRules.GoldAmount(__1,OfflineEconomy.GoldMultiplier());
        if(!OfflineToolkit.Settings.Gold || __0.goldTracker==null) return true;
        __0.goldTracker.modifyGold(__1);return false;
    }
}
[HarmonyPatch(typeof(GroundItemManager),nameof(GroundItemManager.dropPotionForPlayer))]
internal static class OfflinePotionPickupPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Actor __0)
    {
        if(!OfflinePickup.Local(__0) || !OfflineToolkit.Settings.Potions) return true;
        var potion=__0.GetComponent<HealthPotion>();
        if(potion==null || potion.maxCharges<=0 || potion.currentCharges>=potion.maxCharges) return true;
        potion.currentCharges++;return false;
    }
}
[HarmonyPatch(typeof(GroundItemManager),nameof(GroundItemManager.dropAncientBoneForPlayer))]
internal static class OfflineBonePickupPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Actor __0,int __1)
    {
        if(!OfflinePickup.Local(__0) || !OfflineToolkit.Settings.Bones || __1<=0) return true;
        var tracker=__0.ancientBonesTracker ?? PlayerFinder.getAncientBonesTracker();
        return tracker==null || !tracker.modifyAncientBones(__1);
    }
}
[HarmonyPatch(typeof(GroundItemManager),nameof(GroundItemManager.dropItemForPlayer))]
internal static class OfflineSpecialPickupPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(Actor __0,ItemData __1,ref Vector3 __2,ref bool __3,out bool __state)
    {
        __state=OfflinePickup.Local(__0) && OfflinePickup.Special(__1);
        if(!__state) return;
        __2=__0.position();__3=false;
    }
    private static void Postfix(GroundItemManager __instance,Actor __0,ItemData __1,bool __state)
    {
        if(!__state || !__instance.TryGetGroundItemList(__0,out var list) || list?.items==null || list.items.Count==0) return;
        var item=list.items[list.items.Count-1];
        if(item?.itemData!=null && item.itemData.Pointer==__1.Pointer)
            __instance.pickupItem(__0,item.id,StackableItemFlags.All);
    }
}
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
