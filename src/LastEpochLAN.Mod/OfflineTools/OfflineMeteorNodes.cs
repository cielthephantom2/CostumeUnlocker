using System.Globalization;
using System.Text.RegularExpressions;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.UI.Tooltips;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class MeteorNodeRules
{
    internal const string Extinction="Meteor Crit Chance",HeatFlux="Meteor Frost Claw on Impact";
    internal static int FrostClaws(int native)=>native<=0?0:(int)Math.Min(int.MaxValue,(long)native*2);
    internal static float HeatFluxMana(float native)=>float.IsFinite(native) && native>0?native/1.2f:native;
    // Native ScaleValueText appends localized "per point" text (and may wrap
    // the number in TMP tags). Preserve that suffix and formatting verbatim.
    private static Match Number(string value)=>Regex.Match(value??"",@"^((?:\s|<[^>]+>)*[+]?)(\d+(?:[.,]\d+)?)(\s*)(%?)(.*)$",RegexOptions.Singleline);
    private static bool Read(Match match,out decimal number)
    {
        number=0;return match.Success && decimal.TryParse(match.Groups[2].Value.Replace(',','.'),
            NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out number);
    }
    internal static string Tooltip(string node,string original,string displayed)
    {
        var baseline=Number(original);var value=Number(displayed);
        if(!Read(baseline,out var initial) || !Read(value,out var current) || baseline.Groups[4].Value!=value.Groups[4].Value)return displayed;
        bool percent=baseline.Groups[4].Value=="%";
        decimal result;
        if(node==Extinction && percent && initial==10)result=Math.Min(100m,Math.Min(current,100m)*3.3m);
        else if(node==HeatFlux && !percent && initial==1)result=Math.Min(current,decimal.MaxValue/2)*2;
        else if(node==HeatFlux && percent && initial==120)result=100;
        else return displayed;
        string number=result.ToString("0.####",CultureInfo.InvariantCulture);
        if(value.Groups[2].Value.Contains(','))number=number.Replace('.',',');
        return value.Groups[1].Value+number+value.Groups[3].Value+value.Groups[4].Value+value.Groups[5].Value;
    }
}

internal static class MeteorNodeRuntime
{
    internal static long TreeUpdates,ManaAdjustments;
    internal static float LastLargeMeteorChance;
    internal static int LastFrostClawCount;
    internal static long CastAttempts,CastUpdates,MissingTrees;
    internal static bool Apply(MeteorMutator mutator,LocalTreeData.TreeData tree)
    {
        if(mutator==null || tree?.nodes==null)return false;
        var global=GlobalTreeData.get()?.getTreeData(tree.treeID);if(global==null)return false;
        int extinction=0,heat=0;
        foreach(var node in tree.nodes)
        {
            if(node==null)continue;
            var name=global.getNode(node.id)?.name;
            if(name==MeteorNodeRules.Extinction)extinction+=node.pointsAllocated;
            else if(name==MeteorNodeRules.HeatFlux)heat+=node.pointsAllocated;
        }
        // Derive absolute values from allocation; a tree refresh and repeated
        // casts must never multiply an already modified mutator a second time.
        mutator.chanceFor100increasedArea=Math.Min(1,.33f*extinction);
        mutator.frostClawCastsOnImpact=MeteorNodeRules.FrostClaws(heat);
        LastLargeMeteorChance=mutator.chanceFor100increasedArea;
        LastFrostClawCount=mutator.frostClawCastsOnImpact;
        return true;
    }
    internal static bool Local(Actor actor)=>OfflineToolkit.Enabled && actor!=null &&
        actor.Pointer==PlayerFinder.getPlayerActor()?.Pointer;
    internal static string Node(SkillTreeNode node)
    {
        if(!OfflineToolkit.Enabled || node?.tree?.TryCast<MeteorSkillTree>()==null)return "";
        // Use the same internal node key as native updateMutator, independent
        // of localized display names and unrelated crit/mana stats on the node.
        var name=GlobalTreeData.get()?.getTreeData(node.tree.treeID)?.getNode(node.id)?.name;
        return name is MeteorNodeRules.Extinction or MeteorNodeRules.HeatFlux?name:"";
    }
    internal static string Description(string original,string node)
    {
        if(node==MeteorNodeRules.HeatFlux)original=Regex.Replace(original??"",@"(?<![\d.,])120\s*%","100%");
        return original;
    }
}

[HarmonyPatch(typeof(MeteorSkillTree),nameof(MeteorSkillTree.updateMutator))]
internal static class OfflineMeteorTreePatch
{
    private static void Postfix(LocalTreeData localTreeData,LocalTreeData.TreeData tree)
    {
        if(localTreeData==null || !MeteorNodeRuntime.Local(localTreeData.actor))return;
        var mutator=localTreeData.GetComponent<MeteorMutator>();if(mutator==null)return;
        if(MeteorNodeRuntime.Apply(mutator,tree))MeteorNodeRuntime.TreeUpdates++;
    }
}

[HarmonyPatch(typeof(MeteorMutator),nameof(MeteorMutator.Mutate))]
internal static class OfflineMeteorCastPatch
{
    private static void Prefix(MeteorMutator __instance)
    {
        if(!MeteorNodeRuntime.Local(__instance.actor))return;
        MeteorNodeRuntime.CastAttempts++;
        var tree=__instance.actor.localTreeData?.getSkillTreeData(Ability.getAbility(AbilityID.meteor));
        if(MeteorNodeRuntime.Apply(__instance,tree))MeteorNodeRuntime.CastUpdates++;
        else MeteorNodeRuntime.MissingTrees++;
    }
}

internal sealed class MeteorManaScope:IDisposable
{
    [ThreadStatic] internal static MeteorManaScope Current;
    private readonly MeteorManaScope previous;
    internal readonly IntPtr Mana;
    internal MeteorManaScope(IntPtr mana){previous=Current;Mana=mana;Current=this;}
    public void Dispose()=>Current=previous;
}

[HarmonyPatch(typeof(MeteorAoEMutator),nameof(MeteorAoEMutator.Mutate))]
internal static class OfflineMeteorImpactScopePatch
{
    private static void Prefix(MeteorAoEMutator __instance,out MeteorManaScope __state)
    {
        __state=__instance.frostClawCastsOnImpact>0 && MeteorNodeRuntime.Local(__instance.actor) && __instance.actor.mana!=null?
            new MeteorManaScope(__instance.actor.mana.Pointer):null;
    }
    private static void Finalizer(MeteorManaScope __state)=>__state?.Dispose();
}

// The native impact consumer multiplies the ID-overload cost by an inlined1.2
// for both its affordability check and its eventual reduceMana call. Cancel
// that factor only for this actor's Heat Flux query; do not refund mana later
// or change Frost Claw's manual casts, shared assets or unrelated mana calls.
[HarmonyPatch(typeof(BaseMana),nameof(BaseMana.getManaCost),new[]{typeof(AbilityID),typeof(float),typeof(bool),typeof(AbilityMutator),typeof(bool)})]
internal static class OfflineMeteorManaCostPatch
{
    private static void Postfix(BaseMana __instance,AbilityID abilityID,ref float __result)
    {
        if(!OfflineToolkit.Enabled || abilityID!=AbilityID.frostClaw || MeteorManaScope.Current?.Mana!=__instance.Pointer)return;
        __result=MeteorNodeRules.HeatFluxMana(__result);MeteorNodeRuntime.ManaAdjustments++;
    }
}

[HarmonyPatch(typeof(NodeTooltipContentBuilder),nameof(NodeTooltipContentBuilder.GetModifierInfo))]
internal static class OfflineMeteorTooltipValuePatch
{
    private static void Postfix(SkillTreeNode.NodeTooltipStat stat,SkillTreeNode node,NodeTooltipModifierInfo __result)
    {if(stat!=null && __result!=null)__result.Value=MeteorNodeRules.Tooltip(MeteorNodeRuntime.Node(node),stat.value,__result.Value);}
}
[HarmonyPatch(typeof(NodeTooltipManager),nameof(NodeTooltipManager.BuildColoredValue))]
internal static class OfflineMeteorLegacyValuePatch
{
    private static void Prefix(SkillTreeNode.NodeTooltipStat stat,SkillTreeNode node,ref string valueText)
    {if(stat!=null)valueText=MeteorNodeRules.Tooltip(MeteorNodeRuntime.Node(node),stat.value,valueText);}
}
[HarmonyPatch(typeof(NodeTooltipContentBuilder),nameof(NodeTooltipContentBuilder.GetNodeTooltipInfo))]
internal static class OfflineMeteorTooltipInfoPatch
{
    private static void Postfix(SkillTreeNode node,NodeTooltipInfo __result)
    {if(__result!=null)__result.Description=MeteorNodeRuntime.Description(__result.Description,MeteorNodeRuntime.Node(node));}
}
[HarmonyPatch(typeof(NodeTooltipManager),nameof(NodeTooltipManager.setInfoForNode))]
internal static class OfflineMeteorLegacyInfoPatch
{
    private static void Postfix(NodeTooltipManager __instance,SkillTreeNode node)
    {
        if(__instance?.description==null)return;
        var original=__instance.description.text;var text=MeteorNodeRuntime.Description(original,MeteorNodeRuntime.Node(node));
        if(text!=original){__instance.description.text=text;__instance.requireLayoutUpdate=true;}
    }
}
