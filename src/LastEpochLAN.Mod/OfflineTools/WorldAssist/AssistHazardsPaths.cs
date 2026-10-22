using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using UnityEngine.AI;
using IconManager=Il2CppLE.UI.Minimap.MinimapIconManager;
using NVec=System.Numerics.Vector3;

namespace LastEpochLAN.Mod.OfflineTools.WorldAssist;

internal static partial class WorldAssistRuntime
{
    private sealed class Hazard
    {
        internal AbilitySync Sync;
        internal readonly Motion Motion=new();
        internal string Name="";
        internal bool Enemy;
        internal HazardSpec Spec;
        internal Vector3 Direction;
    }
    private static readonly Dictionary<int,Hazard> hazards=new();
    private static readonly List<int> deadHazards=new();
    private static readonly List<Hazard> visibleHazards=new();
    private static Dictionary<string,HazardSpec> radii=new(StringComparer.OrdinalIgnoreCase);
    private static string radiusText;
    private static int badRadii;
    private static readonly Motion playerMotion=new();
    private static NVec[] path=Array.Empty<NVec>();
    private static NavMeshPath nativePath;
    private static int pathTarget;
    private static float nextPath;
    private static string pathStatus="no route";
    private static bool partialPath;
    private static Vector3 pathGoal;
    private static bool pathHeadClear;
    private static int pathTail;
    private static void ValidatePathHead()
    {
        pathHeadClear=false;
        if(path.Length<2)return;
        pathTail=AssistRules.TailStart(N(playerMotion.Position),path);
        if(pathTail<=0 || pathTail>=path.Length)return;
        pathHeadClear=!NavMesh.Raycast(playerMotion.Position,U(path[pathTail]),out _,NavMesh.AllAreas);
    }
    internal static void Track(AbilitySync sync)
    {
        if(!OfflineToolkit.Enabled || !Options.Enabled || !Options.DangerAreas || !Live(sync) || hazards.Count>=512)return;
        int id=sync.GetInstanceID();
        if(!hazards.ContainsKey(id))
        {var h=new Hazard{Sync=sync};h.Motion.Sample(sync.transform.position,Time.realtimeSinceStartupAsDouble);hazards[id]=h;}
    }
    private static bool IsEnemy(CreationReferences refs)
    {
        var local=PlayerFinder.getPlayerActor();
        var localSync=PlayerFinder.getLocalActorSync();
        var alignment=local?.alignmentManager?.alignment;
        if(refs==null || local==null || alignment==null)return false;
        // Follow bounded summon ownership as well as direct local-player casts.
        // Unknown ownership is not advertised as an enemy hitbox.
        var creator=refs.creator;
        var sync=refs.syncCreator;
        for(int i=0;i<8;i++)
        {
            if(creator!=null && creator.Pointer==local.Pointer)return false;
            if(sync!=null && (sync.IsLocalPlayer || localSync!=null && sync.Pointer==localSync.Pointer))return false;
            if(creator?.isPlayerActor==true || sync?.actorVisuals?.isPlayer==true)return false;
            var parent=creator?.creationReferences ?? sync?.creationReferences;
            if(parent==null)break;
            var nextActor=parent.creator;var nextSync=parent.syncCreator;
            if((nextActor==null || creator!=null && nextActor.Pointer==creator.Pointer) && (nextSync==null || sync!=null && nextSync.Pointer==sync.Pointer))break;
            creator=nextActor;sync=nextSync;
        }
        var source=creator?.alignmentManager?.alignment ?? sync?.actorVisuals?.alignment;
        return source!=null && source.IsFoe(alignment);
    }
    private static void UpdateHazards()
    {
        visibleHazards.Clear();
        if(!Options.DangerAreas){hazards.Clear();return;}
        if(radiusText!=Options.AbilityRadii)
        {radiusText=Options.AbilityRadii;radii=AssistRules.ParseRadii(radiusText,out badRadii);}
        deadHazards.Clear();
        var player=PlayerFinder.getLocalActorSync()?.transform;
        foreach(var pair in hazards)
        {
            var h=pair.Value;var sync=h.Sync;
            if(!Live(sync) || sync.IsDestroyed || sync.queuedForDestruction){deadHazards.Add(pair.Key);continue;}
            h.Enemy=false;
            if(player==null || (sync.transform.position-player.position).sqrMagnitude>3600)continue;
            var refs=sync.creationReferences ?? sync.abilityVisuals?.creationReferences;
            if(!IsEnemy(refs))continue;
            h.Enemy=true;
            h.Name=sync.abilityInfoClientSide?.abilityName;
            if(string.IsNullOrWhiteSpace(h.Name))h.Name=refs?.thisAbility?.abilityName;
            if(string.IsNullOrWhiteSpace(h.Name))h.Name=sync.debugAbilityName;
            if(string.IsNullOrWhiteSpace(h.Name))h.Name=sync.name;
            h.Name=h.Name.Trim();
            Vector3 velocity=sync.abilityVisuals!=null?sync.abilityVisuals.velocity:sync.velocity;
            if(velocity.sqrMagnitude<.01f)velocity=h.Motion.Velocity;
            bool moving=velocity.sqrMagnitude>.25f;
            if(!radii.TryGetValue(h.Name,out var spec))
                spec=moving?new(HazardShape.Projectile,Options.ProjectileRadius,0,Options.ProjectileLength):new(HazardShape.Ring,Options.AreaRadius);
            h.Spec=spec;
            h.Direction=moving?velocity.normalized:(sync.abilityVisuals!=null?sync.abilityVisuals.transform:sync.transform).forward;
            h.Direction.y=0;
            if(h.Direction.sqrMagnitude>.001f)h.Direction.Normalize();
            visibleHazards.Add(h);
        }
        foreach(int id in deadHazards)hazards.Remove(id);
        visibleHazards.Sort((a,b)=>(a.Motion.Position-playerMotion.Position).sqrMagnitude.CompareTo((b.Motion.Position-playerMotion.Position).sqrMagnitude));
    }
    private static void UpdatePath()
    {
        if(!Options.Targets || !(Options.WorldPath || Options.MapPath || Options.MinimapPath)){ClearPath();return;}
        var player=PlayerFinder.getLocalActorSync()?.transform;
        if(player==null)return;
        Target chosen=null;
        float distance=float.MaxValue;
        foreach(var t in targets)
        {
            if(!Live(t.Transform) || t.Completed || !Options.Styles[(int)t.Kind].Path)continue;
            float d=(t.Transform.position-player.position).sqrMagnitude;
            if(d>Options.Range*Options.Range)continue;
            if(selected!=0){if(t.Id==selected){chosen=t;break;}continue;}
            if(d<distance){chosen=t;distance=d;}
        }
        if(chosen==null){ClearPath();pathStatus=selected==0?"no target":"selected target unavailable";return;}
        if(pathTarget!=chosen.Id){ClearPath();pathTarget=chosen.Id;}
        if(Time.unscaledTime<nextPath)return;
        nextPath=Time.unscaledTime+.4f;
        nativePath??=new NavMeshPath();
        var goal=chosen.PathTransform!=null?chosen.PathTransform.position:chosen.Transform.position;
        if(!NavMesh.SamplePosition(player.position,out var start,3,NavMesh.AllAreas) || !NavMesh.SamplePosition(goal,out var end,5,NavMesh.AllAreas))
        {path=Array.Empty<NVec>();pathStatus="target outside walkable mesh";return;}
        if(!NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,nativePath) || nativePath.status==NavMeshPathStatus.PathInvalid)
        {path=Array.Empty<NVec>();pathStatus="no walkable route";return;}
        var corners=nativePath.corners;
        path=corners.Select(p=>N(p)).ToArray();
        pathGoal=goal;partialPath=nativePath.status==NavMeshPathStatus.PathPartial;
        pathStatus=$"{chosen.Name}: {path.Length} corners"+(partialPath?" (partial)":"");
    }
    private static void ClearPath()
    {
        path=Array.Empty<NVec>();pathHeadClear=false;pathTarget=0;nextPath=0;partialPath=false;pathStatus="no route";
        nativePath?.ClearCorners();
    }
}

[HarmonyPatch(typeof(AbilitySync),nameof(AbilitySync.OnEnable))]
internal static class AssistAbilityEnabledPatch
{private static void Postfix(AbilitySync __instance)=>WorldAssistRuntime.Guard(()=>WorldAssistRuntime.Track(__instance));}

// Sample after the game's own motion/animation work and before map projection.
[HarmonyPatch(typeof(IconManager),nameof(IconManager.LateUpdate))]
internal static class AssistMapFramePatch
{
    private static void Prefix()=>WorldAssistRuntime.Guard(WorldAssistRuntime.LateUpdate);
    private static void Postfix(IconManager __instance)=>WorldAssistRuntime.Guard(()=>WorldAssistRuntime.ReadMapProjection(__instance));
}
