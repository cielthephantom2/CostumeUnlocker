using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;
namespace ItemNodeNativeQA;

// Private observer: accumulates in memory, writes once at completion.
internal static class EchoLoadProfile
{
 sealed class Metric { public long calls; public double totalMs,maxMs; }
 static readonly Dictionary<string,Metric> metrics=new();
 static readonly List<object> states=new();
 static readonly List<object> slowFrames=new();
 static readonly List<string> hooks=new();
 static Stopwatch clock;
 static double previous;
 static string lastState;
 static long generatedActors,failedActors;
 static readonly Dictionary<IntPtr,Spawner> observedSpawners=new();
 static void ObserveSpawner(Spawner __instance){observedSpawners[__instance.Pointer]=__instance;}
 internal static object VerifyGeneration()
 {
  var spawners=observedSpawners.Values.Where(s=>s!=null && s.monsterGenerator!=null).ToArray();
  long expected=spawners.Sum(s=>(long)s.monsterGenerator.totalSpawnCount);
  long remaining=spawners.Sum(s=>(long)s.monsterGenerator.spawnsRemaining);
  if(remaining!=0 || generatedActors<expected || failedActors!=0 || PendingSteps()!=0)
   throw new Exception($"Native generation incomplete: expected={expected}, created={generatedActors}, failed={failedActors}, remaining={remaining}, pending={PendingSteps()}");
  return new{spawners=spawners.Length,expected,generatedActors,failedActors,remaining,pending=PendingSteps()};
 }
 internal static void VerifyScopes(Action<bool,string,object> check)
 {
  var type=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="LastEpochLAN").GetType("LastEpochLAN.Mod.OfflineTools.OfflineEchoSpawnQueue",true);
  var stateType=type.GetNestedType("State",BindingFlags.NonPublic);
  var before=type.GetMethod("BeforeEnqueue",BindingFlags.NonPublic|BindingFlags.Static);
  var restore=type.GetMethod("Restore",BindingFlags.NonPublic|BindingFlags.Static);
  var spawner=observedSpawners.Values.First();
  bool original=spawner.allowAmortizedSpawn;
  bool Run()
  {
   object[] args={spawner,Activator.CreateInstance(stateType)};before.Invoke(null,args);
   bool changed=(bool)stateType.GetField("Changed",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(args[1]);
   restore.Invoke(null,new[]{(object)spawner,args[1]});
   check(spawner.allowAmortizedSpawn==original,"Pooled native spawner flag restored after enqueue scope",null);
   return changed;
  }
  check(Run(),"Dense native echo enables queue routing",null);
  bool online=Il2CppEHG.Multiplayer.GameplayEnvironment.IsOnlinePlay;
  try{Il2CppEHG.Multiplayer.GameplayEnvironment.IsOnlinePlay=true;check(!Run(),"Online play excluded from echo queue override",null);}
  finally{Il2CppEHG.Multiplayer.GameplayEnvironment.IsOnlinePlay=online;}
  var run=Il2CppLE.Gameplay.Monolith.MonolithGameplayManager.ActiveRun;
  int corruption=run.web.corruption;
  try{run.web.corruption=100;check(!Run(),"Native x1 density excluded from queue override",null);}
  finally{run.web.corruption=corruption;}
  try{Il2CppLE.Gameplay.Monolith.MonolithGameplayManager.ActiveRun=null;check(!Run(),"Missing native Monolith run leaves queue routing unchanged",null);}
  finally{Il2CppLE.Gameplay.Monolith.MonolithGameplayManager.ActiveRun=run;}
 }
 static void Generated(Actor __result){if(__result!=null)generatedActors++;else failedActors++;}
 internal static bool Amortized=>Environment.GetCommandLineArgs().Contains("--lelan-echo-amortized-qa");
 internal static int PendingSteps()
 {
  int total=0;
  if(SpawnQueueManager.instances!=null)foreach(var pair in SpawnQueueManager.instances)
   if(pair.Value?.amortizedQueue!=null)total+=pair.Value.amortizedQueue.GetTotalStepCountRemaining();
  return total;
 }
 static GameLoader bootLoader;
 static float nextBootReport;
 public static void InstallBoot(HarmonyLib.Harmony harmony)
 {
  harmony.Patch(typeof(GameLoader).GetMethod("InitializeAsync"),prefix:new HarmonyMethod(typeof(EchoLoadProfile),nameof(BootLoader)));
  if(Amortized)
  {
   harmony.Patch(typeof(Spawner).GetMethod("EntityGenerationOnStart"),prefix:new HarmonyMethod(typeof(EchoLoadProfile),nameof(UseAmortized)));
   harmony.Patch(typeof(BaseSteppedQueue).GetMethod("ProcessEntireQueueImmediately"),prefix:new HarmonyMethod(typeof(EchoLoadProfile),nameof(KeepAmortized)));
  }
 }
 static void UseAmortized(Spawner __instance)
 {
  if(MonolithZoneManager.instance==null || !MonolithZoneManager.instance.gameObject.scene.isLoaded)return;
  __instance.allowAmortizedSpawn=true;
  var manager=SpawnQueueManager.GetInstance(__instance.gameObject.scene);
  if(manager?.amortizedQueue!=null)manager.amortizedQueue.stepsWithWorkPerFrame=8;
 }
 static bool KeepAmortized(BaseSteppedQueue __instance)=>MonolithZoneManager.instance==null || !MonolithZoneManager.instance.gameObject.scene.isLoaded || __instance.TryCast<AmortizedSteppedQueue>()==null;
 static void BootLoader(GameLoader __instance) { bootLoader=__instance; }
 public static void BootSample()
 {
  if(Time.unscaledTime<nextBootReport)return;
  nextBootReport=Time.unscaledTime+3;
  if(clock==null)Application.targetFrameRate=120;
  var steps=bootLoader?._stepLoader?._allSteps;
  var rows=new List<object>();
  if(steps!=null)foreach(var step in steps)rows.Add(new{name=step.Name,status=step.Status.ToString(),seconds=step.LoadingSeconds,error=step.ErrorMessage});
  File.WriteAllText(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","echo-boot-profile.json"),JsonSerializer.Serialize(new{state=Il2CppClientAppState.ClientStateManager.CurrentClientAppStateType.ToString(),transitioning=Il2CppClientAppState.ClientStateManager._sceneManager?._isTransitioning,seconds=Time.unscaledTime,userService=Il2CppLE.Services.ServiceProvider._UserService?.TryCast<Il2CppLE.Services.OfflineUserService>()!=null,steps=rows}));
 }
 public static void Begin(HarmonyLib.Harmony harmony)
 {
  clock=Stopwatch.StartNew();
  harmony.Patch(typeof(Spawner).GetMethod("EntityGenerationOnStart"),prefix:new HarmonyMethod(typeof(EchoLoadProfile),nameof(ObserveSpawner)));
  harmony.Patch(typeof(MonsterGenerator).GetMethod("InstantiateOneActor"),postfix:new HarmonyMethod(typeof(EchoLoadProfile),nameof(Generated)));
  var targets=new (Type type,string[] names)[]{
   (typeof(SpawnerPlacementManager),new[]{"CreatePoints","DefineRooms","ImproveRooms","CreateSmallTriangles","UpdateConnectedNavmeshTriangles","RollSpawners","GetEligibleSpawners"}),
   (typeof(SpawnerPlacementRoom),new[]{"rollSpawners","chooseSpawner","pointValidForSpawner"}),
   (typeof(Spawner),new[]{"EntityGenerationOnStart","EnqueueSpawnSteps","SpawnOneImmediately"})};
  foreach(var target in targets)
  foreach(var method in target.type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance).Where(m=>target.names.Contains(m.Name)))
  {
   try {harmony.Patch(method,prefix:new HarmonyMethod(typeof(EchoLoadProfile),nameof(Before)),postfix:new HarmonyMethod(typeof(EchoLoadProfile),nameof(After)));hooks.Add(method.ToString());}
   catch(Exception ex){hooks.Add("FAILED "+method+": "+ex.Message);}
  }
 }
 static void Before(out long __state) { __state=Stopwatch.GetTimestamp(); }
 static void After(MethodBase __originalMethod,long __state)
 {
  double ms=(Stopwatch.GetTimestamp()-__state)*1000d/Stopwatch.Frequency;
  string key=__originalMethod.DeclaringType.Name+"."+__originalMethod.Name+"/"+__originalMethod.GetParameters().Length;
  if(!metrics.TryGetValue(key,out var metric))metrics[key]=metric=new();
  metric.calls++;metric.totalMs+=ms;metric.maxMs=Math.Max(ms,metric.maxMs);
 }
 public static void Sample(int stage)
 {
  if(clock==null)return;
  double now=clock.Elapsed.TotalMilliseconds;
  if(previous>0 && now-previous>100 && slowFrames.Count<500)slowFrames.Add(new{atMs=now,ms=now-previous,stage});
  previous=now;
  string state=stage+":"+Il2CppLE.Gameplay.Monolith.MonolithLoader.CurrentLoadState+":"+Il2CppLE.UI.LoadingScreen.IsVisible;
  if(state!=lastState){states.Add(new{atMs=now,state,frame=Time.frameCount});lastState=state;}
 }
 public static void Finish(Exception error)
 {
  if(clock==null)return;
  File.WriteAllText(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","echo-load-profile.json"),JsonSerializer.Serialize(new{error=error?.ToString(),elapsedMs=clock.Elapsed.TotalMilliseconds,hooks,states,slowFrames,metrics},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
 }
}
