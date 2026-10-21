using HarmonyLib;
using Il2Cpp;
using Il2CppLE.UI.PanelSystem;
using UnityEngine;
using System.Reflection;

namespace LastEpochLAN.Mod;

internal sealed partial class NativeSharedStashView
{
    private readonly List<(CanvasGroup Group,float Alpha,bool Blocks,bool Interactable,bool Ignore)> sourceVisibility=new();
    private readonly List<(Canvas Canvas,bool Enabled)> sourceCanvases=new();
    private StashPanel stagingSource;
    internal bool PreparingNative=>owner.Open && root==null;
    internal void MaskNativePreparation(StashPanel panel)
    {
        if(!PreparingNative || panel==null)return;
        stagingSource=panel;
        var group=panel.GetComponent<CanvasGroup>()??panel.gameObject.AddComponent<CanvasGroup>();
        foreach(var candidate in panel.GetComponentsInChildren<CanvasGroup>(true))
        {
            if(sourceVisibility.All(s=>s.Group.Pointer!=candidate.Pointer))
                sourceVisibility.Add((candidate,candidate.alpha,candidate.blocksRaycasts,candidate.interactable,candidate.ignoreParentGroups));
            candidate.alpha=0;candidate.blocksRaycasts=false;candidate.interactable=false;candidate.ignoreParentGroups=false;
        }
        // A native fade can animate CanvasGroup.alpha after OnOpen. Disabling
        // source canvases also prevents that transition from flashing on screen.
        foreach(var canvas in panel.GetComponentsInChildren<Canvas>(true))
        {
            if(sourceCanvases.All(s=>s.Canvas.Pointer!=canvas.Pointer))sourceCanvases.Add((canvas,canvas.enabled));
            canvas.enabled=false;
        }
    }
    private void RestoreStagingVisibility(bool clone)
    {
        foreach(var state in sourceVisibility)
        {
            var group=clone?Cloned(stagingSource.transform,state.Group.transform)?.GetComponent<CanvasGroup>():state.Group;
            if(group==null)continue;
            group.alpha=state.Alpha;group.blocksRaycasts=state.Blocks;group.interactable=state.Interactable;group.ignoreParentGroups=state.Ignore;
        }
        foreach(var state in sourceCanvases)
        {
            var canvas=clone?Cloned(stagingSource.transform,state.Canvas.transform)?.GetComponent<Canvas>():state.Canvas;
            if(canvas!=null)canvas.enabled=state.Enabled;
        }
        if(!clone){sourceVisibility.Clear();sourceCanvases.Clear();stagingSource=null;}
    }
    internal void Hide()
    {
        if(root!=null)root.SetActive(false);
        if(root==null && stagingSource!=null)
        {
            // Cancel an outstanding invisible preparation on F2 close.
            var panel=stagingSource;
            try{if(panel.gameObject.activeInHierarchy)UIBase.instance?.PanelSystem?.ClosePanel(panel);}
            finally{RestoreStagingVisibility(false);loadStarted=-1;}
        }
        if(root==null){loadFailed=false;loadStarted=-1;retry=0;}
    }
}

// Runs before an opened personal panel can be rendered. The panel is only a
// hidden source for native artwork; the shared projection never binds its data.
[HarmonyPatch]
internal static class SharedStashPreparationVisibilityPatch
{
    private static IEnumerable<MethodBase> TargetMethods()=>new[]{typeof(StashPanel),typeof(StashPanelV2)}
        .SelectMany(t=>t.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)
            .Where(m=>m.Name is "OnOpen" or "OnOpenWithArgs"));
    private static void Prefix(StashPanel __instance)=>NativeSharedStashView.Active?.MaskNativePreparation(__instance);
    private static void Postfix(StashPanel __instance)=>NativeSharedStashView.Active?.MaskNativePreparation(__instance);
}
