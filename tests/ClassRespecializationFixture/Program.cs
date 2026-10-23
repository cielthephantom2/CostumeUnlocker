using System.Reflection;
using System.Text.Json;
using Il2Cpp;
using Il2CppLE.Data;
using LastEpochLAN.Mod.OfflineTools;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
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
var d2=actor.characterDataTracker.charData;
ClassRespecialization.Open();Check(ClassRespecialization.MenuOpen && ClassRespecialization.Selected==-1 && !ClassRespecialization.Busy,"opening panel does not change character");
Check(ClassRespecialization.Classes.Select(c=>c.ToString()).SequenceEqual(new[]{"Mage","Rogue","Primalist","Sentinel","Acolyte"}),"panel offers all five classes");
ClassRespecialization.Select(1);Check(ClassRespecialization.Selected==-1,"current class cannot be selected");
ClassRespecialization.Select(4);Check(ClassRespecialization.Selected==4 && !ClassRespecialization.Busy && UIBase.Exits==0,"selection requires separate confirmation");
ClassRespecialization.Cancel();ClassRespecialization.Confirm();Check(!ClassRespecialization.Busy && UIBase.Exits==0,"cancel clears pending confirmation");
ClassRespecialization.Select(4);ClassRespecialization.Confirm();Check(ClassRespecialization.Busy && UIBase.Exits==1 && d2.CharacterClass==1 && d2.SaveCalls==0,"confirmation exits first without touching live class data");
ClassRespecialization.Tick();Check(d2.CharacterClass==1 && d2.SaveCalls==0,"in-game transition cannot mutate or save class");
PlayerFinder.Actor=null;Il2CppClientAppState.ClientStateManager.CurrentClientAppStateType=Il2CppClientAppState.ClientAppStateType.CharacterSelect;
var dir=Path.Combine(Path.GetTempPath(),"class-respec-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(dir,"Saves"));File.WriteAllText(Path.Combine(dir,"Saves","slot.bin"),"byte-exact-native-save");UnityEngine.Application.persistentDataPath=dir;LocalCharacterSlots.instance.characterSlots.Add(d2);CharacterData.NextSave=new(){Done=false};
ClassRespecialization.Tick();Check(d2.CharacterClass==4 && d2.SaveCalls==1 && ClassRespecialization.Busy,"menu-side migration starts native save and remains busy until completion");ClassRespecialization.Tick();Check(ClassRespecialization.Busy,"unfinished native save is awaited");CharacterData.NextSave.Done=true;ClassRespecialization.Tick();Check(!ClassRespecialization.Busy && d2.Level==90 && d2.CharacterClass==4,"completed native save releases load gate with new class intact");
Check(Directory.GetFiles(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","LastEpochLAN","backups"),"slot.bin",SearchOption.AllDirectories).Any(f=>File.ReadAllText(f)=="byte-exact-native-save"),"raw native save backed up before class mutation");
// Runtime failure and repeat behavior; all use the actual transition state machine.
void Request(CharacterData value,int id){
 PlayerFinder.Actor=new();PlayerFinder.Actor.characterDataTracker.charData=value;Il2CppClientAppState.ClientStateManager.CurrentClientAppStateType=Il2CppClientAppState.ClientAppStateType.InGame;
 ClassRespecialization.Open();ClassRespecialization.Select(id);ClassRespecialization.Confirm();
 PlayerFinder.Actor=null;LocalCharacterSlots.instance.characterSlots.Clear();LocalCharacterSlots.instance.characterSlots.Add(value);Il2CppClientAppState.ClientStateManager.CurrentClientAppStateType=Il2CppClientAppState.ClientAppStateType.CharacterSelect;
}
var failed=new CharacterData();var before=ClassRespecializationData.Capture(failed);CharacterData.NextSave=new(){Done=false,Error=new IOException("native save failed")};Request(failed,0);ClassRespecialization.Tick();Check(failed.CharacterClass==0 && ClassRespecialization.Busy,"second class request works after completed migration");CharacterData.NextSave.Done=true;ClassRespecialization.Tick();Check(!ClassRespecialization.Busy && failed.CharacterClass==1 && ReferenceEquals(failed.SavedCharacterTree,before.Passive) && ReferenceEquals(failed.AbilityBar,before.Bar),"native save failure restores original class and original passive/bar objects");
var delayed=new CharacterData();CharacterData.NextSave=new();Request(delayed,2);LocalCharacterSlots.instance.CharacterSlotsLoaded=false;ClassRespecialization.Tick();Check(delayed.CharacterClass==1 && delayed.SaveCalls==0,"wait for native offline character list before migration");LocalCharacterSlots.instance.CharacterSlotsLoaded=true;GlobalTreeData.Missing=true;ClassRespecialization.Tick();Check(delayed.CharacterClass==1 && !ClassRespecialization.Busy,"unavailable target tree aborts before mutation");GlobalTreeData.Missing=false;
PlayerFinder.Actor=new();PlayerFinder.Actor.characterDataTracker.charData=new();Il2CppClientAppState.ClientStateManager.CurrentClientAppStateType=Il2CppClientAppState.ClientAppStateType.InGame;
OfflineToolkit.Enabled=false;Check(!ClassRespecialization.Available,"online mode disables reset");OfflineToolkit.Enabled=true;
OfflineToolkit.Ready=false;Check(!ClassRespecialization.Available,"uninitialized character disables reset");OfflineToolkit.Ready=true;
PlayerFinder.Actor.health.currentHealth=0;Check(!ClassRespecialization.Available,"dead character disables reset");PlayerFinder.Actor.health.currentHealth=100;
ClassRespecialization.Open();ClassRespecialization.Select(2);PlayerFinder.Actor.characterDataTracker.charData=new(){Id="another-character"};ClassRespecialization.Confirm();Check(!ClassRespecialization.Busy && ClassRespecialization.Selected==-1,"changed character invalidates pending confirmation");
ClassRespecialization.Select(99);Check(ClassRespecialization.Selected==-1,"invalid panel class is rejected");
Console.WriteLine("PASS "+checks.Count+" actual-source checks");Directory.CreateDirectory("validation");File.WriteAllText("validation/dev56-panel-class-fixture.json",JsonSerializer.Serialize(new{passed=checks.Count,checks,actualSource=true,nativeGameplayValidated=false},new JsonSerializerOptions{WriteIndented=true}));
