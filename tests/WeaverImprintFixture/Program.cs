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
    faction.OnCacheOpenedChanceForSimilarItems=.029f;
    Call(point,"Postfix",faction,WeaverTreeNodeEffect.ImprintContainerCachesDropMoreItemsOfType,1);
    Check(Math.Abs(faction.OnCacheOpenedChanceForSimilarItems-.12f)<.000001f,"One Sealed point overrides legacy .029 with .12 to match unmodded 6% x2");
}
Call(point,"Postfix",faction,WeaverTreeNodeEffect.ImprintContainerCachesDropMoreItemsOfType,1);
Check(Math.Abs(faction.OnCacheOpenedChanceForSimilarItems-.12f)<.000001f,"Repeating the Sealed hook without native reset never stacks above12%");
faction.OnCacheOpenedChanceForSimilarItems=0;
Call(point,"Postfix",faction,WeaverTreeNodeEffect.ImprintContainerCachesDropMoreItemsOfType,0);
Check(faction.OnCacheOpenedChanceForSimilarItems==0,"Removing all Sealed points removes bonus");
faction.ImprintContainerChampionsChanceToDropMoreItemsOfType=.09f;
Call(point,"Postfix",faction,WeaverTreeNodeEffect.ImprintContainerChampionsDropMoreItemsOfType,3);
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
Check(targets.Count()==4 && targets.Any(m=>m.DeclaringType==typeof(EchoWeb) && m.Name=="getIslandTypeForNewIsland"),"Woven scope wraps outer Monolith selection including native spawn-weight consumer");
Check(!targets.Any(m=>m.Name=="IncreaseChanceForSpecificEchoTypes"),"Empowered-to-Normal ref-float trampoline boundary is never detoured");
Check(targets.Single(m=>m.Name=="getIslandTypeForNewIsland").GetParameters().All(p=>!p.ParameterType.IsByRef),"replacement outer boundary contains no by-reference arguments");
foreach(bool empowered in new[]{true,false,true,false})
{
    object[] state={null};Call(typeof(OfflineWeaverWovenImprintScopePatch),"Prefix",state);
    Check(Chance(wovenWeight,0)==0,"zero/disabled native echo weighting stays zero during mode switch: "+empowered);
    Check(Chance(wovenWeight,.05f)==.1f,"outer selection retains Woven weighting x2 during mode switch: "+empowered);
    object[] nestedState={null};Call(typeof(OfflineWeaverWovenImprintScopePatch),"Prefix",nestedState);
    Check(Chance(wovenWeight,.05f)==.1f,"nested native selection scales once during mode switch");
    Call(typeof(OfflineWeaverWovenImprintScopePatch),"Finalizer",nestedState[0]);
    try{throw new InvalidOperationException("simulated selection failure");}catch(InvalidOperationException){Call(typeof(OfflineWeaverWovenImprintScopePatch),"Finalizer",state[0]);}
    Check(WeaverImprintCallScope.Current==null && Chance(wovenWeight,.05f)==.05f,"selection failure restores native weighting context between timelines");
}
OfflineToolkit.Enabled=false;
var disabled=Begin(zone);End(zone,disabled);
Check(zone.totalSpawners==75.25f && Chance(typeof(OfflineWeaverCommonerChancePatch),.02f,zone)==.02f,"Online mode does not modify Commoner data or helper");
using(var scope=new WeaverImprintCallScope(true,true))
    Check(Chance(echoChance,1.2f)==1.2f && Chance(wovenWeight,.05f)==.05f,"Online mode never boosts Echo/Woven even with an existing scope");
faction.OnCacheOpenedChanceForSimilarItems=.029f;
Call(point,"Postfix",faction,WeaverTreeNodeEffect.ImprintContainerCachesDropMoreItemsOfType,1);
Check(faction.OnCacheOpenedChanceForSimilarItems==.029f,"Online mode leaves faction point-derived rates unchanged");
OfflineToolkit.Enabled=true;
var warlord=new WeaverTreeNode{nodeEffect=WeaverTreeNodeEffect.ImprintContainerChampionsDropMoreItemsOfType};
var tooltip=typeof(OfflineWeaverImprintTooltipStatPatch);
for(int i=0;i<3;i++)
{
    var stat=new Il2CppLE.UI.Tooltips.NodeTooltipModifierInfo{Value="3%"};Call(tooltip,"Postfix",warlord,stat);
    Check(stat.Value=="6%","fresh Warlord native tooltip refresh shows6% without repeated stacking");
}
var sealedNode=new WeaverTreeNode{nodeEffect=WeaverTreeNodeEffect.ImprintContainerCachesDropMoreItemsOfType};
var comma=new Il2CppLE.UI.Tooltips.NodeTooltipModifierInfo{Value="+6,0 %"};Call(tooltip,"Postfix",sealedNode,comma);
Check(comma.Value=="+12 %","verified Sealed 6% tooltip becomes12% and preserves plus sign and percent spacing");
var echoNode=new WeaverTreeNode{nodeEffect=WeaverTreeNodeEffect.ImprintContainerEchoCompletionsDropMoreItemsOfType};
var maximum=new Il2CppLE.UI.Tooltips.NodeTooltipModifierInfo{Value="50%"};Call(tooltip,"Postfix",echoNode,maximum);
Check(maximum.Value=="100%","Echoing displayed maximum doubles50% to100% rather than remaining50%");
var unrelated=new Il2CppLE.UI.Tooltips.NodeTooltipModifierInfo{Value="3%"};Call(tooltip,"Postfix",new Il2Cpp.SkillTreeNode(),unrelated);
Check(unrelated.Value=="3%","non-Weaver node tooltip remains untouched");
var prose=new Il2CppLE.UI.Tooltips.NodeTooltipModifierInfo{Value="3 items"};Call(tooltip,"Postfix",warlord,prose);
Check(prose.Value=="3 items","pile quantity is never doubled by tooltip correction");
foreach(var effect in Enum.GetValues<WeaverTreeNodeEffect>().Where(e=>e!=WeaverTreeNodeEffect.Other))
{
    var info=new Il2CppLE.UI.Tooltips.NodeTooltipInfo{Description="Native description"};
    Call(typeof(OfflineWeaverImprintTooltipInfoPatch),"Postfix",new WeaverTreeNode{nodeEffect=effect},info);
    Check(info.Description.StartsWith("Native description\n\nOffline bonus:") &&
        (effect is not (WeaverTreeNodeEffect.ImprintContainerSpawnMoreWovenEchoesOfType or WeaverTreeNodeEffect.ImprintContainerSpawnMoreWovenEchoesOfTypeSecond) || info.Description.Contains("weighting x2")),
        "six Imprint descriptions preserve native rules and accurately explain offline rate/weight multiplier");
}
OfflineToolkit.Enabled=false;var online=new Il2CppLE.UI.Tooltips.NodeTooltipModifierInfo{Value="3%"};Call(tooltip,"Postfix",warlord,online);
Check(online.Value=="3%","official online tooltip keeps original values");
OfflineToolkit.Enabled=true;
object[] legacyArgs={warlord,"3%"};Call(typeof(OfflineWeaverImprintLegacyValuePatch),"Prefix",legacyArgs);
Check((string)legacyArgs[1]=="6%","legacy BuildColoredValue receives6% before native coloring, even for unallocated preview");
object[] legacySealed={sealedNode,"6%"};Call(typeof(OfflineWeaverImprintLegacyValuePatch),"Prefix",legacySealed);
Check((string)legacySealed[1]=="12%","legacy one-point Sealed tooltip matches12% gameplay target");
object[] legacyEcho={echoNode,"50%"};Call(typeof(OfflineWeaverImprintLegacyValuePatch),"Prefix",legacyEcho);
Check((string)legacyEcho[1]=="100%","legacy Echoing tooltip shows100% maximum");
var sealedInfo=new Il2CppLE.UI.Tooltips.NodeTooltipInfo{Description="Native Sealed description"};
Call(typeof(OfflineWeaverImprintTooltipInfoPatch),"Postfix",sealedNode,sealedInfo);
Check(sealedInfo.Description.Contains("12% per allocated point") && !sealedInfo.Description.Contains("5.8%"),"Sealed description no longer contradicts native12% tooltip or gameplay target");
var oldView=new Il2Cpp.NodeTooltipManager();oldView.description.text="Native legacy description";
Call(typeof(OfflineWeaverImprintLegacyInfoPatch),"Postfix",oldView,warlord);
Check(oldView.description.text.Contains("6% per allocated point") && oldView.requireLayoutUpdate,"legacy renderer description is updated and native layout invalidated");
string once=oldView.description.text;Call(typeof(OfflineWeaverImprintLegacyInfoPatch),"Postfix",oldView,warlord);
Check(oldView.description.text==once,"legacy description correction does not append duplicate notes");
object[] unrelatedLegacy={new Il2Cpp.SkillTreeNode(),"3%"};Call(typeof(OfflineWeaverImprintLegacyValuePatch),"Prefix",unrelatedLegacy);
Check((string)unrelatedLegacy[1]=="3%","legacy unrelated skill/passive node remains untouched");
OfflineToolkit.Enabled=false;object[] onlineLegacy={warlord,"3%"};Call(typeof(OfflineWeaverImprintLegacyValuePatch),"Prefix",onlineLegacy);
Check((string)onlineLegacy[1]=="3%","legacy official online values stay native");
if(args.Length>0)File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=checks.Count,scope="actual patch source linked against native stubs; no game launched or native detours executed",checks},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"All {checks.Count} Weaver patch fixture checks passed.");
