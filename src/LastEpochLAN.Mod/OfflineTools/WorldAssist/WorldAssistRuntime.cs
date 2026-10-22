using Il2Cpp;
using Il2CppDMM;
using UnityEngine;
using UObj=UnityEngine.Object;
using IconManager=Il2CppLE.UI.Minimap.MinimapIconManager;
using NVec=System.Numerics.Vector3;

namespace LastEpochLAN.Mod.OfflineTools.WorldAssist;

internal static partial class WorldAssistRuntime
{
    private sealed class Motion
    {
        internal Vector3 Position,Velocity;
        internal double Time=-1;
        internal void Sample(Vector3 position,double now)
        {
            if(!AssistRules.Finite(N(position)) || Time>=0 && now-Time<.001)return;
            Velocity=Time>=0?U(AssistRules.Velocity(N(Position),N(position),now-Time)):Vector3.zero;
            Position=position;Time=now;
        }
        internal Vector3 Render(double now)=>U(AssistRules.Predict(N(Position),N(Velocity),Time,now,Options.Smooth));
    }
    private sealed class Target
    {
        internal int Id;
        internal Transform Transform,PathTransform;
        internal TargetKind Kind;
        internal string Name;
        internal Func<bool> IsCompleted;
        internal bool Completed;
        internal readonly Motion Motion=new();
        internal GameObject Anchor;
        internal DMMapIcon Icon;
        internal int IconKind=-1;
    }
    private static readonly List<Target> targets=new();
    private static readonly Dictionary<int,Target> byId=new();
    private static float nextDiscover,nextScan;
    private static int selected;
    private static int actorId;
    private static bool wasReady;
    private static string lastError="";
    private static float nextWarning;
    internal static object Diagnostics=>new { enabled=Options.Enabled,targets=targets.Count,registeredAbilities=hazards.Count,hostileAbilities=visibleHazards.Count,
        drawnHazards=Math.Min(64,visibleHazards.Count),radiusOverrides=radii.Count,invalidRadii=badRadii,selected,pathStatus,partialPath,pathCorners=path.Length,
        mapProjectionFrame=mapFrame,currentFrame=Time.frameCount,lastError,
        targetKinds=Enumerable.Range(0,14).Select(i=>new{kind=AssistRules.Labels[i],count=targets.Count(t=>(int)t.Kind==i)}).ToArray() };
    internal static string Status=> $"{targets.Count} targets | {hazards.Count} abilities | {pathStatus}";
    internal static WorldAssistSettings Options=>OfflineToolkit.Settings.WorldAssist;
    internal static NVec N(Vector3 p)=>new(p.x,p.y,p.z);
    internal static Vector3 U(NVec p)=>new(p.X,p.Y,p.Z);
    internal static void Guard(Action action)
    {
        try{action();}catch(Exception ex)
        {
            string message=ex.GetBaseException().Message;
            if(lastError!=message && Time.unscaledTime>=nextWarning){MelonLoader.MelonLogger.Warning("World assist: "+message);lastError=message;nextWarning=Time.unscaledTime+5;}
        }
    }
    internal static void Tick()
    {
        bool ready=OfflineToolkit.Ready && Options.Enabled;
        if(!ready){if(wasReady)Clear();return;}
        var actor=PlayerFinder.getPlayerActor();
        if(actor==null)return;
        int id=actor.GetInstanceID();
        if(actorId!=0 && actorId!=id)Clear();
        wasReady=true;actorId=id;
        if(Time.unscaledTime>=nextDiscover)
        {
            nextDiscover=Time.unscaledTime+1;
            Discover();
        }
        UpdateTargets();UpdateHazards();UpdatePath();EnsureMapProbes();
    }
    private static bool Live(Component source)=>source!=null && source.gameObject!=null && source.gameObject.activeInHierarchy && source.gameObject.scene.IsValid() && source.gameObject.scene.isLoaded;
    private static string HierarchyName(Transform t)
    {
        string result="";
        for(int i=0;i<4 && t!=null;i++,t=t.parent)result+=" "+t.name;
        return result;
    }
    private static void Discover()
    {
        if(Options.Targets)
        {
            var listeners=WorldObjectClickListener.allListener;
            if(listeners!=null) for(int i=0;i<Math.Min(listeners.Count,4096);i++)
            {
                var listener=listeners[i];if(!Live(listener))continue;
                var hover=listener.hoverTagOnMouseOver;
                string text=HierarchyName(listener.transform);
                bool transition=false;
                if(hover!=null)
                {
                    text+=" "+hover.hoverText+" "+hover.localizationKey;
                    transition=hover.sceneInteraction!=null || hover.dungeonTransitionInteraction!=null;
                    if(!transition && (hover.hoverTargetType==HoverTagOnMouseOver.HoverTargetType.NPC || hover.hoverTargetType==HoverTagOnMouseOver.HoverTargetType.SIGNPOST))continue;
                }
                var kind=AssistRules.Classify(text,transition);
                AddTarget(listener.transform,kind,hover?.hoverText,null,listener.useOtherTransformForPathfinding?listener.transformForPathfinding:null);
            }
            var eggs=TimeBeastEggsVisuals.all;
            if(eggs!=null)for(int i=0;i<eggs.Count;i++){var e=eggs[i];if(Live(e))AddTarget(e.transform,TargetKind.RiftBeastNest,null,()=>e==null || e.currentVisualState!=TimeBeastEggs.VisualState.Interactable);}
            var prisons=RunePrisonVisuals.all;
            if(prisons!=null)for(int i=0;i<prisons.Count;i++){var p=prisons[i];if(Live(p))AddTarget(p.transform,TargetKind.RunePrison,null,()=>p==null || p.triggered);}
            var rifts=TimeBeastRiftVisuals.all;
            if(rifts!=null)for(int i=0;i<rifts.Count;i++){var r=rifts[i];if(Live(r))AddTarget(r.transform,TargetKind.TimeRift,null,()=>r==null || r.triggered);}
            var caches=OneShotCacheGameplayObject.all;
            if(caches!=null)for(int i=0;i<caches.Count;i++){var c=caches[i];if(Live(c))AddTarget(c.transform,TargetKind.Cache,null,()=>c==null || c.actorsOpenedFor?.Contains(PlayerFinder.getPlayerActor())==true);}
            var encounters=SynclessRandomEncounter.allInstances;
            if(encounters!=null)for(int i=0;i<encounters.Count;i++)
            {
                var e=encounters[i];if(!Live(e))continue;
                var nemesis=e.TryCast<NemesisEncounterGameplayObject>();
                if(nemesis!=null)AddTarget(e.transform,TargetKind.Nemesis,null,()=>nemesis==null || nemesis.hasEverHadActiveEncounter && !nemesis.HasActiveEncounter);
            }
            // Native icons also cover quest conditions and transitions without a click listener.
            var icons=IconManager.Instance?.icons;
            if(icons!=null)for(int i=0;i<Math.Min(icons.Count,4096);i++)
            {
                var icon=icons[i]?.icon;if(!Live(icon) || icon.name.StartsWith("TM ",StringComparison.Ordinal))continue;
                string text=HierarchyName(icon.transform)+" "+icon.sprite?.name;
                var tint=icon.tint;var main=SetMapIconColourFromObjectiveCondition.mainQuestColour;var side=SetMapIconColourFromObjectiveCondition.sideQuestColour;
                bool Same(Color32 a,Color32 b)=>a.a>0 && b.a>0 && a.r==b.r && a.g==b.g && a.b==b.b;
                bool quest=Same(tint,main) || Same(tint,side) || icon.GetComponentInParent<SetMapIconColourFromObjectiveCondition>()!=null;
                var kind=AssistRules.Classify(text,quest:quest);
                if(kind!=TargetKind.Other)AddTarget(icon.transform,kind,null,null);
            }
        }
        // Slow bootstrap finds already-enabled components, including objects born
        // before our scene callback. Never search the scene from a render callback.
        if(Time.unscaledTime<nextScan)return;
        nextScan=Time.unscaledTime+4;
        if(Options.DangerAreas)
            foreach(var sync in UObj.FindObjectsOfType<AbilitySync>())Track(sync);
        if(!Options.Targets)return;
        foreach(var s in UObj.FindObjectsOfType<ShrineSync>())if(Live(s))AddTarget(s.ShrineObject!=null?s.ShrineObject.transform:s.transform,TargetKind.Shrine,null,()=>s==null || s.IsDestroyed || s.NetObject?.IsActivated==true);
        foreach(var c in UObj.FindObjectsOfType<ChestSync>())if(Live(c))
        {
            var t=c.chestObject!=null?c.chestObject.transform:c.transform;
            var kind=AssistRules.Classify("chest "+HierarchyName(t));
            AddTarget(t,kind,null,()=>c==null || c.IsDestroyed || c.NetObject?.IsOpened==true);
        }
        foreach(var o in UObj.FindObjectsOfType<ObserverWindowSync>())if(Live(o))AddTarget(o.visuals!=null?o.visuals.transform:o.transform,TargetKind.OmenWindow,null,()=>o==null || o.IsDestroyed || o.state>=ObserverWindowLogic.State.Finished);
        foreach(var t in UObj.FindObjectsOfType<OpenTombEntrance>())if(Live(t))AddTarget(t.transform,TargetKind.TombEntrance,null,null);
        foreach(var hover in UObj.FindObjectsOfType<HoverTagOnMouseOver>())
        {
            if(!Live(hover) || hover.hoverTargetType==HoverTagOnMouseOver.HoverTargetType.NPC || hover.hoverTargetType==HoverTagOnMouseOver.HoverTargetType.SIGNPOST)continue;
            bool transition=hover.sceneInteraction!=null || hover.dungeonTransitionInteraction!=null;
            var kind=AssistRules.Classify(HierarchyName(hover.transform)+" "+hover.hoverText+" "+hover.localizationKey,transition);
            AddTarget(hover.transform,kind,hover.hoverText,null);
        }
    }
    private static void AddTarget(Transform transform,TargetKind kind,string name,Func<bool> completed,Transform pathTransform=null)
    {
        if(transform==null || targets.Count>=1024)return;
        int id=transform.GetInstanceID();
        if(byId.TryGetValue(id,out var existing))
        {
            if(pathTransform!=null)existing.PathTransform=pathTransform;
            // More authoritative typed discovery replaces a text classification.
            if(completed!=null){existing.IsCompleted=completed;existing.Kind=kind;}
            else if(existing.Kind==TargetKind.Other && kind!=TargetKind.Other)existing.Kind=kind;
            return;
        }
        foreach(var e in targets)
            if(e.Transform!=null && (e.Kind==kind || e.Kind==TargetKind.Other || kind==TargetKind.Other) && (e.Transform.position-transform.position).sqrMagnitude<.25f)
            {if(pathTransform!=null)e.PathTransform=pathTransform;if(completed!=null){e.IsCompleted=completed;e.Kind=kind;}else if(e.Kind==TargetKind.Other)e.Kind=kind;return;}
        var target=new Target{Id=id,Transform=transform,PathTransform=pathTransform,Kind=kind,Name=string.IsNullOrWhiteSpace(name)?AssistRules.Labels[(int)kind]:name.Replace("\n"," "),IsCompleted=completed};
        target.Motion.Sample(transform.position,Time.realtimeSinceStartupAsDouble);
        targets.Add(target);byId[id]=target;
    }
    private static void UpdateTargets()
    {
        var player=PlayerFinder.getLocalActorSync()?.transform;
        if(player==null)return;
        bool overlay=UIBase.instance?.overlayMapOpen==true;
        for(int i=targets.Count-1;i>=0;i--)
        {
            var t=targets[i];
            if(!Live(t.Transform)){RemoveTarget(i);continue;}
            t.Completed=t.IsCompleted?.Invoke()==true;
            var style=Options.Styles[(int)t.Kind];
            bool shown=Options.Targets && (!Options.HideCompleted || !t.Completed) && (t.Transform.position-player.position).sqrMagnitude<=Options.Range*Options.Range;
            bool map=shown && (overlay?style.Map:style.Minimap);
            if(!map){if(t.Anchor!=null)t.Anchor.SetActive(false);continue;}
            EnsureSprites();
            if(t.Icon==null)
            {
                t.Anchor=new GameObject("TM World target "+t.Kind);t.Anchor.SetActive(false);
                t.Icon=t.Anchor.AddComponent<DMMapIcon>();ConfigureIcon(t.Icon,sprites[(int)t.Kind]);
            }
            t.Anchor.transform.position=t.Motion.Render(Time.realtimeSinceStartupAsDouble);
            t.Anchor.transform.localScale=Vector3.one*(overlay?1f:.65f);
            t.Icon.tint=Colour(style.Colour,t.Completed ? .3f : 1f);
            t.Icon.sprite=sprites[(int)t.Kind];t.Icon.icon=sprites[(int)t.Kind];
            if(t.Icon.img!=null)t.Icon.img.raycastTarget=false;
            if(!t.Anchor.activeSelf)t.Anchor.SetActive(true);
            var manager=IconManager.Instance;
            if(manager!=null && !manager.Exists(t.Icon))manager.Add(t.Icon,sprites[(int)t.Kind]);
            if(t.IconKind!=(int)t.Kind){t.Icon.ChangeIcon(sprites[(int)t.Kind]);t.IconKind=(int)t.Kind;}
        }
    }
    private static void ConfigureIcon(DMMapIcon icon,Sprite sprite)
    {
        icon.sprite=sprite;icon.icon=sprite;icon.tint=new Color32(255,255,255,255);
        icon.scaleWithZoom=false;icon.overrideGlobalScaleMode=true;icon.ignoreGlobalScale=true;
        icon.rotate=false;icon.rotateWithMap=false;icon.useDirectionIndicator=false;icon.useCompositeDirectionIndicator=false;
        icon.WorldScale=false;icon.ApplyScaling=true;icon.SinScale=false;
    }
    internal static void LateUpdate()
    {
        if(!wasReady || !Options.Enabled || !OfflineToolkit.Ready)return;
        double now=Time.realtimeSinceStartupAsDouble;
        var player=PlayerFinder.getLocalActorSync()?.transform;
        if(player!=null)playerMotion.Sample(player.position,now);
        foreach(var t in targets)if(t.Transform!=null)
        {
            t.Motion.Sample(t.Transform.position,now);
            if(t.Anchor!=null && t.Anchor.activeSelf)t.Anchor.transform.position=t.Motion.Render(now);
        }
        foreach(var h in hazards.Values)if(h.Sync!=null && h.Sync.transform!=null)h.Motion.Sample((h.Sync.abilityVisuals!=null?h.Sync.abilityVisuals.transform:h.Sync.transform).position,now);
        ValidatePathHead();
    }
    private static void RemoveTarget(int index)
    {
        var t=targets[index];byId.Remove(t.Id);
        if(t.Anchor!=null){t.Anchor.SetActive(false);UObj.Destroy(t.Anchor);}
        if(selected==t.Id){selected=0;ClearPath();}
        targets.RemoveAt(index);
    }
    internal static void Clear()
    {
        for(int i=targets.Count-1;i>=0;i--)RemoveTarget(i);
        hazards.Clear();visibleHazards.Clear();ClearPath();ClearMapProbes();
        playerMotion.Time=-1;wasReady=false;actorId=0;selected=0;nextDiscover=nextScan=0;
    }
    internal static void Dispose()
    {
        Clear();
        foreach(var sprite in sprites)if(sprite!=null)UObj.Destroy(sprite);
        Array.Clear(sprites,0,sprites.Length);
        if(atlas!=null)UObj.Destroy(atlas);atlas=null;
    }
}
