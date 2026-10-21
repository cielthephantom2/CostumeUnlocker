using System.Reflection;
using System.Text.Json;
using Il2Cpp;
using Il2CppLE.Factions;
using LastEpochLAN.Mod.OfflineTools;

var checks=new List<string>();
void Check(bool condition,string text) {if(!condition)throw new Exception(text);checks.Add(text);Console.WriteLine("PASS: "+text);}
object Call(Type type,string method,params object[] args) => type.GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
float Chance(Type patch,float value,object instance=null)
{
    object[] args=instance==null ? new object[]{value} : new object[]{instance,value};
    Call(patch,"Postfix",args);return (float)args[^1];
}
var point=typeof(OfflineWeaverImprintPointRatesPatch);
var faction=new TheWeaver();
for(int i=0;i<3;i++)
{
    // Native recomputes from points before each postfix, including loading/respec.
    faction.OnCacheOpenedChanceForSimilarItems=.029f*2;
    Call(point,"Postfix",faction,WeaverTreeNodeEffect.ImprintContainerCachesDropMoreItemsOfType);
    Check(Math.Abs(faction.OnCacheOpenedChanceForSimilarItems-.116f)<.000001f,"Repeated native point refresh remains x2, never x4/x8");
}
faction.OnCacheOpenedChanceForSimilarItems=0;
Call(point,"Postfix",faction,WeaverTreeNodeEffect.ImprintContainerCachesDropMoreItemsOfType);
Check(faction.OnCacheOpenedChanceForSimilarItems==0,"Removing all Sealed points removes bonus");
faction.ImprintContainerChampionsChanceToDropMoreItemsOfType=.09f;
Call(point,"Postfix",faction,WeaverTreeNodeEffect.ImprintContainerChampionsDropMoreItemsOfType);
Check(Math.Abs(faction.ImprintContainerChampionsChanceToDropMoreItemsOfType-.18f)<.000001f,"Champion drop field gets x2 before native consumers read it");
var zone=new MonolithZoneManager{Pointer=(IntPtr)1,totalSpawners=75.25f,enemiesCanDropPileOfSimilarItems=true};
object Begin(MonolithZoneManager z) {object[] args={z,default(OfflineWeaverCommonerDropPatch.State)};Call(typeof(OfflineWeaverCommonerDropPatch),"Prefix",args);return args[1];}
void End(MonolithZoneManager z,object state,Exception error=null) => Call(typeof(OfflineWeaverCommonerDropPatch),"Finalizer",z,state,error);
var outer=Begin(zone);
Check(zone.totalSpawners==150.5f,"Actual Commoner prefix doubles inlined numerator");
Check(Chance(typeof(OfflineWeaverCommonerChancePatch),.02f,zone)==.02f,"Helper inside native Commoner scope does not multiply a second time");
var inner=Begin(zone);End(zone,inner);
Check(zone.totalSpawners==150.5f,"Reentrant drop for same zone does not multiply or restore outer scope");
var zone2=new MonolithZoneManager{Pointer=(IntPtr)2,totalSpawners=20,enemiesCanDropPileOfSimilarItems=true};
var other=Begin(zone2);End(zone2,other,new InvalidOperationException("simulated native failure"));
Check(zone2.totalSpawners==20 && zone.totalSpawners==150.5f,"Nested different-zone exception restores only its own original numerator");
End(zone,outer,new InvalidOperationException("simulated native failure"));
Check(zone.totalSpawners==75.25f && !OfflineWeaverCommonerDropPatch.IsScaled(zone),"Native failure finalizer restores exact numerator and clears scope");
Check(Chance(typeof(OfflineWeaverCommonerChancePatch),.02f,zone)==.04f,"Direct Commoner chance helper doubles once outside drop scope");
var echoChance=typeof(OfflineWeaverEchoImprintChancePatch);
var wovenWeight=typeof(OfflineWeaverWovenImprintWeightPatch);
Check(Chance(echoChance,1.2f)==1.2f && Chance(wovenWeight,.05f)==.05f,"Shared helpers remain unchanged outside Imprint consumers");
using(var echo=new WeaverImprintCallScope(true,false))
{
    Check(Chance(echoChance,1.2f)==2.4f && Chance(wovenWeight,.05f)==.05f,"Echo scope boosts only its Corruption factor, including factors above1");
    try
    {
        using var woven=new WeaverImprintCallScope(false,true);
        using var nested=new WeaverImprintCallScope(false,true);
        Check(Chance(wovenWeight,.05f)==.1f && Chance(echoChance,1.2f)==2.4f,"Nested Woven scopes scale exactly once and preserve enclosing Echo context");
        throw new InvalidOperationException("simulated native failure");
    }
    catch(InvalidOperationException) {}
    Check(WeaverImprintCallScope.Current.Echo && !WeaverImprintCallScope.Current.Woven,"Exception disposes nested Woven scopes without clearing outer Echo scope");
}
Check(WeaverImprintCallScope.Current==null,"All Imprint contexts cleared after operation");
var targets=(IEnumerable<MethodBase>)Call(typeof(OfflineWeaverWovenImprintScopePatch),"TargetMethods");
Check(targets.Count()==4 && targets.Any(m=>m.Name=="IncreaseChanceForSpecificEchoTypes"),"Woven scope includes outer Monolith spawn-weight consumer, not only final normalized selection");
OfflineToolkit.Enabled=false;
var disabled=Begin(zone);End(zone,disabled);
Check(zone.totalSpawners==75.25f && Chance(typeof(OfflineWeaverCommonerChancePatch),.02f,zone)==.02f,"Online mode does not modify Commoner data or helper");
using(var scope=new WeaverImprintCallScope(true,true))
    Check(Chance(echoChance,1.2f)==1.2f && Chance(wovenWeight,.05f)==.05f,"Online mode never boosts Echo/Woven even with an existing scope");
faction.OnCacheOpenedChanceForSimilarItems=.029f;
Call(point,"Postfix",faction,WeaverTreeNodeEffect.ImprintContainerCachesDropMoreItemsOfType);
Check(faction.OnCacheOpenedChanceForSimilarItems==.029f,"Online mode leaves faction point-derived rates unchanged");
if(args.Length>0)File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=checks.Count,scope="actual patch source linked against native stubs; no game launched or native detours executed",checks},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"All {checks.Count} Weaver patch fixture checks passed.");
