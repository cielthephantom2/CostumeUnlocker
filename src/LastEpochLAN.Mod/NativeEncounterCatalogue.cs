using Il2Cpp;
using LastEpochLAN.Core;
using LastEpochLAN.Core.Encounters;
using UnityEngine;

namespace LastEpochLAN.Mod;

// Development-only discovery. No native callbacks are invoked or suppressed,
// and no component is disabled on either side. Inactive without the explicit
// development switch and a ready co-op room. This is not a gameplay adapter.
internal sealed class NativeEncounterCatalogue
{
    private static readonly bool Enabled=Environment.GetCommandLineArgs().Contains("--lelan-encounter-development");
    private readonly Dictionary<IntPtr,Guid> ids=new();
    private readonly List<object> entries=new();
    private LanSession room;
    private string scene="",run="";
    private float nextRead;
    private long epoch,reads;
    internal string LastError {get;private set;}="";
    internal object Diagnostics=>new {enabled=Enabled,gameplayEnabled=false,epoch,reads,scene,run,LastError,entries=entries.ToArray()};
    internal void Tick(LanSession next,string currentScene,bool ready)
    {
        if(!Enabled)return;
        if(next?.Connected!=true || !ready){Reset();return;}
        var local=next.PlayerPositions.FirstOrDefault(p=>p.Id==next.LocalId);
        if(local==null || !local.Available || !local.Cooperative){Reset();return;}
        if(room!=next || scene!=currentScene || run!=local.EchoRunId)
        {Reset();room=next;scene=currentScene;run=local.EchoRunId;epoch++;}
        if(Time.unscaledTime<nextRead)return;nextRead=Time.unscaledTime+2;
        reads++;entries.Clear();var seen=new HashSet<IntPtr>();
        try
        {
            foreach(var value in UnityEngine.Object.FindObjectsOfType<NemesisEncounterGameplayObject>())
                Add(value,EncounterKind.Nemesis,value.HasActiveEncounter?"Active":"Available",null,seen);
            foreach(var value in UnityEngine.Object.FindObjectsOfType<TimeBeastRift>())
                Add(value,EncounterKind.RiftBeast,value.HasBeenTriggered?"Opened":"Available",null,seen);
            foreach(var value in UnityEngine.Object.FindObjectsOfType<ObserverWindowLogic>())
                Add(value,EncounterKind.OmenWindow,value.state.ToString(),Area(value.transform.position,value.radius,value.anyPlayerInside),seen);
            foreach(var value in UnityEngine.Object.FindObjectsOfType<RageOfMorditasLogic>())
                Add(value,EncounterKind.BloodCrystal,value.GetState().ToString(),
                    value._encounterTypeConfig==null?null:Area(value._gauntletLocation,value._encounterTypeConfig.GauntletAreaRadius,!value._gauntletNoPlayerInRange),seen);
            foreach(var gone in ids.Keys.Where(p=>!seen.Contains(p)).ToArray())ids.Remove(gone);
            LastError="";
        }
        catch(Exception ex){LastError=ex.ToString();}
    }
    private static EncounterArea Area(Vector3 position,float radius,bool active)
    {
        var area=new EncounterArea(Point(position),radius,active);return area.IsValid()?area:null;
    }
    private void Add(Component component,EncounterKind kind,string state,EncounterArea area,HashSet<IntPtr> seen)
    {
        if(component==null || !component.gameObject.scene.IsValid() || component.gameObject.scene.name!=scene || entries.Count>=EncounterHost.MaximumEntities)return;
        var position=Point(component.transform.position);if(!position.IsValid())return;
        seen.Add(component.Pointer);
        if(!ids.TryGetValue(component.Pointer,out var id))ids.Add(component.Pointer,id=Guid.NewGuid());
        entries.Add(new {id,kind=kind.ToString(),position,state,area});
    }
    private static VisualPoint Point(Vector3 point)=>new(){X=point.x,Y=point.y,Z=point.z};
    internal void Reset(){room=null;ids.Clear();entries.Clear();scene=run="";nextRead=0;}
}
