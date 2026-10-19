using HarmonyLib;
using Il2Cpp;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class OfflineToolkitSmoke
{
    internal static int PatchClasses;
    private static bool specialsStarted;
    private static int keysBefore,materialBefore;
    private static float pickupDeadline;
    internal static object BonusObservations;
    internal static object FortuneObservations;
    private static Dps.DpsNativeSmoke dps;
    internal static object DpsObservations;
    private static NativePartyStatusSmoke status;
    internal static object SharedStatusObservations;
    internal static bool VerifyPrivateStatus(Actor actor)
    {
        status??=new NativePartyStatusSmoke(actor);
        try
        {
            if(!status.Tick()) return false;
            SharedStatusObservations=status.Observations;status.Dispose();status=null;return true;
        }
        catch {status.Dispose();status=null;throw;}
    }
    internal static bool VerifyPrivateDps(Actor actor)
    {
        if(DpsObservations!=null) return true;
        dps ??= new Dps.DpsNativeSmoke(actor);
        try
        {
            if(!dps.Tick()) return false;
            DpsObservations=dps.Observations;dps.Dispose();dps=null;return true;
        }
        catch {dps.Dispose();dps=null;throw;}
    }
    internal static void VerifyPrivateBonuses(Actor actor)
    {
        if(!Environment.GetCommandLineArgs().Contains("--lelan-offline-tools-smoke")) throw new InvalidOperationException("Private bonus QA flag required.");
        var buffs=actor.statBuffs;
        OfflineBuffs.Ensure(buffs);
        int initial=buffs.buffs.Count;
        for(int repeat=0;repeat<20;repeat++) OfflineBuffs.Ensure(buffs);
        if(buffs.buffs.Count!=initial) throw new InvalidOperationException("Fixed buffs stacked during repeated refresh.");
        foreach(var spec in OfflineBuffs.Specs)
        {
            if(!buffs.activeBuffNames.TryGetValue(spec.Name,out var buff) || buff==null || buff.stat==null ||
                buff.stat.property!=spec.Property || buff.stat.addedValue!=spec.Added || buff.stat.increasedValue!=spec.Increased)
                throw new InvalidOperationException("Native fixed buff missing or incorrect: "+spec.Name);
        }
        UniqueList.Entry entry=null;
        foreach(var unique in UniqueList.instance.uniques)
            if(unique!=null && !unique.isSetItem && unique.legendaryType==UniqueList.LegendaryType.LegendaryPotential && unique.subTypes?.Count>0)
            {entry=unique;break;}
        if(entry==null) throw new InvalidOperationException("No native LP-capable unique found.");
        var item=ItemData.DummyData(entry.baseType,entry.subTypes[0],0);
        var rolls=new List<int>();
        for(int repeat=0;repeat<32;repeat++)
        {
            int potential=item.RollLegendaryPotential(entry,0,1,0,1,out bool improved,1);
            if(potential<1 || potential>4) throw new InvalidOperationException("Native minimum LP not respected.");
            rolls.Add(potential);
        }
        BonusObservations=new {nativeFixedBuffs=true,noDuplicateBuffsAfter20Refreshes=true,
            nativeLpRolls=rolls.ToArray(),lpUnique=entry.uniqueID,
            monolithGoldGameplayValidated=false};
    }
    internal static void VerifyHooks()
    {
        var expected=typeof(OfflineToolkit).Assembly.GetTypes().Where(type=>(type.Namespace==typeof(OfflineToolkit).Namespace || type.Namespace?.StartsWith(typeof(OfflineToolkit).Namespace+".")==true) &&
            type.GetCustomAttributes(typeof(HarmonyPatch),false).Length>0).ToArray();
        var actual=new HashSet<Type>();
        foreach(var method in HarmonyLib.Harmony.GetAllPatchedMethods())
        {
            var info=HarmonyLib.Harmony.GetPatchInfo(method);
            if(info==null) continue;
            foreach(var patch in info.Prefixes.Concat(info.Postfixes).Concat(info.Finalizers).Concat(info.Transpilers))
                if(patch.PatchMethod.DeclaringType!=null) actual.Add(patch.PatchMethod.DeclaringType);
        }
        var missing=expected.Where(type=>!actual.Contains(type)).Select(type=>type.Name).ToArray();
        if(missing.Length>0) throw new InvalidOperationException("Offline tools native hooks missing: "+string.Join(", ",missing));
        PatchClasses=expected.Length;
        FortuneObservations=OfflineFortune.NativeSmoke();
        MelonLoader.MelonLogger.Msg("Offline tools native hook registration verified: "+PatchClasses+" classes.");
    }
    internal static bool VerifyPrivatePickups(Actor actor)
    {
        if(!Environment.GetCommandLineArgs().Contains("--lelan-offline-tools-smoke")) throw new InvalidOperationException("Private pickup QA flag required.");
        if(specialsStarted)
        {
            var items=actor.itemContainersManager;
            bool complete=CountKeys(items)>keysBefore && (items.materials.shattering.content?.Quantity??0)>materialBefore;
            if(!complete && UnityEngine.Time.unscaledTime>pickupDeadline) throw new InvalidOperationException("Native key/material auto-pickup did not reach storage.");
            return complete;
        }
        var ground=GroundItemManager.instance;
        if(ground==null || actor.goldTracker==null) throw new InvalidOperationException("Private pickup QA requires native ground/gold tracker.");
        int gold=actor.goldTracker.value;
        ground.dropGoldForPlayer(actor,7,actor.position()+UnityEngine.Vector3.right*40,false);
        if(actor.goldTracker.value!=gold+7) throw new InvalidOperationException("Native offline auto-gold did not grant exactly once.");
        var potion=actor.GetComponent<HealthPotion>();
        if(potion==null || potion.maxCharges<=0) throw new InvalidOperationException("Private pickup QA requires a native health potion.");
        int charges=potion.currentCharges;
        try
        {
            potion.currentCharges=potion.maxCharges-1;
            ground.dropPotionForPlayer(actor,actor.position()+UnityEngine.Vector3.right*40,false);
            if(potion.currentCharges!=potion.maxCharges) throw new InvalidOperationException("Native offline auto-potion did not refill one charge.");
        }
        finally {potion.currentCharges=charges;}
        var bones=actor.ancientBonesTracker;
        if(bones!=null)
        {
            int count=bones.value;
            ground.dropAncientBoneForPlayer(actor,3,actor.position()+UnityEngine.Vector3.right*40,false,false);
            if(bones.value!=count+3) throw new InvalidOperationException("Native offline auto-bones did not grant exactly once.");
        }
        var manager=actor.itemContainersManager;
        if(manager?.keys==null || manager.materials?.shattering==null) throw new InvalidOperationException("Native key/material storage is unavailable.");
        int keyType=-1;
        for(int type=0;type<256;type++) if(Item.isKey(type)) {keyType=type;break;}
        if(keyType<0) throw new InvalidOperationException("Native key item type unavailable.");
        keysBefore=CountKeys(manager);materialBefore=manager.materials.shattering.content?.Quantity??0;
        var key=ItemData.DummyData((byte)keyType,0,0);key.RebuildID();
        var storage=manager.materials.shattering;
        var material=ItemData.DummyData((byte)storage.allowedBaseType[0],(ushort)storage.allowedSubID,0);material.RebuildID();
        specialsStarted=true;pickupDeadline=UnityEngine.Time.unscaledTime+10;
        ground.dropItemForPlayer(actor,key,actor.position()+UnityEngine.Vector3.right*40,false);
        ground.dropItemForPlayer(actor,material,actor.position()+UnityEngine.Vector3.right*40,false);
        return false;
    }
    private static int CountKeys(ItemContainersManager items)
    {
        int total=0;
        foreach(var entry in items.keys.Cast<IItemContainer>().GetContent()) total+=entry.Quantity;
        return total;
    }
}
