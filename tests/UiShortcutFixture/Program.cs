using System.Text.Json;
using Il2Cpp;
using Il2CppLE.Services;
using LastEpochLAN.Mod;
using LastEpochLAN.Mod.OfflineTools;
using UnityEngine;
var checks=new List<string>();
void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);Console.WriteLine("PASS: "+message);}
var config=UiShortcutRules.Read("{}");Check(config.Count==0,"hotkeys default unbound");
foreach(var key in new[]{KeyCode.F1,KeyCode.F2,KeyCode.F4,KeyCode.Mouse0,KeyCode.Escape,KeyCode.Delete,KeyCode.LeftControl,(KeyCode)999})
 Check(!UiShortcutRules.ValidKey(key),"reserved/invalid key rejected: "+key);
Check(UiShortcutRules.Assign(config,ShortcutAction.Telescope,new(){Key=KeyCode.A,Ctrl=true}),"Ctrl+A can be assigned");
Check(!UiShortcutRules.Assign(config,ShortcutAction.Mastery,new(){Key=KeyCode.A,Ctrl=true}),"duplicate chord rejected");
Check(UiShortcutRules.Assign(config,ShortcutAction.Mastery,new(){Key=KeyCode.A}),"same key with different modifiers allowed");
var loaded=UiShortcutRules.Read(JsonSerializer.Serialize(config));Check(loaded.Count==2 && loaded[ShortcutAction.Telescope].Ctrl,"bindings survive JSON roundtrip");
Check(UiShortcutRules.Read("{\"0\":{\"Key\":97},\"1\":{\"Key\":97},\"99\":{\"Key\":98},\"2\":null,\"4\":{\"Key\":282}}").Count==1,"load filters duplicates, unknown actions, null bindings and reserved keys");
bool invalid=false;try{UiShortcutRules.Read(new string(' ',16385));}catch(InvalidDataException){invalid=true;}Check(invalid,"oversized config rejected");
var runtime=new UiShortcutRuntime{Open=true};
bool Tick(bool panel=true,bool other=false,bool supported=true)
{
 Time.unscaledTime+=1;int before=UIBase.instance.Calls;bool result=runtime.Tick(panel,other,supported);
 if(result && UIBase.instance.Calls>before)
 {
  Check(EpochInputManager.instance.forceDisableInput && runtime.Busy,"async native panel request blocks input and pauses Assist until panel appears");
  UIBase.instance.Panel=true;runtime.Tick(false,false,true);UIBase.instance.Panel=false;
  Check(!EpochInputManager.instance.forceDisableInput && !runtime.Busy,"native panel appearance releases shortcut input ownership");
 }
 return result;
}
void DrawClick(string label){runtime.Open=true;GUI.Click=label;runtime.Draw();}
void Click(string label){DrawClick(label);}
Click("Mesembria Telescope");Check(Tick() && UIBase.instance.Last=="telescope:South" && !runtime.Open,"Telescope requests native South panel and hides shortcut UI");
Click("Respecialize Mastery");Check(Tick() && UIBase.instance.Last=="mastery:True","Mastery opens native respec mode");
Click("Respecialize Mastery Points");Check(Tick() && UIBase.instance.Last=="points:True","points open native passive respec panel");
Click("Personal Stash");Check(Tick() && UIBase.instance.Last=="personal","stash uses personal native panel");
void Guarded(string message,Action set,Action reset)
{
 var before=UIBase.instance.Calls;set();Click("Personal Stash");Check(!Tick() && UIBase.instance.Calls==before,message);reset();
}
Guarded("text input blocks click",()=>EpochInputManager.instance.IsInputFieldActive=true,()=>EpochInputManager.instance.IsInputFieldActive=false);
Guarded("chat blocks click",()=>UIBase.instance.IsChatInputFocused=true,()=>UIBase.instance.IsChatInputFocused=false);
Guarded("loading blocks click",()=>Il2CppLE.UI.LoadingScreen.IsVisible=true,()=>Il2CppLE.UI.LoadingScreen.IsVisible=false);
Guarded("dead actor blocks click",()=>PlayerFinder.Player.health.currentHealth=0,()=>PlayerFinder.Player.health.currentHealth=100);
Guarded("cinematic blocks click",()=>EpochInputManager.instance.cinematicActive=true,()=>EpochInputManager.instance.cinematicActive=false);
Guarded("unfocused game blocks click",()=>Application.isFocused=false,()=>Application.isFocused=true);
Guarded("native transition blocks click",()=>ServiceProvider.OfflineTransitionService.Busy=true,()=>ServiceProvider.OfflineTransitionService.Busy=false);
Guarded("character setup blocks click",()=>NewCharacterBoost.BlocksRoom=true,()=>NewCharacterBoost.BlocksRoom=false);
Guarded("shared stash transaction blocks personal stash",()=>LanSharedChest.Active.Busy=true,()=>LanSharedChest.Active.Busy=false);
Guarded("gift transaction blocks personal stash",()=>LanGift.Active.Busy=true,()=>LanGift.Active.Busy=false);
Guarded("native game panel blocks overlay click",()=>UIBase.instance.Panel=true,()=>UIBase.instance.Panel=false);
runtime.Suspend();EpochInputManager.instance.forceDisableInput=true;Click("Personal Stash");Check(!Tick(),"external input block is respected");runtime.Suspend();Check(EpochInputManager.instance.forceDisableInput,"external input block is restored");EpochInputManager.instance.forceDisableInput=false;
runtime.Open=true;Tick();Check(EpochInputManager.instance.forceDisableInput,"visible shortcut UI blocks gameplay input");Tick(false);Check(!EpochInputManager.instance.forceDisableInput,"F4 close restores gameplay input");
runtime.Open=true;Tick();Input.Down.Add(KeyCode.F1);Tick();Input.Down.Clear();Check(!runtime.Open && !EpochInputManager.instance.forceDisableInput,"F1 releases shortcut input ownership before Assist opens");
DrawClick("Set hotkey");Input.Down.Add(KeyCode.B);Input.Held.Add(KeyCode.LeftControl);Check(!Tick(),"capturing a chord never executes it on the same keypress");Input.Down.Clear();Input.Held.Clear();
runtime.Open=false;Input.Down.Add(KeyCode.B);int calls=UIBase.instance.Calls;Tick(false);Check(UIBase.instance.Calls==calls,"Ctrl+B does not trigger with bare B");
Input.Held.Add(KeyCode.LeftControl);Check(Tick(false) && UIBase.instance.Last=="telescope:South","configured chord triggers native panel in solo mode");Input.Down.Clear();Input.Held.Clear();
DrawClick("Ctrl+B");Input.Down.Add(KeyCode.Escape);Tick();Input.Down.Clear();Check(runtime.Status.Contains("cancelled"),"Escape cancels capture");
DrawClick("Ctrl+B");Input.Down.Add(KeyCode.Delete);Tick();Input.Down.Clear();Check(runtime.Status.Contains("cleared"),"Delete clears binding");
SceneList.Scene.DisplayName="Arena combat";Click("TP to Champions' Gate");Check(!Tick() && ServiceProvider.OfflineTransitionService.Calls==0,"travel refuses mismatched scene metadata");SceneList.Scene.DisplayName="Champions' Gate";
Click("TP to Champions' Gate");Check(Tick() && ServiceProvider.OfflineTransitionService.Calls==1 && ServiceProvider.OfflineTransitionService.Scene=="ArenaLobby" && ServiceProvider.OfflineTransitionService.Gate==0,"travel uses verified ArenaLobby waypoint gate zero");
Click("TP to Champions' Gate");Check(!Tick() && ServiceProvider.OfflineTransitionService.Calls==1,"pending travel prevents duplicate requests");
runtime.Suspend();ServiceProvider.OfflineTransitionService.Task.State.Done=true;Tick(false,false,false);Tick(false,false,false);
Check(ServiceProvider.OfflineTransitionService.Task.State.Reads==1,"native travel result consumed once while UI closed and runtime suspended");
Check(!EpochInputManager.instance.forceDisableInput,"suspension releases input ownership");
runtime.Open=true;Tick();Input.Down.Add(KeyCode.F2);Tick();Input.Down.Clear();
Check(!runtime.Open && !EpochInputManager.instance.forceDisableInput,"F2 releases shortcut input ownership before Shared Stash opens");
var slow=new UiShortcutRuntime{Open=true};GUI.Click="Personal Stash";slow.Draw();Time.unscaledTime+=1;
Check(slow.Tick(true,false,true) && slow.Busy && EpochInputManager.instance.forceDisableInput,"delayed panel keeps input blocked after request");
Time.unscaledTime+=4;slow.Tick(false,false,true);
Check(!slow.Busy && !EpochInputManager.instance.forceDisableInput && slow.Status.Contains("No game panel"),"panel timeout restores input and reports native denial / failure");
var failedTravel=new UiShortcutRuntime{Open=true};ServiceProvider.OfflineTransitionService.Task=new(){State=new(){Done=true,Result=new(){IsSuccess=false}}};
GUI.Click="TP to Champions' Gate";failedTravel.Draw();Time.unscaledTime+=1;failedTravel.Tick(true,false,true);failedTravel.Tick(false,false,true);failedTravel.Tick(false,false,true);
Check(failedTravel.Status.Contains("Travel failed") && ServiceProvider.OfflineTransitionService.Task.State.Reads==1,"native travel error is reported and consumed exactly once");
Console.WriteLine($"All {checks.Count} shortcut checks passed; native engine/rendering not exercised.");
if(args.Length>0)File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=checks.Count,nativeGameplayVerified=false,checks},new JsonSerializerOptions{WriteIndented=true}));
