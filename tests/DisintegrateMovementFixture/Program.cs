using Il2Cpp;
using System.Reflection;
using System.Text.Json;
using LastEpochLAN.Mod.OfflineTools;
var checks=new List<string>();void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
void Invoke(Type type,string name,params object[] values)=>type.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,values);
var actor=PlayerFinder.Player=new();var skill=Ability.getAbility(AbilityID.disintegrate);var use=new UsingAbilityPlayer{Owner=actor,ability=skill,channelling=true};var speed=new SpeedManager{Owner=actor,actor=actor};actor.Components[typeof(UsingAbility)]=use;actor.Components[typeof(SpeedManager)]=speed;
actor.stateController.usingAbility=use;actor.stateController.currentState=use;
var mutator=new DisintegrateMutator{actor=actor};
object[] movement={mutator,AbilityMovement.None};Invoke(typeof(OfflineDisintegrateMovementPatch),"Postfix",movement);Check((AbilityMovement)movement[1]==AbilityMovement.Spin,"Disintegrate selects native mobile channel mode");
var targets=(IEnumerable<MethodBase>)typeof(OfflineDisintegrateMobileFlagsPatch).GetMethod("TargetMethods",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);Check(targets.Count()==4 && targets.All(t=>t!=null),"all four native flag targets resolve independently");
object[] flag={mutator,false};Invoke(typeof(OfflineDisintegrateMobileFlagsPatch),"Postfix",flag);Check((bool)flag[1],"channel keeps aim updates and stops on skill release");
object[] cast={mutator,true};Invoke(typeof(OfflineDisintegrateCastSpeedPatch),"Postfix",cast);Check(!(bool)cast[1],"cast speed cannot amplify walking speed");
Check(typeof(DisintegrateMovement).Assembly.GetType("LastEpochLAN.Mod.OfflineTools.OfflineDisintegrateSpeedPatch")==null,"custom speed penalty hook is absent from actual source");
Check(!typeof(DisintegrateMovement).GetFields(BindingFlags.Static|BindingFlags.NonPublic).Any(f=>f.Name=="SpeedMultiplier"),"custom speed multiplier is absent");
PlayerFinder.Player=new();Check(!DisintegrateMovement.Applies(mutator),"other actors cannot receive local movement override");PlayerFinder.Player=actor;
Check(!DisintegrateMovement.Applies(new AbilityMutator()),"other mutator types stay native");
object[] end={use,false};Invoke(typeof(OfflineDisintegrateStopPatch),"Prefix",end);use.channelling=false;actor.navMeshAgent.speed=1;Invoke(typeof(OfflineDisintegrateStopPatch),"Finalizer",use,end[1]);Check(actor.navMeshAgent.speed==speed.NormalSpeed,"channel finalizer invokes native speed restoration");
object[] description={skill,"native description"};Invoke(typeof(OfflineDisintegrateDescriptionPatch),"Postfix",description);Check(((string)description[1]).Contains("normal movement speed") && !((string)description[1]).Contains("80%") && ((string)description[1]).StartsWith("native description"),"description retains native text and explains normal movement speed");
// Regression: aiming a held beam must not act as a movement command.
var motor=new MovementFromAbility{actor=actor,currentAbility=skill};use.movementFromAbility=motor;use.channelling=true;actor.Position=new(3,0,5);
object[] start={motor,new UnityEngine.Vector3(99,0,99),actor.Position,skill,AbilityMovement.Spin,true,true,false};Invoke(typeof(OfflineDisintegrateStartPatch),"Prefix",start);
Check(((UnityEngine.Vector3)start[1]).Equals(actor.Position) && !(bool)start[5] && !(bool)start[6] && (bool)start[7],"cast start neutralizes mouse destination and native auto-follow flags");
Invoke(typeof(OfflineDisintegrateStartPatch),"Postfix",motor,skill,AbilityMovement.Spin);Check(motor.Destination.Equals(actor.Position) && !motor.changeDestinationWithMousePosition && motor.dontStopAtDestination,"initial channel destination stays at player while beam remains active");
object[] steering={mutator,true};Invoke(typeof(OfflineDisintegrateMouseSteeringPatch),"Postfix",steering);Check(!(bool)steering[1],"mutator disables mouse-driven walking");
object[] aim={use,false};Invoke(typeof(DisintegrateAimUpdateScopePatch),"Prefix",aim);
Check(!(bool)typeof(DisintegrateAimIsNotMovementPatch).GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{motor}) && DisintegrateSteering.AimUpdateDepth==1,"native aim refresh cannot overwrite movement destination");
object[] nested={use,false};Invoke(typeof(DisintegrateAimUpdateScopePatch),"Prefix",nested);Invoke(typeof(DisintegrateAimUpdateScopePatch),"Finalizer",nested[1],new Exception("native aim failure"));Check(DisintegrateSteering.AimUpdateDepth==1,"nested aim exception restores outer scope");
Invoke(typeof(DisintegrateAimUpdateScopePatch),"Finalizer",aim[1],null);Check(DisintegrateSteering.AimUpdateDepth==0 && (bool)typeof(DisintegrateAimIsNotMovementPatch).GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{motor}),"aim finalizer releases filter for explicit movement");
var moving=new MovingPlayer{usingAbilityPlayer=use};
object[] command={use,skill,false};Invoke(typeof(DisintegrateSkillCommandScopePatch),"Prefix",command);
int before=motor.DestinationCalls;object[] move={moving,new UnityEngine.Vector3(50,0,50),false,true};Invoke(typeof(DisintegrateChannelExplicitMovePatch),"Prefix",move);Check(!(bool)move[3] && motor.DestinationCalls==before,"held skill-generated MoveToPoint is blocked");
Invoke(typeof(DisintegrateSkillCommandScopePatch),"Finalizer",command[2],new Exception("native cast failure"));Check(DisintegrateSteering.SkillCommandDepth==0,"cast finalizer clears movement suppression after exception");
object[] explicitMove={moving,new UnityEngine.Vector3(8,0,9),false,false};Invoke(typeof(DisintegrateChannelExplicitMovePatch),"Prefix",explicitMove);Check((bool)explicitMove[3] && motor.Destination.Equals(explicitMove[1]) && use.channelling,"separate move command updates native destination without ending channel");
var keyboard=new MovingPlayerThumbstick{usingAbilityPlayer=use};
foreach(var direction in new[]{new UnityEngine.Vector3(1,0,0),new UnityEngine.Vector3(-1,0,0),new UnityEngine.Vector3(0,0,1),new UnityEngine.Vector3(0,0,-1),new UnityEngine.Vector3(.7071f,0,.7071f)}){
 keyboard.WorldMovementDir=direction;object[] key={keyboard,new UnityEngine.Vector2(1,0),true,false};Invoke(typeof(DisintegrateKeyboardMovementPatch),"Prefix",key);Invoke(typeof(DisintegrateKeyboardMovementPatch),"Postfix",keyboard,key[1],key[3]);Check(!(bool)key[2] && motor.Destination.Equals(actor.Position+direction*2) && use.channelling,"keyboard uses native camera-relative direction while channel remains active: "+direction.x+","+direction.z);
}
keyboard.input=new(1,0);object[] release={keyboard,new UnityEngine.Vector2(0,0),true,false};Invoke(typeof(DisintegrateKeyboardMovementPatch),"Prefix",release);keyboard.input=new();Invoke(typeof(DisintegrateKeyboardMovementPatch),"Postfix",keyboard,release[1],release[3]);Check(motor.Destination.Equals(actor.Position) && use.channelling && use.bufferedCommandHolder.Clears>0,"releasing movement stops walking and clears buffer, without stopping beam");
actor.stateController.numberOfRoots=1;motor.UpdateDestination(new(80,0,80));Invoke(typeof(DisintegrateRespectsRootsPatch),"Prefix",motor);Check(motor.Destination.Equals(actor.Position) && !DisintegrateSteering.CanSteer(actor,false),"Arcane Ascendance or root clears movement and retains native root restriction");actor.stateController.numberOfRoots=0;
UnityEngine.Application.isFocused=false;Check(!DisintegrateSteering.CanSteer(actor,false),"unfocused application cannot steer channel");UnityEngine.Application.isFocused=true;
EpochInputManager.instance.IsInputFieldActive=true;Check(!DisintegrateSteering.CanSteer(actor,false),"chat/input fields cannot steer channel");EpochInputManager.instance.IsInputFieldActive=false;
Il2CppLE.UI.LoadingScreen.IsVisible=true;Check(!DisintegrateSteering.CanSteer(actor,false),"loading cannot accept channel movement");Il2CppLE.UI.LoadingScreen.IsVisible=false;
PlayerFinder.Containers.cursor.Content.Add(1);Check(!DisintegrateSteering.CanSteer(actor,false),"carried item blocks world movement commands");PlayerFinder.Containers.cursor.Content.Clear();
UIMouseListener.Over=true;Check(!DisintegrateSteering.CanSteer(actor,true) && DisintegrateSteering.CanSteer(actor,false),"mouse movement respects UI hover while keyboard keeps its native policy");UIMouseListener.Over=false;
OfflineToolkit.Enabled=false;Check(DisintegrateSteering.ChannelMovement(use)==null,"online channel does not receive custom input handling");OfflineToolkit.Enabled=true;
// Extension: real-source allocation and both tooltip paths.
var definition=new GlobalTreeData.TreeData();definition.Nodes[25]=new(){name="Disintegrate Range"};GlobalTreeData.Instance.Trees["dig5"]=definition;
var allocation=new LocalTreeData.SkillTreeData{treeID="dig5"};allocation.nodes.Add(new(){id=25});
actor.localTreeData=new(){Owner=actor,actor=actor,Skill=allocation};actor.Components[typeof(DisintegrateMutator)]=mutator;
foreach(byte points in new byte[]{0,1,2,3}){
 allocation.nodes[0].pointsAllocated=points;mutator.increasedRange=.12f*points;
 Invoke(typeof(OfflineDisintegrateExtensionTreePatch),"Postfix",actor.localTreeData,allocation);
 Check(Math.Abs(mutator.increasedRange-.30f*points)<.0001f,"Extension native refresh applies30 percent per point: "+points);
 for(int n=0;n<100;n++)Invoke(typeof(OfflineDisintegrateExtensionCastPatch),"Prefix",mutator);
 Check(Math.Abs(mutator.increasedRange-.30f*points)<.0001f,"repeated and twin-beam casts cannot accumulate Extension range: "+points);
}
allocation.nodes.Clear();Invoke(typeof(OfflineDisintegrateExtensionCastPatch),"Prefix",mutator);Check(mutator.increasedRange==0,"respec to zero clears prior Extension bonus");
mutator.increasedRange=.77f;var wrong=new LocalTreeData.SkillTreeData{treeID="other"};Check(!DisintegrateExtension.Apply(mutator,wrong) && mutator.increasedRange==.77f,"another skill tree cannot change Disintegrate range");
var remote=new DisintegrateMutator{actor=new(),increasedRange=.77f};Check(!DisintegrateExtension.Apply(remote,allocation) && remote.increasedRange==.77f,"other actor range is untouched");
OfflineToolkit.Enabled=false;Check(!DisintegrateExtension.Apply(mutator,allocation) && mutator.increasedRange==.77f,"online range stays native");OfflineToolkit.Enabled=true;
var node=new SkillTreeNode{tree=new DisintegrateTree{treeID="dig5"},id=25};Check(DisintegrateExtension.IsNode(node) && !DisintegrateExtension.IsNode(new(){tree=node.tree,id=11}),"tooltip identifies only Extension on Disintegrate");
foreach(var pair in new[]{("12% per point","30% per point"),("36%","90%"),("<color=#abc>12%</color>","<color=#abc>30%</color>"),("12,0% par point","30% par point")})Check(DisintegrateExtension.Tooltip("12%",pair.Item1)==pair.Item2,"legacy tooltip preserves formatting: "+pair.Item1);
Check(DisintegrateExtension.Tooltip("5%","5% per point")=="5% per point" && DisintegrateExtension.Tooltip("12","12")=="12","unrelated tooltip rows stay native");
var stat=new SkillTreeNode.NodeTooltipStat{value="12%"};var info=new Il2CppLE.UI.Tooltips.NodeTooltipModifierInfo{Value=.36f};Invoke(typeof(OfflineDisintegrateExtensionValuePatch),"Postfix",stat,node,info);Check(Math.Abs(info.Value-.90f)<.0001f,"V2 tooltip total updates36 to90 percent");
object[] legacy={stat,node,"12% per point"};Invoke(typeof(OfflineDisintegrateExtensionLegacyValuePatch),"Prefix",legacy);Check((string)legacy[2]=="30% per point","legacy patch displays30 percent per point");
Check(DisintegrateExtension.Description("12% range; 112% other")=="30% range; 112% other","description replaces exact old12 percent token only");
Directory.CreateDirectory("validation");File.WriteAllText("validation/disintegrate-movement-fixture.json",JsonSerializer.Serialize(new{passed=checks.Count,checks,actualSource=true,nativeGameplayValidated=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("PASS "+checks.Count+" actual-source movement checks; native gameplay pending");
