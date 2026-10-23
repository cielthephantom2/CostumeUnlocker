using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Minimap=Il2CppLE.UI.Minimap.Minimap;
namespace LastEpochLAN.Mod.OfflineTools;

// Preserve native exploration. A second terrain-only draw uses a private
// material with a neutral gray fog ramp and the native FoW input; no discovery textures are written.
internal static class MapFog
{
    private static Minimap current;
    private static RawImage source,preview;
    private static Material previewMaterial;
    private static Texture2D fogRamp;
    private static readonly int fogRampProperty=Shader.PropertyToID("_FowRamp");
    private static float nextResolve;
    private static readonly int[] terrainColors={Shader.PropertyToID("_OutlineColor"),Shader.PropertyToID("_FillColor"),Shader.PropertyToID("_FillShadowColor"),Shader.PropertyToID("_OuterNoiseColor")};
    internal static Material PreviewMaterial=>previewMaterial;
    internal static float ClampOpacity(float value)=>float.IsFinite(value)?Math.Clamp(value,0f,0.75f):0.2f;
    internal static void Apply(Minimap map)
    {
        if(map==null || !OfflineToolkit.Enabled) return;
        if(current!=null && current.Pointer!=map.Pointer) ClearPreview();
        current=map;
        if(!OfflineToolkit.Settings.RemoveFog) ClearPreview();
    }
    internal static void Tick()
    {
        if(!OfflineToolkit.Settings.RemoveFog || !OfflineToolkit.Ready) {ClearPreview();return;}
        var image=Il2CppDMM.DMMap.Instance?.mapImage;
        if(image==null || !image.isActiveAndEnabled) {if(preview!=null) preview.enabled=false;return;}
        if(current==null || current.minimapInformation==null)
        {
            if(Time.unscaledTime<nextResolve) return;
            nextResolve=Time.unscaledTime+1;
            current=Resources.FindObjectsOfTypeAll<Minimap>().FirstOrDefault(m=>m!=null && m.minimapInformation!=null && m.isActiveAndEnabled);
            if(current==null) return;
        }
        if(current.minimapInformation.FoWSdf==null || current.minimapInformation.LiveSdf==null) {ClearPreview();return;}
        var material=image.material;
        if(material==null || material.shader==null || material.shader.name!="LastEpoch/Minimap") {ClearPreview();return;}
        if(source==null || source.Pointer!=image.Pointer || preview==null || previewMaterial==null)
        {
            ClearPreview();source=image;
            var go=new GameObject("TM Unexplored map preview");
            go.AddComponent<RectTransform>();
            preview=go.AddComponent<RawImage>();preview.raycastTarget=false;
            previewMaterial=new Material(material);previewMaterial.name="TM Unexplored terrain only";
            preview.material=previewMaterial;
            fogRamp=new Texture2D(64,1,TextureFormat.RGBA32,false,true);
            fogRamp.name="TM Unexplored gray fill ramp";
            fogRamp.wrapMode=TextureWrapMode.Clamp;fogRamp.filterMode=FilterMode.Bilinear;
            for(int x=0;x<64;x++)
            {
                float alpha=Mathf.SmoothStep(0,1,x/63f);
                fogRamp.SetPixel(x,0,new Color(0.12f,0.12f,0.12f,alpha));
            }
            fogRamp.Apply(false,true);
        }
        var from=source.rectTransform;var to=preview.rectTransform;
        if(to.parent!=from.parent) to.SetParent(from.parent,false);
        // Keep the neutral gray terrain below the untouched discovered terrain and icons.
        var targetIndex=from.GetSiblingIndex();
        if(to.GetSiblingIndex()<targetIndex) targetIndex--;
        if(to.GetSiblingIndex()!=targetIndex) to.SetSiblingIndex(targetIndex);
        to.anchorMin=from.anchorMin;to.anchorMax=from.anchorMax;to.pivot=from.pivot;
        to.sizeDelta=from.sizeDelta;to.anchoredPosition3D=from.anchoredPosition3D;
        to.localRotation=from.localRotation;to.localScale=from.localScale;
        preview.texture=source.texture;preview.uvRect=source.uvRect;preview.maskable=source.maskable;
        previewMaterial.CopyPropertiesFromMaterial(material);
        // Draw only the fog fill, not a second copy of the explored terrain.
        // The native shader uses this ramp's alpha for interior coverage; the
        // normal map FillColor alpha is almost zero and cannot supply that fill.
        foreach(var property in terrainColors) previewMaterial.SetColor(property,Color.clear);
        previewMaterial.SetTexture(fogRampProperty,fogRamp);
        previewMaterial.SetTexture(Minimap.shaderMinimapSdf,current.minimapInformation.LiveSdf);
        previewMaterial.SetTexture(Minimap.shaderFowSdf,current.minimapInformation.FoWSdf);
        previewMaterial.SetFloat(Minimap.shaderOpacityMultiplier,ClampOpacity(OfflineToolkit.Settings.UnexploredOpacity)*Mathf.Clamp01(current.opacity));
        preview.enabled=source.enabled;
    }
    private static void ClearPreview()
    {
        if(preview!=null) {preview.enabled=false;UnityEngine.Object.Destroy(preview.gameObject);}
        if(previewMaterial!=null) UnityEngine.Object.Destroy(previewMaterial);
        if(fogRamp!=null) UnityEngine.Object.Destroy(fogRamp);
        source=null;preview=null;previewMaterial=null;fogRamp=null;
    }
    internal static void Clear() {ClearPreview();current=null;nextResolve=0;}
}
[HarmonyPatch]
internal static class MapFogLifecycle
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {foreach(var name in new[]{"Awake","OnFoWDataReady","OnMapLoaded"}) yield return AccessTools.Method(typeof(Minimap),name);}
    private static void Postfix(Minimap __instance)=>MapFog.Apply(__instance);
}
