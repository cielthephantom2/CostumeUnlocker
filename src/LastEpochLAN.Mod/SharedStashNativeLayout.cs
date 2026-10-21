using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpochLAN.Mod;

// Copy the native layout recipe, remapping every element to the clone. Never
// retain source-tab/controller references or replace native sizing with constants.
internal static class SharedStashNativeLayout
{
    internal static void SizeLabel(TMP_Text label)
    {
        // SimpleLayoutGroup reads child rect widths, not TMP preferred widths.
        // The cloned tab has no controller left to resize its label after a rename.
        // Measure with the native font (including rich text/margins), in local UI
        // units, before the parent lays out the icon, padding and tab background.
        float width=label.GetPreferredValues(label.text).x;
        if(float.IsFinite(width) && width>=0)
            label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,MathF.Ceiling(width));
    }
    private static Transform Map(Transform source,Transform clone,Transform child)
    {
        if(child==null)return null;
        var path=new List<int>();
        for(var node=child;node.Pointer!=source.Pointer;node=node.parent)
        {if(node.parent==null)return null;path.Add(node.GetSiblingIndex());}
        for(int i=path.Count-1;i>=0;i--)clone=clone.GetChild(path[i]);
        return clone;
    }
    internal static SimpleLayoutGroup CopySettings(Transform holder,SimpleLayoutGroup source)
    {
        var layout=holder.GetComponent<SimpleLayoutGroup>()??holder.gameObject.AddComponent<SimpleLayoutGroup>();
        layout._rectTransform=holder.GetComponent<RectTransform>();
        layout._layoutMode=source._layoutMode;layout._padding=source._padding;layout._spacing=source._spacing;
        layout._childAlignment=source._childAlignment;layout._updateMode=source._updateMode;
        layout._adaptHorizontalSize=source._adaptHorizontalSize;layout._adaptVerticalSize=source._adaptVerticalSize;
        layout._minWidth=source._minWidth;layout._minHeight=source._minHeight;
        layout._preferredWidth=source._preferredWidth;layout._preferredHeight=source._preferredHeight;
        layout.Elements=new Il2CppSystem.Collections.Generic.List<SimpleLayoutGroup.SimpleLayoutElement>();
        layout._elementsCache=new Il2CppSystem.Collections.Generic.List<SimpleLayoutGroup.SimpleLayoutElement>();
        layout.ElementsChanged=null;
        return layout;
    }
    internal static void Restore(Transform source,Transform clone)
    {
        var groups=source.GetComponentsInChildren<SimpleLayoutGroup>(true).ToArray();
        // Create all groups before remapping child layout references.
        foreach(var original in groups)CopySettings(Map(source,clone,original.transform),original);
        foreach(var original in groups)
        {
            var layout=Map(source,clone,original.transform).GetComponent<SimpleLayoutGroup>();
            if(original.Elements==null)continue;
            foreach(var element in original.Elements)
            {
                if(element==null)continue;
                var rect=Map(source,clone,element.RectTransformReference)?.GetComponent<RectTransform>();
                if(rect==null)continue;
                var nested=element.LayoutGroupReference==null?null:
                    Map(source,clone,element.LayoutGroupReference.transform)?.GetComponent<SimpleLayoutGroup>();
                var mapped=new SimpleLayoutGroup.SimpleLayoutElement(nested,rect,element.ParentControlsSize,element.ExpandAcrossAxis);
                mapped._excludeIfInactive=element._excludeIfInactive;mapped.Ignored=element.Ignored;
                layout.Elements.Add(mapped);
            }
        }
    }
    internal static void Reflow(GameObject artwork)
    {
        // Children determine preferred sizes before their parent places them.
        foreach(var fitter in artwork.GetComponentsInChildren<ContentSizeFitter>(true).Reverse())
            if(fitter.gameObject.activeInHierarchy){fitter.SetLayoutHorizontal();fitter.SetLayoutVertical();}
        foreach(var layout in artwork.GetComponentsInChildren<SimpleLayoutGroup>(true).Reverse())layout.UpdateLayout();
    }
    internal static void PreserveHitSurface(Graphic surface,Button button,bool createdHit)
    {
        if(!createdHit)return;
        if(button.targetGraphic?.Pointer==surface.Pointer)button.transition=Selectable.Transition.None;
        surface.color=new Color(0,0,0,.001f);
    }
}
