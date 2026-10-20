using HarmonyLib;
using Il2Cpp;
using Il2CppCysharp.Threading.Tasks;
using Il2CppLE.Gameplay.Monolith;
using Il2CppLE.Gameplay.Frontend;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LastEpochLAN.Core;
using UnityEngine;
using EchoGame=Il2CppLE.Gameplay.Monolith.MonolithGameplayManager;

namespace LastEpochLAN.Mod;

// The borrowed run is detached from MonolithRunsManager.runs. Only the Host
// chooses/completes Echoes; the Guest runs native loading and presentation.
internal sealed class LanMonolithParty
{
    internal static LanMonolithParty Active;
    private LanSession session,operationSession;
    private PartyEchoState hostState,target;
    private PartyEchoState receivedEcho;
    private EchoCompletion completion;
    private string[] rewardCharacters=Array.Empty<string>();
    private int completionEchoId=-1;
    private EchoRewardLedger rewardLedger;
    private EchoCompletionReward pendingRestReward;
    internal LanMonolithParty(EchoRewardLedger ledger=null)=>rewardLedger=ledger;
    private LanSession receivedSession;
    private MonolithRun borrowed,previousRun;
    private MonolithRun previousRealRun;
    private EchoMetadata previousMetadata;
    private EchoWebIsland previousIsland;
    private MonolithRNGState previousRng;
    private PlayerActorSync previousHost;
    private Il2CppLE.Services.Models.UserIdentity previousHostIdentity;
    private PlayerActorSync borrowedPlayer;
    private MonolithRun previousRespawnRun;
    private bool hadRespawnRun;
    private string previousEchoScene="",joinedRun="",loadedScene="",failedRun="";
    private int hostEchoId=-1;
    private IntPtr hostRunPointer;
    private string hostRunId="";
    private long revision,appliedRevision,objectiveRevision;
    private float nextObjectiveRetry;
    internal string ObjectiveError="";
    private float nextCapture,deadline,retryAt;
    private UniTask.Awaiter pending;
    private enum Step {None,Exit,Unload,Load,Enter,Spawn}
    private Step step;
    private bool abort,returnOnly,taskPending,cleanupPending;
    private bool entryRequested;
    internal bool Busy=>step!=Step.None;
    internal bool Borrowing=>borrowed!=null;
    internal string Message="Join party only; use Teleport to enter Host Echo";
    internal string Error="";
    internal long Entries,Restores,RewardGrants;
    internal bool GuestAuthority=>Borrowing && !Il2CppEHG.Multiplayer.GameplayEnvironment.IsOnlinePlay;
    internal bool GuestSession=>session?.Connected==true && !session.IsHost && !Il2CppEHG.Multiplayer.GameplayEnvironment.IsOnlinePlay;
    internal bool GameplayReady
    {
        get
        {
            if(!GuestSession) return !Borrowing && !Busy;
            var state=ReadEcho();
            if(!Borrowing && !Busy && state?.Active!=true) return true;
            return !Busy && Borrowing && PartyEchoRules.Matches(state,joinedRun,SceneList.GetCurrentSceneDetails()?.Name??"") &&
                session.WorldEchoRunId==joinedRun;
        }
    }
    internal string HostWorldRun(string scene)=>session?.IsHost==true && hostState?.Active==true && hostState.Scene==scene ? hostState.RunId : "";
    internal string LocalRun=>session?.IsHost==true ? HostWorldRun(SceneList.GetCurrentSceneDetails()?.Name??"") : GameplayReady ? joinedRun : "";
    internal bool BlockSelection()
    {
        if(!Borrowing && !Busy) return false;
        Message="Monolith: wait for Host to choose an Echo";return true;
    }
    internal bool Owns(MonolithRun run)=>GuestAuthority && run?.Pointer==borrowed.Pointer;

    internal void Tick(LanSession value)
    {
        Active=this;session=value;
        if(Il2CppEHG.Multiplayer.GameplayEnvironment.IsOnlinePlay) return;
        try
        {
            if(pendingRestReward!=null && Ready() && SceneList.IsMonolithRestZone(SceneList.GetCurrentSceneDetails()?.Name??"") && MonolithRestZoneManager.instance!=null)
            {
                var reward=pendingRestReward;pendingRestReward=null;
                MonolithRestZoneManager.instance.Initialize(reward,PlayerFinder.getPlayerActor());
            }
            if(!Busy && cleanupPending && Ready()){cleanupPending=false;Begin(null,true);return;}
            if(value?.Connected==true && value.IsHost)
            {
                if(Time.unscaledTime>=nextCapture || (EchoGame.ActiveMetadata?.EchoId??-1)!=hostEchoId ||
                    (EchoGame.ActiveRun?.Pointer??IntPtr.Zero)!=hostRunPointer || (SceneList.GetCurrentSceneDetails()?.Name??"")!=hostState?.HostScene)
                {nextCapture=Time.unscaledTime+0.5f;Capture();}
                return;
            }
            if(Busy)
            {
                if(value!=operationSession || value?.Connected!=true) abort=true;
                else if(!returnOnly && !PartyEchoRules.Matches(ReadEcho(),target.RunId,target.Scene)) abort=true;
                Advance();return;
            }
            if(value?.Connected!=true)
            {
                if(Borrowing && Ready()) Begin(null,true);
                return;
            }
            if(value.IsHost || LanPartyTravel.Active?.Busy==true || LanGift.Active?.Busy==true || !Ready()) return;
            var state=ReadEcho();
            if(state==null) return;
            if(session.PlayerPositions.FirstOrDefault()?.Available!=true) return;
            var current=SceneList.GetCurrentSceneDetails()?.Name??"";
            if(PartyEchoRules.NeedsEntry(state,joinedRun,current))
            {
                if(entryRequested && state.RunId!=failedRun && Time.unscaledTime>=retryAt) {entryRequested=false;Begin(state,false);}
                return;
            }
            if(Borrowing && (!state.Active || !string.Equals(state.HostScene,state.Scene,StringComparison.OrdinalIgnoreCase)))
            {Begin(null,true);return;}
            if(Borrowing && PartyEchoRules.Matches(state,joinedRun,current))
                ApplyProgress(state);
        }
        catch(Exception ex){Fail(ex);}
    }

    private static PlayerActorSync Player()=>PlayerFinder.getPlayerActor()?.GetActorSync()?.TryCast<PlayerActorSync>();
    private PartyEchoState ReadEcho()
    {
        if(receivedSession!=session || (receivedEcho?.Revision??0)!=(session?.EchoRevision??0))
        {receivedSession=session;receivedEcho=session?.Echo;}
        return receivedEcho;
    }
    private static bool Ready()
    {
        var actor=PlayerFinder.getPlayerActor();
        return actor!=null && actor.HasMovedToPlayerSpawn && actor.FactionInfo!=null && !actor.isDying(false) &&
            actor.GetActorSync()?.actorVisuals?.HasBeenInitialized==true && !Il2CppLE.UI.LoadingScreen.IsVisible &&
            Il2CppLE.Services.ServiceProvider.OfflineTransitionService?.IsTransitioning(Player()?.UserIdentity)==false;
    }

    private unsafe void Capture()
    {
        var actor=PlayerFinder.getPlayerActor();
        if(actor==null || !actor.HasMovedToPlayerSpawn || Il2CppLE.UI.LoadingScreen.IsVisible) return;
        var scene=SceneList.GetCurrentSceneDetails()?.Name??"";
        var metadata=EchoGame.ActiveMetadata;var run=EchoGame.ActiveRun;
        var zone=MonolithZoneManager.instance;
        bool inEcho=metadata!=null && run!=null && string.Equals(scene,metadata.ZoneName,StringComparison.OrdinalIgnoreCase) &&
            zone!=null && zone.IsInitializationFinished;
        if(inEcho)
        {
            if(hostEchoId!=metadata.EchoId || hostRunPointer!=run.Pointer || hostRunId=="")
            {
                // serialise()/fromSerialisation() are the legacy option-only
                // format: they omit EchoWeb and construct with a null web.
                // Use the game's full network context codec on both ends.
                var runBuffer=new byte[4*1024*1024];int runOffset=0;
                fixed(byte* ptr=runBuffer) run.WriteToBuffer(ptr,ref runOffset);
                if(runOffset<=0 || runOffset>262144) throw new InvalidDataException("Host Monolith run exceeds transfer budget.");
                var bytes=runBuffer.Take(runOffset).ToArray();
                var buffer=new byte[8192];int offset=0;
                fixed(byte* ptr=buffer) metadata.WriteToBuffer(ptr,ref offset);
                if(offset<=0 || offset>buffer.Length || bytes.Length>262144) throw new InvalidDataException("Echo context exceeds transfer budget.");
                hostEchoId=metadata.EchoId;hostRunPointer=run.Pointer;hostRunId=Guid.NewGuid().ToString("N");
                hostState=new PartyEchoState {Active=true,RunId=hostRunId,Scene=scene,HostScene=scene,
                    Run=bytes,Metadata=buffer.Take(offset).ToArray(),RandomState=MonolithRNGManager.CurrentState.RandomState,
                    ObjectiveSeed=MonolithRNGManager.objectiveSeed,ObjectiveType=(int)MonolithRNGManager.CurrentState.ObjectiveType};
            }
            var descriptions=MonolithUIManager.ObjectiveData?.ObjectiveDescriptions;
            var objectives=new List<EchoObjective>();
            if(descriptions!=null) for(int i=0;i<descriptions.Count;i++)
            {
                var d=descriptions[i];
                objectives.Add(new EchoObjective((int)d.ObjectiveGameMode,d.ObjectiveType,(int)d.Message,
                    d.ObjectiveData,d.ObjectiveCount,(int)d.Category,d.Completed));
            }
            Publish(hostState with {HostScene=scene,Completed=zone.objectiveComplete,PortalTarget=(int)EchoGame.portalTarget,
                Reward=zone.objectiveComplete && metadata.EchoId==completionEchoId ? completion : null,
                RewardCharacters=zone.objectiveComplete && metadata.EchoId==completionEchoId ? rewardCharacters : Array.Empty<string>(),
                BonusStability=Math.Max(0,zone.bonusStablity),MaxBonusStability=Math.Max(0,zone.maxBonusStablity),Objectives=objectives.ToArray()});
        }
        else
        {
            if(hostState==null || hostState.Active)
                Publish(new PartyEchoState {HostScene=scene});
            else if(hostState.HostScene!=scene) Publish(hostState with {HostScene=scene});
            // Next native load is a new generation, including reused scene/EchoId.
            hostRunId="";
            hostEchoId=metadata?.EchoId??-1;hostRunPointer=run?.Pointer??IntPtr.Zero;
        }
    }
    private void Publish(PartyEchoState next)
    {
        if(hostState!=null && next.Active==hostState.Active && next.RunId==hostState.RunId &&
            next.HostScene==hostState.HostScene && next.Completed==hostState.Completed && next.PortalTarget==hostState.PortalTarget &&
            next.Reward==hostState.Reward &&
            next.BonusStability==hostState.BonusStability && next.MaxBonusStability==hostState.MaxBonusStability &&
            next.Objectives.SequenceEqual(hostState.Objectives) && hostState.Revision>0) return;
        hostState=next with {Revision=++revision};session.PublishEcho(hostState);
    }

    internal bool RequestHost(string id)
    {
        if(session?.Connected!=true || session.IsHost || session.PlayerPositions.FirstOrDefault()?.Id!=id) return false;
        if(LanSharedChest.Active?.Busy==true){Message="Wait for shared chest transaction";return true;}
        var state=ReadEcho();
        if(state?.Active!=true || !string.Equals(state.HostScene,state.Scene,StringComparison.OrdinalIgnoreCase))
        {
            var host=session.PlayerPositions.FirstOrDefault();
            if(SceneList.FindSceneDetails(host?.Scene??"")?.sceneType.ToString()!="Monolith") return false;
            Message="Waiting for Host Echo generation context...";
            entryRequested=true;
            return true;
        }
        if(Time.unscaledTime<retryAt) return true;
        if(!Busy && Ready())
        {
            failedRun="";
            if(PartyEchoRules.NeedsEntry(state,joinedRun,SceneList.GetCurrentSceneDetails()?.Name??""))
            {try {entryRequested=false;Begin(state,false);} catch(Exception ex){Fail(ex);}}
            else PlaceBeside(id);
        }
        return true;
    }

    private void Begin(PartyEchoState state,bool leaving)
    {
        target=state;returnOnly=leaving;abort=false;operationSession=session;Error="";
        // Always leave the old native Echo before importing another run. This
        // avoids same-scene shortcuts and unload callbacks clearing new context.
        loadedScene=MonolithLoader.loadedEchoScene??"";
        if(loadedScene!="" && !MonolithLoader.IsEchoUnloaded)
        {
            pending=EchoGame.MovePlayer(Player(),MonolithUserLocation.Rest,0,null,true,false).GetAwaiter();
            Start(Step.Exit,"Leaving previous Echo...");
        }
        else if(leaving){Restore();step=Step.None;}
        else InstallAndLoad();
    }
    private void Start(Step next,string message)
    {step=next;taskPending=true;deadline=Time.unscaledTime+90;Message=message;}
    private void Advance()
    {
        if(step==Step.Spawn)
        {
            if(!Ready()){if(Time.unscaledTime>deadline) throw new TimeoutException("Echo player spawn is not ready.");return;}
            if(abort){Begin(null,true);return;}
            var current=SceneList.GetCurrentSceneDetails()?.Name??"";
            if(!PartyEchoRules.Matches(ReadEcho(),target.RunId,current)) {Begin(null,true);return;}
            joinedRun=target.RunId;step=Step.None;Entries++;
            if(!ReadEcho().Completed) Ledger().Participate(joinedRun,Character());
            ApplyProgress(ReadEcho());
            var host=session.PlayerPositions.FirstOrDefault();
            if(host!=null && host.Scene==current) PlaceBeside(host.Id);
            Message="Joined Host Echo";return;
        }
        if(!pending.IsCompleted)
        {
            // Keep observing the one native task; never issue a competing load
            // or consume a UniTask awaiter twice after a slow transition.
            if(Time.unscaledTime>deadline) Message="Monolith transition still loading...";
            return;
        }
        taskPending=false;pending.GetResult();
        if(step==Step.Exit)
        {
            pending=MonolithLoader.UnloadEchoAsync(loadedScene).GetAwaiter();Start(Step.Unload,"Unloading previous Echo...");return;
        }
        if(step==Step.Unload)
        {
            Restore();
            if(returnOnly || abort){step=Step.None;Message="Left shared Echo";return;}
            InstallAndLoad();return;
        }
        if(step==Step.Load)
        {
            if(abort){Begin(null,true);return;}
            EchoGame.InitialiseOnEchoStart();
            pending=EchoGame.MovePlayer(Player(),MonolithUserLocation.Echo,0,null,true,false).GetAwaiter();
            Start(Step.Enter,"Entering Host Echo...");return;
        }
        if(step==Step.Enter){step=Step.Spawn;deadline=Time.unscaledTime+30;Message="Waiting for Echo spawn...";}
    }
    private void PlaceBeside(string id)
    {
        try {LanPartyTravel.Active?.PlaceBeside(id);}
        catch(Exception ex){Error="Echo loaded; nearby placement: "+ex.GetBaseException().Message;MelonLoader.MelonLogger.Warning(Error);}
    }

    private unsafe void InstallAndLoad()
    {
        if(target==null || !target.IsValid() || !PartyEchoRules.Matches(ReadEcho(),target.RunId,target.Scene))
            throw new InvalidOperationException("Host Echo changed before entry.");
        var details=SceneList.FindSceneDetails(target.Scene);
        if(details?.monolithInformation==null || SceneList.IsNonCombatZone(target.Scene))
            throw new InvalidOperationException("Host destination has no Monolith generation data.");
        var run=new MonolithRun();int runOffset=0;
        fixed(byte* ptr=target.Run)
            if(!run.TryReadFromBuffer(ptr,target.Run.Length,ref runOffset) || runOffset!=target.Run.Length)
                throw new InvalidDataException("Host Monolith run context could not be decoded.");
        var metadata=new EchoMetadata();int offset=0;
        fixed(byte* ptr=target.Metadata)
            if(!metadata.TryReadFromBuffer(ptr,target.Metadata.Length,ref offset) || offset!=target.Metadata.Length)
                throw new InvalidDataException("Host Echo metadata could not be decoded.");
        if(run==null || run.web==null || metadata.ZoneName!=target.Scene || metadata.TimelineID!=run.timelineID || metadata.DifficultyIndex!=run.difficultyIndex)
            throw new InvalidDataException("Host run and Echo metadata do not match.");
        // The network codec intentionally omits process-local Actor references.
        // GatherNextLevel reads faction/Weaver info through this field even for
        // ordinary Echoes. Bind only the detached web to our real local Actor;
        // never alter or save the character's persistent Monolith run.
        var localActor=PlayerFinder.getPlayerActor();
        if(localActor?.FactionInfo==null) throw new InvalidOperationException("Waiting for local faction context before Echo entry.");
        run.web.ActorOnServerOffline=localActor;
        // Woven/Tombs have extra sub-instance state not contained in an Echo run.
        if(metadata.IsTombsEcho || metadata.Island?.IsWovenEcho==true)
            throw new NotSupportedException("Woven/Tombs Echo sub-instances are not supported by shared Echo travel yet.");
        if(metadata.IsQuestEcho)
            throw new NotSupportedException("Quest Echo progression needs shared quest context and is not supported yet.");
        previousRun=EchoGame.ActiveRun;previousIsland=EchoGame.ActiveIsland;previousMetadata=EchoGame.ActiveMetadata;
        previousRealRun=EchoGame._previousRealRun;
        previousEchoScene=EchoGame.activeEchoScene??"";previousRng=MonolithRNGManager.CurrentState;
        previousHost=EchoGame.HostPlayer;
        previousHostIdentity=EchoGame.HostUserIdentity;
        borrowedPlayer=Player();
        hadRespawnRun=EchoGame._activeRunsToSetBackPerPlayerRespawn!=null &&
            EchoGame._activeRunsToSetBackPerPlayerRespawn.TryGetValue(borrowedPlayer,out previousRespawnRun);
        borrowed=run;
        EchoGame.HostPlayer=Player();
        var host=session.PlayerPositions.FirstOrDefault()??throw new InvalidOperationException("Host identity has not arrived yet.");
        EchoGame.HostUserIdentity=new Il2CppLE.Services.Models.UserIdentity(host.Name,host.Id,host.Id,"LAN","LAN",false);
        EchoGame.ActiveRun=run;EchoGame.ActiveIsland=metadata.Island;EchoGame.SetActiveMetaData(metadata);
        EchoGame.activeEchoScene=target.Scene;
        MonolithRNGManager.GatherNextLevel(details.monolithInformation,run);
        ZoneInfoManager.ApplyMonolithLevelScaling(run);
        pending=MonolithLoader.LoadEchoAsync(target.Scene).GetAwaiter();
        Start(Step.Load,"Loading Host Echo...");
    }
    private void ApplyProgress(PartyEchoState state)
    {
        var zone=MonolithZoneManager.instance;if(zone==null || !zone.IsInitializationFinished) return;
        if(state.Revision!=appliedRevision)
        {
            zone.bonusStablity=state.BonusStability;zone.maxBonusStablity=state.MaxBonusStability;
            EchoGame.portalTarget=(MonolithUserLocation)state.PortalTarget;
            if(state.Completed && !zone.objectiveComplete) zone.OnObjectiveCompletedOnOnlineClient();
            if(state.Completed && state.Reward!=null) GrantReward(state);
            appliedRevision=state.Revision;
        }
        // A delayed/broken HUD must never roll back an otherwise loaded Echo.
        // Retry separately even when the gameplay revision is already applied.
        if(state.Revision==objectiveRevision || Time.unscaledTime<nextObjectiveRetry) return;
        try
        {
            ApplyObjectives(state);
            objectiveRevision=state.Revision;ObjectiveError="";
        }
        catch(Exception ex)
        {
            var error=ex.GetBaseException().Message;
            if(error!=ObjectiveError) MelonLoader.MelonLogger.Warning("Monolith objective display (Echo retained): "+error);
            ObjectiveError=error;nextObjectiveRetry=Time.unscaledTime+1;
        }
    }
    private static void ApplyObjectives(PartyEchoState state)
    {
        var objectives=new Il2CppSystem.Collections.Generic.List<DynamicObjectiveDescription>();
        foreach(var d in state.Objectives)
        {
            if((DynamicObjectiveGameMode)d.GameMode!=DynamicObjectiveGameMode.Monolith ||
                !Enum.IsDefined(typeof(MonolithObjectiveType),(MonolithObjectiveType)d.Type) || !Enum.IsDefined(typeof(DynamicObjectiveMessage),(DynamicObjectiveMessage)d.Message) ||
                !Enum.IsDefined(typeof(ObjectiveCategory),(ObjectiveCategory)d.Category)) throw new InvalidDataException("Unknown native Echo objective.");
            // The native formatter uses a runtime cast to MonolithObjectiveDescription.
            // A base description with GameMode=Monolith casts to null and crashes
            // GetFormattedMonolithMessage, even if all scalar fields are correct.
            objectives.Add(new MonolithObjectiveDescription((MonolithObjectiveType)d.Type,(DynamicObjectiveMessage)d.Message,
                d.Data,d.Count,(ObjectiveCategory)d.Category,d.Completed));
        }
        MonolithUIManager.ReplaceObjectiveDescriptions(objectives);
    }
    internal void RecordCompletion(EchoCompletion reward)
    {
        if(session?.IsHost!=true || !reward.IsValid()) return;
        completion=reward;completionEchoId=EchoGame.ActiveMetadata?.EchoId??-1;nextCapture=0;
        rewardCharacters=session.PlayerPositions.Where(p=>p.Cooperative && p.Available && !p.Dead && p.EchoRunId==hostRunId && p.Scene==hostState?.Scene && p.CharacterId!="")
            .Select(p=>p.CharacterId).Distinct().Take(4).ToArray();
    }
    private static string Character()=>PlayerFinder.getPlayerActor()?.characterDataTracker?.charData?.Id??"";
    private EchoRewardLedger Ledger()=>rewardLedger??=new EchoRewardLedger(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","LanEchoRewards"));
    private void GrantReward(PartyEchoState state)
    {
        var character=Character();if(character=="") throw new InvalidOperationException("Echo reward character identity is unavailable.");
        if(!state.RewardCharacters.Contains(character)) return;
        if(!Ledger().TryClaim(state.RunId,character)) return;
        var host=session.PlayerPositions.FirstOrDefault()??throw new InvalidOperationException("Echo Host is unavailable.");
        var identity=new Il2CppLE.Services.Models.UserIdentity(host.Name,host.Id,host.Id,"LAN","LAN",false);
        var r=state.Reward;
        var native=new EchoCompletionReward(identity,borrowed,r.EnemyScore,r.BonusStability,r.Chests,r.HasReward,r.RewardIndex,
            r.HasGuestReward,r.GuestIndex1,r.GuestIndex2,r.CountModifier);
        EchoGame.ProcessEchoCompletionForPlayer(Player(),native,0);RewardGrants++;
        pendingRestReward=native;
    }
    internal void ApplyGeneration()
    {
        if(!GuestAuthority || target==null || MonolithRNGManager.CurrentState==null) return;
        if(!Enum.IsDefined(typeof(MonolithObjectiveType),(MonolithObjectiveType)target.ObjectiveType))
            throw new InvalidDataException("Unknown Host Echo objective type.");
        MonolithRNGManager.CurrentState.RandomState=target.RandomState;
        MonolithRNGManager.CurrentState.ObjectiveType=(MonolithObjectiveType)target.ObjectiveType;
        MonolithRNGManager.objectiveSeed=target.ObjectiveSeed;
    }
    private void Restore()
    {
        if(!Borrowing) return;
        // Never attach the imported run to the player's persistent run list.
        EchoGame.ActiveRun=previousRun;EchoGame.ActiveIsland=previousIsland;EchoGame.ActiveMetadata=previousMetadata;
        EchoGame.HostPlayer=previousHost;previousHost=null;
        EchoGame.HostUserIdentity=previousHostIdentity;previousHostIdentity=null;
        EchoGame._previousRealRun=previousRealRun;previousRealRun=null;
        if(borrowedPlayer!=null && EchoGame._activeRunsToSetBackPerPlayerRespawn!=null)
        {
            EchoGame._activeRunsToSetBackPerPlayerRespawn.Remove(borrowedPlayer);
            if(hadRespawnRun) EchoGame._activeRunsToSetBackPerPlayerRespawn[borrowedPlayer]=previousRespawnRun;
        }
        borrowedPlayer=null;previousRespawnRun=null;hadRespawnRun=false;
        EchoGame.activeEchoScene=previousEchoScene;MonolithRNGManager.CurrentState=previousRng;
        borrowed=null;previousRun=null;previousIsland=null;previousMetadata=null;previousRng=null;
        joinedRun="";appliedRevision=0;objectiveRevision=0;nextObjectiveRetry=0;ObjectiveError="";Restores++;
    }
    private void Fail(Exception ex)
    {
        Error=ex.GetBaseException().Message;Message="Monolith: "+Error;MelonLoader.MelonLogger.Warning(Message);
        failedRun=target?.RunId??"";retryAt=Time.unscaledTime+5;
        // Observe pending native work through completion before rollback.
        abort=true;
        if(!taskPending){step=Step.None;cleanupPending=Borrowing;}
    }
    internal void Disconnect()
    {
        abort=true;if(hostState!=null) hostState=hostState with {Revision=0};
        receivedEcho=null;receivedSession=null;failedRun="";revision=0;entryRequested=false;
    }
}

[HarmonyPatch(typeof(EchoGame),nameof(EchoGame.TryGetRunForPlayer))]
internal static class SharedEchoRunLookupPatch
{
    private static bool Prefix(PlayerActorSync player,TimelineID timelineID,int difficultyIndex,ref MonolithRun output,ref bool __result)
    {
        var shared=LanMonolithParty.Active;
        if(shared?.GuestAuthority!=true || player?.GameplayActor?.Pointer!=PlayerFinder.getPlayerActor()?.Pointer) return true;
        var run=EchoGame.ActiveRun;
        if(!shared.Owns(run) || run.timelineID!=timelineID || run.difficultyIndex!=difficultyIndex) return true;
        output=run;__result=true;return false;
    }
}

// Native player movement calls these manager methods as part of entering and
// leaving. The imported run must remain transient on the Guest.
[HarmonyPatch]
internal static class SharedEchoPersistentRunPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()=>new[]{"setCurrentRun","setBackRun","endRun","completeRun",
        "onEnteredRestZoneFromRun","onRestZoneEnteredAfterEchoCompleted"}.Select(name=>AccessTools.Method(typeof(MonolithRunsManager),name));
    private static bool Prefix(MonolithRun run)=>LanMonolithParty.Active?.Owns(run)!=true;
}

[HarmonyPatch(typeof(MonolithZoneManager),nameof(MonolithZoneManager.CompleteObjective))]
internal static class SharedEchoCompletionPatch
{
    private static bool Prefix()=>LanMonolithParty.Active?.GuestAuthority!=true;
}

[HarmonyPatch(typeof(MonolithRNGManager),nameof(MonolithRNGManager.GatherNextLevel))]
internal static class SharedEchoGenerationPatch
{
    private static void Postfix()=>LanMonolithParty.Active?.ApplyGeneration();
}

[HarmonyPatch(typeof(EchoGame),nameof(EchoGame.ProcessEchoCompletion))]
internal static class SharedEchoRewardAuthorityPatch
{
    private static bool Prefix()=>LanMonolithParty.Active?.GuestAuthority!=true;
    private static void Postfix(float enemyScore,int bonusStability,int rewardChests,bool hasEchoReward,int echoRewardIndex,
        bool hasEchoSpecificRewardForGuests,int guestRewardIndex1,int guestRewardIndex2,float echoRewardCountModifier)=>
        LanMonolithParty.Active?.RecordCompletion(new EchoCompletion(enemyScore,bonusStability,rewardChests,hasEchoReward,echoRewardIndex,
            hasEchoSpecificRewardForGuests,guestRewardIndex1,guestRewardIndex2,echoRewardCountModifier));
}

[HarmonyPatch]
internal static class SharedEchoPrivateProgressPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()=>new[]
    {
        AccessTools.Method(typeof(MonolithRunsManager),"onEchoIslandCompleted"),
        AccessTools.Method(typeof(MonolithProgressManager),"onEchoCompleted"),
        AccessTools.Method(typeof(MonolithProgressManager),"completeTimeline"),
        AccessTools.Method(typeof(MonolithProgressManager),"onTimelineCompleted")
    };
    private static bool Prefix()=>LanMonolithParty.Active?.GuestAuthority!=true;
}

[HarmonyPatch]
internal static class SharedEchoSelectionPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()=>new[]{"AttemptStartRun","AttemptStartQuestAsync","AttemptStartActivity"}
        .Select(name=>AccessTools.Method(typeof(EchoGame),name));
    private static bool Prefix(ref UniTask<Il2CppOperationResult.Status> __result)
    {
        if(LanMonolithParty.Active?.BlockSelection()!=true) return true;
        __result=new UniTask<Il2CppOperationResult.Status>(Il2CppOperationResult.Status.ErrorStatus);return false;
    }
}
