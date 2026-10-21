using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Core;
using LastEpochLAN.Core;

namespace LastEpochLAN.Mod.OfflineTools;

[HarmonyPatch(typeof(CraftingManager),nameof(CraftingManager.Forge))]
internal static class OfflineHopeForgeScopePatch
{
    private static void Prefix(CraftingManager __instance,out HopeForgeState __state)
    {
        bool local=OfflineToolkit.Enabled && __instance?.actor!=null &&
            __instance.actor.Pointer==PlayerFinder.getPlayerActor()?.Pointer;
        var glyph=local ? __instance.support?.getItem() : null;
        bool hope=glyph!=null && glyph.itemType==103 && glyph.subType==0;
        __state=new HopeForgeState(hope ? CraftingManager.rng?.Pointer??IntPtr.Zero : IntPtr.Zero,hope);
    }
    private static void Finalizer(HopeForgeState __state)=>__state?.Dispose();
}

internal sealed class HopeForgeState : IDisposable
{
    private readonly CraftingHopeScope scope;
    private readonly bool leased;
    private bool disposed;
    internal HopeForgeState(IntPtr rng,bool eligible)
    {
        scope=new CraftingHopeScope(rng,eligible);
        if(!eligible || rng==IntPtr.Zero)return;
        try {OfflineHopeRollHook.Acquire();leased=true;}
        catch {scope.Dispose();throw;}
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        try {scope.Dispose();} finally {if(leased)OfflineHopeRollHook.Release();}
    }
}

// Roll is a hot path used by native combat/loot. Attach only while a local Hope
// Forge is executing, then remove our own hook in its finalizer. Nested Hope
// crafts share a lease; inactive scopes still mask the parent's probability.
internal static class OfflineHopeRollHook
{
    private static readonly HarmonyLib.Harmony harmony=new("LastEpochLAN.LocalHopeCraft");
    private static readonly object gate=new();
    private static readonly System.Reflection.MethodInfo target=AccessTools.Method(typeof(RngElement),nameof(RngElement.Roll),new[]{typeof(float)});
    private static int leases;
    internal static void Acquire()
    {
        lock(gate)
        {
            if(leases==0)
            {
                try {harmony.Patch(target,prefix:new HarmonyMethod(AccessTools.Method(typeof(OfflineHopeRollHook),nameof(Prefix))));}
                catch {harmony.UnpatchSelf();throw;}
            }
            leases++;
        }
    }
    internal static void Release()
    {
        lock(gate) {if(leases>0 && --leases==0)harmony.UnpatchSelf();}
    }
    private static void Prefix(RngElement __instance,ref float __0)
    {
        var scope=CraftingHopeScope.Current;
        if(scope!=null)scope.Adjust(__instance.Pointer,ref __0);
    }
}

[HarmonyPatch(typeof(ItemData),nameof(ItemData.getChanceToSealAffix))]
internal static class OfflineDespairChancePatch
{
    private static void Postfix(int __0,ref float __result)
    {if(OfflineToolkit.Enabled)__result=GlyphCraftingRules.DespairChance(__0,__result);}
}

[HarmonyPatch(typeof(Il2CppLE.UI.Tooltips.ItemTooltipContentBuilder),"GetDescription")]
internal static class OfflineGlyphDescriptionPatch
{
    private static void Postfix(ItemDataUnpacked __0,ref string __result)
    {
        if(OfflineToolkit.Enabled && __0!=null && __0.itemType==103)
            __result=GlyphCraftingRules.Description(__0.subType,__result);
    }
}
