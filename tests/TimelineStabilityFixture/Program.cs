using System.Reflection;
using System.Text.Json;
using Il2Cpp;
using LastEpochLAN.Mod.OfflineTools;
var checks=new List<string>();
void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);Console.WriteLine("PASS: "+text);}
void Call(Type type,string name,params object[] a)=>type.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,a);
var local=MeteorNodeRuntime.Player;var run=new MonolithRun();
int Base(Actor actor,int original){object[] a={actor,run,original};Call(typeof(OfflineEchoStabilityPatch),"Postfix",a);return (int)a[2];}
Check(Base(local,24)==48 && Base(local,48)==96,"base Echo24 becomes48 or96 after a native faction double");
foreach(string kind in new[]{"Normal","Arena","Shade","Beacon","VesselOfChaos","VesselOfMemory","Cemetery","SpecialWovenEcho","SuperArena","SuperBeacon"})
 Check(Base(local,24)==48,"completion multiplier has no island-type exclusions: "+kind);
Check(Base(new Actor(),24)==24,"other actor completion remains native");
OfflineToolkit.Enabled=false;Check(Base(local,24)==24,"online completion remains native");OfflineToolkit.Enabled=true;
object Bonus(EchoCompletionReward reward,Actor actor){object[] a={reward,actor,null};Call(typeof(OfflineEchoBonusStabilityScopePatch),"Prefix",a);return a[2];}
object Key(MonolithRun target,Actor actor){object[] a={target,actor,null};Call(typeof(OfflineTemporalKeystoneScopePatch),"Prefix",a);return a[2];}
int Add(MonolithRun target,int value)
{
 object[] a={target,value,null};Call(typeof(OfflineScopedStabilityAwardPatch),"Prefix",a);
 int amount=(int)a[1];target.stability=Math.Min(target.Max,target.stability+amount);
 Call(typeof(OfflineScopedStabilityAwardPatch),"Postfix",target,a[2]);return amount;
}
void EndBonus(object scope)=>Call(typeof(OfflineEchoBonusStabilityScopePatch),"Finalizer",scope);
void EndKey(object scope)=>Call(typeof(OfflineTemporalKeystoneScopePatch),"Finalizer",scope);
var reward=new EchoCompletionReward{timelineID=run.timelineID,difficultyIndex=run.difficultyIndex,bonusStability=50};
var bonus=Bonus(reward,local);run.stability=0;Check(Add(run,50)==100 && run.stability==100,"native bonus payout50 becomes100");
Check(reward.bonusStability==50,"saved completion reward stays unmodified");
Check(Add(run,50)==50,"same run can be scaled only once in a native payout scope");EndBonus(bonus);
Check(StabilityAwardScope.Current==null,"bonus finalizer clears payout scope");
run.stability=0;Check(Add(run,37)==37,"Unstable Rift or unrelated AddStability stays native outside scope");
var wrongRun=new MonolithRun{timelineID=TimelineID.Second};bonus=Bonus(reward,local);Check(Add(wrongRun,20)==20,"another timeline stays native inside bonus scope");EndBonus(bonus);
Check(Bonus(reward,new Actor())==null && Key(run,new Actor())==null,"other actors cannot begin payout scopes");
foreach(int value in new[]{0,1,150,210,1600,2150})
{
 run.stability=0;run.Max=10000;var key=Key(run,local);int supplied=Add(run,value);
 object[] a={true,key,value};Call(typeof(OfflineTemporalKeystoneScopePatch),"Postfix",a);EndKey(key);
 Check(supplied==value*2 && (int)a[2]==value*2,"Keystone doubles native amount and reports actual delta: "+value);
}
run.Max=1000;run.stability=900;var capped=Key(run,local);Add(run,150);object[] cappedArgs={true,capped,150};Call(typeof(OfflineTemporalKeystoneScopePatch),"Postfix",cappedArgs);EndKey(capped);
Check(run.stability==1000 && (int)cappedArgs[2]==100,"Keystone respects timeline cap and reports100 actually gained");
run.stability=0;var failed=Key(run,local);object[] failArgs={false,failed,0};Call(typeof(OfflineTemporalKeystoneScopePatch),"Postfix",failArgs);EndKey(failed);Check((int)failArgs[2]==0 && run.stability==0,"failed Keystone does not grant or replay any stability");
bonus=Bonus(reward,local);var inner=Key(run,local);EndKey(inner);Check(ReferenceEquals(StabilityAwardScope.Current,bonus),"nested Keystone restores outer bonus scope");
try{throw new Exception("native failure");}catch{EndBonus(bonus);}Check(StabilityAwardScope.Current==null,"native exception clears all scoped scaling");
int[] Preview(int current,int cap,int min,int max){run.stability=current;run.Max=cap;object[] a={run,min,max};Call(typeof(OfflineTemporalKeystoneRangePatch),"Postfix",a);return new[]{(int)a[1],(int)a[2]};}
Check(Preview(0,1000,150,210).SequenceEqual(new[]{300,420}),"Keystone preview150-210 becomes300-420");
Check(Preview(650,1000,150,210).SequenceEqual(new[]{300,350}),"Keystone preview clips doubled range to remaining cap");
Check(Preview(950,1000,50,50).SequenceEqual(new[]{50,50}),"near-cap preview is actual remaining50, not100");
Check(Preview(1000,1000,0,0).SequenceEqual(new[]{0,0}),"full timeline preview remains zero");
var tooltip=new EchoTooltip();
EchoModListItem Row(string text,string icon)=>new(){description=new(){text=text},spriteSoftRef=new(){SubPath=icon}};
var baseRow=Row("24 Timeline Stability","Icon_Stability");var bonusRow=Row("Up to <color=#123456>50</color> Bonus Timeline Stability","Icon_Stability");var itemRow=Row("24 Tomes of Experience","Icon_XP");
tooltip._echoRewards.AddRange(new[]{baseRow,bonusRow,itemRow});
for(int refresh=0;refresh<3;refresh++)
{
 baseRow.description.text="24 Timeline Stability";bonusRow.description.text="Up to <color=#123456>50</color> Bonus Timeline Stability";
 Call(typeof(OfflineEchoStabilityTooltipPatch),"Postfix",tooltip);
 Check(baseRow.description.text=="48 Timeline Stability" && bonusRow.description.text=="Up to <color=#123456>100</color> Bonus Timeline Stability","native tooltip refresh doubles both screenshot rows exactly once: "+refresh);
}
Check(itemRow.description.text=="24 Tomes of Experience","other reward quantities are not doubled");
Check(TimelineStabilityRules.RewardText("Bis zu 1.500 Stabilitaet")=="Bis zu 3.000 Stabilitaet","localized wording and grouped integer preserved");
object[] bar={24,50};Call(typeof(OfflineEchoStabilityBarPatch),"Prefix",bar);Check((int)bar[0]==48 && (int)bar[1]==100,"Echo preview bar uses48 basic and100 bonus");
object[] tracker={25,50};Call(typeof(OfflineEchoBonusStabilityDisplayPatch),"Prefix",tracker);Check((int)tracker[0]==50 && (int)tracker[1]==100,"live display doubles both numbers without changing bonus fill ratio");
Check(TimelineStabilityRules.Double(int.MaxValue)==int.MaxValue && TimelineStabilityRules.Award(int.MaxValue,int.MaxValue-5)==5,"doubling saturates safely without native signed overflow");
OfflineToolkit.Enabled=false;Check(Bonus(reward,local)==null && Key(run,local)==null,"online cannot create payout scope");
bar=new object[]{24,50};Call(typeof(OfflineEchoStabilityBarPatch),"Prefix",bar);Check((int)bar[0]==24 && (int)bar[1]==50,"online bar is unchanged");
Check(Preview(0,1000,150,210).SequenceEqual(new[]{150,210}),"online Keystone preview is unchanged");
Console.WriteLine($"All {checks.Count} actual-source Timeline Stability checks passed; native engine not exercised.");
if(args.Length>0)File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=checks.Count,nativeGameplayVerified=false,checks},new JsonSerializerOptions{WriteIndented=true}));
