using System.Reflection;
using System.Text.Json;
using Il2Cpp;
using Il2CppLE.Factions;
using LastEpochLAN.Mod.OfflineTools;
using UnityEngine;
var checks=new List<string>();
void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);Console.WriteLine("PASS: "+text);}
void Call(Type t,string method,params object[] args)=>t.GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args);
var player=new Actor();PlayerFinder.Player=player;
var fk=new ForgottenKnights{Rank=10,CachedPlayerActor=player};player.FactionInfo=new Provider{FK=fk};
float Value(int rank)=>rank switch{1=>fk.AdditionalChanceForSwappableNemesisUnique,2=>fk.DoubleStabilityGainChance,5=>fk.AdditionalChanceForHarbingersToDropTemporalKeystone,_=>0};
void Toggle(int rank,bool enable){fk.ToggleRank(rank,enable,true);Call(typeof(OfflineForgottenKnightsRankPatch),"Postfix",fk,rank,enable);}
foreach(int rank in new[]{1,2,5})
{
 Toggle(rank,true);Check(Value(rank)==(rank==5?.5f:1),"actual native rank field corrected: "+rank);
 for(int i=0;i<3;i++)OfflineForgottenKnights.ApplyRank(fk,rank,true);
 Check(Value(rank)==(rank==5?.5f:1),"rank correction remains absolute after repeated refresh: "+rank);
 Toggle(rank,false);Time.unscaledTime+=2;OfflineForgottenKnights.Tick();
 Check(Value(rank)==0,"disabled rank stays zero after tick repair: "+rank);
 Toggle(rank,true);Check(Value(rank)==(rank==5?.5f:1),"re-enabled rank restores intended chance: "+rank);
}
Check(fk.BonusMaxForgingPotentialWhenEmpoweringNemesis==6 && fk.BonusCorruptionPerGazeOfOrobyss==1,"other faction ranks remain unchanged");
foreach(int earned in new[]{0,1,2,4,5,10})
{
 fk.Rank=earned;
 foreach(int rank in new[]{1,2,5})
 {
  fk.ToggleRank(rank,true,true);float original=Value(rank);OfflineForgottenKnights.ApplyRank(fk,rank,true);
  Check(Value(rank)==(earned>=rank?(rank==5?.5f:1):original),"earned rank gating "+earned+" / "+rank);
 }
}
fk.Rank=10;fk.IsMember=false;
foreach(int rank in new[]{1,2,5}){fk.ToggleRank(rank,true,true);float original=Value(rank);OfflineForgottenKnights.ApplyRank(fk,rank,true);Check(Value(rank)==original,"nonmember keeps native chance: "+rank);}
fk.IsMember=true;fk.CachedPlayerActor=new Actor();
foreach(int rank in new[]{1,2,5}){float original=Value(rank);OfflineForgottenKnights.ApplyRank(fk,rank,true);Check(Value(rank)==original,"other actor keeps native chance: "+rank);}
fk.CachedPlayerActor=player;OfflineToolkit.Enabled=false;
foreach(int rank in new[]{1,2,5}){float original=Value(rank);OfflineForgottenKnights.ApplyRank(fk,rank,true);Check(Value(rank)==original,"online retains native chance: "+rank);}
OfflineToolkit.Enabled=true;fk.rankToggles=null;
foreach(int rank in new[]{1,2,5})OfflineForgottenKnights.ApplyRank(fk,rank,true);
Check(Value(1)==1 && Value(2)==1 && Value(5)==.5f,"missing optional toggle dictionary uses earned rank defaults");
fk.rankToggles=new();foreach(int rank in new[]{1,2,5})fk.ToggleRank(rank,true,true);
OfflineToolkit.Ready=false;Time.unscaledTime+=2;OfflineForgottenKnights.Tick();Check(Value(1)==.4f,"tick waits until local gameplay is ready");
OfflineToolkit.Ready=true;OfflineForgottenKnights.Tick();Check(Value(1)==1 && Value(2)==1 && Value(5)==.5f,"tick repairs faction initialized before player discovery");
player.FactionInfo=null;Time.unscaledTime+=2;OfflineForgottenKnights.Tick();Check(true,"missing faction provider safely ignored");
player.FactionInfo=new Provider{FK=fk};
string[] originals={"+40% chance for Nemesis items to include an Egg of the Forgotten","+10% chance to gain twice the amount of stability when completing an echo","20% additional Temporal Keystone drop chance from Harbingers, including Aberroth"};
int[] ranks={1,2,5};
for(int i=0;i<ranks.Length;i++)
{
 object[] args2={ranks[i],FactionID.ForgottenKnights,originals[i]};Call(typeof(OfflineForgottenKnightsDescriptionPatch),"Postfix",args2);
 string expected=originals[i].Replace(i==0?"40%":i==1?"10%":"20%",i==2?"50%":"100%");
 Check((string)args2[2]==expected,"rank description matches actual percentage: "+ranks[i]);
 Call(typeof(OfflineForgottenKnightsDescriptionPatch),"Postfix",args2);Check((string)args2[2]==expected,"description replacement is idempotent: "+ranks[i]);
 object[] other={ranks[i],FactionID.CircleOfFortune,originals[i]};Call(typeof(OfflineForgottenKnightsDescriptionPatch),"Postfix",other);Check((string)other[2]==originals[i],"other faction description unchanged: "+ranks[i]);
 OfflineToolkit.Enabled=false;object[] online={ranks[i],FactionID.ForgottenKnights,originals[i]};Call(typeof(OfflineForgottenKnightsDescriptionPatch),"Postfix",online);Check((string)online[2]==originals[i],"online rank text unchanged: "+ranks[i]);OfflineToolkit.Enabled=true;
}
Check(ForgottenKnightsRules.Description(1,"<color=green>+40 %</color> chance; 140% elsewhere")=="<color=green>+100 %</color> chance; 140% elsewhere","markup, whitespace, plus sign and unrelated numbers preserved");
Check(ForgottenKnightsRules.Description(3,"40% native rank3")=="40% native rank3","unmodified rank description remains native");
Check(ForgottenKnightsRules.Description(1,null)==null,"null description safely ignored");
Console.WriteLine($"All {checks.Count} actual-source Forgotten Knights checks passed; native gameplay/UI not exercised.");
if(args.Length>0)File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=checks.Count,nativeGameplayVerified=false,checks},new JsonSerializerOptions{WriteIndented=true}));
