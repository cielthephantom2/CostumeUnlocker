using HarmonyLib;
using Il2Cpp;
namespace LastEpochLAN.Mod.OfflineTools;

// The native +/- buttons still handle the step, zoom preference and map update.
// Extend only their finite clamp range; retain the prefab's values for restore.
internal static class MinimapZoom
{
    private sealed record State(DMMapZoom Controller,float Min,float Max);
    private static readonly Dictionary<int,State> tracked=new();
    internal static void Apply(DMMapZoom controller)
    {
        if(controller==null || !OfflineToolkit.Enabled) return;
        int id=controller.GetInstanceID();
        if(!tracked.TryGetValue(id,out var state) || state.Controller==null || state.Controller.Pointer!=controller.Pointer)
        {state=new(controller,controller.minMinimapZoom,controller.maxMinimapZoom);tracked[id]=state;}
        controller.minMinimapZoom=OfflineToolkit.Settings.WideMinimapZoom?Math.Min(state.Min,2.5f):state.Min;
        controller.maxMinimapZoom=OfflineToolkit.Settings.WideMinimapZoom?Math.Max(state.Max,150f):state.Max;
    }
    internal static void ApplyCurrent()
    {
        foreach(var state in tracked.Values.ToArray())
            if(state.Controller!=null) Apply(state.Controller);
    }
    internal static void Clear()
    {
        foreach(var state in tracked.Values)
            if(state.Controller!=null) {state.Controller.minMinimapZoom=state.Min;state.Controller.maxMinimapZoom=state.Max;}
        tracked.Clear();
    }
}
[HarmonyPatch]
internal static class ExtendNativeMinimapZoom
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {foreach(var name in new[]{"Start","ZoomInMinimap","ZoomOutMinimap"}) yield return AccessTools.Method(typeof(DMMapZoom),name);}
    private static void Prefix(DMMapZoom __instance)=>MinimapZoom.Apply(__instance);
}
