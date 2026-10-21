using System.Text.Json;
using UnityEngine;
using Il2Cpp;
using Il2CppLE.UI.PanelSystem;
using LastEpochLAN.Mod;

var checks=new List<string>();
void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);Console.WriteLine("PASS: "+text);}
var view=new NativeSharedStashView();var original=new GameObject();var panel=original.AddComponent<StashPanel>();
NativeSharedStashView.Active=view;
var group=original.AddComponent<CanvasGroup>();group.alpha=.6f;group.ignoreParentGroups=true;group.interactable=false;
var canvas=original.AddComponent<Canvas>();
var child=new GameObject();original.children.Add(child);var childGroup=child.AddComponent<CanvasGroup>();childGroup.alpha=.8f;
var childCanvas=child.AddComponent<Canvas>();childCanvas.enabled=false;
view.MaskNativePreparation(panel);
Check(group.alpha==0 && childGroup.alpha==0 && !canvas.enabled && !childCanvas.enabled,"actual lifecycle masks every source CanvasGroup and Canvas before shared source preparation");
Check(!group.blocksRaycasts && !group.interactable && !group.ignoreParentGroups,"hidden source cannot intercept input or ignore its hidden parent group");
view.MaskNativePreparation(panel);
Check(view.Recorded==4,"native base/V2 repeated open hooks preserve one original state per component");
var clone=new GameObject();var cg=clone.AddComponent<CanvasGroup>();var cc=clone.AddComponent<Canvas>();
var cloneChild=new GameObject();clone.children.Add(cloneChild);var chg=cloneChild.AddComponent<CanvasGroup>();var chc=cloneChild.AddComponent<Canvas>();
view.Map(original,clone);view.Map(child,cloneChild);view.Root(clone);view.Restore(true);
Check(cg.alpha==.6f && cg.ignoreParentGroups && !cg.interactable && chg.alpha==.8f && cc.enabled && !chc.enabled,
    "projection clone receives original visual states rather than hidden preparation values");
Check(group.alpha==0 && !canvas.enabled,"restoring clone never reveals the still-open personal source");
view.Restore(false);
Check(group.alpha==.6f && group.ignoreParentGroups && !group.interactable && canvas.enabled && childGroup.alpha==.8f && !childCanvas.enabled,
    "source close restores exact original alpha, input, parent-group and Canvas states");
Check(view.Recorded==0,"source visibility records are released after source restoration");
view.Open(false);view.Hide();
Check(!clone.activeSelf,"F2 close hides the cached projection");
view.Open(true);view.MaskNativePreparation(panel);
Check(group.alpha==.6f && canvas.enabled && view.Recorded==0,"opening personal stash later is unaffected once a shared projection already exists");
view.Root(null);view.StartLoad();view.MaskNativePreparation(panel);view.Open(false);view.Hide();
Check(UIBase.instance.PanelSystem.Closed==1 && !original.activeSelf && group.alpha==.6f && canvas.enabled && view.LoadStarted<0 && view.Recorded==0,
    "canceling F2 during preparation closes hidden source and restores state without starting a game");
view.FailLoad();view.Hide();Check(view.RetryReady,"closing a failed shared view allows a fresh F2 preparation attempt");
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=checks.Count,checks,scope="actual lifecycle source with Unity-state stubs; no rendered/native UI acceptance"},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"All {checks.Count} lifecycle fixture checks passed.");
