using System.Reflection;
using System.Text.Json;
using Il2Cpp;
using Il2CppLE.UI.Tooltips;
using LastEpochLAN.Mod.OfflineTools;
var checks=new List<string>();
void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);Console.WriteLine("PASS: "+message);}
void Call(Type patch,string name,params object[] args)=>patch.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args);
bool Near(float a,float b)=>Math.Abs(a-b)<.0001f;
var player=new Actor{mana=new()};PlayerFinder.Player=player;
var meteor=new MeteorMutator();var tree=new LocalTreeData{actor=player,Attached=meteor};
for(int points=0;points<=4;points++)
{
 meteor.chanceFor100increasedArea=.1f*points;meteor.frostClawCastsOnImpact=points;
 Call(typeof(OfflineMeteorTreePatch),"Postfix",tree);
 Check(Near(meteor.chanceFor100increasedArea,Math.Min(1,.33f*points)) && meteor.frostClawCastsOnImpact==2*points,
  "native recomputation then postfix sets33% and2 Frost Claws per point, including zero and probability cap: "+points);
}
for(int refresh=0;refresh<3;refresh++)
{
 meteor.chanceFor100increasedArea=.2f;meteor.frostClawCastsOnImpact=2;Call(typeof(OfflineMeteorTreePatch),"Postfix",tree);
 Check(Near(meteor.chanceFor100increasedArea,.66f) && meteor.frostClawCastsOnImpact==4,"repeated full native refresh does not accumulate modifiers");
}
tree.actor=new Actor();meteor.chanceFor100increasedArea=.1f;meteor.frostClawCastsOnImpact=1;
Call(typeof(OfflineMeteorTreePatch),"Postfix",tree);
Check(meteor.chanceFor100increasedArea==.1f && meteor.frostClawCastsOnImpact==1,"enemy or other actor tree remains native");tree.actor=player;
OfflineToolkit.Enabled=false;Call(typeof(OfflineMeteorTreePatch),"Postfix",tree);
Check(meteor.chanceFor100increasedArea==.1f && meteor.frostClawCastsOnImpact==1,"online tree stays native");OfflineToolkit.Enabled=true;
var impact=new MeteorAoEMutator{actor=player,frostClawCastsOnImpact=2};
object Begin(MeteorAoEMutator value){object[] args={value,null};Call(typeof(OfflineMeteorImpactScopePatch),"Prefix",args);return args[1];}
void End(object scope)=>Call(typeof(OfflineMeteorImpactScopePatch),"Finalizer",scope);
float Cost(BaseMana mana,AbilityID id,float original){object[] args={mana,id,original};Call(typeof(OfflineMeteorManaCostPatch),"Postfix",args);return (float)args[2];}
Check(Cost(player.mana,AbilityID.frostClaw,20)==20,"manual Frost Claw outside impact keeps native mana cost");
var outer=Begin(impact);
foreach(float original in new[]{0f,1f,10f,20f,250f})
 Check(Near(Cost(player.mana,AbilityID.frostClaw,original)*1.2f,original),"native1.2 consumer receives100% original cost after scoped correction: "+original);
Check(Cost(player.mana,AbilityID.fireball,20)==20 && Cost(new BaseMana(),AbilityID.frostClaw,20)==20,"scope cannot alter another ability or actor's mana cost");
var inner=Begin(impact);End(inner);
Check(Near(Cost(player.mana,AbilityID.frostClaw,12),10),"nested impact restores enclosing mana scope");
try{throw new Exception("native failure");}catch{End(outer);}
Check(MeteorManaScope.Current==null && Cost(player.mana,AbilityID.frostClaw,20)==20,"finalizer cleanup restores normal cost even after native failure");
impact.frostClawCastsOnImpact=0;var empty=Begin(impact);Check(empty==null,"no Heat Flux points means no mana scope");impact.frostClawCastsOnImpact=2;
impact.actor=new Actor{mana=new()};Check(Begin(impact)==null,"another actor's impact cannot start a mana scope");impact.actor=player;
OfflineToolkit.Enabled=false;Check(Begin(impact)==null,"online impact cannot start a mana scope");OfflineToolkit.Enabled=true;
var metadata=new GlobalTreeData.TreeData();metadata.Nodes[1]=new(){name=MeteorNodeRules.Extinction};metadata.Nodes[2]=new(){name=MeteorNodeRules.HeatFlux};
GlobalTreeData.Instance.Trees["Meteor"]=metadata;
var extinction=new SkillTreeNode{tree=new MeteorSkillTree{treeID="Meteor"},id=1};
var heat=new SkillTreeNode{tree=extinction.tree,id=2};
string Legacy(SkillTreeNode node,string baseline,string value){object[] args={new SkillTreeNode.NodeTooltipStat{value=baseline},node,value};Call(typeof(OfflineMeteorLegacyValuePatch),"Prefix",args);return (string)args[2];}
Check(Legacy(extinction,"10%","30%")=="99%","legacy three-point Extinction chance matches99% rather than changing crit chance");
Check(Legacy(extinction,"3%","9%")=="9%","Extinction's separate crit chance bonus is preserved");
Check(Legacy(heat,"1","3")=="6" && Legacy(heat,"120%","120%")=="100%","legacy Heat Flux shows2 casts/point and100% constant mana ratio");
var value=new NodeTooltipModifierInfo{Value="10%"};Call(typeof(OfflineMeteorTooltipValuePatch),"Postfix",new SkillTreeNode.NodeTooltipStat{value="10%"},extinction,value);
Check(value.Value=="33%","V2 Extinction tooltip shows33%");
value.Value="1";Call(typeof(OfflineMeteorTooltipValuePatch),"Postfix",new SkillTreeNode.NodeTooltipStat{value="1"},heat,value);
Check(value.Value=="2","V2 Heat Flux tooltip shows2");
value.Value="120%";Call(typeof(OfflineMeteorTooltipValuePatch),"Postfix",new SkillTreeNode.NodeTooltipStat{value="120%"},heat,value);
Check(value.Value=="100%","V2 Heat Flux mana shows100%");
var different=new SkillTreeNode{tree=new Tree{treeID="Meteor"},id=1};
Check(Legacy(different,"10%","10%")=="10%","matching name/ID in a non-Meteor tree is never modified");
var info=new NodeTooltipInfo{Description="Consumes 120% of Frost Claw mana cost."};Call(typeof(OfflineMeteorTooltipInfoPatch),"Postfix",heat,info);
Check(info.Description.Contains("Consumes 100%") && info.Description.Contains("2 Frost Claw"),"V2 Heat Flux prose and bonus note agree with new mechanics");
var old=new NodeTooltipManager();old.description.text="Consumes 120% of mana.";Call(typeof(OfflineMeteorLegacyInfoPatch),"Postfix",old,heat);
Check(old.requireLayoutUpdate && old.description.text.Contains("100%"),"legacy description requests native layout refresh");
string once=old.description.text;Call(typeof(OfflineMeteorLegacyInfoPatch),"Postfix",old,heat);
Check(old.description.text==once,"description notes do not accumulate on repeated refresh");
OfflineToolkit.Enabled=false;Check(Legacy(extinction,"10%","10%")=="10%" && Legacy(heat,"1","1")=="1","online tooltips remain native");
Check(MeteorNodeRules.FrostClaws(int.MaxValue)==int.MaxValue && MeteorNodeRules.LargeMeteorChance(float.NaN)==0,"invalid and overflowing inputs remain bounded");
Console.WriteLine($"All {checks.Count} actual-source Meteor checks passed; native execution/rendering not exercised.");
if(args.Length>0)File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=checks.Count,nativeGameplayVerified=false,checks},new JsonSerializerOptions{WriteIndented=true}));
