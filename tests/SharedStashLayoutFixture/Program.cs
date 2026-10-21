using Il2Cpp;
using LastEpochLAN.Mod;
using UnityEngine;
using UnityEngine.UI;
using System.Text.Json;
int count=0;
void Check(bool good,string label){if(!good)throw new Exception(label);count++;Console.WriteLine("PASS: "+label);}
var source=new GameObject("source");var icon=new GameObject("icon");var label=new GameObject("label");source.Child(icon);source.Child(label);
var group=source.AddComponent<SimpleLayoutGroup>();var nested=label.AddComponent<SimpleLayoutGroup>();
group._spacing=7;group._minWidth=20;group._updateMode=3;group._adaptHorizontalSize=true;group.ElementsChanged=new();
group.Elements.Add(new(null,icon.transform,false,false){_excludeIfInactive=true});
group.Elements.Add(new(nested,label.transform,true,true){Ignored=false});
group._elementsCache.Add(group.Elements[0]);
var clone=new GameObject("clone");var cloneIcon=new GameObject("clone-icon");var cloneLabel=new GameObject("clone-label");clone.Child(cloneIcon);clone.Child(cloneLabel);
SharedStashNativeLayout.Restore(source.transform,clone.transform);
var copied=clone.GetComponent<SimpleLayoutGroup>();var childLayout=cloneLabel.GetComponent<SimpleLayoutGroup>();
Check(copied._spacing==7 && copied._minWidth==20 && copied._updateMode==3 && copied._adaptHorizontalSize,"native spacing/minimum/adaptive/update settings survive cloning");
Check(copied.Elements[0].RectTransformReference.Pointer==cloneIcon.transform.Pointer && copied.Elements[1].RectTransformReference.Pointer==cloneLabel.transform.Pointer,"layout element rects point to clone artwork, never source UI");
Check(copied.Elements[1].LayoutGroupReference.Pointer==childLayout.Pointer && copied.Elements[1].ParentControlsSize && copied.Elements[1].ExpandAcrossAxis,"nested group references and parent sizing flags are preserved");
Check(copied.Elements[0]._excludeIfInactive && copied._elementsCache.Count==0 && copied.ElementsChanged==null,"inactive icon policy is retained; stale cache/delegates are removed");
Check(group.Elements.Count==2 && group._elementsCache.Count==1 && group.ElementsChanged!=null,"restoring clone never alters personal stash layout or callbacks");
SharedStashNativeLayout.Restore(source.transform,clone.transform);
Check(clone.Components.OfType<SimpleLayoutGroup>().Count()==1 && copied.Elements.Count==2,"restoring/reflowing does not accumulate duplicate layout components or elements");
clone.AddComponent<ContentSizeFitter>();cloneLabel.AddComponent<ContentSizeFitter>();
SharedStashNativeLayout.Reflow(clone);
Check(SimpleLayoutGroup.Calls.SequenceEqual(new[]{"clone-label","clone"}) && ContentSizeFitter.Calls[0]=="clone-label:H","preferred child sizes are refreshed before parent placement");
var surface=clone.AddComponent<Graphic>();var button=clone.AddComponent<Button>();button.targetGraphic=surface;
SharedStashNativeLayout.PreserveHitSurface(surface,button,true);
Check(surface.color.a==.001f && button.transition==Selectable.Transition.None,"new hit Image cannot be tinted into an opaque white/colored rectangle");
surface.color=new(1,0,0,.7f);button.transition=Selectable.Transition.ColorTint;
SharedStashNativeLayout.PreserveHitSurface(surface,button,false);
Check(surface.color.a==.7f && button.transition==Selectable.Transition.ColorTint,"original artwork color and button transitions remain native");
var originalTarget=cloneLabel.AddComponent<Graphic>();button.targetGraphic=originalTarget;
SharedStashNativeLayout.PreserveHitSurface(surface,button,true);
Check(button.transition==Selectable.Transition.ColorTint && surface.color.a==.001f,"existing native target retains hover tint while separate hit surface stays invisible");
var text=cloneLabel.AddComponent<Il2CppTMPro.TMP_Text>();
foreach(var name in new[]{"Unique","Set","Exalted","Shared 1","A long renamed stash tab","i"})
{
    text.text=name;SharedStashNativeLayout.SizeLabel(text);
    Check(text.rectTransform.width==MathF.Ceiling(text.GetPreferredValues(name).x) && text.rectTransform.height==40 && text.text==name,
        "label width follows native preferred measurement after rename: "+name);
}
text.fontScale=1.6f;SharedStashNativeLayout.SizeLabel(text);
Check(text.rectTransform.width==MathF.Ceiling(text.GetPreferredValues(text.text).x),"label measures current font metrics instead of a fixed pixels-per-character estimate");
Check(label.transform.width==25,"shared label resizing leaves the source personal label untouched");
Console.WriteLine($"All {count} actual-source layout stub checks passed; Unity rendering not exercised.");
if(args.Length>0)File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=count,nativeGameplayVerified=false}));
