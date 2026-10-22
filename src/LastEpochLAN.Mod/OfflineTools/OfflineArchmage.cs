using System.Globalization;
using System.Text.RegularExpressions;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.UI.Tooltips;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class ArchmageRules
{
    internal const string Tree="mg-1";
    internal const byte Node=30;
    internal static int Tier(int maximumMana)=>maximumMana>=1000?3:maximumMana>=300?2:1;
    internal static float Chance(float nativePerPointChance,int tier)=>float.IsFinite(nativePerPointChance)?Math.Clamp(nativePerPointChance*tier,0,1):0;
    internal const string Description="You have additional spell damage and a chance to refund mana paid when directly using a skill.\n\n"+
        "At 5 allocated points:\n"+
        "Below 300 maximum mana: +5 spell damage; 35% chance to refund 20% of mana cost.\n"+
        "300-999 maximum mana: +10 spell damage; 70% chance to refund 40% of mana cost.\n"+
        "1000+ maximum mana: +15 spell damage; 100% chance to refund 60% of mana cost.\n\n"+
        "Spell damage and refund chance scale with allocated points. Refund chance cannot exceed 100%.";
    internal static string Value(string baseline,string displayed,int tier)
    {
        var b=Number(baseline);var d=Number(displayed);
        if(!Read(b,out var original) || !Read(d,out var value) || b.Groups[4].Value!=d.Groups[4].Value)return displayed;
        bool percent=b.Groups[4].Value=="%";
        if(percent && original==7)value=Math.Min(100,Math.Min(100,value)*tier);
        else if(!percent && original==1)value=Math.Min(value,decimal.MaxValue/tier)*tier;
        else return displayed;
        string number=value.ToString("0.####",CultureInfo.InvariantCulture);
        if(d.Groups[2].Value.Contains(','))number=number.Replace('.',',');
        return d.Groups[1].Value+number+d.Groups[3].Value+d.Groups[4].Value+d.Groups[5].Value;
    }
    private static Match Number(string value)=>Regex.Match(value??"",@"^((?:\s|<[^>]+>)*[+]?)(\d+(?:[.,]\d+)?)(\s*)(%?)(.*)$",RegexOptions.Singleline);
    private static bool Read(Match value,out decimal number)
    {number=0;return value.Success && decimal.TryParse(value.Groups[2].Value.Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out number);}
    internal static string StatName(int index,int tier,string original)=>index switch
    {
        1=>$"Chance To Refund {20*tier}% Of Mana Cost",
        2=>"300+ Max Mana: Double Damage/Chance, Refund 40%",
        3=>"1000+ Max Mana: Triple Damage/Chance, Refund 60% (Chance Cap 100%)",
        _=>original
    };
}

internal sealed class ArchmageCastScope:IDisposable
{
    [ThreadStatic] internal static ArchmageCastScope Current;
    private readonly ArchmageCastScope previous;
    private readonly CharacterMutator mutator;
    private readonly float saved,assigned,effective;
    internal readonly IntPtr Mana;
    internal readonly int CastId,Tier;
    internal readonly float NativeRefund;
    internal bool Used;
    internal ArchmageCastScope(CharacterMutator owner,float manaCost)
    {
        previous=Current;mutator=owner;saved=owner.refund20PercentCostChanceBasedOnMaxMana;
        // Nested casts may observe the enclosing scope's temporary chance cap.
        effective=previous?.mutator.Pointer==owner.Pointer?previous.effective:saved;
        Tier=ArchmageRules.Tier(owner.mana.maxMana);Mana=owner.mana.Pointer;
        CastId=owner.mana.CurrentManaRefundCastID;NativeRefund=manaCost*.2f;
        assigned=ArchmageRules.Chance(effective,Tier)/Tier;
        owner.refund20PercentCostChanceBasedOnMaxMana=assigned;Current=this;
    }
    public void Dispose()
    {
        try
        {
            // A real stats refresh inside a callback owns its newer value.
            if(mutator!=null && mutator.refund20PercentCostChanceBasedOnMaxMana==assigned)
                mutator.refund20PercentCostChanceBasedOnMaxMana=saved;
        }
        finally{Current=previous;}
    }
}

// The native OnAbilityUse contains one RefundManaFromMostRecentCast call, in
// the Archmage branch. Keep its native RNG and timing; scope the correction by
// mana object, refund cast ID, expected native 20% amount and one consumption.
[HarmonyPatch(typeof(CharacterMutator),nameof(CharacterMutator.OnAbilityUse))]
internal static class OfflineArchmageCastPatch
{
    internal static long Casts;
    private static void Prefix(CharacterMutator __instance,float manaCost,out ArchmageCastScope __state)
    {
        __state=null;
        if(!MeteorNodeRuntime.Local(__instance.actor) || __instance.mana==null || !float.IsFinite(manaCost) || manaCost<=0 ||
            !float.IsFinite(__instance.refund20PercentCostChanceBasedOnMaxMana) || __instance.refund20PercentCostChanceBasedOnMaxMana<=0)return;
        __state=new(__instance,manaCost);Casts++;
    }
    private static void Finalizer(ArchmageCastScope __state)=>__state?.Dispose();
}

[HarmonyPatch(typeof(BaseMana),nameof(BaseMana.RefundManaFromMostRecentCast))]
internal static class OfflineArchmageRefundPatch
{
    internal static long Refunds;
    internal static int LastMaximumMana;
    internal static float LastRequestedRefund;
    private static void Prefix(BaseMana __instance,ref float manaToAttemptToRefund)
    {
        var scope=ArchmageCastScope.Current;
        if(!OfflineToolkit.Enabled || scope==null || scope.Used || scope.Mana!=__instance.Pointer || scope.CastId!=__instance.CurrentManaRefundCastID ||
            !float.IsFinite(manaToAttemptToRefund) || Math.Abs(manaToAttemptToRefund-scope.NativeRefund)>Math.Max(.0001f,Math.Abs(scope.NativeRefund)*.00001f))return;
        scope.Used=true;manaToAttemptToRefund*=scope.Tier;
        LastMaximumMana=__instance.maxMana;LastRequestedRefund=manaToAttemptToRefund;Refunds++;
        // Native refund still clamps to AvailableManaToRefund and maximum mana
        // and emits its original mana-refunded event. No gainMana replay.
    }
}

internal static class ArchmageTooltip
{
    internal static bool IsNode(SkillTreeNode node)=>OfflineToolkit.Enabled && node?.tree?.treeID==ArchmageRules.Tree && node.id==ArchmageRules.Node;
    internal static int Tier=>ArchmageRules.Tier(PlayerFinder.getPlayerActor()?.mana?.maxMana??0);
}
[HarmonyPatch(typeof(Il2CppLocalizationSystem.LocalizationSkills),nameof(Il2CppLocalizationSystem.LocalizationSkills.GetNodeDescription))]
internal static class OfflineArchmageDescriptionPatch
{
    private static void Postfix(string treeID,byte nodeID,ref string __result)
    {if(OfflineToolkit.Enabled && treeID==ArchmageRules.Tree && nodeID==ArchmageRules.Node)__result=ArchmageRules.Description;}
}
[HarmonyPatch(typeof(Il2CppLocalizationSystem.LocalizationSkills),nameof(Il2CppLocalizationSystem.LocalizationSkills.GetStatName))]
internal static class OfflineArchmageStatNamePatch
{
    private static void Postfix(string treeID,byte nodeID,int statIndex,ref string __result)
    {if(OfflineToolkit.Enabled && treeID==ArchmageRules.Tree && nodeID==ArchmageRules.Node)__result=ArchmageRules.StatName(statIndex,ArchmageTooltip.Tier,__result);}
}
[HarmonyPatch(typeof(NodeTooltipContentBuilder),nameof(NodeTooltipContentBuilder.GetModifierInfo))]
internal static class OfflineArchmageTooltipValuePatch
{
    private static void Postfix(SkillTreeNode.NodeTooltipStat stat,SkillTreeNode node,NodeTooltipModifierInfo __result)
    {if(ArchmageTooltip.IsNode(node) && stat!=null && __result!=null)__result.Value=ArchmageRules.Value(stat.value,__result.Value,ArchmageTooltip.Tier);}
}
[HarmonyPatch(typeof(NodeTooltipManager),nameof(NodeTooltipManager.BuildColoredValue))]
internal static class OfflineArchmageLegacyValuePatch
{
    private static void Prefix(SkillTreeNode.NodeTooltipStat stat,SkillTreeNode node,ref string valueText)
    {if(ArchmageTooltip.IsNode(node) && stat!=null)valueText=ArchmageRules.Value(stat.value,valueText,ArchmageTooltip.Tier);}
}
