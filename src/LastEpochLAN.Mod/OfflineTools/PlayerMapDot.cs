using Il2Cpp;
using Il2CppDMM;
using UnityEngine;
using IconManager=Il2CppLE.UI.Minimap.MinimapIconManager;
namespace LastEpochLAN.Mod.OfflineTools;

// One local marker using the same native renderer and dot art as EnemyMapDots.
// Scale only our child anchor: never change the player's world transform.
internal static class PlayerMapDot
{
    private static ActorVisuals visual;
    private static DMMapIcon original,dot;
    private static GameObject anchor;
    private static IconManager manager;
    private static bool hiddenOriginal;
    internal static void Tick()
    {
        var current=OfflineToolkit.Ready?PlayerFinder.getLocalActorSync()?.actorVisuals:null;
        var native=IconManager.Instance;
        var icon=current?.minimapIcon;
        if(current==null || icon==null || native==null)
        {Clear();return;}
        if(visual==null || visual.Pointer!=current.Pointer || original==null || original.Pointer!=icon.Pointer ||
            manager==null || manager.Pointer!=native.Pointer)
        {Clear();visual=current;original=icon;manager=native;}
        // The request concerns the corner minimap. The Tab overlay retains its
        // original player icon and size. Native OnEnable/OnDisable register icons.
        bool show=UIBase.instance!=null && !UIBase.instance.overlayMapOpen && visual.isActiveAndEnabled &&
            original.gameObject.activeInHierarchy && (hiddenOriginal || original.enabled);
        if(!show)
        {
            if(anchor!=null && anchor.activeSelf)anchor.SetActive(false);
            RestoreOriginal();return;
        }
        if(anchor==null || dot==null)Create();
        EnemyMapDots.ApplySize(anchor,3f);
        if(original.enabled){hiddenOriginal=true;original.enabled=false;}
        if(!anchor.activeSelf)anchor.SetActive(true);
        if(dot.img!=null)dot.img.raycastTarget=false;
    }
    private static void Create()
    {
        if(anchor!=null){anchor.SetActive(false);UnityEngine.Object.Destroy(anchor);}
        anchor=new GameObject("TM Local player minimap dot");anchor.SetActive(false);
        anchor.transform.SetParent(visual.transform,false);
        dot=anchor.AddComponent<DMMapIcon>();
        dot.sprite=dot.icon=EnemyMapDots.PlayerSprite;
        dot.tint=new Color32(255,255,255,255);dot.layer=original.layer;dot.Order=original.Order;
        dot.scaleWithZoom=false;dot.overrideGlobalScaleMode=true;dot.ignoreGlobalScale=true;
        dot.rotate=false;dot.rotateWithMap=false;dot.useDirectionIndicator=false;
        dot.useCompositeDirectionIndicator=false;dot.WorldScale=false;
        dot.ApplyScaling=true;dot.SinScale=false;
    }
    private static void RestoreOriginal()
    {
        if(!hiddenOriginal)return;
        hiddenOriginal=false;
        if(original!=null)original.enabled=true;
    }
    internal static void Clear()
    {
        try
        {
            if(anchor!=null){anchor.SetActive(false);UnityEngine.Object.Destroy(anchor);}
        }
        finally
        {
            anchor=null;dot=null;
            try{RestoreOriginal();}
            finally{visual=null;original=null;manager=null;}
        }
    }
}
