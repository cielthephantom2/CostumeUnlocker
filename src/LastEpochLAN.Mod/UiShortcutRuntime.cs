using System.Text.Json;
using Il2Cpp;
using Il2CppCysharp.Threading.Tasks;
using Il2CppLE.Services;
using Il2CppLE.Services.Models;
using Il2CppLE.Services.Transitions;
using Il2CppOperationResult;
using LastEpochLAN.Mod.OfflineTools;
using UnityEngine;

namespace LastEpochLAN.Mod;

internal sealed class UiShortcutRuntime
{
    internal bool Open;
    internal string Status="Choose an action or configure its hotkey.";
    private Dictionary<ShortcutAction,ShortcutBinding> bindings=new();
    private ShortcutAction? capture,pending;
    private EpochInputManager blockedInput;
    private bool previousDisableInput,travelling;
    private float cooldown;
    private float openingUntil;
    internal bool Busy=>travelling || openingUntil>0;
    private UniTask<Status<TransitionError>>.Awaiter travel;
    private static readonly KeyCode[] keys=Enum.GetValues<KeyCode>().Where(UiShortcutRules.ValidKey).Distinct().ToArray();
    private static string ConfigPath=>Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","LastEpochLAN.shortcuts.json");
    internal object Diagnostics=>new {Open,Status,capture,pending,travelling,openingUntil,bindings};
    internal void Initialize()
    {
        try
        {
            if(File.Exists(ConfigPath))
            {
                if(new FileInfo(ConfigPath).Length>16384)throw new InvalidDataException("Shortcut settings exceed 16 KiB");
                bindings=UiShortcutRules.Read(File.ReadAllText(ConfigPath));
            }
        }
        catch(Exception ex){Status="Shortcut settings: "+ex.GetBaseException().Message;}
    }
    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
        File.WriteAllText(ConfigPath+".tmp",JsonSerializer.Serialize(bindings,new JsonSerializerOptions{WriteIndented=true}));
        File.Move(ConfigPath+".tmp",ConfigPath,true);
    }
    private void Block(bool block)
    {
        var input=EpochInputManager.instance;
        if(blockedInput!=null && (!block || input==null || input.Pointer!=blockedInput.Pointer))
        {blockedInput.forceDisableInput=previousDisableInput;blockedInput=null;}
        if(block && blockedInput==null && input!=null)
        {blockedInput=input;previousDisableInput=input.forceDisableInput;input.forceDisableInput=true;}
    }
    internal void Suspend(){Open=false;capture=pending=null;openingUntil=0;Block(false);}
    private static bool Held(KeyCode a,KeyCode b)=>Input.GetKey(a) || Input.GetKey(b);
    private static ShortcutBinding Binding(KeyCode key)=>new(){Key=key,
        Ctrl=Held(KeyCode.LeftControl,KeyCode.RightControl),Alt=Held(KeyCode.LeftAlt,KeyCode.RightAlt),Shift=Held(KeyCode.LeftShift,KeyCode.RightShift)};
    private string Guard(bool click,bool otherPanel)
    {
        if(travelling)return "Travel in progress.";
        var actor=PlayerFinder.getPlayerActor();var ui=UIBase.instance;var input=EpochInputManager.instance;
        if(!OfflineToolkit.Enabled || actor==null || !actor.gameObject.activeInHierarchy || !actor.HasMovedToPlayerSpawn ||
            actor.health?.currentHealth>0!=true || actor.isDying(false) || actor.GetActorSync()?.actorVisuals?.HasBeenInitialized!=true ||
            Il2CppLE.UI.LoadingScreen.IsVisible)return "Wait for a living offline character to finish loading.";
        var identity=actor.characterDataTracker?.charData?.UserIdentity;
        var transition=ServiceProvider.OfflineTransitionService;
        if(identity==null || transition==null || transition.IsTransitioning(identity) || NewCharacterBoost.BlocksRoom)return "Wait for character setup / transition.";
        if(!Application.isFocused || Time.timeScale<=0 || input==null || ui==null)return "Game input is unavailable.";
        bool externalBlock=input.forceDisableInput && (blockedInput==null || previousDisableInput);
        if(externalBlock || input.cinematicActive || input.IsInputFieldActive || ui.menuOpen || ui.IsChatInputFocused)return "Close text input, menu or cinematic first.";
        if(!click && (otherPanel || ui.AnyPanelOpened() || input.ButtonPressBlocked))return "Close the current panel first.";
        if(click && ui.AnyPanelOpened())return "Close the current game panel first.";
        if(LanSharedChest.Active?.Open==true || LanSharedChest.Active?.Busy==true || LanGift.Active?.Busy==true || LanMonolithParty.Active?.Busy==true)
            return "Close Shared Stash and wait for transactions to finish.";
        return "";
    }
    // Returns true only after issuing a native UI/transition request, allowing
    // LanMod to hide F4. Poll the awaiter even when the UI is closed or suspended.
    internal bool Tick(bool panel,bool otherPanel,bool supported)
    {
        try
        {
            if(travelling && travel.IsCompleted)
            {
                travelling=false;
                var result=travel.GetResult();
                Status=result.IsSuccess?"Arrived at Champions' Gate.":"Travel failed: "+result.Error;
                MelonLoader.MelonLogger.Msg("UI Shortcut: "+Status);
            }
            if(!supported || !OfflineToolkit.Enabled){Suspend();return false;}
            // Release our input ownership before F1/F2 opens another mod UI.
            if(Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.F2)){Suspend();return false;}
            if(openingUntil>0)
            {
                if(UIBase.instance?.AnyPanelOpened()==true)openingUntil=0;
                else if(Time.unscaledTime>=openingUntil)
                {openingUntil=0;Status="No game panel opened. Check native requirements / messages.";}
                else {Block(Application.isFocused);return false;}
            }
            if(otherPanel){Suspend();return false;}
            bool visible=panel && Open;
            Block(visible && Application.isFocused);
            if(!visible){capture=null;pending=null;}
            if(capture.HasValue)
            {
                if(!Application.isFocused || EpochInputManager.instance?.IsInputFieldActive==true)return false;
                if(Input.GetKeyDown(KeyCode.Escape)){capture=null;Status="Hotkey capture cancelled.";return false;}
                if(Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete))
                {bindings.Remove(capture.Value);capture=null;Save();Status="Hotkey cleared.";return false;}
                foreach(var key in keys)if(Input.GetKeyDown(key))
                {
                    if(!UiShortcutRules.Assign(bindings,capture.Value,Binding(key)))
                    {Status="That hotkey is already used.";return false;}
                    capture=null;Save();Status="Hotkey saved.";return false;
                }
                return false;
            }
            bool click=pending.HasValue;var requested=pending;pending=null;
            if(!click && !visible && !otherPanel)
                foreach(var pair in bindings)
                    if(Input.GetKeyDown(pair.Value.Key) && pair.Value.Same(Binding(pair.Value.Key))){requested=pair.Key;break;}
            if(!requested.HasValue || Time.unscaledTime<cooldown)return false;
            var reason=Guard(click,otherPanel);
            if(reason!=""){Status=reason;return false;}
            cooldown=Time.unscaledTime+.5f;
            Block(false);Execute(requested.Value);
            Open=false;Block(openingUntil>0 && Application.isFocused);return true;
        }
        catch(Exception ex)
        {Status="Shortcut failed: "+ex.GetBaseException().Message;MelonLoader.MelonLogger.Warning(Status);return false;}
    }
    private void Execute(ShortcutAction action)
    {
        var ui=UIBase.instance;
        switch(action)
        {
            case ShortcutAction.Telescope:ui.OpenObservatoryPanel(Il2CppLE.Factions.ProphecyRegion.South,true,false);break;
            case ShortcutAction.Mastery:ui.openMasteryPanel(true,true);break;
            case ShortcutAction.MasteryPoints:ui.OpenPassivePanel(true);break;
            case ShortcutAction.PersonalStash:ui.openStash(true,true,true,"",true);break;
            case ShortcutAction.ChampionsGate:
                // Verified in shipped PermaLoad SceneList and ArenaLobby spawn
                // 30618: previousScene=Waypoint, gate=0, TransitionType=Waypoint.
                var scene=SceneList.FindSceneDetails("ArenaLobby");
                if(scene==null || scene.DisplayName!="Champions' Gate" || scene.arenaInformation!=null)
                    throw new InvalidOperationException("Champions' Gate scene metadata does not match this build.");
                var identity=PlayerFinder.getPlayerActor().characterDataTracker.charData.UserIdentity;
                travel=ServiceProvider.OfflineTransitionService.Waypoint(identity,TransitionFadeType.ToBlack,scene.Name,0).GetAwaiter();
                travelling=true;break;
        }
        Status=action==ShortcutAction.ChampionsGate?"Travelling to Champions' Gate...":"Requested "+Label(action)+"; native confirmation and costs apply.";
        if(action!=ShortcutAction.ChampionsGate)openingUntil=Time.unscaledTime+3;
    }
    private static string Label(ShortcutAction action)=>action switch
    {
        ShortcutAction.Telescope=>"Mesembria Telescope",ShortcutAction.Mastery=>"Respecialize Mastery",
        ShortcutAction.MasteryPoints=>"Respecialize Mastery Points",ShortcutAction.ChampionsGate=>"TP to Champions' Gate",_=>"Personal Stash"
    };
    internal void Draw()
    {
        GUI.Box(new Rect(16,16,560,340),"UI Shortcut");
        for(int i=0;i<UiShortcutRules.Actions.Length;i++)
        {
            var action=UiShortcutRules.Actions[i];float y=55+i*40;
            bool enabled=GUI.enabled;GUI.enabled=enabled && !capture.HasValue && !travelling;
            if(GUI.Button(new Rect(30,y,310,30),Label(action)))pending=action;
            GUI.enabled=enabled && !travelling;
            var text=capture==action?"Press a key...":bindings.TryGetValue(action,out var bind)?bind.ToString():"Set hotkey";
            if(GUI.Button(new Rect(350,y,210,30),text)){capture=action;pending=null;Status="Ctrl / Alt / Shift supported. Esc: cancel; Backspace / Delete: clear. F1/F2/F4 reserved.";}
            GUI.enabled=enabled;
        }
        GUI.Label(new Rect(30,258,530,52),Status);
        if(GUI.Button(new Rect(30,318,180,26),"Back to Co-op")){Open=false;capture=pending=null;}
        if(Event.current.type==EventType.MouseDown && new Rect(16,16,560,340).Contains(Event.current.mousePosition))Event.current.Use();
    }
}
