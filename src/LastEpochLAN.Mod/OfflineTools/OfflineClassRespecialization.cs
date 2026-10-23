using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Data;
using Il2CppCysharp.Threading.Tasks;
using UnityEngine;
using ClientState=Il2CppClientAppState.ClientAppStateType;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class ClassRespecialization
{
    private static CharacterData data;
    private static CharacterClass target;
    private static ClassRespecializationData.Snapshot snapshot;
    private static UniTask.Awaiter save;
    private static bool saving;
    private static string characterId;
    internal static string Status="";
    internal static bool Busy=>data!=null;
    internal static readonly CharacterClassID[] Classes={CharacterClassID.Mage,CharacterClassID.Rogue,CharacterClassID.Primalist,CharacterClassID.Sentinel,CharacterClassID.Acolyte};
    internal static bool MenuOpen {get;private set;}
    internal static int Selected {get;private set;}=-1;
    private static string selectedCharacterId;
    internal static bool Available=>!Busy && OfflineToolkit.Ready &&
        Il2CppClientAppState.ClientStateManager.CurrentClientAppStateType==ClientState.InGame &&
        PlayerFinder.getPlayerActor()?.health?.currentHealth>0 && UIBase.instance!=null;
    internal static void Open(){if(!Available)return;MenuOpen=true;Cancel();}
    internal static void Cancel(){Selected=-1;selectedCharacterId=null;}
    internal static void Select(int id)
    {
        Cancel();
        var current=PlayerFinder.getPlayerActor()?.characterDataTracker?.charData;
        if(!MenuOpen || !Available || id is <0 or >4 || current==null || current.CharacterClass==id)return;
        Selected=id;selectedCharacterId=current.Id;
    }
    internal static void Confirm()
    {
        if(!MenuOpen || !Available || Selected is <0 or >4)return;
        var current=PlayerFinder.getPlayerActor()?.characterDataTracker?.charData;
        if(current==null || current.Id!=selectedCharacterId){Cancel();return;}
        int id=Selected;Cancel();
        try{Begin(id);}catch(Exception ex){Fail(ex);}
    }
    private static GUIStyle bodyStyle;
    internal static void Draw(float x,float y)
    {
        GUI.Box(new Rect(x,y,310,265),"Reset Class (offline)");
        bodyStyle??=new GUIStyle(GUI.skin.label){wordWrap=true};
        bool enabled=GUI.enabled;
        try
        {
            GUI.enabled=enabled && Available;
            if(!MenuOpen)
            {
                if(GUI.Button(new Rect(x+14,y+30,282,28),"Reset Class"))Open();
            }
            else if(Selected<0)
            {
                int current=PlayerFinder.getPlayerActor()?.characterDataTracker?.charData?.CharacterClass??-1;
                for(int i=0;i<Classes.Length;i++)
                {
                    var id=Classes[i];GUI.enabled=enabled && Available && current!=(int)id;
                    if(GUI.Button(new Rect(x+14,y+30+i*28,180,25),id.ToString()))Select((int)id);
                }
                GUI.enabled=enabled && !Busy;
                if(GUI.Button(new Rect(x+202,y+30,94,25),"Back")){Cancel();MenuOpen=false;}
            }
            else
            {
                GUI.enabled=enabled;
                GUI.Label(new Rect(x+14,y+30,282,24),"Change to "+((CharacterClassID)Selected)+"?");
                GUI.Label(new Rect(x+14,y+57,282,78),"Keep level, items and progress.\nReset mastery, passives and skills.\nReturn to character selection, back up saves, then save the change.",bodyStyle);
                GUI.enabled=enabled && Available;
                if(GUI.Button(new Rect(x+14,y+140,180,28),"Confirm Reset Class"))Confirm();
                GUI.enabled=enabled && !Busy;
                if(GUI.Button(new Rect(x+202,y+140,94,28),"Cancel"))Cancel();
            }
            GUI.enabled=enabled;
            GUI.Label(new Rect(x+14,y+182,282,75),string.IsNullOrEmpty(Status)?
                (Available?"Choose another class. Confirmation is required.":"Enter the game with a living offline character."):Status,bodyStyle);
        }
        finally{GUI.enabled=enabled;}
    }
    private static void Begin(int id)
    {
        if(!Available || id is <0 or >4)return;
        var actor=PlayerFinder.getPlayerActor();var current=actor?.characterDataTracker?.charData;
        var next=CharacterClassList.getClass(id);
        if(current==null || next==null || current.CharacterClass==id)return;
        // Exit first so native code saves and tears down the live actor.
        data=current;characterId=current.Id;target=next;snapshot=null;
        MenuOpen=false;
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
            string source=Il2CppLE.Core.Paths.GetDataPath();
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
    private static void Clear(){data=null;target=null;snapshot=null;saving=false;characterId=null;}
}
[HarmonyPatch(typeof(LocalCharacterSlots),nameof(LocalCharacterSlots.LoadCharacterByData))]
internal static class OfflineClassRespecLoadGatePatch
{
    private static bool Prefix(ref UniTask __result)
    {if(!ClassRespecialization.Busy)return true;__result=UniTask.CompletedTask;return false;}
}
