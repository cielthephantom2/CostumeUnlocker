using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class ObjectiveReveal
{
    private static readonly Dictionary<int,(DelayedZoneObjectivePulse Pulse,float ReadyAt)> pulses=new();
    internal static void Track(DelayedZoneObjectivePulse pulse)=>pulses[pulse.GetInstanceID()]=(pulse,Time.unscaledTime+0.5f);
    internal static void Tick()
    {
        using var performance = RuntimePerformance.Measure(PerformanceArea.Objectives);
        if(!OfflineToolkit.Ready || !OfflineToolkit.Settings.RevealObjectives) return;
        foreach(var pair in pulses.ToArray())
        {
            var pulse=pair.Value.Pulse;
            if(pulse==null || pulse.activated) {pulses.Remove(pair.Key);continue;}
            if(Time.unscaledTime<pair.Value.ReadyAt || pulse.dungeonZoneManager==null) continue;
            pulses.Remove(pair.Key); // Claim before the callback can re-enter native setup.
            pulse.activate();
        }
    }
    internal static void Clear()=>pulses.Clear();
}
[HarmonyPatch(typeof(DelayedZoneObjectivePulse),"Start")]
internal static class RevealDungeonObjectivesPatch
{private static void Postfix(DelayedZoneObjectivePulse __instance) {if(OfflineToolkit.Enabled) ObjectiveReveal.Track(__instance);}}
[HarmonyPatch(typeof(MonolithZoneManager),"initialise")]
internal static class RevealMonolithObjectivesPatch
{
    private static void Postfix(MonolithZoneManager __instance)
    {if(OfflineToolkit.Enabled && OfflineToolkit.Settings.RevealObjectives) __instance.objectiveRevealThresholdModifier=float.MaxValue;}
}
