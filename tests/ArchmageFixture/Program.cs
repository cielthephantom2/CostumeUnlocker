using System.Reflection;
using System.Text.Json;
using Il2Cpp;
using Il2CppLE.UI.Tooltips;
using LastEpochLAN.Mod.OfflineTools;
var checks=new List<string>();
void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);Console.WriteLine("PASS: "+label);}
void Call(Type type,string method,params object[] args)=>type.GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args);
bool Near(float a,float b)=>Math.Abs(a-b)<.00001f;
var player=new Actor{mana=new()};PlayerFinder.Player=player;
var mutator=new CharacterMutator{actor=player,mana=player.mana,refund20PercentCostChanceBasedOnMaxMana=.35f};
object Begin(CharacterMutator owner,float cost=100){object[] a={owner,cost,null};Call(typeof(OfflineArchmageCastPatch),"Prefix",a);return a[2];}
void End(object scope)=>Call(typeof(OfflineArchmageCastPatch),"Finalizer",scope);
float Refund(BaseMana mana,float amount){object[] a={mana,amount};Call(typeof(OfflineArchmageRefundPatch),"Prefix",a);return (float)a[1];}
foreach(var (mana,tier) in new[]{(0,1),(299,1),(300,2),(999,2),(1000,3),(1001,3),(int.MaxValue,3)})
{
 player.mana.maxMana=mana;var scope=Begin(mutator);
 Check(scope!=null && ArchmageRules.Tier(mana)==tier && Near(mutator.refund20PercentCostChanceBasedOnMaxMana*tier,Math.Min(1,.35f*tier)),"native RNG sees 35/70/100% with boundary mana "+mana);
 Check(Near(Refund(player.mana,20),20*tier),"native 20% refund becomes 20/40/60% at mana "+mana);
 Check(Refund(player.mana,20)==20,"scope consumes only one matching Archmage refund");
 End(scope);Check(Near(mutator.refund20PercentCostChanceBasedOnMaxMana,.35f) && ArchmageCastScope.Current==null,"native chance and scope restored at mana "+mana);
}
for(int points=1;points<=5;points++)
foreach(var (mana,tier) in new[]{(299,1),(300,2),(1000,3)})
{
 player.mana.maxMana=mana;mutator.refund20PercentCostChanceBasedOnMaxMana=.07f*points;
 var scope=Begin(mutator);
 Check(Near(mutator.refund20PercentCostChanceBasedOnMaxMana*tier,Math.Min(1,.07f*points*tier)),"partial allocation keeps native per-point probability: "+points+" points, tier "+tier);
 Check(Refund(player.mana,20)==20*tier,"refund fraction depends on mana tier rather than allocated point count");End(scope);
}
mutator.refund20PercentCostChanceBasedOnMaxMana=.35f;player.mana.maxMana=1000;
Check(Refund(player.mana,20)==20,"Meteor and manual refunds outside Archmage event stay native");
var outer=Begin(mutator);
Check(Refund(new BaseMana(),20)==20,"scope cannot modify another actor's mana object");
Check(Refund(player.mana,12)==12 && !ArchmageCastScope.Current.Used,"unrelated refund amount does not consume correction");
player.mana.CurrentManaRefundCastID++;
Check(Refund(player.mana,20)==20,"new refund cast ID cannot receive an older cast's correction");player.mana.CurrentManaRefundCastID--;
player.mana.maxMana=300;var inner=Begin(mutator);
Check(Near(mutator.refund20PercentCostChanceBasedOnMaxMana,.35f),"nested cast recovers original probability after outer temporary cap");
Check(Refund(player.mana,20)==40,"nested cast uses its own mana tier");End(inner);
Check(ArchmageCastScope.Current==outer && Near(mutator.refund20PercentCostChanceBasedOnMaxMana,1f/3),"nested finalizer restores enclosing scope and temporary field");
Check(Refund(player.mana,20)==60,"enclosing cast keeps tier captured at cast time");End(outer);
Check(Near(mutator.refund20PercentCostChanceBasedOnMaxMana,.35f),"outer finalizer restores original probability");
outer=Begin(mutator);try{throw new InvalidOperationException("simulated native exception");}catch(InvalidOperationException){End(outer);}
Check(ArchmageCastScope.Current==null && Near(mutator.refund20PercentCostChanceBasedOnMaxMana,.35f),"native failure finalizer clears scope and restores temporary chance");
player.mana.maxMana=1000;outer=Begin(mutator);mutator.refund20PercentCostChanceBasedOnMaxMana=.07f;End(outer);
Check(Near(mutator.refund20PercentCostChanceBasedOnMaxMana,.07f),"stats refresh during native callbacks owns newer probability");
mutator.refund20PercentCostChanceBasedOnMaxMana=0;Check(Begin(mutator)==null,"zero Archmage points creates no refund scope");
mutator.refund20PercentCostChanceBasedOnMaxMana=.35f;
Check(Begin(mutator,0)==null && Begin(mutator,-1)==null && Begin(mutator,float.NaN)==null,"zero, negative and invalid mana costs create no scope");
mutator.actor=new Actor();Check(Begin(mutator)==null,"other actor keeps native Archmage");mutator.actor=player;
OfflineToolkit.Enabled=false;Check(Begin(mutator)==null,"online Archmage stays native");OfflineToolkit.Enabled=true;
var node=new SkillTreeNode{tree=new Tree{treeID="mg-1"},id=30};
string Legacy(string baseline,string value){object[] a={new SkillTreeNode.NodeTooltipStat{value=baseline},node,value};Call(typeof(OfflineArchmageLegacyValuePatch),"Prefix",a);return (string)a[2];}
foreach(var (mana,tier) in new[]{(299,1),(300,2),(1000,3)})
{
 player.mana.maxMana=mana;
 Check(Legacy("1","5")==""+(5*tier),"legacy tooltip shows +5/+10/+15 spell damage for five points");
 Check(Legacy("7%","35%")==Math.Min(100,35*tier)+"%","legacy tooltip shows 35/70/100% refund chance for five points");
 Check(Legacy("1","<color=#fff>1</color> per point")==$"<color=#fff>{tier}</color> per point","TMP markup and per-point suffix preserved");
 var result=new NodeTooltipModifierInfo{Value="35%"};Call(typeof(OfflineArchmageTooltipValuePatch),"Postfix",new SkillTreeNode.NodeTooltipStat{value="7%"},node,result);
 Check(result.Value==Math.Min(100,35*tier)+"%","V2 tooltip matches legacy probability");
 object[] label={"mg-1",(byte)30,1,"Chance To Refund 20% Of Mana Cost"};Call(typeof(OfflineArchmageStatNamePatch),"Postfix",label);
 Check(((string)label[3]).Contains((20*tier)+"%"),"tooltip label shows actual 20/40/60% refund fraction");
}
Check(Legacy("300","300")=="300" && Legacy("1000","1000")=="1000","mana thresholds are never multiplied in tooltip");
object[] description={"mg-1",(byte)30,"stock"};Call(typeof(OfflineArchmageDescriptionPatch),"Postfix",description);
Check(((string)description[2]).Contains("+5 spell damage; 35% chance to refund 20%") && ((string)description[2]).Contains("+10 spell damage; 70% chance to refund 40%") && ((string)description[2]).Contains("+15 spell damage; 100% chance to refund 60%"),"description includes all three exact totals and maximum-mana conditions");
node.id=31;Check(Legacy("7%","35%")=="35%","neighboring passive tooltip remains native");node.id=30;
node.tree.treeID="SkeletonMage";Check(Legacy("1","5")=="5","Skeleton Mage Archmage remains native");node.tree.treeID="mg-1";
OfflineToolkit.Enabled=false;Check(Legacy("1","5")=="5","online tooltip remains native");
description=new object[]{"mg-1",(byte)30,"stock"};Call(typeof(OfflineArchmageDescriptionPatch),"Postfix",description);Check((string)description[2]=="stock","online description remains native");
Console.WriteLine($"All {checks.Count} actual-source Archmage checks passed; native engine not exercised.");
if(args.Length>0)File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=checks.Count,nativeGameplayVerified=false,checks},new JsonSerializerOptions{WriteIndented=true}));
