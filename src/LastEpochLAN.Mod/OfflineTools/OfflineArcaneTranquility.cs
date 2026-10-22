using HarmonyLib;
using Il2Cpp;
using NativeStats=Il2CppSystem.Collections.Generic.List<Il2Cpp.SkillTreeNode.NodeTooltipStat>;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class ArcaneTranquility
{
    internal const string Tree="arcas",InternalName="Arcane Ascendance Mana Efficiency";
    internal const byte Node=19;
    internal const string Description="All of your spells cost less mana while Arcane Ascendance is active.";
    internal static long RemovedPenalties;
    internal static bool IsNode(SkillTreeNode node)=>OfflineToolkit.Enabled && node?.tree?.treeID==Tree && node.id==Node;
    internal static void Apply(ArcaneAscendanceMutator mutator)
    {
        if(mutator==null || !MeteorNodeRuntime.Local(mutator.actor) || mutator.statsWhileActive==null)return;
        // Native updateMutator creates this exact stat only for Tranquility:
        // untagged ManaRegen, no added/increased value, one -100% more value.
        // Remove it before the active-stat consumer; retain mana efficiency,
        // drain, other regeneration modifiers and every other source of blocking.
        var stats=mutator.statsWhileActive;
        for(int i=stats.Count-1;i>=0;i--)
        {
            var stat=stats[i];
            if(stat!=null && stat.property==SP.ManaRegen && stat.tags==0 && stat.specialTag==0 && stat.extraTag==0 &&
                stat.addedValue==0 && stat.increasedValue==0 && stat.moreValues?.Count==1 && stat.moreValues[0]==-1)
            {stats.RemoveAt(i);RemovedPenalties++;}
        }
    }
}

[HarmonyPatch(typeof(ArcaneAscendanceTree),nameof(ArcaneAscendanceTree.updateMutator))]
internal static class OfflineArcaneTranquilityTreePatch
{
    private static void Postfix(LocalTreeData localTreeData)
    {
        if(!MeteorNodeRuntime.Local(localTreeData?.actor))return;
        ArcaneTranquility.Apply(localTreeData.GetComponent<ArcaneAscendanceMutator>());
    }
}
[HarmonyPatch(typeof(ArcaneAscendanceMutator),nameof(ArcaneAscendanceMutator.Mutate))]
internal static class OfflineArcaneTranquilityCastPatch
{
    // Covers tree initialization before the local actor was available.
    private static void Prefix(ArcaneAscendanceMutator __instance)=>ArcaneTranquility.Apply(__instance);
}
[HarmonyPatch(typeof(Il2CppLocalizationSystem.LocalizationSkills),nameof(Il2CppLocalizationSystem.LocalizationSkills.GetNodeDescription))]
internal static class OfflineArcaneTranquilityDescriptionPatch
{
    private static void Postfix(string treeID,byte nodeID,ref string __result)
    {if(OfflineToolkit.Enabled && treeID==ArcaneTranquility.Tree && nodeID==ArcaneTranquility.Node)__result=ArcaneTranquility.Description;}
}

// Both tooltip builders use the node's serialized stat list. Temporarily omit
// only Tranquility's second (non-scaling downside) row, retaining native indexes
// and formatting for its first efficiency row. Always restore on native failure.
internal sealed class TranquilityTooltipScope:IDisposable
{
    private readonly SkillTreeNode node;
    private readonly NativeStats saved,filtered;
    internal TranquilityTooltipScope(SkillTreeNode owner)
    {
        node=owner;saved=owner.stats;filtered=new();
        for(int i=0;i<saved.Count;i++)if(i!=1)filtered.Add(saved[i]);
        node.stats=filtered;
    }
    internal static TranquilityTooltipScope Begin(SkillTreeNode node)=>ArcaneTranquility.IsNode(node) && node.stats?.Count==2
        ?new(node):null;
    public void Dispose()
    {if(node!=null && node.stats?.Pointer==filtered.Pointer)node.stats=saved;}
}
[HarmonyPatch(typeof(NodeTooltipManager),nameof(NodeTooltipManager.setInfoForNode))]
internal static class OfflineArcaneTranquilityLegacyTooltipPatch
{
    private static void Prefix(SkillTreeNode node,out TranquilityTooltipScope __state)=>__state=TranquilityTooltipScope.Begin(node);
    private static void Finalizer(TranquilityTooltipScope __state)=>__state?.Dispose();
}
[HarmonyPatch(typeof(Il2CppLE.UI.Tooltips.NodeTooltipContentBuilder),nameof(Il2CppLE.UI.Tooltips.NodeTooltipContentBuilder.GetNodeTooltipInfo))]
internal static class OfflineArcaneTranquilityTooltipPatch
{
    private static void Prefix(SkillTreeNode node,out TranquilityTooltipScope __state)=>__state=TranquilityTooltipScope.Begin(node);
    private static void Finalizer(TranquilityTooltipScope __state)=>__state?.Dispose();
}
