using Il2Cpp;
using Il2CppCysharp.Threading.Tasks;
using Il2CppLE.Gameplay.Monolith;
using UnityEngine;
using System.Diagnostics;
using System.Reflection;
using Game=Il2CppLE.Gameplay.Monolith.MonolithGameplayManager;
namespace ItemNodeNativeQA;

// Private QA helper only. Timers/observers never ship in the Steam mod pair.
public partial class Audit
{
 int echo500Stage;
 UniTask.Awaiter echo500Pending;
 Stopwatch echo500Clock;
 double echo500PreparedMs,echo500LoadMs,echo500MoveMs;
 float echo500ReadySince;
 string echo500Scene;
 MonolithRun echo500Run;
 int echo500FirstFrame;
 int echoCorruption=500;
 double firstVisibleMs;
 void EchoLoad500(Actor actor)
 {
  if(echo500Stage==0)
  {
   string expected=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","GuestProfile"));
   Check(Il2CppLE.Core.PlatformFileManager.ApplicationSavePath==expected && actor.characterDataTracker.charData.CharacterName.StartsWith("LANQA"),"Echo500 uses isolated disposable native character");
   var settings=T("OfflineToolkit").GetField("Settings",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
   settings.GetType().GetProperty("AutoCompleteNewCharacter").SetValue(settings,false);
   var corruptionArg=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("--lelan-echo-corruption="));
   if(corruptionArg!=null)echoCorruption=int.Parse(corruptionArg.Split('=')[1]);
   EchoLoadProfile.Begin(HarmonyInstance);
   bool pcg=Environment.GetCommandLineArgs().Contains("--lelan-echo500-pcg");
   var timeline=pcg ? TimelineList.get().realTimelines.ToArray().First(t=>t.empoweredNonQuestZones.ToArray().Any(z=>z.sceneName=="M_M70")) : TimelineList.getTimeline(TimelineID.UndeadAbom);
   var timelineId=timeline.timelineID;
   Check(timeline!=null && timeline.difficulties.Count>1,"Native empowered timeline assets available");
   Application.targetFrameRate=120;QualitySettings.vSyncCount=0;Application.runInBackground=true;
   echo500FirstFrame=Time.frameCount;echo500Clock=Stopwatch.StartNew();
   var seedArg=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("--lelan-echo-seed="));
   if(seedArg!=null)
   {
    uint seed=uint.Parse(seedArg.Split('=')[1]);
    EchoWeb.rng.Seed(seed);EchoWebIsland.rng.Seed(seed);MonolithRNGManager.Rng.Seed(seed);
    SpawnerPlacementRoom.rng.Seed(seed);Spawner.staticRng.Seed(seed);MonsterGenerator.rng.Seed(seed);
    UnityEngine.Random.InitState((int)seed);
   }
   var web=EchoWeb.initialiseNewWeb(timelineId,1,echoCorruption,actor);
   var island=web.islands.entries.ToArray().First(i=>i.islandType==EchoWebIsland.IslandType.Normal);
   echo500Run=new MonolithRun(timelineId,1,new SharedMonolithRunsData(),web,0,false,false,false,0,EchoWebIsland.IslandType.Origin,0,0,0,false,false,new(),new());
   echo500Run.timeline=timeline;echo500Run.selectIsland(island);
   var zones=timeline.empoweredNonQuestZones.Count>0?timeline.empoweredNonQuestZones:timeline.nonQuestZones;
   if(pcg)for(int i=0;i<zones.Count;i++)if(zones[i].sceneName=="M_M70"){island.zoneIndex=i;break;}
   echo500Scene=zones[island.zoneIndex].sceneName;
   var details=SceneList.FindSceneDetails(echo500Scene);
   Check(details?.monolithInformation!=null && web.corruption==echoCorruption,"Real detached run has requested corruption and native ordinary echo scene",new{echo500Scene,island.zoneIndex,corruption=web.corruption});
   var player=actor.GetActorSync().TryCast<PlayerActorSync>();
   Game.SetHostPlayer(player);Game.HostUserIdentity=player.UserIdentity;Game.ActiveRun=echo500Run;Game.ActiveIsland=island;
   Game.SetActiveMetaData(new EchoMetadata{HostIdentity=player.PlayerConnectionId,TimelineID=timelineId,DifficultyIndex=1,ZoneDifficultyLevel=100,ZoneName=echo500Scene,Island=island,IsFirstAttempt=true,EchoId=1234500});
   Game.activeEchoScene=echo500Scene;
   MonolithRNGManager.GatherNextLevel(details.monolithInformation,echo500Run);
   ZoneInfoManager.ApplyMonolithLevelScaling(echo500Run);
   echo500PreparedMs=echo500Clock.Elapsed.TotalMilliseconds;
   echo500Pending=MonolithLoader.LoadEchoAsync(echo500Scene).GetAwaiter();echo500Stage=1;
   MelonLoader.MelonLogger.Msg("PRIVATE ECHO500 QA loading "+echo500Scene);
   return;
  }
  EchoLoadProfile.Sample(echo500Stage);
  if(echo500Clock.Elapsed.TotalSeconds>300)throw new TimeoutException("Echo500 native loading timeout: "+MonolithLoader.CurrentLoadState+"; stage="+echo500Stage+"; LoadingScreen="+Il2CppLE.UI.LoadingScreen.IsVisible);
  if(echo500Stage==1)
  {
   if(!echo500Pending.IsCompleted)return;
   echo500Pending.GetResult();echo500LoadMs=echo500Clock.Elapsed.TotalMilliseconds;
   Check(MonolithZoneManager.instance!=null && echo500Run.web.corruption==echoCorruption,"Native echo load completed with real Monolith manager at requested corruption",new{echo500Scene,echo500PreparedMs,echo500LoadMs});
   Game.InitialiseOnEchoStart();
   echo500Pending=Game.MovePlayer(actor.GetActorSync().TryCast<PlayerActorSync>(),MonolithUserLocation.Echo,0,null,true,false).GetAwaiter();echo500Stage=2;return;
  }
  if(echo500Stage==2)
  {
   if(!echo500Pending.IsCompleted)return;
   echo500Pending.GetResult();echo500MoveMs=echo500Clock.Elapsed.TotalMilliseconds;echo500Stage=3;
  }
  var playerActor=PlayerFinder.getPlayerActor();
  if(playerActor?.HasMovedToPlayerSpawn!=true || Il2CppLE.UI.LoadingScreen.IsVisible || SceneList.GetCurrentSceneDetails()?.Name!=echo500Scene){echo500ReadySince=0;return;}
  if(echo500ReadySince==0){firstVisibleMs=echo500Clock.Elapsed.TotalMilliseconds;echo500ReadySince=Time.unscaledTime;return;}
  if(Time.unscaledTime-echo500ReadySince<2)return;
  if(EchoLoadProfile.PendingSteps()>0)return;
  var density=(string)T("OfflineDensity").GetField("Status",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
  Check(density.Contains($"Monolith x{echoCorruption/100} ({echoCorruption} corruption)"),"Real native placement used expected density hook at requested corruption",new{density});
  Check(MonolithZoneManager.instance?.IsInitializationFinished==true,"Native player placed in echo, manager initialized and loading screen closed",new{echo500Scene,corruption=echo500Run.web.corruption,firstVisibleMs,amortized=EchoLoadProfile.Amortized,pendingSteps=EchoLoadProfile.PendingSteps(),prepareMs=echo500PreparedMs,loadMs=echo500LoadMs-echo500PreparedMs,movePlayerMs=echo500MoveMs-echo500LoadMs,totalToVisibleMs=echo500Clock.Elapsed.TotalMilliseconds,frames=Time.frameCount-echo500FirstFrame,targetFps=Application.targetFrameRate,scope="Private cold-load ordinary empowered echo; not all Woven/Tombs maps; no profiler in normal DLL"});
  Check(true,"Every expected native monster generated; no queue loss",EchoLoadProfile.VerifyGeneration());
  EchoLoadProfile.VerifyScopes(Check);
  Finish(null);
 }
}
