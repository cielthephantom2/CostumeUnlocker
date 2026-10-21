using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Factions;
using Il2CppLE.UI.Tooltips;
using LastEpochLAN.Core;

namespace LastEpochLAN.Mod.OfflineTools;

// Native tooltip values come from serialized node stats/localization, separately
// from TheWeaver derived gameplay fields. Change only the fresh tooltip result.
[HarmonyPatch(typeof(NodeTooltipContentBuilder), nameof(NodeTooltipContentBuilder.GetModifierInfo))]
internal static class OfflineWeaverImprintTooltipStatPatch
{
    internal static long Calls;
    private static void Postfix(SkillTreeNode node, NodeTooltipModifierInfo __result)
    {
        if(!OfflineToolkit.Enabled || __result==null)return;
        var weaver=node?.TryCast<WeaverTreeNode>();
        if(weaver==null || weaver.nodeEffect is not (WeaverTreeNodeEffect.ImprintContainerCachesDropMoreItemsOfType or
            WeaverTreeNodeEffect.ImprintContainerChampionsDropMoreItemsOfType or
            WeaverTreeNodeEffect.ImprintContainerEchoCompletionsDropMoreItemsOfType))return;
        __result.Value=WeaverImprintRules.DoubleTooltipPercent(__result.Value);
        Calls++;
    }
}

[HarmonyPatch(typeof(NodeTooltipContentBuilder), nameof(NodeTooltipContentBuilder.GetNodeTooltipInfo))]
internal static class OfflineWeaverImprintTooltipInfoPatch
{
    private static void Postfix(SkillTreeNode node, NodeTooltipInfo __result)
    {
        if(!OfflineToolkit.Enabled || __result==null)return;
        var weaver=node?.TryCast<WeaverTreeNode>();if(weaver==null)return;
        __result.Description=AppendNote(__result.Description,weaver.nodeEffect);
    }
    internal static string AppendNote(string description,WeaverTreeNodeEffect effect)
    {
        string note=effect switch {
            WeaverTreeNodeEffect.ImprintContainerCachesDropMoreItemsOfType => "Offline bonus: 12% per allocated point (unmodded tooltip: 6%). Requires the node and an imprinted item. Changes apply to newly entered Echoes.",
            WeaverTreeNodeEffect.ImprintContainerChampionsDropMoreItemsOfType => "Offline bonus: 6% per allocated point. Requires the node and an imprinted item.",
            WeaverTreeNodeEffect.ImprintContainerEnemiesDropMoreItemsOfType => "Offline bonus: item-pile trigger chance x2; native enemy/spawner and Corruption scaling still apply. Requires the node and an imprinted item.",
            WeaverTreeNodeEffect.ImprintContainerEchoCompletionsDropMoreItemsOfType => "Offline bonus: Echo-completion item-pile trigger chance x2; displayed maximum 100%. Native reward-score and Corruption scaling still apply; not a guaranteed drop on every Echo. Requires the node and an imprinted item.",
            WeaverTreeNodeEffect.ImprintContainerSpawnMoreWovenEchoesOfType or WeaverTreeNodeEffect.ImprintContainerSpawnMoreWovenEchoesOfTypeSecond =>
                "Offline bonus: imprinted Woven Echo natural spawn weighting x2. Final chance depends on eligible echoes; native unlock and Corruption requirements still apply.",
            _ => ""};
        return note=="" || description?.Contains(note,StringComparison.Ordinal)==true?description:(description??"")+"\n\n"+note;
    }
}

// The active legacy Weaver tree uses NodeTooltipManager, not the V2 builder.
// Prefix the value before native coloring, so HTML/color tags remain native.
[HarmonyPatch(typeof(NodeTooltipManager), nameof(NodeTooltipManager.BuildColoredValue))]
internal static class OfflineWeaverImprintLegacyValuePatch
{
    internal static long Calls;
    private static void Prefix(SkillTreeNode node,ref string valueText)
    {
        if(!OfflineToolkit.Enabled)return;
        var weaver=node?.TryCast<WeaverTreeNode>();
        if(weaver==null || weaver.nodeEffect is not (WeaverTreeNodeEffect.ImprintContainerCachesDropMoreItemsOfType or
            WeaverTreeNodeEffect.ImprintContainerChampionsDropMoreItemsOfType or
            WeaverTreeNodeEffect.ImprintContainerEchoCompletionsDropMoreItemsOfType))return;
        valueText=WeaverImprintRules.DoubleTooltipPercent(valueText);Calls++;
    }
}

[HarmonyPatch(typeof(NodeTooltipManager), nameof(NodeTooltipManager.setInfoForNode))]
internal static class OfflineWeaverImprintLegacyInfoPatch
{
    internal static long Calls;
    private static void Postfix(NodeTooltipManager __instance,SkillTreeNode node)
    {
        if(!OfflineToolkit.Enabled || __instance?.description==null)return;
        var weaver=node?.TryCast<WeaverTreeNode>();if(weaver==null)return;
        var original=__instance.description.text;
        var text=OfflineWeaverImprintTooltipInfoPatch.AppendNote(original,weaver.nodeEffect);
        if(text==original)return;
        __instance.description.text=text;__instance.requireLayoutUpdate=true;Calls++;
    }
}
