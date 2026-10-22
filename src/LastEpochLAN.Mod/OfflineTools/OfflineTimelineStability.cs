using System.Globalization;
using System.Text.RegularExpressions;
using HarmonyLib;
using Il2Cpp;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class TimelineStabilityRules
{
    internal static int Double(int value)=>value<=0?value:(int)Math.Min(int.MaxValue,(long)value*2);
    internal static int Award(int value,int current)=>value<=0?value:(int)Math.Min(Double(value),Math.Max(0,(long)int.MaxValue-current));
    internal static int Range(int value,int remaining)=>Math.Min(Math.Max(0,remaining),Math.Max(0,Double(value)));
    internal static string RewardText(string text)
    {
        bool changed=false;
        return Regex.Replace(text??"",@"<[^>]*>|\d+(?:[,.]\d{3})*",m=>
        {
            if(changed || m.Value.StartsWith("<"))return m.Value;
            string digits=m.Value.Replace(",","").Replace(".","");
            if(!int.TryParse(digits,NumberStyles.None,CultureInfo.InvariantCulture,out int value))return m.Value;
            changed=true;int result=Double(value);
            if(m.Value.Contains(',') || m.Value.Contains('.'))return result.ToString("N0",CultureInfo.InvariantCulture).Replace(",",m.Value.Contains(',')?",":".");
            return result.ToString(CultureInfo.InvariantCulture);
        });
    }
}

// The native bonus award happens when entering the rest zone. Keystones use
// the same AddStability consumer, with a separate one-use scope. Neither the
// stored save values nor unselected sources (rift awards, costs) are scaled.
internal sealed class StabilityAwardScope:IDisposable
{
    [ThreadStatic] internal static StabilityAwardScope Current;
    private readonly StabilityAwardScope previous;
    private readonly IntPtr runPointer;
    private readonly TimelineID timeline;
    private readonly int difficulty;
    private readonly HashSet<IntPtr> used=new();
    internal int ActualDelta;
    internal bool Applied;
    internal StabilityAwardScope(MonolithRun run)
    {previous=Current;runPointer=run.Pointer;Current=this;}
    internal StabilityAwardScope(EchoCompletionReward reward)
    {previous=Current;timeline=reward.timelineID;difficulty=reward.difficultyIndex;Current=this;}
    internal bool Claim(MonolithRun run)=>run!=null && (runPointer!=IntPtr.Zero?run.Pointer==runPointer:
        run.timelineID==timeline && run.difficultyIndex==difficulty) && used.Add(run.Pointer);
    public void Dispose()=>Current=previous;
}
internal sealed class StabilityAwardEdit
{
    internal StabilityAwardScope Scope;
    internal int Before;
}
[HarmonyPatch(typeof(EchoWebIsland),nameof(EchoWebIsland.GetStabilityRewardAfterFactionModifiers))]
internal static class OfflineEchoStabilityPatch
{
    // Native Forgotten Knights RNG has already applied here, preserving its
    // 25% chance:24 becomes48 normally, or96 on a native double roll.
    private static void Postfix(Actor actor,MonolithRun run,ref int __result)
    {if(MeteorNodeRuntime.Local(actor) && run!=null)__result=TimelineStabilityRules.Award(__result,run.stability);}
}
[HarmonyPatch(typeof(MonolithRestZoneManager),nameof(MonolithRestZoneManager.Initialize))]
internal static class OfflineEchoBonusStabilityScopePatch
{
    private static void Prefix(EchoCompletionReward reward,Actor actor,out StabilityAwardScope __state)
    {__state=MeteorNodeRuntime.Local(actor) && reward!=null?new(reward):null;}
    private static void Finalizer(StabilityAwardScope __state)=>__state?.Dispose();
}
[HarmonyPatch(typeof(MonolithRun),nameof(MonolithRun.TryUseTemporalKeystoneItemAndIncreaseStability))]
internal static class OfflineTemporalKeystoneScopePatch
{
    private static void Prefix(MonolithRun __instance,Actor actor,out StabilityAwardScope __state)
    {__state=MeteorNodeRuntime.Local(actor)?new(__instance):null;}
    private static void Postfix(bool __result,StabilityAwardScope __state,ref int addedStability)
    {if(__result && __state?.Applied==true)addedStability=__state.ActualDelta;}
    private static void Finalizer(StabilityAwardScope __state)=>__state?.Dispose();
}
[HarmonyPatch(typeof(MonolithRun),nameof(MonolithRun.AddStability))]
internal static class OfflineScopedStabilityAwardPatch
{
    private static void Prefix(MonolithRun __instance,ref int stability,out StabilityAwardEdit __state)
    {
        __state=null;var scope=StabilityAwardScope.Current;
        if(!OfflineToolkit.Enabled || scope==null || stability<=0 || !scope.Claim(__instance))return;
        __state=new(){Scope=scope,Before=__instance.stability};stability=TimelineStabilityRules.Award(stability,__instance.stability);
    }
    private static void Postfix(MonolithRun __instance,StabilityAwardEdit __state)
    {
        if(__state==null)return;
        __state.Scope.Applied=true;__state.Scope.ActualDelta=(int)Math.Clamp((long)__instance.stability-__state.Before,0,int.MaxValue);
    }
}
[HarmonyPatch(typeof(MonolithRun),nameof(MonolithRun.GetStabilityRangeFromTemporalKeystone))]
internal static class OfflineTemporalKeystoneRangePatch
{
    private static void Postfix(MonolithRun __instance,ref int min,ref int max)
    {
        if(!OfflineToolkit.Enabled || __instance==null)return;
        int remaining=(int)Math.Clamp((long)__instance.GetMaxStability()-__instance.stability,0,int.MaxValue);
        min=TimelineStabilityRules.Range(min,remaining);max=TimelineStabilityRules.Range(max,remaining);
    }
}
[HarmonyPatch(typeof(EchoTooltip),nameof(EchoTooltip.InitEchoRewards))]
internal static class OfflineEchoStabilityTooltipPatch
{
    private static void Postfix(EchoTooltip __instance)
    {
        if(!OfflineToolkit.Enabled || __instance?._echoRewards==null)return;
        foreach(var row in __instance._echoRewards)
        {
            // Both stability rows use this shipped icon. Other echo rewards
            // retain their original quantities, colors and localized wording.
            if(row?.spriteSoftRef?.SubPath=="Icon_Stability" && row.description!=null)
                row.description.text=TimelineStabilityRules.RewardText(row.description.text);
        }
    }
}
[HarmonyPatch(typeof(StabilityBar),nameof(StabilityBar.SetCurrentStabilityGainMax))]
internal static class OfflineEchoStabilityBarPatch
{
    private static void Prefix(ref int stabilityToGain,ref int maxBonusStability)
    {if(OfflineToolkit.Enabled){stabilityToGain=TimelineStabilityRules.Double(stabilityToGain);maxBonusStability=TimelineStabilityRules.Double(maxBonusStability);}}
}
[HarmonyPatch(typeof(QuestTrackerUI),nameof(QuestTrackerUI.ApplyBonusStability))]
internal static class OfflineEchoBonusStabilityDisplayPatch
{
    private static void Prefix(ref int bonusStability,ref int maxBonusStability)
    {if(OfflineToolkit.Enabled){bonusStability=TimelineStabilityRules.Double(bonusStability);maxBonusStability=TimelineStabilityRules.Double(maxBonusStability);}}
}
