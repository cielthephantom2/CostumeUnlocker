using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Data;
using Il2CppCysharp.Threading.Tasks;
using System.Text.Json;
using UnityEngine;
using Il2CppLE.Services;
using Il2CppLE.Services.Models;
using Il2CppLE.Services.Transitions;
using Il2CppOperationResult;

namespace LastEpochLAN.Mod.OfflineTools;

// Pending IDs come only from native offline creation, never character-list load.
// A persistent journal lets an interrupted first entry resume without re-granting
// quests already marked completed by the native character quest receiver.
internal static class NewCharacterBoost
{
    internal sealed class Job
    {
        public bool Completed {get;set;}
        public string Error {get;set;}="";
    }
    private static Dictionary<string,Job> jobs=new();
    private static readonly string path=Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","LastEpochLAN.new-characters.json");
    private static string current="";
    private static float nextPoll;
    private static readonly CharacterSetupGate gate=new();
    // CreatedAt's generated Nullable<DateTimeOffset> getter is broken in this
    // client. Native creation events distinguish reused offline save slots.
    private static string Key(CharacterData data)=>data.Id+"|"+data.CharacterName+"|"+data.CharacterClass;
    private static Queue<Quest> pending;
    private static HashSet<ZoneChapterManager.Chapter> chapters;
    private static UniTask.Awaiter save;
    private static bool saving;
    private static UniTask<Status<TransitionError>>.Awaiter travel;
    private static bool travelling;
    private static int stage;
    private static float travelAt;
    internal static bool CompletedFor(CharacterData data)=>jobs.TryGetValue(Key(data),out var job) && job.Completed;
    internal static string ErrorFor(CharacterData data)=>jobs.TryGetValue(Key(data),out var job) ? job.Error : "";
    internal static string Status="New characters: automatic campaign completion enabled.";
    internal static bool BlocksRoom {get;private set;}
    internal static object Diagnostics=>new {Status,stage,saving,travelling,BlocksRoom,creationPending=gate.Pending(Time.unscaledTime)};
    internal static void Load()
    {
        if(File.Exists(path)) jobs=JsonSerializer.Deserialize<Dictionary<string,Job>>(File.ReadAllText(path)) ?? new();
        // An old pending job must not block a different character at the menu.
        BlocksRoom=false;
    }
    private static void Store()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path+".tmp",JsonSerializer.Serialize(jobs,new JsonSerializerOptions {WriteIndented=true}));
        File.Move(path+".tmp",path,true);
    }
    internal static void Created(CharacterData data,bool nativeCreation=false)
    {
        if(!OfflineToolkit.Enabled || !OfflineToolkit.Settings.AutoCompleteNewCharacter || data==null || string.IsNullOrEmpty(data.Id)) return;
        string key=Key(data);
        if(jobs.ContainsKey(key) && !nativeCreation) return;
        jobs[key]=new();Store();BlocksRoom=true;
        MelonLoader.MelonLogger.Msg("New offline character queued for native campaign completion: "+data.CharacterName);
    }
    internal static void ObserveCreation(LocalCharacterSlots slots)
    {
        if(!OfflineToolkit.Enabled || !OfflineToolkit.Settings.AutoCompleteNewCharacter || slots==null || slots.selectedClass==null) return;
        var ids=new List<string>();
        if(slots.characterSlots!=null)
            foreach(var data in slots.characterSlots) if(data!=null) ids.Add(data.Id);
        gate.Arm(slots.inputTextTMP?.text,(int)slots.selectedClass.classID,ids,Time.unscaledTime);
        BlocksRoom=gate.Pending(Time.unscaledTime);
        MelonLoader.MelonLogger.Msg("New-character creation requested; campaign setup waits for native loading to finish.");
        // Do not inspect, await or detour the native async state machine.
    }
    internal static void SceneChanged() {gate.ResetReadiness();BlocksRoom=saving || travelling;}
    internal static void Tick()
    {
        // Consume native save completion once, including during a scene change.
        if(saving)
        {
            if(!save.IsCompleted) return;
            saving=false;
            try {save.GetResult();stage=3;travelAt=Time.unscaledTime+0.5f;}
            catch(Exception ex) {Fail(ex);}
        }
        if(travelling)
        {
            if(!travel.IsCompleted) return;
            travelling=false;
            try
            {
                var result=travel.GetResult();
                if(!result.IsSuccess) throw new InvalidOperationException("End of Time travel failed: "+result.Error);
                stage=4;gate.ResetReadiness();
            }
            catch(Exception ex) {Fail(ex);}
        }
        if(!OfflineToolkit.Settings.AutoCompleteNewCharacter) {BlocksRoom=false;return;}
        if(Time.unscaledTime<nextPoll) return;
        nextPoll=Time.unscaledTime+0.1f;
        if(!OfflineToolkit.Ready) {gate.ResetReadiness();BlocksRoom=gate.Pending(Time.unscaledTime);return;}
        var actor=PlayerFinder.getPlayerActor();var data=actor.characterDataTracker.charData;
        string key=Key(data);
        var transition=ServiceProvider.OfflineTransitionService;
        var identity=data.UserIdentity;
        bool playable=actor.HasMovedToPlayerSpawn && actor.gameObject.scene.IsValid() && actor.gameObject.scene.isLoaded &&
            Il2CppClientAppState.ClientStateManager.CurrentClientAppStateType==Il2CppClientAppState.ClientAppStateType.InGame &&
            !Il2CppLE.UI.LoadingScreen.IsVisible && transition!=null && identity!=null && !transition.IsTransitioning(identity);
        if(!gate.Ready(key+"|"+actor.gameObject.scene.handle,playable,Time.unscaledTime))
        {BlocksRoom=gate.Pending(Time.unscaledTime) || playable && jobs.TryGetValue(key,out var waiting) && !waiting.Completed && waiting.Error=="";return;}
        if(gate.ConsumeCreation(data.Id,data.CharacterName,(int)data.CharacterClass,Time.unscaledTime)) Created(data,true);
        if(!jobs.TryGetValue(key,out var job) || job.Completed) {BlocksRoom=false;return;}
        BlocksRoom=true;
        if(current!=key)
        {current=key;pending=null;stage=0;MelonLoader.MelonLogger.Msg("New-character setup: player spawned, loading hidden and scene stable.");}
        if(job.Error!="") {BlocksRoom=false;Status="Character boost paused: "+job.Error;return;}
        try
        {
            if(stage==0)
            {
                if(QuestList.get()?.quests==null || SceneList.instance?.scenes==null || actor.experienceTracker==null) return;
                if(pending==null)
                {
                    pending=new();chapters=new();
                    foreach(var quest in QuestList.get().quests)
                        if(quest!=null && (quest.mainLineQuest || quest.idolUnlockReward>0 || quest.passivePointsReward>0))
                        {chapters.Add(quest.chapter);if(quest.id!=148) pending.Enqueue(quest);}
                    data.ReachedTown=true;
                }
                int budget=4;
                while(budget-->0 && pending.TryDequeue(out var quest))
                {
                    var list=quest.getLocalStatefulQuestList();
                    if(list==null) throw new InvalidOperationException("Quest receiver not ready for "+quest.name);
                    if(list.getQuestState(quest)==QuestState.Completed) continue;
                    quest.startQuestNoNotification();quest.completeQuest(actor,true);
                    if(list.getQuestState(quest)!=QuestState.Completed) throw new InvalidOperationException("Quest did not complete: "+quest.name);
                }
                Status=$"Completing campaign: {pending.Count} quests remaining.";
                if(pending.Count>0) return;
                var scenes=SceneList.instance.scenes;
                int sceneCount=scenes.Cast<Il2CppSystem.Collections.Generic.ICollection<SceneDetails>>().Count;
                for(int i=0;i<sceneCount;i++)
                {
                    var scene=scenes[i];
                    if(scene!=null && chapters.Contains(scene.chapter) && !data.UnlockedWaypointScenes.Contains(scene.Name))
                        data.UnlockedWaypointScenes.Add(scene.Name);
                }
                if(!data.UnlockedWaypointScenes.Contains("EoT")) data.UnlockedWaypointScenes.Add("EoT");
                UnlockPortalInteraction.unlockPortal(actor.gameObject);stage=1;
            }
            if(stage==1)
            {
                int budget=2;
                while(actor.experienceTracker.CurrentLevel<58 && budget-->0) actor.experienceTracker.LevelUp(true);
                Status="Preparing character level: "+actor.experienceTracker.CurrentLevel+" / 58";
                if(actor.experienceTracker.CurrentLevel<58) return;
                save=data.SaveData().GetAwaiter();saving=true;stage=2;
                MelonLoader.MelonLogger.Msg("New-character setup: campaign and level prepared; waiting for native save.");return;
            }
            if(stage==3 || stage==4)
            {
                if(actor.gameObject.scene.name=="EoT")
                {job.Completed=true;Store();BlocksRoom=false;Status="New character ready: campaign completed, level 58+, End of Time.";return;}
                if(stage==4) throw new InvalidOperationException("Travel finished outside End of Time; automatic setup paused.");
                if(Time.unscaledTime<travelAt) return;
                byte waypointGate=0;
                foreach(var pin in Resources.FindObjectsOfTypeAll<UIWaypoint>())
                    if(pin!=null && !pin.noWaypointInScene && pin.sceneName=="EoT") {waypointGate=pin.gate;if(waypointGate==0) break;}
                travel=transition.Waypoint(identity,TransitionFadeType.ToBlack,"EoT",waypointGate).GetAwaiter();
                travelling=true;
                Status="Campaign saved; travelling to End of Time...";
                MelonLoader.MelonLogger.Msg("New-character setup: saved; issued one End of Time transition.");
            }
        }
        catch(Exception ex) {Fail(ex);}
    }
    private static void Fail(Exception ex)
    {
        BlocksRoom=false;
        var error=ex.GetBaseException().Message;Status="Character boost paused: "+error;
        if(jobs.TryGetValue(current,out var job)) {job.Error=error;Store();}
        MelonLoader.MelonLogger.Warning(Status);
    }
}
[HarmonyPatch(typeof(LocalCharacterSlots),nameof(LocalCharacterSlots.CreateCharacter))]
internal static class QueueNewOfflineCharacterBoostPatch
{
    private static void Prefix(LocalCharacterSlots __instance)
    {try {NewCharacterBoost.ObserveCreation(__instance);}catch(Exception ex) {OfflineToolkit.Error="Character creation hook: "+ex.GetBaseException().Message;}}
}
