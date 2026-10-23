using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace LastEpochLAN.Mod.OfflineTools;

// Restore drop-driven map-wide pickup, with one fixed delay per drop. Native
// ground records remain until pickup succeeds; cancellation never loses a grant.
internal static class OfflinePickup
{
    private enum Kind {Item,Gold,Potion,Bone}
    private readonly record struct Key(Kind Type,uint Id,IntPtr Pointer);
    private sealed record Pending(Key Key,float Due,GroundItemManager.GroundItem Item,IntPtr Data,IntPtr Source,PickupGroundObjectInteraction Interaction);
    internal sealed record DropState(int Count,IntPtr Source,float Due);
    private static readonly PriorityQueue<Pending,float> queue=new();
    private static readonly Dictionary<Key,float> deadlines=new();
    private static IntPtr owner,managerPointer;
    internal static bool Local(Actor actor)=>Owned(actor) && actor.itemContainersManager!=null;
    private static bool Owned(Actor actor)=>OfflineToolkit.Enabled && actor!=null && actor.Pointer==PlayerFinder.getPlayerActor()?.Pointer;
    internal static bool Special(ItemData item)=>item!=null && !item.isTutorialItem() &&
        (OfflineToolkit.Settings.Keys && Item.isKey(item.itemType) ||
         OfflineToolkit.Settings.Materials && ItemList.isCraftingItem(item.itemType) ||
         OfflineToolkit.Settings.WovenEchoes && Item.isWovenEcho(item.itemType));
    private static bool Type(PickupGroundObjectInteraction interaction,out Kind kind)
    {
        kind=Kind.Gold;
        if(interaction?.TryCast<GoldPickupInteraction>()!=null)return true;
        if(interaction?.TryCast<PotionPickupInteraction>()!=null){kind=Kind.Potion;return true;}
        if(interaction?.TryCast<PickupAncientBonesInteraction>()!=null){kind=Kind.Bone;return true;}
        return false;
    }
    private static bool Enabled(Kind kind)=>kind switch
    {Kind.Gold=>OfflineToolkit.Settings.Gold,Kind.Potion=>OfflineToolkit.Settings.Potions,Kind.Bone=>OfflineToolkit.Settings.Bones,_=>false};
    private static void Bind(GroundItemManager manager,Actor actor)
    {
        if(managerPointer!=manager.Pointer || owner!=actor.Pointer)Clear();
        managerPointer=manager.Pointer;owner=actor.Pointer;
    }
    private static void Add(Pending entry)
    {if(deadlines.TryAdd(entry.Key,entry.Due))queue.Enqueue(entry,entry.Due);}
    internal static void Enqueue(PickupGroundObjectInteraction interaction)
    {
        var manager=GroundItemManager.instance;
        if(manager==null || interaction==null || !Owned(interaction.player) || !Type(interaction,out var kind) || !Enabled(kind))return;
        Bind(manager,interaction.player);
        var key=new Key(kind,interaction.id,interaction.Pointer);
        Add(new(key,Time.unscaledTime+PickupRules.Delay,null,IntPtr.Zero,manager.offlineItems?.Pointer??IntPtr.Zero,interaction));
    }
    internal static DropState Prepare(GroundItemManager manager,Actor actor,ItemData data,ref Vector3 location,ref bool sound)
    {
        if(manager==null || !Owned(actor) || !Special(data) || manager.offlineItems?.items==null)return null;
        Bind(manager,actor);
        // Same drop placement as the old map-wide pickup. Only the pickup action
        // is deferred; the ground record is still owned/saved by the game.
        location=actor.position();sound=false;
        return new(manager.offlineItems.items.Count,manager.offlineItems.Pointer,Time.unscaledTime+PickupRules.Delay);
    }
    internal static void Dropped(GroundItemManager manager,Actor actor,ItemData data,DropState state)
    {
        var list=manager?.offlineItems;
        if(state==null || !Owned(actor) || list?.items==null || list.Pointer!=state.Source || manager.Pointer!=managerPointer || actor.Pointer!=owner)return;
        for(int i=state.Count;i<list.items.Count;i++)
        {
            var item=list.items[i];
            if(item?.itemData==null || item.itemData.Pointer!=data.Pointer)continue;
            Add(new(new(Kind.Item,item.id,item.Pointer),state.Due,item,item.itemData.Pointer,list.Pointer,null));
        }
    }
    internal static bool AllowNative(PickupGroundObjectInteraction interaction)
    {
        if(interaction==null || !Owned(interaction.player) || !Type(interaction,out var kind) || !Enabled(kind))return true;
        return !deadlines.TryGetValue(new(kind,interaction.id,interaction.Pointer),out float due) || Time.unscaledTime>=due;
    }
    private static bool SameItem(Pending entry)=>entry.Item!=null && entry.Item.id==entry.Key.Id &&
        entry.Item.Pointer==entry.Key.Pointer && entry.Item.itemData?.Pointer==entry.Data;
    internal static void Tick()
    {
        if(queue.Count==0)return;
        if(!OfflineToolkit.Enabled){Clear();return;}
        var actor=PlayerFinder.getPlayerActor();var manager=GroundItemManager.instance;
        if(actor==null || manager==null)return;
        if(actor.Pointer!=owner || manager.Pointer!=managerPointer){Clear();return;}
        if(!Local(actor))return;
        float now=Time.unscaledTime;
        if(!queue.TryPeek(out _,out float due) || now<due)return;
        var items=new List<Pending>();
        // Process every ready drop. No radius, motion gate, polling cadence or
        // per-frame count cap; 0.5s is a wait from each drop, not a throttle.
        while(queue.TryPeek(out var entry,out due) && now>=due)
        {
            queue.Dequeue();deadlines.Remove(entry.Key);
            if(entry.Key.Type==Kind.Item)
            {
                if(manager.offlineItems?.Pointer==entry.Source && SameItem(entry) && Special(entry.Item.itemData))items.Add(entry);
                continue;
            }
            var interaction=entry.Interaction;
            if(entry.Source!=IntPtr.Zero && manager.offlineItems?.Pointer!=entry.Source)continue;
            if(!Enabled(entry.Key.Type) || interaction==null || !interaction.isActiveAndEnabled || interaction.id!=entry.Key.Id || interaction.player?.Pointer!=owner)continue;
            try
            {
                switch(entry.Key.Type)
                {
                    case Kind.Gold:PickupGold(manager,actor,entry.Key.Id,interaction.TryCast<GoldPickupInteraction>());break;
                    case Kind.Potion:
                        var potion=actor.GetComponent<HealthPotion>();
                        if(potion!=null && potion.currentCharges<potion.maxCharges)manager.pickupPotion(actor,entry.Key.Id,interaction.TryCast<PotionPickupInteraction>());
                        break;
                    case Kind.Bone:manager.pickupAncientBone(actor,entry.Key.Id,interaction.TryCast<PickupAncientBonesInteraction>());break;
                }
            }
            catch(Exception ex){Report(ex);}
        }
        if(items.Count==0)return;
        var source=manager.offlineItems;
        var batch=items.Select(entry=>entry.Item).ToArray();
        using(var scope=new AutomaticPickupScope(batch,source))
            foreach(var entry in items)
            {
                if(!SameItem(entry) || !AutomaticPickupScope.Allows(entry.Item.itemData))continue;
                // Preserve native All -> vacuum -> storage. The allow-list
                // prevents vacuum from collecting a newer drop before its delay.
                try{manager.pickupItem(actor,entry.Key.Id,StackableItemFlags.All);}
                catch(Exception ex){Report(ex);}
            }
    }
    private static void PickupGold(GroundItemManager manager,Actor actor,uint id,GoldPickupInteraction interaction)
    {
        // Native pickupGold validates the saved ground-record position, even
        // when called directly. Move only this record for the native transaction.
        var source=manager.offlineItems;
        if(source?.gold==null)return;
        GroundItemManager.GroundGold ground=null;
        foreach(var entry in source.gold)if(entry!=null && entry.id==id){ground=entry;break;}
        if(ground==null)return; // Already collected manually or pooled.
        var location=ground.location;var pointer=ground.Pointer;
        try
        {
            ground.location=actor.position();
            manager.pickupGold(actor,id,interaction);
        }
        finally
        {
            // On rejection/exception restore the floor location. A successful
            // pickup already removed/pooled the record; never touch it again.
            if(manager.offlineItems?.Pointer==source.Pointer)
                foreach(var entry in source.gold)
                    if(entry!=null && entry.Pointer==pointer && entry.id==id){entry.location=location;break;}
        }
    }
    private static void Report(Exception ex)
    {
        string error="Auto Pickup: "+ex.GetBaseException().Message;
        if(OfflineToolkit.Error!=error)MelonLoader.MelonLogger.Warning(error);
        OfflineToolkit.Error=error;
    }
    internal static void Clear(){queue.Clear();deadlines.Clear();owner=managerPointer=IntPtr.Zero;}
}
[HarmonyPatch(typeof(PickupGroundObjectInteraction),nameof(PickupGroundObjectInteraction.initialise))]
internal static class OfflineDelayedPickupInteractionPatch
{private static void Postfix(PickupGroundObjectInteraction __instance)=>OfflinePickup.Enqueue(__instance);}
[HarmonyPatch(typeof(GroundItemManager),nameof(GroundItemManager.dropItemForPlayer))]
internal static class OfflineSpecialPickupPatch
{
    private static void Prefix(GroundItemManager __instance,Actor __0,ItemData __1,ref Vector3 __2,ref bool __3,out OfflinePickup.DropState __state)=>
        __state=OfflinePickup.Prepare(__instance,__0,__1,ref __2,ref __3);
    private static void Postfix(GroundItemManager __instance,Actor __0,ItemData __1,OfflinePickup.DropState __state)=>OfflinePickup.Dropped(__instance,__0,__1,__state);
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
    private static bool Prefix(PickupGroundObjectInteraction __instance)=>OfflinePickup.AllowNative(__instance);
}
[HarmonyPatch(typeof(ItemList),nameof(ItemList.IsStackableItem),new[]{typeof(ItemData)})]
internal static class OfflineAutoPickupItemFilterPatch
{
    private static void Postfix(ItemData __0,ref bool __result)
    {if(__result && AutomaticPickupScope.Depth>0)__result=AutomaticPickupScope.Allows(__0);}
}

// Explicit clicks keep their existing immediate Range Pickup behavior.
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
