using System.Globalization;
using System.Text.RegularExpressions;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.UI.Tooltips;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class DisintegrateExtension
{
    internal const string Tree="dig5",Node="Disintegrate Range";
    internal const byte NodeId=25;
    internal const float RangePerPoint=.30f;
    internal static bool Apply(DisintegrateMutator mutator,LocalTreeData.TreeData tree)
    {
        if(!DisintegrateMovement.Local(mutator?.actor) || tree?.treeID!=Tree || tree.nodes==null)return false;
        var definition=GlobalTreeData.get()?.getTreeData(Tree)?.getNode(NodeId);
        if(definition?.name!=Node)return false;
        int points=0;
        foreach(var node in tree.nodes)if(node!=null && node.id==NodeId)points+=node.pointsAllocated;
        // Native updateMutator derives this field solely from Extension.
        // Assign from allocation so refreshes, twin beams and repeated casts
        // cannot multiply an already modified range. Native global range stats
        // remain part of the beam consumer, outside this node contribution.
        mutator.increasedRange=RangePerPoint*points;
        return true;
    }
    internal static bool IsNode(SkillTreeNode node)=>OfflineToolkit.Enabled && node?.tree?.TryCast<DisintegrateTree>()!=null &&
        node.tree.treeID==Tree && node.id==NodeId && GlobalTreeData.get()?.getTreeData(Tree)?.getNode(NodeId)?.name==Node;
    private static Match Number(string value)=>Regex.Match(value??"",@"^((?:\s|<[^>]+>)*[+]?)(\d+(?:[.,]\d+)?)(\s*)(%?)(.*)$",RegexOptions.Singleline);
    private static bool Read(Match match,out decimal value)
    {value=0;return match.Success && decimal.TryParse(match.Groups[2].Value.Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out value);}
    private static bool RangeStat(string original)
    {var match=Number(original);return Read(match,out var value) && value==12 && match.Groups[4].Value=="%";}
    internal static float Tooltip(string original,float value)=>RangeStat(original)?value*2.5f:value;
    internal static string Tooltip(string original,string displayed)
    {
        if(!RangeStat(original))return displayed;
        var match=Number(displayed);
        if(!Read(match,out var value) || match.Groups[4].Value!="%")return displayed;
        string number=(Math.Min(value,decimal.MaxValue/2.5m)*2.5m).ToString("0.####",CultureInfo.InvariantCulture);
        if(match.Groups[2].Value.Contains(','))number=number.Replace('.',',');
        return match.Groups[1].Value+number+match.Groups[3].Value+match.Groups[4].Value+match.Groups[5].Value;
    }
    internal static string Description(string original)=>Regex.Replace(original??"",@"(?<![\d.,])12\s*%","30%");
}

[HarmonyPatch(typeof(DisintegrateTree),nameof(DisintegrateTree.updateMutator))]
internal static class OfflineDisintegrateExtensionTreePatch
{
    private static void Postfix(LocalTreeData localTreeData,LocalTreeData.TreeData tree)
    {if(localTreeData!=null && DisintegrateMovement.Local(localTreeData.actor))DisintegrateExtension.Apply(localTreeData.GetComponent<DisintegrateMutator>(),tree);}
}
[HarmonyPatch(typeof(DisintegrateMutator),nameof(DisintegrateMutator.Mutate))]
internal static class OfflineDisintegrateExtensionCastPatch
{
    private static void Prefix(DisintegrateMutator __instance)
    {if(DisintegrateMovement.Local(__instance?.actor))DisintegrateExtension.Apply(__instance,__instance.actor.localTreeData?.getSkillTreeData(Ability.getAbility(AbilityID.disintegrate)));}
}
[HarmonyPatch(typeof(NodeTooltipContentBuilder),nameof(NodeTooltipContentBuilder.GetModifierInfo))]
internal static class OfflineDisintegrateExtensionValuePatch
{
    private static void Postfix(SkillTreeNode.NodeTooltipStat stat,SkillTreeNode node,NodeTooltipModifierInfo __result)
    {if(stat!=null && __result!=null && DisintegrateExtension.IsNode(node))__result.Value=DisintegrateExtension.Tooltip(stat.value,__result.Value);}
}
[HarmonyPatch(typeof(NodeTooltipManager),nameof(NodeTooltipManager.BuildColoredValue))]
internal static class OfflineDisintegrateExtensionLegacyValuePatch
{
    private static void Prefix(SkillTreeNode.NodeTooltipStat stat,SkillTreeNode node,ref string valueText)
    {if(stat!=null && DisintegrateExtension.IsNode(node))valueText=DisintegrateExtension.Tooltip(stat.value,valueText);}
}
[HarmonyPatch(typeof(Il2CppLocalizationSystem.LocalizationSkills),nameof(Il2CppLocalizationSystem.LocalizationSkills.GetNodeDescription))]
internal static class OfflineDisintegrateExtensionDescriptionPatch
{
    private static void Postfix(string treeID,byte nodeID,ref string __result)
    {if(OfflineToolkit.Enabled && treeID==DisintegrateExtension.Tree && nodeID==DisintegrateExtension.NodeId)__result=DisintegrateExtension.Description(__result);}
}
[HarmonyPatch(typeof(NodeTooltipContentBuilder),nameof(NodeTooltipContentBuilder.GetNodeTooltipInfo))]
internal static class OfflineDisintegrateExtensionInfoPatch
{
    private static void Postfix(SkillTreeNode node,NodeTooltipInfo __result)
    {if(__result!=null && DisintegrateExtension.IsNode(node))__result.Description=DisintegrateExtension.Description(__result.Description);}
}
[HarmonyPatch(typeof(NodeTooltipManager),nameof(NodeTooltipManager.setInfoForNode))]
internal static class OfflineDisintegrateExtensionLegacyInfoPatch
{
    private static void Postfix(NodeTooltipManager __instance,SkillTreeNode node)
    {
        if(__instance?.description==null || !DisintegrateExtension.IsNode(node))return;
        var old=__instance.description.text;var text=DisintegrateExtension.Description(old);
        if(old!=text){__instance.description.text=text;__instance.requireLayoutUpdate=true;}
    }
}
