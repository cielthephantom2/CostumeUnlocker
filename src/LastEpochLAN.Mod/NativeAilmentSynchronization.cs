using HarmonyLib;
using LastEpochLAN.Core;

namespace LastEpochLAN.Mod;

internal static class NativeAilmentSynchronization
{
    // Native receivers stay on the source PC; only presentation is shared.
    private static readonly Queue<int> guestPoll=new();
    private sealed record View(Il2Cpp.Actor Actor,Il2Cpp.AilmentVFXCreator Vfx,Dictionary<int,float> Counts);
    private static readonly Dictionary<IntPtr,View> views=new();
    private static readonly Dictionary<int,IntPtr> actorViews=new();
    [ThreadStatic] private static bool presenting;
    [ThreadStatic] internal static bool InSourceDot;
    internal static long SourceDotScopes;
    private static readonly bool visualExperiment=Environment.GetCommandLineArgs().Contains("--lelan-source-visuals-smoke");
    internal static long ReportsSent,ReportsRejected;
    private static readonly HashSet<int> knownAilments=new();
    internal static bool ValidateCatalogue(StatusVisualReport report)
    {
        foreach(var group in report.Groups)
        {
            if(knownAilments.Contains(group.Id)) continue;
            if(!Il2Cpp.AilmentList.TryGetAilment((Il2Cpp.AilmentID)group.Id,out var ailment) || ailment==null)
            {Error="Unknown status visual ID rejected: "+group.Id;return false;}
            knownAilments.Add(group.Id);
        }
        return true;
    }
    private sealed class Capture
    {
        internal Il2Cpp.AilmentReceiver Receiver;
        internal bool Dirty=true;
        internal float NextReport;
        internal AilmentCount[] Counts=Array.Empty<AilmentCount>();
    }
    private static readonly Dictionary<int,Capture> captures=new();
    private static readonly Dictionary<int,Dictionary<int,float>> presented=new();
    [ThreadStatic] private static int replayDepth;
    internal static long Applied;
    internal static string Error="";
    internal static void Dirty(Il2Cpp.AilmentReceiver receiver)
    {
        if(receiver==null || receiver.actor==null) return;
        int id=receiver.actor.GetInstanceID();
        if(!captures.TryGetValue(id,out var entry))
        {
            var world=WorldSynchronizer.Active;
            if(world?.CanCaptureEnemyVisuals(receiver.actor)!=true && world?.IsReplica(receiver.actor)!=true) return;
            captures[id]=entry=new Capture {Receiver=receiver};
            if(world?.Room?.IsHost==false) guestPoll.Enqueue(id);
        }
        entry.Dirty=true;
    }
    internal static AilmentCount[] Read(Il2Cpp.Actor actor)
    {
        int id=actor.GetInstanceID();
        if(!captures.TryGetValue(id,out var entry))
        {var receiver=actor.GetComponent<Il2Cpp.AilmentReceiver>();if(receiver==null) return Array.Empty<AilmentCount>();captures[id]=entry=new Capture {Receiver=receiver};}
        if(entry.Receiver==null) {captures.Remove(id);return Array.Empty<AilmentCount>();}
        if(!entry.Dirty) return entry.Counts;
        var counts=new List<AilmentCount>();
        if(entry.Receiver.ailmentStacks!=null)
            foreach(var pair in entry.Receiver.ailmentStacks)
                if(pair.Value>0) counts.Add(new((int)pair.Key,pair.Value));
        if(counts.Count>512) throw new InvalidOperationException("Native ailment catalogue exceeds synchronized status budget.");
        entry.Counts=counts.OrderBy(a=>a.Id).ToArray();entry.Dirty=false;return entry.Counts;
    }
    internal static void Present(Il2Cpp.Actor actor,AilmentCount[] counts)
    {
        var vfx=actor.GetActorSync()?.actorVisuals?.ailmentVFXCreator;
        if(vfx==null) return;
        int key=actor.GetInstanceID();
        if(counts.Length==0 && !presented.ContainsKey(key)) return;
        if(!presented.TryGetValue(key,out var previous)) presented[key]=previous=new();
        var next=counts.ToDictionary(a=>a.Id,a=>a.Count);
        if(actorViews.TryGetValue(key,out var oldView) && oldView!=vfx.Pointer) views.Remove(oldView);
        actorViews[key]=vfx.Pointer;views[vfx.Pointer]=new(actor,vfx,next);
        presenting=true;
        try
        {
        foreach(var id in previous.Keys.Where(id=>!next.ContainsKey(id)).ToArray())
        {vfx.setInstanceCount((Il2Cpp.AilmentID)id,0,false);previous.Remove(id);}
        foreach(var count in counts)
        {
            if(vfx.getInstanceCount((Il2Cpp.AilmentID)count.Id)==count.Count) {previous[count.Id]=count.Count;continue;}
            if(!Il2Cpp.AilmentList.TryGetAilment((Il2Cpp.AilmentID)count.Id,out var ailment) || ailment==null)
                throw new InvalidOperationException("Unknown native synchronized ailment ID: "+count.Id);
            // Absolute replacement, not additive: replay/baselines cannot multiply stacks.
            vfx.setInstanceCount((Il2Cpp.AilmentID)count.Id,count.Count,false);previous[count.Id]=count.Count;
        }
        }
        finally {presenting=false;}
    }
    internal static void OverrideVisual(Il2Cpp.AilmentVFXCreator vfx,Il2Cpp.AilmentID id,ref float instances,ref bool additive)
    {
        if(presenting || !views.TryGetValue(vfx.Pointer,out var view) || view.Vfx==null) return;
        instances=view.Counts.GetValueOrDefault((int)id);additive=false;
    }
    internal static void GuestTick(WorldSynchronizer world)
    {
        // This experiment was deferred by the user. Never enable it implicitly
        // in an offline tools build or present it as shared combat authority.
        if(!visualExperiment) return;
        if(world.Room?.IsHost!=false || world.GuestWorld==null) return;
        var local=Il2Cpp.PlayerFinder.getPlayerActor();
        if(local==null) return;
        int budget=Math.Min(64,guestPoll.Count);
        for(int n=0;n<budget;n++)
        {
            int id=guestPoll.Dequeue();
            if(!captures.TryGetValue(id,out var entry)) continue;
            var receiver=entry.Receiver;var actor=receiver?.actor;
            if(receiver==null || actor==null || !world.TryGetReplicaId(actor,out var enemy)) {captures.Remove(id);continue;}
            if(UnityEngine.Time.unscaledTime<entry.NextReport) {guestPoll.Enqueue(id);continue;}
            // Keep independent native expirations; compress only identical ID/deadline buckets.
            var groups=new Dictionary<(int Id,int Tenth),float>();
            if(actor.health?.currentHealth>0 && receiver.ailments!=null)
                foreach(var active in receiver.ailments)
                {
                    if(active?.ailment==null || !OwnedBy(active.creator,local) || active.stacksRepresented<=0) continue;
                    float remaining=active.ailment.duration<=0 ? 86400 : active.remainingDuration;
                    if(!float.IsFinite(remaining) || remaining<=0) continue;
                    var key=((int)active.ailment.id,(int)Math.Ceiling(Math.Min(86400,remaining)*10));
                    groups[key]=groups.GetValueOrDefault(key)+active.stacksRepresented;
                }
            var snapshot=groups.Select(p=>new StatusVisualGroup(p.Key.Id,p.Value,p.Key.Tenth/10f)).ToArray();
            if(!world.Room.PublishStatusVisuals(world.GuestWorld.Epoch,enemy,snapshot))
            {ReportsRejected++;Error="Status visual queue or native group budget exceeded; damage remains local.";guestPoll.Enqueue(id);continue;}
            ReportsSent++;entry.NextReport=UnityEngine.Time.unscaledTime+0.5f;
            if(snapshot.Length==0) captures.Remove(id);else guestPoll.Enqueue(id);
        }
    }
    private static bool OwnedBy(Il2Cpp.Actor creator,Il2Cpp.Actor local)
    {
        for(int depth=0;creator!=null && depth<8;depth++)
        {
            if(creator.Pointer==local.Pointer) return true;
            var parent=creator.GetCreator();
            if(parent==null || parent.Pointer==creator.Pointer) break;
            creator=parent;
        }
        return false;
    }
    internal static bool TryResolve(Il2Cpp.AilmentReceiver.ActiveAilment active,out ResolvedAilment resolved)
    {
        resolved=null;var ailment=active?.ailment;var stats=active?.damageStats;
        if(ailment==null || stats==null || stats.isHit || !ailment.dealsDamage || ailment.duration<=0 ||
            ailment.roots || ailment.isCurse || ailment.spreads || ailment.movementAilment || ailment.buffs?.Count>0 || ailment.procsAbilityWhenStacksReached || ailment.procsAbilityOnExpiration ||
            ailment.dealsDamageWhenHit || ailment.dealsDamageWhenAffectedHitsOthers || ailment.dealsAllDamageAtEnd ||
            active.isMovementAilment || active.getAppliesAilmentsWhenHit() || active.getProcsAbilitiesOnDeath() || active.abilityToProcOnExpiration!=null ||
            active.expiresOnCreatorDeathOrDestruction || active.stopsWhenHit || active.mutationSetStats?.Count>0 || active.overrideSpreads ||
            active.callbackOnExpiration!=null || stats.additionalLeech!=0 || stats.leechVsPlayers!=0 || stats.cullChance!=0 || stats.conditionalEffects?.Count>0 ||
            stats.trackerStatStorage?.addedValues?.Count>0 || stats.trackerStatStorage?.increasedValues?.Count>0 || stats.trackerStatStorage?.moreValues?.Count>0)
            return false;
        resolved=new ResolvedAilment {Id=(int)ailment.id,Ability=AbilityId(active.ability),OriginalAbility=AbilityId(active.originalAbility),
            PrimaryAbility=AbilityId(active.primaryAbility),UseType=(int)active.useType,Stacks=active.stacksRepresented,
            Duration=active.remainingDuration,IncreasedEffect=active.increasedEffect,IncreasedHealing=active.increasedHealing,
            Damage=Enumerable.Range(0,7).Select(i=>stats.damage[i]*active.moreDamage).ToArray(),
            Penetration=Enumerable.Range(0,7).Select(i=>stats.penetration!=null ? stats.getPenetration(i) : 0).ToArray(),Tags=(int)stats.getTags()};
        return resolved.IsValid();
    }
    private static int AbilityId(Il2Cpp.Ability ability)
        =>ability!=null && ability.TryGetAbilityID(out var id) ? (int)id : -1;
    private static Il2Cpp.Ability Ability(int id)=>id<0 ? null : Il2Cpp.Ability.getAbility((ushort)id);
    internal static void AppliedLocally(Il2Cpp.AilmentReceiver receiver,Il2Cpp.AilmentReceiver.ActiveAilment active)
    {
        Dirty(receiver);
        // Do not remove, duplicate or transfer this application. Its native DoT
        // ticks continue through the existing post-mitigation HP forwarding path.
    }
    internal static void ApplyNativeFixture(Il2Cpp.Actor target,Il2Cpp.Actor creator,ResolvedAilment application)
    {
        if(creator==null || target==null || !Il2Cpp.AilmentList.TryGetAilment((Il2Cpp.AilmentID)application.Id,out var ailment) || ailment==null)
            throw new InvalidOperationException("Resolved ailment has no native target/creator/catalogue entry.");
        var receiver=target.GetComponent<Il2Cpp.AilmentReceiver>() ?? throw new InvalidOperationException("Target has no native ailment receiver.");
        var stats=new Il2Cpp.DamageStats {damage=application.Damage,penetration=new Il2Cpp.DamageStats.Penetration(application.Penetration),
            isHit=false,otherTags=(Il2Cpp.AT)application.Tags};
        replayDepth++;
        try
        {
            var active=receiver.ApplyAilmentWithDamageStats(ailment,creator,Ability(application.Ability),Ability(application.OriginalAbility),
                Ability(application.PrimaryAbility),stats,creator.GetAlignment(),(Il2Cpp.UseType)application.UseType,application.Stacks,
                application.Duration/ailment.duration-1,application.IncreasedEffect,application.IncreasedHealing,false,0);
            if(active!=null) Applied++;
            Dirty(receiver);
        }
        finally {replayDepth--;}
    }
    internal static void Forget(Il2Cpp.Actor actor)
    {
        if(actor==null) return;
        NativePartyStatusMirror.Forget(actor);
        int id=actor.GetInstanceID();captures.Remove(id);presented.Remove(id);
        if(actorViews.Remove(id,out var pointer)) views.Remove(pointer);
    }
    internal static void Reset()
    {
        var oldViews=views.Values.ToArray();views.Clear();actorViews.Clear();
        foreach(var view in oldViews)
        {
            if(view.Actor==null || view.Vfx==null) continue;
            try
            {
                var receiver=view.Actor.GetComponent<Il2Cpp.AilmentReceiver>();
                foreach(var id in view.Counts.Keys)
                {
                    float count=receiver?.ailmentStacks!=null && receiver.ailmentStacks.ContainsKey((Il2Cpp.AilmentID)id) ? receiver.ailmentStacks[(Il2Cpp.AilmentID)id] : 0;
                    view.Vfx.setInstanceCount((Il2Cpp.AilmentID)id,count,false);
                }
            }
            catch(Exception ex) {Error="Status visual restore: "+ex.GetBaseException().Message;}
        }
        captures.Clear();presented.Clear();guestPoll.Clear();
    }

#if LELAN_QA
    internal static void NativeSmoke()
    {
        var root=new UnityEngine.GameObject("LAN inactive native ailment capture QA");root.SetActive(false);
        var template=UnityEngine.ScriptableObject.CreateInstance<Il2Cpp.Ailment>();
        try
        {
            var actor=root.AddComponent<Il2Cpp.Actor>();
            var receiver=root.AddComponent<Il2Cpp.AilmentReceiver>();receiver.actor=actor;
            receiver.ailmentStacks[(Il2Cpp.AilmentID)1]=3;
            var first=Read(actor);
            if(first.Length!=1 || first[0]!=new AilmentCount(1,3)) throw new InvalidOperationException("Native status catalogue count capture failed.");
            receiver.ailmentStacks[(Il2Cpp.AilmentID)1]=5;Dirty(receiver);
            if(Read(actor)[0].Count!=5 || first[0].Count!=3) throw new InvalidOperationException("Native status dirty cache or snapshot isolation failed.");
            receiver.ailmentStacks.Clear();Dirty(receiver);
            if(Read(actor).Length!=0) throw new InvalidOperationException("Native status removal failed.");
            template.id=(Il2Cpp.AilmentID)1;template.duration=4;template.dealsDamage=true;
            var active=new Il2Cpp.AilmentReceiver.ActiveAilment {ailment=template,remainingDuration=3,stacksRepresented=2,moreDamage=1.5f,
                damageStats=new Il2Cpp.DamageStats {damage=new float[]{10,0,0,0,0,0,0},isHit=false}};
            if(!TryResolve(active,out var resolved) || resolved.Damage[0]!=15 || resolved.Duration!=3 || resolved.Stacks!=2)
                throw new InvalidOperationException("Native resolved DoT potency/remaining duration/source group capture failed.");
            active.overrideSpreads=true;
            if(TryResolve(active,out _)) throw new InvalidOperationException("Unsupported native mutation was silently reduced to a base DoT.");
        }
        finally {Reset();UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(template);}
    }
#endif
}

[HarmonyPatch(typeof(Il2Cpp.AilmentReceiver),nameof(Il2Cpp.AilmentReceiver.ApplyAilmentWithDamageStats))]
internal static class LanAilmentResolvedPatch
{
    private static void Postfix(Il2Cpp.AilmentReceiver __instance,Il2Cpp.AilmentReceiver.ActiveAilment __result)
    {try {NativeAilmentSynchronization.AppliedLocally(__instance,__result);}catch(Exception ex){NativeAilmentSynchronization.Error=ex.GetBaseException().Message;}}
}
[HarmonyPatch]
internal static class LanAilmentDirtyPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()=>new[]{"changeStackCount","OnDeath"}
        .Select(name=>AccessTools.Method(typeof(Il2Cpp.AilmentReceiver),name));
    private static void Postfix(Il2Cpp.AilmentReceiver __instance)=>NativeAilmentSynchronization.Dirty(__instance);
}

[HarmonyPatch(typeof(Il2Cpp.AilmentVFXCreator),nameof(Il2Cpp.AilmentVFXCreator.setInstanceCount))]
internal static class LanSharedAilmentVisualPatch
{
    private static void Prefix(Il2Cpp.AilmentVFXCreator __instance,Il2Cpp.AilmentID ailment,ref float instances,ref bool addToExistingCount)
        =>NativeAilmentSynchronization.OverrideVisual(__instance,ailment,ref instances,ref addToExistingCount);
}

[HarmonyPatch(typeof(Il2Cpp.AilmentReceiver),nameof(Il2Cpp.AilmentReceiver.DealDamageForAilment))]
internal static class LanSourceDotScopePatch
{
    private static void Prefix(Il2Cpp.AilmentReceiver __instance,out bool __state)
    {
        __state=NativeAilmentSynchronization.InSourceDot;
        if(WorldSynchronizer.Active?.IsReplica(__instance.actor)==true)
        {NativeAilmentSynchronization.InSourceDot=true;NativeAilmentSynchronization.SourceDotScopes++;}
    }
    private static Exception Finalizer(Exception __exception,bool __state)
    {NativeAilmentSynchronization.InSourceDot=__state;return __exception;}
}
