using Il2Cpp;
using System.Reflection;
using System.Text.Json;
using LastEpochLAN.Mod.OfflineTools;
var checks=new List<string>();void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
void Invoke(Type type,string name,params object[] values)=>type.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,values);
var actor=PlayerFinder.Player=new();var skill=Ability.getAbility(AbilityID.disintegrate);var use=new UsingAbility{Owner=actor,ability=skill,channelling=true};var speed=new SpeedManager{Owner=actor,actor=actor};actor.Components[typeof(UsingAbility)]=use;actor.Components[typeof(SpeedManager)]=speed;
var mutator=new DisintegrateMutator{actor=actor};
object[] movement={mutator,AbilityMovement.None};Invoke(typeof(OfflineDisintegrateMovementPatch),"Postfix",movement);Check((AbilityMovement)movement[1]==AbilityMovement.Spin,"Disintegrate selects native mobile channel mode");
var targets=(IEnumerable<MethodBase>)typeof(OfflineDisintegrateMobileFlagsPatch).GetMethod("TargetMethods",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);Check(targets.Count()==5 && targets.All(t=>t!=null),"all five native flag targets resolve independently");
object[] flag={mutator,false};Invoke(typeof(OfflineDisintegrateMobileFlagsPatch),"Postfix",flag);Check((bool)flag[1],"channel can change target and destination and stop on release");
object[] cast={mutator,true};Invoke(typeof(OfflineDisintegrateCastSpeedPatch),"Postfix",cast);Check(!(bool)cast[1],"cast speed cannot amplify walking speed");
foreach(float normal in new[]{5f,10f,22.5f}){speed.NormalSpeed=normal;for(int n=0;n<100;n++){speed.updateSpeed();Invoke(typeof(OfflineDisintegrateSpeedPatch),"Postfix",speed);}Check(Math.Abs(actor.navMeshAgent.speed-normal*.8f)<.001f,"80 percent native speed without repeated stacking: "+normal);}
use.channelling=false;speed.updateSpeed();Invoke(typeof(OfflineDisintegrateSpeedPatch),"Postfix",speed);Check(actor.navMeshAgent.speed==speed.NormalSpeed,"stopping channel restores full speed");
use.channelling=true;use.ability=Ability.getAbility(AbilityID.fireball);speed.updateSpeed();Invoke(typeof(OfflineDisintegrateSpeedPatch),"Postfix",speed);Check(actor.navMeshAgent.speed==speed.NormalSpeed,"other skills keep native speed");use.ability=skill;
OfflineToolkit.Enabled=false;speed.updateSpeed();Invoke(typeof(OfflineDisintegrateSpeedPatch),"Postfix",speed);Check(actor.navMeshAgent.speed==speed.NormalSpeed && !DisintegrateMovement.Applies(mutator),"online path stays native");OfflineToolkit.Enabled=true;
PlayerFinder.Player=new();Check(!DisintegrateMovement.Applies(mutator) && !DisintegrateMovement.Channel(actor),"other actors cannot receive local movement override");PlayerFinder.Player=actor;
Check(!DisintegrateMovement.Applies(new AbilityMutator()),"other mutator types stay native");
object[] end={use,false};Invoke(typeof(OfflineDisintegrateStopPatch),"Prefix",end);use.channelling=false;actor.navMeshAgent.speed=1;Invoke(typeof(OfflineDisintegrateStopPatch),"Finalizer",use,end[1]);Check(actor.navMeshAgent.speed==speed.NormalSpeed,"channel finalizer invokes native speed restoration");
object[] description={skill,"native description"};Invoke(typeof(OfflineDisintegrateDescriptionPatch),"Postfix",description);Check(((string)description[1]).Contains("80%") && ((string)description[1]).StartsWith("native description"),"description retains native text and explains80 percent speed");
Directory.CreateDirectory("validation");File.WriteAllText("validation/disintegrate-movement-fixture.json",JsonSerializer.Serialize(new{passed=checks.Count,checks,actualSource=true,nativeGameplayValidated=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("PASS "+checks.Count+" actual-source movement checks; native gameplay pending");
