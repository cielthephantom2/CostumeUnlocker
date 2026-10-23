using HarmonyLib;
using Il2Cpp;
using NodeList=Il2CppSystem.Collections.Generic.List<Il2Cpp.LocalTreeData.NodeData>;
using StatList=Il2CppSystem.Collections.Generic.List<Il2Cpp.Stats.Stat>;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class MeteorStardust
{
    internal const string Tree="me27";
    internal const byte Node=10;
    internal const string Description="Meteors deal 1% more damage for every 10 Maximum Mana (multiplicative with other modifiers).";
    internal const string Label="More Damage per 10 Maximum Mana";
    private static IntPtr prepared;
    internal static bool IsNode(SkillTreeNode node)=>OfflineToolkit.Enabled && node?.tree?.treeID==Tree && node.id==Node;
    internal static float More(int mana)=>Math.Max(0,mana)*.001f;
    internal static bool Allocated(MeteorMutator mutator)
    {
        if(!MeteorNodeRuntime.Local(mutator?.actor))return false;
        var tree=mutator.actor.localTreeData?.getSkillTreeData(Ability.getAbility(AbilityID.meteor));
        if(tree?.treeID!=Tree || tree.nodes==null)return false;
        foreach(var node in tree.nodes)if(node!=null && node.id==Node && node.pointsAllocated>0)return true;
        return false;
    }
    internal static void Clear()=>prepared=IntPtr.Zero;
    internal static void Ensure(MeteorMutator mutator)
    {
        if(!MeteorNodeRuntime.Local(mutator?.actor) || prepared==mutator.Pointer)return;
        var local=mutator.actor.localTreeData;var tree=local?.getSkillTreeData(Ability.getAbility(AbilityID.meteor));
        if(tree?.treeID==Tree)MeteorSkillTree.updateMutator(local,tree);
    }
    internal static void Prepared(MeteorMutator mutator){if(mutator!=null)prepared=mutator.Pointer;}
    internal static void Configure(SkillTreeNode node)
    {
        if(!IsNode(node))return;
        node.maxPoints=1;
        if(node.stats?.Count==1 && node.stats[0]?.statName==Label && node.stats[0].noScaling)return;
        var rows=new Il2CppSystem.Collections.Generic.List<SkillTreeNode.NodeTooltipStat>();
        rows.Add(new SkillTreeNode.NodeTooltipStat{statName=Label,value="1%",noScaling=true});node.stats=rows;
        node.description=Description;node.altText="Uses Maximum Mana, including bonuses from equipment and passives. Applies to all damage types.";
    }
    // Return a fresh list: native getTempStats reuses conditionalTempStats and
    // nested calls must never retain our additional stat in that native cache.
    internal static void Add(MeteorMutator mutator,ref StatList result)
    {
        if(result==null || !Allocated(mutator) || mutator.actor.mana==null)return;
        var next=new StatList();foreach(var stat in result)next.Add(stat);
        next.Add(Stats.MoreStat(SP.Damage,0,More(mutator.actor.mana.maxMana),0,0));result=next;
    }
}

internal sealed class StardustTreeScope:IDisposable
{
    private readonly LocalTreeData.TreeData tree;
    private readonly NodeList saved,filtered;
    internal readonly MeteorMutator Mutator;
    internal StardustTreeScope(LocalTreeData local,LocalTreeData.TreeData owner)
    {
        tree=owner;Mutator=local.GetComponent<MeteorMutator>();saved=tree.nodes;filtered=new();
        foreach(var node in saved)
        {
            if(node?.id==MeteorStardust.Node)
            {
                // Native unspent skill points are calculated from allocated
                // nodes, so reducing old ranks refunds the excess naturally.
                if(node.pointsAllocated>1)node.pointsAllocated=1;
            }
            else filtered.Add(node);
        }
        tree.nodes=filtered;
    }
    public void Dispose(){if(tree.nodes?.Pointer==filtered.Pointer)tree.nodes=saved;}
}
[HarmonyPatch(typeof(MeteorSkillTree),nameof(MeteorSkillTree.updateMutator))]
internal static class OfflineMeteorStardustTreePatch
{
    private static void Prefix(LocalTreeData localTreeData,LocalTreeData.TreeData tree,out StardustTreeScope __state)
    {
        __state=MeteorNodeRuntime.Local(localTreeData?.actor) && tree?.treeID==MeteorStardust.Tree && tree.nodes!=null?
            new StardustTreeScope(localTreeData,tree):null;
    }
    private static Exception Finalizer(Exception __exception,StardustTreeScope __state)
    {__state?.Dispose();if(__exception==null && __state!=null)MeteorStardust.Prepared(__state.Mutator);return __exception;}
}
[HarmonyPatch(typeof(MeteorMutator),nameof(MeteorMutator.addsTempStats))]
internal static class OfflineMeteorStardustHasStatsPatch
{
    private static void Prefix(MeteorMutator __instance)=>MeteorStardust.Ensure(__instance);
    private static void Postfix(MeteorMutator __instance,ref bool __result)
    {if(MeteorStardust.Allocated(__instance))__result=true;}
}
[HarmonyPatch(typeof(MeteorMutator),nameof(MeteorMutator.getTempStats))]
internal static class OfflineMeteorStardustStatsPatch
{
    private static void Prefix(MeteorMutator __instance)=>MeteorStardust.Ensure(__instance);
    private static void Postfix(MeteorMutator __instance,ref StatList __result)=>MeteorStardust.Add(__instance,ref __result);
}
[HarmonyPatch(typeof(GlobalTreeData),nameof(GlobalTreeData.getTreeData))]
internal static class OfflineMeteorStardustLimitPatch
{
    private static void Postfix(string id,GlobalTreeData.TreeData __result)
    {if(OfflineToolkit.Enabled && id==MeteorStardust.Tree && __result?.getNode(MeteorStardust.Node) is {} node)node.maxPoints=1;}
}
[HarmonyPatch(typeof(SkillTreeNode),nameof(SkillTreeNode.findTreeAndRegister))]
internal static class OfflineMeteorStardustNodePatch
{private static void Postfix(SkillTreeNode __instance)=>MeteorStardust.Configure(__instance);}
[HarmonyPatch(typeof(SkillTreeNode),nameof(SkillTreeNode.updateText))]
internal static class OfflineMeteorStardustTextPatch
{private static void Prefix(SkillTreeNode __instance)=>MeteorStardust.Configure(__instance);}
[HarmonyPatch(typeof(Il2CppLocalizationSystem.LocalizationSkills),nameof(Il2CppLocalizationSystem.LocalizationSkills.GetNodeDescription))]
internal static class OfflineMeteorStardustDescriptionPatch
{
    private static void Postfix(string treeID,byte nodeID,ref string __result)
    {if(OfflineToolkit.Enabled && treeID==MeteorStardust.Tree && nodeID==MeteorStardust.Node)__result=MeteorStardust.Description;}
}
[HarmonyPatch(typeof(Il2CppLocalizationSystem.LocalizationSkills),nameof(Il2CppLocalizationSystem.LocalizationSkills.GetNodeAltText))]
internal static class OfflineMeteorStardustAltPatch
{
    private static void Postfix(string treeID,byte nodeID,ref string __result)
    {if(OfflineToolkit.Enabled && treeID==MeteorStardust.Tree && nodeID==MeteorStardust.Node)__result="Uses Maximum Mana and applies to all damage types.";}
}
[HarmonyPatch(typeof(Il2CppLocalizationSystem.LocalizationSkills),nameof(Il2CppLocalizationSystem.LocalizationSkills.GetStatName))]
internal static class OfflineMeteorStardustLabelPatch
{
    private static void Postfix(string treeID,byte nodeID,ref string __result)
    {if(OfflineToolkit.Enabled && treeID==MeteorStardust.Tree && nodeID==MeteorStardust.Node)__result=MeteorStardust.Label;}
}
