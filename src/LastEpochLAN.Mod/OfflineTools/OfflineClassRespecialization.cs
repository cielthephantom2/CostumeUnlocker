using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Data;
using Il2CppCysharp.Threading.Tasks;
using UnityEngine;
using DialogueUI=Il2CppPixelCrushers.DialogueSystem.AbstractDialogueUI;
using TmpDialogueUI=Il2CppPixelCrushers.DialogueSystem.TextMeshPro.TextMeshProDialogueUI;
using ResponseButton=Il2CppPixelCrushers.DialogueSystem.TextMeshPro.TextMeshProResponseButton;
using Il2CppPixelCrushers.DialogueSystem;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using ClientState=Il2CppClientAppState.ClientAppStateType;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class ClassRespecialization
{
    private const int Open=90000,Cancel=90001,Back=90002,Choose=90100,Confirm=90200;
    private static readonly Dictionary<IntPtr,int> actions=new();
    private static DialogueUI ui;
    private static Subtitle subtitle;
    private static Il2CppReferenceArray<Response> original;
    private static bool drawing;
    private static CharacterData data;
    private static CharacterClass target;
    private static ClassRespecializationData.Snapshot snapshot;
    private static UniTask.Awaiter save;
    private static bool saving;
    private static string characterId;
    internal static string Status="";
    internal static bool Busy=>data!=null;
    private static readonly CharacterClassID[] classes={CharacterClassID.Mage,CharacterClassID.Rogue,CharacterClassID.Primalist,CharacterClassID.Sentinel,CharacterClassID.Acolyte};
    private static Response Option(int action,string text)
    {
        var response=new Response(FormattedText.Parse(text,null),new DialogueEntry{id=action},true);
        response.formattedText.position=-1;
        actions[response.Pointer]=action;return response;
    }
    internal static void Responses(DialogueUI owner,Subtitle speech,ref Il2CppReferenceArray<Response> responses)
    {
        if(drawing || !OfflineToolkit.Enabled || speech==null || responses==null)return;
        // Response subtitles may identify the player as speaker and the NPC as
        // listener. The exact native mastery root also works with internal NPC aliases.
        bool IsLerinne(Il2CppPixelCrushers.DialogueSystem.CharacterInfo info)=>info!=null && ((info.nameInDatabase??"")+" "+(info.Name??""))
            .Contains("Lerinne",StringComparison.OrdinalIgnoreCase);
        var labels=responses.Select(r=>r?.formattedText?.text??"").ToArray();
        bool nativeRoot=labels.Any(t=>t.Contains("Respecialize Mastery Point",StringComparison.OrdinalIgnoreCase)) &&
            labels.Any(t=>t.Contains("Respecialize Mastery",StringComparison.OrdinalIgnoreCase) &&
                !t.Contains("Point",StringComparison.OrdinalIgnoreCase));
        if(!nativeRoot && !IsLerinne(speech.speakerInfo) && !IsLerinne(speech.listenerInfo))return;
        if(responses.Length==0)return;
        responses=responses.Where(response=>response==null || !actions.ContainsKey(response.Pointer)).ToArray();
        ui=owner;subtitle=speech;original=responses;actions.Clear();
        var next=new Il2CppReferenceArray<Response>(responses.Length+1);
        for(int i=0;i<responses.Length;i++)next[i]=responses[i];
        next[responses.Length]=Option(Open,"<sprite name=respec> Respecialize Class");responses=next;
    }
    private static void Show(params Response[] responses)
    {
        if(ui==null || subtitle==null)return;
        drawing=true;
        try{ui.ShowResponses(subtitle,responses,0);}finally{drawing=false;}
    }
    private static void Classes()
    {
        actions.Clear();var options=new List<Response>();
        var current=PlayerFinder.getPlayerActor()?.characterDataTracker?.charData;
        foreach(var id in classes)
        {
            var choice=Option(Choose+(int)id,id.ToString());
            choice.enabled=current!=null && current.CharacterClass!=(int)id && !Busy;
            options.Add(choice);
        }
        options.Add(Option(Cancel,"Back"));Show(options.ToArray());
    }
    internal static bool Click(ResponseButton button)
    {
        return ClickResponse(button?.response);
    }
    internal static bool ClickResponse(Response response)
    {
        if(response==null || !actions.TryGetValue(response.Pointer,out int action))return true;
        if(!OfflineToolkit.Enabled || Busy || !response.enabled)return false;
        try
        {
            if(action==Open || action==Back){Classes();return false;}
            if(action==Cancel)
            {
                actions.Clear();
                // Reinject the root option through the standard native path.
                var roots=original;Responses(ui,subtitle,ref roots);
                drawing=true;try{ui.ShowResponses(subtitle,roots,0);}finally{drawing=false;}
                return false;
            }
            if(action>=Choose && action<Choose+5)
            {
                int id=action-Choose;actions.Clear();
                Show(Option(Confirm+id,"Change to "+((CharacterClassID)id)+" and return to character selection\nKeep level, items and progress; reset mastery, passives and skills."),Option(Back,"Back"));
                return false;
            }
            if(action>=Confirm && action<Confirm+5)Begin(action-Confirm);
        }
        catch(Exception ex){Fail(ex);}
        return false;
    }
    private static void Begin(int id)
    {
        if(!OfflineToolkit.Ready || Il2CppClientAppState.ClientStateManager.CurrentClientAppStateType!=ClientState.InGame)return;
        var actor=PlayerFinder.getPlayerActor();var current=actor?.characterDataTracker?.charData;
        var next=CharacterClassList.getClass(id);
        if(current==null || next==null || current.CharacterClass==id || UIBase.instance==null)return;
        if(actor.health==null || actor.health.currentHealth<=0)return;
        // Exit first. Native state transition finishes the current character's
        // save and tears down the actor before any class-specific data changes.
        data=current;characterId=current.Id;target=next;snapshot=null;
        Status="Returning to character selection to change class...";
        UIBase.instance.ExitToCharacterSelect();
    }
    internal static void Tick()
    {
        if(data==null)return;
        try
        {
            if(!OfflineToolkit.Enabled)throw new InvalidOperationException("Class change requires offline mode");
            if(saving)
            {
                if(!save.IsCompleted)return;
                save.GetResult();saving=false;
                Status="Class changed to "+target.className+". Select the character to continue.";
                var selector=CharacterSelect.instance;
                if(selector?.availableCharacterTiles!=null)
                    foreach(var tile in selector.availableCharacterTiles)
                        if(tile?.characterData?.Id==characterId)tile.Set(data);
                MelonLoader.MelonLogger.Msg(Status);Clear();return;
            }
            if(Il2CppClientAppState.ClientStateManager.CurrentClientAppStateType!=ClientState.CharacterSelect ||
                PlayerFinder.getPlayerActor()!=null || CharacterSelect.instance==null)return;
            // Switch to the freshly loaded menu entity if native exit reloaded
            // the datastore. Only the requested character can be modified.
            var holder=LocalCharacterSlots.instance;
            if(holder==null || !holder.CharacterSlotsLoaded || holder.characterSlots==null)return;
            CharacterData fresh=null;
            foreach(var slot in holder.characterSlots)if(slot?.Id==characterId){fresh=slot;break;}
            if(fresh==null)throw new InvalidOperationException("Requested character is not in the offline character list");
            data=fresh;
            var global=GlobalTreeData.get()?.getTreeData(target.treeID);
            if(global==null)throw new InvalidOperationException("Target passive tree unavailable");
            string source=Path.Combine(Application.persistentDataPath,"Saves");
            var files=Directory.GetFiles(source);
            if(files.Length==0)throw new InvalidOperationException("No native save files available to back up");
            string backup=Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","LastEpochLAN","backups","class-respec-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backup);
            foreach(var file in files)File.Copy(file,Path.Combine(backup,Path.GetFileName(file)));
            if(data.DataStoreCache==null || data.UserIdentity==null || data.IDataStore==null || !data.IDataStore.IsOffline())
                throw new InvalidOperationException("Offline save cache is unavailable");
            snapshot=ClassRespecializationData.Capture(data);
            ClassRespecializationData.Apply(data,target,global.version);
            // Flush the native datastore directly: CharacterData.SaveData resolves
            // an in-world PlayerStore identity which no longer exists in the menu.
            data.SetDirty(true);save=data.DataStoreCache.Flush(data.UserIdentity,false).GetAwaiter();saving=true;
            Status="Saving class change...";MelonLoader.MelonLogger.Msg("Class respecialization backup: "+backup);
        }
        catch(Exception ex){Fail(ex);}
    }
    private static void Fail(Exception ex)
    {
        if(data!=null && snapshot!=null)ClassRespecializationData.Restore(data,snapshot);
        Status="Class change stopped: "+ex.GetBaseException().Message;OfflineToolkit.Error=Status;
        MelonLoader.MelonLogger.Warning(Status);Clear();
    }
    private static void Clear(){data=null;target=null;snapshot=null;saving=false;characterId=null;actions.Clear();}
    internal static int Rendering;
    internal static void CloseDialog(){if(drawing || Rendering>0)return;ui=null;subtitle=null;original=null;actions.Clear();}
}
[HarmonyPatch(typeof(TmpDialogueUI),nameof(TmpDialogueUI.ShowResponses))]
internal static class OfflineClassRespecMenuPatch
{
    private static void Prefix(TmpDialogueUI __instance,Subtitle subtitle,ref Il2CppReferenceArray<Response> responses)
    {ClassRespecialization.Rendering++;ClassRespecialization.Responses(__instance,subtitle,ref responses);}
    private static void Finalizer()=>ClassRespecialization.Rendering--;
}
[HarmonyPatch(typeof(ResponseButton),nameof(ResponseButton.OnClick))]
internal static class OfflineClassRespecClickPatch
{private static bool Prefix(ResponseButton __instance)=>ClassRespecialization.Click(__instance);}
[HarmonyPatch(typeof(TmpDialogueUI),nameof(TmpDialogueUI.HideResponses))]
internal static class OfflineClassRespecClosePatch
{private static void Postfix()=>ClassRespecialization.CloseDialog();}
[HarmonyPatch(typeof(LocalCharacterSlots),nameof(LocalCharacterSlots.LoadCharacterByData))]
internal static class OfflineClassRespecLoadGatePatch
{
    private static bool Prefix(ref UniTask __result)
    {if(!ClassRespecialization.Busy)return true;__result=UniTask.CompletedTask;return false;}
}

// The shipped UI can use either native Dialogue System renderer.
[HarmonyPatch(typeof(UnityUIDialogueUI),nameof(UnityUIDialogueUI.ShowResponses))]
internal static class OfflineClassRespecUnityMenuPatch
{
    private static void Prefix(UnityUIDialogueUI __instance,Subtitle subtitle,ref Il2CppReferenceArray<Response> responses)
    {ClassRespecialization.Rendering++;ClassRespecialization.Responses(__instance,subtitle,ref responses);}
    private static void Finalizer()=>ClassRespecialization.Rendering--;
}
[HarmonyPatch(typeof(UnityUIResponseButton),nameof(UnityUIResponseButton.OnClick))]
internal static class OfflineClassRespecUnityClickPatch
{private static bool Prefix(UnityUIResponseButton __instance)=>ClassRespecialization.ClickResponse(__instance?.response);}
[HarmonyPatch(typeof(UnityUIDialogueUI),nameof(UnityUIDialogueUI.HideResponses))]
internal static class OfflineClassRespecUnityClosePatch
{private static void Postfix()=>ClassRespecialization.CloseDialog();}
