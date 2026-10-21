using System.Reflection;
using System.Text.Json;
using Il2Cpp;
using LastEpochLAN.Mod.OfflineTools;

int count=0;
void Check(bool passed,string label){if(!passed)throw new Exception(label);count++;Console.WriteLine("PASS: "+label);}
object Call(Type type,string method,params object[] args)=>type.GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
var player=new Actor{Pointer=(IntPtr)1};PlayerFinder.Player=player;
var minion=new Actor{Pointer=(IntPtr)2,Stance=PlayerMinionStance.Assassinate,References=new(){creator=player}};
Check(AggressiveMinionRange.Applies(minion),"attack stance applies to local player's minion/companion");
Check(!AggressiveMinionRange.Applies(player) && !AggressiveMinionRange.Applies(new Actor{Pointer=(IntPtr)3,Stance=PlayerMinionStance.Assassinate}),"player and wild enemy are excluded");
minion.Stance=PlayerMinionStance.Protect;
Check(!AggressiveMinionRange.Applies(minion),"protect stance keeps native acquisition range");
minion.Stance=PlayerMinionStance.Assassinate;
var foreign=new Actor{Pointer=(IntPtr)4,Stance=PlayerMinionStance.Assassinate,References=new(){creator=new Actor{Pointer=(IntPtr)5}}};
Check(!AggressiveMinionRange.Applies(foreign),"summons belonging to another actor are excluded");
var sub=new Actor{Pointer=(IntPtr)6,References=new(){creator=minion}};
Check(AggressiveMinionRange.Applies(sub),"sub-minions inherit attack stance through bounded local ownership chain");
var loop=new Actor{Pointer=(IntPtr)7,Stance=PlayerMinionStance.Assassinate};loop.References=new(){creator=loop};
Check(!AggressiveMinionRange.Applies(loop),"cyclic creator chain cannot hang target search");
OfflineToolkit.Enabled=false;Check(!AggressiveMinionRange.Applies(minion),"online environment is unchanged");OfflineToolkit.Enabled=true;
var ability=new UsingAbility{Actor=minion};minion.stateController.aggroRangeModifier=0.5f;
object[] prefix={ability,null};Call(typeof(AggressiveMinionAcquirePatch),"Prefix",prefix);
Check(minion.stateController.aggroRangeModifier==2,"existing +50% native range becomes x2 total range, rather than adding 100 percentage points");
var failure=new InvalidOperationException("native failure");
Check(ReferenceEquals(Call(typeof(AggressiveMinionAcquirePatch),"Finalizer",failure,prefix[1]),failure) && minion.stateController.aggroRangeModifier==0.5f,"native exception is propagated and original modifier is restored");
for(int i=0;i<100;i++){object[] p={ability,null};Call(typeof(AggressiveMinionAcquirePatch),"Prefix",p);Call(typeof(AggressiveMinionAcquirePatch),"Finalizer",null,p[1]);}
Check(minion.stateController.aggroRangeModifier==0.5f,"100 acquisition cycles cause no permanent range inflation");
var finder=new TargetFinder{thisActor=minion,MaxDistanceFromSummoner=new(30)};
object[] target={finder,false,false,null};Call(typeof(AggressiveMinionTargetPatch),"Prefix",target);
Check(finder.MaxDistanceFromSummoner.Value==60,"target acquisition extends finite summoner search boundary x2");
Call(typeof(AggressiveMinionTargetPatch),"Finalizer",failure,target[3]);
Check(finder.MaxDistanceFromSummoner.Value==30,"target boundary restores after exception");
foreach(var flags in new[]{(true,false),(false,true)}){object[] p={finder,flags.Item1,flags.Item2,null};Call(typeof(AggressiveMinionTargetPatch),"Prefix",p);Check(p[3]==null && finder.MaxDistanceFromSummoner.Value==30,"ally/self targeting does not expand hostile acquisition boundary");}
finder.MaxDistanceFromSummoner=default;object[] unlimited={finder,false,false,null};Call(typeof(AggressiveMinionTargetPatch),"Prefix",unlimited);
Check(unlimited[3]==null && !finder.MaxDistanceFromSummoner.HasValue,"unlimited native boundary remains unlimited");
Check(AggressiveMinionRange.DoubleRange(float.PositiveInfinity)==float.PositiveInfinity && AggressiveMinionRange.DoubleRange(float.MaxValue)==float.MaxValue && AggressiveMinionRange.DoubleRange(0)==0,"invalid, overflowing and zero ranges stay unchanged");
Console.WriteLine($"All {count} actual-source minion stub checks passed; Unity gameplay not exercised.");
if(args.Length>0)File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=count,nativeGameplayVerified=false}));
