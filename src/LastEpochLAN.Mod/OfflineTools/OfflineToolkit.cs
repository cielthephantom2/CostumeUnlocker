using System.Text.Json;
using UnityEngine;
using Il2Cpp;

namespace LastEpochLAN.Mod.OfflineTools;

internal sealed class ToolkitSettings
{
    public bool AutoCompleteNewCharacter {get;set;}=true;
    public bool UnlockCosmetics {get;set;}=true;
    public bool Gold {get;set;}=true;
    public bool Keys {get;set;}=true;
    public bool Potions {get;set;}=true;
    public bool Materials {get;set;}=true;
    public bool Bones {get;set;}=true;
    public bool RangePickup {get;set;}=true;
    public bool RevealObjectives {get;set;}=true;
    public bool EnemyMapDots {get;set;}=true;
    public bool RemoveFog {get;set;}=true;
    public float UnexploredOpacity {get;set;}=0.2f;
    public bool WideMinimapZoom {get;set;}=true;
    public bool CustomZoom {get;set;}=true;
    public float ZoomDistance {get;set;}=1f;
    public bool DpsVisible {get;set;}=true;
    public float DpsOffsetX {get;set;}
    public float DpsBottomOffset {get;set;}
    public float DpsTopOffset {get;set;}
    public int DpsPositionVersion {get;set;}
    public float DpsScale {get;set;}=1f;
}

internal static class OfflineToolkit
{
    internal static ToolkitSettings Settings=new();
    internal static bool Enabled=>!Il2CppEHG.Multiplayer.GameplayEnvironment.IsOnlinePlay;
    internal static bool Ready=>Enabled && PlayerFinder.getPlayerActor()?.characterDataTracker?.charData!=null &&
        PlayerFinder.getLocalActorSync()?.actorVisuals?.HasBeenInitialized==true;
    internal static string Error="";
    internal static bool PanelOpen;
    private static float saveAt=-1;
    private static readonly string path=Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","LastEpochLAN.tools.json");
    internal static void Initialize()
    {
        try
        {
            if(File.Exists(path)) Settings=JsonSerializer.Deserialize<ToolkitSettings>(File.ReadAllText(path)) ?? new();
            if(Settings.DpsPositionVersion<1)
            {
                Settings.DpsOffsetX=0;Settings.DpsBottomOffset=0;Settings.DpsTopOffset=0;
                Settings.DpsPositionVersion=1;saveAt=0;
            }
            NewCharacterBoost.Load();
        }
        catch(Exception ex) {Error=ex.GetBaseException().Message;MelonLoader.MelonLogger.Warning("Offline tools settings: "+Error);}
    }
    internal static void ScheduleSave()=>saveAt=Time.unscaledTime+0.5f;
    private static void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path+".tmp",JsonSerializer.Serialize(Settings,new JsonSerializerOptions {WriteIndented=true}));
        File.Move(path+".tmp",path,true);saveAt=-1;
    }
    private static void Safely(Action action)
    {try {action();}catch(Exception ex) {var error=ex.GetBaseException().Message;if(error!=Error) MelonLoader.MelonLogger.Warning("Offline tools: "+error);Error=error;}}
    internal static void Tick(bool panel)
    {
        Safely(OfflineOnly.Tick);
        Safely(NewCharacterBoost.Tick);
        Safely(OfflineBuffs.Tick);
        Safely(OfflineFortune.Tick);
        Safely(OfflineWovenRanks.Tick);
        Safely(OfflinePickup.Tick);
        Safely(OfflineForgottenKnights.Tick);
        Safely(Dps.DpsRuntime.Tick);
        Safely(()=>CameraZoom.InputTick(panel));
        Safely(CameraZoom.ApplyCurrent);
        Safely(MapFog.Tick);
        Safely(EnemyMapDots.Tick);
        Safely(ObjectiveReveal.Tick);
        if(saveAt>=0 && Time.unscaledTime>=saveAt) Safely(Save);
    }
    internal static void SceneChanged() {OfflinePickup.Clear();NewCharacterBoost.SceneChanged();MapFog.Clear();EnemyMapDots.Clear();CameraZoom.Restore();ObjectiveReveal.Clear();Dps.DpsRuntime.SceneChanged();}
    internal static void Dispose() {Safely(OfflinePickup.Clear);SceneChanged();Safely(OfflineBuffs.Restore);Safely(OfflineFortune.RestoreRewards);EnemyMapDots.Dispose();MinimapZoom.Clear();if(saveAt>=0) Safely(Save);}
    internal static void Draw()
    {
        float x=16,y=16;
        GUI.Box(new Rect(x,y,425,610),"Mod Settings");
        GUI.Box(new Rect(x+449,y,310,150),"DPS Tracker (offline local damage)");
        GUI.Label(new Rect(x+463,y+27,282,22),$"HUD scale: {Dps.DpsSettings.damageMeterScale:0.00}x");
        Dps.DpsSettings.damageMeterScale=GUI.HorizontalSlider(new Rect(x+463,y+55,282,20),Dps.DpsSettings.damageMeterScale,0.5f,2);
        if(GUI.Button(new Rect(x+463,y+84,132,25),"Reset DPS")) Dps.DamageMeterOverlay.ResetCombatData();
        if(GUI.Button(new Rect(x+606,y+84,140,25),"Reset position"))
        {Settings.DpsOffsetX=0;Settings.DpsBottomOffset=0;Settings.DpsTopOffset=0;Settings.DpsPositionVersion=1;Settings.DpsScale=1;ScheduleSave();}
        if(GUI.Button(new Rect(x+463,y+117,132,25),Dps.DamageMeterOverlay.IsPaused?"Resume DPS":"Pause DPS")) Dps.DamageMeterOverlay.TogglePause();
        if(GUI.Button(new Rect(x+606,y+117,140,25),Settings.DpsVisible?"Hide DPS":"Show DPS"))
        {Settings.DpsVisible=!Settings.DpsVisible;ScheduleSave();}
        bool Toggle(bool value,string label,int row)=>GUI.Toggle(new Rect(x+14,y+35+row*25,395,23),value,label);
        var s=Settings;
        bool previousChanged=GUI.changed;GUI.changed=false;
        s.AutoCompleteNewCharacter=Toggle(s.AutoCompleteNewCharacter,"New character: quests + level 58 + End of Time",0);
        GUI.Label(new Rect(x+14,y+62,395,23),"Offline mode enforced; existing characters kept.");
        s.UnlockCosmetics=Toggle(s.UnlockCosmetics,"Unlock native cosmetic selection",2);
        s.Gold=Toggle(s.Gold,"Auto Gold: radius 5, including while standing",3);
        s.Keys=Toggle(s.Keys,"Auto Keys: radius 5, including while standing",4);
        s.Potions=Toggle(s.Potions,"Auto Potions: radius 5, charges have room",5);
        s.Materials=Toggle(s.Materials,"Auto Materials: radius 5, including while standing",6);
        s.Bones=Toggle(s.Bones,"Auto Bones: radius 5, including while standing",7);
        s.RangePickup=Toggle(s.RangePickup,"Range Pickup: click loot without walking to it",8);
        s.RevealObjectives=Toggle(s.RevealObjectives,"Reveal Monolith / Dungeon objectives",9);
        s.EnemyMapDots=Toggle(s.EnemyMapDots,"Enemy minimap dots",10);
        s.RemoveFog=Toggle(s.RemoveFog,"Reveal unexplored map (gray)",11);
        s.WideMinimapZoom=Toggle(s.WideMinimapZoom,"Extended minimap +/-",12);
        s.CustomZoom=Toggle(s.CustomZoom,"Camera zoom: comma / period",13);
        GUI.Label(new Rect(x+14,y+389,395,23),$"Camera zoom: {s.ZoomDistance:0.0}x (0.5–5)");
        s.ZoomDistance=GUI.HorizontalSlider(new Rect(x+14,y+417,395,20),CameraZoom.Clamp(s.ZoomDistance),0.5f,5f);
        if(GUI.changed) {ScheduleSave();MinimapZoom.ApplyCurrent();}
        GUI.changed|=previousChanged;
        GUI.Label(new Rect(x+14,y+446,395,48),NewCharacterBoost.Status);
        GUI.Label(new Rect(x+14,y+494,395,42),OfflineDensity.Status);
        if(Error!="") GUI.Label(new Rect(x+14,y+539,395,34),Error);
        if(GUI.Button(new Rect(x+14,y+575,240,25),"Back to Co-op")) PanelOpen=false;
        if(!string.IsNullOrEmpty(GUI.tooltip)) GUI.Label(new Rect(x+449,y+395,310,60),GUI.tooltip);
    }
}
