using System.Reflection;
using System.Text.Json;
using Il2Cpp;
using Il2CppLE.Data;
using LastEpochLAN.Mod.OfflineTools;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppPixelCrushers.DialogueSystem;
using Il2CppPixelCrushers.DialogueSystem.TextMeshPro;
var checks=new List<string>();void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
for(int i=0;i<5;i++)CharacterClassList.All[i]=new(){classID=(CharacterClassID)i,className=((CharacterClassID)i).ToString(),treeID="tree-"+i,defaultAbilities=new(){new(){playerAbilityID="default-"+i}}};
for(int i=0;i<5;i++){
 var d=new CharacterData{CharacterClass=(i+1)%5};var inventory=d.SavedItems;var quests=d.SavedQuests;var monolith=d.MonolithRuns;var weave=d.SavedWeaverTree;var factions=d.Factions;var old=ClassRespecializationData.Capture(d);
 ClassRespecializationData.Apply(d,CharacterClassList.getClass(i),5);
 Check(d.CharacterClass==i && d.ChosenMastery==0 && d.OriginalMastery==0,"class/mastery reset for "+(CharacterClassID)i);
 Check(d.Level==90 && d.Gold==98765 && d.Location=="EoT" && d.SavedItems==inventory && d.SavedQuests==quests && d.MonolithRuns==monolith && d.SavedWeaverTree==weave && d.Factions==factions,"level inventory gold quests Monolith Weaver factions identity remain intact for "+i);
 Check(d.SavedCharacterTree.NodeIDs.Count==0 && d.SavedCharacterTree.UnspentPoints==15 && d.SavedSkillTrees.Count==0 && d.AbilityBar.Count==5 && d.AbilityBar[0]=="default-"+i && d.CharacterTreeNodeProgression.Count==0,"passive refund/default bars/skill reset for "+i);
 ClassRespecializationData.Restore(d,old);Check(d.CharacterClass==(i+1)%5 && ReferenceEquals(d.SavedCharacterTree,old.Passive) && d.SavedSkillTrees.Count==1,"rollback restores original objects for "+i);
}
var same=new CharacterData();var sameTree=same.SavedCharacterTree;ClassRespecializationData.Apply(same,CharacterClassList.getClass(1),5);Check(ReferenceEquals(sameTree,same.SavedCharacterTree),"same class is a no-op");
try{ClassRespecializationData.Apply(same,new(){classID=(CharacterClassID)99},0);throw new Exception("accepted invalid");}catch(ArgumentException){Check(same.CharacterClass==1,"invalid class rejected");}
var actor=PlayerFinder.Actor=new();PlayerFinder.Sync=new();actor.characterDataTracker.charData=new();actor.StatsMutator.meteorConvertedToLightning=true;
var stats=new CharacterStats{actor=actor};object[] scope={stats,IntPtr.Zero};typeof(OfflineBaseRegenerationScopePatch).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,scope);
float Regen(SP property,float value,BaseStats.ModType kind){object[] a={stats,property,value,kind};typeof(OfflineBaseRegenerationValuePatch).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,a);return(float)a[2];}
Check(Regen(SP.ManaRegen,8,BaseStats.ModType.ADDED)==16 && Regen(SP.HealthRegen,8,BaseStats.ModType.ADDED)==16,"base8 becomes16 for both regeneration stats");Check(Regen(SP.ManaRegen,.5f,BaseStats.ModType.INCREASED)==.5f && Regen(SP.Movespeed,8,BaseStats.ModType.ADDED)==8,"increased and unrelated base stats remain native");
var error=new Exception("native");var propagated=typeof(OfflineBaseRegenerationScopePatch).GetMethod("Finalizer",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{error,scope[1]});Check(ReferenceEquals(error,propagated) && BaseRegeneration.Initializing==IntPtr.Zero && Regen(SP.ManaRegen,8,BaseStats.ModType.ADDED)==8,"scope restores on native failure; gear/passive flat regen unchanged");
var ui=new TextMeshProDialogueUI();var sub=new Subtitle{speakerInfo=new(){Name="Chronomancer Lerinne",nameInDatabase="Chronomancer Lerinne"}};
Il2CppReferenceArray<Response> root=new Response[]{new(FormattedText.Parse("Respecialize Mastery",null),new(){id=1},true)};
ClassRespecialization.Responses(ui,sub,ref root);Check(root.Length==2,"native NPC menu gets one class option alongside original mastery");ClassRespecialization.Click(new(){response=root[1]});Check(ui.Last.Count==6,"all five class choices plus Back");var rogue=ui.Last.Single(r=>r.formattedText.text=="Rogue");ClassRespecialization.Click(new(){response=rogue});Check(ui.Last[0].formattedText.text.Contains("Keep level, items and progress"),"class confirmation describes preserved and reset data");
var d2=actor.characterDataTracker.charData;ClassRespecialization.Click(new(){response=ui.Last[0]});Check(ClassRespecialization.Busy && UIBase.Exits==1 && d2.CharacterClass==1 && d2.SaveCalls==0,"confirmation exits first without touching live class data");
ClassRespecialization.Tick();Check(d2.CharacterClass==1 && d2.SaveCalls==0,"in-game transition cannot mutate or save class");
PlayerFinder.Actor=null;Il2CppClientAppState.ClientStateManager.CurrentClientAppStateType=Il2CppClientAppState.ClientAppStateType.CharacterSelect;
var dir=Path.Combine(Path.GetTempPath(),"class-respec-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(dir,"Saves"));File.WriteAllText(Path.Combine(dir,"Saves","slot.bin"),"byte-exact-native-save");UnityEngine.Application.persistentDataPath=dir;LocalCharacterSlots.instance.characterSlots.Add(d2);CharacterData.NextSave=new(){Done=false};
ClassRespecialization.Tick();Check(d2.CharacterClass==4 && d2.SaveCalls==1 && ClassRespecialization.Busy,"menu-side migration starts native save and remains busy until completion");ClassRespecialization.Tick();Check(ClassRespecialization.Busy,"unfinished native save is awaited");CharacterData.NextSave.Done=true;ClassRespecialization.Tick();Check(!ClassRespecialization.Busy && d2.Level==90 && d2.CharacterClass==4,"completed native save releases load gate with new class intact");
Check(Directory.GetFiles(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","LastEpochLAN","backups"),"slot.bin",SearchOption.AllDirectories).Any(f=>File.ReadAllText(f)=="byte-exact-native-save"),"raw native save backed up before class mutation");
// Runtime failure and repeat behavior; all use the actual transition state machine.
void Request(CharacterData value,int id){
 PlayerFinder.Actor=new();PlayerFinder.Actor.characterDataTracker.charData=value;Il2CppClientAppState.ClientStateManager.CurrentClientAppStateType=Il2CppClientAppState.ClientAppStateType.InGame;
 Il2CppReferenceArray<Response> roots=new Response[]{new(FormattedText.Parse("Respecialize Mastery",null),new(){id=1},true)};
 ClassRespecialization.Responses(ui,sub,ref roots);ClassRespecialization.Click(new(){response=roots.Last()});ClassRespecialization.Click(new(){response=ui.Last.Single(r=>r.formattedText.text==((CharacterClassID)id).ToString())});ClassRespecialization.Click(new(){response=ui.Last[0]});
 PlayerFinder.Actor=null;LocalCharacterSlots.instance.characterSlots.Clear();LocalCharacterSlots.instance.characterSlots.Add(value);Il2CppClientAppState.ClientStateManager.CurrentClientAppStateType=Il2CppClientAppState.ClientAppStateType.CharacterSelect;
}
var failed=new CharacterData();var before=ClassRespecializationData.Capture(failed);CharacterData.NextSave=new(){Done=false,Error=new IOException("native save failed")};Request(failed,0);ClassRespecialization.Tick();Check(failed.CharacterClass==0 && ClassRespecialization.Busy,"second class request works after completed migration");CharacterData.NextSave.Done=true;ClassRespecialization.Tick();Check(!ClassRespecialization.Busy && failed.CharacterClass==1 && ReferenceEquals(failed.SavedCharacterTree,before.Passive) && ReferenceEquals(failed.AbilityBar,before.Bar),"native save failure restores original class and original passive/bar objects");
var delayed=new CharacterData();CharacterData.NextSave=new();Request(delayed,2);LocalCharacterSlots.instance.CharacterSlotsLoaded=false;ClassRespecialization.Tick();Check(delayed.CharacterClass==1 && delayed.SaveCalls==0,"wait for native offline character list before migration");LocalCharacterSlots.instance.CharacterSlotsLoaded=true;GlobalTreeData.Missing=true;ClassRespecialization.Tick();Check(delayed.CharacterClass==1 && !ClassRespecialization.Busy,"unavailable target tree aborts before mutation");GlobalTreeData.Missing=false;
Il2CppReferenceArray<Response> repeated=new Response[]{new(FormattedText.Parse("Respecialize Mastery",null),new(){id=1},true)};ClassRespecialization.Responses(ui,sub,ref repeated);ClassRespecialization.Responses(ui,sub,ref repeated);Check(repeated.Length==2,"repeated NPC menu refresh never duplicates injected class option");
Il2CppReferenceArray<Response> other=new Response[]{new(FormattedText.Parse("Respecialize Mastery",null),new(){id=1},true)};ClassRespecialization.Responses(ui,new(){speakerInfo=new(){Name="Other NPC"}},ref other);Check(other.Length==1,"other NPC conversations untouched");
Il2CppReferenceArray<Response> Roots()=>new Response[]{new(FormattedText.Parse("Respecialize Mastery Point allocations.",null),new(){id=1},true),new(FormattedText.Parse("Respecialize Mastery",null),new(){id=2},true)};
var listenerSub=new Subtitle{speakerInfo=new(){Name="Player"},listenerInfo=new(){Name="Chronomancer Lerinne"}};
var lr=Roots();ClassRespecialization.Responses(ui,listenerSub,ref lr);Check(lr.Length==3,"player-speaker / Lerinne-listener root gets class option");
Check(lr.Last().formattedText.position==-1,"injected native option uses the automatic button position");
var aliased=new Subtitle{speakerInfo=new(){Name="Player",nameInDatabase="Player"},listenerInfo=new(){Name="Respec NPC"}};
var ar=Roots();ClassRespecialization.Responses(ui,aliased,ref ar);Check(ar.Length==3,"exact native mastery root works with internal actor aliases");
ClassRespecialization.Rendering++;ClassRespecialization.CloseDialog();ClassRespecialization.Rendering--;
PlayerFinder.Actor=new();PlayerFinder.Actor.characterDataTracker.charData=new();ClassRespecialization.Click(new(){response=ar.Last()});Check(ui.Last.Count==6,"native hide during ShowResponses cannot discard the menu action state");
var unity=new UnityUIDialogueUI();var ur=Roots();ClassRespecialization.Responses(unity,listenerSub,ref ur);ClassRespecialization.ClickResponse(ur.Last());Check(unity.Last.Count==6,"Unity UI renderer dispatches all five classes and Back through native virtual ShowResponses");
var back=unity.Last.Last();ClassRespecialization.ClickResponse(back);Check(unity.Last.Count==3,"Back restores both native mastery actions plus class option");
var unrelated=Roots();OfflineToolkit.Enabled=false;ClassRespecialization.Responses(ui,listenerSub,ref unrelated);Check(unrelated.Length==2,"online menu remains unchanged");OfflineToolkit.Enabled=true;
Console.WriteLine("PASS "+checks.Count+" actual-source checks");Directory.CreateDirectory("validation");File.WriteAllText("validation/dev55-lerinne-fix-fixture.json",JsonSerializer.Serialize(new{passed=checks.Count,checks,actualSource=true,nativeGameplayValidated=false},new JsonSerializerOptions{WriteIndented=true}));
