using LastEpochLAN.Core;
using UnityEngine;

namespace LastEpochLAN.Mod;

internal sealed class NativeSummonCapture
{
    internal sealed record Snapshot(string Epoch,double Time,SummonState[] States);
    private static NativeSummonCapture active;
    private readonly Dictionary<int,(IntPtr Pointer,int Id)> ids=new();
    private readonly HashSet<IntPtr> owned=new();
    private readonly Dictionary<IntPtr,(long Sequence,byte Animation,float Speed,float Until,int Frame)> attacks=new();
    private LanSession room;
    private IntPtr ownerPointer;
    private string scene="";
    private int sceneHandle,nextId;
    private long nextAttack;
    private float nextCapture;
    private Snapshot snapshot=new("",0,Array.Empty<SummonState>());
    internal string Error="";
    internal int Omitted;
    internal int Count=>snapshot.States.Length;
    internal Snapshot Capture(LanSession session,Il2Cpp.Actor owner,string zone)
    {
        int handle=UnityEngine.SceneManagement.SceneManager.GetSceneByName(zone).handle;
        if(room!=session || ownerPointer!=owner.Pointer || scene!=zone || sceneHandle!=handle)
        {
            Reset();room=session;ownerPointer=owner.Pointer;scene=zone;sceneHandle=handle;
            snapshot=new(Guid.NewGuid().ToString("N"),0,Array.Empty<SummonState>());
        }
        active=this;
        if(Time.unscaledTime<nextCapture)return snapshot;
        nextCapture=Time.unscaledTime+0.1f;
        try
        {
            var actors=new List<Il2Cpp.Actor>();
            if(owner.TryGetExistingSummonTracker(out var tracker) && tracker!=null)
            {
                var all=tracker.getMinionsAndAllSubMinions();
                if(all!=null)foreach(var summon in all)
                    if(summon?.actor is {} actor && actor.Pointer!=owner.Pointer && actor.data!=null &&
                        actor.gameObject.activeInHierarchy && actor.gameObject.scene.isLoaded)actors.Add(actor);
            }
            var unique=actors.GroupBy(a=>a.GetInstanceID()).Select(g=>g.First()).OrderBy(a=>a.GetInstanceID()).ToArray();
            var present=unique.Select(a=>a.GetInstanceID()).ToHashSet();
            foreach(var id in ids.Keys.Where(id=>!present.Contains(id)).ToArray())ids.Remove(id);
            owned.Clear();owned.UnionWith(unique.Select(a=>a.Pointer));
            foreach(var pointer in attacks.Keys.Where(p=>!owned.Contains(p)).ToArray())attacks.Remove(pointer);
            Omitted=Math.Max(0,unique.Length-SummonState.MaximumPerPlayer);
            var states=new List<SummonState>();
            foreach(var actor in unique.Take(SummonState.MaximumPerPlayer))
            {
                try
                {
                    var visual=actor.GetActorSync()?.actorVisuals;
                    var prefab=visual?.visualsSoftRef;
                    if(prefab==null || !prefab)prefab=actor.data.GetReferenceForVisuals(0);
                    if(prefab==null || !prefab)continue;
                    int nativeId=actor.GetInstanceID();
                    if(!ids.TryGetValue(nativeId,out var identity) || identity.Pointer!=actor.Pointer)
                        ids[nativeId]=identity=(actor.Pointer,++nextId);
                    var transform=visual!=null ? visual.transform : actor.transform;
                    var motion=NativePlayerAnimationReplica.Capture(actor);
                    var animation=motion==null ? NativeAnimation.Capture(actor,true,24) : null;
                    attacks.TryGetValue(actor.Pointer,out var attack);
                    bool attacking=Time.unscaledTime<attack.Until;
                    var value=new SummonState {Id=identity.Id,Template=actor.data.name,
                        Prefab=new() {P0=prefab.Guid.P0,P1=prefab.Guid.P1,SubPath=prefab.SubPath??""},
                        Position=NativeSkillVisualCapture.Point(transform.position),Yaw=transform.eulerAngles.y,
                        Scale=NativeSkillVisualCapture.Point(transform.lossyScale),
                        Visible=visual==null || visual.gameObject.activeInHierarchy && !visual.renderersCurrentlyDisabledAwaitingEmerge,
                        Motion=motion,Animation=animation,AttackSequence=attacking ? attack.Sequence : 0,
                        AttackAnimation=attacking ? attack.Animation : (byte)0,AttackSpeed=attacking ? attack.Speed : 1};
                    if(value.IsValid())states.Add(value);
                }
                catch(Exception ex){Error="Summon capture: "+ex.GetBaseException().Message;}
            }
            snapshot=new(snapshot.Epoch,LanSession.MonotonicSeconds,states.ToArray());
        }
        catch(Exception ex){Error="Summon registry: "+ex.GetBaseException().Message;}
        return snapshot;
    }
    internal static void RecordAttack(Il2Cpp.Actor actor,byte animation,float speed)
    {
        var capture=active;
        if(capture==null || actor==null || !capture.owned.Contains(actor.Pointer) || animation>63 ||
            !float.IsFinite(speed) || speed is <=0 or >100)return;
        if(capture.attacks.TryGetValue(actor.Pointer,out var old) && old.Frame==Time.frameCount && old.Animation==animation && old.Speed==speed)return;
        capture.attacks[actor.Pointer]=(++capture.nextAttack,animation,speed,Time.unscaledTime+0.8f/speed,Time.frameCount);
    }
    internal void Reset()
    {
        if(active==this)active=null;
        ids.Clear();owned.Clear();attacks.Clear();room=null;ownerPointer=IntPtr.Zero;scene="";sceneHandle=0;
        nextId=0;nextAttack=0;nextCapture=0;Omitted=0;Error="";snapshot=new("",0,Array.Empty<SummonState>());
    }
}
